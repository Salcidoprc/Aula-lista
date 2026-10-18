import { HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { Api } from '../../api/api';
import { guardarAsistencia } from '../../api/fn/asistencia/guardar-asistencia';
import { sincronizar } from '../../api/fn/calificaciones/sincronizar';
import { crearNota } from '../../api/fn/notas/crear-nota';
import { CambioCelda } from '../../api/models/cambio-celda';
import { CrearNota } from '../../api/models/crear-nota';
import { EstadoAlumno } from '../../api/models/estado-alumno';
import { TomaAsistenciaDto } from '../../api/models/toma-asistencia-dto';
import { CeldaDto } from '../../api/models/celda-dto';
import { CeldaRechazada } from '../../api/models/celda-rechazada';
import { MatrizCalificacionesDto } from '../../api/models/matriz-calificaciones-dto';
import { ALMACEN_LOCAL, AsistenciaEnCola, CambioEnCola, NotaEnCola } from './almacen-local';
import { conCambios, conCeldas, llaveCelda, llavesDeCambio } from './celdas';

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
 * Lo que respondió el servidor a una asistencia de la cola: la toma vigente o, si se descartó (fecha fuera de
 * unidad, unidad cerrada, grupo ajeno), el código del motivo.
 */
export interface AsistenciaEnviada {
  readonly pendiente: AsistenciaEnCola;
  readonly toma: TomaAsistenciaDto | null;
  readonly codigo: string | null;
}

/**
 * Cola de salida sin conexión (RF-037, RF-103, reglas/02). Cada captura se guarda primero en el dispositivo junto con
 * su cambio y un UUID; la cola los manda en orden a `POST /sincronizacion` cuando hay red. El servidor ignora
 * los UUID que ya aplicó, así que reenviar nunca duplica. Una celda rechazada no detiene la cola.
 */
@Injectable({ providedIn: 'root' })
export class ColaSincronizacion {
  private readonly api = inject(Api);
  private readonly almacen = inject(ALMACEN_LOCAL);
  private readonly oyentes = new Set<(resultado: ResultadoLote) => void>();
  private readonly oyentesNotas = new Set<(alumnoId: string) => void>();
  private readonly oyentesAsistencia = new Set<(enviada: AsistenciaEnviada) => void>();
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

  /** Celdas con una captura que se está escribiendo en el dispositivo (cuántas a la vez). */
  private readonly escribiendo = new Map<string, number>();
  /** Confirmaciones en curso: cada una junta las celdas que se capturan mientras se aplica. */
  private readonly colectores = new Set<Set<string>>();

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
    // Mientras se escribe, una confirmación que llegue no debe pisar estas celdas con lo que tenía el servidor.
    const llaves = cambios.flatMap((c) => llavesDeCambio(matriz, c));
    for (const llave of llaves) {
      this.escribiendo.set(llave, (this.escribiendo.get(llave) ?? 0) + 1);
      for (const tocadas of this.colectores) {
        tocadas.add(llave);
      }
    }
    try {
      await this.encolar(matriz, cambios);
    } finally {
      for (const llave of llaves) {
        const n = (this.escribiendo.get(llave) ?? 1) - 1;
        if (n > 0) {
          this.escribiendo.set(llave, n);
        } else {
          this.escribiendo.delete(llave);
        }
      }
    }
    await this.contar();
    void this.enviar();
  }

  private async encolar(
    matriz: MatrizCalificacionesDto,
    cambios: readonly CambioCelda[],
  ): Promise<void> {
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

  /**
   * Una nota nueva (SPEC-03): se guarda en el dispositivo y se envía cuando haya red. Cada nota es un registro
   * nuevo con su id, así que reenviarla no la duplica (02-arquitectura).
   */
  async guardarNota(alumnoId: string, nota: CrearNota): Promise<void> {
    await this.almacen.encolarNota({ id: nota.id, alumnoId, creado: Date.now(), nota });
    await this.contar();
    void this.enviar();
  }

  /** Las notas del alumno que todavía no llegan al servidor. */
  async notasPendientes(alumnoId: string): Promise<readonly NotaEnCola[]> {
    return this.almacen.notasPendientes(alumnoId).catch(() => []);
  }

  /** Avisa cuando una nota llegó al servidor (o se descartó). Devuelve cómo dejar de escuchar. */
  alEnviarNota(oyente: (alumnoId: string) => void): () => void {
    this.oyentesNotas.add(oyente);
    return () => this.oyentesNotas.delete(oyente);
  }

  /**
   * Pasar lista (SPEC-10): los estados se guardan en el dispositivo y la cola los manda en orden con su id como
   * `Idempotency-Key`. Por alumno gana la última escritura, así que reenviar no cambia nada.
   */
  async guardarAsistencia(
    grupoId: string,
    fecha: string,
    alumnos: readonly EstadoAlumno[],
    id: string = crypto.randomUUID(),
  ): Promise<AsistenciaEnCola> {
    const pendiente: AsistenciaEnCola = { id, grupoId, fecha, creado: Date.now(), alumnos };
    await this.almacen.encolarAsistencia(pendiente);
    await this.contar();
    void this.enviar();
    return pendiente;
  }

  /** Las asistencias del grupo que todavía no llegan al servidor, en orden. */
  async asistenciasPendientes(grupoId: string): Promise<readonly AsistenciaEnCola[]> {
    return this.almacen.asistenciasPendientes(grupoId).catch(() => []);
  }

  /** Avisa de cada asistencia que llegó al servidor (o se descartó). Devuelve cómo dejar de escuchar. */
  alEnviarAsistencia(oyente: (enviada: AsistenciaEnviada) => void): () => void {
    this.oyentesAsistencia.add(oyente);
    return () => this.oyentesAsistencia.delete(oyente);
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
    await this.enviarCeldas();
    await this.enviarNotas();
    await this.enviarAsistencias();
  }

  /** Una por una y en orden. Un 4xx (fecha fuera de unidad, unidad cerrada) la descarta con su motivo. */
  private async enviarAsistencias(): Promise<void> {
    for (const pendiente of await this.almacen.asistenciasPendientes()) {
      if (!this.conexion()) {
        return;
      }
      this.mandando.set(true);
      let enviada: AsistenciaEnviada;
      try {
        const respuesta = await this.api.invoke(guardarAsistencia, {
          id: pendiente.grupoId,
          fecha: pendiente.fecha,
          'Idempotency-Key': pendiente.id,
          body: { alumnos: [...pendiente.alumnos] },
        });
        enviada = { pendiente, toma: respuesta.toma, codigo: null };
      } catch (error) {
        const estado = error instanceof HttpErrorResponse ? error.status : 0;
        if (estado === 0) {
          this.conexion.set(false);
        }
        if (estado === 0 || estado === 401 || estado >= 500) {
          if (estado !== 401) {
            this.programarReintento();
          }
          return;
        }
        const cuerpo =
          error instanceof HttpErrorResponse ? (error.error as { codigo?: string }) : null;
        enviada = { pendiente, toma: null, codigo: cuerpo?.codigo ?? 'desconocido' };
      }
      await this.almacen.quitarAsistencia(pendiente.id);
      await this.contar();
      for (const oyente of this.oyentesAsistencia) {
        oyente(enviada);
      }
    }
  }

  /** Las notas, una por una y en orden. Un 4xx (alumno o periodo que ya no admite notas) la descarta. */
  private async enviarNotas(): Promise<void> {
    for (const pendiente of await this.almacen.notasPendientes()) {
      if (!this.conexion()) {
        return;
      }
      this.mandando.set(true);
      try {
        await this.api.invoke(crearNota, { id: pendiente.alumnoId, body: pendiente.nota });
      } catch (error) {
        const estado = error instanceof HttpErrorResponse ? error.status : 0;
        if (estado === 0) {
          this.conexion.set(false);
        }
        if (estado === 0 || estado === 401 || estado >= 500) {
          if (estado !== 401) {
            this.programarReintento();
          }
          return;
        }
      }
      await this.almacen.quitarNota(pendiente.id);
      await this.contar();
      for (const oyente of this.oyentesNotas) {
        oyente(pendiente.alumnoId);
      }
    }
  }

  private async enviarCeldas(): Promise<void> {
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
    // Celdas que se capturan mientras se confirma: su cambio quizá aún no está en la cola que se lee aquí.
    const tocadas = new Set(this.escribiendo.keys());
    this.colectores.add(tocadas);
    try {
      await this.aplicarConfirmacion(lote, aplicadas, rechazadas, tocadas);
    } finally {
      this.colectores.delete(tocadas);
    }
  }

  private async aplicarConfirmacion(
    lote: readonly CambioEnCola[],
    aplicadas: readonly CeldaDto[],
    rechazadas: readonly CeldaRechazada[],
    tocadas: ReadonlySet<string>,
  ): Promise<void> {
    await this.almacen.quitar(lote.map((c) => c.id));
    // Las celdas con otro cambio detrás en la cola; uno por equipo cuenta por cada integrante.
    const matrices = new Map<string, MatrizCalificacionesDto | undefined>();
    const siguen = new Set<string>();
    for (const c of await this.almacen.pendientes()) {
      if (!matrices.has(c.periodoId)) {
        matrices.set(c.periodoId, (await this.almacen.leerMatriz(c.periodoId))?.matriz);
      }
      const matriz = matrices.get(c.periodoId);
      for (const llave of matriz ? llavesDeCambio(matriz, c.cambio) : [llaveCelda(c.cambio)]) {
        siguen.add(llave);
      }
    }
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
    const sinTocadas = (): void => {
      for (const llave of tocadas) {
        vigentes.delete(llave);
      }
    };
    sinTocadas();

    for (const periodoId of new Set(lote.map((c) => c.periodoId))) {
      const guardada = await this.almacen.leerMatriz(periodoId);
      if (guardada) {
        await this.almacen.guardarMatriz(conCeldas(guardada.matriz, vigentes));
      }
    }
    await this.contar();
    sinTocadas(); // las que se capturaron mientras se guardaba la copia local
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
