import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Api } from '../../api/api';
import { CambioCelda } from '../../api/models/cambio-celda';
import { CrearNota } from '../../api/models/crear-nota';
import { GuardarAsistencia } from '../../api/models/guardar-asistencia';
import { MatrizCalificacionesDto } from '../../api/models/matriz-calificaciones-dto';
import { SincronizarCambios } from '../../api/models/sincronizar-cambios';
import {
  ALMACEN_LOCAL,
  AlmacenLocal,
  AsistenciaEnCola,
  CambioEnCola,
  MatrizGuardada,
  NotaEnCola,
} from './almacen-local';
import { ColaSincronizacion } from './cola-sincronizacion';

/** IndexedDB en memoria con el mismo contrato. */
class AlmacenMemoria implements AlmacenLocal {
  readonly matrices = new Map<string, MatrizGuardada>();
  cola: CambioEnCola[] = [];

  async leerMatriz(id: string) {
    return this.matrices.get(id);
  }
  async guardarMatriz(matriz: MatrizCalificacionesDto) {
    this.matrices.set(matriz.periodoId, {
      periodoId: matriz.periodoId,
      guardadaEn: Date.now(),
      matriz,
    });
  }
  async guardarYEncolar(matriz: MatrizCalificacionesDto, cambios: readonly CambioEnCola[]) {
    await this.guardarMatriz(matriz);
    this.cola.push(...cambios);
  }
  async pendientes(limite?: number) {
    // Como IndexedDB: el límite de getAll es un entero sin signo de 32 bits.
    if (limite !== undefined && limite > 0xffffffff) {
      throw new TypeError('count fuera de rango');
    }
    return [...this.cola].sort((a, b) => a.creado - b.creado).slice(0, limite);
  }
  async pendientesDe(id: string) {
    return this.cola.filter((c) => c.periodoId === id);
  }
  async quitar(ids: readonly string[]) {
    this.cola = this.cola.filter((c) => !ids.includes(c.id));
  }
  notas: NotaEnCola[] = [];
  async contar() {
    return this.cola.length + this.notas.length + this.asistencias.length;
  }
  async encolarNota(nota: NotaEnCola) {
    this.notas = [...this.notas.filter((n) => n.id !== nota.id), nota];
  }
  async notasPendientes(alumnoId?: string) {
    return this.notas.filter((n) => alumnoId === undefined || n.alumnoId === alumnoId);
  }
  async quitarNota(id: string) {
    this.notas = this.notas.filter((n) => n.id !== id);
  }
  asistencias: AsistenciaEnCola[] = [];
  async encolarAsistencia(a: AsistenciaEnCola) {
    this.asistencias = [...this.asistencias.filter((x) => x.id !== a.id), a];
  }
  async asistenciasPendientes(grupoId?: string) {
    return this.asistencias.filter((a) => grupoId === undefined || a.grupoId === grupoId);
  }
  async quitarAsistencia(id: string) {
    this.asistencias = this.asistencias.filter((a) => a.id !== id);
  }
  async masAntiguo() {
    return this.cola[0]?.creado ?? null;
  }
  readonly datos = new Map<string, unknown>();
  async leerDato<T>(clave: string) {
    return this.datos.get(clave) as T | undefined;
  }
  async guardarDato(clave: string, valor: unknown) {
    this.datos.set(clave, valor);
  }
  async borrarTodo() {
    this.datos.clear();
    this.matrices.clear();
    this.cola = [];
    this.notas = [];
    this.asistencias = [];
  }
}

/** El servidor: aplica cada UUID una sola vez, como `cambio_sincronizado`. */
class ServidorFalso {
  readonly aplicados = new Set<string>();
  readonly celdas = new Map<string, number>();
  sinRed = false;

  readonly notas = new Map<string, number>();
  /** La toma de cada fecha por alumno y las claves ya aplicadas, como `cambio_sincronizado`. */
  readonly tomas = new Map<string, Map<string, string>>();
  readonly claves = new Set<string>();

  async invoke(
    _fn: unknown,
    params: {
      body: SincronizarCambios | CrearNota | GuardarAsistencia;
      fecha?: string;
      'Idempotency-Key'?: string;
    },
  ) {
    if (this.sinRed) {
      throw new HttpErrorResponse({ status: 0 });
    }
    if ('alumnos' in params.body) {
      // PUT /grupos/{id}/asistencias/{fecha}: la toma nace con todos presentes y una clave repetida no se aplica.
      const toma =
        this.tomas.get(params.fecha!) ??
        new Map([
          ['al1', 'presente'],
          ['al2', 'presente'],
        ]);
      if (!this.claves.has(params['Idempotency-Key']!)) {
        this.claves.add(params['Idempotency-Key']!);
        for (const a of params.body.alumnos) {
          toma.set(a.alumnoId, a.estado);
        }
      }
      this.tomas.set(params.fecha!, toma);
      const alumnos = [...toma].map(([alumnoId, estado]) => ({ alumnoId, estado, version: 1 }));
      return { toma: { fecha: params.fecha, alumnos }, omitidos: [] };
    }
    if (!('cambios' in params.body)) {
      // POST /alumnos/{id}/notas: la misma nota con el mismo id no se duplica.
      this.notas.set(params.body.id, (this.notas.get(params.body.id) ?? 0) + 1);
      return { ...params.body };
    }
    const aplicadas = params.body.cambios.map((c) => {
      const llave = `${c.actividadId}:${c.alumnoId}`;
      if (!this.aplicados.has(c.id!)) {
        this.aplicados.add(c.id!);
        this.celdas.set(llave, (this.celdas.get(llave) ?? 0) + 1);
      }
      return { ...c, origen: 'individual', version: this.celdas.get(llave)!, criterios: [] };
    });
    return { aplicadas, rechazadas: [] };
  }
}

const matriz = (): MatrizCalificacionesDto =>
  ({ periodoId: 'p1', celdas: [] }) as unknown as MatrizCalificacionesDto;

const cambio = (alumno: number): CambioCelda => ({
  id: null,
  actividadId: 'a1',
  alumnoId: `al${alumno}`,
  valor: 5 + (alumno % 6),
  marca: 'ninguna',
  criterios: null,
  version: 0,
});

describe('Cola de sincronización sin conexión (RF-037)', () => {
  let almacen: AlmacenMemoria;
  let servidor: ServidorFalso;
  let cola: ColaSincronizacion;

  beforeEach(() => {
    almacen = new AlmacenMemoria();
    servidor = new ServidorFalso();
    TestBed.configureTestingModule({
      providers: [
        { provide: ALMACEN_LOCAL, useValue: almacen },
        { provide: Api, useValue: servidor },
      ],
    });
    cola = TestBed.inject(ColaSincronizacion);
  });

  it('SPEC-03: 30 capturas sin conexión llegan al reconectar, sin duplicados aunque se reenvíen', async () => {
    servidor.sinRed = true;
    for (let i = 0; i < 30; i++) {
      await cola.guardar(matriz(), [cambio(i)]);
    }
    await cola.enviar();
    expect(cola.pendientes()).toBe(30);
    expect(cola.estado()).toBe('sin_conexion');

    // La app se cierra y se reabre: la cola sigue en el dispositivo y se reenvía entera dos veces.
    const copia = [...almacen.cola];
    servidor.sinRed = false;
    window.dispatchEvent(new Event('online'));
    await cola.enviar();
    almacen.cola = copia;
    await cola.enviar();

    expect(cola.pendientes()).toBe(0);
    expect(cola.estado()).toBe('guardado');
    expect(servidor.celdas.size).toBe(30);
    expect([...servidor.celdas.values()].every((v) => v === 1)).toBe(true);
  });

  it('guarda en el dispositivo antes de enviar y deja la versión del servidor', async () => {
    await cola.guardar(matriz(), [cambio(1)]);
    await cola.enviar();
    const guardada = await almacen.leerMatriz('p1');
    expect(guardada?.matriz.celdas).toEqual([
      expect.objectContaining({ alumnoId: 'al1', version: 1 }),
    ]);
  });

  it('con dos cambios de la misma celda en la cola, confirma el más nuevo', async () => {
    servidor.sinRed = true;
    await cola.guardar(matriz(), [{ ...cambio(1), valor: 7 }]);
    await cola.guardar(matriz(), [{ ...cambio(1), valor: 9 }]);
    const confirmados: unknown[] = [];
    cola.alConfirmar((r) => confirmados.push(...[...r.vigentes.values()].map((c) => c?.valor)));
    servidor.sinRed = false;
    window.dispatchEvent(new Event('online'));
    await cola.enviar();
    expect(confirmados).toEqual([9]);
  });

  it('una confirmación no pisa una celda que se está capturando en ese momento', async () => {
    servidor.sinRed = true;
    await cola.guardar(matriz(), [{ ...cambio(1), valor: 6 }]);
    await cola.enviar();
    servidor.sinRed = false;
    // La siguiente captura de la misma celda tarda en escribirse: la confirmación llega antes.
    let soltar!: () => void;
    const original = almacen.guardarYEncolar.bind(almacen);
    almacen.guardarYEncolar = async (m, c) => {
      await new Promise<void>((r) => (soltar = r));
      return original(m, c);
    };
    const escritura = cola.guardar(matriz(), [{ ...cambio(1), valor: 9 }]);
    const confirmados: unknown[] = [];
    cola.alConfirmar((r) => confirmados.push(...[...r.vigentes.values()].map((c) => c?.valor)));
    window.dispatchEvent(new Event('online'));
    await vi.waitFor(() => expect(almacen.cola).toHaveLength(0));
    expect(servidor.aplicados.size).toBe(1);
    expect(confirmados).toEqual([]);
    soltar();
    await escritura;
    await cola.enviar();
    await vi.waitFor(() => expect(confirmados).toEqual([9]));
  });

  it('SPEC-03: una nota escrita sin conexión se guarda en el dispositivo y llega al volver la red', async () => {
    servidor.sinRed = true;
    const nota: CrearNota = {
      id: 'n1',
      periodoId: 'p1',
      texto: 'Participó en la lluvia de ideas.',
      etiqueta: 'fortaleza',
      fecha: '2026-10-17',
    };
    await cola.guardarNota('al1', nota);
    await cola.enviar();
    expect(cola.pendientes()).toBe(1);
    expect(await cola.notasPendientes('al1')).toEqual([expect.objectContaining({ id: 'n1' })]);

    const enviadas: string[] = [];
    cola.alEnviarNota((alumno) => enviadas.push(alumno));
    servidor.sinRed = false;
    window.dispatchEvent(new Event('online'));
    await cola.enviar();

    expect(cola.pendientes()).toBe(0);
    expect(servidor.notas.get('n1')).toBe(1);
    expect(enviadas).toEqual(['al1']);
  });

  it('SPEC-10: pasar lista sin conexión llega al volver la red, sin duplicados aunque se reenvíe', async () => {
    servidor.sinRed = true;
    await cola.guardarAsistencia('g1', '2026-10-19', []); // Todos presentes
    await cola.guardarAsistencia('g1', '2026-10-19', [{ alumnoId: 'al2', estado: 'falta' }]);
    await cola.enviar();
    expect(cola.pendientes()).toBe(2);
    expect(await cola.asistenciasPendientes('g1')).toHaveLength(2);

    // La app se cierra con la cola en el dispositivo; al volver la red se envía y luego se reenvía entera.
    const copia = [...almacen.asistencias];
    const enviadas: (string | null)[] = [];
    cola.alEnviarAsistencia((e) => enviadas.push(e.codigo));
    servidor.sinRed = false;
    window.dispatchEvent(new Event('online'));
    await cola.enviar();
    // Otro dispositivo justifica la falta; después llega el reenvío viejo de este.
    servidor.tomas.get('2026-10-19')!.set('al2', 'justificada');
    almacen.asistencias = copia;
    await cola.enviar();

    expect(cola.pendientes()).toBe(0);
    expect(enviadas).toEqual([null, null, null, null]);
    expect(servidor.tomas.size).toBe(1);
    expect(servidor.claves.size).toBe(2);
    expect(servidor.tomas.get('2026-10-19')!.get('al2')).toBe('justificada');
  });

  it('al cerrar sesión no deja calificaciones en el dispositivo', async () => {
    servidor.sinRed = true;
    await cola.guardar(matriz(), [cambio(1)]);
    await cola.borrarTodo();
    expect(almacen.cola).toEqual([]);
    expect(almacen.matrices.size).toBe(0);
    expect(cola.pendientes()).toBe(0);
  });
});
