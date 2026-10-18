import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { CierreDto } from '../../api/models/cierre-dto';
import { EstiloDto } from '../../api/models/estilo-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { MatrizCalificacionesDto } from '../../api/models/matriz-calificaciones-dto';
import { NotaDto } from '../../api/models/nota-dto';
import { ObservacionDto } from '../../api/models/observacion-dto';
import { codigoDeError } from '../../core/errores/problema';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { mostrarFecha } from '../../shared/fechas';
import { crearEnfoque } from '../../shared/foco';
import { claveGrado } from '../../shared/grados';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { controlarPortada } from '../../shared/portada/portada.service';
import { EvaluacionService } from '../evaluacion/evaluacion.service';
import { portadaDeGrupo } from '../grupos/grupos.service';
import { CierreService } from './cierre.service';
import {
  SUGERENCIAS,
  calificacion,
  contarPalabras,
  estaAprobada,
  siguientePendiente,
  vecinos,
} from './cierre';

const CONSULTA_MS = 3000;
const LONGITUD_TEXTO = 1500;
const LONGITUD_INSTRUCCION = 200;

/**
 * SPEC-04, "Revisar observación" (RF-042, RF-045): los datos del alumno arriba y el texto abajo. "Aprobar y
 * siguiente" guarda y aprueba en una llamada y pasa al siguiente sin aprobar; es la acción repetida y no se
 * anima (reglas/07). Solo "Aprobar", en la última, cambia a "Aprobada" con su transición del catálogo.
 */
@Component({
  selector: 'app-revisar-observacion',
  imports: [TranslocoDirective, RouterLink, NgTemplateOutlet, Icono, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="pantalla pantalla--ancha"
      [attr.data-grado]="clave() ?? null"
      *transloco="let t; prefix: 'revisar'"
    >
      <a class="volver" [routerLink]="rutaCierre()">
        <app-icono nombre="volver" />
        {{ t('volver') }}
      </a>

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (!alumno()) {
        <div role="status" [attr.aria-label]="t('cargando')">
          <span class="esqueleto linea-esqueleto" aria-hidden="true"></span>
          <span class="esqueleto texto-esqueleto" aria-hidden="true"></span>
        </div>
      } @else if (alumno(); as a) {
        <div class="cabeza">
          <hgroup>
            <p class="dato-suave cifras posicion">
              {{ t('posicion', { posicion: lugar().posicion, total: total() }) }}
            </p>
            <h1 tabindex="-1">
              <span class="numero cifras">{{ a.numeroLista }}.</span>
              {{ a.nombres }} {{ a.apellidos }}
            </h1>
          </hgroup>
          <nav class="vecinos" [attr.aria-label]="t('navegar')">
            <button
              class="boton-icono"
              type="button"
              [disabled]="!lugar().anterior"
              [attr.aria-label]="t('anterior')"
              (click)="irA(lugar().anterior?.alumnoId)"
            >
              <app-icono nombre="volver" />
            </button>
            <button
              class="boton-icono"
              type="button"
              [disabled]="!lugar().siguiente"
              [attr.aria-label]="t('siguiente')"
              (click)="irA(lugar().siguiente?.alumnoId)"
            >
              <app-icono nombre="siguiente" />
            </button>
          </nav>
        </div>

        @if (!enLinea()) {
          <p class="aviso aviso--pendiente" role="status">{{ t('sinConexion') }}</p>
        }
        @if (cierre()?.soloLectura) {
          <p class="aviso aviso--pendiente">{{ t('soloLectura') }}</p>
        }

        <div class="dos-columnas">
          <details class="datos solo-celular">
            <summary>
              {{ t('datos') }}
              <span class="cifras dato-suave">{{ calificacion(a.calificacion) }}</span>
            </summary>
            <ng-container [ngTemplateOutlet]="datos" />
          </details>
          <aside class="datos lateral solo-escritorio" aria-labelledby="datos-titulo">
            <h2 id="datos-titulo">{{ t('datos') }}</h2>
            <ng-container [ngTemplateOutlet]="datos" />
          </aside>

          <section class="redaccion" aria-labelledby="observacion-titulo">
            <div class="titulo-observacion">
              <h2 id="observacion-titulo">{{ t('observacion') }}</h2>
              @if (observacion(); as o) {
                <span class="estado" [attr.data-estado]="o.estado">{{
                  texto('cierre.estados.' + o.estado)
                }}</span>
              }
            </div>

            @if (observacion()?.datosCambiaron) {
              <p class="aviso aviso--pendiente">{{ t('datosCambiaron') }}</p>
            }
            @if (observacion()?.fallo && !generando()) {
              <p class="aviso aviso--error" role="alert">{{ t('fallo') }}</p>
            }
            @if (errorAccion(); as codigo) {
              <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
            }

            @if (generando()) {
              <div class="generando" role="status">
                <app-icono nombre="sincronizando" />
                <p>{{ t('generando') }}</p>
              </div>
              <div class="papel" aria-hidden="true">
                <span class="esqueleto renglon"></span>
                <span class="esqueleto renglon"></span>
                <span class="esqueleto renglon corto"></span>
              </div>
            } @else if (aprobada()) {
              <p class="papel leido">{{ borrador() }}</p>
            } @else {
              <label class="solo-lector" for="texto-observacion">{{ t('textoEtiqueta') }}</label>
              <textarea
                #campo
                id="texto-observacion"
                class="papel"
                rows="7"
                [maxLength]="maximo"
                [value]="borrador()"
                [disabled]="!editable()"
                [placeholder]="t('placeholder')"
                [attr.aria-describedby]="'conteo-palabras atajo'"
                (input)="escribir($event)"
                (blur)="guardar()"
                (keydown)="atajo($event)"
              ></textarea>
            }

            <p
              id="conteo-palabras"
              class="conteo cifras"
              [class.fuera]="palabras() > 0 && !enRango()"
            >
              {{
                t('palabras', {
                  palabras: palabras(),
                  longitud: texto('estilo.longitudes.' + (estilo()?.longitud ?? 'normal')),
                  minimo: estilo()?.palabrasMinimas ?? 60,
                  maximo: estilo()?.palabrasMaximas ?? 90,
                })
              }}
            </p>

            @if (!aprobada() && !generando() && editable()) {
              <div class="acciones">
                @if (puedeGenerar()) {
                  <button
                    class="boton boton--secundario"
                    type="button"
                    [disabled]="!enLinea()"
                    [appOcupado]="trabajando() === 'generar'"
                    (click)="generar()"
                  >
                    {{ t('generar') }}
                  </button>
                }
                <button class="enlace" type="button" (click)="aMano()">
                  <app-icono nombre="editar" />
                  {{ t('aMano') }}
                </button>
              </div>

              @if (observacion()?.texto) {
                <fieldset class="regenerar">
                  <legend>{{ t('regenerar.titulo') }}</legend>
                  <div class="sugerencias">
                    @for (s of sugerencias; track s) {
                      <button
                        class="chip"
                        type="button"
                        [attr.aria-pressed]="sugerencia() === s"
                        (click)="elegirSugerencia(s)"
                      >
                        {{ t('regenerar.sugerencias.' + s) }}
                      </button>
                    }
                  </div>
                  <div class="campo">
                    <label for="instruccion">{{ t('regenerar.instruccion') }}</label>
                    <input
                      id="instruccion"
                      type="text"
                      autocomplete="off"
                      [maxLength]="maximoInstruccion"
                      [value]="instruccion()"
                      [placeholder]="t('regenerar.ejemplo')"
                      (input)="instruccion.set($any($event.target).value)"
                    />
                    <p class="ayuda">{{ t('regenerar.sinNombres') }}</p>
                  </div>
                  <div class="fila-regenerar">
                    <button
                      class="boton boton--secundario"
                      type="button"
                      [disabled]="!enLinea() || restantes() === 0"
                      [appOcupado]="trabajando() === 'regenerar'"
                      (click)="regenerar()"
                    >
                      {{ t('regenerar.accion') }}
                    </button>
                    <span class="dato-suave cifras">{{
                      t('regenerar.restantes', { restantes: restantes() })
                    }}</span>
                  </div>
                </fieldset>
              }
            }

            @if (!aprobada() && editable()) {
              <p id="atajo" class="dato-suave atajo solo-escritorio">{{ t('atajo') }}</p>
            }

            <div class="pie" [class.pie-accion]="!aprobada()">
              <ng-container [ngTemplateOutlet]="aprobar" />
            </div>
          </section>
        </div>
      }

      <ng-template #aprobar>
        @if (aprobada()) {
          <div class="aprobada-fila">
            <span class="sello" [class.recien]="recienAprobada()" role="status">
              <app-icono nombre="guardado" />
              {{ t('aprobada') }}
            </span>
            @if (editable() && observacion()?.estado === 'aprobada') {
              <button
                class="boton boton--secundario"
                type="button"
                [disabled]="!enLinea()"
                [appOcupado]="trabajando() === 'reabrir'"
                (click)="reabrir()"
              >
                {{ t('reabrir') }}
              </button>
            }
            @if (siguiente(); as s) {
              <button class="boton" type="button" (click)="irA(s.alumnoId)">
                {{ t('irSiguiente') }}
              </button>
            } @else {
              <a class="boton" [routerLink]="rutaCierre()">{{ t('volverLista') }}</a>
            }
          </div>
        } @else if (editable() && !generando()) {
          <button
            class="boton boton--ancho"
            type="button"
            [disabled]="!enLinea() || !borrador().trim()"
            [appOcupado]="trabajando() === 'aprobar'"
            (click)="aprobarYSeguir()"
          >
            {{ siguiente() ? t('aprobarYSiguiente') : t('aprobarSolo') }}
          </button>
        }
      </ng-template>

      <ng-template #datos>
        @if (alumno(); as a) {
          <dl class="promedio">
            <dt>{{ t('calificacion') }}</dt>
            <dd class="cifras">{{ calificacion(a.calificacion) }}</dd>
          </dl>
          <ul class="rubros">
            @for (r of rubros(); track r.id) {
              <li>
                <span class="nombre-rubro">{{ r.nombre }}</span>
                <span class="dato-suave cifras">{{ r.peso }} %</span>
                <span class="cifras valor">{{ r.promedio ?? t('sinDatos') }}</span>
              </li>
            }
          </ul>
          <h3>{{ t('notas') }}</h3>
          @if (notas().length === 0) {
            <p class="dato-suave">{{ t('sinNotas') }}</p>
          } @else {
            <ul class="notas">
              @for (n of notas(); track n.id) {
                <li>
                  <p class="dato-suave meta cifras">
                    {{ fecha(n.fecha) }}
                    @if (n.etiqueta !== 'ninguna') {
                      ·
                      <span [attr.data-etiqueta]="n.etiqueta">{{
                        texto('avance.etiquetas.' + n.etiqueta)
                      }}</span>
                    }
                  </p>
                  <p>{{ n.texto }}</p>
                </li>
              }
            </ul>
          }
        }
      </ng-template>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .cabeza {
      display: flex;
      align-items: end;
      justify-content: space-between;
      gap: var(--espacio-3);
    }

    .posicion {
      font-size: var(--texto-sm);
    }

    h1 .numero {
      color: var(--color-texto-suave);
      font-weight: var(--peso-normal);
    }

    .vecinos {
      display: flex;
      gap: var(--espacio-1);
    }

    .dos-columnas {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      gap: var(--espacio-5);
    }

    @media (min-width: 64rem) {
      .dos-columnas {
        grid-template-columns: minmax(16rem, 22rem) minmax(0, 1fr);
        align-items: start;
      }

      .datos.lateral {
        position: sticky;
        inset-block-start: var(--espacio-4);
      }
    }

    details.datos {
      border-radius: var(--radio-lg);
      background: var(--color-superficie-hundida);
    }

    details.datos summary {
      display: flex;
      align-items: center;
      gap: var(--espacio-3);
      min-block-size: var(--tactil-minimo);
      padding: var(--espacio-2) var(--espacio-4);
      font-weight: var(--peso-medio);
      cursor: pointer;
    }

    details.datos summary .cifras {
      margin-inline-start: auto;
    }

    details.datos summary:focus-visible {
      box-shadow: var(--foco);
      outline: none;
      border-radius: var(--radio-lg);
    }

    details.datos[open] {
      padding-block-end: var(--espacio-4);
    }

    details.datos > :not(summary) {
      margin-inline: var(--espacio-4);
    }

    .datos {
      display: grid;
      gap: var(--espacio-3);
    }

    details.datos[open] {
      display: grid;
    }

    .datos h3 {
      margin-block-start: var(--espacio-2);
      font-size: var(--texto-base);
    }

    .promedio {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      margin: 0;
    }

    .promedio dd {
      margin: 0;
      font-size: var(--texto-xl);
      font-weight: var(--peso-fuerte);
    }

    .rubros,
    .notas {
      display: grid;
      gap: var(--espacio-2);
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .rubros li {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto 3.5rem;
      gap: var(--espacio-2);
      align-items: baseline;
      padding-block: var(--espacio-1);
      border-bottom: 1px solid var(--color-divisoria);
    }

    .nombre-rubro {
      overflow-wrap: anywhere;
    }

    .valor {
      font-weight: var(--peso-medio);
      text-align: end;
    }

    .notas li {
      display: grid;
      gap: var(--espacio-1);
    }

    .meta {
      font-size: var(--texto-xs);
    }

    [data-etiqueta='fortaleza'] {
      color: var(--color-exito-tinta);
    }

    [data-etiqueta='mejora'] {
      color: var(--color-aviso-tinta);
    }

    .redaccion {
      display: grid;
      gap: var(--espacio-3);
      min-inline-size: 0;
    }

    .titulo-observacion {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--espacio-3);
    }

    .titulo-observacion h2 {
      font-size: var(--texto-lg);
    }

    .estado {
      padding: var(--espacio-1) var(--espacio-3);
      border-radius: var(--radio-pleno);
      background: var(--color-superficie-hundida);
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
      font-weight: var(--peso-medio);
    }

    .estado[data-estado='borrador'] {
      background: var(--grado-tinte);
      color: var(--grado-tinta);
    }

    .estado[data-estado='aprobada'],
    .estado[data-estado='publicada'] {
      background: var(--color-exito-tinte);
      color: var(--color-exito-tinta);
    }

    .estado[data-estado='generando'] {
      background: var(--color-aviso-tinte);
      color: var(--color-aviso-tinta);
    }

    /* La observación se lee como texto de libro: sobre papel, con medida cómoda, no en una caja gris. */
    .papel {
      inline-size: 100%;
      max-inline-size: 42rem;
      min-block-size: 12rem;
      margin: 0;
      padding: var(--espacio-4);
      border: 1px solid var(--color-divisoria);
      border-radius: var(--radio-lg);
      background: var(--color-superficie);
      box-shadow: inset 0 -2px 0 var(--color-texto-tenue);
      color: var(--color-texto);
      font: inherit;
      font-size: 1.125rem;
      line-height: var(--interlineado);
      resize: vertical;
      field-sizing: content;
      transition: box-shadow var(--dur-small) var(--ease-out);
    }

    textarea.papel:focus-visible {
      box-shadow: var(--foco);
      outline: none;
    }

    .papel.leido {
      white-space: pre-wrap;
      box-shadow: none;
    }

    div.papel {
      display: grid;
      align-content: start;
      gap: var(--espacio-3);
    }

    .renglon {
      display: block;
      block-size: 1rem;
    }

    .renglon.corto {
      inline-size: 60%;
    }

    .generando {
      display: flex;
      align-items: center;
      gap: var(--espacio-2);
      color: var(--color-aviso-tinta);
    }

    .generando app-icono {
      animation: girar 1s linear infinite;
    }

    @media (prefers-reduced-motion: reduce) {
      .generando app-icono {
        animation: none;
      }
    }

    @keyframes girar {
      to {
        transform: rotate(1turn);
      }
    }

    .conteo {
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
    }

    .conteo.fuera {
      color: var(--color-aviso-tinta);
      font-weight: var(--peso-medio);
    }

    .acciones,
    .fila-regenerar,
    .aprobada-fila {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--espacio-3);
    }

    .enlace {
      display: inline-flex;
      align-items: center;
      gap: var(--espacio-2);
      min-block-size: var(--tactil-minimo);
    }

    button.enlace {
      padding: 0;
      border: 0;
      background: none;
      font: inherit;
      cursor: pointer;
    }

    .regenerar {
      display: grid;
      gap: var(--espacio-3);
      max-inline-size: 42rem;
      margin: 0;
      padding: var(--espacio-4);
      border: 0;
      border-radius: var(--radio-lg);
      background: var(--color-superficie-hundida);
    }

    .regenerar legend {
      float: left;
      padding: 0;
      font-weight: var(--peso-medio);
    }

    .regenerar .chip {
      background: var(--color-superficie);
    }

    .regenerar .chip[aria-pressed='true'] {
      background: var(--grado);
    }

    .regenerar .boton--secundario {
      background: var(--color-superficie);
    }

    .regenerar .campo input {
      background: var(--color-superficie);
    }

    .sugerencias {
      display: flex;
      flex-wrap: wrap;
      gap: var(--espacio-2);
      clear: both;
    }

    .atajo {
      font-size: var(--texto-xs);
    }

    @media (min-width: 64rem) {
      .pie .boton--ancho {
        inline-size: auto;
      }
    }

    /*
     * Catálogo de reglas/07: "Aprobar" pasa a "Aprobada" descubriéndose de izquierda a derecha con clip-path,
     * con su check. Solo cuando se aprueba sin pasar al siguiente; al cargar ya aprobada, no se mueve.
     */
    .sello {
      display: inline-flex;
      align-items: center;
      gap: var(--espacio-2);
      min-block-size: var(--tactil-minimo);
      padding: 0 var(--espacio-4);
      border-radius: var(--radio-pleno);
      background: var(--color-exito-tinte);
      color: var(--color-exito-tinta);
      font-weight: var(--peso-fuerte);
    }

    .sello.recien {
      animation: descubrir 280ms var(--ease-out) both;
    }

    @keyframes descubrir {
      from {
        clip-path: inset(0 100% 0 0 round var(--radio-pleno));
      }
      to {
        clip-path: inset(0 0 0 0 round var(--radio-pleno));
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .sello.recien {
        animation: none;
      }
    }

    .linea-esqueleto {
      display: block;
      inline-size: 50%;
      block-size: 2rem;
      margin-block: var(--espacio-4);
    }

    .texto-esqueleto {
      display: block;
      inline-size: 100%;
      block-size: 12rem;
    }
  `,
})
export class RevisarObservacion implements OnInit {
  private readonly servicio = inject(CierreService);
  private readonly evaluacion = inject(EvaluacionService);
  private readonly i18n = inject(TranslocoService);
  private readonly router = inject(Router);
  private readonly avisos = inject(AvisosService);
  private readonly pintarPortada = controlarPortada();
  private readonly enfocar = crearEnfoque();
  protected readonly enLinea = this.servicio.enLinea;
  protected readonly calificacion = calificacion;
  protected readonly sugerencias = SUGERENCIAS;
  protected readonly maximo = LONGITUD_TEXTO;
  protected readonly maximoInstruccion = LONGITUD_INSTRUCCION;
  private temporizador: ReturnType<typeof setTimeout> | null = null;

  readonly id = input.required<string>();
  readonly periodoId = input.required<string>();
  readonly alumnoId = input.required<string>();

  private readonly campo = viewChild<ElementRef<HTMLTextAreaElement>>('campo');
  protected readonly grupo = signal<GrupoDto | null>(null);
  protected readonly cierre = signal<CierreDto | null>(null);
  private readonly observaciones = signal<ReadonlyMap<string, ObservacionDto>>(new Map());
  private readonly matriz = signal<MatrizCalificacionesDto | null>(null);
  private readonly todasLasNotas = signal<{ alumnoId: string; notas: NotaDto[] } | null>(null);
  protected readonly estilo = signal<EstiloDto | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly errorAccion = signal<string | null>(null);
  protected readonly trabajando = signal<'aprobar' | 'regenerar' | 'generar' | 'reabrir' | null>(
    null,
  );
  /** El texto en pantalla y si difiere del guardado. */
  protected readonly borrador = signal('');
  private sucio = false;
  protected readonly sugerencia = signal<(typeof SUGERENCIAS)[number] | null>(null);
  protected readonly instruccion = signal('');
  protected readonly recienAprobada = signal(false);

  protected readonly clave = computed(() => {
    const g = this.grupo();
    return g ? claveGrado(g.nivel, g.grado) : undefined;
  });
  protected readonly alumno = computed(
    () => this.cierre()?.alumnos.find((a) => a.alumnoId === this.alumnoId()) ?? null,
  );
  protected readonly observacion = computed(
    () => this.observaciones().get(this.alumnoId()) ?? null,
  );
  protected readonly total = computed(() => this.cierre()?.alumnos.length ?? 0);
  protected readonly lugar = computed(() => vecinos(this.cierre()?.alumnos ?? [], this.alumnoId()));
  protected readonly siguiente = computed(() =>
    siguientePendiente(this.cierre()?.alumnos ?? [], this.alumnoId()),
  );
  protected readonly generando = computed(() => this.observacion()?.estado === 'generando');
  protected readonly aprobada = computed(() => estaAprobada(this.observacion()?.estado ?? ''));
  protected readonly editable = computed(
    () => !!this.cierre() && !this.cierre()!.soloLectura && this.cierre()!.estado === 'en_cierre',
  );
  protected readonly puedeGenerar = computed(
    () => !!this.observacion() && this.observacion()!.estado === 'sin_generar',
  );
  protected readonly restantes = computed(() =>
    Number(this.observacion()?.regeneracionesRestantes ?? 0),
  );
  protected readonly palabras = computed(() => contarPalabras(this.borrador()));
  protected readonly enRango = computed(() => {
    const e = this.estilo();
    const p = this.palabras();
    return !e || (p >= Number(e.palabrasMinimas) && p <= Number(e.palabrasMaximas));
  });
  protected readonly rubros = computed(() => {
    const m = this.matriz();
    const r = m?.resultados.find((x) => x.alumnoId === this.alumnoId());
    return (m?.rubros ?? []).map(({ rubro }) => {
      const promedio = r?.rubros.find((p) => p.rubroId === rubro.id)?.promedio ?? null;
      return {
        id: rubro.id,
        nombre: rubro.nombre,
        peso: Number(rubro.peso),
        promedio: promedio === null ? null : calificacion(promedio),
      };
    });
  });
  protected readonly notas = computed(() => {
    const n = this.todasLasNotas();
    return n?.alumnoId === this.alumnoId()
      ? n.notas
          .filter((x) => x.periodoId === this.periodoId())
          .sort((a, b) => b.fecha.localeCompare(a.fecha))
      : [];
  });
  protected readonly rutaCierre = computed(() => [
    '/grupos',
    this.id(),
    'periodos',
    this.periodoId(),
    'cierre',
  ]);

  constructor() {
    // Al cambiar de alumno: su texto, sus notas y el foco en el nombre.
    effect(() => {
      const alumnoId = this.alumnoId();
      untracked(() => {
        this.sucio = false;
        this.recienAprobada.set(false);
        this.sugerencia.set(null);
        this.instruccion.set('');
        this.errorAccion.set(null);
        this.borrador.set(this.observaciones().get(alumnoId)?.texto ?? '');
        if (this.cierre()) {
          void this.cargarNotas(alumnoId);
          this.vigilar();
          this.enfocar('h1');
        }
      });
    });
    inject(DestroyRef).onDestroy(() => this.detener());
  }

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      const [grupo, cierre, observaciones, matriz, estilo] = await Promise.all([
        this.evaluacion.grupo(this.id()),
        this.servicio.cierre(this.periodoId()),
        this.servicio.observaciones(this.periodoId()),
        this.evaluacion.matriz(this.periodoId()),
        this.servicio.estilo(),
      ]);
      this.pintarPortada(portadaDeGrupo(grupo));
      this.grupo.set(grupo);
      this.matriz.set(matriz);
      this.estilo.set(estilo);
      this.ponerObservaciones(observaciones);
      this.cierre.set(cierre);
      this.borrador.set(this.observacion()?.texto ?? '');
      void this.cargarNotas(this.alumnoId());
      this.vigilar();
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  private async cargarNotas(alumnoId: string): Promise<void> {
    try {
      const notas = await this.evaluacion.notas(alumnoId);
      this.todasLasNotas.set({ alumnoId, notas });
    } catch {
      this.todasLasNotas.set({ alumnoId, notas: [] });
    }
  }

  protected texto(clave: string, datos?: Record<string, unknown>): string {
    return this.i18n.translate(clave, datos);
  }

  protected fecha(valor: string): string {
    return mostrarFecha(valor);
  }

  protected escribir(evento: Event): void {
    this.borrador.set((evento.target as HTMLTextAreaElement).value);
    this.sucio = true;
  }

  /** Al salir del campo, si cambió y tiene texto. */
  protected async guardar(): Promise<void> {
    const o = this.observacion();
    const texto = this.borrador().trim();
    if (!this.sucio || !o || !texto || !this.editable() || !this.enLinea() || this.aprobada()) {
      return;
    }
    this.sucio = false;
    try {
      this.actualizar(await this.servicio.editar(o.id, texto));
    } catch (e) {
      this.sucio = true;
      this.errorAccion.set(codigoDeError(e));
    }
  }

  /** Ctrl+Enter (o Cmd+Enter) aprueba y pasa al siguiente. */
  protected atajo(evento: KeyboardEvent): void {
    if (evento.key === 'Enter' && (evento.ctrlKey || evento.metaKey)) {
      evento.preventDefault();
      void this.aprobarYSeguir();
    }
  }

  protected aMano(): void {
    this.borrador.set('');
    this.sucio = true;
    this.campo()?.nativeElement.focus();
  }

  protected elegirSugerencia(s: (typeof SUGERENCIAS)[number]): void {
    this.sugerencia.update((actual) => (actual === s ? null : s));
  }

  /** RN-043: reemplaza el borrador con otra versión; lo editado a mano se pierde a propósito. */
  protected async regenerar(): Promise<void> {
    const o = this.observacion();
    if (!o) {
      return;
    }
    const partes = [
      this.sugerencia() ? this.texto('revisar.regenerar.instrucciones.' + this.sugerencia()) : '',
      this.instruccion().trim(),
    ].filter(Boolean);
    await this.hacer('regenerar', async () => {
      this.actualizar(await this.servicio.regenerar(o.id, partes.join(', ') || null));
      this.sugerencia.set(null);
      this.instruccion.set('');
      this.vigilar();
    });
  }

  protected async generar(): Promise<void> {
    await this.hacer('generar', async () => {
      const cierre = await this.servicio.generar(this.periodoId(), [this.alumnoId()]);
      this.cierre.set(cierre);
      this.ponerObservaciones(await this.servicio.observaciones(this.periodoId()));
      this.vigilar();
    });
  }

  /** RN-042: guarda y aprueba en una llamada; sigue al próximo sin aprobar, o se queda en "Aprobada". */
  protected async aprobarYSeguir(): Promise<void> {
    const o = this.observacion();
    const texto = this.borrador().trim();
    if (!o || !texto || this.aprobada() || this.trabajando() || !this.editable()) {
      return;
    }
    const siguiente = this.siguiente();
    await this.hacer('aprobar', async () => {
      this.sucio = false;
      this.actualizar(await this.servicio.aprobar(o.id, texto));
      if (siguiente) {
        await this.irA(siguiente.alumnoId);
      } else {
        this.recienAprobada.set(true);
        this.avisos.mostrar(this.texto('revisar.todas'), 'exito');
      }
    });
  }

  protected async reabrir(): Promise<void> {
    const o = this.observacion();
    if (!o) {
      return;
    }
    await this.hacer('reabrir', async () => {
      this.actualizar(await this.servicio.reabrir(o.id));
      this.recienAprobada.set(false);
      this.borrador.set(this.observacion()?.texto ?? '');
      this.enfocar('#texto-observacion');
    });
  }

  /** Guarda lo escrito y cambia de alumno sin volver a la lista. */
  protected async irA(alumnoId: string | undefined): Promise<void> {
    if (!alumnoId) {
      return;
    }
    await this.guardar();
    await this.router.navigate([
      '/grupos',
      this.id(),
      'periodos',
      this.periodoId(),
      'cierre',
      alumnoId,
    ]);
  }

  private async hacer(
    accion: 'aprobar' | 'regenerar' | 'generar' | 'reabrir',
    trabajo: () => Promise<void>,
  ): Promise<void> {
    this.errorAccion.set(null);
    this.trabajando.set(accion);
    try {
      await trabajo();
    } catch (e) {
      this.errorAccion.set(codigoDeError(e));
    } finally {
      this.trabajando.set(null);
    }
  }

  private ponerObservaciones(lista: readonly ObservacionDto[]): void {
    this.observaciones.set(new Map(lista.map((o) => [o.alumnoId, o])));
  }

  /** Una observación nueva del servidor: en el mapa, en el estado de la fila y, si no hay cambios, en pantalla. */
  private actualizar(o: ObservacionDto): void {
    this.observaciones.update((m) => new Map(m).set(o.alumnoId, o));
    this.cierre.update((c) =>
      c
        ? {
            ...c,
            alumnos: c.alumnos.map((a) =>
              a.alumnoId === o.alumnoId
                ? { ...a, estado: o.estado, datosCambiaron: o.datosCambiaron, fallo: o.fallo }
                : a,
            ),
          }
        : c,
    );
    if (o.alumnoId === this.alumnoId() && !this.sucio) {
      this.borrador.set(o.texto ?? '');
    }
  }

  /** Mientras la IA escribe la de este alumno, consulta cada 3 s. */
  private vigilar(): void {
    this.detener();
    if (!this.generando()) {
      return;
    }
    this.temporizador = setTimeout(async () => {
      try {
        const lista = await this.servicio.observaciones(this.periodoId());
        const nueva = lista.find((o) => o.alumnoId === this.alumnoId());
        if (nueva) {
          this.actualizar(nueva);
        }
      } catch {
        // Se vuelve a intentar en la siguiente vuelta.
      }
      this.vigilar();
    }, CONSULTA_MS);
  }

  private detener(): void {
    if (this.temporizador) {
      clearTimeout(this.temporizador);
      this.temporizador = null;
    }
  }
}
