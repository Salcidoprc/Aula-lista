import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ClaveGrado } from '../shared/portada/portada';

/**
 * Un paso del periodo en la landing (RF-010), como un libro abierto: la página izquierda es la portada del
 * capítulo en el color de su grado, con el número muy grande y recortado; la derecha, en el tinte, lleva la
 * captura real o la demostración. En celular las dos páginas se apilan.
 */
@Component({
  selector: 'app-capitulo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-grado]': 'grado()', '[class.invertido]': 'invertido()' },
  template: `
    <div class="portadilla">
      <span class="numero" aria-hidden="true">{{ numero() }}</span>
      <h3>{{ titulo() }}</h3>
      <p>{{ texto() }}</p>
      <ul>
        @for (punto of puntos(); track punto) {
          <li>{{ punto }}</li>
        }
      </ul>
    </div>
    <div class="pagina">
      <ng-content />
    </div>
  `,
  styles: `
    :host {
      display: grid;
      overflow: hidden;
      border-radius: var(--radio-tapa);
      box-shadow: var(--sombra-flotante);
    }

    .portadilla {
      position: relative;
      isolation: isolate;
      overflow: hidden;
      display: grid;
      align-content: end;
      gap: var(--espacio-4);
      min-block-size: 20rem;
      padding: var(--espacio-7) var(--espacio-5) var(--espacio-6);
      background: var(--grado);
      box-shadow: var(--sombra-lomo);
      color: var(--color-sobre-grado);
    }

    .numero {
      position: absolute;
      z-index: -1;
      inset-block-start: -0.18em;
      inset-inline-end: -0.04em;
      font-size: clamp(10rem, 22vw, 17rem);
      font-weight: var(--peso-fuerte);
      line-height: 1;
      color: color-mix(in oklab, var(--grado) 80%, var(--color-sobre-grado));
      user-select: none;
    }

    h3 {
      margin: 0;
      max-inline-size: 16ch;
      font-size: clamp(var(--texto-xl), 3.4vw, 2.5rem);
      line-height: 1.1;
      letter-spacing: -0.02em;
      text-wrap: balance;
    }

    p {
      margin: 0;
      max-inline-size: 34rem;
      font-size: var(--texto-lg);
      line-height: var(--interlineado);
    }

    ul {
      display: grid;
      gap: var(--espacio-2);
      margin: 0;
      padding: 0;
      list-style: none;
      color: var(--grado-rotulo);
    }

    li {
      display: flex;
      gap: var(--espacio-2);
      align-items: baseline;
    }

    li::before {
      content: '';
      flex: none;
      inline-size: 0.5rem;
      block-size: 0.5rem;
      border-radius: var(--radio-sm);
      background: currentColor;
      transform: translateY(-0.1em);
    }

    /* La página derecha: el tinte del grado y la sombra del pliegue junto al lomo. */
    .pagina {
      display: grid;
      place-items: center;
      padding: var(--espacio-6) var(--espacio-4);
      background: var(--grado-tinte);
      box-shadow: inset 0 1rem 1.25rem -1rem var(--color-pliegue);
    }

    @media (min-width: 60rem) {
      :host {
        grid-template-columns: 1fr 1fr;
      }

      .portadilla {
        min-block-size: 34rem;
        padding: var(--espacio-7);
      }

      .pagina {
        padding: var(--espacio-7) var(--espacio-6);
        box-shadow: inset 1.25rem 0 1.5rem -1.25rem var(--color-pliegue);
      }

      :host(.invertido) .portadilla {
        order: 2;
      }

      :host(.invertido) .pagina {
        box-shadow: inset -1.25rem 0 1.5rem -1.25rem var(--color-pliegue);
      }
    }
  `,
})
export class Capitulo {
  readonly numero = input.required<number>();
  readonly grado = input.required<ClaveGrado>();
  readonly titulo = input.required<string>();
  readonly texto = input.required<string>();
  readonly puntos = input<readonly string[]>([]);
  /** En pantalla ancha, la captura va a la izquierda para alternar. */
  readonly invertido = input(false);
}
