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
import { RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { BoletaAlumnoDto } from '../../api/models/boleta-alumno-dto';
import { BoletasDto } from '../../api/models/boletas-dto';
import { TutorPorCompartirDto } from '../../api/models/tutor-por-compartir-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { PeriodoDto } from '../../api/models/periodo-dto';
import { codigoDeError } from '../../core/errores/problema';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { crearEnfoque } from '../../shared/foco';
import { claveGrado, nombreGrupo } from '../../shared/grados';
import { Hoja } from '../../shared/hoja';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { forma } from '../../shared/plural';
import { controlarPortada } from '../../shared/portada/portada.service';
import { EvaluacionService } from '../evaluacion/evaluacion.service';
import { urlWhatsApp } from '../alumnos/ficha-alumno';
import { nombresAlumnos } from '../avisos/avisos';
import { AvisosGrupoService } from '../avisos/avisos.service';
import { portadaDeGrupo } from '../grupos/grupos.service';
import { CandadoPro } from '../planes/candado-pro';
import { EtiquetaPlan } from '../planes/etiqueta-plan';
import { tienePro } from '../planes/planes';
import { BoletasService } from './boletas.service';
import {
  avancePreparacion,
  calificacionFinal,
  porCorregir,
  porGenerar,
  publicables,
  resumenPublicacion,
} from './boletas';

/** Los PDF se preparan en segundo plano: la pantalla pregunta cada 2 s mientras falte alguno. */
const CONSULTA_MS = 2000;

/** El detalle de la primera publicación sale una sola vez por docente en este dispositivo (catálogo de reglas/07). */
const CLAVE_PRIMERA = 'aula-lista.boletas.primera-publicacion';

type Accion = 'generar' | 'publicar' | 'corregir' | 'cerrar' | 'reabrir' | 'grupo' | 'excel';

/**
 * SPEC-05, "Boletas y envío" (RF-050 a RF-055): genera las boletas de las observaciones aprobadas, deja ver cada PDF,
 * descargar el del grupo y exportar a Excel, publica con un resumen previo, emite versiones corregidas y cierra o
 * reabre la unidad (RN-048, RN-038). Publicar avisa a los tutores (RN-054); a quien no tiene ningún canal, el
 * docente le comparte la boleta desde su WhatsApp.
 */
@Component({
  selector: 'app-boletas-periodo',
  imports: [
    TranslocoDirective,
    RouterLink,
    NgTemplateOutlet,
    Icono,
    Ocupado,
    Hoja,
    CandadoPro,
    EtiquetaPlan,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="pantalla pantalla--ancha"
      [attr.data-grado]="clave() ?? null"
      *transloco="let t; prefix: 'boletas'"
    >
      <a class="volver" [routerLink]="['/grupos', id(), 'periodos', periodoId(), 'cierre']">
        <app-icono nombre="volver" />
        {{ t('volver') }}
      </a>

      <div class="cabeza-pantalla">
        <hgroup>
          <h1 tabindex="-1">{{ t('titulo') }}</h1>
          @if (periodo(); as p) {
            <p class="dato-suave">{{ nombre() }} · {{ p.nombre }}</p>
          }
        </hgroup>
        @if (boletas(); as b) {
          <app-etiqueta-plan
            [plan]="b.plan.plan"
            [diasPrueba]="b.plan.diasPrueba"
            [hastaPublicar]="b.plan.pruebaHastaPublicar"
          />
        }
      </div>

      @if (!enLinea()) {
        <p class="aviso aviso--pendiente" role="status">{{ t('sinConexion') }}</p>
      }
      @if (boletas(); as b) {
        @if (grupo()?.archivadoEn) {
          <p class="aviso aviso--pendiente">{{ t('archivado') }}</p>
        } @else if (b.estado === 'cerrado') {
          <p class="aviso aviso--pendiente">{{ t('cerrada') }}</p>
        } @else if (b.estado === 'abierto') {
          <p class="aviso aviso--pendiente">
            {{ t('sinCierre') }}
            <a [routerLink]="['/grupos', id(), 'periodos', periodoId(), 'cierre']">{{
              t('irAlCierre')
            }}</a>
          </p>
        }
      }

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (!boletas()) {
        <div class="lista" role="status" [attr.aria-label]="t('cargando')">
          @for (i of [1, 2, 3, 4]; track i) {
            <span class="esqueleto fila-esqueleto" aria-hidden="true"></span>
          }
        </div>
      } @else if (boletas(); as b) {
        @if (!conPro() && !b.soloLectura && b.estado !== 'abierto') {
          <app-candado-pro funcion="boletas" [plan]="b.plan" [grupoId]="id()" />
        }
        <div class="columnas">
          <div class="principal">
            <div class="solo-celular">
              <ng-container [ngTemplateOutlet]="resumen" />
            </div>

            @if (errorAccion(); as codigo) {
              <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
            }

            @if (elegibles().length > 0 && !b.soloLectura) {
              <div class="eleccion-barra">
                <label class="casilla-todas">
                  <input
                    type="checkbox"
                    [checked]="todasElegidas()"
                    [indeterminate]="elegidos().size > 0 && !todasElegidas()"
                    (change)="elegirTodas($any($event.target).checked)"
                  />
                  {{ t('elegirTodas') }}
                </label>
                @if (elegidos().size > 0) {
                  <span class="dato-suave cifras">{{
                    t('elegidas.' + forma(elegidos().size), { cantidad: elegidos().size })
                  }}</span>
                }
              </div>
            }

            <ol
              class="lista"
              [class.con-casillas]="elegibles().length > 0 && !b.soloLectura"
              [attr.aria-label]="t('lista')"
            >
              @for (a of b.alumnos; track a.alumnoId) {
                <li
                  class="fila"
                  [class.llega]="llegadas().has(a.alumnoId)"
                  [style.--orden]="llegadas().get(a.alumnoId) ?? 0"
                  [attr.data-estado]="a.estado"
                >
                  @if (elegibles().length > 0 && !b.soloLectura) {
                    <span class="casilla">
                      @if (esElegible(a)) {
                        <input
                          type="checkbox"
                          [id]="'elegir-' + a.alumnoId"
                          [checked]="elegidos().has(a.alumnoId)"
                          (change)="elegir(a.alumnoId, $any($event.target).checked)"
                          [attr.aria-label]="t('elegir', { nombre: nombreDe(a) })"
                        />
                      }
                    </span>
                  }
                  @if (a.boletaId && (a.estado === 'lista' || a.estado === 'publicada')) {
                    <button
                      class="abrir"
                      type="button"
                      [attr.aria-label]="t('verPdf', { nombre: nombreDe(a) })"
                      (click)="ver(a)"
                    >
                      <ng-container
                        [ngTemplateOutlet]="contenido"
                        [ngTemplateOutletContext]="{ $implicit: a }"
                      />
                    </button>
                  } @else if (a.estado === 'sin_aprobar' && a.observacionId && !b.soloLectura) {
                    <a
                      class="abrir"
                      [routerLink]="[
                        '/grupos',
                        id(),
                        'periodos',
                        periodoId(),
                        'cierre',
                        a.alumnoId,
                      ]"
                    >
                      <ng-container
                        [ngTemplateOutlet]="contenido"
                        [ngTemplateOutletContext]="{ $implicit: a }"
                      />
                    </a>
                  } @else {
                    <div class="abrir">
                      <ng-container
                        [ngTemplateOutlet]="contenido"
                        [ngTemplateOutletContext]="{ $implicit: a }"
                      />
                    </div>
                  }
                  @if (corregible(a) && !b.soloLectura) {
                    <button
                      class="boton boton--secundario corregir"
                      type="button"
                      [disabled]="!enLinea()"
                      (click)="pedirCorreccion(a)"
                    >
                      {{ t('corregir.accion') }}
                      <span class="solo-lector">{{ nombreDe(a) }}</span>
                    </button>
                  }
                </li>
              }
            </ol>
            <p class="solo-lector" role="status">{{ anuncio() }}</p>

            @if (!b.soloLectura && conPro() && (aGenerar().length > 0 || aPublicar().length > 0)) {
              <div class="pie-accion solo-celular">
                <ng-container [ngTemplateOutlet]="principal" />
              </div>
            }
            <div class="solo-celular">
              <ng-container [ngTemplateOutlet]="secundarias" />
            </div>
          </div>

          <aside class="lateral solo-escritorio" aria-labelledby="resumen-titulo">
            <h2 id="resumen-titulo">{{ t('resumenTitulo') }}</h2>
            <ng-container [ngTemplateOutlet]="resumen" />
            @if (!b.soloLectura && conPro()) {
              <ng-container [ngTemplateOutlet]="principal" />
            }
            <ng-container [ngTemplateOutlet]="secundarias" />
          </aside>
        </div>
      }

      <ng-template #contenido let-a>
        <span class="numero cifras">{{ a.numeroLista }}</span>
        <span class="nombre"
          ><span class="apellidos">{{ a.apellidos }}</span> {{ a.nombres }}</span
        >
        <span class="calificacion cifras">
          <span class="solo-lector">{{ t('calificacion') }}</span>
          {{ final(a.calificacion) }}
        </span>
        <span class="marcas">
          <span class="estado" [attr.data-estado]="a.estado">
            @if (a.estado === 'preparando') {
              <app-icono nombre="sincronizando" />
            } @else if (a.estado === 'publicada') {
              <app-icono nombre="guardado" />
            }
            {{ t('estados.' + a.estado) }}
          </span>
          @if (Number(a.version) > 1) {
            <span class="marca-extra cifras">{{ t('version', { version: a.version }) }}</span>
          }
          @if (a.desactualizada && a.estado === 'publicada') {
            <span class="marca-extra aviso-tinte">{{ t('cambioDespues') }}</span>
          } @else if (a.desactualizada && a.estado === 'lista') {
            <span class="marca-extra aviso-tinte">{{ t('desactualizada') }}</span>
          }
          @if (a.falloPdf && a.estado === 'sin_boleta') {
            <span class="marca-extra peligro-tinte">{{ t('noSePreparo') }}</span>
          }
        </span>
      </ng-template>

      <ng-template #resumen>
        @if (boletas(); as b) {
          <div class="resumen">
            <div class="resumen-cabeza">
              <p class="cifras total" tabindex="-1">
                {{ t('publicadas', { publicadas: publicadas(), total: b.alumnos.length }) }}
              </p>
              @if (sello()) {
                <span class="sello" aria-hidden="true">{{ t('sello') }}</span>
              }
            </div>
            @if (preparando() > 0) {
              <div class="preparando">
                <p class="dato-suave cifras">
                  <app-icono nombre="sincronizando" />
                  {{ t('preparandoAvance', { listas: avance().listas, total: tanda() }) }}
                </p>
                <span
                  class="progreso"
                  role="progressbar"
                  [attr.aria-label]="t('preparandoEtiqueta')"
                  aria-valuemin="0"
                  [attr.aria-valuemax]="tanda()"
                  [attr.aria-valuenow]="avance().listas"
                >
                  <span [style.--fraccion]="avance().fraccion"></span>
                </span>
              </div>
            }
            <dl class="conteo cifras">
              @for (e of conteos(); track e.estado) {
                <div [attr.data-estado]="e.estado">
                  <dt>{{ t('conteos.' + e.estado) }}</dt>
                  <dd>{{ e.cantidad }}</dd>
                </div>
              }
            </dl>
          </div>
        }
      </ng-template>

      <ng-template #principal>
        @if (preparando() === 0) {
          <div class="principales">
            @if (aGenerar().length > 0) {
              <button
                class="boton boton--ancho"
                type="button"
                [disabled]="!enLinea()"
                [appOcupado]="trabajando() === 'generar'"
                (click)="generar()"
              >
                {{ t('generar', { cantidad: aGenerar().length }) }}
              </button>
            }
            @if (aPublicar().length > 0) {
              <button
                class="boton boton--ancho"
                [class.boton--secundario]="aGenerar().length > 0"
                type="button"
                [disabled]="!enLinea()"
                (click)="pedirPublicacion()"
              >
                {{
                  elegidos().size > 0
                    ? t('publicarElegidas', { cantidad: aPublicar().length })
                    : t('publicar', { cantidad: aPublicar().length })
                }}
              </button>
            }
          </div>
        }
      </ng-template>

      <ng-template #secundarias>
        @if (boletas(); as b) {
          <div class="secundarias">
            <button
              class="enlace"
              type="button"
              [disabled]="!enLinea() || publicadas() + listas() === 0"
              [appOcupado]="trabajando() === 'grupo'"
              (click)="descargarGrupo()"
            >
              <app-icono nombre="descargar" />
              {{ t('pdfGrupo') }}
            </button>
            <button
              class="enlace"
              type="button"
              [disabled]="!enLinea()"
              [appOcupado]="trabajando() === 'excel'"
              (click)="exportar()"
            >
              <app-icono nombre="tabla" />
              {{ t('excel') }}
            </button>
            @if (!grupo()?.archivadoEn) {
              @if (b.estado === 'en_cierre') {
                <button
                  class="enlace"
                  type="button"
                  [disabled]="!enLinea()"
                  (click)="dialogoCerrar.showModal()"
                >
                  <app-icono nombre="guardado" />
                  {{ t('cerrar.accion') }}
                </button>
              } @else if (b.estado === 'cerrado') {
                <button
                  class="enlace"
                  type="button"
                  [disabled]="!enLinea()"
                  (click)="dialogoReabrir.showModal()"
                >
                  <app-icono nombre="editar" />
                  {{ t('reabrir.accion') }}
                </button>
              }
            }
          </div>
        }
      </ng-template>

      <app-hoja #hojaPublicar etiqueta="publicar-titulo" (cerrada)="errorPublicar.set(null)">
        @if (resumenPrevio(); as r) {
          <h2 id="publicar-titulo" class="cifras">
            {{ t('confirmar.titulo.' + forma(r.boletas), { cantidad: r.boletas }) }}
          </h2>
          <dl class="contactos cifras">
            <div>
              <dt>{{ t('confirmar.whatsapp') }}</dt>
              <dd>{{ r.whatsapp }}</dd>
            </div>
            <div>
              <dt>{{ t('confirmar.correo') }}</dt>
              <dd>{{ r.correo }}</dd>
            </div>
            <div>
              <dt>{{ t('confirmar.push') }}</dt>
              <dd>{{ r.push }}</dd>
            </div>
            <div [class.falta]="r.porCompartir > 0">
              <dt>{{ t('confirmar.porCompartir') }}</dt>
              <dd>{{ r.porCompartir }}</dd>
            </div>
            <div [class.falta]="r.sinContacto.length > 0">
              <dt>{{ t('confirmar.sinContacto') }}</dt>
              <dd>{{ r.sinContacto.length }}</dd>
            </div>
          </dl>
          @if (r.sinContacto.length > 0) {
            <details class="sin-contacto">
              <summary>{{ t('confirmar.quienes') }}</summary>
              <ul>
                @for (a of r.sinContacto; track a.alumnoId) {
                  <li>{{ nombreDe(a) }}</li>
                }
              </ul>
            </details>
          }
          <p class="dato-suave">{{ t('confirmar.avisos') }}</p>
          <p class="dato-suave">{{ t('confirmar.leyenda') }}</p>
          @if (errorPublicar(); as codigo) {
            <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
          }
          <div class="acciones-hoja">
            <button
              class="boton boton--ancho"
              type="button"
              [disabled]="!enLinea()"
              [appOcupado]="trabajando() === 'publicar'"
              (click)="publicar()"
            >
              {{ t('confirmar.accion') }}
            </button>
            <button
              class="boton boton--secundario boton--ancho"
              type="button"
              (click)="hoja().cerrar()"
            >
              {{ t('cancelar') }}
            </button>
          </div>
        }
      </app-hoja>

      <app-hoja #hojaCompartir etiqueta="compartir-titulo" (cerrada)="errorCompartir.set(null)">
        <h2 id="compartir-titulo">{{ t('compartir.titulo') }}</h2>
        <p class="dato-suave">
          {{ t('compartir.texto.' + forma(porCompartir().length), { n: porCompartir().length }) }}
        </p>
        <ul class="filas por-compartir">
          @for (p of porCompartir(); track p.tutorId) {
            <li>
              <span class="quien">
                <span class="nombre">{{ p.nombre }}</span>
                <span class="dato-suave">{{ hijosDe(p) }}</span>
              </span>
              <button
                class="boton"
                type="button"
                [class.boton--secundario]="compartidos().has(p.tutorId)"
                [disabled]="!enLinea()"
                [appOcupado]="compartiendo() === p.tutorId"
                (click)="compartir(p)"
              >
                <app-icono [nombre]="compartidos().has(p.tutorId) ? 'guardado' : 'mensaje'" />
                {{ compartidos().has(p.tutorId) ? t('compartir.hecho') : t('compartir.accion') }}
                <span class="solo-lector">{{ p.nombre }}</span>
              </button>
            </li>
          }
        </ul>
        @if (errorCompartir(); as codigo) {
          <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
        }
        <div class="acciones-hoja">
          <button
            class="boton boton--secundario boton--ancho"
            type="button"
            (click)="hojaCompartir.cerrar()"
          >
            {{ t('compartir.listo') }}
          </button>
        </div>
      </app-hoja>

      <dialog #dialogoCorregir class="dialogo" closedby="any" aria-labelledby="corregir-titulo">
        <div class="contenido">
          <h2 id="corregir-titulo">{{ t('corregir.titulo') }}</h2>
          @if (aCorregir(); as a) {
            <p>
              {{
                t('corregir.texto', {
                  nombre: nombreDe(a),
                  version: a.version,
                  nueva: Number(a.version) + 1,
                })
              }}
            </p>
          }
          @if (errorDialogo(); as codigo) {
            <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
          }
          <div class="acciones">
            <button class="boton boton--secundario" type="button" (click)="dialogoCorregir.close()">
              {{ t('cancelar') }}
            </button>
            <button
              class="boton"
              type="button"
              [disabled]="!enLinea()"
              [appOcupado]="trabajando() === 'corregir'"
              (click)="corregir()"
            >
              {{ t('corregir.confirmar') }}
            </button>
          </div>
        </div>
      </dialog>

      <dialog #dialogoCerrar class="dialogo" closedby="any" aria-labelledby="cerrar-titulo">
        <div class="contenido">
          <h2 id="cerrar-titulo">{{ t('cerrar.titulo') }}</h2>
          <p>
            @if (sinPublicar() > 0) {
              {{ t('cerrar.conPendientes.' + forma(sinPublicar()), { cantidad: sinPublicar() }) }}
            } @else {
              {{ t('cerrar.texto') }}
            }
          </p>
          @if (errorDialogo(); as codigo) {
            <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
          }
          <div class="acciones">
            <button class="boton boton--secundario" type="button" (click)="dialogoCerrar.close()">
              {{ t('cancelar') }}
            </button>
            <button
              class="boton"
              type="button"
              [disabled]="!enLinea()"
              [appOcupado]="trabajando() === 'cerrar'"
              (click)="cerrar()"
            >
              {{ t('cerrar.confirmar') }}
            </button>
          </div>
        </div>
      </dialog>

      <dialog #dialogoReabrir class="dialogo" closedby="any" aria-labelledby="reabrir-titulo">
        <div class="contenido">
          <h2 id="reabrir-titulo">{{ t('reabrir.titulo') }}</h2>
          <p>{{ t('reabrir.texto') }}</p>
          <p class="dato-suave">{{ t('reabrir.registro') }}</p>
          @if (errorDialogo(); as codigo) {
            <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
          }
          <div class="acciones">
            <button class="boton boton--secundario" type="button" (click)="dialogoReabrir.close()">
              {{ t('cancelar') }}
            </button>
            <button
              class="boton"
              type="button"
              [disabled]="!enLinea()"
              [appOcupado]="trabajando() === 'reabrir'"
              (click)="reabrir()"
            >
              {{ t('reabrir.confirmar') }}
            </button>
          </div>
        </div>
      </dialog>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .principal {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      align-content: start;
      gap: var(--espacio-4);
    }

    .resumen {
      display: grid;
      gap: var(--espacio-3);
    }

    .resumen-cabeza {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--espacio-2) var(--espacio-3);
    }

    .total {
      font-size: var(--texto-lg);
      font-weight: var(--peso-fuerte);
    }

    /*
     * Catálogo de reglas/07, "Publicar boletas": la primera vez, un sello que se descubre de izquierda a derecha
     * en 280 ms. Una sola vez; con movimiento reducido solo aparece.
     */
    .sello {
      display: inline-flex;
      align-items: center;
      padding: var(--espacio-1) var(--espacio-3);
      border: 2px solid var(--color-exito);
      border-radius: var(--radio-sm);
      color: var(--color-exito-tinta);
      font-size: var(--texto-sm);
      font-weight: var(--peso-fuerte);
      letter-spacing: 0.06em;
      text-transform: uppercase;
      animation: sellar 280ms var(--ease-out) both;
    }

    @keyframes sellar {
      from {
        clip-path: inset(0 100% 0 0);
      }
      to {
        clip-path: inset(0 0 0 0);
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .sello {
        animation: none;
      }
    }

    .preparando {
      display: grid;
      gap: var(--espacio-2);
    }

    .preparando p {
      display: flex;
      align-items: center;
      gap: var(--espacio-2);
      font-size: var(--texto-sm);
    }

    .preparando app-icono,
    .estado[data-estado='preparando'] app-icono {
      animation: girar var(--dur-giro) linear infinite;
    }

    @media (prefers-reduced-motion: reduce) {
      .preparando app-icono,
      .estado[data-estado='preparando'] app-icono {
        animation: none;
      }
    }

    @keyframes girar {
      to {
        transform: rotate(1turn);
      }
    }

    /* Barra de avance real: crece con cada PDF listo; se anima solo el transform (reglas/07). */
    .progreso {
      block-size: 0.5rem;
      border-radius: var(--radio-pleno);
    }

    .progreso > span {
      transform: scaleX(var(--fraccion, 0));
      transform-origin: left center;
      transition: transform 240ms var(--ease-out);
    }

    .conteo {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(min(100%, 4.5rem), 1fr));
      gap: var(--espacio-2);
      margin: 0;
    }

    .conteo div {
      display: grid;
      gap: var(--espacio-1);
      padding-inline-start: var(--espacio-2);
      border-inline-start: 3px solid var(--color-superficie-hundida);
    }

    .conteo [data-estado='publicada'] {
      border-color: var(--color-exito);
    }

    .conteo [data-estado='lista'] {
      border-color: var(--grado);
    }

    .conteo [data-estado='pendiente'] {
      border-color: var(--color-aviso);
    }

    .conteo dt {
      color: var(--color-texto-suave);
      font-size: var(--texto-xs);
    }

    .conteo dd {
      margin: 0;
      font-size: var(--texto-lg);
      font-weight: var(--peso-fuerte);
    }

    .principales,
    .secundarias {
      display: grid;
      gap: var(--espacio-2);
    }

    .secundarias {
      justify-items: start;
    }

    .eleccion-barra {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--espacio-2) var(--espacio-4);
    }

    .casilla-todas {
      display: inline-flex;
      align-items: center;
      gap: var(--espacio-2);
      min-block-size: var(--tactil-minimo);
      font-weight: var(--peso-medio);
    }

    input[type='checkbox'] {
      inline-size: 1.25rem;
      block-size: 1.25rem;
      margin: 0;
    }

    .lista {
      display: grid;
      margin: 0;
      padding: 0;
      list-style: none;
      border-top: 1px solid var(--color-divisoria);
      container-type: inline-size;
    }

    .fila {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      align-items: center;
      border-bottom: 1px solid var(--color-divisoria);
    }

    .con-casillas .fila {
      grid-template-columns: var(--tactil-minimo) minmax(0, 1fr) auto;
    }

    .casilla {
      display: grid;
      place-items: center;
      align-self: stretch;
    }

    .abrir {
      display: grid;
      grid-template-columns: 2ch minmax(0, 1fr) auto auto;
      align-items: center;
      gap: var(--espacio-3);
      min-block-size: 3.5rem;
      padding: var(--espacio-2) var(--espacio-3);
      border: 0;
      background: none;
      color: inherit;
      font: inherit;
      text-align: start;
      text-decoration: none;
      transition: background-color var(--dur-small) var(--ease-out);
      scroll-margin-block-end: calc(var(--alto-pie-accion, 0px) + var(--espacio-4));
    }

    .con-casillas .abrir {
      padding-inline-start: 0;
    }

    .fila:not(:has(.corregir)) .abrir {
      grid-column: span 2;
    }

    button.abrir,
    a.abrir {
      cursor: pointer;
    }

    .abrir:focus-visible {
      position: relative;
      box-shadow: var(--foco);
      outline: none;
    }

    @media (hover: hover) and (pointer: fine) {
      button.abrir:hover,
      a.abrir:hover {
        background: var(--color-superficie-hundida);
      }
    }

    .corregir {
      margin-inline-end: var(--espacio-3);
      white-space: nowrap;
    }

    /* El mismo fundido del cierre cuando una boleta termina de prepararse (catálogo de reglas/07). */
    .fila.llega .marcas {
      animation: llegar 240ms var(--ease-out) both;
      animation-delay: calc(var(--orden, 0) * 40ms);
    }

    @keyframes llegar {
      from {
        opacity: 0;
        transform: translateY(0.25rem);
      }
    }

    @media (prefers-reduced-motion: reduce) {
      @keyframes llegar {
        from {
          opacity: 0;
        }
      }
    }

    .numero {
      color: var(--color-texto-suave);
      text-align: end;
    }

    .nombre {
      overflow-wrap: break-word;
      min-inline-size: 0;
    }

    .apellidos {
      font-weight: var(--peso-medio);
    }

    .calificacion {
      color: var(--color-texto-suave);
    }

    .marcas {
      display: flex;
      flex-wrap: wrap;
      justify-content: end;
      gap: var(--espacio-1);
    }

    .estado,
    .marca-extra {
      display: inline-flex;
      align-items: center;
      gap: var(--espacio-1);
      padding: var(--espacio-1) var(--espacio-3);
      border-radius: var(--radio-pleno);
      background: var(--color-superficie-hundida);
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
      font-weight: var(--peso-medio);
      white-space: nowrap;
    }

    .estado[data-estado='preparando'],
    .estado[data-estado='sin_aprobar'],
    .aviso-tinte {
      background: var(--color-aviso-tinte);
      color: var(--color-aviso-tinta);
    }

    .estado[data-estado='lista'] {
      background: var(--grado-tinte);
      color: var(--grado-tinta);
    }

    .estado[data-estado='publicada'] {
      background: var(--color-exito-tinte);
      color: var(--color-exito-tinta);
    }

    .peligro-tinte {
      background: var(--color-peligro-tinte);
      color: var(--color-peligro-tinta);
    }

    /* Angosto o con texto grande: calificación, estado y corregir bajan de renglón bajo el nombre. */
    @container (max-width: 34rem) {
      .abrir {
        grid-template-columns: 2ch minmax(0, 1fr) auto;
        row-gap: var(--espacio-1);
      }

      .marcas {
        grid-column: 2 / -1;
        justify-content: start;
      }

      .fila .abrir {
        grid-column: span 2;
      }

      .con-casillas .fila .abrir {
        grid-column: 2 / span 2;
      }

      .corregir {
        grid-column: 1 / -1;
        justify-self: stretch;
        margin: 0 var(--espacio-3) var(--espacio-3);
      }

      .con-casillas .corregir {
        grid-column: 2 / -1;
        margin-inline-start: 0;
      }
    }

    .enlace {
      display: inline-flex;
      align-items: center;
      gap: var(--espacio-2);
      min-block-size: var(--tactil-minimo);
    }

    .enlace:disabled {
      color: var(--color-texto-suave);
      text-decoration: none;
      cursor: not-allowed;
    }

    .contactos {
      display: grid;
      gap: var(--espacio-2);
      margin: var(--espacio-4) 0;
    }

    .contactos div {
      display: flex;
      justify-content: space-between;
      gap: var(--espacio-4);
      padding-block: var(--espacio-2);
      border-bottom: 1px solid var(--color-divisoria);
    }

    .contactos dd {
      margin: 0;
      font-weight: var(--peso-fuerte);
    }

    .contactos .falta dd {
      color: var(--color-aviso-tinta);
    }

    .sin-contacto {
      margin-block-end: var(--espacio-3);
    }

    .sin-contacto summary {
      min-block-size: var(--tactil-minimo);
      display: flex;
      align-items: center;
      cursor: pointer;
      font-weight: var(--peso-medio);
    }

    .sin-contacto ul {
      margin: 0;
      padding-inline-start: var(--espacio-5);
    }

    .acciones-hoja {
      display: grid;
      gap: var(--espacio-2);
      margin-block-start: var(--espacio-4);
    }

    .por-compartir {
      margin-block: var(--espacio-3);
      border-block: 1px solid var(--color-divisoria);
    }

    .por-compartir li {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--espacio-2) var(--espacio-4);
      padding-block: var(--espacio-3);
    }

    .por-compartir .quien {
      display: grid;
      gap: 0.125rem;
      min-inline-size: 0;
    }

    .por-compartir .nombre {
      font-weight: var(--peso-medio);
      overflow-wrap: anywhere;
    }

    .fila-esqueleto {
      display: block;
      inline-size: 100%;
      block-size: 3.5rem;
      margin-block-end: var(--espacio-1);
    }
  `,
})
export class BoletasPeriodo implements OnInit {
  private readonly servicio = inject(BoletasService);
  private readonly avisosTutores = inject(AvisosGrupoService);
  private readonly evaluacion = inject(EvaluacionService);
  private readonly i18n = inject(TranslocoService);
  private readonly avisos = inject(AvisosService);
  private readonly pintarPortada = controlarPortada();
  private readonly enfocar = crearEnfoque();
  protected readonly enLinea = this.servicio.enLinea;
  protected readonly Number = Number;
  protected readonly forma = forma;
  private temporizador: ReturnType<typeof setTimeout> | null = null;

  readonly id = input.required<string>();
  readonly periodoId = input.required<string>();

  protected readonly hoja = viewChild.required<Hoja>('hojaPublicar');
  protected readonly hojaCompartir = viewChild.required<Hoja>('hojaCompartir');
  private readonly dialogoCorregir =
    viewChild.required<ElementRef<HTMLDialogElement>>('dialogoCorregir');
  private readonly dialogoCerrar =
    viewChild.required<ElementRef<HTMLDialogElement>>('dialogoCerrar');
  private readonly dialogoReabrir =
    viewChild.required<ElementRef<HTMLDialogElement>>('dialogoReabrir');

  protected readonly grupo = signal<GrupoDto | null>(null);
  protected readonly periodo = signal<PeriodoDto | null>(null);
  protected readonly boletas = signal<BoletasDto | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly errorAccion = signal<string | null>(null);
  protected readonly errorPublicar = signal<string | null>(null);
  protected readonly errorCompartir = signal<string | null>(null);
  /** RN-054: tutores sin ningún canal tras publicar; el docente les comparte la boleta desde su WhatsApp. */
  protected readonly porCompartir = signal<readonly TutorPorCompartirDto[]>([]);
  protected readonly compartidos = signal<ReadonlySet<string>>(new Set());
  protected readonly compartiendo = signal<string | null>(null);
  protected readonly errorDialogo = signal<string | null>(null);
  protected readonly trabajando = signal<Accion | null>(null);
  protected readonly anuncio = signal('');
  protected readonly elegidos = signal<ReadonlySet<string>>(new Set());
  protected readonly aCorregir = signal<BoletaAlumnoDto | null>(null);
  protected readonly sello = signal(false);
  /** Cuántas boletas tiene la tanda que se prepara, para el "18 de 30". */
  protected readonly tanda = signal(0);
  /** Las que acaban de quedar listas en esta consulta, con su orden para el escalonado. */
  protected readonly llegadas = signal<ReadonlyMap<string, number>>(new Map());

  protected readonly clave = computed(() => {
    const g = this.grupo();
    return g ? claveGrado(g.nivel, g.grado) : undefined;
  });
  protected readonly nombre = computed(() => {
    const g = this.grupo();
    return g ? nombreGrupo(g.grado, g.nombre) : '';
  });
  private readonly alumnos = computed(() => this.boletas()?.alumnos ?? []);
  protected readonly conPro = computed(() => tienePro(this.boletas()?.plan));
  protected readonly aGenerar = computed(() => porGenerar(this.alumnos()));
  protected readonly elegibles = computed(() => publicables(this.alumnos()));
  protected readonly aPublicar = computed(() => {
    const elegidos = this.elegidos();
    const todas = this.elegibles();
    return elegidos.size > 0 ? todas.filter((a) => elegidos.has(a.alumnoId)) : todas;
  });
  protected readonly todasElegidas = computed(
    () => this.elegibles().length > 0 && this.elegidos().size === this.elegibles().length,
  );
  protected readonly resumenPrevio = computed(() => resumenPublicacion(this.aPublicar()));
  protected readonly publicadas = computed(() => Number(this.boletas()?.conteos.publicada ?? 0));
  protected readonly listas = computed(() => Number(this.boletas()?.conteos.lista ?? 0));
  protected readonly preparando = computed(() => Number(this.boletas()?.conteos.preparando ?? 0));
  protected readonly sinPublicar = computed(() => this.alumnos().length - this.publicadas());
  protected readonly avance = computed(() => avancePreparacion(this.tanda(), this.preparando()));
  protected readonly conteos = computed(() => {
    const c = this.boletas()?.conteos;
    if (!c) {
      return [];
    }
    return [
      { estado: 'publicada', cantidad: Number(c.publicada) },
      { estado: 'lista', cantidad: Number(c.lista) },
      { estado: 'pendiente', cantidad: Number(c.sinBoleta) + Number(c.preparando) },
      { estado: 'sin_aprobar', cantidad: Number(c.sinAprobar) },
    ];
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => this.detener());
  }

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      const [grupo, periodos, boletas] = await Promise.all([
        this.evaluacion.grupo(this.id()),
        this.evaluacion.periodos(this.id()),
        this.servicio.boletas(this.periodoId()),
      ]);
      this.pintarPortada(portadaDeGrupo(grupo));
      this.grupo.set(grupo);
      this.periodo.set(periodos.find((p) => p.id === this.periodoId()) ?? null);
      this.mostrar(boletas);
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected texto(clave: string, datos?: Record<string, unknown>): string {
    return this.i18n.translate(clave, datos);
  }

  protected final(valor: number | string | null): string {
    return calificacionFinal(valor, Number(this.boletas()?.decimalesFinal ?? 1));
  }

  protected nombreDe(a: BoletaAlumnoDto): string {
    return `${a.apellidos} ${a.nombres}`.trim();
  }

  protected esElegible(a: BoletaAlumnoDto): boolean {
    return a.estado === 'lista' && !a.desactualizada;
  }

  protected corregible(a: BoletaAlumnoDto): boolean {
    return porCorregir(a) && this.boletas()?.estado === 'en_cierre';
  }

  protected elegir(alumnoId: string, elegido: boolean): void {
    const nuevos = new Set(this.elegidos());
    if (elegido) {
      nuevos.add(alumnoId);
    } else {
      nuevos.delete(alumnoId);
    }
    this.elegidos.set(nuevos);
  }

  protected elegirTodas(elegidas: boolean): void {
    this.elegidos.set(elegidas ? new Set(this.elegibles().map((a) => a.alumnoId)) : new Set());
  }

  protected async generar(): Promise<void> {
    await this.accion('generar', () => this.servicio.generar(this.periodoId()));
    this.enfocar('.total');
  }

  protected pedirPublicacion(): void {
    this.errorPublicar.set(null);
    this.hoja().abrir();
  }

  protected async publicar(): Promise<void> {
    const elegidos = this.elegidos();
    const cantidad = this.aPublicar().length;
    const primera = this.publicadas() === 0 && !this.yaPublico();
    this.errorPublicar.set(null);
    this.trabajando.set('publicar');
    try {
      const { boletas: nuevo, porCompartir } = await this.servicio.publicar(
        this.periodoId(),
        elegidos.size > 0 ? [...elegidos] : undefined,
      );
      this.hoja().cerrar();
      this.elegidos.set(new Set());
      this.mostrar(nuevo);
      if (porCompartir.length > 0) {
        this.porCompartir.set(porCompartir);
        this.compartidos.set(new Set());
        this.hojaCompartir().abrir();
      }
      this.avisos.mostrar(this.texto('boletas.publicadasAviso.' + forma(cantidad), { cantidad }));
      if (primera) {
        this.recordarPublicacion();
        this.sello.set(true);
      }
      this.enfocar('.total');
    } catch (e) {
      this.errorPublicar.set(codigoDeError(e));
    } finally {
      this.trabajando.set(null);
    }
  }
  protected hijosDe(p: TutorPorCompartirDto): string {
    return nombresAlumnos(p.alumnos);
  }

  /** La ventana se abre en el mismo toque, para que el navegador no la bloquee; wa.me llega con el enlace. */
  protected async compartir(p: TutorPorCompartirDto): Promise<void> {
    const ventana = window.open('', '_blank');
    this.errorCompartir.set(null);
    this.compartiendo.set(p.tutorId);
    try {
      const enlace = await this.avisosTutores.compartirBoleta(p.boletaId, p.tutorId);
      const mensaje = this.i18n.translate('boletas.compartir.mensaje', {
        tutor: p.nombre,
        hijos: nombresAlumnos(p.alumnos),
        unidad: this.periodo()?.nombre ?? '',
        url: enlace.url,
      });
      const destino = urlWhatsApp(p.telefono, mensaje);
      if (ventana) {
        ventana.opener = null;
        ventana.location.href = destino;
      } else {
        window.location.href = destino;
      }
      this.compartidos.update((c) => new Set(c).add(p.tutorId));
    } catch (e) {
      ventana?.close();
      this.errorCompartir.set(codigoDeError(e));
    } finally {
      this.compartiendo.set(null);
    }
  }

  protected pedirCorreccion(a: BoletaAlumnoDto): void {
    this.errorDialogo.set(null);
    this.aCorregir.set(a);
    this.dialogoCorregir().nativeElement.showModal();
  }

  protected async corregir(): Promise<void> {
    const a = this.aCorregir();
    if (!a?.boletaId) {
      return;
    }
    const boletaId = a.boletaId;
    if (await this.enDialogo('corregir', () => this.servicio.corregir(boletaId))) {
      this.dialogoCorregir().nativeElement.close();
      this.avisos.mostrar(
        this.texto('boletas.corregir.hecho', {
          nombre: this.nombreDe(a),
          version: Number(a.version) + 1,
        }),
      );
    }
  }

  protected async cerrar(): Promise<void> {
    const listo = await this.enDialogo('cerrar', async () => {
      await this.servicio.cerrar(this.periodoId());
      return this.servicio.boletas(this.periodoId());
    });
    if (listo) {
      this.dialogoCerrar().nativeElement.close();
      this.elegidos.set(new Set());
      this.avisos.mostrar(this.texto('boletas.cerrar.hecho'));
      this.enfocar('h1');
    }
  }

  protected async reabrir(): Promise<void> {
    const listo = await this.enDialogo('reabrir', async () => {
      await this.servicio.reabrir(this.periodoId());
      return this.servicio.boletas(this.periodoId());
    });
    if (listo) {
      this.dialogoReabrir().nativeElement.close();
      this.avisos.mostrar(this.texto('boletas.reabrir.hecho'));
      this.enfocar('h1');
    }
  }

  /**
   * Vista previa: se abre la pestaña al tocar (si no, el navegador la bloquea por llegar después de una espera) y se
   * le carga el PDF cuando llega. En Android eso abre el visor de PDF.
   */
  protected async ver(a: BoletaAlumnoDto): Promise<void> {
    if (!a.boletaId) {
      return;
    }
    const ventana = window.open('', '_blank');
    try {
      const pdf = await this.servicio.pdf(a.boletaId);
      const url = URL.createObjectURL(pdf);
      if (ventana) {
        ventana.location.href = url;
      } else {
        const enlace = document.createElement('a');
        enlace.href = url;
        enlace.target = '_blank';
        enlace.rel = 'noopener';
        enlace.click();
      }
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (e) {
      ventana?.close();
      this.avisos.mostrar(this.texto('errores.' + codigoDeError(e)), 'error');
    }
  }

  protected async descargarGrupo(): Promise<void> {
    await this.descarga('grupo', () => this.servicio.descargarGrupo(this.periodoId()));
  }

  protected async exportar(): Promise<void> {
    await this.descarga('excel', () => this.servicio.exportar(this.periodoId()));
  }

  private async descarga(accion: Accion, bajar: () => Promise<void>): Promise<void> {
    this.trabajando.set(accion);
    try {
      await bajar();
    } catch (e) {
      // Un aviso y no el error de la pantalla: la lista sigue a la vista.
      this.avisos.mostrar(this.texto('errores.' + codigoDeError(e)), 'error');
    } finally {
      this.trabajando.set(null);
    }
  }

  private async accion(accion: Accion, llamar: () => Promise<BoletasDto>): Promise<void> {
    this.errorAccion.set(null);
    this.trabajando.set(accion);
    try {
      this.mostrar(await llamar());
    } catch (e) {
      this.errorAccion.set(codigoDeError(e));
    } finally {
      this.trabajando.set(null);
    }
  }

  /** Acción de un diálogo de confirmación: el error se queda dentro del diálogo. */
  private async enDialogo(accion: Accion, llamar: () => Promise<BoletasDto>): Promise<boolean> {
    this.errorDialogo.set(null);
    this.trabajando.set(accion);
    try {
      this.mostrar(await llamar());
      return true;
    } catch (e) {
      this.errorDialogo.set(codigoDeError(e));
      return false;
    } finally {
      this.trabajando.set(null);
    }
  }

  /** Pinta las boletas, marca las que quedaron listas desde la consulta anterior y agenda la siguiente si falta. */
  private mostrar(nuevo: BoletasDto): void {
    const anterior = this.boletas();
    const preparando = Number(nuevo.conteos.preparando);
    const antes = Number(anterior?.conteos.preparando ?? 0);
    if (anterior) {
      const estados = new Map(anterior.alumnos.map((a) => [a.alumnoId, a.estado]));
      const llegaron = nuevo.alumnos.filter(
        (a) => estados.get(a.alumnoId) === 'preparando' && a.estado !== 'preparando',
      );
      this.llegadas.set(new Map(llegaron.map((a, i) => [a.alumnoId, Math.min(i, 8)])));
      if (antes > 0 && preparando === 0) {
        this.anunciarFin(nuevo);
      }
    }
    // Una tanda nueva empieza cuando antes no había ninguna preparándose; si crece a media tanda, se suma.
    if (preparando > 0 && (antes === 0 || preparando > antes)) {
      this.tanda.set(antes === 0 ? preparando : this.tanda() + preparando - antes);
    }
    // La selección solo guarda alumnos que todavía se pueden publicar.
    const elegibles = new Set(publicables(nuevo.alumnos).map((a) => a.alumnoId));
    const elegidos = [...this.elegidos()].filter((id) => elegibles.has(id));
    if (elegidos.length !== this.elegidos().size) {
      this.elegidos.set(new Set(elegidos));
    }
    this.boletas.set(nuevo);
    this.detener();
    if (preparando > 0) {
      this.temporizador = setTimeout(() => void this.consultar(), CONSULTA_MS);
    }
  }

  private async consultar(): Promise<void> {
    // En una pestaña oculta no se consulta; se retoma al volver.
    if (document.visibilityState === 'hidden') {
      this.temporizador = setTimeout(() => void this.consultar(), CONSULTA_MS);
      return;
    }
    try {
      this.mostrar(await this.servicio.boletas(this.periodoId()));
    } catch {
      // Sin red la consulta se vuelve a intentar; la pantalla conserva lo último que supo.
      this.temporizador = setTimeout(() => void this.consultar(), CONSULTA_MS);
    }
  }

  /** El lector de pantalla oye el resultado una vez, al terminar, no en cada consulta. */
  private anunciarFin(b: BoletasDto): void {
    const fallidas = b.alumnos.filter((a) => a.estado === 'sin_boleta' && a.falloPdf).length;
    if (fallidas > 0) {
      this.avisos.mostrar(
        this.texto('boletas.terminadoConFallas.' + forma(fallidas), { fallidas }),
        'error',
      );
    } else {
      this.anuncio.set(
        this.texto('boletas.terminado.' + forma(b.conteos.lista), {
          cantidad: Number(b.conteos.lista),
        }),
      );
    }
  }

  private yaPublico(): boolean {
    try {
      return localStorage.getItem(CLAVE_PRIMERA) === '1';
    } catch {
      return false;
    }
  }

  private recordarPublicacion(): void {
    try {
      localStorage.setItem(CLAVE_PRIMERA, '1');
    } catch {
      // Sin almacenamiento el sello puede volver a salir; no pasa nada.
    }
  }

  private detener(): void {
    if (this.temporizador) {
      clearTimeout(this.temporizador);
      this.temporizador = null;
    }
  }
}
