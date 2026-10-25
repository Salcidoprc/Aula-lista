import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
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
import { GrupoDto } from '../../api/models/grupo-dto';
import { PeriodoDto } from '../../api/models/periodo-dto';
import { RubrosDelPeriodoDto } from '../../api/models/rubros-del-periodo-dto';
import { TipoRubroDto } from '../../api/models/tipo-rubro-dto';
import { Forma } from '../../core/calculo/calculo';
import { comparar, formatear, fraccion } from '../../core/calculo/decimal';
import { codigoDeError } from '../../core/errores/problema';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { crearEnfoque, PRIMER_ERROR } from '../../shared/foco';
import { claveGrado, nombreGrupo } from '../../shared/grados';
import { Hoja } from '../../shared/hoja';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { ConCambiosSinGuardar } from '../../shared/rutas';
import { controlarPortada } from '../../shared/portada/portada.service';
import { GruposService, portadaDeGrupo } from '../grupos/grupos.service';
import {
  CRITERIOS_MAXIMO,
  CriterioBorrador,
  ErrorBorrador,
  MAXIMO_RUBROS,
  RubroBorrador,
  aEntrada,
  criterioNuevo,
  deRubros,
  rubroNuevo,
  sumaPesos,
  validar,
} from './borrador-rubros';
import { EvaluacionService } from './evaluacion.service';
import { numero } from './matriz';

/** De dónde se pueden copiar rubros (RF-031): otra unidad de este grupo o de otro grupo activo. */
interface Fuente {
  readonly periodoId: string;
  readonly etiqueta: string;
}

/**
 * SPEC-03, "Rubros del periodo": editor por bloques. Cada rubro con nombre libre, tipo del catálogo, peso,
 * forma de calificar y mínima (RN-110, RN-118); con criterios, de 2 a 10 que suman 100 (RN-111). La suma de
 * pesos está siempre a la vista (RN-031) y se guarda aunque no sume 100: es un borrador hasta el cierre.
 */
@Component({
  selector: 'app-rubros',
  imports: [TranslocoDirective, RouterLink, NgTemplateOutlet, Hoja, Icono, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="pantalla pantalla--ancha"
      [attr.data-grado]="clave() ?? null"
      *transloco="let t; prefix: 'rubros'"
    >
      <a class="volver" [routerLink]="['/grupos', id()]" [queryParams]="{ unidad: periodoId() }">
        <app-icono nombre="volver" />
        {{ t('volver') }}
      </a>

      <hgroup>
        <h1 tabindex="-1">{{ t('titulo') }}</h1>
        @if (periodo(); as p) {
          <p class="dato-suave">{{ nombreGrupo() }} · {{ p.nombre }}</p>
        }
      </hgroup>

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (!servidor()) {
        <div class="bloques" role="status" [attr.aria-label]="t('cargando')">
          @for (i of [1, 2]; track i) {
            <span class="esqueleto bloque-esqueleto" aria-hidden="true"></span>
          }
        </div>
      } @else {
        @if (!editable()) {
          <p class="aviso aviso--pendiente">
            {{ grupo()?.archivadoEn ? t('archivado') : t('bloqueado.' + servidor()!.estado) }}
          </p>
        }

        <form class="columnas" (submit)="$event.preventDefault(); guardar()" novalidate>
          <div class="principal">
            @if (editable() && servidor()!.rubros.length === 0 && borrador().length === 0) {
              <section class="vacio" aria-labelledby="vacio-titulo">
                <h2 id="vacio-titulo">{{ t('vacio.titulo') }}</h2>
                <p class="dato-suave">{{ t('vacio.texto') }}</p>
                <div class="acciones-vacio">
                  <button class="boton" type="button" (click)="agregar()">
                    <app-icono nombre="mas" />
                    {{ t('agregar') }}
                  </button>
                  <button class="boton boton--secundario" type="button" (click)="abrirCopiar()">
                    <app-icono nombre="copiar" />
                    {{ t('copiar.abrir') }}
                  </button>
                </div>
              </section>
            }

            <fieldset class="bloques" [disabled]="!editable()">
              <legend class="solo-lector">{{ t('titulo') }}</legend>
              @for (r of borrador(); track r.clave; let i = $index) {
                <section class="bloque" [attr.aria-labelledby]="r.clave + '-titulo'">
                  <header class="cabeza-bloque">
                    <h2 [id]="r.clave + '-titulo'">
                      <span class="numero cifras" aria-hidden="true">{{ i + 1 }}</span>
                      {{ r.nombre.trim() || t('sinNombre') }}
                    </h2>
                    @if (editable()) {
                      <button
                        class="boton-icono peligro"
                        type="button"
                        (click)="quitar(i)"
                        [attr.aria-label]="t('quitar', { nombre: r.nombre || i + 1 })"
                      >
                        <app-icono nombre="borrar" />
                      </button>
                    }
                  </header>

                  @if (r.tieneCalificaciones) {
                    <p class="ayuda-bloque dato-suave">{{ t('conCalificaciones') }}</p>
                  }

                  <div class="rejilla">
                    <div class="campo campo--nombre">
                      <label [for]="r.clave + '-nombre'">{{ t('nombre') }}</label>
                      <input
                        [id]="r.clave + '-nombre'"
                        maxlength="40"
                        autocomplete="off"
                        [attr.list]="'sugeridos-' + r.tipoRubroId"
                        [value]="r.nombre"
                        (input)="cambiar(i, { nombre: valor($event) })"
                        [attr.aria-invalid]="falla(r.clave + '-nombre')"
                        [attr.aria-describedby]="
                          falla(r.clave + '-nombre') ? r.clave + '-nombre-error' : null
                        "
                      />
                      <ng-container
                        [ngTemplateOutlet]="mensaje"
                        [ngTemplateOutletContext]="{ campo: r.clave + '-nombre' }"
                      />
                    </div>

                    <div class="campo">
                      <label [for]="r.clave + '-tipo'">{{ t('tipo') }}</label>
                      <span class="selector">
                        <select
                          [id]="r.clave + '-tipo'"
                          [value]="r.tipoRubroId"
                          (change)="cambiarTipo(i, valor($event))"
                          [attr.aria-invalid]="falla(r.clave + '-tipo')"
                        >
                          @for (tipo of opcionesTipo(r.tipoRubroId); track tipo.id) {
                            <option
                              [value]="tipo.id"
                              [selected]="tipo.id === r.tipoRubroId"
                              [textContent]="
                                esInactivo(tipo.id)
                                  ? t('tipoDesactivado', { nombre: tipo.nombre })
                                  : tipo.nombre
                              "
                            ></option>
                          }
                        </select>
                      </span>
                    </div>

                    <div class="campo campo--corto">
                      <label [for]="r.clave + '-peso'">{{ t('peso') }}</label>
                      <span class="con-sufijo">
                        <input
                          [id]="r.clave + '-peso'"
                          class="cifras"
                          inputmode="decimal"
                          autocomplete="off"
                          [value]="r.peso"
                          (input)="cambiar(i, { peso: valor($event) })"
                          [attr.aria-invalid]="falla(r.clave + '-peso')"
                          [attr.aria-describedby]="
                            falla(r.clave + '-peso') ? r.clave + '-peso-error' : null
                          "
                        />
                        <span class="sufijo" aria-hidden="true">%</span>
                      </span>
                      <ng-container
                        [ngTemplateOutlet]="mensaje"
                        [ngTemplateOutletContext]="{ campo: r.clave + '-peso' }"
                      />
                    </div>

                    @if (!esAsistencia(r)) {
                      <div class="campo campo--corto">
                        <label [for]="r.clave + '-minima'">{{ t('minima') }}</label>
                        <input
                          [id]="r.clave + '-minima'"
                          class="cifras"
                          inputmode="decimal"
                          autocomplete="off"
                          [value]="r.califMinima"
                          (input)="cambiar(i, { califMinima: valor($event) })"
                          [attr.aria-invalid]="falla(r.clave + '-minima')"
                          [attr.aria-describedby]="
                            falla(r.clave + '-minima')
                              ? r.clave + '-minima-error'
                              : r.clave + '-minima-ayuda'
                          "
                        />
                        <p class="ayuda" [id]="r.clave + '-minima-ayuda'">{{ t('minimaAyuda') }}</p>
                        <ng-container
                          [ngTemplateOutlet]="mensaje"
                          [ngTemplateOutletContext]="{ campo: r.clave + '-minima' }"
                        />
                      </div>
                    }
                  </div>

                  @if (esAsistencia(r)) {
                    <p class="dato-suave">{{ t('asistencia') }}</p>
                  } @else {
                    @if (!r.conCriterios) {
                      <ng-container
                        [ngTemplateOutlet]="forma"
                        [ngTemplateOutletContext]="{
                          nombre: r.clave + '-forma',
                          actual: r.forma,
                          bloqueada: r.tieneCalificaciones,
                          elegir: elegirFormaRubro(i),
                        }"
                      />
                    }

                    <label class="casilla">
                      <input
                        type="checkbox"
                        [checked]="r.conCriterios"
                        [disabled]="r.tieneCalificaciones"
                        (change)="alternarCriterios(i, marcado($event))"
                        [attr.aria-describedby]="r.clave + '-criterios-ayuda'"
                      />
                      <span>
                        {{ t('conCriterios') }}
                        <span class="ayuda" [id]="r.clave + '-criterios-ayuda'">
                          {{ t('conCriteriosAyuda') }}
                        </span>
                      </span>
                    </label>

                    @if (r.conCriterios) {
                      <div
                        class="criterios"
                        [attr.aria-labelledby]="r.clave + '-criterios-titulo'"
                        role="group"
                      >
                        <div class="cabeza-criterios">
                          <h3 [id]="r.clave + '-criterios-titulo'">{{ t('criterios') }}</h3>
                          <span
                            class="suma-criterios cifras"
                            [class.incompleta]="!criteriosSumanCien(r)"
                            >{{ t('sumaCriterios', { suma: suma(r.criterios) }) }}</span
                          >
                        </div>
                        <ol class="lista-criterios">
                          @for (c of r.criterios; track c.clave; let j = $index) {
                            <li class="criterio">
                              <div class="campo campo--nombre">
                                <label [for]="c.clave + '-nombre'">{{
                                  t('criterio', { n: j + 1 })
                                }}</label>
                                <input
                                  [id]="c.clave + '-nombre'"
                                  maxlength="40"
                                  autocomplete="off"
                                  [value]="c.nombre"
                                  (input)="cambiarCriterio(i, j, { nombre: valor($event) })"
                                  [attr.aria-invalid]="falla(c.clave + '-nombre')"
                                  [attr.aria-describedby]="
                                    falla(c.clave + '-nombre') ? c.clave + '-nombre-error' : null
                                  "
                                />
                                <ng-container
                                  [ngTemplateOutlet]="mensaje"
                                  [ngTemplateOutletContext]="{ campo: c.clave + '-nombre' }"
                                />
                              </div>
                              <div class="campo campo--corto">
                                <label [for]="c.clave + '-peso'">{{ t('peso') }}</label>
                                <span class="con-sufijo">
                                  <input
                                    [id]="c.clave + '-peso'"
                                    class="cifras"
                                    inputmode="decimal"
                                    autocomplete="off"
                                    [value]="c.peso"
                                    (input)="cambiarCriterio(i, j, { peso: valor($event) })"
                                    [attr.aria-invalid]="falla(c.clave + '-peso')"
                                    [attr.aria-describedby]="
                                      falla(c.clave + '-peso') ? c.clave + '-peso-error' : null
                                    "
                                  />
                                  <span class="sufijo" aria-hidden="true">%</span>
                                </span>
                                <ng-container
                                  [ngTemplateOutlet]="mensaje"
                                  [ngTemplateOutletContext]="{ campo: c.clave + '-peso' }"
                                />
                              </div>
                              <div class="campo campo--corto">
                                <label [for]="c.clave + '-minima'">{{ t('minima') }}</label>
                                <input
                                  [id]="c.clave + '-minima'"
                                  class="cifras"
                                  inputmode="decimal"
                                  autocomplete="off"
                                  [value]="c.califMinima"
                                  (input)="cambiarCriterio(i, j, { califMinima: valor($event) })"
                                  [attr.aria-invalid]="falla(c.clave + '-minima')"
                                  [attr.aria-describedby]="
                                    falla(c.clave + '-minima') ? c.clave + '-minima-error' : null
                                  "
                                />
                                <ng-container
                                  [ngTemplateOutlet]="mensaje"
                                  [ngTemplateOutletContext]="{ campo: c.clave + '-minima' }"
                                />
                              </div>
                              <div class="forma-criterio">
                                <ng-container
                                  [ngTemplateOutlet]="forma"
                                  [ngTemplateOutletContext]="{
                                    nombre: c.clave + '-forma',
                                    actual: c.forma,
                                    bloqueada: c.tieneCalificaciones,
                                    elegir: elegirFormaCriterio(i, j),
                                  }"
                                />
                              </div>
                              @if (editable() && r.criterios.length > 2) {
                                <button
                                  class="boton-icono peligro quitar-criterio"
                                  type="button"
                                  (click)="quitarCriterio(i, j)"
                                  [attr.aria-label]="
                                    t('quitarCriterio', { nombre: c.nombre || j + 1 })
                                  "
                                >
                                  <app-icono nombre="borrar" />
                                </button>
                              }
                            </li>
                          }
                        </ol>
                        @if (errores().get(r.clave + '-criterios'); as e) {
                          <p class="error" [id]="r.clave + '-criterios-error'" role="alert">
                            {{ t('errores.' + e) }}
                          </p>
                        }
                        @if (editable() && r.criterios.length < maximoCriterios) {
                          <button
                            class="enlace enlace--solo"
                            type="button"
                            (click)="agregarCriterio(i)"
                          >
                            {{ t('agregarCriterio') }}
                          </button>
                        }
                      </div>
                    }
                  }
                </section>
              }
            </fieldset>

            @if (editable() && borrador().length > 0 && borrador().length < maximoRubros) {
              <button class="boton boton--secundario agregar" type="button" (click)="agregar()">
                <app-icono nombre="mas" />
                {{ t('agregar') }}
              </button>
            }
            @if (borrador().length >= maximoRubros) {
              <p class="dato-suave">{{ t('maximo', { maximo: maximoRubros }) }}</p>
            }

            @if (borrador().length > 0 || hayCambios()) {
              <div class="pie-accion solo-celular">
                <ng-container [ngTemplateOutlet]="pie" />
              </div>
            }
          </div>

          @if (borrador().length > 0 || hayCambios()) {
            <aside class="lateral solo-escritorio" aria-labelledby="resumen-rubros">
              <h2 id="resumen-rubros">{{ periodo()?.nombre }}</h2>
              <ng-container [ngTemplateOutlet]="pie" />
            </aside>
          }
        </form>
      }

      <ng-template #mensaje let-campo="campo">
        @if (errores().get(campo); as e) {
          <p class="error" [id]="campo + '-error'">{{ t('errores.' + e) }}</p>
        }
      </ng-template>

      <ng-template
        #forma
        let-nombre="nombre"
        let-actual="actual"
        let-bloqueada="bloqueada"
        let-elegir="elegir"
      >
        <fieldset class="eleccion eleccion--forma" [disabled]="bloqueada">
          <legend>{{ t('forma.titulo') }}</legend>
          @for (f of formas; track f) {
            <label>
              <input
                type="radio"
                [name]="nombre"
                [value]="f"
                [checked]="actual === f"
                (change)="elegir(f)"
              />
              {{ t('forma.' + f) }}
            </label>
          }
        </fieldset>
      </ng-template>

      <ng-template #pie>
        <div class="suma" [class.incompleta]="!sumanCien()">
          <span class="cifras">{{ t('pesos', { suma: sumaTexto() }) }}</span>
          <span class="progreso" aria-hidden="true">
            <span [style.inline-size.%]="sumaBarra()"></span>
          </span>
          @if (!sumanCien()) {
            <span class="nota">{{ t('pesosIncompletos') }}</span>
          }
        </div>
        @if (errorGuardar(); as codigo) {
          <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
        }
        @if (editable()) {
          <button class="boton boton--ancho" type="submit" [appOcupado]="guardando()">
            {{ t('guardar') }}
          </button>
        }
      </ng-template>

      @for (tipo of tipos(); track tipo.id) {
        <datalist [id]="'sugeridos-' + tipo.id">
          @for (n of tipo.nombresSugeridos; track n) {
            <option [value]="n"></option>
          }
        </datalist>
      }

      <dialog #dialogoBorrar class="dialogo" closedby="any" aria-labelledby="dialogo-borrar-titulo">
        <div class="contenido">
          <h2 id="dialogo-borrar-titulo">{{ t('confirmarBorrado.titulo') }}</h2>
          <p>{{ t('confirmarBorrado.texto') }}</p>
          <div class="acciones">
            <button class="boton boton--secundario" type="button" (click)="dialogoBorrar.close()">
              {{ t('cancelar') }}
            </button>
            <button
              class="boton boton--peligro"
              type="button"
              [appOcupado]="guardando()"
              (click)="guardar(true)"
            >
              {{ t('confirmarBorrado.accion') }}
            </button>
          </div>
        </div>
      </dialog>

      <dialog
        #dialogoSalir
        class="dialogo"
        closedby="any"
        aria-labelledby="dialogo-salir-titulo"
        (close)="decidirSalida(dialogoSalir.returnValue === 'salir')"
      >
        <form class="contenido" method="dialog">
          <h2 id="dialogo-salir-titulo">{{ t('salir.titulo') }}</h2>
          <p>{{ t('salir.texto') }}</p>
          <div class="acciones">
            <button class="boton boton--secundario" value="quedarse">
              {{ t('salir.quedarse') }}
            </button>
            <button class="boton boton--peligro" value="salir">{{ t('salir.accion') }}</button>
          </div>
        </form>
      </dialog>

      <app-hoja etiqueta="copiar-titulo">
        <h2 id="copiar-titulo">{{ t('copiar.titulo') }}</h2>
        @if (fuentes() === null) {
          <p class="dato-suave" role="status">{{ t('copiar.buscando') }}</p>
        } @else if (fuentes()!.length === 0) {
          <p class="dato-suave">{{ t('copiar.ninguna') }}</p>
        } @else {
          <form class="formulario" (submit)="$event.preventDefault(); copiar()" novalidate>
            <div class="campo">
              <label for="copiar-desde">{{ t('copiar.desde') }}</label>
              <span class="selector">
                <select id="copiar-desde" [value]="fuente()" (change)="fuente.set(valor($event))">
                  @for (f of fuentes(); track f.periodoId) {
                    <option
                      [value]="f.periodoId"
                      [selected]="f.periodoId === fuente()"
                      [textContent]="f.etiqueta"
                    ></option>
                  }
                </select>
              </span>
              <p class="ayuda">{{ t('copiar.ayuda') }}</p>
            </div>
            <button class="boton boton--ancho" type="submit" [appOcupado]="copiando()">
              {{ t('copiar.accion') }}
            </button>
          </form>
        }
        @if (errorCopiar(); as codigo) {
          <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
        }
      </app-hoja>
    </div>
  `,
  styles: `
    hgroup {
      display: grid;
      gap: var(--espacio-1);
    }

    .principal {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      align-content: start;
      gap: var(--espacio-5);
    }

    .bloques {
      display: grid;
      gap: var(--espacio-5);
      min-width: 0;
      margin: 0;
      padding: 0;
      border: 0;
    }

    .bloque {
      display: grid;
      gap: var(--espacio-4);
      padding-top: var(--espacio-4);
      border-top: 1px solid var(--color-divisoria);
    }

    .cabeza-bloque {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--espacio-2);
    }

    .cabeza-bloque h2 {
      display: flex;
      align-items: baseline;
      gap: var(--espacio-2);
      min-width: 0;
      font-size: var(--texto-lg);
      overflow-wrap: anywhere;
    }

    .ayuda-bloque {
      font-size: var(--texto-sm);
    }

    .numero {
      color: var(--grado-tinta);
      font-weight: var(--peso-fuerte);
    }

    /* Nombre a todo lo ancho; tipo, peso y mínima en una fila cuando caben. */
    .rejilla {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
      gap: var(--espacio-3);
    }

    .rejilla .campo--nombre,
    .rejilla .campo:nth-child(2) {
      grid-column: 1 / -1;
    }

    @media (min-width: 40rem) {
      .rejilla {
        grid-template-columns: minmax(0, 2fr) minmax(0, 1.5fr) minmax(0, 1fr) minmax(0, 1fr);
      }

      .rejilla .campo--nombre {
        grid-column: 1 / -1;
      }

      .rejilla .campo:nth-child(2) {
        grid-column: auto;
      }
    }

    /* Escritorio: el nombre se queda en la misma fila que tipo, peso y mínima; cada rubro mide la mitad. */
    @media (min-width: 64rem) {
      .rejilla {
        grid-template-columns: minmax(0, 2.5fr) minmax(0, 1.75fr) minmax(0, 1fr) minmax(0, 1fr);
      }

      .rejilla .campo--nombre {
        grid-column: auto;
      }
    }

    .con-sufijo {
      position: relative;
      display: grid;
    }

    .con-sufijo input {
      padding-inline-end: var(--espacio-6);
    }

    .sufijo {
      position: absolute;
      inset-inline-end: var(--espacio-3);
      inset-block-start: 50%;
      translate: 0 -50%;
      color: var(--color-texto-suave);
      pointer-events: none;
    }

    .eleccion--forma {
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }

    .eleccion--forma .ayuda {
      grid-column: 1 / -1;
    }

    .casilla .ayuda {
      display: block;
    }

    @media (hover: hover) and (pointer: fine) {
      .boton-icono:hover {
        background: var(--color-superficie-hundida);
        color: var(--color-peligro-tinta);
      }
    }

    .criterios {
      display: grid;
      gap: var(--espacio-3);
      padding: var(--espacio-4);
      border-radius: var(--radio-lg);
      background: var(--color-superficie-hundida);
    }

    .criterios .campo input,
    .criterios .eleccion label {
      background: var(--color-superficie);
    }

    .cabeza-criterios {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      justify-content: space-between;
      gap: var(--espacio-2);
    }

    .cabeza-criterios h3 {
      font-size: var(--texto-base);
    }

    .suma-criterios {
      font-size: var(--texto-sm);
      font-weight: var(--peso-medio);
      color: var(--grado-tinta);
    }

    .suma-criterios.incompleta {
      color: var(--color-aviso-tinta);
    }

    .lista-criterios {
      display: grid;
      gap: var(--espacio-4);
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .criterio {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) auto;
      align-items: start;
      gap: var(--espacio-3);
    }

    .criterio + .criterio {
      padding-top: var(--espacio-4);
      border-top: 1px solid var(--color-divisoria);
    }

    .criterio .campo--nombre,
    .criterio .forma-criterio {
      grid-column: 1 / -1;
    }

    .quitar-criterio {
      grid-column: 3;
      grid-row: 2;
      align-self: end;
    }

    @media (min-width: 40rem) {
      .criterio {
        grid-template-columns: minmax(0, 2fr) minmax(0, 1fr) minmax(0, 1fr) auto;
      }

      .criterio .campo--nombre {
        grid-column: auto;
      }

      .quitar-criterio {
        grid-column: 4;
        grid-row: 1;
      }
    }

    .vacio {
      display: grid;
      justify-items: start;
      gap: var(--espacio-3);
    }

    .vacio h2 {
      font-size: var(--texto-lg);
    }

    .acciones-vacio {
      display: flex;
      flex-wrap: wrap;
      gap: var(--espacio-2);
    }

    .agregar {
      justify-self: start;
    }

    .suma {
      display: grid;
      gap: var(--espacio-2);
      color: var(--grado-tinta);
      font-weight: var(--peso-medio);
    }

    .suma.incompleta {
      color: var(--color-aviso-tinta);
    }

    .suma .nota {
      font-size: var(--texto-sm);
      font-weight: var(--peso-normal);
    }

    .suma.incompleta .progreso > span {
      background: var(--color-aviso);
    }

    .bloque-esqueleto {
      display: block;
      block-size: 12rem;
    }
  `,
})
export class Rubros implements OnInit, ConCambiosSinGuardar {
  private readonly servicio = inject(EvaluacionService);
  private readonly grupos = inject(GruposService);
  private readonly avisos = inject(AvisosService);
  private readonly i18n = inject(TranslocoService);
  private readonly router = inject(Router);
  private readonly pintarPortada = controlarPortada();
  private readonly enfocar = crearEnfoque();
  protected readonly formas: readonly Forma[] = ['numerica', 'cumplimiento'];
  protected readonly maximoRubros = MAXIMO_RUBROS;
  protected readonly maximoCriterios = CRITERIOS_MAXIMO;

  readonly id = input.required<string>();
  readonly periodoId = input.required<string>();

  protected readonly hoja = viewChild.required(Hoja);
  private readonly dialogoSalir = viewChild.required<ElementRef<HTMLDialogElement>>('dialogoSalir');
  private resolverSalida: ((salir: boolean) => void) | null = null;
  protected readonly dialogoBorrar =
    viewChild.required<ElementRef<HTMLDialogElement>>('dialogoBorrar');

  protected readonly grupo = signal<GrupoDto | null>(null);
  protected readonly periodos = signal<readonly PeriodoDto[]>([]);
  protected readonly tipos = signal<readonly TipoRubroDto[]>([]);
  protected readonly servidor = signal<RubrosDelPeriodoDto | null>(null);
  protected readonly borrador = signal<readonly RubroBorrador[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly intento = signal(false);
  protected readonly guardando = signal(false);
  protected readonly errorGuardar = signal<string | null>(null);
  protected readonly hayCambios = signal(false);

  protected readonly fuentes = signal<readonly Fuente[] | null>(null);
  protected readonly fuente = signal('');
  protected readonly copiando = signal(false);
  protected readonly errorCopiar = signal<string | null>(null);

  protected readonly clave = computed(() => {
    const g = this.grupo();
    return g ? claveGrado(g.nivel, g.grado) : undefined;
  });
  protected readonly nombreGrupo = computed(() => {
    const g = this.grupo();
    return g ? nombreGrupo(g.grado, g.nombre) : '';
  });
  protected readonly periodo = computed(() =>
    this.periodos().find((p) => p.id === this.periodoId()),
  );
  /** RN-094: un tipo desactivado sigue sirviendo a los rubros que ya lo usan, aunque ya no se ofrezca. */
  protected readonly tiposPorId = computed(
    () =>
      new Map([...this.tipos(), ...(this.servidor()?.tiposInactivos ?? [])].map((t) => [t.id, t])),
  );
  /** Los tipos activos y, si el rubro ya usa uno desactivado, ese también (RN-094). */
  protected opcionesTipo(tipoId: string): readonly TipoRubroDto[] {
    const inactivo = this.servidor()?.tiposInactivos.find((t) => t.id === tipoId);
    return inactivo ? [...this.tipos(), inactivo] : this.tipos();
  }

  protected esInactivo(tipoId: string): boolean {
    return !!this.servidor()?.tiposInactivos.some((t) => t.id === tipoId);
  }

  /** RN-037 y RN-038: en cierre o cerrado, los rubros se ven pero no se editan. */
  protected readonly editable = computed(
    () => this.servidor()?.estado === 'abierto' && !this.grupo()?.archivadoEn,
  );
  private readonly sumaFraccion = computed(() => sumaPesos(this.borrador()));
  protected readonly sumaTexto = computed(() => numero(formatear(this.sumaFraccion(), 2)));
  protected readonly sumanCien = computed(() => comparar(this.sumaFraccion(), fraccion(100)) === 0);
  protected readonly sumaBarra = computed(() => Math.min(100, Number(this.sumaTexto())));
  /** Los errores se muestran después del primer intento de guardar, y se van al corregirlos. */
  protected readonly errores = computed(() =>
    this.intento() ? validar(this.borrador(), this.tiposPorId()) : new Map<string, ErrorBorrador>(),
  );

  /** Con cambios sin guardar, pregunta antes de salir (se pierden pesos y criterios editados). */
  puedeSalir(): boolean | Promise<boolean> {
    if (!this.hayCambios() || !this.editable()) {
      return true;
    }
    const dialogo = this.dialogoSalir().nativeElement;
    dialogo.returnValue = '';
    dialogo.showModal();
    return new Promise((resolver) => (this.resolverSalida = resolver));
  }

  protected decidirSalida(salir: boolean): void {
    this.resolverSalida?.(salir);
    this.resolverSalida = null;
  }

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      const [grupo, periodos, tipos, rubros] = await Promise.all([
        this.servicio.grupo(this.id()),
        this.servicio.periodos(this.id()),
        this.servicio.tiposRubro(),
        this.servicio.rubros(this.periodoId()),
      ]);
      this.grupo.set(grupo);
      this.pintarPortada(portadaDeGrupo(grupo));
      this.periodos.set(periodos);
      this.tipos.set(tipos);
      this.aplicar(rubros);
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  private aplicar(rubros: RubrosDelPeriodoDto): void {
    this.servidor.set(rubros);
    this.borrador.set(deRubros(rubros.rubros));
    this.intento.set(false);
    this.hayCambios.set(false);
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  protected valor(evento: Event): string {
    return (evento.target as HTMLInputElement).value;
  }

  protected marcado(evento: Event): boolean {
    return (evento.target as HTMLInputElement).checked;
  }

  protected falla(campo: string): boolean {
    return this.errores().has(campo);
  }

  protected esAsistencia(r: RubroBorrador): boolean {
    return this.tiposPorId().get(r.tipoRubroId)?.comportamiento === 'asistencia';
  }

  protected suma(lista: readonly { peso: string }[]): string {
    return numero(formatear(sumaPesos(lista), 2));
  }

  protected criteriosSumanCien(r: RubroBorrador): boolean {
    return comparar(sumaPesos(r.criterios), fraccion(100)) === 0;
  }

  private editar(cambio: (lista: RubroBorrador[]) => void): void {
    this.borrador.update((actual) => {
      const lista = [...actual];
      cambio(lista);
      return lista;
    });
    this.hayCambios.set(true);
    this.errorGuardar.set(null);
  }

  protected cambiar(i: number, parcial: Partial<RubroBorrador>): void {
    this.editar((lista) => (lista[i] = { ...lista[i], ...parcial }));
  }

  /** Al cambiar el tipo, el nombre sugerido y la forma sugerida lo siguen si el docente no los tocó. */
  protected cambiarTipo(i: number, tipoId: string): void {
    const r = this.borrador()[i];
    const anterior = this.tiposPorId().get(r.tipoRubroId);
    const nuevo = this.tiposPorId().get(tipoId);
    if (!nuevo) {
      return;
    }
    const nombreSugerido = !r.nombre.trim() || anterior?.nombresSugeridos.includes(r.nombre.trim());
    this.cambiar(i, {
      tipoRubroId: tipoId,
      nombre: nombreSugerido ? (nuevo.nombresSugeridos[0] ?? nuevo.nombre) : r.nombre,
      forma: r.tieneCalificaciones ? r.forma : (nuevo.formaSugerida as Forma) || r.forma,
      conCriterios: nuevo.comportamiento === 'asistencia' ? false : r.conCriterios,
    });
  }

  protected elegirFormaRubro(i: number): (f: Forma) => void {
    return (forma) => this.cambiar(i, { forma });
  }

  protected elegirFormaCriterio(i: number, j: number): (f: Forma) => void {
    return (forma) => this.cambiarCriterio(i, j, { forma });
  }

  /** Activar criterios propone dos con pesos 50/50; el docente los nombra. */
  protected alternarCriterios(i: number, activar: boolean): void {
    const r = this.borrador()[i];
    const criterios =
      activar && r.criterios.length === 0
        ? [criterioNuevo(r.califMinima, r.forma), criterioNuevo(r.califMinima, r.forma)].map(
            (c) => ({ ...c, peso: '50' }),
          )
        : r.criterios;
    this.cambiar(i, { conCriterios: activar, criterios });
    if (activar && r.criterios.length === 0) {
      this.enfocar(`#${criterios[0].clave}-nombre`);
    }
  }

  protected cambiarCriterio(i: number, j: number, parcial: Partial<CriterioBorrador>): void {
    this.editar((lista) => {
      const criterios = [...lista[i].criterios];
      criterios[j] = { ...criterios[j], ...parcial };
      lista[i] = { ...lista[i], criterios };
    });
  }

  protected agregarCriterio(i: number): void {
    const r = this.borrador()[i];
    const nuevo = criterioNuevo(r.califMinima);
    this.cambiar(i, { criterios: [...r.criterios, nuevo] });
    this.enfocar(`#${nuevo.clave}-nombre`);
  }

  protected quitarCriterio(i: number, j: number): void {
    const r = this.borrador()[i];
    this.cambiar(i, { criterios: r.criterios.filter((_, k) => k !== j) });
    this.enfocar(`#${r.clave}-titulo`);
  }

  /** Un rubro nuevo, del primer tipo que todavía no se usa. */
  protected agregar(): void {
    const usados = new Set(this.borrador().map((r) => r.tipoRubroId));
    const tipo = this.tipos().find((t) => !usados.has(t.id)) ?? this.tipos()[0];
    if (!tipo) {
      return;
    }
    const nuevo = rubroNuevo(tipo, numero(this.grupo()?.califMinima ?? 5), this.sumaFraccion());
    this.editar((lista) => lista.push(nuevo));
    this.enfocar(`#${nuevo.clave}-nombre`);
  }

  protected quitar(i: number): void {
    const lista = this.borrador();
    this.editar((l) => l.splice(i, 1));
    const vecino = lista[i + 1] ?? lista[i - 1];
    this.enfocar(vecino ? `#${vecino.clave}-titulo` : 'h1');
  }

  protected async guardar(confirmarBorrado = false): Promise<void> {
    this.intento.set(true);
    this.errorGuardar.set(null);
    if (this.errores().size > 0) {
      this.enfocar(PRIMER_ERROR);
      return;
    }
    this.guardando.set(true);
    try {
      const guardados = await this.servicio.reemplazarRubros(
        this.periodoId(),
        aEntrada(this.borrador(), this.tiposPorId()),
        confirmarBorrado,
      );
      this.dialogoBorrar().nativeElement.close();
      this.aplicar(guardados);
      this.avisos.mostrar(this.i18n.translate('rubros.guardados'));
      await this.router.navigate(['/grupos', this.id()], {
        queryParams: { unidad: this.periodoId() },
      });
    } catch (e) {
      const codigo = codigoDeError(e);
      if (codigo === 'borrado_requiere_confirmacion' && !confirmarBorrado) {
        this.dialogoBorrar().nativeElement.showModal();
      } else {
        this.dialogoBorrar().nativeElement.close();
        this.errorGuardar.set(codigo);
      }
    } finally {
      this.guardando.set(false);
    }
  }

  /** Fuentes para copiar: las demás unidades de este grupo y las de los otros grupos activos. */
  protected async abrirCopiar(): Promise<void> {
    this.errorCopiar.set(null);
    this.hoja().abrir();
    if (this.fuentes() !== null) {
      return;
    }
    try {
      const otros = (await this.grupos.listar()).filter((g) => g.id !== this.id());
      const lista: Fuente[] = this.periodos()
        .filter((p) => p.id !== this.periodoId())
        .map((p) => ({ periodoId: p.id, etiqueta: `${this.nombreGrupo()} · ${p.nombre}` }));
      const deOtros = await Promise.all(
        otros.map(async (g) =>
          (await this.servicio.periodos(g.id)).map((p) => ({
            periodoId: p.id,
            etiqueta: `${nombreGrupo(g.grado, g.nombre)} · ${p.nombre}`,
          })),
        ),
      );
      const todas = [...lista, ...deOtros.flat()];
      this.fuentes.set(todas);
      this.fuente.set(todas[0]?.periodoId ?? '');
    } catch (e) {
      this.fuentes.set([]);
      this.errorCopiar.set(codigoDeError(e));
    }
  }

  protected async copiar(): Promise<void> {
    this.copiando.set(true);
    this.errorCopiar.set(null);
    try {
      const copiados = await this.servicio.copiarRubros(this.periodoId(), this.fuente());
      if (copiados.rubros.length === 0) {
        this.errorCopiar.set('origen_sin_rubros');
        return;
      }
      this.aplicar(copiados);
      this.hoja().cerrar();
      this.avisos.mostrar(this.i18n.translate('rubros.copiar.listo'));
      this.enfocar('h1');
    } catch (e) {
      this.errorCopiar.set(codigoDeError(e));
    } finally {
      this.copiando.set(false);
    }
  }
}
