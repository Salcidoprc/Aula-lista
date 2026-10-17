import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { AlumnoDto } from '../../api/models/alumno-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { MatrizCalificacionesDto } from '../../api/models/matriz-calificaciones-dto';
import { NotaDto } from '../../api/models/nota-dto';
import { PeriodoDto } from '../../api/models/periodo-dto';
import { formatear } from '../../core/calculo/decimal';
import { codigoDeError } from '../../core/errores/problema';
import { ColaSincronizacion } from '../../core/sincronizacion/cola-sincronizacion';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { mostrarFecha } from '../../shared/fechas';
import { crearEnfoque } from '../../shared/foco';
import { Ocupado } from '../../shared/ocupado';
import { EvaluacionService } from '../evaluacion/evaluacion.service';
import {
  celdaVacia,
  dosDecimales,
  hoy,
  indexar,
  resultadoAlumno,
  textoCelda,
  unidadDeHoy,
  valorDeCelda,
} from '../evaluacion/matriz';

const LONGITUD_NOTA = 1000;
/** El contador aparece cerca del límite: antes solo distrae. */
const AVISO_LONGITUD = 800;
const ETIQUETAS = ['ninguna', 'fortaleza', 'mejora', 'conducta'] as const;
type Etiqueta = (typeof ETIQUETAS)[number];

/** Una nota en la lista: las del servidor y las que este dispositivo aún no envía. */
interface NotaVista {
  readonly id: string;
  readonly texto: string;
  readonly etiqueta: string;
  readonly fecha: string;
  readonly periodoId: string;
  readonly pendiente: boolean;
}

/**
 * SPEC-03, "Vista por alumno": en la ficha, su promedio actual, sus calificaciones por rubro y sus notas. Las
 * notas se guardan primero en el dispositivo (RF-037) y solo las ve el docente: nunca llegan a tutores.
 */
@Component({
  selector: 'app-avance-alumno',
  imports: [TranslocoDirective, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="avance" *transloco="let t; prefix: 'avance'">
      @if (periodos().length > 1) {
        <div class="unidades" role="group" [attr.aria-label]="t('unidades')">
          @for (p of periodos(); track p.id) {
            <button
              class="chip"
              type="button"
              [attr.aria-pressed]="p.id === periodo()?.id"
              (click)="elegir(p)"
            >
              {{ p.nombre }}
            </button>
          }
        </div>
      }

      @if (sinUnidades()) {
        <p class="dato-suave">{{ t('sinUnidades') }}</p>
      } @else if (!periodo() && error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="reintentar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else {
        <div class="columnas columnas--pares">
          <section class="calificaciones" aria-labelledby="avance-titulo">
            <h2 id="avance-titulo" class="solo-lector">{{ t('titulo') }}</h2>
            <!-- El error y la espera son solo de las calificaciones: las notas se escriben aunque no haya red. -->
            @if (error(); as codigo) {
              <div class="aviso aviso--error" role="alert">
                <p>{{ texto('errores.' + codigo) }}</p>
                <button class="boton boton--secundario" type="button" (click)="reintentar()">
                  {{ t('reintentar') }}
                </button>
              </div>
            } @else if (!matriz()) {
              <div role="status" [attr.aria-label]="t('cargando')">
                <span class="esqueleto promedio-esqueleto" aria-hidden="true"></span>
                <span class="esqueleto fila-esqueleto" aria-hidden="true"></span>
                <span class="esqueleto fila-esqueleto" aria-hidden="true"></span>
              </div>
            } @else {
              <div class="promedio-actual">
                <span class="etiqueta dato-suave">{{ t('promedio') }}</span>
                <span class="valor cifras">{{ promedio() }}</span>
                @if (parcial()) {
                  <span class="parcial dato-suave">{{ t('parcial') }}</span>
                }
              </div>

              @if (rubros().length === 0) {
                <p class="dato-suave">{{ t('sinRubros') }}</p>
              } @else {
                <ul class="rubros">
                  @for (r of rubros(); track r.id) {
                    <li class="rubro">
                      <details>
                        <summary>
                          <span class="cabeza-rubro">
                            <span class="nombre">{{ r.nombre }}</span>
                            <span class="peso dato-suave cifras">{{
                              t('peso', { peso: r.peso })
                            }}</span>
                            <span
                              class="valor-rubro cifras"
                              [class.sin-datos]="r.promedio === null"
                              >{{ r.promedio ?? t('sinDatos') }}</span
                            >
                            <span class="progreso" aria-hidden="true">
                              <span [style.inline-size.%]="r.porcentaje"></span>
                            </span>
                          </span>
                        </summary>
                        @if (r.actividades.length === 0) {
                          <p class="dato-suave sin-actividades">{{ t('sinActividades') }}</p>
                        } @else {
                          <ul class="actividades">
                            @for (a of r.actividades; track a.id) {
                              <li>
                                <span class="nombre-actividad">{{ a.nombre }}</span>
                                <span class="cifras" [class.dato-suave]="!a.capturada">{{
                                  a.texto
                                }}</span>
                              </li>
                            }
                          </ul>
                        }
                      </details>
                    </li>
                  }
                </ul>
              }
            }
          </section>

          <section class="notas" aria-labelledby="notas-titulo">
            <hgroup>
              <h2 id="notas-titulo">{{ t('notas.titulo') }}</h2>
              <p class="privado dato-suave">{{ t('notas.privado') }}</p>
            </hgroup>

            @if (puedeAnotar()) {
              <form class="formulario" (submit)="$event.preventDefault(); guardarNota()" novalidate>
                <div class="campo">
                  <label for="nota-texto">{{ t('notas.texto') }}</label>
                  <textarea
                    id="nota-texto"
                    rows="3"
                    [attr.maxlength]="longitud"
                    autocapitalize="sentences"
                    [value]="borrador()"
                    (input)="borrador.set(valor($event))"
                    [attr.aria-invalid]="intento() && !borrador().trim() ? true : null"
                    [attr.aria-describedby]="
                      intento() && !borrador().trim() ? 'nota-error nota-ayuda' : 'nota-ayuda'
                    "
                  ></textarea>
                  <p id="nota-ayuda" class="ayuda dato-suave">
                    {{ t('notas.dictado') }}
                    @if (borrador().length >= avisoLongitud) {
                      <span class="cifras contador" aria-live="polite">
                        {{ t('notas.contador', { usados: borrador().length, maximo: longitud }) }}
                      </span>
                    }
                  </p>
                  @if (intento() && !borrador().trim()) {
                    <p id="nota-error" class="error" role="alert">{{ t('notas.requerido') }}</p>
                  }
                </div>
                <fieldset class="eleccion etiquetas">
                  <legend>{{ t('notas.etiqueta') }}</legend>
                  @for (e of etiquetas; track e) {
                    <label>
                      <input
                        type="radio"
                        name="nota-etiqueta"
                        [value]="e"
                        [checked]="etiqueta() === e"
                        (change)="etiqueta.set(e)"
                      />
                      {{ t('etiquetas.' + e) }}
                    </label>
                  }
                </fieldset>
                <button class="boton" type="submit" [appOcupado]="guardando()">
                  {{ t('notas.guardar') }}
                </button>
              </form>
            }

            @if (sinConexionNotas()) {
              <p class="dato-suave">{{ t('notas.sinConexion') }}</p>
            }

            @if (notas().length === 0) {
              <p class="dato-suave">{{ t('notas.vacio') }}</p>
            } @else {
              <ol class="lista-notas" [attr.aria-label]="t('notas.lista')">
                @for (n of notas(); track n.id) {
                  <li class="nota">
                    <p class="meta dato-suave">
                      <span class="cifras">{{ fecha(n.fecha) }}</span>
                      @if (n.etiqueta !== 'ninguna') {
                        <span class="etiqueta-nota" [attr.data-etiqueta]="n.etiqueta">{{
                          t('etiquetas.' + n.etiqueta)
                        }}</span>
                      }
                      @if (n.pendiente) {
                        <span class="pendiente">{{ t('notas.porEnviar') }}</span>
                      }
                    </p>
                    <p class="texto-nota">{{ n.texto }}</p>
                  </li>
                }
              </ol>
            }
          </section>
        </div>
      }
    </div>
  `,
  styles: `
    .avance {
      display: grid;
      gap: var(--espacio-5);
    }

    .calificaciones,
    .notas {
      display: grid;
      align-content: start;
      gap: var(--espacio-4);
    }

    .promedio-actual {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      column-gap: var(--espacio-3);
    }

    .promedio-actual .etiqueta {
      flex-basis: 100%;
      font-size: var(--texto-sm);
    }

    .promedio-actual .valor {
      color: var(--grado-tinta);
      font-size: var(--texto-2xl);
      font-weight: var(--peso-fuerte);
      line-height: 1.1;
    }

    .parcial {
      font-size: var(--texto-sm);
    }

    .rubros,
    .actividades,
    .lista-notas {
      display: grid;
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .rubros {
      border-top: 1px solid var(--color-divisoria);
    }

    .rubro {
      border-bottom: 1px solid var(--color-divisoria);
    }

    /* La flecha global del desplegable a la izquierda; nombre, peso y promedio en una fila y la barra de
       proporción (promedio sobre 10) debajo. */
    summary {
      display: flex;
      align-items: flex-start;
      min-block-size: var(--tactil-minimo);
      padding-block: var(--espacio-3);
      cursor: pointer;
    }

    summary::before {
      margin-block-start: 0.45em;
    }

    .cabeza-rubro {
      flex: 1;
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto auto;
      align-items: baseline;
      column-gap: var(--espacio-3);
      row-gap: var(--espacio-2);
      min-inline-size: 0;
    }

    summary:focus-visible {
      border-radius: var(--radio-md);
      box-shadow: var(--foco);
      outline: none;
    }

    .cabeza-rubro .nombre {
      font-weight: var(--peso-medio);
      overflow-wrap: anywhere;
    }

    .peso {
      font-size: var(--texto-sm);
    }

    .valor-rubro {
      min-inline-size: 4ch;
      font-weight: var(--peso-fuerte);
      text-align: end;
    }

    .valor-rubro.sin-datos {
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
      font-weight: var(--peso-normal);
    }

    .cabeza-rubro .progreso {
      grid-column: 1 / -1;
    }

    .actividades {
      padding-block-end: var(--espacio-3);
    }

    .actividades li {
      display: flex;
      justify-content: space-between;
      gap: var(--espacio-3);
      padding: var(--espacio-1) 0 var(--espacio-1) calc(1rem + var(--espacio-3));
      font-size: var(--texto-sm);
    }

    .nombre-actividad {
      overflow-wrap: anywhere;
    }

    .sin-actividades {
      padding-block-end: var(--espacio-3);
      font-size: var(--texto-sm);
    }

    .notas hgroup {
      display: grid;
      gap: var(--espacio-1);
    }

    .notas h2 {
      font-size: var(--texto-lg);
    }

    .privado,
    .ayuda {
      font-size: var(--texto-sm);
    }

    .ayuda {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      gap: var(--espacio-2);
      margin: 0;
    }

    .contador {
      color: var(--color-aviso-tinta);
    }

    .etiquetas {
      display: flex;
      flex-wrap: wrap;
    }

    .lista-notas {
      gap: var(--espacio-3);
    }

    .nota {
      display: grid;
      gap: var(--espacio-1);
      padding-block-end: var(--espacio-3);
      border-bottom: 1px solid var(--color-divisoria);
    }

    .meta {
      display: flex;
      flex-wrap: wrap;
      gap: var(--espacio-1) var(--espacio-3);
      margin: 0;
      font-size: var(--texto-sm);
    }

    .etiqueta-nota {
      color: var(--color-texto);
      font-weight: var(--peso-medio);
    }

    .pendiente {
      color: var(--color-aviso-tinta);
      font-weight: var(--peso-medio);
    }

    .texto-nota {
      margin: 0;
      white-space: pre-line;
      overflow-wrap: anywhere;
    }

    .promedio-esqueleto {
      display: block;
      inline-size: 6rem;
      block-size: 3rem;
      margin-block-end: var(--espacio-4);
    }

    .fila-esqueleto {
      display: block;
      block-size: 3.25rem;
      margin-block-end: var(--espacio-2);
    }
  `,
})
export class AvanceAlumno {
  private readonly servicio = inject(EvaluacionService);
  private readonly cola = inject(ColaSincronizacion);
  private readonly avisos = inject(AvisosService);
  private readonly i18n = inject(TranslocoService);
  private readonly enfocar = crearEnfoque();
  protected readonly etiquetas = ETIQUETAS;
  protected readonly longitud = LONGITUD_NOTA;
  protected readonly avisoLongitud = AVISO_LONGITUD;

  readonly alumno = input.required<AlumnoDto>();
  readonly grupo = input<GrupoDto | null>(null);

  protected readonly periodos = signal<readonly PeriodoDto[]>([]);
  protected readonly periodo = signal<PeriodoDto | null>(null);
  protected readonly sinUnidades = signal(false);
  protected readonly matriz = signal<MatrizCalificacionesDto | null>(null);
  protected readonly error = signal<string | null>(null);

  private readonly delServidor = signal<readonly NotaDto[]>([]);
  private readonly pendientes = signal<readonly NotaVista[]>([]);
  protected readonly sinConexionNotas = signal(false);
  protected readonly borrador = signal('');
  protected readonly etiqueta = signal<Etiqueta>('ninguna');
  protected readonly intento = signal(false);
  protected readonly guardando = signal(false);

  private readonly indice = computed(() => {
    const m = this.matriz();
    return m ? indexar(m) : null;
  });
  private readonly resultado = computed(() => {
    const m = this.matriz();
    const i = this.indice();
    return m && i ? resultadoAlumno(m, i, this.alumno().id) : null;
  });
  /** El promedio de la unidad con los decimales y el redondeo del grupo (RN-035). */
  protected readonly promedio = computed(() => {
    const r = this.resultado();
    const decimales = Number(this.matriz()?.configuracion.decimalesFinal ?? 2);
    return r?.final ? formatear(r.final, decimales) : '—';
  });
  /** RN-033: algún rubro sin datos y su peso se repartió entre los demás. */
  protected readonly parcial = computed(() => {
    const r = this.resultado();
    return !!r?.periodo && r.sinDatos.length > 0;
  });
  protected readonly rubros = computed(() => {
    const m = this.matriz();
    const i = this.indice();
    const r = this.resultado();
    if (!m || !i) {
      return [];
    }
    const alumnoId = this.alumno().id;
    return m.rubros.map(({ rubro, actividades }) => {
      const promedio = r?.rubros.get(rubro.id) ?? null;
      const texto = promedio === null ? null : dosDecimales(promedio);
      return {
        id: rubro.id,
        nombre: rubro.nombre,
        peso: Number(rubro.peso),
        promedio: texto,
        porcentaje: texto === null ? 0 : Math.min(100, Number(texto) * 10),
        actividades: actividades.map((a) => {
          const celda = i.celdas.get(`${a.id}:${alumnoId}`);
          const capturada = !celdaVacia(celda);
          const valor =
            rubro.forma === 'numerica' && !rubro.criterios.length
              ? textoCelda(celda)
              : capturada
                ? dosDecimales(valorDeCelda(rubro, celda))
                : '';
          return { id: a.id, nombre: a.nombre, capturada, texto: valor || '—' };
        }),
      };
    });
  });

  /** Las notas de la unidad elegida, de la más nueva a la más vieja; las pendientes primero. */
  protected readonly notas = computed(() => {
    const periodoId = this.periodo()?.id;
    const pendientes = this.pendientes().filter((n) => n.periodoId === periodoId);
    const ids = new Set(pendientes.map((n) => n.id));
    const servidor = this.delServidor()
      .filter((n) => n.periodoId === periodoId && !ids.has(n.id))
      .map((n) => ({ ...n, pendiente: false }));
    return [...pendientes, ...servidor].sort((a, b) =>
      a.pendiente !== b.pendiente ? (a.pendiente ? -1 : 1) : b.fecha.localeCompare(a.fecha),
    );
  });

  /** Solo en una unidad abierta, de un alumno activo y un grupo vigente (igual que la captura). */
  protected readonly puedeAnotar = computed(
    () =>
      this.periodo()?.estado === 'abierto' &&
      !this.alumno().bajaEn &&
      !this.grupo()?.archivadoEn &&
      !this.matriz()?.soloLectura,
  );

  constructor() {
    effect(() => {
      const grupoId = this.alumno().grupoId;
      untracked(() => void this.cargar(grupoId));
    });
    const dejar = this.cola.alEnviarNota((alumnoId) => {
      if (alumnoId === this.alumno().id) {
        void this.cargarNotas();
      }
    });
    inject(DestroyRef).onDestroy(dejar);
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  protected valor(evento: Event): string {
    return (evento.target as HTMLTextAreaElement).value;
  }

  protected fecha(valor: string): string {
    return mostrarFecha(valor);
  }

  private async cargar(grupoId: string): Promise<void> {
    this.error.set(null);
    try {
      const periodos = await this.servicio.periodos(grupoId);
      this.periodos.set(periodos);
      const elegido = unidadDeHoy(periodos) ?? null;
      this.sinUnidades.set(!elegido);
      this.periodo.set(elegido);
      await Promise.all([this.cargarMatriz(), this.cargarNotas()]);
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected async elegir(p: PeriodoDto): Promise<void> {
    if (p.id === this.periodo()?.id) {
      return;
    }
    this.periodo.set(p);
    await this.cargarMatriz();
  }

  /** Sin unidad, se vuelve a leer todo; con unidad, solo sus calificaciones. */
  protected reintentar(): void {
    if (this.periodo()) {
      void this.cargarMatriz();
    } else {
      void this.cargar(this.alumno().grupoId);
    }
  }

  protected async cargarMatriz(): Promise<void> {
    const p = this.periodo();
    if (!p) {
      return;
    }
    this.error.set(null);
    this.matriz.set(null);
    try {
      const matriz = await this.servicio.matriz(p.id);
      if (this.periodo()?.id === p.id) {
        this.matriz.set(matriz);
      }
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  /** Sin conexión se ven las del dispositivo y un aviso; las del servidor llegan al volver la conexión. */
  private async cargarNotas(): Promise<void> {
    const alumnoId = this.alumno().id;
    const pendientes = await this.servicio.notasPendientes(alumnoId);
    this.pendientes.set(
      pendientes.map((p) => ({
        id: p.id,
        texto: p.nota.texto,
        etiqueta: p.nota.etiqueta ?? 'ninguna',
        fecha: p.nota.fecha ?? hoy(),
        pendiente: true,
        periodoId: p.nota.periodoId,
      })),
    );
    try {
      this.delServidor.set(await this.servicio.notas(alumnoId));
      this.sinConexionNotas.set(false);
    } catch (e) {
      this.sinConexionNotas.set(e instanceof HttpErrorResponse && e.status === 0);
    }
  }

  protected async guardarNota(): Promise<void> {
    this.intento.set(true);
    const texto = this.borrador().trim();
    const p = this.periodo();
    if (!texto) {
      this.enfocar('#nota-texto');
      return;
    }
    if (!p) {
      return;
    }
    this.guardando.set(true);
    try {
      await this.servicio.crearNota(this.alumno().id, {
        id: crypto.randomUUID(),
        periodoId: p.id,
        texto,
        etiqueta: this.etiqueta() === 'ninguna' ? null : this.etiqueta(),
        fecha: hoy(),
      });
      this.borrador.set('');
      this.etiqueta.set('ninguna');
      this.intento.set(false);
      await this.cargarNotas();
      this.avisos.mostrar(
        this.i18n.translate(
          this.cola.estado() === 'sin_conexion'
            ? 'avance.notas.guardadaAqui'
            : 'avance.notas.guardada',
        ),
      );
    } catch {
      this.avisos.mostrar(this.i18n.translate('captura.noSeGuardo'), 'error');
    } finally {
      this.guardando.set(false);
    }
  }
}
