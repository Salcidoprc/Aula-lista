import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  afterRenderEffect,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { MetricasDto } from '../api/models/metricas-dto';
import { codigoDeError } from '../core/errores/problema';
import { AdminService } from './admin.service';
import { ESTILOS_PANEL } from './estilos';
import {
  ClaveMetrica,
  EstadoMeta,
  METRICAS,
  cifraMetrica,
  estadoMeta,
  metaDe,
  semanaCorta,
  valorMetrica,
} from './panel';

interface Celda {
  readonly texto: string;
  readonly estado: EstadoMeta;
}

interface Renglon {
  readonly clave: ClaveMetrica;
  readonly meta: string;
  readonly celdas: readonly Celda[];
}

/**
 * RF-091: las seis métricas del piloto como una boleta. Cada métrica es un renglón y cada semana una columna; la
 * celda se marca si cumple su meta, como la calificación aprobatoria de la boleta que entrega el docente. La semana
 * en curso va al final, destacada; en el celular la boleta se desliza y abre en esa semana.
 */
@Component({
  selector: 'app-metricas',
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t; prefix: 'admin.metricas'">
      <div class="cabeza">
        <h1 tabindex="-1">{{ t('titulo') }}</h1>
        @if (cumplidas(); as c) {
          <p class="resumen">
            {{ t('resumen.antes') }} <strong>{{ t('resumen.cuenta', c) }}</strong
            >{{ t('resumen.despues') }}
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
        <div class="desliza" #desliza tabindex="0" [attr.aria-label]="t('boleta')" role="region">
          <table class="libreta boleta">
            <caption class="solo-lector">
              {{
                t('boleta')
              }}
            </caption>
            <thead>
              <tr>
                <th scope="col" class="metrica">{{ t('metrica') }}</th>
                @for (s of semanas(); track s.semana; let ultima = $last) {
                  <th scope="col" class="num" [class.actual]="ultima">
                    @if (ultima) {
                      <span class="esta">{{ t('estaSemana') }}</span>
                    }
                    {{ corta(s.semana) }}
                  </th>
                }
              </tr>
            </thead>
            <tbody>
              @for (r of renglones(); track r.clave) {
                <tr>
                  <th scope="row" class="metrica">
                    {{ t('nombre.' + r.clave) }}
                    <span class="secundario">{{ r.meta }}</span>
                  </th>
                  @for (c of r.celdas; track $index; let ultima = $last) {
                    <td class="num" [class.actual]="ultima" [attr.data-estado]="c.estado">
                      <span class="valor">{{ c.texto }}</span>
                      @if (c.estado !== 'sinDatos') {
                        <span class="solo-lector">{{ t('estado.' + c.estado) }}</span>
                      }
                    </td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>

        <section class="como" aria-labelledby="como-titulo">
          <h2 id="como-titulo">{{ t('como.titulo') }}</h2>
          <dl>
            @for (clave of claves; track clave) {
              <div>
                <dt>{{ t('nombre.' + clave) }}</dt>
                <dd>{{ t('como.' + clave) }}</dd>
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

      .leyenda {
        margin-block-end: var(--espacio-3);
        color: var(--color-texto-suave);
        font-size: var(--texto-sm);
      }

      .boleta {
        min-inline-size: 44rem;
      }

      .boleta .metrica {
        position: sticky;
        inset-inline-start: 0;
        z-index: 1;
        min-inline-size: 11rem;
        max-inline-size: 14rem;
        background: var(--color-fondo);
        box-shadow: inset -1px 0 var(--color-divisoria);
        text-align: start;
      }

      .boleta td {
        color: var(--color-texto-suave);
        vertical-align: middle;
      }

      .boleta td[data-estado='cumple'] {
        color: var(--color-exito-tinta);
        font-weight: var(--peso-medio);
      }

      .boleta td[data-estado='sinDatos'] {
        color: var(--color-texto-suave);
      }

      /* Las semanas pasadas no dicen "cumple" solo con el color. */
      .boleta td[data-estado='cumple']:not(.actual) .valor::after {
        content: ' ✓' / '';
      }

      /* La semana en curso es la columna que se lee: va sobre papel claro, con la marca de cumplida o pendiente. */
      .boleta .actual {
        background: var(--color-superficie);
        box-shadow:
          inset 1px 0 var(--color-divisoria),
          inset -1px 0 var(--color-divisoria);
      }

      .boleta td.actual {
        color: var(--color-texto);
        font-size: var(--texto-base);
        font-weight: var(--peso-fuerte);
      }

      .boleta td.actual .valor {
        display: inline-block;
        min-inline-size: 4.5rem;
        padding: var(--espacio-1) var(--espacio-2);
        border-radius: var(--radio-sm);
      }

      .boleta td.actual[data-estado='cumple'] .valor {
        background: var(--color-exito-tinte);
        color: var(--color-exito-tinta);
      }

      .boleta td.actual[data-estado='falta'] .valor {
        background: var(--color-aviso-tinte);
        color: var(--color-aviso-tinta);
      }

      .esta {
        display: block;
        color: var(--color-accion-tinta);
        font-family: var(--fuente);
        font-weight: var(--peso-fuerte);
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
        .como dl {
          grid-template-columns: repeat(2, minmax(0, 1fr));
          max-width: none;
          gap: var(--espacio-4) var(--espacio-6);
        }
      }

      @media (forced-colors: active) {
        .boleta td.actual[data-estado='cumple'] .valor {
          outline: 2px solid CanvasText;
        }
      }
    `,
  ],
})
export class Metricas implements OnInit {
  private readonly servicio = inject(AdminService);
  private readonly i18n = inject(TranslocoService);
  private readonly desliza = viewChild<ElementRef<HTMLElement>>('desliza');

  protected readonly claves = METRICAS;
  protected readonly datos = signal<MetricasDto | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly corta = semanaCorta;

  protected readonly semanas = computed(() => this.datos()?.semanas ?? []);
  protected readonly renglones = computed<readonly Renglon[]>(() => {
    const d = this.datos();
    if (!d) {
      return [];
    }
    return METRICAS.map((clave) => {
      const meta = metaDe(d.metas, clave);
      return {
        clave,
        meta: this.i18n.translate(`admin.metricas.meta.${clave}`, {
          meta: cifraMetrica(meta, clave),
        }),
        celdas: d.semanas.map((s) => {
          const valor = valorMetrica(s, clave);
          return { texto: cifraMetrica(valor, clave), estado: estadoMeta(valor, meta, clave) };
        }),
      };
    });
  });
  /** Cuántas metas cumple la semana en curso, de las que tienen datos. */
  protected readonly cumplidas = computed(() => {
    const filas = this.renglones();
    if (filas.length === 0) {
      return null;
    }
    const actuales = filas.map((r) => r.celdas.at(-1)?.estado ?? 'sinDatos');
    return { n: actuales.filter((e) => e === 'cumple').length, de: filas.length };
  });

  constructor() {
    // La boleta se pinta después de cargar y las fuentes la ensanchan: se va al final cuando ya existe y midió.
    afterRenderEffect(() => {
      if (this.desliza()) {
        void document.fonts.ready.then(() => this.alFinal());
      }
    });
  }

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      this.datos.set(await this.servicio.metricas());
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  /** En pantalla angosta la boleta abre en la semana en curso, no en la más vieja. */
  private alFinal(): void {
    const el = this.desliza()?.nativeElement;
    if (el) {
      el.scrollLeft = el.scrollWidth;
    }
  }
}
