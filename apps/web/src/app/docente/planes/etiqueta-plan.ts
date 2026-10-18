import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { forma } from '../../shared/plural';

/**
 * "Pro" o "Prueba Pro · quedan 9 días" junto al grupo (SPEC-08, RN-081). En Gratis no muestra nada: Gratis es lo
 * normal y no se anuncia.
 */
@Component({
  selector: 'app-etiqueta-plan',
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[hidden]': "plan() === 'gratis'" },
  template: `
    <ng-container *transloco="let t; prefix: 'plan.etiqueta'">
      @if (plan() === 'pro') {
        <span class="etiqueta">{{ t('pro') }}</span>
      } @else if (plan() === 'prueba') {
        <span class="etiqueta prueba">
          @if (detalle()) {
            @if (hastaPublicar()) {
              {{ t('pruebaHastaPublicar', { dias: dias() }) }}
            } @else {
              {{ t('pruebaDias.' + forma(dias()), { dias: dias() }) }}
            }
          } @else {
            {{ t('prueba') }}
          }
        </span>
      }
    </ng-container>
  `,
  styles: `
    :host {
      display: inline-flex;
    }

    :host([hidden]) {
      display: none;
    }

    .etiqueta {
      display: inline-flex;
      align-items: center;
      min-height: 1.5rem;
      padding: 0 var(--espacio-2);
      border-radius: var(--radio-sm);
      background: var(--color-accion-tinte);
      color: var(--color-accion-tinta);
      font-size: var(--texto-xs);
      font-weight: var(--peso-fuerte);
      line-height: var(--interlineado-ajustado);
      white-space: nowrap;
    }

    /* La prueba termina: tinte de aviso, sin alarma. */
    .prueba {
      background: var(--color-aviso-tinte);
      color: var(--color-aviso-tinta);
    }

    @media (forced-colors: active) {
      .etiqueta {
        border: 1px solid CanvasText;
      }
    }
  `,
})
export class EtiquetaPlan {
  protected readonly forma = forma;
  /** `gratis`, `pro` o `prueba` (PlanVigente de la API). */
  readonly plan = input.required<string>();
  readonly diasPrueba = input<number | string | null | undefined>(null);
  readonly hastaPublicar = input(false);
  /** En la lista basta "Prueba Pro"; en el cierre y las boletas, cuántos días quedan. */
  readonly detalle = input(true);

  protected readonly dias = computed(() => Number(this.diasPrueba() ?? 0));
}
