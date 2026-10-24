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
import {
  EquipoDeActividad,
  SIN_ALUMNO,
  composicion,
  conCambios,
  conCeldas,
  llaveCelda,
} from '../../core/sincronizacion/celdas';
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
  MODOS,
  Modo,
  numero,
  textoCelda,
  validarValor,
  valorDeCelda,
} from './matriz';

const RAPIDOS = ['10', '9', '8', '7', '6', '5'] as const;
const PREFIJO_EQUIPO = 'equipo:';

/** Una fila que se captura: un alumno, un equipo completo o un integrante ajustado (RN-115). */
interface Fila {
  readonly clave: string;
  readonly tipo: 'alumno' | 'equipo' | 'integrante';
  readonly numero: string;
  readonly apellidos: string;
  readonly nombres: string;
  readonly etiqueta: string;
  readonly equipo?: EquipoDeActividad;
}

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

            @if (porEquipo()) {
              @if (equipos().length === 0 && activos().length) {
                <div class="aviso aviso--pendiente sin-equipos">
                  <p>{{ t('equipo.sinEquipos') }}</p>
                  <a class="boton boton--secundario" [routerLink]="['/grupos', id(), 'equipos']">
                    <app-icono nombre="equipo" />
                    {{ t('equipo.armar') }}
                  </a>
                </div>
              }
              @for (e of equipos(); track e.equipoId) {
                @let fe = filaEquipo(e);
                <section class="equipo" [attr.aria-label]="fe.etiqueta">
                  <ol class="alumnos">
                    @if (todosAjustados(e)) {
                      <!-- Sin integrantes que sigan al equipo, no hay calificación de equipo que capturar. -->
                      <li class="fila fila--equipo">
                        <span class="alumno">
                          <app-icono class="numero" nombre="equipo" />
                          <span class="nombre"
                            ><span class="apellidos">{{ fe.apellidos }}</span>
                            <span class="dato-suave cifras">{{ fe.nombres }}</span></span
                          >
                        </span>
                        <span class="todos-ajustados dato-suave">{{
                          t('equipo.todosAjustados')
                        }}</span>
                      </li>
                    } @else {
                      <ng-container
                        [ngTemplateOutlet]="fila"
                        [ngTemplateOutletContext]="{ $implicit: fe, baja: false }"
                      />
                    }
                  </ol>
                  <ol class="alumnos integrantes" [attr.aria-label]="t('equipo.integrantes')">
                    @for (a of integrantesDe(e); track a.id) {
                      @if (editandoIntegrante(a.id)) {
                        <ng-container
                          [ngTemplateOutlet]="fila"
                          [ngTemplateOutletContext]="{ $implicit: filaDe(a.id)!, baja: false }"
                        />
                      } @else {
                        <li class="integrante" [attr.data-alumno]="a.id">
                          <button
                            class="alumno"
                            type="button"
                            [disabled]="soloLectura()"
                            [attr.aria-label]="
                              t('equipo.ajustarA', {
                                alumno: nombreDe(a),
                                valor: textoCelda(celdaDe(a.id)) || t('sinCalificacion'),
                              })
                            "
                            (click)="ajustar(a.id)"
                          >
                            <span class="numero cifras">{{ a.numeroLista }}</span>
                            <span class="nombre"
                              ><span class="apellidos">{{ a.apellidos }}</span>
                              {{ a.nombres }}</span
                            >
                            <span class="valor-integrante cifras">{{
                              textoCelda(celdaDe(a.id)) || '—'
                            }}</span>
                            @if (!soloLectura()) {
                              <span class="ajustar dato-suave">{{ t('equipo.ajustar') }}</span>
                            }
                          </button>
                        </li>
                      }
                    }
                  </ol>
                </section>
              }
              @if (sinEquipo().length) {
                <section class="sin-equipo" aria-labelledby="sin-equipo-titulo">
                  <h2 id="sin-equipo-titulo">{{ t('equipo.sinEquipo') }}</h2>
                  <ol class="alumnos">
                    @for (a of sinEquipo(); track a.id) {
                      <ng-container
                        [ngTemplateOutlet]="fila"
                        [ngTemplateOutletContext]="{ $implicit: filaDe(a.id)!, baja: false }"
                      />
                    }
                  </ol>
                </section>
              }
            } @else {
              <ol class="alumnos" [attr.aria-label]="t('lista')">
                @for (a of activos(); track a.id) {
                  <ng-container
                    [ngTemplateOutlet]="fila"
                    [ngTemplateOutletContext]="{ $implicit: filaDe(a.id)!, baja: false }"
                  />
                }
              </ol>
            }

            @if (bajas().length) {
              <section class="bajas" aria-labelledby="bajas-titulo">
                <h2 id="bajas-titulo">{{ t('bajas') }}</h2>
                <ol class="alumnos">
                  @for (a of bajas(); track a.id) {
                    <ng-container
                      [ngTemplateOutlet]="fila"
                      [ngTemplateOutletContext]="{ $implicit: filaAlumno(a), baja: true }"
                    />
                  }
                </ol>
              </section>
            }

            @if (!soloLectura() && activos().length) {
              <div #pieTeclado class="pie-accion teclado-celular solo-celular">
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

      <ng-template #fila let-f let-baja="baja">
        @let celda = celdaDe(f.clave);
        @let fallo = errores().get(f.clave);
        <li
          class="fila"
          [class.activa]="!baja && activo() === f.clave"
          [class.con-criterios]="conCriterios()"
          [class.fila--equipo]="f.tipo === 'equipo'"
          [class.fila--integrante]="f.tipo === 'integrante'"
          [attr.data-alumno]="f.clave"
        >
          <button
            class="alumno"
            type="button"
            [disabled]="baja || soloLectura()"
            [attr.aria-current]="!baja && activo() === f.clave ? 'true' : null"
            (click)="activar(f.clave)"
          >
            @if (f.tipo === 'equipo') {
              <app-icono class="numero" nombre="equipo" />
              <span class="nombre"
                ><span class="apellidos">{{ f.apellidos }}</span>
                <span class="dato-suave cifras">{{ f.nombres }}</span></span
              >
            } @else {
              <span class="numero cifras">{{ f.numero }}</span>
              <span class="nombre"
                ><span class="apellidos">{{ f.apellidos }}</span> {{ f.nombres }}</span
              >
            }
          </button>

          @if (conCriterios()) {
            <div class="criterios-fila" role="group" [attr.aria-label]="f.etiqueta">
              @for (c of rubro()!.criterios; track c.id) {
                @let vc = criterioDe(celda, c.id);
                @if (c.forma === 'cumplimiento') {
                  <div class="criterio">
                    <span
                      class="etiqueta-criterio"
                      [id]="'c-' + f.clave + '-' + c.id"
                      [attr.title]="c.nombre"
                      >{{ c.nombre }}</span
                    >
                    <div
                      class="par"
                      role="group"
                      [attr.aria-labelledby]="'c-' + f.clave + '-' + c.id"
                    >
                      @for (m of marcasCumplimiento; track m) {
                        <button
                          type="button"
                          class="marca marca--chica"
                          [attr.data-marca]="m"
                          [attr.aria-pressed]="vc?.marca === m"
                          [attr.aria-label]="t('marca.' + m)"
                          [disabled]="baja || soloLectura() || bloqueadaPorMarca(celda)"
                          (focus)="activar(f.clave, c.id)"
                          (click)="capturarCriterio(f.clave, c, vc?.marca === m ? '' : m)"
                        >
                          {{ m === 'cumplio' ? '✓' : '✗' }}
                        </button>
                      }
                    </div>
                  </div>
                } @else {
                  <div class="criterio">
                    <label
                      class="etiqueta-criterio"
                      [for]="'c-' + f.clave + '-' + c.id"
                      [attr.title]="c.nombre"
                      >{{ c.nombre }}</label
                    >
                    <input
                      class="celda celda--chica cifras"
                      [id]="'c-' + f.clave + '-' + c.id"
                      [attr.data-campo]="f.clave + ':' + c.id"
                      inputmode="decimal"
                      enterkeyhint="next"
                      autocomplete="off"
                      [class.con-valor]="vc?.valor !== null && vc?.valor !== undefined"
                      [value]="
                        vc?.valor === null || vc?.valor === undefined ? '' : numero(vc.valor)
                      "
                      [readOnly]="baja || soloLectura() || bloqueadaPorMarca(celda)"
                      [attr.aria-invalid]="errorCriterio(f.clave, c.id) ? true : null"
                      [attr.aria-describedby]="
                        errorCriterio(f.clave, c.id) ? 'error-' + f.clave : null
                      "
                      (focus)="activar(f.clave, c.id)"
                      (change)="capturarCriterio(f.clave, c, valor($event))"
                      (keydown.enter)="$event.preventDefault(); siguiente(f.clave, c.id)"
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
            <div class="par" role="group" [attr.aria-label]="f.etiqueta">
              @for (m of marcasCumplimiento; track m) {
                <button
                  type="button"
                  class="marca"
                  [attr.data-marca]="m"
                  [attr.data-campo]="f.clave + ':' + m"
                  [attr.aria-pressed]="celda?.marca === m"
                  [attr.aria-label]="t('marca.' + m)"
                  [disabled]="baja || soloLectura()"
                  (focus)="activar(f.clave)"
                  (click)="capturarMarca(f.clave, celda?.marca === m ? 'ninguna' : m)"
                >
                  {{ m === 'cumplio' ? '✓' : '✗' }}
                </button>
              }
            </div>
          } @else {
            <input
              class="celda cifras"
              [attr.data-campo]="f.clave"
              inputmode="decimal"
              enterkeyhint="next"
              autocomplete="off"
              [attr.aria-label]="t('calificacionDe', { alumno: f.etiqueta })"
              [class.con-valor]="!celdaVacia(celda)"
              [value]="textoCelda(celda)"
              [readOnly]="baja || soloLectura()"
              [attr.aria-invalid]="fallo ? true : null"
              [attr.aria-describedby]="fallo ? 'error-' + f.clave : null"
              (focus)="activar(f.clave); seleccionar($event)"
              (change)="capturarTexto(f.clave, valor($event))"
              (keydown.enter)="
                $event.preventDefault(); capturarTexto(f.clave, valor($event)); siguiente(f.clave)
              "
              (keydown.arrowdown)="$event.preventDefault(); siguiente(f.clave)"
              (keydown.arrowup)="$event.preventDefault(); anterior(f.clave)"
            />
          }

          @if ((forma() === 'cumplimiento' || conCriterios()) && marcaEspecial(celda); as m) {
            <span class="marca-especial">{{ t('marca.' + m) }}</span>
          }

          @if (f.tipo === 'integrante' && !baja) {
            <div class="ajuste">
              @if (celda?.origen === 'ajustada') {
                <span class="ajustado">{{ t('equipo.ajustado') }}</span>
                @if (!soloLectura()) {
                  <button class="enlace" type="button" (click)="quitarAjuste(f.clave)">
                    {{ t('equipo.quitarAjuste') }}
                  </button>
                }
              } @else {
                <span class="dato-suave">{{ t('equipo.ajustando') }}</span>
                <button class="enlace" type="button" (click)="cancelarAjuste(f.clave)">
                  {{ t('equipo.cancelar') }}
                </button>
              }
            </div>
          }

          @if (fallo) {
            <p class="error" [id]="'error-' + f.clave" role="alert">
              {{ texto('errores.' + fallo) }}
            </p>
          }
        </li>
      </ng-template>

      <ng-template #teclado>
        <div class="teclado" role="group" [attr.aria-label]="t('teclado.titulo')">
          <p class="para" aria-live="polite">
            @if (alumnoActivo(); as f) {
              <span class="dato-suave">{{ t('teclado.para') }}</span>
              @if (f.tipo === 'equipo') {
                <span class="nombre-activo"
                  >{{ f.apellidos }}
                  <span class="cifras">({{ f.equipo!.alumnoIds.length }})</span></span
                >
              } @else {
                <span class="numero cifras">{{ f.numero }}</span>
                <span class="nombre-activo">{{ f.apellidos }} {{ f.nombres }}</span>
              }
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
          <fieldset class="eleccion">
            <legend>{{ texto('captura.modo.titulo') }}</legend>
            @for (m of modos; track m) {
              <label>
                <input
                  type="radio"
                  name="editar-modo"
                  [value]="m"
                  [checked]="modoActividad() === m"
                  (change)="modoActividad.set(m)"
                />
                {{ texto('captura.modo.' + m) }}
              </label>
            }
          </fieldset>
          @if (modoActividad() === 'por_equipo' && !matriz()?.equipos?.length) {
            <p class="dato-suave ayuda-modo">
              {{ texto('captura.modo.sinEquipos') }}
              <a [routerLink]="['/grupos', id(), 'equipos']">{{ texto('captura.modo.armar') }}</a>
            </p>
          }
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

    /* Lista angosta (320 px o texto al 200 %): la celda baja de renglón y el nombre conserva el ancho. */
    .alumnos {
      container-type: inline-size;
    }

    @container (max-width: 22rem) {
      .fila {
        grid-template-columns: minmax(0, 1fr);
        row-gap: var(--espacio-2);
      }

      .fila > .par,
      .fila > .celda {
        justify-self: end;
      }
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

    /* Hasta dos renglones: los criterios suelen empezar igual y se distinguen por el final. */
    .etiqueta-criterio {
      display: -webkit-box;
      max-inline-size: 10rem;
      overflow: hidden;
      color: var(--color-texto-suave);
      font-size: var(--texto-xs);
      overflow-wrap: anywhere;
      -webkit-box-orient: vertical;
      -webkit-line-clamp: 2;
      line-clamp: 2;
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

    /* Por equipo: la fila del equipo lleva la celda; sus integrantes cuelgan debajo con su valor como texto. */
    .equipo {
      display: grid;
    }

    .fila--equipo .apellidos {
      font-weight: var(--peso-fuerte);
    }

    .fila--equipo .nombre {
      display: flex;
      flex-wrap: wrap;
      column-gap: var(--espacio-2);
      align-items: baseline;
    }

    .fila--equipo .nombre .dato-suave {
      font-size: var(--texto-sm);
    }

    .fila--equipo app-icono.numero {
      align-self: center;
      color: var(--grado-tinta);
    }

    .integrantes {
      border-top: 0;
    }

    /* La sangría va en el contenido, no en la lista: así la celda de un integrante cabe en su renglón. */
    .integrantes > li > .alumno {
      margin-inline-start: var(--espacio-5);
    }

    .integrantes > li.integrante {
      margin-inline-start: var(--espacio-5);
    }

    .integrantes > li.integrante > .alumno {
      margin-inline-start: 0;
    }

    .integrante {
      border-bottom: 1px solid var(--color-divisoria);
    }

    .integrante .alumno {
      grid-template-columns: 2ch minmax(0, 1fr) auto auto;
      inline-size: 100%;
      color: var(--color-texto-suave);
    }

    .integrante .apellidos {
      font-weight: var(--peso-normal);
    }

    .valor-integrante {
      min-inline-size: 3ch;
      color: var(--color-texto);
      font-weight: var(--peso-medio);
      text-align: end;
    }

    .ajustar {
      font-size: var(--texto-sm);
    }

    .ajuste {
      grid-column: 1 / -1;
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      justify-content: end;
      gap: var(--espacio-1) var(--espacio-3);
      padding-inline: var(--espacio-2);
      font-size: var(--texto-sm);
    }

    .ajuste .enlace {
      display: inline-flex;
      align-items: center;
      min-block-size: var(--tactil-minimo);
    }

    .todos-ajustados {
      font-size: var(--texto-sm);
      text-align: end;
    }

    .ajustado {
      font-weight: var(--peso-medio);
      color: var(--color-aviso-tinta);
    }

    .sin-equipo {
      display: grid;
      gap: var(--espacio-2);
    }

    .sin-equipo h2 {
      font-size: var(--texto-base);
    }

    .sin-equipos {
      display: grid;
      gap: var(--espacio-3);
      justify-items: start;
    }

    .ayuda-modo {
      font-size: var(--texto-sm);
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

      .alumno:hover .apellidos,
      .integrante .alumno:hover .ajustar {
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
  protected readonly modoActividad = signal<Modo>('individual');
  protected readonly modos = MODOS;
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
  /** RN-115: por equipo, la lista va por equipos con sus integrantes y al final los que no tienen equipo. */
  protected readonly porEquipo = computed(() => this.actividad()?.modo === 'por_equipo');
  protected readonly equipos = computed(() => {
    const m = this.matriz();
    return m && this.porEquipo()
      ? composicion(m, this.actividadId()).filter((e) => e.alumnoIds.length > 0)
      : [];
  });
  private readonly enEquipo = computed(() => new Set(this.equipos().flatMap((e) => e.alumnoIds)));
  protected readonly sinEquipo = computed(() =>
    this.activos().filter((a) => !this.enEquipo().has(a.id)),
  );
  /** Integrantes que la docente abrió para ajustar; los ya ajustados se quedan abiertos solos. */
  private readonly ajustando = signal<ReadonlySet<string>>(new Set());
  /** La calificación de cada equipo: la de sus integrantes que la siguen (no ajustados). */
  private readonly celdasEquipo = computed(() => {
    const mapa = new Map<string, CeldaDto | undefined>();
    for (const e of this.equipos()) {
      const celda = e.alumnoIds
        .map((id) => this.celdaAlumno(id))
        .find((c) => c && c.origen !== 'ajustada' && !celdaVacia(c));
      mapa.set(PREFIJO_EQUIPO + e.equipoId, celda);
    }
    return mapa;
  });
  /** Las filas que se capturan, en el orden de la pantalla: el teclado y Enter avanzan por aquí. */
  protected readonly filas = computed(() => {
    const filas: Fila[] = [];
    if (!this.porEquipo()) {
      filas.push(...this.activos().map((a) => this.filaAlumno(a)));
    } else {
      for (const e of this.equipos()) {
        if (!this.todosAjustados(e)) {
          filas.push(this.filaEquipo(e));
        }
        filas.push(
          ...this.integrantesDe(e)
            .filter((a) => this.editandoIntegrante(a.id))
            .map((a) => this.filaAlumno(a, 'integrante')),
        );
      }
      filas.push(...this.sinEquipo().map((a) => this.filaAlumno(a)));
    }
    return filas;
  });
  private readonly filaPorClave = computed(() => new Map(this.filas().map((f) => [f.clave, f])));
  protected readonly alumnoActivo = computed(() => this.filaPorClave().get(this.activo() ?? ''));
  /** Con criterios, los números del teclado van al criterio numérico con foco. */
  protected readonly teclaNumericaBloqueada = computed(() => {
    if (!this.conCriterios()) {
      return false;
    }
    const c = this.rubro()?.criterios.find((x) => x.id === this.criterioActivo());
    return !c || c.forma !== 'numerica';
  });

  private readonly pieTeclado = viewChild<ElementRef<HTMLElement>>('pieTeclado');

  constructor() {
    // El teclado fijo cambia de alto con el texto grande y la rotación: se mide para que la fila activa no
    // quede debajo (scroll-margin de .fila y scroll-padding de la página leen --alto-pie-accion).
    const raiz = document.documentElement;
    let medidor: ResizeObserver | undefined;
    const soltar = () => {
      medidor?.disconnect();
      raiz.style.removeProperty('--alto-pie-accion');
    };
    effect(() => {
      const pie = this.pieTeclado()?.nativeElement;
      soltar();
      if (!pie || typeof ResizeObserver === 'undefined') {
        return;
      }
      medidor = new ResizeObserver(() => {
        const alto = pie.getBoundingClientRect().height;
        if (alto > 0) {
          raiz.style.setProperty('--alto-pie-accion', `${Math.ceil(alto)}px`);
        } else {
          raiz.style.removeProperty('--alto-pie-accion');
        }
      });
      medidor.observe(pie);
    });
    inject(DestroyRef).onDestroy(soltar);
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
          nuevos.set(r.equipoId ? PREFIJO_EQUIPO + r.equipoId : r.alumnoId, r.codigo);
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

  /** Al volver a una celda con valor, escribir lo reemplaza en lugar de pegarse al final. */
  protected seleccionar(evento: Event): void {
    (evento.target as HTMLInputElement).select();
  }

  protected fecha(valor: string): string {
    return mostrarFecha(valor);
  }

  protected nombreDe(a: AlumnoMatrizDto): string {
    return `${a.numeroLista}. ${a.apellidos} ${a.nombres}`;
  }

  protected filaAlumno(a: AlumnoMatrizDto, tipo: Fila['tipo'] = 'alumno'): Fila {
    return {
      clave: a.id,
      tipo,
      numero: String(a.numeroLista),
      apellidos: a.apellidos,
      nombres: a.nombres,
      etiqueta: this.nombreDe(a),
    };
  }

  protected filaEquipo(e: EquipoDeActividad): Fila {
    const cantidad = e.alumnoIds.length;
    const integrantes = this.i18n.translate(
      `captura.equipo.cuantos.${cantidad === 1 ? 'uno' : 'otros'}`,
      { cantidad },
    );
    return {
      clave: PREFIJO_EQUIPO + e.equipoId,
      tipo: 'equipo',
      numero: '',
      apellidos: e.nombre,
      nombres: integrantes,
      etiqueta: `${e.nombre} (${integrantes})`,
      equipo: e,
    };
  }

  protected filaDe(clave: string): Fila | undefined {
    return this.filaPorClave().get(clave);
  }

  protected integrantesDe(e: EquipoDeActividad): AlumnoMatrizDto[] {
    const ids = new Set(e.alumnoIds);
    return this.activos().filter((a) => ids.has(a.id));
  }

  protected editandoIntegrante(alumnoId: string): boolean {
    return this.ajustando().has(alumnoId) || this.celdaAlumno(alumnoId)?.origen === 'ajustada';
  }

  private celdaAlumno(alumnoId: string): CeldaDto | undefined {
    return this.indice()?.celdas.get(llaveCelda({ actividadId: this.actividadId(), alumnoId }));
  }

  /** La celda de una fila: la del alumno o, para un equipo, la que comparten sus integrantes. */
  protected celdaDe(clave: string): CeldaDto | undefined {
    return clave.startsWith(PREFIJO_EQUIPO)
      ? this.celdasEquipo().get(clave)
      : this.celdaAlumno(clave);
  }

  /** Tocar a un integrante abre su campo: lo que capture ahí es solo suyo (RN-115). */
  protected ajustar(alumnoId: string): void {
    this.ajustando.update((s) => new Set(s).add(alumnoId));
    this.activar(alumnoId);
    this.enfocar(`[data-campo="${alumnoId}"], [data-campo^="${alumnoId}:"]`);
  }

  protected todosAjustados(e: EquipoDeActividad): boolean {
    return e.alumnoIds.every((id) => this.celdaAlumno(id)?.origen === 'ajustada');
  }

  /** Cierra el campo de un integrante que se abrió sin cambiarlo. */
  protected cancelarAjuste(alumnoId: string): void {
    this.ajustando.update((s) => {
      const n = new Set(s);
      n.delete(alumnoId);
      return n;
    });
    this.marcarError(alumnoId, null);
    this.enfocar(`[data-alumno="${alumnoId}"] .alumno`);
  }

  /** Vuelve a la calificación del equipo: se vacía su celda y el equipo se manda otra vez. */
  protected quitarAjuste(alumnoId: string): void {
    const e = this.equipos().find((x) => x.alumnoIds.includes(alumnoId));
    if (!e) {
      return;
    }
    const clave = PREFIJO_EQUIPO + e.equipoId;
    const delEquipo = this.celdaDe(clave);
    const cambios = [
      cambio(
        this.actividadId(),
        alumnoId,
        { valor: null, marca: 'ninguna' },
        this.celdaAlumno(alumnoId),
      ),
    ];
    if (delEquipo) {
      cambios.push(
        this.cambioDe(
          clave,
          {
            valor: delEquipo.valor === null ? null : String(delEquipo.valor),
            marca: delEquipo.marca as Marca,
          },
          delEquipo.criterios.map((c) => ({
            criterioId: c.criterioId,
            valor: c.valor,
            marca: c.marca,
          })),
        ),
      );
    }
    this.ajustando.update((s) => {
      const n = new Set(s);
      n.delete(alumnoId);
      return n;
    });
    this.marcarError(alumnoId, null);
    this.encolar(cambios);
    this.activar(clave);
    this.enfocar(`[data-alumno="${alumnoId}"] .alumno`);
  }

  /** El cambio de una fila; el de un equipo va sin alumno y con su `equipoId`. */
  private cambioDe(
    clave: string,
    captura: CapturaCelda,
    criterios: CambioCelda['criterios'] = null,
  ): CambioCelda {
    if (clave.startsWith(PREFIJO_EQUIPO)) {
      return {
        ...cambio(this.actividadId(), SIN_ALUMNO, captura, undefined, criterios),
        equipoId: clave.slice(PREFIJO_EQUIPO.length),
        version: null,
      };
    }
    return cambio(this.actividadId(), clave, captura, this.celdaAlumno(clave), criterios);
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
    const lista = this.filas();
    const desde = despuesDe ? lista.findIndex((f) => f.clave === despuesDe) + 1 : 0;
    return (
      lista.slice(desde).find((f) => celdaVacia(this.celdaDe(f.clave)))?.clave ??
      lista[desde]?.clave ??
      null
    );
  }

  /** Pasa a la fila (o al criterio) siguiente sin animar nada; si había foco en una celda, lo lleva. */
  protected siguiente(alumnoId: string, criterioId?: string): void {
    const lista = this.filas();
    const i = lista.findIndex((f) => f.clave === alumnoId);
    if (criterioId) {
      const numericos = this.rubro()!.criterios.filter((c) => c.forma === 'numerica');
      const j = numericos.findIndex((c) => c.id === criterioId);
      if (j >= 0 && j < numericos.length - 1) {
        this.irA(alumnoId, numericos[j + 1].id);
        return;
      }
      const otro = lista[i + 1];
      if (otro) {
        this.irA(otro.clave, numericos[0]?.id ?? null);
      } else {
        this.activo.set(null);
      }
      return;
    }
    const otro = lista[i + 1];
    if (otro) {
      this.irA(otro.clave, null);
    } else {
      this.activo.set(null);
      // Lista completa: el foco va al título para no perderse en la página.
      this.host.nativeElement.querySelector<HTMLElement>('h1')?.focus();
    }
  }

  protected anterior(alumnoId: string): void {
    const lista = this.filas();
    const i = lista.findIndex((f) => f.clave === alumnoId);
    if (i > 0) {
      this.irA(lista[i - 1].clave, null);
    }
  }

  /** Si el foco ya está en una celda, se mueve a la siguiente; si no, solo cambia la fila activa. */
  private irA(alumnoId: string, criterioId: string | null): void {
    const enCelda = (document.activeElement as HTMLElement | null)?.matches?.('.celda, .marca');
    this.activar(alumnoId, criterioId);
    // En cumplimiento cada marca es su propio campo (`<alumno>:<marca>`): el foco va a la primera.
    const selector = criterioId
      ? `[data-campo="${alumnoId}:${criterioId}"]`
      : `[data-campo="${alumnoId}"], [data-campo^="${alumnoId}:"]`;
    const el = this.host.nativeElement.querySelector<HTMLElement>(selector);
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
    this.encolar([this.cambioDe(alumnoId, captura)]);
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
    this.encolar([this.cambioDe(alumnoId, { valor: null, marca: 'ninguna' }, criterios)]);
  }

  /** "Todos cumplieron": las celdas vacías del grupo quedan en ✓; las que ya tienen algo no se tocan. */
  protected todosCumplieron(): void {
    const cambios = this.filas()
      .filter((f) => f.tipo !== 'integrante' && celdaVacia(this.celdaDe(f.clave)))
      .map((f) => this.cambioDe(f.clave, { valor: null, marca: 'cumplio' }));
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
    this.servicio.capturar(nueva, cambios, 'actividad').catch(() => {
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
    this.modoActividad.set(a.modo as Modo);
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
        modo: this.modoActividad(),
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
