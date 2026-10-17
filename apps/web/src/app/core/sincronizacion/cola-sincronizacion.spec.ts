import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Api } from '../../api/api';
import { CambioCelda } from '../../api/models/cambio-celda';
import { MatrizCalificacionesDto } from '../../api/models/matriz-calificaciones-dto';
import { SincronizarCambios } from '../../api/models/sincronizar-cambios';
import { ALMACEN_LOCAL, AlmacenLocal, CambioEnCola, MatrizGuardada } from './almacen-local';
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
  async contar() {
    return this.cola.length;
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
  }
}

/** El servidor: aplica cada UUID una sola vez, como `cambio_sincronizado`. */
class ServidorFalso {
  readonly aplicados = new Set<string>();
  readonly celdas = new Map<string, number>();
  sinRed = false;

  async invoke(_fn: unknown, params: { body: SincronizarCambios }) {
    if (this.sinRed) {
      throw new HttpErrorResponse({ status: 0 });
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

  it('al cerrar sesión no deja calificaciones en el dispositivo', async () => {
    servidor.sinRed = true;
    await cola.guardar(matriz(), [cambio(1)]);
    await cola.borrarTodo();
    expect(almacen.cola).toEqual([]);
    expect(almacen.matrices.size).toBe(0);
    expect(cola.pendientes()).toBe(0);
  });
});
