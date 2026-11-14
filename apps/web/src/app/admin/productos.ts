import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  OnInit,
  afterNextRender,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { CatalogoAdminDto } from '../api/models/catalogo-admin-dto';
import { PrecioAdminDto } from '../api/models/precio-admin-dto';
import { ProductoAdminDto } from '../api/models/producto-admin-dto';
import { ProductoEntrada } from '../api/models/producto-entrada';
import { codigoDeError } from '../core/errores/problema';
import { AvisosService } from '../shared/avisos/avisos.service';
import { fechaAviso } from '../shared/fechas';
import { Hoja } from '../shared/hoja';
import { Ocupado } from '../shared/ocupado';
import { forma } from '../shared/plural';
import { AdminService } from './admin.service';
import { ESTILOS_PANEL } from './estilos';
import { entero, pesos } from './panel';

interface Borrador {
  readonly nombre: string;
  readonly descripcion: string;
  readonly funciones: readonly string[];
  readonly meses: readonly number[];
  readonly orden: string;
  readonly primera: string;
  readonly adicional: string;
}

type Campo = 'nombre' | 'descripcion' | 'funciones' | 'orden' | 'primera' | 'adicional';
type Hojas = 'producto' | 'precio';

const MAXIMOS = { nombre: 60, descripcion: 300 } as const;
/** RN-071: los meses que se pueden ofrecer por OXXO; el producto elige cuáles. */
const MESES = [1, 2, 3, 4, 6, 12] as const;
const CIFRA = /^\d{1,5}(\.\d{1,2})?$/;

/**
 * RF-095 y ADR-0017: los productos que se venden y sus precios. El precio vive en Stripe; cambiarlo crea una versión
 * nueva y archiva la anterior allá, y quien ya paga conserva la suya. Cada cambio queda en la auditoría.
 */
@Component({
  selector: 'app-productos',
  imports: [NgTemplateOutlet, TranslocoDirective, Hoja, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t; prefix: 'admin.productos'">
      <div class="cabeza cabeza--accion">
        <div class="cabeza">
          <h1 tabindex="-1">{{ t('titulo') }}</h1>
          <p class="resumen">{{ t('resumen') }}</p>
        </div>
        @if (catalogo()) {
          <button class="boton" type="button" (click)="nuevo()">{{ t('nuevo') }}</button>
        }
      </div>

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (!catalogo()) {
        <div class="cargando" role="status" [attr.aria-label]="t('cargando')">
          @for (i of [1, 2]; track i) {
            <span class="esqueleto esqueleto--alto" aria-hidden="true"></span>
          }
        </div>
      } @else if (catalogo(); as c) {
        @if (c.proveedor === 'simulado') {
          <p class="aviso">{{ t('simulado') }}</p>
        }
        @if (!aLaVenta()) {
          <p class="aviso aviso--error" role="status">{{ t('sinVenta') }}</p>
        }

        @if (c.productos.length === 0) {
          <p class="vacio">{{ t('vacio') }}</p>
        }
        <ul class="productos" role="list">
          @for (p of enUso(); track p.id) {
            <li>
              <ng-container
                [ngTemplateOutlet]="tarjeta"
                [ngTemplateOutletContext]="{ $implicit: p }"
              />
            </li>
          }
        </ul>
        @if (archivados().length > 0) {
          <details class="archivados">
            <summary>
              {{ t('archivados.' + forma(archivados().length), { n: archivados().length }) }}
            </summary>
            <ul class="productos" role="list">
              @for (p of archivados(); track p.id) {
                <li>
                  <ng-container
                    [ngTemplateOutlet]="tarjeta"
                    [ngTemplateOutletContext]="{ $implicit: p }"
                  />
                </li>
              }
            </ul>
          </details>
        }
      }

      <ng-template #tarjeta let-p>
        <article class="producto" [attr.aria-labelledby]="'producto-' + p.id">
          <header class="producto-cabeza">
            <h2 [id]="'producto-' + p.id">{{ p.nombre }}</h2>
            <span class="sellos">
              <span class="sello" [attr.data-tono]="tonoEstado(p)">{{
                t('estados.' + estado(p))
              }}</span>
              @if (p.errorSincronizacion) {
                <span class="sello" data-tono="peligro">{{ t('sincronizacion.fallo') }}</span>
              } @else if (p.porSincronizar) {
                <span class="sello" data-tono="aviso">{{ t('sincronizacion.pendiente') }}</span>
              }
            </span>
          </header>

          @if (p.descripcion) {
            <p class="descripcion">{{ p.descripcion }}</p>
          }

          @if (precioActual(p); as v) {
            <p class="precio">
              <span class="cifra">{{ dinero(v.precioPrimera) }}</span>
              {{ t('precio.primera', { moneda: v.moneda }) }}
              <span class="mas">+</span>
              <span class="cifra">{{ dinero(v.precioAdicional) }}</span>
              {{ t('precio.adicional') }}
            </p>
            @if (v.estado === 'pendiente' && p.estado === 'activo') {
              <p class="dato-suave">{{ t('precio.pendiente') }}</p>
            }
          } @else {
            <p class="dato-suave">{{ t('precio.sinPrecio') }}</p>
          }

          <dl class="datos">
            <div>
              <dt>{{ t('incluye') }}</dt>
              <dd>
                <ul class="funciones" role="list">
                  @for (f of p.funciones; track f) {
                    <li>{{ t('funciones.' + f) }}</li>
                  }
                </ul>
              </dd>
            </div>
            <div>
              <dt>{{ t('oxxo') }}</dt>
              <dd>{{ meses(p.mesesPrepago) }}</dd>
            </div>
            <div>
              <dt>{{ t('suscriptores') }}</dt>
              <dd class="cifras">{{ entero(p.suscriptores) }}</dd>
            </div>
            <div>
              <dt>{{ t('orden') }}</dt>
              <dd class="cifras">{{ p.orden }}</dd>
            </div>
          </dl>

          @if (p.errorSincronizacion; as falla) {
            <div class="aviso aviso--error" role="alert">
              <p>{{ t('sincronizacion.detalle', { error: falla }) }}</p>
            </div>
          }

          @if (p.precios.length > 0) {
            <details class="versiones">
              <summary>
                {{ t('versiones.titulo.' + forma(p.precios.length), { n: p.precios.length }) }}
              </summary>
              <table class="libreta apilable">
                <thead>
                  <tr>
                    <th scope="col" class="num">{{ t('versiones.version') }}</th>
                    <th scope="col">{{ t('versiones.precio') }}</th>
                    <th scope="col">{{ t('versiones.estado') }}</th>
                    <th scope="col" class="num">{{ t('suscriptores') }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (v of versiones(p); track v.id) {
                    <tr>
                      <th scope="row" class="num">v{{ v.version }}</th>
                      <td [attr.data-etiqueta]="t('versiones.precio')">
                        {{
                          t('versiones.monto', {
                            primera: dinero(v.precioPrimera),
                            adicional: dinero(v.precioAdicional),
                          })
                        }}
                      </td>
                      <td [attr.data-etiqueta]="t('versiones.estado')">
                        <span>
                          {{ t('versiones.estados.' + v.estado) }}
                          <span class="secundario">{{ fechaVersion(v) }}</span>
                        </span>
                      </td>
                      <td class="num" [attr.data-etiqueta]="t('suscriptores')">
                        {{ entero(v.suscriptores) }}
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </details>
          }

          @if (p.estado !== 'archivado' || p.porSincronizar) {
            <div class="acciones">
              @if (p.estado !== 'archivado') {
                <button
                  class="boton boton--secundario"
                  type="button"
                  (click)="editar(p)"
                  [attr.aria-label]="t('editarProducto', { nombre: p.nombre })"
                >
                  {{ t('editar') }}
                </button>
              }
              @if (p.estado === 'activo') {
                <button
                  class="boton boton--secundario"
                  type="button"
                  (click)="abrirPrecio(p)"
                  [attr.aria-label]="t('cambiarPrecioDe', { nombre: p.nombre })"
                >
                  {{ t('cambiarPrecio') }}
                </button>
              }
              @if (p.estado === 'borrador') {
                <button
                  class="boton"
                  type="button"
                  [disabled]="!precioActual(p)"
                  [appOcupado]="enCurso() === p.id + ':publicar'"
                  (click)="accion(p, 'publicar')"
                >
                  {{ t('publicar') }}
                </button>
              }
              @if (p.porSincronizar && p.estado !== 'borrador') {
                <button
                  class="boton boton--secundario"
                  type="button"
                  [appOcupado]="enCurso() === p.id + ':sincronizar'"
                  (click)="accion(p, 'sincronizar')"
                >
                  {{ t(p.errorSincronizacion ? 'sincronizar' : 'enviar') }}
                </button>
              }
              @if (p.estado !== 'archivado') {
                <button
                  class="boton boton--peligro"
                  type="button"
                  [appOcupado]="enCurso() === p.id + ':archivar'"
                  (click)="pedirArchivo(p)"
                  [attr.aria-label]="t('archivarProducto', { nombre: p.nombre })"
                >
                  {{ t('archivar') }}
                </button>
              }
            </div>
          }
          @if (porArchivar() === p.id) {
            <div class="confirmar" role="alertdialog" [attr.aria-labelledby]="'archivar-' + p.id">
              <p [id]="'archivar-' + p.id">
                {{
                  p.estado === 'borrador'
                    ? t('confirmarArchivo.borrador')
                    : t('confirmarArchivo.' + forma(p.suscriptores), { n: p.suscriptores })
                }}
              </p>
              <div class="acciones">
                <button class="boton boton--peligro" type="button" (click)="accion(p, 'archivar')">
                  {{ t('confirmarArchivo.si') }}
                </button>
                <button
                  class="boton boton--secundario"
                  type="button"
                  (click)="porArchivar.set(null)"
                >
                  {{ t('cancelar') }}
                </button>
              </div>
            </div>
          }
          @if (errorAccion()?.id === p.id) {
            <p class="aviso aviso--error" role="alert">
              {{ texto('errores.' + errorAccion()!.codigo) }}
            </p>
          }
        </article>
      </ng-template>

      <app-hoja etiqueta="producto-hoja-titulo" (cerrada)="errorGuardar.set(null)">
        @if (hojaAbierta() === 'producto' && borrador(); as b) {
          <form #formulario class="formulario" (submit)="guardar($event)" novalidate>
            <h2 id="producto-hoja-titulo">
              {{ editando() ? t('editarProducto', { nombre: editando()!.nombre }) : t('nuevo') }}
            </h2>

            <div class="campo">
              <label for="producto-nombre">{{ t('campos.nombre') }}</label>
              <input
                id="producto-nombre"
                [attr.maxlength]="maximos.nombre"
                [value]="b.nombre"
                (input)="cambiar('nombre', valor($event))"
                [attr.aria-invalid]="falla('nombre')"
                [attr.aria-describedby]="
                  falla('nombre') ? 'producto-nombre-error' : 'producto-nombre-ayuda'
                "
                autocomplete="off"
              />
              @if (falla('nombre')) {
                <p class="error" id="producto-nombre-error">
                  {{ t('validacion.nombre', { maximo: maximos.nombre }) }}
                </p>
              } @else {
                <p class="ayuda" id="producto-nombre-ayuda">{{ t('ayudas.nombre') }}</p>
              }
            </div>

            <div class="campo">
              <label for="producto-descripcion">{{ t('campos.descripcion') }}</label>
              <textarea
                id="producto-descripcion"
                rows="3"
                [attr.maxlength]="maximos.descripcion"
                [value]="b.descripcion"
                (input)="cambiar('descripcion', valor($event))"
                [attr.aria-invalid]="falla('descripcion')"
                aria-describedby="producto-descripcion-ayuda"
              ></textarea>
              <p class="ayuda" [class.error]="falla('descripcion')" id="producto-descripcion-ayuda">
                {{ t('ayudas.descripcion', { maximo: maximos.descripcion }) }}
              </p>
            </div>

            <fieldset
              class="eleccion"
              [attr.aria-invalid]="falla('funciones')"
              aria-describedby="producto-funciones-ayuda"
            >
              <legend>{{ t('campos.funciones') }}</legend>
              @for (f of funciones(); track f) {
                <label>
                  <input
                    type="checkbox"
                    [checked]="b.funciones.includes(f)"
                    (change)="alternarFuncion(f, marcado($event))"
                  />
                  {{ t('funciones.' + f) }}
                </label>
              }
              <p class="ayuda" [class.error]="falla('funciones')" id="producto-funciones-ayuda">
                {{ t('ayudas.funciones') }}
              </p>
            </fieldset>

            <fieldset class="eleccion" aria-describedby="producto-meses-ayuda">
              <legend>{{ t('campos.meses') }}</legend>
              <div class="meses">
                @for (m of mesesPosibles; track m) {
                  <label class="mes">
                    <input
                      type="checkbox"
                      [checked]="b.meses.includes(m)"
                      (change)="alternarMes(m, marcado($event))"
                    />
                    {{ t('mes.' + forma(m), { n: m }) }}
                  </label>
                }
              </div>
              <p class="ayuda" id="producto-meses-ayuda">{{ t('ayudas.meses') }}</p>
            </fieldset>

            @if (!editando() || editando()!.estado === 'borrador') {
              <ng-container
                [ngTemplateOutlet]="cifras"
                [ngTemplateOutletContext]="{ b, prefijo: 'producto' }"
              />
            }

            <div class="campo campo--corto">
              <label for="producto-orden">{{ t('campos.orden') }}</label>
              <input
                id="producto-orden"
                class="cifras"
                inputmode="numeric"
                [value]="b.orden"
                (input)="cambiar('orden', valor($event))"
                [attr.aria-invalid]="falla('orden')"
                aria-describedby="producto-orden-ayuda"
              />
              <p class="ayuda" [class.error]="falla('orden')" id="producto-orden-ayuda">
                {{ t('ayudas.orden') }}
              </p>
            </div>

            @if (editando()?.estado === 'activo') {
              <p class="dato-suave">{{ t('ayudas.precioAparte') }}</p>
            }

            @if (errorGuardar(); as codigo) {
              <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
            }
            <div class="acciones-hoja">
              <button class="boton boton--ancho" type="submit" [appOcupado]="guardando()">
                {{ t(editando() ? 'guardar' : 'crear') }}
              </button>
              <button
                class="boton boton--secundario boton--ancho"
                type="button"
                (click)="hoja().cerrar()"
              >
                {{ t('cancelar') }}
              </button>
            </div>
          </form>
        }

        @if (hojaAbierta() === 'precio' && borrador(); as b) {
          <form #formulario class="formulario" (submit)="guardarPrecio($event)" novalidate>
            <h2 id="producto-hoja-titulo">
              {{ t('cambiarPrecioDe', { nombre: editando()!.nombre }) }}
            </h2>
            <p class="explicacion">
              {{
                t('explicacionPrecio.' + forma(editando()!.suscriptores), {
                  n: editando()!.suscriptores,
                })
              }}
            </p>
            @if (precioActual(editando()!); as v) {
              <p class="dato-suave">
                {{
                  t('precioActual', {
                    primera: dinero(v.precioPrimera),
                    adicional: dinero(v.precioAdicional),
                    moneda: v.moneda,
                  })
                }}
              </p>
            }
            <ng-container
              [ngTemplateOutlet]="cifras"
              [ngTemplateOutletContext]="{ b, prefijo: 'precio' }"
            />

            @if (errorGuardar(); as codigo) {
              <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
            }
            <div class="acciones-hoja">
              <button class="boton boton--ancho" type="submit" [appOcupado]="guardando()">
                {{ t('guardarPrecio') }}
              </button>
              <button
                class="boton boton--secundario boton--ancho"
                type="button"
                (click)="hoja().cerrar()"
              >
                {{ t('cancelar') }}
              </button>
            </div>
          </form>
        }
      </app-hoja>

      <ng-template #cifras let-b="b" let-prefijo="prefijo">
        <div class="campos-en-fila">
          <div class="campo">
            <label [for]="prefijo + '-primera'">{{
              t('campos.primera', { moneda: moneda() })
            }}</label>
            <input
              [id]="prefijo + '-primera'"
              class="cifras"
              inputmode="decimal"
              [value]="b.primera"
              (input)="cambiar('primera', valor($event))"
              [attr.aria-invalid]="falla('primera')"
              [attr.aria-describedby]="prefijo + '-primera-ayuda'"
            />
            <p class="ayuda" [class.error]="falla('primera')" [id]="prefijo + '-primera-ayuda'">
              {{ t('ayudas.primera') }}
            </p>
          </div>
          <div class="campo">
            <label [for]="prefijo + '-adicional'">{{
              t('campos.adicional', { moneda: moneda() })
            }}</label>
            <input
              [id]="prefijo + '-adicional'"
              class="cifras"
              inputmode="decimal"
              [value]="b.adicional"
              (input)="cambiar('adicional', valor($event))"
              [attr.aria-invalid]="falla('adicional')"
              [attr.aria-describedby]="prefijo + '-adicional-ayuda'"
            />
            <p class="ayuda" [class.error]="falla('adicional')" [id]="prefijo + '-adicional-ayuda'">
              {{ t('ayudas.adicional') }}
            </p>
          </div>
        </div>
      </ng-template>
    </ng-container>
  `,
  styles: [
    ESTILOS_PANEL,
    `
      .cabeza--accion {
        display: flex;
        flex-wrap: wrap;
        align-items: end;
        justify-content: space-between;
        gap: var(--espacio-4);
      }

      .aviso {
        margin: 0;
      }

      .cargando .esqueleto--alto {
        block-size: 12rem;
      }

      .productos {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(min(100%, 26rem), 1fr));
        gap: var(--espacio-4);
        margin: 0;
        padding: 0;
        list-style: none;
      }

      .producto {
        display: grid;
        align-content: start;
        gap: var(--espacio-3);
        block-size: 100%;
        padding: var(--espacio-4) var(--espacio-5) var(--espacio-5);
        border: 1px solid var(--color-borde);
        border-radius: var(--radio-lg);
        background: var(--color-superficie);
      }

      .producto-cabeza {
        display: flex;
        flex-wrap: wrap;
        align-items: baseline;
        justify-content: space-between;
        gap: var(--espacio-2);
      }

      .producto-cabeza h2 {
        min-inline-size: 0;
        overflow-wrap: anywhere;
      }

      .sellos {
        display: flex;
        flex-wrap: wrap;
        gap: var(--espacio-1);
      }

      .descripcion,
      .explicacion {
        margin: 0;
        color: var(--color-texto-suave);
        text-wrap: pretty;
        overflow-wrap: anywhere;
      }

      .precio {
        margin: 0;
        color: var(--color-texto-suave);
      }

      .precio .cifra {
        color: var(--color-texto);
        font-size: var(--texto-lg);
        font-weight: var(--peso-fuerte);
        font-variant-numeric: tabular-nums;
      }

      .precio .mas {
        margin-inline: var(--espacio-1);
      }

      .dato-suave {
        margin: 0;
      }

      .datos {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: var(--espacio-3) var(--espacio-4);
        margin: 0;
      }

      .datos > div:first-child {
        grid-column: 1 / -1;
      }

      .datos dt {
        color: var(--color-texto-suave);
        font-size: var(--texto-sm);
      }

      .datos dd {
        margin: var(--espacio-1) 0 0;
      }

      .funciones {
        display: flex;
        flex-wrap: wrap;
        gap: var(--espacio-1);
        margin: 0;
        padding: 0;
        list-style: none;
      }

      .funciones li {
        padding: var(--espacio-1) var(--espacio-2);
        border-radius: var(--radio-sm);
        background: var(--color-superficie-hundida);
        font-size: var(--texto-sm);
      }

      .archivados > summary,
      .versiones summary {
        min-block-size: var(--tactil-minimo);
        display: flex;
        align-items: center;
        color: var(--color-accion);
        cursor: pointer;
      }

      .archivados > .productos {
        margin-block-start: var(--espacio-3);
      }

      .versiones .libreta {
        margin-block-start: var(--espacio-2);
      }

      td > span {
        display: inline-grid;
        gap: var(--espacio-1);
      }

      .acciones {
        display: flex;
        flex-wrap: wrap;
        gap: var(--espacio-2);
        margin-block-start: var(--espacio-1);
      }

      .confirmar {
        display: grid;
        gap: var(--espacio-3);
        padding: var(--espacio-4);
        border-radius: var(--radio-lg);
        background: var(--color-superficie-hundida);
      }

      .confirmar p {
        margin: 0;
        text-wrap: pretty;
      }

      .formulario {
        display: grid;
        gap: var(--espacio-4);
      }

      .formulario h2 {
        margin: 0;
        font-size: var(--texto-lg);
        overflow-wrap: anywhere;
      }

      .formulario textarea {
        min-block-size: auto;
      }

      .eleccion {
        margin: 0;
      }

      .eleccion .ayuda {
        margin: 0;
      }

      .meses {
        display: grid;
        grid-template-columns: repeat(3, minmax(0, 1fr));
        gap: var(--espacio-1) var(--espacio-3);
      }

      .campo--corto {
        max-inline-size: 12rem;
      }

      .acciones-hoja {
        display: grid;
        gap: var(--espacio-2);
      }

      @media (max-width: 47.99rem) {
        .producto {
          padding: var(--espacio-4);
        }

        .acciones .boton {
          flex: 1 1 auto;
        }
      }
    `,
  ],
})
export class Productos implements OnInit {
  private readonly servicio = inject(AdminService);
  private readonly i18n = inject(TranslocoService);
  private readonly avisos = inject(AvisosService);
  private readonly injector = inject(Injector);
  private readonly formulario = viewChild<ElementRef<HTMLFormElement>>('formulario');
  protected readonly hoja = viewChild.required(Hoja);

  protected readonly maximos = MAXIMOS;
  protected readonly mesesPosibles = MESES;
  protected readonly entero = entero;
  protected readonly forma = forma;

  protected readonly catalogo = signal<CatalogoAdminDto | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly hojaAbierta = signal<Hojas | null>(null);
  protected readonly editando = signal<ProductoAdminDto | null>(null);
  protected readonly borrador = signal<Borrador | null>(null);
  protected readonly intento = signal(false);
  protected readonly guardando = signal(false);
  protected readonly errorGuardar = signal<string | null>(null);
  protected readonly enCurso = signal<string | null>(null);
  protected readonly porArchivar = signal<string | null>(null);
  protected readonly errorAccion = signal<{ id: string; codigo: string } | null>(null);

  protected readonly enUso = computed(() =>
    (this.catalogo()?.productos ?? []).filter((p) => p.estado !== 'archivado'),
  );
  /** Los archivados no se borran (RN-096): quedan aparte, plegados, con su historial. */
  protected readonly archivados = computed(() =>
    (this.catalogo()?.productos ?? []).filter((p) => p.estado === 'archivado'),
  );
  protected readonly funciones = computed(() => this.catalogo()?.funciones ?? []);
  protected readonly moneda = computed(() => this.catalogo()?.moneda ?? 'MXN');
  /** Sin un producto que se venda, la pantalla Plan no puede cobrar. */
  protected readonly aLaVenta = computed(() =>
    (this.catalogo()?.productos ?? []).some((p) => p.seVende),
  );

  protected readonly fallas = computed<ReadonlySet<Campo>>(() => {
    const b = this.borrador();
    if (!b || !this.intento()) {
      return new Set();
    }
    const fallas = new Set<Campo>();
    const conPrecio =
      this.hojaAbierta() === 'precio' || !this.editando() || this.editando()!.estado === 'borrador';
    if (this.hojaAbierta() === 'producto') {
      const nombre = b.nombre.trim();
      if (!nombre || nombre.length > MAXIMOS.nombre) {
        fallas.add('nombre');
      }
      if (b.descripcion.trim().length > MAXIMOS.descripcion) {
        fallas.add('descripcion');
      }
      if (b.funciones.length === 0) {
        fallas.add('funciones');
      }
      const orden = Number(b.orden);
      if (!/^\d+$/.test(b.orden.trim()) || orden < 1 || orden > 999) {
        fallas.add('orden');
      }
    }
    if (conPrecio) {
      const primera = b.primera.trim();
      if (!CIFRA.test(primera) || Number(primera) < 1 || Number(primera) > 10_000) {
        fallas.add('primera');
      }
      const adicional = b.adicional.trim();
      if (!CIFRA.test(adicional) || Number(adicional) > 10_000) {
        fallas.add('adicional');
      }
    }
    return fallas;
  });

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      this.catalogo.set(await this.servicio.catalogo());
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected estado(p: ProductoAdminDto): string {
    return p.estado === 'activo' && !p.seVende ? 'sinSincronizar' : p.estado;
  }

  protected tonoEstado(p: ProductoAdminDto): string {
    return { activo: p.seVende ? 'exito' : 'aviso', borrador: 'accion' }[p.estado] ?? '';
  }

  /** La versión que se cobra; en borrador, la que se publicará. */
  protected precioActual(p: ProductoAdminDto): PrecioAdminDto | null {
    return (
      p.precios.find((v) => v.estado === 'pendiente') ??
      p.precios.find((v) => v.estado === 'vigente') ??
      null
    );
  }

  protected versiones(p: ProductoAdminDto): PrecioAdminDto[] {
    return [...p.precios].sort((a, b) => Number(b.version) - Number(a.version));
  }

  protected fechaVersion(v: PrecioAdminDto): string {
    const fecha =
      v.estado === 'archivado'
        ? v.archivadoEn
        : v.estado === 'vigente'
          ? v.vigenteDesde
          : v.creadoEn;
    return fecha ? fechaAviso(fecha) : '';
  }

  protected dinero(monto: number | string): string {
    return pesos(monto).replace(/\.00$/, '');
  }

  protected meses(lista: readonly (number | string)[]): string {
    if (lista.length === 0) {
      return this.i18n.translate('admin.productos.sinOxxo');
    }
    return lista
      .map(Number)
      .sort((a, b) => a - b)
      .map((m) => this.i18n.translate(`admin.productos.mes.${forma(m)}`, { n: m }))
      .join(', ');
  }

  protected nuevo(): void {
    const ultimo = Math.max(0, ...(this.catalogo()?.productos ?? []).map((p) => Number(p.orden)));
    this.abrir('producto', null, {
      nombre: '',
      descripcion: '',
      funciones: [],
      meses: [1, 3, 6],
      orden: String(Math.min(999, ultimo + 1)),
      primera: '',
      adicional: '',
    });
  }

  protected editar(p: ProductoAdminDto): void {
    const precio = this.precioActual(p);
    this.abrir('producto', p, {
      nombre: p.nombre,
      descripcion: p.descripcion,
      funciones: [...p.funciones],
      meses: p.mesesPrepago.map(Number),
      orden: String(p.orden),
      primera: precio ? String(Number(precio.precioPrimera)) : '',
      adicional: precio ? String(Number(precio.precioAdicional)) : '',
    });
  }

  protected abrirPrecio(p: ProductoAdminDto): void {
    const precio = this.precioActual(p);
    this.abrir('precio', p, {
      nombre: p.nombre,
      descripcion: p.descripcion,
      funciones: [...p.funciones],
      meses: p.mesesPrepago.map(Number),
      orden: String(p.orden),
      primera: precio ? String(Number(precio.precioPrimera)) : '',
      adicional: precio ? String(Number(precio.precioAdicional)) : '',
    });
  }

  private abrir(hoja: Hojas, p: ProductoAdminDto | null, borrador: Borrador): void {
    this.hojaAbierta.set(hoja);
    this.editando.set(p);
    this.borrador.set(borrador);
    this.intento.set(false);
    this.errorGuardar.set(null);
    this.hoja().abrir();
  }

  protected cambiar<K extends keyof Borrador>(campo: K, valor: Borrador[K]): void {
    this.borrador.update((b) => (b ? { ...b, [campo]: valor } : b));
  }

  protected alternarFuncion(f: string, si: boolean): void {
    const actuales = this.borrador()?.funciones ?? [];
    this.cambiar('funciones', si ? [...actuales, f] : actuales.filter((x) => x !== f));
  }

  protected alternarMes(m: number, si: boolean): void {
    const actuales = this.borrador()?.meses ?? [];
    this.cambiar(
      'meses',
      (si ? [...actuales, m] : actuales.filter((x) => x !== m)).sort((a, b) => a - b),
    );
  }

  protected falla(campo: Campo): boolean {
    return this.fallas().has(campo);
  }

  private validar(): boolean {
    this.intento.set(true);
    if (this.fallas().size === 0) {
      return true;
    }
    // WCAG 3.3.1: en la hoja del celular el error puede quedar fuera de la vista; el foco lo lleva ahí.
    afterNextRender(
      () =>
        this.formulario()
          ?.nativeElement.querySelector<HTMLElement>('[aria-invalid="true"]')
          ?.focus(),
      { injector: this.injector },
    );
    return false;
  }

  protected async guardar(evento: Event): Promise<void> {
    evento.preventDefault();
    const b = this.borrador();
    if (!this.validar() || !b || this.guardando()) {
      return;
    }
    const actual = this.editando();
    const conPrecio = !actual || actual.estado === 'borrador';
    const entrada: ProductoEntrada = {
      nombre: b.nombre.trim().replace(/\s+/g, ' '),
      descripcion: b.descripcion.trim(),
      funciones: [...b.funciones],
      mesesPrepago: [...b.meses],
      orden: Number(b.orden),
      precioPrimera: conPrecio ? Number(b.primera) : null,
      precioAdicional: conPrecio ? Number(b.adicional) : null,
    };
    await this.ejecutar(
      () =>
        actual
          ? this.servicio.editarProducto(actual.id, entrada)
          : this.servicio.crearProducto(entrada),
      actual ? 'guardado' : 'creado',
    );
  }

  protected async guardarPrecio(evento: Event): Promise<void> {
    evento.preventDefault();
    const b = this.borrador();
    const actual = this.editando();
    if (!this.validar() || !b || !actual || this.guardando()) {
      return;
    }
    await this.ejecutar(
      () =>
        this.servicio.cambiarPrecio(actual.id, {
          precioPrimera: Number(b.primera),
          precioAdicional: Number(b.adicional),
        }),
      'precioCambiado',
    );
  }

  private async ejecutar(llamada: () => Promise<ProductoAdminDto>, aviso: string): Promise<void> {
    this.guardando.set(true);
    this.errorGuardar.set(null);
    try {
      const guardado = await llamada();
      this.reemplazar(guardado);
      this.hoja().cerrar();
      this.avisar(guardado, aviso);
    } catch (e) {
      this.errorGuardar.set(codigoDeError(e));
    } finally {
      this.guardando.set(false);
    }
  }

  protected pedirArchivo(p: ProductoAdminDto): void {
    this.errorAccion.set(null);
    this.porArchivar.set(p.id);
  }

  protected async accion(
    p: ProductoAdminDto,
    cual: 'publicar' | 'archivar' | 'sincronizar',
  ): Promise<void> {
    if (this.enCurso()) {
      return;
    }
    this.porArchivar.set(null);
    this.errorAccion.set(null);
    this.enCurso.set(`${p.id}:${cual}`);
    try {
      const llamadas = {
        publicar: () => this.servicio.publicarProducto(p.id),
        archivar: () => this.servicio.archivarProducto(p.id),
        sincronizar: () => this.servicio.sincronizarProducto(p.id),
      };
      const guardado = await llamadas[cual]();
      this.reemplazar(guardado);
      this.avisar(
        guardado,
        { publicar: 'publicado', archivar: 'archivado', sincronizar: 'sincronizado' }[cual],
      );
    } catch (e) {
      this.errorAccion.set({ id: p.id, codigo: codigoDeError(e) });
    } finally {
      this.enCurso.set(null);
    }
  }

  /** Un producto que quedó con error de sincronización se guardó igual: se avisa del error, no del éxito. */
  private avisar(p: ProductoAdminDto, clave: string): void {
    const final = p.errorSincronizacion ? 'pendienteSincronizar' : clave;
    this.avisos.mostrar(
      this.i18n.translate(`admin.productos.avisos.${final}`, { nombre: p.nombre }),
    );
  }

  private reemplazar(p: ProductoAdminDto): void {
    this.catalogo.update((c) =>
      c
        ? {
            ...c,
            productos: [...c.productos.filter((x) => x.id !== p.id), p].sort(
              (x, y) =>
                Number(x.orden) - Number(y.orden) || x.nombre.localeCompare(y.nombre, 'es-MX'),
            ),
          }
        : c,
    );
  }

  protected valor(evento: Event): string {
    return (evento.target as HTMLInputElement).value;
  }

  protected marcado(evento: Event): boolean {
    return (evento.target as HTMLInputElement).checked;
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }
}
