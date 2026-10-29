import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ResumenDto } from '../api/models/resumen-dto';
import { SemanaResumenDto } from '../api/models/semana-resumen-dto';
import { codigoDeError } from '../core/errores/problema';
import { forma } from '../shared/plural';
import { AdminService } from './admin.service';
import { ESTILOS_PANEL } from './estilos';
import { FormaGrafica, Grafica } from './grafica';
import { entero, pesos, porciento, semanaCorta } from './panel';

type Serie =
  | 'activos'
  | 'registros'
  | 'pagan'
  | 'altasPago'
  | 'bajas'
  | 'boletas'
  | 'observaciones'
  | 'avisos';

/** Las columnas de "Ver los datos por semana", en el orden en que aparecen las gráficas. */
const SERIES_TABLA: readonly Serie[] = [
  'activos',
  'registros',
  'pagan',
  'altasPago',
  'bajas',
  'boletas',
  'observaciones',
  'avisos',
];

interface Cifra {
  readonly clave: string;
  readonly valor: string;
  readonly detalle: string;
  readonly negativo: boolean;
}

interface Figura {
  readonly serie: Serie;
  readonly forma: FormaGrafica;
  readonly valores: readonly number[];
  readonly lectura: string;
  readonly reciente: string;
}

interface Renglon {
  readonly clave: string;
  readonly cifra: string;
  readonly detalle: string;
  /** De 0 a 1 cuando la cifra es un porcentaje que se dibuja como medidor. */
  readonly medidor: number | null;
}

interface Paso {
  readonly paso: string;
  readonly cifra: string;
  readonly porcentaje: string;
  readonly ancho: number;
  readonly delAnterior: string | null;
}

interface Barra {
  readonly clave: 'ingreso' | 'costo';
  readonly monto: string;
  readonly ancho: number;
}

interface Celda {
  readonly texto: string;
  /** Paso del mapa de calor: 0 es nadie volvió y 4 es más de 75 %; `null` es una semana que no termina. */
  readonly paso: number | null;
}

interface Cohorte {
  readonly semana: string;
  readonly docentes: string;
  readonly celdas: readonly Celda[];
}

/** El paso del mapa de calor de retención: cuatro tramos de 25 % y el cero aparte. */
export function pasoDeCalor(porcentaje: number | string | null): number | null {
  if (porcentaje === null) {
    return null;
  }
  const p = Number(porcentaje);
  return p <= 0 ? 0 : Math.min(4, Math.ceil(p / 25));
}

/**
 * RF-094: el tablero del dueño. Una franja con las cuatro cifras que importan y, debajo, la tendencia de 12 semanas en
 * gráficas de una tinta, el embudo de activación en barras, la retención como mapa de calor y la tabla con todos los
 * datos. Solo conteos y montos (RN-090). Nada se anima.
 */
@Component({
  selector: 'app-resumen',
  imports: [NgTemplateOutlet, TranslocoDirective, Grafica],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t; prefix: 'admin.resumen'">
      <div class="cabeza">
        <h1 tabindex="-1">{{ t('titulo') }}</h1>
        <p class="resumen">{{ t('leyenda') }}</p>
      </div>

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (!datos()) {
        <div class="cargando" role="status" [attr.aria-label]="t('cargando')">
          @for (i of [1, 2, 3, 4, 5, 6]; track i) {
            <span class="esqueleto" aria-hidden="true"></span>
          }
        </div>
      } @else {
        <dl class="franja">
          @for (c of franja(); track c.clave) {
            <div>
              <dt>{{ t('franja.' + c.clave) }}</dt>
              <dd class="grande" [class.negativo]="c.negativo">{{ c.valor }}</dd>
              <dd class="secundario">{{ c.detalle }}</dd>
            </div>
          }
        </dl>

        <div class="bloques">
          <section class="bloque bloque--ancho" aria-labelledby="bloque-uso">
            <h2 id="bloque-uso">{{ t('uso.titulo') }}</h2>
            <div class="figuras figuras--dos">
              @for (f of figuras().uso; track f.serie) {
                <ng-container
                  [ngTemplateOutlet]="figura"
                  [ngTemplateOutletContext]="{ $implicit: f }"
                />
              }
            </div>
            <ng-container
              [ngTemplateOutlet]="lista"
              [ngTemplateOutletContext]="{ $implicit: renglones().uso, bloque: 'uso' }"
            />
          </section>

          <section class="bloque" aria-labelledby="bloque-dinero">
            <h2 id="bloque-dinero">{{ t('dinero.titulo') }}</h2>
            <dl class="balanza">
              @for (b of balanza(); track b.clave) {
                <div>
                  <dt>{{ t('dinero.' + b.clave) }}</dt>
                  <dd class="pista">
                    <span
                      class="relleno"
                      [class.relleno--costo]="b.clave === 'costo'"
                      [style.width.%]="b.ancho"
                    ></span>
                  </dd>
                  <dd class="monto">{{ b.monto }}</dd>
                </div>
              }
            </dl>
            @for (f of figuras().dinero; track f.serie) {
              <ng-container
                [ngTemplateOutlet]="figura"
                [ngTemplateOutletContext]="{ $implicit: f }"
              />
            }
            <ng-container
              [ngTemplateOutlet]="lista"
              [ngTemplateOutletContext]="{ $implicit: renglones().dinero, bloque: 'dinero' }"
            />
          </section>

          <section class="bloque" aria-labelledby="bloque-embudo">
            <h2 id="bloque-embudo">{{ t('embudo.titulo') }}</h2>
            <p class="nota">{{ t('embudo.nota') }}</p>
            <ol class="embudo">
              @for (p of embudo(); track p.paso) {
                <li>
                  <span class="paso">{{ t('embudo.paso.' + p.paso) }}</span>
                  @if (p.delAnterior; as anterior) {
                    <span class="secundario anterior">{{
                      t('embudo.delAnterior', { p: anterior })
                    }}</span>
                  }
                  <span class="pista" aria-hidden="true">
                    <span class="relleno" [style.width.%]="p.ancho"></span>
                  </span>
                  <span class="punta">
                    <span class="conteo">{{ p.cifra }}</span>
                    <span class="porcentaje">{{ p.porcentaje }}</span>
                  </span>
                </li>
              }
            </ol>
          </section>

          <section class="bloque" aria-labelledby="bloque-valor">
            <h2 id="bloque-valor">{{ t('valor.titulo') }}</h2>
            <div class="figuras">
              @for (f of figuras().valor; track f.serie) {
                <ng-container
                  [ngTemplateOutlet]="figura"
                  [ngTemplateOutletContext]="{ $implicit: f, alto: 112 }"
                />
              }
            </div>
            <ng-container
              [ngTemplateOutlet]="lista"
              [ngTemplateOutletContext]="{ $implicit: renglones().valor, bloque: 'valor' }"
            />
          </section>

          <section class="bloque" aria-labelledby="bloque-retencion">
            <h2 id="bloque-retencion">{{ t('retencion.titulo') }}</h2>
            <p class="nota">{{ t('retencion.nota') }}</p>
            @if (cohortes().length === 0) {
              <p class="vacio">{{ t('retencion.vacio') }}</p>
            } @else {
              <div
                class="desliza"
                tabindex="0"
                role="region"
                [attr.aria-label]="t('retencion.titulo')"
              >
                <table class="libreta calor">
                  <caption class="solo-lector">
                    {{
                      t('retencion.titulo')
                    }}
                  </caption>
                  <thead>
                    <tr>
                      <th scope="col">{{ t('retencion.cohorte') }}</th>
                      <th scope="col" class="num">{{ t('retencion.docentes') }}</th>
                      @for (s of semanasRetencion; track s) {
                        <th scope="col" class="num">{{ t('retencion.semana', { n: s }) }}</th>
                      }
                    </tr>
                  </thead>
                  <tbody>
                    @for (c of cohortes(); track c.semana) {
                      <tr>
                        <th scope="row">{{ c.semana }}</th>
                        <td class="num">{{ c.docentes }}</td>
                        @for (celda of c.celdas; track $index) {
                          <td class="num celda" [attr.data-paso]="celda.paso">{{ celda.texto }}</td>
                        }
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
              <p class="escala" aria-hidden="true">
                <span>0 %</span>
                @for (p of [0, 1, 2, 3, 4]; track p) {
                  <span class="muestra" [attr.data-paso]="p"></span>
                }
                <span>100 %</span>
              </p>
            }
          </section>
        </div>

        <details class="datos">
          <summary>{{ t('datos.titulo') }}</summary>
          <div class="desliza" tabindex="0" role="region" [attr.aria-label]="t('datos.titulo')">
            <table class="libreta">
              <caption class="solo-lector">
                {{
                  t('datos.titulo')
                }}
              </caption>
              <thead>
                <tr>
                  <th scope="col">{{ t('datos.semana') }}</th>
                  @for (s of seriesTabla; track s) {
                    <th scope="col" class="num">{{ t('serie.' + s) }}</th>
                  }
                </tr>
              </thead>
              <tbody>
                @for (s of semanasTabla(); track s.semana) {
                  <tr>
                    <th scope="row">{{ s.fecha }}</th>
                    @for (v of s.valores; track $index) {
                      <td class="num">{{ v }}</td>
                    }
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </details>

        <section class="como" aria-labelledby="como-titulo">
          <h2 id="como-titulo">{{ t('como.titulo') }}</h2>
          <dl>
            @for (clave of claves; track clave) {
              <div>
                <dt>{{ t('como.' + clave + '.nombre') }}</dt>
                <dd>{{ t('como.' + clave + '.texto') }}</dd>
              </div>
            }
          </dl>
        </section>
      }

      <ng-template #figura let-f let-alto="alto">
        <figure class="figura">
          <figcaption>
            <span class="titulo-figura">{{ t('serie.' + f.serie) }}</span>
            <span class="secundario">{{ f.reciente }}</span>
          </figcaption>
          <app-grafica
            [valores]="f.valores"
            [semanas]="semanas()"
            [forma]="f.forma"
            [lectura]="f.lectura"
            [textoSemana]="textoSemana"
            [alto]="alto ?? 160"
          />
        </figure>
      </ng-template>

      <ng-template #lista let-renglones let-bloque="bloque">
        <dl class="renglones">
          @for (r of renglones; track r.clave) {
            <div class="renglon">
              <dt>
                {{ t(bloque + '.' + r.clave) }}
                <span class="secundario">{{ r.detalle }}</span>
              </dt>
              <dd class="cifra">{{ r.cifra }}</dd>
              @if (r.medidor !== null) {
                <dd class="medidor" aria-hidden="true">
                  <span [style.width.%]="r.medidor * 100"></span>
                </dd>
              }
            </div>
          }
        </dl>
      </ng-template>
    </ng-container>
  `,
  styles: [
    ESTILOS_PANEL,
    `
      .negativo {
        color: var(--color-peligro-tinta);
      }

      .nota {
        margin: 0;
        color: var(--color-texto-suave);
        font-size: var(--texto-sm);
        text-wrap: pretty;
      }

      /* La franja: cuatro cifras sobre el papel, separadas por divisorias y sin tarjetas. */
      .franja {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        margin: 0;
        border-block: 1px solid var(--color-divisoria);
      }

      .franja > div {
        display: grid;
        align-content: start;
        gap: var(--espacio-1);
        padding: var(--espacio-4) var(--espacio-4) var(--espacio-4) 0;
      }

      .franja > div:nth-child(even) {
        padding-inline-start: var(--espacio-4);
        border-inline-start: 1px solid var(--color-divisoria);
      }

      .franja > div:nth-child(n + 3) {
        border-block-start: 1px solid var(--color-divisoria);
      }

      .franja dt {
        color: var(--color-texto-suave);
        font-size: var(--texto-sm);
        font-weight: var(--peso-medio);
      }

      .franja dd {
        margin: 0;
      }

      .grande {
        font-family: var(--fuente-cifras);
        font-size: var(--texto-xl);
        font-weight: var(--peso-fuerte);
        line-height: var(--interlineado-ajustado);
        letter-spacing: var(--tracking-titulo);
        white-space: nowrap;
      }

      .bloques {
        display: grid;
        grid-template-columns: minmax(0, 1fr);
        gap: var(--espacio-7);
      }

      .bloque {
        display: grid;
        align-content: start;
        gap: var(--espacio-4);
        min-width: 0;
      }

      .figuras {
        display: grid;
        grid-template-columns: minmax(0, 1fr);
        gap: var(--espacio-5);
      }

      .figura {
        display: grid;
        gap: var(--espacio-2);
        min-width: 0;
        margin: 0;
      }

      .figura figcaption {
        display: flex;
        flex-wrap: wrap;
        align-items: baseline;
        justify-content: space-between;
        gap: var(--espacio-1) var(--espacio-3);
      }

      .titulo-figura {
        font-weight: var(--peso-medio);
      }

      .renglones {
        margin: 0;
        border-block-start: 1px solid var(--color-divisoria);
      }

      .renglon {
        display: grid;
        grid-template-columns: minmax(0, 1fr) auto;
        align-items: center;
        column-gap: var(--espacio-4);
        row-gap: var(--espacio-2);
        padding-block: var(--espacio-3);
        border-block-end: 1px solid var(--color-divisoria);
      }

      .renglon dt {
        font-weight: var(--peso-medio);
      }

      .renglon dd {
        margin: 0;
      }

      .cifra {
        font-family: var(--fuente-cifras);
        font-size: var(--texto-xl);
        font-weight: var(--peso-fuerte);
        line-height: var(--interlineado-ajustado);
        text-align: end;
        white-space: nowrap;
      }

      .medidor {
        grid-column: 1 / -1;
        block-size: 8px;
        border-radius: var(--radio-sm);
        background: var(--color-accion-tinte);
        overflow: hidden;
      }

      .medidor span {
        display: block;
        block-size: 100%;
        border-radius: var(--radio-sm);
        background: var(--color-accion);
      }

      /* Ingreso contra costo: dos barras en la misma escala. */
      .balanza {
        display: grid;
        gap: var(--espacio-3);
        margin: 0;
      }

      .balanza > div {
        display: grid;
        grid-template-columns: 4.5rem minmax(0, 1fr) auto;
        align-items: center;
        gap: var(--espacio-3);
      }

      .balanza dt {
        color: var(--color-texto-suave);
        font-size: var(--texto-sm);
      }

      .balanza dd {
        margin: 0;
      }

      .pista {
        display: block;
        block-size: 20px;
      }

      .relleno {
        display: block;
        min-inline-size: 2px;
        block-size: 100%;
        border-radius: 0 var(--radio-sm) var(--radio-sm) 0;
        background: var(--color-accion);
      }

      .relleno--costo {
        background: var(--color-texto-tenue);
      }

      .monto {
        font-family: var(--fuente-cifras);
        font-weight: var(--peso-fuerte);
        text-align: end;
        white-space: nowrap;
      }

      .embudo {
        display: grid;
        gap: var(--espacio-4);
        margin: 0;
        padding: 0;
        list-style: none;
      }

      .embudo li {
        display: grid;
        grid-template-columns: minmax(0, 1fr) auto;
        align-items: center;
        column-gap: var(--espacio-3);
        row-gap: var(--espacio-1);
      }

      .paso {
        font-weight: var(--peso-medio);
      }

      .anterior {
        text-align: end;
      }

      .embudo .pista {
        grid-column: 1;
        border-radius: 0 var(--radio-sm) var(--radio-sm) 0;
        background: var(--color-superficie-hundida);
      }

      .punta {
        display: inline-flex;
        gap: var(--espacio-2);
        align-items: baseline;
        font-family: var(--fuente-cifras);
        white-space: nowrap;
      }

      .conteo {
        font-weight: var(--peso-fuerte);
      }

      .porcentaje {
        min-inline-size: 3.5rem;
        color: var(--color-texto-suave);
        font-size: var(--texto-sm);
        text-align: end;
      }

      .desliza {
        /* Contiene el texto oculto para lectores; sin esto ensancha la página en el celular. */
        position: relative;
        overflow-x: auto;
        border-radius: var(--radio-lg);
        outline: none;
        overscroll-behavior-x: contain;
      }

      .desliza:focus-visible {
        box-shadow: var(--foco);
      }

      /* Mapa de calor: cinco pasos de la tinta de acción; el texto cambia de tinta donde el fondo oscurece. */
      .calor th:first-child {
        white-space: nowrap;
      }

      .calor td.celda {
        border: 2px solid var(--color-fondo);
        border-radius: var(--radio-sm);
        font-weight: var(--peso-medio);
        text-align: center;
      }

      [data-paso='0'] {
        background: var(--color-superficie-hundida);
      }

      [data-paso='1'] {
        background: color-mix(in oklab, var(--color-accion) 24%, var(--color-superficie));
      }

      [data-paso='2'] {
        background: color-mix(in oklab, var(--color-accion) 40%, var(--color-superficie));
      }

      [data-paso='3'] {
        background: color-mix(in oklab, var(--color-accion) 56%, var(--color-superficie));
      }

      [data-paso='4'] {
        background: var(--color-accion);
        color: var(--color-sobre-accion);
      }

      .calor td.celda:not([data-paso]) {
        color: var(--color-texto-tenue);
      }

      .escala {
        display: flex;
        align-items: center;
        gap: 2px;
        margin: 0;
        color: var(--color-texto-suave);
        font-family: var(--fuente-cifras);
        font-size: var(--texto-xs);
      }

      .escala span:first-child {
        margin-inline-end: var(--espacio-2);
      }

      .escala span:last-child {
        margin-inline-start: var(--espacio-2);
      }

      .muestra {
        inline-size: 1.5rem;
        block-size: 0.75rem;
        border-radius: 2px;
      }

      @media (max-width: 47.99rem) {
        .calor th,
        .calor td {
          padding-inline: var(--espacio-1);
        }

        .calor thead th:first-child {
          white-space: normal;
        }
      }

      .datos summary {
        min-block-size: 44px;
        display: flex;
        align-items: center;
        color: var(--color-accion);
        font-weight: var(--peso-medio);
        cursor: pointer;
      }

      .datos summary:focus-visible {
        border-radius: var(--radio-sm);
        outline: none;
        box-shadow: var(--foco);
      }

      .datos tbody th {
        white-space: nowrap;
      }

      .como dl {
        display: grid;
        gap: var(--espacio-3);
        max-width: var(--ancho-lectura);
        margin: var(--espacio-3) 0 0;
      }

      .como dt {
        font-weight: var(--peso-medio);
      }

      .como dd {
        margin: 0;
        color: var(--color-texto-suave);
        font-size: var(--texto-sm);
      }

      @media (min-width: 48rem) {
        .grande {
          font-size: var(--texto-2xl);
        }

        .franja {
          grid-template-columns: repeat(4, minmax(0, 1fr));
        }

        .franja > div:nth-child(n) {
          padding-inline: var(--espacio-4);
          border-block-start: 0;
          border-inline-start: 1px solid var(--color-divisoria);
        }

        .franja > div:first-child {
          padding-inline-start: 0;
          border-inline-start: 0;
        }

        .figuras--dos {
          grid-template-columns: repeat(2, minmax(0, 1fr));
          column-gap: var(--espacio-6);
        }
      }

      @media (min-width: 64rem) {
        .bloques {
          grid-template-columns: repeat(2, minmax(0, 1fr));
          column-gap: var(--espacio-7);
        }

        .bloque--ancho {
          grid-column: 1 / -1;
        }

        .como dl {
          grid-template-columns: repeat(2, minmax(0, 1fr));
          max-width: none;
          gap: var(--espacio-4) var(--espacio-6);
        }
      }

      @media (forced-colors: active) {
        .relleno,
        .medidor span {
          background: Highlight;
        }

        .medidor {
          border: 1px solid CanvasText;
        }

        .calor td.celda[data-paso] {
          forced-color-adjust: none;
          background: Canvas;
          color: CanvasText;
        }
      }
    `,
  ],
})
export class Resumen implements OnInit {
  private readonly servicio = inject(AdminService);
  private readonly i18n = inject(TranslocoService);

  protected readonly datos = signal<ResumenDto | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly semanasRetencion = [1, 2, 4] as const;
  protected readonly seriesTabla = SERIES_TABLA;
  protected readonly claves = [
    'activos',
    'frecuencia',
    'embudo',
    'retencion',
    'mrr',
    'margen',
  ] as const;

  protected readonly textoSemana = (semana: string, enCurso: boolean): string =>
    this.i18n.translate(`admin.resumen.${enCurso ? 'semanaEnCurso' : 'semanaDel'}`, {
      fecha: semanaCorta(semana),
    });

  protected readonly semanas = computed(() => (this.datos()?.semanas ?? []).map((s) => s.semana));

  protected readonly franja = computed<readonly Cifra[]>(() => {
    const d = this.datos();
    if (!d) {
      return [];
    }
    const { uso, dinero } = d;
    const moneda = dinero.moneda;
    return [
      {
        clave: 'activosDia',
        valor: entero(uso.activosDia),
        detalle: this.tx('uso.activosDiaDetalle'),
        negativo: false,
      },
      {
        clave: 'activosMes',
        valor: entero(uso.activosMes),
        detalle: this.tx('uso.activosMesDetalle', { total: entero(uso.docentes) }),
        negativo: false,
      },
      {
        clave: 'mrr',
        valor: `${pesos(dinero.mrr)} ${moneda}`,
        detalle: this.tx('franja.mrrDetalle.' + forma(dinero.docentesQuePagan), {
          n: entero(dinero.docentesQuePagan),
        }),
        negativo: false,
      },
      {
        clave: 'margen',
        valor: `${pesos(dinero.margen)} ${moneda}`,
        detalle: this.tx('dinero.margenDetalle'),
        negativo: Number(dinero.margen) < 0,
      },
    ];
  });

  protected readonly figuras = computed(() => {
    const d = this.datos();
    const f = (serie: Serie, forma: FormaGrafica = 'columnas'): Figura => {
      const semanas = d?.semanas ?? [];
      const valores = semanas.map((s) => Number(s[serie]));
      const n = valores.length;
      return {
        serie,
        forma,
        valores,
        lectura: this.lectura(serie, semanas),
        reciente:
          n < 2
            ? ''
            : this.tx('reciente', {
                actual: entero(valores[n - 1]),
                anterior: entero(valores[n - 2]),
              }),
      };
    };
    return {
      uso: [f('activos', 'linea'), f('registros')],
      dinero: [f('pagan')],
      valor: [f('boletas'), f('observaciones'), f('avisos')],
    };
  });

  protected readonly renglones = computed(() => {
    const d = this.datos();
    if (!d) {
      return { uso: [], dinero: [], valor: [] };
    }
    const { uso, dinero, valor } = d;
    const moneda = dinero.moneda;
    const medidor = (p: number | string | null) =>
      p === null ? null : Math.min(1, Math.max(0, Number(p) / 100));
    const r = (
      clave: string,
      cifra: string,
      detalle: string,
      m: number | null = null,
    ): Renglon => ({
      clave,
      cifra,
      detalle,
      medidor: m,
    });
    return {
      uso: [
        r('activosSemana', entero(uso.activosSemana), this.tx('uso.activosSemanaDetalle')),
        r(
          'frecuencia',
          porciento(uso.porcentajeFrecuencia),
          this.tx('uso.frecuenciaDetalle'),
          medidor(uso.porcentajeFrecuencia),
        ),
        r(
          'registros',
          entero(uso.registrosSemana),
          this.tx('uso.registrosDetalle', { n: entero(uso.registrosMes) }),
        ),
        r(
          'alumnos',
          entero(uso.alumnos),
          this.tx('uso.alumnosDetalle.' + forma(uso.grupos), { n: entero(uso.grupos) }),
        ),
      ],
      dinero: [
        r(
          'pagan',
          entero(dinero.docentesQuePagan),
          this.tx('dinero.paganDetalle.' + forma(dinero.gruposPro), {
            n: entero(dinero.gruposPro),
          }),
        ),
        r(
          'promedio',
          dinero.ingresoPromedio === null ? '—' : `${pesos(dinero.ingresoPromedio)} ${moneda}`,
          this.tx('dinero.promedioDetalle'),
        ),
        r('pruebas', entero(dinero.pruebasActivas), this.tx('dinero.pruebasDetalle')),
        r('bajas', entero(dinero.bajasMes), this.tx('dinero.bajasDetalle')),
      ],
      valor: [
        r(
          'leidos',
          porciento(valor.porcentajeAvisosLeidos),
          this.tx('valor.leidosDetalle.' + forma(valor.entregas), { n: entero(valor.entregas) }),
          medidor(valor.porcentajeAvisosLeidos),
        ),
      ],
    };
  });

  /** Ingreso y costo del mes en la misma escala: la barra más larga llena la pista. */
  protected readonly balanza = computed<readonly Barra[]>(() => {
    const d = this.datos();
    if (!d) {
      return [];
    }
    const ingreso = Math.max(0, Number(d.dinero.mrr));
    const costo = Math.max(0, Number(d.dinero.costoMes));
    const tope = Math.max(ingreso, costo) || 1;
    return [
      {
        clave: 'ingreso',
        monto: `${pesos(ingreso)} ${d.dinero.moneda}`,
        ancho: (ingreso / tope) * 100,
      },
      { clave: 'costo', monto: `${pesos(costo)} ${d.dinero.moneda}`, ancho: (costo / tope) * 100 },
    ];
  });

  protected readonly embudo = computed<readonly Paso[]>(() =>
    (this.datos()?.embudo ?? []).map((p) => ({
      paso: p.paso,
      cifra: entero(p.docentes),
      porcentaje: porciento(p.porcentaje),
      ancho: p.porcentaje === null ? 0 : Math.min(100, Number(p.porcentaje)),
      delAnterior: p.porcentajeDelAnterior === null ? null : porciento(p.porcentajeDelAnterior),
    })),
  );

  /** De la cohorte más nueva a la más vieja; las semanas en que nadie se registró no se listan. */
  protected readonly cohortes = computed<readonly Cohorte[]>(() =>
    (this.datos()?.retencion ?? [])
      .filter((c) => Number(c.docentes) > 0)
      .reverse()
      .map((c) => ({
        semana: semanaCorta(c.semana),
        docentes: entero(c.docentes),
        celdas: [c.semana1, c.semana2, c.semana4].map((v) => ({
          texto: porciento(v),
          paso: pasoDeCalor(v),
        })),
      })),
  );

  /** La tabla de "Ver los datos por semana": de la semana en curso hacia atrás. */
  protected readonly semanasTabla = computed(() =>
    (this.datos()?.semanas ?? [])
      .map((s, i, todas) => ({
        semana: s.semana,
        fecha: this.textoSemana(s.semana, i === todas.length - 1),
        valores: SERIES_TABLA.map((serie) => entero(s[serie])),
      }))
      .reverse(),
  );

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      this.datos.set(await this.servicio.resumen());
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  private tx(clave: string, params?: Record<string, unknown>): string {
    return this.i18n.translate(`admin.resumen.${clave}`, params);
  }

  /** "Docentes activos por semana: 13 oct 4, 20 oct 6, …": lo que la gráfica dice con marcas. */
  private lectura(serie: Serie, semanas: readonly SemanaResumenDto[]): string {
    const valores = semanas.map((s) => `${semanaCorta(s.semana)} ${entero(s[serie])}`).join(', ');
    return this.tx('porSemana', { indicador: this.tx('serie.' + serie), valores });
  }
}
