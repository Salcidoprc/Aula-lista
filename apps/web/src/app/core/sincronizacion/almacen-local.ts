import { InjectionToken } from '@angular/core';
import Dexie, { Table } from 'dexie';
import { CambioCelda } from '../../api/models/cambio-celda';
import { MatrizCalificacionesDto } from '../../api/models/matriz-calificaciones-dto';

/** Un cambio de la cola de salida con su UUID de idempotencia (RF-037, 02-arquitectura). */
export interface CambioEnCola {
  readonly id: string;
  readonly periodoId: string;
  /** Milisegundos desde 1970, para enviar en orden y avisar a partir del día 3 (SPEC-03, casos borde). */
  readonly creado: number;
  readonly cambio: CambioCelda;
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
  contar(): Promise<number>;
  masAntiguo(): Promise<number | null>;
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

  constructor() {
    super('aula-lista');
    this.version(1).stores({
      matrices: 'periodoId',
      cola: 'id, creado, periodoId',
      datos: 'clave',
    });
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

  contar(): Promise<number> {
    return this.db.cola.count();
  }

  async masAntiguo(): Promise<number | null> {
    return (await this.db.cola.orderBy('creado').first())?.creado ?? null;
  }

  async leerDato<T>(clave: string): Promise<T | undefined> {
    return (await this.db.datos.get(clave))?.valor as T | undefined;
  }

  async guardarDato(clave: string, valor: unknown): Promise<void> {
    await this.db.datos.put({ clave, valor });
  }

  async borrarTodo(): Promise<void> {
    await this.db.transaction('rw', this.db.matrices, this.db.cola, this.db.datos, async () => {
      await this.db.matrices.clear();
      await this.db.cola.clear();
      await this.db.datos.clear();
    });
  }
}

export const ALMACEN_LOCAL = new InjectionToken<AlmacenLocal>('AlmacenLocal', {
  providedIn: 'root',
  factory: () => new AlmacenDexie(),
});
