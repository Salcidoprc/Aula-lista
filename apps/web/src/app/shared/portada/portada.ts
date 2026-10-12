import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Clave del grado: p1 … p6 (primaria) o s1 … s3 (secundaria). Define el color de la portada. */
export type ClaveGrado = 'p1' | 'p2' | 'p3' | 'p4' | 'p5' | 'p6' | 's1' | 's2' | 's3';

/**
 * Portada: la cabecera de Aula Lista, como la portada de un libro de texto. Se pinta con el color
 * del grado activo y muestra el número del grado muy grande y recortado. Sin grado, usa el color
 * de la marca y no lleva número. Ver DESIGN.md, componente "Portada".
 */
@Component({
  selector: 'app-portada',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[attr.data-grado]': 'grado() ?? null',
    '[class.con-numero]': '!!numero()',
  },
  template: `
    @if (numero(); as n) {
      <span class="numero" aria-hidden="true">{{ n }}</span>
    }
    <span class="titulo">{{ titulo() }}</span>
    @if (subtitulo(); as s) {
      <span class="subtitulo">{{ s }}</span>
    }
    <ng-content />
  `,
  styles: `
    :host {
      position: relative;
      isolation: isolate;
      overflow: hidden;
      display: grid;
      align-content: end;
      gap: var(--espacio-1);
      min-height: calc(4.5rem + env(safe-area-inset-top));
      padding: calc(var(--espacio-3) + env(safe-area-inset-top))
        max(var(--espacio-4), env(safe-area-inset-right)) var(--espacio-4)
        max(var(--espacio-4), env(safe-area-inset-left));
      background: var(--grado);
      color: var(--color-sobre-grado);
    }

    /* El texto nunca pisa el número: se reserva su lugar a la derecha. */
    :host(.con-numero) {
      padding-inline-end: max(5.5rem, env(safe-area-inset-right));
    }

    .numero {
      position: absolute;
      z-index: -1;
      inset-inline-end: -0.06em;
      inset-block-start: calc(env(safe-area-inset-top) - 0.2em);
      font-size: var(--texto-grado);
      font-weight: var(--peso-fuerte);
      line-height: 1;
      letter-spacing: 0;
      color: color-mix(in oklch, var(--grado) 80%, var(--color-sobre-grado));
      pointer-events: none;
      user-select: none;
    }

    .titulo {
      font-size: var(--texto-lg);
      font-weight: var(--peso-fuerte);
      line-height: var(--interlineado-ajustado);
      letter-spacing: var(--tracking-titulo);
      overflow-wrap: anywhere;
    }

    .subtitulo {
      font-size: var(--texto-sm);
      color: var(--grado-tinte);
      overflow-wrap: anywhere;
    }

    /* Escritorio: el texto y el número se alinean con el contenedor de la app, no con la orilla. */
    @media (min-width: 48rem) {
      :host {
        --orilla: max(var(--margen-app), (100% - var(--ancho-app)) / 2);
        min-height: 6rem;
        padding-inline: var(--orilla);
      }

      :host(.con-numero) {
        padding-inline-end: calc(var(--orilla) + 6rem);
      }

      .numero {
        inset-inline-end: calc(var(--orilla) - 0.06em);
      }

      .titulo {
        font-size: var(--texto-xl);
      }
    }
  `,
})
export class Portada {
  readonly titulo = input.required<string>();
  readonly subtitulo = input<string>();
  /** Número del grado tal como se muestra ("4"). Decorativo: el título ya nombra el grupo. */
  readonly numero = input<string>();
  readonly grado = input<ClaveGrado>();
}
