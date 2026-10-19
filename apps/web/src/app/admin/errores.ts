import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ErrorTrabajoDto } from '../api/models/error-trabajo-dto';
import { codigoDeError } from '../core/errores/problema';
import { forma } from '../shared/plural';
import { AdminService } from './admin.service';
import { ESTILOS_PANEL } from './estilos';
import { entero, instante } from './panel';

/**
 * RF-091: los errores más recientes de los trabajos en segundo plano. Solo el tipo de error y a qué docente tocó,
 * nunca el mensaje, que podría llevar el nombre o el teléfono de alguien.
 */
@Component({
  selector: 'app-errores',
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t; prefix: 'admin.errores'">
      <div class="cabeza">
        <h1 tabindex="-1">{{ t('titulo') }}</h1>
        <p class="resumen">{{ t('resumen') }}</p>
      </div>

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (!errores()) {
        <div class="cargando" role="status" [attr.aria-label]="t('cargando')">
          @for (i of [1, 2, 3]; track i) {
            <span class="esqueleto" aria-hidden="true"></span>
          }
        </div>
      } @else if (errores(); as lista) {
        @if (lista.length === 0) {
          <p class="vacio">{{ t('vacio') }}</p>
        } @else {
          <p class="dato-suave">
            {{ t('cuenta.' + forma(lista.length), { n: entero(lista.length) }) }}
          </p>
          <table class="libreta apilable" role="table">
            <thead role="rowgroup">
              <tr role="row">
                <th scope="col" role="columnheader">{{ t('cuando') }}</th>
                <th scope="col" role="columnheader">{{ t('origen') }}</th>
                <th scope="col" role="columnheader">{{ t('tipo') }}</th>
                <th scope="col" role="columnheader">{{ t('docente') }}</th>
              </tr>
            </thead>
            <tbody role="rowgroup">
              @for (e of lista; track e.id) {
                <tr role="row">
                  <th scope="row" role="rowheader" class="num-izq">{{ instante(e.fecha) }}</th>
                  <td role="cell" [attr.data-etiqueta]="t('origen')">
                    <span>
                      <span class="sello" [attr.data-tono]="tono(e.origen)">{{
                        t('origenes.' + e.origen)
                      }}</span>
                      <span class="secundario">{{ e.trabajo }}</span>
                    </span>
                  </td>
                  <td role="cell" [attr.data-etiqueta]="t('tipo')">
                    <code>{{ e.tipoError }}</code>
                  </td>
                  <td role="cell" [attr.data-etiqueta]="t('docente')">
                    {{ e.correo ?? t('sinDocente') }}
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      }
    </ng-container>
  `,
  styles: [
    ESTILOS_PANEL,
    `
      .dato-suave {
        margin: 0;
      }

      .num-izq {
        font-family: var(--fuente-cifras);
        font-variant-numeric: tabular-nums;
        white-space: nowrap;
      }

      td > span {
        display: inline-grid;
        justify-items: start;
        gap: var(--espacio-1);
      }

      code {
        font-size: var(--texto-xs);
        overflow-wrap: anywhere;
      }
    `,
  ],
})
export class Errores implements OnInit {
  private readonly servicio = inject(AdminService);
  private readonly i18n = inject(TranslocoService);

  protected readonly errores = signal<ErrorTrabajoDto[] | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly instante = instante;
  protected readonly entero = entero;
  protected readonly forma = forma;

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      this.errores.set(await this.servicio.errores());
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected tono(origen: string): string {
    return origen === 'pagos' || origen === 'retencion' ? 'peligro' : 'aviso';
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }
}
