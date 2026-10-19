import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { CostoGrupoDto } from '../api/models/costo-grupo-dto';
import { CostosDto } from '../api/models/costos-dto';
import { codigoDeError } from '../core/errores/problema';
import { nombreGrupo } from '../shared/grados';
import { forma } from '../shared/plural';
import { Tapa } from '../shared/tapa';
import { AdminService } from './admin.service';
import { ESTILOS_PANEL } from './estilos';
import { entero, mesLargo, pesos } from './panel';

/**
 * RF-091 y RNF-022: costo estimado del mes por grupo (tokens de IA y mensajes de WhatsApp) contra lo que ese grupo
 * paga. Los grupos que rebasan el umbral van marcados; el orden es del más caro al más barato.
 */
@Component({
  selector: 'app-costos',
  imports: [TranslocoDirective, Tapa],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t; prefix: 'admin.costos'">
      <div class="cabeza">
        <h1 tabindex="-1">{{ t('titulo') }}</h1>
        @if (datos(); as d) {
          <p class="resumen">
            {{ t('resumen.mes', { mes: mes(d.mes) }) }}
            <strong>{{ pesos(d.costoTotal) }} {{ d.moneda }}</strong>
            {{ t('resumen.contra') }}
            <strong>{{ pesos(d.ingresoTotal) }} {{ d.moneda }}</strong>
            {{ t('resumen.ingreso') }}
            @if (sobreUmbral() > 0) {
              {{
                t('resumen.sobre.' + forma(sobreUmbral()), {
                  n: sobreUmbral(),
                  umbral: pesos(d.umbralGrupo) + ' ' + d.moneda,
                })
              }}
            } @else {
              {{ t('resumen.ninguno', { umbral: pesos(d.umbralGrupo) + ' ' + d.moneda }) }}
            }
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
          @for (i of [1, 2, 3, 4]; track i) {
            <span class="esqueleto" aria-hidden="true"></span>
          }
        </div>
      } @else if (datos(); as d) {
        @if (d.grupos.length === 0) {
          <p class="vacio">{{ t('vacio') }}</p>
        } @else {
          <table class="libreta apilable" role="table">
            <caption>
              {{
                t('leyenda', { moneda: d.moneda, umbralIa: pesos(d.umbralIa) })
              }}
            </caption>
            <thead role="rowgroup">
              <tr role="row">
                <th scope="col" role="columnheader">{{ t('grupo') }}</th>
                <th scope="col" role="columnheader">{{ t('plan') }}</th>
                <th scope="col" role="columnheader" class="num">{{ t('ia') }}</th>
                <th scope="col" role="columnheader" class="num">{{ t('whatsapp') }}</th>
                <th scope="col" role="columnheader" class="num">{{ t('costo') }}</th>
                <th scope="col" role="columnheader" class="num">{{ t('ingreso') }}</th>
              </tr>
            </thead>
            <tbody role="rowgroup">
              @for (g of d.grupos; track g.grupoId) {
                <tr role="row" [class.sobre]="g.sobreUmbral">
                  <th scope="row" role="rowheader">
                    <span class="grupo">
                      <app-tapa [nivel]="g.nivel" [grado]="g.grado" tamano="chica" />
                      <span>
                        {{ nombre(g) }}
                        <span class="secundario">{{ g.correo }}</span>
                      </span>
                    </span>
                  </th>
                  <td role="cell" [attr.data-etiqueta]="t('plan')">
                    <span class="sello" [attr.data-tono]="tonoPlan(g.plan)">{{
                      t('planes.' + g.plan)
                    }}</span>
                  </td>
                  <td role="cell" class="num" [attr.data-etiqueta]="t('ia')">
                    <span>
                      {{ pesos(g.costoIa) }}
                      <span class="secundario">{{
                        t('llamadas.' + forma(g.llamadasIa), { n: entero(g.llamadasIa) })
                      }}</span>
                      @if (g.iaSobreUmbral) {
                        <span class="sello" data-tono="aviso">{{ t('iaAlta') }}</span>
                      }
                    </span>
                  </td>
                  <td role="cell" class="num" [attr.data-etiqueta]="t('whatsapp')">
                    <span>
                      {{ pesos(g.costoWhatsapp) }}
                      <span class="secundario">{{
                        t('mensajes.' + forma(g.mensajesWhatsapp), {
                          n: entero(g.mensajesWhatsapp),
                        })
                      }}</span>
                    </span>
                  </td>
                  <td role="cell" class="num" [attr.data-etiqueta]="t('costo')">
                    <span>
                      <span class="cifra-costo">{{ pesos(g.costo) }}</span>
                      @if (g.sobreUmbral) {
                        <span class="sello" data-tono="peligro">{{ t('sobreUmbral') }}</span>
                      }
                    </span>
                  </td>
                  <td role="cell" class="num" [attr.data-etiqueta]="t('ingreso')">
                    {{ pesos(g.ingreso) }}
                  </td>
                </tr>
              }
            </tbody>
            <tfoot role="rowgroup">
              <tr role="row">
                <th scope="row" role="rowheader" colspan="4">{{ t('total') }}</th>
                <td role="cell" class="num" [attr.data-etiqueta]="t('costo')">
                  {{ pesos(d.costoTotal) }}
                </td>
                <td role="cell" class="num" [attr.data-etiqueta]="t('ingreso')">
                  {{ pesos(d.ingresoTotal) }}
                </td>
              </tr>
            </tfoot>
          </table>
        }
      }
    </ng-container>
  `,
  styles: [
    ESTILOS_PANEL,
    `
      .grupo {
        display: flex;
        align-items: flex-start;
        gap: var(--espacio-3);
        min-width: 0;
      }

      .grupo app-tapa {
        margin-block-start: 0.125rem;
      }

      td.num > span {
        display: inline-grid;
        justify-items: end;
        gap: var(--espacio-1);
      }

      .cifra-costo {
        font-weight: var(--peso-fuerte);
      }

      tr.sobre .cifra-costo {
        color: var(--color-peligro-tinta);
      }

      @media (max-width: 47.99rem) {
        td.num > span {
          justify-items: start;
          text-align: start;
        }

        tfoot th {
          display: block;
        }
      }
    `,
  ],
})
export class Costos implements OnInit {
  private readonly servicio = inject(AdminService);
  private readonly i18n = inject(TranslocoService);

  protected readonly datos = signal<CostosDto | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly pesos = pesos;
  protected readonly entero = entero;
  protected readonly forma = forma;
  protected readonly mes = mesLargo;
  protected readonly sobreUmbral = computed(
    () => this.datos()?.grupos.filter((g) => g.sobreUmbral).length ?? 0,
  );

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      this.datos.set(await this.servicio.costos());
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected nombre(g: CostoGrupoDto): string {
    const base = nombreGrupo(g.grado, g.nombre);
    return g.materia ? `${base} · ${g.materia}` : base;
  }

  protected tonoPlan(plan: string): string {
    return plan === 'pro' ? 'accion' : plan === 'prueba' ? 'aviso' : '';
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }
}
