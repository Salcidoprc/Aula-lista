import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { AlumnoMatrizDto } from '../../api/models/alumno-matriz-dto';
import { CambioCelda } from '../../api/models/cambio-celda';
import { CeldaDto } from '../../api/models/celda-dto';
import { CriterioCambio } from '../../api/models/criterio-cambio';
import { CriterioDto } from '../../api/models/criterio-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { MatrizCalificacionesDto } from '../../api/models/matriz-calificaciones-dto';
import { Forma, Marca } from '../../core/calculo/calculo';
import { codigoDeError } from '../../core/errores/problema';
import { conCambios, conCeldas, llaveCelda } from '../../core/sincronizacion/celdas';
import { ColaSincronizacion } from '../../core/sincronizacion/cola-sincronizacion';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { mostrarFecha } from '../../shared/fechas';
import { crearEnfoque, PRIMER_ERROR } from '../../shared/foco';
import { claveGrado } from '../../shared/grados';
import { Hoja } from '../../shared/hoja';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { controlarPortada } from '../../shared/portada/portada.service';
import { portadaDeGrupo } from '../grupos/grupos.service';
import { EvaluacionService } from './evaluacion.service';
import {
  Captura as CapturaCelda,
  celdaVacia,
  cambio,
  dosDecimales,
  indexar,
  interpretar,
  numero,
  textoCelda,
  validarValor,
  valorDeCelda,
} from './matriz';

const RAPIDOS = ['10', '9', '8', '7', '6', '5'] as const;

/**
 * SPEC-03, "Vista por actividad (celular)": la lista del grupo con una celda grande por alumno. Escribir y
 * confirmar pasa al siguiente; el teclado rápido (10 a 5, No entregó, Exento) actúa sobre la fila activa y
 * avanza. Cada celda se guarda al salir, primero en el dispositivo y luego en el servidor, sin botón
 * "Guardar". Nada se anima al avanzar (reglas/07): solo el fondo de la celda cambia al guardarse.
 */
@Component({
  selector: 'app-captura',
  imports: [TranslocoDirective, RouterLink, NgTemplateOutlet, Hoja, Icono, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="pantalla pantalla--ancha"
      [attr.data-grado]="clave() ?? null"
      *transloco="let t; prefix: 'captura'"
    >
      <a class="volver" [routerLink]="['/grupos', id()]" [queryParams]="{ unidad: periodoId() }">
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
      } @else if (!matriz()) {
        <div role="status" [attr.aria-label]="t('cargando')">
          <span class="esqueleto titulo-esqueleto" aria-hidden="true"></span>
          @for (i of [1, 2, 3, 4]; track i) {
            <span class="esqueleto fila-esqueleto" aria-hidden="true"></span>
          }
        </div>
      } @else if (!actividad()) {
        <p class="aviso aviso--error" role="alert">{{ texto('errores.no_encontrado') }}</p>
      } @else {
        <div class="cabeza-pantalla">
          <hgroup>
            <h1 tabindex="-1">{{ actividad()!.nombre }}</h1>
            <p class="dato-suave">
              {{ rubro()!.nombre }}
              @if (actividad()!.fecha; as f) {
                · <span class="cifras">{{ fecha(f) }}</span>
              }
            </p>
          </hgroup>
          @if (!soloLectura()) {
            <div class="acciones-actividad">
              <button
                class="boton-icono"
                type="button"
                (click)="abrirEditar()"
                [attr.aria-label]="t('editar.titulo')"
              >
                <app-icono nombre="editar" />
              </button>
              <button
                class="boton-icono peligro"
                type="button"
                (click)="dialogoBorrar.showModal()"
                [attr.aria-label]="t('borrar.titulo')"
              >
                <app-icono nombre="borrar" />
              </button>
            </div>
          }
        </div>

        <p class="avance cifras">
          {{ t('avance', { capturadas: capturadas(), total: activos().length }) }}
          <span class="progreso" aria-hidden="true">
            <span [style.inline-size.%]="porcentaje()"></span>
          </span>
        </p>

        @if (soloLectura()) {
          <p class="aviso aviso--pendiente">{{ t('soloLectura') }}</p>
        }

        <div class="columnas">
          <div class="principal">
            @if (
              forma() === 'cumplimiento' &&
              !conCriterios() &&
              !soloLectura() &&
              capturadas() < activos().length
            ) {
              <button
                class="boton boton--secundario todos"
                type="button"
                (click)="todosCumplieron()"
              >
                <app-icono nombre="guardado" />
                {{ t('todosCumplieron') }}
              </button>
            }

            @if (activos().length === 0) {
              <p class="dato-suave">{{ t('sinAlumnos') }}</p>
            }

            <ol class="alumnos" [attr.aria-label]="t('lista')">
              @for (a of activos(); track a.id) {
                <ng-container
                  [ngTemplateOutlet]="fila"
                  [ngTemplateOutletContext]="{ $implicit: a, baja: false }"
                />
              }
            </ol>

            @if (bajas().length) {
              <section class="bajas" aria-labelledby="bajas-titulo">
                <h2 id="bajas-titulo">{{ t('bajas') }}</h2>
                <ol class="alumnos">
                  @for (a of bajas(); track a.id) {
                    <ng-container
                      [ngTemplateOutlet]="fila"
                      [ngTemplateOutletContext]="{ $implicit: a, baja: true }"
                    />
                  }
                </ol>
              </section>
            }

            @if (!soloLectura() && activos().length) {
              <div class="pie-accion teclado-celular solo-celular">
                <ng-container [ngTemplateOutlet]="teclado" />
              </div>
            }
          </div>

          @if (!soloLectura() && activos().length) {
            <aside class="lateral solo-escritorio" [attr.aria-label]="t('teclado.titulo')">
              <ng-container [ngTemplateOutlet]="teclado" />
            </aside>
          }
        </div>
      }

      <ng-template #fila let-a let-baja="baja">
        @let celda = celdaDe(a.id);
        @let fallo = errores().get(a.id);
        <li
          class="fila"
          [class.activa]="!baja && activo() === a.id"
          [class.con-criterios]="conCriterios()"
          [attr.data-alumno]="a.id"
        >
          <button
            class="alumno"
            type="button"
            [disabled]="baja || soloLectura()"
            [attr.aria-current]="!baja && activo() === a.id ? 'true' : null"
            (click)="activar(a.id)"
          >
            <span class="numero cifras">{{ a.numeroLista }}</span>
            <span class="nombre"
              ><span class="apellidos">{{ a.apellidos }}</span> {{ a.nombres }}</span
            >
          </button>

          @if (conCriterios()) {
            <div class="criterios-fila" role="group" [attr.aria-label]="nombreDe(a)">
              @for (c of rubro()!.criterios; track c.id) {
                @let vc = criterioDe(celda, c.id);
                @if (c.forma === 'cumplimiento') {
                  <div class="criterio">
                    <span class="etiqueta-criterio" [id]="'c-' + a.id + '-' + c.id">{{
                      c.nombre
                    }}</span>
                    <div class="par" role="group" [attr.aria-labelledby]="'c-' + a.id + '-' + c.id">
                      @for (m of marcasCumplimiento; track m) {
                        <button
                          type="button"
                          class="marca marca--chica"
                          [attr.data-marca]="m"
                          [attr.aria-pressed]="vc?.marca === m"
                          [attr.aria-label]="t('marca.' + m)"
                          [disabled]="baja || soloLectura() || bloqueadaPorMarca(celda)"
                          (focus)="activar(a.id, c.id)"
                          (click)="capturarCriterio(a.id, c, vc?.marca === m ? '' : m)"
                        >
                          {{ m === 'cumplio' ? '✓' : '✗' }}
                        </button>
                      }
                    </div>
                  </div>
                } @else {
                  <div class="criterio">
                    <label class="etiqueta-criterio" [for]="'c-' + a.id + '-' + c.id">{{
                      c.nombre
                    }}</label>
                    <input
                      class="celda celda--chica cifras"
                      [id]="'c-' + a.id + '-' + c.id"
                      [attr.data-campo]="a.id + ':' + c.id"
                      inputmode="decimal"
                      enterkeyhint="next"
                      autocomplete="off"
                      [class.con-valor]="vc?.valor !== null && vc?.valor !== undefined"
                      [value]="
                        vc?.valor === null || vc?.valor === undefined ? '' : numero(vc.valor)
                      "
                      [readOnly]="baja || soloLectura() || bloqueadaPorMarca(celda)"
                      [attr.aria-invalid]="errorCriterio(a.id, c.id) ? true : null"
                      [attr.aria-describedby]="errorCriterio(a.id, c.id) ? 'error-' + a.id : null"
                      (focus)="activar(a.id, c.id)"
                      (change)="capturarCriterio(a.id, c, valor($event))"
                      (keydown.enter)="$event.preventDefault(); siguiente(a.id, c.id)"
                    />
                  </div>
                }
              }
              <div class="resultado">
                <span class="etiqueta-criterio">{{ t('resultado') }}</span>
                <span class="cifras valor-resultado">{{ resultadoDe(celda) }}</span>
              </div>
            </div>
          } @else if (forma() === 'cumplimiento') {
            <div class="par" role="group" [attr.aria-label]="nombreDe(a)">
              @for (m of marcasCumplimiento; track m) {
                <button
                  type="button"
                  class="marca"
                  [attr.data-marca]="m"
                  [attr.data-campo]="a.id + ':' + m"
                  [attr.aria-pressed]="celda?.marca === m"
                  [attr.aria-label]="t('marca.' + m)"
                  [disabled]="baja || soloLectura()"
                  (focus)="activar(a.id)"
                  (click)="capturarMarca(a.id, celda?.marca === m ? 'ninguna' : m)"
                >
                  {{ m === 'cumplio' ? '✓' : '✗' }}
                </button>
              }
            </div>
          } @else {
            <input
              class="celda cifras"
              [attr.data-campo]="a.id"
              inputmode="decimal"
              enterkeyhint="next"
              autocomplete="off"
              [attr.aria-label]="t('calificacionDe', { alumno: nombreDe(a) })"
              [class.con-valor]="!celdaVacia(celda)"
              [value]="textoCelda(celda)"
              [readOnly]="baja || soloLectura()"
              [attr.aria-invalid]="fallo ? true : null"
              [attr.aria-describedby]="fallo ? 'error-' + a.id : null"
              (focus)="activar(a.id)"
              (change)="capturarTexto(a.id, valor($event))"
              (keydown.enter)="
                $event.preventDefault(); capturarTexto(a.id, valor($event)); siguiente(a.id)
              "
              (keydown.arrowdown)="$event.preventDefault(); siguiente(a.id)"
              (keydown.arrowup)="$event.preventDefault(); anterior(a.id)"
            />
          }

          @if ((forma() === 'cumplimiento' || conCriterios()) && marcaEspecial(celda); as m) {
            <span class="marca-especial">{{ t('marca.' + m) }}</span>
          }

          @if (fallo) {
            <p class="error" [id]="'error-' + a.id" role="alert">{{ texto('errores.' + fallo) }}</p>
          }
        </li>
      </ng-template>

      <ng-template #teclado>
        <div class="teclado" role="group" [attr.aria-label]="t('teclado.titulo')">
          <p class="para" aria-live="polite">
            @if (alumnoActivo(); as a) {
              <span class="dato-suave">{{ t('teclado.para') }}</span>
              <span class="numero cifras">{{ a.numeroLista }}</span>
              <span class="nombre-activo">{{ a.apellidos }} {{ a.nombres }}</span>
            } @else {
              <span>{{ t('teclado.listo') }}</span>
            }
          </p>
          <div class="teclas" [class.solo-marcas]="forma() === 'cumplimiento' && !conCriterios()">
            @if (forma() === 'numerica' || conCriterios()) {
              @for (n of rapidos; track n) {
                <button
                  type="button"
                  class="tecla cifras"
                  [disabled]="!alumnoActivo() || teclaNumericaBloqueada()"
                  (pointerdown)="$event.preventDefault()"
                  (click)="tecla(n)"
                >
                  {{ n }}
                </button>
              }
            }
            <button
              type="button"
              class="tecla tecla--marca"
              [disabled]="!alumnoActivo()"
              (pointerdown)="$event.preventDefault()"
              (click)="tecla('no_entrego')"
            >
              {{ t('marca.no_entrego') }}
            </button>
            <button
              type="button"
              class="tecla tecla--marca"
              [disabled]="!alumnoActivo()"
              (pointerdown)="$event.preventDefault()"
              (click)="tecla('exento')"
            >
              {{ t('marca.exento') }}
            </button>
          </div>
        </div>
      </ng-template>

      <app-hoja etiqueta="editar-titulo" (cerrada)="intento.set(false)">
        <h2 id="editar-titulo">{{ texto('captura.editar.titulo') }}</h2>
        <form class="formulario" (submit)="$event.preventDefault(); guardarActividad()" novalidate>
          <div class="campo">
            <label for="editar-nombre">{{ texto('captura.editar.nombre') }}</label>
            <input
              id="editar-nombre"
              maxlength="60"
              autocomplete="off"
              [value]="nombre()"
              (input)="nombre.set(valor($event))"
              [attr.aria-invalid]="intento() && !nombre().trim()"
              [attr.aria-describedby]="intento() && !nombre().trim() ? 'editar-error' : null"
            />
            @if (intento() && !nombre().trim()) {
              <p id="editar-error" class="error">{{ texto('captura.editar.requerido') }}</p>
            }
          </div>
          <div class="campo">
            <label for="editar-fecha">{{ texto('captura.editar.fecha') }}</label>
            <input
              id="editar-fecha"
              type="date"
              [value]="fechaActividad()"
              (input)="fechaActividad.set(valor($event))"
            />
          </div>
          @if (errorActividad(); as codigo) {
            <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
          }
          <button class="boton boton--ancho" type="submit" [appOcupado]="ocupado()">
            {{ texto('captura.editar.guardar') }}
          </button>
        </form>
      </app-hoja>

      <dialog #dialogoBorrar class="dialogo" closedby="any" aria-labelledby="dialogo-borrar-titulo">
        <div class="contenido">
          <h2 id="dialogo-borrar-titulo">
            {{ texto('captura.borrar.titulo') }}
          </h2>
          <p>
            @if (capturadasTotales() > 0) {
              {{ textoCon('captura.borrar.conCalificaciones', { cantidad: capturadasTotales() }) }}
            } @else {
              {{ texto('captura.borrar.texto') }}
            }
          </p>
          @if (errorActividad(); as codigo) {
            <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
          }
          <div class="acciones">
            <button class="boton boton--secundario" type="button" (click)="dialogoBorrar.close()">
              {{ texto('captura.cancelar') }}
            </button>
            <button
              class="boton boton--peligro"
              type="button"
              [appOcupado]="ocupado()"
              (click)="borrarActividad()"
            >
              {{ texto('captura.borrar.accion') }}
            </button>
          </div>
        </div>
      </dialog>
    </div>
  `,
  styles: `
    hgroup {
      display: grid;
      gap: var(--espacio-1);
      min-width: 0;
    }

    hgroup h1 {
      overflow-wrap: anywhere;
    }

    .acciones-actividad {
      display: flex;
      flex: none;
      gap: var(--espacio-1);
    }

    .avance {
      display: grid;
      grid-template-columns: auto minmax(0, 1fr);
      align-items: center;
      gap: var(--espacio-3);
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
    }

    .principal {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      align-content: start;
      gap: var(--espacio-4);
    }

    .todos {
      justify-self: start;
    }

    .alumnos {
      display: grid;
      margin: 0;
      padding: 0;
      list-style: none;
      border-top: 1px solid var(--color-divisoria);
    }

    /* Fila: número y nombre a la izquierda, la celda a la derecha, al alcance del pulgar. */
    .fila {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      align-items: center;
      column-gap: var(--espacio-3);
      padding-block: var(--espacio-2);
      border-bottom: 1px solid var(--color-divisoria);
    }

    /* Al pasar de alumno, la fila queda por encima del teclado fijo (alto en --alto-pie-accion). */
    .fila {
      scroll-margin-block: var(--espacio-4) calc(var(--alto-pie-accion, 0px) + var(--espacio-4));
    }

    .fila.con-criterios {
      grid-template-columns: minmax(0, 1fr);
      row-gap: var(--espacio-2);
    }

    .fila.activa {
      background: var(--grado-tinte);
      box-shadow: inset 3px 0 0 var(--grado);
    }

    .alumno {
      display: grid;
      grid-template-columns: 2ch minmax(0, 1fr);
      align-content: center;
      align-items: baseline;
      gap: var(--espacio-3);
      min-height: var(--tactil-minimo);
      padding: 0 var(--espacio-2);
      border: 0;
      border-radius: var(--radio-md);
      background: none;
      color: inherit;
      font: inherit;
      text-align: start;
      cursor: pointer;
    }

    .alumno:disabled {
      cursor: default;
    }

    .alumno:focus-visible {
      box-shadow: var(--foco);
    }

    .numero {
      color: var(--color-texto-suave);
      text-align: end;
    }

    .nombre {
      overflow-wrap: anywhere;
    }

    .apellidos {
      font-weight: var(--peso-medio);
    }

    /* La celda grande: 56 × 40 como mínimo, cifras tabulares y centradas (DESIGN.md). */
    .celda {
      inline-size: 3.5rem;
      block-size: 2.75rem;
      padding: 0;
      border: 0;
      border-radius: var(--radio-md);
      background: var(--color-superficie-hundida);
      box-shadow: inset 0 -1px 0 var(--color-texto-tenue);
      color: var(--color-texto);
      font-size: var(--texto-lg);
      font-weight: var(--peso-fuerte);
      text-align: center;
      transition: background-color var(--dur-small) var(--ease-out);
    }

    .celda--chica {
      inline-size: 3.25rem;
      block-size: var(--tactil-minimo);
      font-size: var(--texto-base);
    }

    /* Guardada: tinte y tinta del grado (DESIGN.md, fila de captura). */
    .celda.con-valor {
      background: var(--grado-tinte);
      color: var(--grado-tinta);
      box-shadow: none;
    }

    /* El foco entra al instante (regla 07: sin animación del foco); solo la celda guardada hace su fundido. */
    .celda:focus-visible {
      background: var(--color-superficie);
      box-shadow: var(--foco);
      outline: none;
      transition: none;
    }

    .celda[aria-invalid='true'] {
      box-shadow: inset 0 0 0 2px var(--color-peligro);
    }

    .celda:read-only {
      box-shadow: none;
    }

    .par {
      display: flex;
      gap: var(--espacio-2);
    }

    .marca-especial {
      grid-column: 1 / -1;
      justify-self: end;
      font-size: var(--texto-sm);
      font-weight: var(--peso-medio);
      color: var(--color-aviso-tinta);
    }

    .criterios-fila {
      display: flex;
      flex-wrap: wrap;
      align-items: end;
      gap: var(--espacio-3);
      padding-inline-start: calc(2ch + var(--espacio-5));
    }

    .criterio,
    .resultado {
      display: grid;
      gap: var(--espacio-1);
      justify-items: start;
    }

    .etiqueta-criterio {
      max-inline-size: 7rem;
      overflow: hidden;
      color: var(--color-texto-suave);
      font-size: var(--texto-xs);
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .resultado {
      margin-inline-start: auto;
      justify-items: end;
    }

    .valor-resultado {
      display: inline-grid;
      place-items: center;
      min-block-size: var(--tactil-minimo);
      font-size: var(--texto-lg);
      font-weight: var(--peso-fuerte);
      color: var(--grado-tinta);
    }

    .fila .error {
      grid-column: 1 / -1;
      padding-inline: var(--espacio-2);
    }

    .bajas {
      display: grid;
      gap: var(--espacio-2);
    }

    .bajas h2 {
      font-size: var(--texto-base);
      color: var(--color-texto-suave);
    }

    @media (hover: hover) and (pointer: fine) {
      .boton-icono:hover {
        background: var(--color-superficie-hundida);
        color: var(--color-texto);
      }

      .boton-icono.peligro:hover {
        color: var(--color-peligro-tinta);
      }

      .alumno:hover .apellidos {
        text-decoration: underline;
        text-underline-offset: 0.2em;
      }
    }

    .titulo-esqueleto {
      display: block;
      inline-size: 12rem;
      block-size: 2rem;
      margin-block-end: var(--espacio-4);
    }

    .fila-esqueleto {
      display: block;
      block-size: 3.25rem;
      margin-block-end: var(--espacio-2);
    }
  `,
})
export class Captura implements OnInit {
  private readonly servicio = inject(EvaluacionService);
  private readonly cola = inject(ColaSincronizacion);
  private readonly avisos = inject(AvisosService);
  private readonly i18n = inject(TranslocoService);
  private readonly router = inject(Router);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly pintarPortada = controlarPortada();
  private readonly enfocar = crearEnfoque();
  protected readonly numero = numero;
  protected readonly textoCelda = textoCelda;
  protected readonly celdaVacia = celdaVacia;
  protected readonly rapidos = RAPIDOS;
  protected readonly marcasCumplimiento: readonly Marca[] = ['cumplio', 'no_cumplio'];

  readonly id = input.required<string>();
  readonly periodoId = input.required<string>();
  readonly actividadId = input.required<string>();

  protected readonly hoja = viewChild.required(Hoja);
  protected readonly dialogoBorrarRef =
    viewChild.required<ElementRef<HTMLDialogElement>>('dialogoBorrar');

  protected readonly grupo = signal<GrupoDto | null>(null);
  protected readonly matriz = signal<MatrizCalificacionesDto | null>(null);
  protected readonly error = signal<string | null>(null);
  /** La fila (y el criterio) sobre la que actúa el teclado rápido. */
  protected readonly activo = signal<string | null>(null);
  private readonly criterioActivo = signal<string | null>(null);
  /** Errores de captura por alumno: del cliente al validar, o del servidor al rechazar (`CeldaRechazada`). */
  protected readonly errores = signal<ReadonlyMap<string, string>>(new Map());
  private readonly erroresCriterio = signal<ReadonlySet<string>>(new Set());

  protected readonly nombre = signal('');
  protected readonly fechaActividad = signal('');
  protected readonly intento = signal(false);
  protected readonly ocupado = signal(false);
  protected readonly errorActividad = signal<string | null>(null);

  protected readonly clave = computed(() => {
    const g = this.grupo();
    return g ? claveGrado(g.nivel, g.grado) : undefined;
  });
  protected readonly indice = computed(() => {
    const m = this.matriz();
    return m ? indexar(m) : null;
  });
  protected readonly actividad = computed(() =>
    this.indice()?.actividades.find((a) => a.id === this.actividadId()),
  );
  protected readonly rubro = computed(() => this.indice()?.rubroDe.get(this.actividadId()));
  protected readonly forma = computed(() => (this.rubro()?.forma ?? 'numerica') as Forma);
  protected readonly conCriterios = computed(() => (this.rubro()?.criterios.length ?? 0) > 0);
  protected readonly soloLectura = computed(
    () => !!this.matriz()?.soloLectura || !!this.grupo()?.archivadoEn,
  );
  protected readonly activos = computed(() =>
    [...(this.indice()?.alumnosActivos ?? [])].sort(
      (a, b) => Number(a.numeroLista) - Number(b.numeroLista),
    ),
  );
  /** Bajas solo si tienen algo capturado en esta actividad; se ven, no se editan. */
  protected readonly bajas = computed(() =>
    (this.matriz()?.alumnos ?? []).filter((a) => a.dadoDeBaja && !celdaVacia(this.celdaDe(a.id))),
  );
  protected readonly capturadas = computed(
    () => this.activos().filter((a) => !celdaVacia(this.celdaDe(a.id))).length,
  );
  protected readonly capturadasTotales = computed(
    () =>
      (this.matriz()?.celdas ?? []).filter(
        (c) => c.actividadId === this.actividadId() && !celdaVacia(c),
      ).length,
  );
  protected readonly porcentaje = computed(() => {
    const total = this.activos().length;
    return total === 0 ? 0 : Math.round((this.capturadas() / total) * 100);
  });
  protected readonly alumnoActivo = computed(() =>
    this.activos().find((a) => a.id === this.activo()),
  );
  /** Con criterios, los números del teclado van al criterio numérico con foco. */
  protected readonly teclaNumericaBloqueada = computed(() => {
    if (!this.conCriterios()) {
      return false;
    }
    const c = this.rubro()?.criterios.find((x) => x.id === this.criterioActivo());
    return !c || c.forma !== 'numerica';
  });

  constructor() {
    const dejar = this.cola.alConfirmar(({ vigentes, rechazadas }) => {
      const m = this.matriz();
      if (!m) {
        return;
      }
      const propias = new Set(this.indice()?.actividades.map((a) => a.id));
      const deAqui = new Map([...vigentes].filter(([llave]) => propias.has(llave.split(':')[0])));
      this.matriz.set(conCeldas(m, deAqui));
      const nuevos = new Map(this.errores());
      for (const r of rechazadas) {
        if (r.actividadId === this.actividadId()) {
          nuevos.set(r.alumnoId, r.codigo);
        }
      }
      this.errores.set(nuevos);
    });
    inject(DestroyRef).onDestroy(dejar);
  }

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      const [grupo, matriz] = await Promise.all([
        this.servicio.grupo(this.id()),
        this.servicio.matriz(this.periodoId()),
      ]);
      this.grupo.set(grupo);
      this.pintarPortada(portadaDeGrupo(grupo));
      this.matriz.set(matriz);
      this.activo.set(this.primeraSinCaptura());
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  protected textoCon(clave: string, params: Record<string, unknown>): string {
    return this.i18n.translate(clave, params);
  }

  protected valor(evento: Event): string {
    return (evento.target as HTMLInputElement).value;
  }

  protected fecha(valor: string): string {
    return mostrarFecha(valor);
  }

  protected nombreDe(a: AlumnoMatrizDto): string {
    return `${a.numeroLista}. ${a.apellidos} ${a.nombres}`;
  }

  protected celdaDe(alumnoId: string): CeldaDto | undefined {
    return this.indice()?.celdas.get(llaveCelda({ actividadId: this.actividadId(), alumnoId }));
  }

  protected criterioDe(celda: CeldaDto | undefined, criterioId: string) {
    return celda?.criterios.find((c) => c.criterioId === criterioId);
  }

  protected resultadoDe(celda: CeldaDto | undefined): string {
    const r = this.rubro();
    return r ? dosDecimales(valorDeCelda(r, celda)) : '—';
  }

  /** "No entregó" y "Exento" son de la actividad completa (RN-112): bloquean los criterios. */
  protected bloqueadaPorMarca(celda: CeldaDto | undefined): boolean {
    return celda?.marca === 'no_entrego' || celda?.marca === 'exento';
  }

  protected marcaEspecial(celda: CeldaDto | undefined): Marca | null {
    return this.bloqueadaPorMarca(celda) ? (celda!.marca as Marca) : null;
  }

  protected errorCriterio(alumnoId: string, criterioId: string): boolean {
    return this.erroresCriterio().has(`${alumnoId}:${criterioId}`);
  }

  protected activar(alumnoId: string, criterioId: string | null = null): void {
    this.activo.set(alumnoId);
    this.criterioActivo.set(criterioId);
  }

  private primeraSinCaptura(despuesDe?: string): string | null {
    const lista = this.activos();
    const desde = despuesDe ? lista.findIndex((a) => a.id === despuesDe) + 1 : 0;
    return (
      lista.slice(desde).find((a) => celdaVacia(this.celdaDe(a.id)))?.id ?? lista[desde]?.id ?? null
    );
  }

  /** Pasa a la fila (o al criterio) siguiente sin animar nada; si había foco en una celda, lo lleva. */
  protected siguiente(alumnoId: string, criterioId?: string): void {
    const lista = this.activos();
    const i = lista.findIndex((a) => a.id === alumnoId);
    if (criterioId) {
      const numericos = this.rubro()!.criterios.filter((c) => c.forma === 'numerica');
      const j = numericos.findIndex((c) => c.id === criterioId);
      if (j >= 0 && j < numericos.length - 1) {
        this.irA(alumnoId, numericos[j + 1].id);
        return;
      }
      const otro = lista[i + 1];
      if (otro) {
        this.irA(otro.id, numericos[0]?.id ?? null);
      } else {
        this.activo.set(null);
      }
      return;
    }
    const otro = lista[i + 1];
    if (otro) {
      this.irA(otro.id, null);
    } else {
      this.activo.set(null);
      (document.activeElement as HTMLElement | null)?.blur();
    }
  }

  protected anterior(alumnoId: string): void {
    const lista = this.activos();
    const i = lista.findIndex((a) => a.id === alumnoId);
    if (i > 0) {
      this.irA(lista[i - 1].id, null);
    }
  }

  /** Si el foco ya está en una celda, se mueve a la siguiente; si no, solo cambia la fila activa. */
  private irA(alumnoId: string, criterioId: string | null): void {
    const enCelda = (document.activeElement as HTMLElement | null)?.matches?.('.celda, .marca');
    this.activar(alumnoId, criterioId);
    const campo = criterioId ? `${alumnoId}:${criterioId}` : alumnoId;
    const el = this.host.nativeElement.querySelector<HTMLElement>(`[data-campo="${campo}"]`);
    if (enCelda && el) {
      el.focus({ preventScroll: true });
    }
    // La fila respeta su scroll-margin: no queda escondida tras el teclado fijo del celular.
    this.host.nativeElement
      .querySelector<HTMLElement>(`[data-alumno="${alumnoId}"]`)
      ?.scrollIntoView({ block: 'nearest' });
  }

  private marcarError(alumnoId: string, codigo: string | null): void {
    const nuevos = new Map(this.errores());
    if (codigo) {
      nuevos.set(alumnoId, codigo);
    } else {
      nuevos.delete(alumnoId);
    }
    this.errores.set(nuevos);
  }

  /** Lo escrito en la celda (número, NE o EX). Si no cambió, no se encola nada. */
  protected capturarTexto(alumnoId: string, texto: string): void {
    const interpretado = interpretar(texto, this.forma());
    if (interpretado === 'marca_invalida') {
      this.marcarError(alumnoId, 'marca_invalida');
      return;
    }
    this.capturar(alumnoId, interpretado);
  }

  protected capturarMarca(alumnoId: string, marca: Marca): void {
    this.capturar(alumnoId, { valor: null, marca });
    if (marca !== 'ninguna') {
      this.siguiente(alumnoId);
    }
  }

  private capturar(alumnoId: string, captura: CapturaCelda): void {
    const r = this.rubro();
    if (!r) {
      return;
    }
    const problema = validarValor(captura.valor, r.califMinima);
    if (problema) {
      this.marcarError(alumnoId, problema);
      this.enfocar(`[data-campo="${alumnoId}"]`);
      return;
    }
    this.marcarError(alumnoId, null);
    const anterior = this.celdaDe(alumnoId);
    if (
      (anterior?.marca ?? 'ninguna') === captura.marca &&
      String(anterior?.valor ?? '') === String(captura.valor ?? '') &&
      (anterior?.criterios.length ?? 0) === 0
    ) {
      return;
    }
    this.encolar([cambio(this.actividadId(), alumnoId, captura, anterior)]);
  }

  /** Un criterio cambió: se manda la celda con todos sus criterios (RN-112). */
  protected capturarCriterio(alumnoId: string, criterio: CriterioDto, texto: string): void {
    const llave = `${alumnoId}:${criterio.id}`;
    const forma = criterio.forma as Forma;
    let nuevo: CriterioCambio | null;
    if (texto === '') {
      nuevo = null;
    } else if (forma === 'cumplimiento') {
      nuevo = { criterioId: criterio.id, valor: null, marca: texto };
    } else {
      const valor = texto.trim().replace(',', '.');
      if (validarValor(valor, criterio.califMinima)) {
        this.erroresCriterio.update((s) => new Set(s).add(llave));
        this.marcarError(alumnoId, validarValor(valor, criterio.califMinima));
        return;
      }
      nuevo = { criterioId: criterio.id, valor, marca: 'ninguna' };
    }
    this.erroresCriterio.update((s) => {
      const n = new Set(s);
      n.delete(llave);
      return n;
    });
    this.marcarError(alumnoId, null);
    const anterior = this.celdaDe(alumnoId);
    const criterios = [
      ...(anterior?.criterios ?? [])
        .filter((c) => c.criterioId !== criterio.id)
        .map((c) => ({ criterioId: c.criterioId, valor: c.valor, marca: c.marca })),
      ...(nuevo ? [nuevo] : []),
    ];
    const actual = this.criterioDe(anterior, criterio.id);
    if (
      String(actual?.valor ?? '') === String(nuevo?.valor ?? '') &&
      (actual?.marca ?? '') === (nuevo?.marca ?? '')
    ) {
      return;
    }
    this.encolar([
      cambio(this.actividadId(), alumnoId, { valor: null, marca: 'ninguna' }, anterior, criterios),
    ]);
  }

  /** "Todos cumplieron": las celdas vacías del grupo quedan en ✓; las que ya tienen algo no se tocan. */
  protected todosCumplieron(): void {
    const cambios = this.activos()
      .filter((a) => celdaVacia(this.celdaDe(a.id)))
      .map((a) =>
        cambio(this.actividadId(), a.id, { valor: null, marca: 'cumplio' }, this.celdaDe(a.id)),
      );
    if (cambios.length) {
      this.encolar(cambios);
    }
    this.activo.set(null);
  }

  /** Teclado rápido: número o marca para la fila activa, y pasa a la siguiente. */
  protected tecla(valor: (typeof RAPIDOS)[number] | 'no_entrego' | 'exento'): void {
    const alumnoId = this.activo();
    if (!alumnoId) {
      return;
    }
    if (valor === 'no_entrego' || valor === 'exento') {
      // Tocar otra vez la misma marca la quita: así se deshace sin tecla de borrar.
      if (this.celdaDe(alumnoId)?.marca === valor) {
        this.capturar(alumnoId, { valor: null, marca: 'ninguna' });
        return;
      }
      this.capturar(alumnoId, { valor: null, marca: valor });
      this.siguiente(alumnoId);
      return;
    }
    const criterioId = this.criterioActivo();
    if (this.conCriterios()) {
      const c = this.rubro()!.criterios.find((x) => x.id === criterioId);
      if (c) {
        this.capturarCriterio(alumnoId, c, valor);
        this.siguiente(alumnoId, c.id);
      }
      return;
    }
    this.capturar(alumnoId, { valor, marca: 'ninguna' });
    if (!this.errores().has(alumnoId)) {
      this.siguiente(alumnoId);
    }
  }

  /** Se ve al instante; se guarda en el dispositivo y la cola lo manda (RF-037). */
  private encolar(cambios: CambioCelda[]): void {
    const m = this.matriz();
    if (!m) {
      return;
    }
    const nueva = conCambios(m, cambios);
    this.matriz.set(nueva);
    this.servicio.capturar(nueva, cambios).catch(() => {
      this.avisos.mostrar(this.i18n.translate('captura.noSeGuardo'), 'error');
    });
  }

  protected abrirEditar(): void {
    const a = this.actividad();
    if (!a) {
      return;
    }
    this.nombre.set(a.nombre);
    this.fechaActividad.set(a.fecha ?? '');
    this.intento.set(false);
    this.errorActividad.set(null);
    this.hoja().abrir();
  }

  protected async guardarActividad(): Promise<void> {
    this.intento.set(true);
    this.errorActividad.set(null);
    if (!this.nombre().trim()) {
      this.enfocar(PRIMER_ERROR);
      return;
    }
    this.ocupado.set(true);
    try {
      const editada = await this.servicio.editarActividad(this.actividadId(), {
        nombre: this.nombre().trim(),
        fecha: this.fechaActividad() || null,
      });
      const m = this.matriz()!;
      this.matriz.set({
        ...m,
        rubros: m.rubros.map((r) => ({
          ...r,
          actividades: r.actividades.map((a) => (a.id === editada.id ? editada : a)),
        })),
      });
      this.hoja().cerrar();
      this.enfocar('h1');
    } catch (e) {
      this.errorActividad.set(codigoDeError(e));
    } finally {
      this.ocupado.set(false);
    }
  }

  /** Siempre se confirma; con calificaciones, el diálogo dice cuántas se borran (SPEC-03, casos borde). */
  protected async borrarActividad(): Promise<void> {
    this.ocupado.set(true);
    this.errorActividad.set(null);
    try {
      await this.servicio.borrarActividad(this.actividadId(), true);
      this.dialogoBorrarRef().nativeElement.close();
      this.avisos.mostrar(this.i18n.translate('captura.borrar.listo'));
      await this.router.navigate(['/grupos', this.id()], {
        queryParams: { unidad: this.periodoId() },
      });
    } catch (e) {
      this.errorActividad.set(codigoDeError(e));
    } finally {
      this.ocupado.set(false);
    }
  }
}
