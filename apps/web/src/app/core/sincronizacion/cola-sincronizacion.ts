import { HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { Api } from '../../api/api';
import { sincronizar } from '../../api/fn/calificaciones/sincronizar';
import { CambioCelda } from '../../api/models/cambio-celda';
import { CeldaDto } from '../../api/models/celda-dto';
import { CeldaRechazada } from '../../api/models/celda-rechazada';
import { MatrizCalificacionesDto } from '../../api/models/matriz-calificaciones-dto';
import { ALMACEN_LOCAL, CambioEnCola } from './almacen-local';
import { conCambios, conCeldas, llaveCelda } from './celdas';

/** 02-arquitectura: lotes de hasta 100 cambios. */
const LOTE = 100;
const REINTENTO_MS = 15_000;
const REINTENTO_MAXIMO_MS = 5 * 60_000;
const DIA_MS = 24 * 60 * 60_000;

export type EstadoSincronizacion = 'guardado' | 'pendiente' | 'enviando' | 'sin_conexion';

/** Lo que respondió el servidor a un lote, ya sin las celdas que tienen otro cambio detrás en la cola. */
export interface ResultadoLote {
  /** Celdas vigentes por llave; `null` si la celda nunca llegó a existir en el servidor. */
  readonly vigentes: ReadonlyMap<string, CeldaDto | null>;
  readonly rechazadas: readonly CeldaRechazada[];
}

/**
 * Cola de salida sin conexión (RF-037, reglas/02). Cada captura se guarda primero en el dispositivo junto con
 * su cambio y un UUID; la cola los manda en orden a `POST /sincronizacion` cuando hay red. El servidor ignora
 * los UUID que ya aplicó, así que reenviar nunca duplica. Una celda rechazada no detiene la cola.
 */
@Injectable({ providedIn: 'root' })
export class ColaSincronizacion {
  private readonly api = inject(Api);
  private readonly almacen = inject(ALMACEN_LOCAL);
  private readonly oyentes = new Set<(resultado: ResultadoLote) => void>();
  private envio: Promise<void> | null = null;
  private otraVez = false;
  private reintento: ReturnType<typeof setTimeout> | null = null;
  private espera = REINTENTO_MS;

  private readonly cuenta = signal(0);
  private turnoConteo = 0;
  private readonly conexion = signal(typeof navigator === 'undefined' ? true : navigator.onLine);
  private readonly mandando = signal(false);
  private readonly antiguo = signal<number | null>(null);

  readonly pendientes = this.cuenta.asReadonly();
  readonly estado = computed<EstadoSincronizacion>(() => {
    if (this.mandando()) {
      return 'enviando';
    }
    if (!this.conexion()) {
      return 'sin_conexion';
    }
    return this.cuenta() > 0 ? 'pendiente' : 'guardado';
  });
  /** Días que lleva el cambio más viejo sin enviarse; la app avisa desde el tercero (SPEC-03). */
  readonly diasSinEnviar = computed(() => {
    const desde = this.antiguo();
    return desde === null ? 0 : Math.floor((Date.now() - desde) / DIA_MS);
  });

  constructor() {
    if (typeof window !== 'undefined') {
      const enLinea = (): void => {
        this.conexion.set(true);
        void this.enviar();
      };
      const sinLinea = (): void => this.conexion.set(false);
      window.addEventListener('online', enLinea);
      window.addEventListener('offline', sinLinea);
      inject(DestroyRef).onDestroy(() => {
        window.removeEventListener('online', enLinea);
        window.removeEventListener('offline', sinLinea);
        this.cancelarReintento();
      });
    }
    void this.iniciar();
  }

  /** Avisa de cada lote confirmado. Devuelve cómo dejar de escuchar. */
  alConfirmar(oyente: (resultado: ResultadoLote) => void): () => void {
    this.oyentes.add(oyente);
    return () => this.oyentes.delete(oyente);
  }

  /** Guarda la matriz con la captura ya aplicada y encola sus cambios. Después intenta enviarlos. */
  async guardar(matriz: MatrizCalificacionesDto, cambios: readonly CambioCelda[]): Promise<void> {
    const ahora = Date.now();
    const enCola: CambioEnCola[] = cambios.map((cambio, i) => {
      const id = crypto.randomUUID();
      return {
        id,
        periodoId: matriz.periodoId,
        creado: ahora + i / 1000,
        cambio: { ...cambio, id },
      };
    });
    await this.almacen.guardarYEncolar(matriz, enCola);
    await this.contar();
    void this.enviar();
  }

  /** La matriz del servidor con los cambios de este dispositivo que aún no se envían. */
  async conPendientes(matriz: MatrizCalificacionesDto): Promise<MatrizCalificacionesDto> {
    const pendientes = await this.almacen.pendientesDe(matriz.periodoId);
    const completa = conCambios(
      matriz,
      pendientes.map((p) => p.cambio),
    );
    await this.almacen.guardarMatriz(completa);
    return completa;
  }

  async matrizGuardada(periodoId: string): Promise<MatrizCalificacionesDto | null> {
    return (await this.almacen.leerMatriz(periodoId))?.matriz ?? null;
  }

  /**
   * Lee de la red y guarda una copia; sin conexión, devuelve la copia. Así la captura abre aunque no haya señal.
   */
  async conCopia<T>(clave: string, leer: () => Promise<T>): Promise<T> {
    try {
      const valor = await leer();
      void this.almacen.guardarDato(clave, valor).catch(() => undefined);
      return valor;
    } catch (error) {
      const copia =
        error instanceof HttpErrorResponse && error.status === 0
          ? await this.almacen.leerDato<T>(clave).catch(() => undefined)
          : undefined;
      if (copia === undefined) {
        throw error;
      }
      return copia;
    }
  }

  /**
   * Envía todo lo pendiente, de 100 en 100. Si ya hay un envío en curso, se repite al terminar: lo que se
   * encoló o la red que volvió mientras tanto no se quedan esperando al siguiente intento.
   */
  enviar(): Promise<void> {
    if (this.envio) {
      this.otraVez = true;
      return this.envio;
    }
    this.envio = (async () => {
      do {
        this.otraVez = false;
        await this.enviarTodo();
      } while (this.otraVez);
    })().finally(() => {
      this.envio = null;
      this.mandando.set(false);
      // Lo que se encoló entre la última vuelta y este cierre tampoco se queda esperando.
      if (this.otraVez) {
        void this.enviar();
      }
    });
    return this.envio;
  }

  /** Al cerrar sesión: los datos de los alumnos no se quedan en el dispositivo (RNF de privacidad). */
  async borrarTodo(): Promise<void> {
    this.cancelarReintento();
    await this.almacen.borrarTodo();
    await this.contar();
  }

  private async iniciar(): Promise<void> {
    try {
      await this.contar();
      await this.enviar();
    } catch {
      // Sin IndexedDB (modo privado antiguo) la captura sigue en línea; el indicador queda en "Guardado".
    }
  }

  private async enviarTodo(): Promise<void> {
    this.cancelarReintento();
    try {
      await this.enviarLotes();
    } finally {
      await this.contar(); // pase lo que pase, el indicador refleja lo que queda en la cola
    }
  }

  private async enviarLotes(): Promise<void> {
    for (;;) {
      const lote = await this.almacen.pendientes(LOTE);
      if (lote.length === 0 || !this.conexion()) {
        return;
      }
      this.mandando.set(true);
      try {
        const respuesta = await this.api.invoke(sincronizar, {
          body: { cambios: lote.map((c) => c.cambio) },
        });
        await this.confirmar(lote, respuesta.aplicadas, respuesta.rechazadas);
        this.espera = REINTENTO_MS;
      } catch (error) {
        if (error instanceof HttpErrorResponse && error.status === 400) {
          // El lote no pasa la validación (no debería ocurrir): se descarta para no atorar la cola.
          await this.confirmar(
            lote,
            [],
            lote.map((c) => rechazo(c, 'validacion_fallida')),
          );
          continue;
        }
        if (error instanceof HttpErrorResponse && error.status === 0) {
          this.conexion.set(false);
        }
        if (!(error instanceof HttpErrorResponse && error.status === 401)) {
          this.programarReintento();
        }
        return;
      }
    }
  }

  private async confirmar(
    lote: readonly CambioEnCola[],
    aplicadas: readonly CeldaDto[],
    rechazadas: readonly CeldaRechazada[],
  ): Promise<void> {
    await this.almacen.quitar(lote.map((c) => c.id));
    const siguen = new Set((await this.almacen.pendientes()).map((c) => llaveCelda(c.cambio)));
    const vigentes = new Map<string, CeldaDto | null>();
    for (const celda of aplicadas) {
      vigentes.set(llaveCelda(celda), celda);
    }
    for (const r of rechazadas.filter((r) => r.codigo !== 'validacion_fallida')) {
      vigentes.set(llaveCelda(r), r.vigente ?? null);
    }
    for (const llave of siguen) {
      vigentes.delete(llave); // otro cambio de esa celda viene detrás: manda el más nuevo
    }

    for (const periodoId of new Set(lote.map((c) => c.periodoId))) {
      const guardada = await this.almacen.leerMatriz(periodoId);
      if (guardada) {
        await this.almacen.guardarMatriz(conCeldas(guardada.matriz, vigentes));
      }
    }
    await this.contar();
    const resultado: ResultadoLote = { vigentes, rechazadas };
    for (const oyente of this.oyentes) {
      oyente(resultado);
    }
  }

  /** Cada cambio a la cola vuelve a contar; si varias cuentas se cruzan, solo vale la última que empezó. */
  private async contar(): Promise<void> {
    const turno = ++this.turnoConteo;
    const [cuenta, antiguo] = await Promise.all([this.almacen.contar(), this.almacen.masAntiguo()]);
    if (turno === this.turnoConteo) {
      this.cuenta.set(cuenta);
      this.antiguo.set(antiguo);
    }
  }

  private programarReintento(): void {
    this.cancelarReintento();
    this.reintento = setTimeout(() => void this.enviar(), this.espera);
    this.espera = Math.min(this.espera * 2, REINTENTO_MAXIMO_MS);
  }

  private cancelarReintento(): void {
    if (this.reintento !== null) {
      clearTimeout(this.reintento);
      this.reintento = null;
    }
  }
}

function rechazo(c: CambioEnCola, codigo: string): CeldaRechazada {
  return {
    id: c.id,
    actividadId: c.cambio.actividadId,
    alumnoId: c.cambio.alumnoId,
    codigo,
    mensaje: '',
    vigente: null,
  };
}
