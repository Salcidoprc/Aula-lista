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
import { entero, pesos, porciento, semanaCorta } from './panel';
import { Tendencia } from './tendencia';

type Serie = 'activos' | 'registros' | 'pagan' | 'bajas' | 'boletas' | 'observaciones' | 'avisos';

interface Renglon {
  readonly clave: string;
  readonly cifra: string;
  readonly detalle: string;
  readonly serie: readonly number[] | null;
  readonly lectura: string;
  readonly peligro: boolean;
}

interface Bloque {
  readonly clave: 'uso' | 'dinero' | 'valor';
  readonly renglones: readonly Renglon[];
}

interface Paso {
  readonly paso: string;
  readonly cifra: string;
  readonly porcentaje: string;
  readonly escala: number;
  readonly delAnterior: string | null;
}

interface Cohorte {
  readonly semana: string;
  readonly docentes: string;
  readonly valores: readonly string[];
}

/**
 * RF-094: el negocio de un vistazo. Una frase con lo que importa y cinco bloques que se leen como un informe: uso,
 * dinero, activación, valor entregado y retención. Las cifras con historia llevan su renglón de semanas, con la semana
 * en curso al final como en la boleta del piloto. Solo conteos y montos (RN-090).
 */
@Component({
  selector: 'app-resumen',
  imports: [TranslocoDirective, Tendencia],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t; prefix: 'admin.resumen'">
      <div class="cabeza">
        <h1 tabindex="-1">{{ t('titulo') }}</h1>
        @if (datos(); as d) {
          <p class="resumen">
            {{ t('frase.hoy') }}
            <strong>{{
              t('docentes.' + forma(d.uso.activosDia), { n: entero(d.uso.activosDia) })
            }}</strong
            >{{ t('frase.mes') }} <strong>{{ entero(d.uso.activosMes) }}</strong>
            {{ t('frase.de', { total: entero(d.uso.docentes) }) }}
            {{ t('frase.entran') }}
            <strong>{{ pesos(d.dinero.mrr) }} {{ d.dinero.moneda }}</strong>
            {{ t('frase.margen') }}
            <strong [class.negativo]="esNegativo(d.dinero.margen)"
              >{{ pesos(d.dinero.margen) }} {{ d.dinero.moneda }}</strong
            >.
          </p>
        }
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
        <p class="leyenda">{{ t('leyenda') }}</p>
        <div class="bloques">
          @for (b of bloques(); track b.clave) {
            <section class="bloque" [attr.aria-labelledby]="'bloque-' + b.clave">
              <h2 [id]="'bloque-' + b.clave">{{ t(b.clave + '.titulo') }}</h2>
              <dl class="renglones">
                @for (r of b.renglones; track r.clave) {
                  <div class="renglon">
                    <dt>
                      {{ t(b.clave + '.' + r.clave) }}
                      <span class="secundario">{{ r.detalle }}</span>
                    </dt>
                    <dd class="cifra" [class.negativo]="r.peligro">{{ r.cifra }}</dd>
                    @if (r.serie; as serie) {
                      <dd class="semanas">
                        <app-tendencia [serie]="serie" [lectura]="r.lectura" />
                      </dd>
                    }
                  </div>
                }
              </dl>
            </section>

            @if (b.clave === 'dinero') {
              <section class="bloque" aria-labelledby="bloque-embudo">
                <h2 id="bloque-embudo">{{ t('embudo.titulo') }}</h2>
                <p class="nota">{{ t('embudo.nota') }}</p>
                <ol class="embudo">
                  @for (p of embudo(); track p.paso) {
                    <li>
                      <span class="paso">{{ t('embudo.paso.' + p.paso) }}</span>
                      <span class="cifras">
                        <span class="conteo">{{ p.cifra }}</span>
                        <span class="porcentaje">{{ p.porcentaje }}</span>
                      </span>
                      <span class="pista" aria-hidden="true">
                        <span class="relleno" [style.transform]="'scaleX(' + p.escala + ')'"></span>
                      </span>
                      @if (p.delAnterior; as anterior) {
                        <span class="secundario">{{
                          t('embudo.delAnterior', { p: anterior })
                        }}</span>
                      }
                    </li>
                  }
                </ol>
              </section>
            }
          }

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
                <table class="libreta cohortes">
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
                        @for (v of c.valores; track $index) {
                          <td class="num" [class.sin-datos]="v === '—'">{{ v }}</td>
                        }
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }
          </section>
        </div>

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
    </ng-container>
  `,
  styles: [
    ESTILOS_PANEL,
    `
      .resumen .negativo,
      .cifra.negativo {
        color: var(--color-peligro-tinta);
      }

      .leyenda,
      .nota {
        margin: 0;
        color: var(--color-texto-suave);
        font-size: var(--texto-sm);
        text-wrap: pretty;
      }

      .bloques {
        display: grid;
        grid-template-columns: minmax(0, 1fr);
        gap: var(--espacio-6);
      }

      .bloque {
        display: grid;
        align-content: start;
        gap: var(--espacio-3);
        min-width: 0;
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
        row-gap: var(--espacio-1);
        padding-block: var(--espacio-3);
        border-block-end: 1px solid var(--color-divisoria);
      }

      .renglon dt {
        grid-row: span 2;
        align-self: center;
        font-weight: var(--peso-medio);
      }

      .renglon dd {
        margin: 0;
        justify-self: end;
      }

      .cifra {
        font-family: var(--fuente-cifras);
        font-size: var(--texto-xl);
        font-variant-numeric: tabular-nums;
        font-weight: var(--peso-fuerte);
        line-height: var(--interlineado-ajustado);
        white-space: nowrap;
      }

      .embudo {
        display: grid;
        margin: 0;
        padding: 0;
        border-block-start: 1px solid var(--color-divisoria);
        list-style: none;
      }

      .embudo li {
        display: grid;
        grid-template-columns: minmax(0, 1fr) auto;
        column-gap: var(--espacio-4);
        row-gap: var(--espacio-2);
        padding-block: var(--espacio-3);
        border-block-end: 1px solid var(--color-divisoria);
      }

      .paso {
        font-weight: var(--peso-medio);
      }

      .cifras {
        display: inline-flex;
        gap: var(--espacio-3);
        align-items: baseline;
        font-family: var(--fuente-cifras);
        font-variant-numeric: tabular-nums;
        white-space: nowrap;
      }

      .conteo {
        font-weight: var(--peso-fuerte);
      }

      .porcentaje {
        min-inline-size: 3.75rem;
        color: var(--color-texto-suave);
        font-size: var(--texto-sm);
        text-align: end;
      }

      .pista {
        grid-column: 1 / -1;
        display: block;
        block-size: 8px;
        border-radius: var(--radio-sm);
        background: var(--color-superficie-hundida);
        overflow: hidden;
      }

      .relleno {
        display: block;
        block-size: 100%;
        background: var(--color-accion);
        transform-origin: left center;
      }

      .embudo .secundario {
        grid-column: 1 / -1;
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

      .cohortes th:first-child {
        white-space: nowrap;
      }

      /* En el celular la tabla cabe sin deslizar: el encabezado de la cohorte parte en dos renglones. */
      @media (max-width: 47.99rem) {
        .cohortes th,
        .cohortes td {
          padding-inline: var(--espacio-1);
        }

        .cohortes thead th:first-child {
          white-space: normal;
        }
      }

      .cohortes td.sin-datos {
        color: var(--color-texto-tenue);
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

      @media (min-width: 64rem) {
        .bloques {
          grid-template-columns: repeat(2, minmax(0, 1fr));
          column-gap: var(--espacio-7);
        }

        .como dl {
          grid-template-columns: repeat(2, minmax(0, 1fr));
          max-width: none;
          gap: var(--espacio-4) var(--espacio-6);
        }
      }

      @media (forced-colors: active) {
        .relleno {
          background: Highlight;
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
  protected readonly entero = entero;
  protected readonly pesos = pesos;
  protected readonly forma = forma;
  protected readonly semanasRetencion = [1, 2, 4] as const;
  protected readonly claves = [
    'activos',
    'frecuencia',
    'embudo',
    'retencion',
    'mrr',
    'margen',
  ] as const;

  protected readonly bloques = computed<readonly Bloque[]>(() => {
    const d = this.datos();
    if (!d) {
      return [];
    }
    const { uso, dinero, valor } = d;
    const moneda = dinero.moneda;
    const r = (
      bloque: string,
      clave: string,
      cifra: string,
      detalle: string,
      serie: Serie | null = null,
      peligro = false,
    ): Renglon => ({
      clave,
      cifra,
      detalle,
      serie: serie ? d.semanas.map((s) => Number(s[serie])) : null,
      lectura: serie ? this.lectura(`admin.resumen.${bloque}.${clave}`, d.semanas, serie) : '',
      peligro,
    });
    const tx = (clave: string, params?: Record<string, unknown>) =>
      this.i18n.translate(`admin.resumen.${clave}`, params);
    return [
      {
        clave: 'uso',
        renglones: [
          r('uso', 'activosDia', entero(uso.activosDia), tx('uso.activosDiaDetalle')),
          r(
            'uso',
            'activosSemana',
            entero(uso.activosSemana),
            tx('uso.activosSemanaDetalle'),
            'activos',
          ),
          r(
            'uso',
            'activosMes',
            entero(uso.activosMes),
            tx('uso.activosMesDetalle', { total: entero(uso.docentes) }),
          ),
          r('uso', 'frecuencia', porciento(uso.porcentajeFrecuencia), tx('uso.frecuenciaDetalle')),
          r(
            'uso',
            'registros',
            entero(uso.registrosSemana),
            tx('uso.registrosDetalle', { n: entero(uso.registrosMes) }),
            'registros',
          ),
          r(
            'uso',
            'alumnos',
            entero(uso.alumnos),
            tx('uso.alumnosDetalle.' + forma(uso.grupos), { n: entero(uso.grupos) }),
          ),
        ],
      },
      {
        clave: 'dinero',
        renglones: [
          r('dinero', 'mrr', `${pesos(dinero.mrr)} ${moneda}`, tx('dinero.mrrDetalle')),
          r(
            'dinero',
            'pagan',
            entero(dinero.docentesQuePagan),
            tx('dinero.paganDetalle.' + forma(dinero.gruposPro), { n: entero(dinero.gruposPro) }),
            'pagan',
          ),
          r(
            'dinero',
            'promedio',
            dinero.ingresoPromedio === null ? '—' : `${pesos(dinero.ingresoPromedio)} ${moneda}`,
            tx('dinero.promedioDetalle'),
          ),
          r('dinero', 'pruebas', entero(dinero.pruebasActivas), tx('dinero.pruebasDetalle')),
          r('dinero', 'bajas', entero(dinero.bajasMes), tx('dinero.bajasDetalle'), 'bajas'),
          r('dinero', 'costo', `${pesos(dinero.costoMes)} ${moneda}`, tx('dinero.costoDetalle')),
          r(
            'dinero',
            'margen',
            `${pesos(dinero.margen)} ${moneda}`,
            tx('dinero.margenDetalle'),
            null,
            this.esNegativo(dinero.margen),
          ),
        ],
      },
      {
        clave: 'valor',
        renglones: [
          r('valor', 'boletas', entero(valor.boletas), tx('valor.boletasDetalle'), 'boletas'),
          r(
            'valor',
            'observaciones',
            entero(valor.observaciones),
            tx('valor.observacionesDetalle'),
            'observaciones',
          ),
          r('valor', 'avisos', entero(valor.avisos), tx('valor.avisosDetalle'), 'avisos'),
          r(
            'valor',
            'leidos',
            porciento(valor.porcentajeAvisosLeidos),
            tx('valor.leidosDetalle.' + forma(valor.entregas), { n: entero(valor.entregas) }),
          ),
        ],
      },
    ];
  });

  protected readonly embudo = computed<readonly Paso[]>(() =>
    (this.datos()?.embudo ?? []).map((p) => ({
      paso: p.paso,
      cifra: entero(p.docentes),
      porcentaje: porciento(p.porcentaje),
      escala: p.porcentaje === null ? 0 : Math.min(1, Number(p.porcentaje) / 100),
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
        valores: [c.semana1, c.semana2, c.semana4].map((v) => porciento(v)),
      })),
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

  protected esNegativo(monto: number | string): boolean {
    return Number(monto) < 0;
  }

  /** "Activos en 7 días por semana: 13 oct 4, 20 oct 6, …": lo que el renglón de semanas dice con barras. */
  private lectura(clave: string, semanas: readonly SemanaResumenDto[], serie: Serie): string {
    const valores = semanas.map((s) => `${semanaCorta(s.semana)} ${entero(s[serie])}`).join(', ');
    return this.i18n.translate('admin.resumen.porSemana', {
      indicador: this.i18n.translate(clave),
      valores,
    });
  }
}
