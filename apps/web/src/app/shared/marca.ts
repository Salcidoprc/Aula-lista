import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * El logo de Aulísima: la libreta con la palomita y el nombre. Se pinta con máscaras para que tome el
 * color del lugar donde está: `--marca-tinta` para la libreta y las letras, `--marca-acento` para el
 * acento de la "í". Sin esas variables usa `currentColor`, así que sobre la portada sale del color del
 * texto de la portada y en modo oscuro cambia solo. `libreta` muestra solo el ícono.
 */
@Component({
  selector: 'app-marca',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    role: 'img',
    'aria-label': 'Aulísima',
    '[class.libreta]': "variante() === 'libreta'",
  },
  template: `<span class="tinta"></span><span class="acento"></span>`,
  styles: `
    :host {
      position: relative;
      display: inline-block;
      flex: none;
      block-size: 1.75rem;
      aspect-ratio: 845 / 192;
      vertical-align: middle;
    }

    :host(.libreta) {
      aspect-ratio: 395 / 433;
    }

    span {
      position: absolute;
      inset: 0;
      mask: var(--imagen) center / contain no-repeat;
    }

    .tinta {
      --imagen: url('/marca/aulisima-tinta.png');
      background: var(--marca-tinta, currentColor);
    }

    .acento {
      --imagen: url('/marca/aulisima-acento.png');
      background: var(--marca-acento, var(--marca-tinta, currentColor));
    }

    :host(.libreta) .tinta {
      --imagen: url('/marca/libreta.png');
    }

    :host(.libreta) .acento {
      display: none;
    }

    @media (forced-colors: active) {
      span {
        background: CanvasText;
      }
    }
  `,
})
export class Marca {
  readonly variante = input<'completa' | 'libreta'>('completa');
}
