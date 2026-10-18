import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { AvisoPagoDto } from '../../api/models/aviso-pago-dto';
import { fechaLarga } from '../../shared/fechas';
import { forma } from '../../shared/plural';

/**
 * La banda de pago (RN-085, RN-089): el prepago por vencer en tinte de aviso; el cobro fallido o el prepago vencido,
 * con los días de gracia que quedan, en tinte de peligro. En Inicio lleva al plan; en el plan, con tarjeta, abre el
 * portal para actualizarla.
 */
@Component({
  selector: 'app-banda-pago',
  imports: [TranslocoDirective, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="aviso"
      [class.aviso--error]="urgente()"
      [class.aviso--pendiente]="!urgente()"
      [attr.role]="urgente() ? 'alert' : null"
      *transloco="let t; prefix: 'plan.banda'"
    >
      <p>
        <strong>{{ t(aviso().tipo + '.titulo') }}</strong>
        {{
          t(aviso().tipo + '.texto.' + forma(aviso().dias), { dias: aviso().dias, fecha: fecha() })
        }}
      </p>
      @if (!enPlan()) {
        <a class="enlace enlace--solo" routerLink="/plan">{{ t(aviso().tipo + '.accion') }}</a>
      } @else if (aviso().tipo === 'cobro_fallido') {
        <button class="boton boton--secundario" type="button" (click)="actualizarTarjeta.emit()">
          {{ t('cobro_fallido.tarjeta') }}
        </button>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .aviso {
      justify-items: start;
    }

    .enlace {
      color: inherit;
      font-weight: var(--peso-medio);
    }
  `,
})
export class BandaPago {
  protected readonly forma = forma;
  readonly aviso = input.required<AvisoPagoDto>();
  readonly enPlan = input(false);
  readonly actualizarTarjeta = output();

  protected readonly urgente = computed(() => this.aviso().tipo !== 'prepago_por_vencer');
  protected readonly fecha = computed(() => fechaLarga(this.aviso().fecha));
}
