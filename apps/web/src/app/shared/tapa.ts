import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { claveGrado } from './grados';

/**
 * Tapa de libro: el grupo con el color de su grado y su lomo (DESIGN.md, "Tapa de libro"). Decorativa:
 * el texto de al lado ya nombra el grupo.
 */
@Component({
  selector: 'app-tapa',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    'aria-hidden': 'true',
    '[attr.data-grado]': 'clave()',
    '[class.chica]': "tamano() === 'chica'",
  },
  template: `
    @if (tamano() === 'normal') {
      <span class="cifras">{{ numero() }}°</span>
    }
  `,
  styles: `
    :host {
      display: inline-grid;
      flex: none;
      place-items: center;
      width: 3.5rem;
      height: 4rem;
      border-radius: var(--radio-tapa);
      background: var(--grado);
      box-shadow: var(--sombra-lomo);
      color: var(--color-sobre-grado);
      font-size: 1.375rem;
      font-weight: var(--peso-fuerte);
      line-height: 1;
    }

    :host(.chica) {
      width: 1.75rem;
      height: 2rem;
      font-size: var(--texto-xs);
      box-shadow: var(--sombra-lomo-chica);
    }

    span {
      padding-inline-start: 0.2em;
    }
  `,
})
export class Tapa {
  readonly nivel = input.required<string>();
  readonly grado = input.required<number | string>();
  readonly tamano = input<'normal' | 'chica'>('normal');

  protected readonly clave = computed(() => claveGrado(this.nivel(), this.grado()));
  protected readonly numero = computed(() => Number(this.grado()));
}
