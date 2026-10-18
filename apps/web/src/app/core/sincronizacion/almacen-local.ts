import { InjectionToken } from '@angular/core';
import Dexie, { Table } from 'dexie';
import { CambioCelda } from '../../api/models/cambio-celda';
import { CrearNota } from '../../api/models/crear-nota';
import { EstadoAlumno } from '../../api/models/estado-alumno';
import { MatrizCalificacionesDto } from '../../api/models/matriz-calificaciones-dto';

/** Un cambio de la cola de salida con su UUID de idempotencia (RF-037, 02-arquitectura). */
export interface CambioEnCola {
  readonly id: string;
  readonly periodoId: string;
  /** Milisegundos desde 1970, para enviar en orden y avisar a partir del día 3 (SPEC-03, casos borde). */
  readonly creado: number;
  readonly cambio: CambioCelda;
}

/** Una nota escrita en el dispositivo que todavía no llega al servidor. Su id es el de la nota. */
export interface NotaEnCola {
  readonly id: string;
  readonly alumnoId: string;
  readonly creado: number;
  readonly nota: CrearNota;
}

/**
 * Estados de una toma de asistencia por enviar (SPEC-10). Su id va como `Idempotency-Key`. Sin alumnos, solo crea
 * la toma con todos presentes (RN-101).
 */
export interface AsistenciaEnCola {
  readonly id: string;
  readonly grupoId: string;
  /** "2026-10-17". */
  readonly fecha: string;
  readonly creado: number;
  readonly alumnos: readonly EstadoAlumno[];
}

/** La matriz del periodo como se vio por última vez, con los cambios locales ya encima. */
export interface MatrizGuardada {
  readonly periodoId: string;
  readonly guardadaEn: number;
  readonly matriz: MatrizCalificacionesDto;
}

/**
 * Lo que la app guarda en el dispositivo (reglas/02: solo `core/sincronizacion` toca IndexedDB). Son datos de
 * menores: se borran al cerrar sesión.
 */
export interface AlmacenLocal {
  leerMatriz(periodoId: string): Promise<MatrizGuardada | undefined>;
  guardarMatriz(matriz: MatrizCalificacionesDto): Promise<void>;
  /** Guarda la matriz y encola sus cambios juntos: si uno falla, no queda ninguno. */
  guardarYEncolar(matriz: MatrizCalificacionesDto, cambios: readonly CambioEnCola[]): Promise<void>;
  /** Los más antiguos primero; sin límite, todos. */
  pendientes(limite?: number): Promise<CambioEnCola[]>;
  pendientesDe(periodoId: string): Promise<CambioEnCola[]>;
  quitar(ids: readonly string[]): Promise<void>;
  /** Calificaciones, notas y asistencias por enviar. */
  contar(): Promise<number>;
  masAntiguo(): Promise<number | null>;
  encolarNota(nota: NotaEnCola): Promise<void>;
  /** Las más antiguas primero; con alumno, solo las suyas. */
  notasPendientes(alumnoId?: string): Promise<NotaEnCola[]>;
  quitarNota(id: string): Promise<void>;
  encolarAsistencia(asistencia: AsistenciaEnCola): Promise<void>;
  /** Las más antiguas primero; con grupo, solo las suyas. */
  asistenciasPendientes(grupoId?: string): Promise<AsistenciaEnCola[]>;
  quitarAsistencia(id: string): Promise<void>;
  /** Lo demás que la captura necesita sin red: el grupo y sus periodos. */
  leerDato<T>(clave: string): Promise<T | undefined>;
  guardarDato(clave: string, valor: unknown): Promise<void>;
  borrarTodo(): Promise<void>;
}

interface Dato {
  readonly clave: string;
  readonly valor: unknown;
}

class BaseLocal extends Dexie {
  matrices!: Table<MatrizGuardada, string>;
  cola!: Table<CambioEnCola, string>;
  datos!: Table<Dato, string>;
  notas!: Table<NotaEnCola, string>;
  asistencias!: Table<AsistenciaEnCola, string>;

  constructor() {
    super('aula-lista');
    this.version(1).stores({
      matrices: 'periodoId',
      cola: 'id, creado, periodoId',
      datos: 'clave',
    });
    this.version(2).stores({ notas: 'id, creado, alumnoId' });
    this.version(3).stores({ asistencias: 'id, creado, grupoId' });
  }
}

/** IndexedDB con Dexie. Se abre al primer uso. */
export class AlmacenDexie implements AlmacenLocal {
  private base: BaseLocal | null = null;

  private get db(): BaseLocal {
    return (this.base ??= new BaseLocal());
  }

  leerMatriz(periodoId: string): Promise<MatrizGuardada | undefined> {
    return this.db.matrices.get(periodoId);
  }

  async guardarMatriz(matriz: MatrizCalificacionesDto): Promise<void> {
    await this.db.matrices.put({ periodoId: matriz.periodoId, guardadaEn: Date.now(), matriz });
  }

  async guardarYEncolar(
    matriz: MatrizCalificacionesDto,
    cambios: readonly CambioEnCola[],
  ): Promise<void> {
    await this.db.transaction('rw', this.db.matrices, this.db.cola, async () => {
      await this.db.matrices.put({ periodoId: matriz.periodoId, guardadaEn: Date.now(), matriz });
      await this.db.cola.bulkAdd([...cambios]);
    });
  }

  pendientes(limite?: number): Promise<CambioEnCola[]> {
    const orden = this.db.cola.orderBy('creado');
    return (limite === undefined ? orden : orden.limit(limite)).toArray();
  }

  async pendientesDe(periodoId: string): Promise<CambioEnCola[]> {
    return (await this.db.cola.where('periodoId').equals(periodoId).toArray()).sort(
      (a, b) => a.creado - b.creado,
    );
  }

  async quitar(ids: readonly string[]): Promise<void> {
    await this.db.cola.bulkDelete([...ids]);
  }

  async contar(): Promise<number> {
    const [celdas, notas, asistencias] = await Promise.all([
      this.db.cola.count(),
      this.db.notas.count(),
      this.db.asistencias.count(),
    ]);
    return celdas + notas + asistencias;
  }

  async masAntiguo(): Promise<number | null> {
    const [celda, nota, asistencia] = await Promise.all([
      this.db.cola.orderBy('creado').first(),
      this.db.notas.orderBy('creado').first(),
      this.db.asistencias.orderBy('creado').first(),
    ]);
    const fechas = [celda?.creado, nota?.creado, asistencia?.creado].filter(
      (f): f is number => f !== undefined,
    );
    return fechas.length ? Math.min(...fechas) : null;
  }

  async encolarNota(nota: NotaEnCola): Promise<void> {
    await this.db.notas.put(nota);
  }

  async notasPendientes(alumnoId?: string): Promise<NotaEnCola[]> {
    const lista =
      alumnoId === undefined
        ? await this.db.notas.toArray()
        : await this.db.notas.where('alumnoId').equals(alumnoId).toArray();
    return lista.sort((a, b) => a.creado - b.creado);
  }

  async quitarNota(id: string): Promise<void> {
    await this.db.notas.delete(id);
  }

  async encolarAsistencia(asistencia: AsistenciaEnCola): Promise<void> {
    await this.db.asistencias.put(asistencia);
  }

  async asistenciasPendientes(grupoId?: string): Promise<AsistenciaEnCola[]> {
    const lista =
      grupoId === undefined
        ? await this.db.asistencias.toArray()
        : await this.db.asistencias.where('grupoId').equals(grupoId).toArray();
    return lista.sort((a, b) => a.creado - b.creado);
  }

  async quitarAsistencia(id: string): Promise<void> {
    await this.db.asistencias.delete(id);
  }

  async leerDato<T>(clave: string): Promise<T | undefined> {
    return (await this.db.datos.get(clave))?.valor as T | undefined;
  }

  async guardarDato(clave: string, valor: unknown): Promise<void> {
    await this.db.datos.put({ clave, valor });
  }

  async borrarTodo(): Promise<void> {
    await this.db.transaction(
      'rw',
      [this.db.matrices, this.db.cola, this.db.datos, this.db.notas, this.db.asistencias],
      async () => {
        await this.db.matrices.clear();
        await this.db.cola.clear();
        await this.db.datos.clear();
        await this.db.notas.clear();
        await this.db.asistencias.clear();
      },
    );
  }
}

export const ALMACEN_LOCAL = new InjectionToken<AlmacenLocal>('AlmacenLocal', {
  providedIn: 'root',
  factory: () => new AlmacenDexie(),
});
