import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

interface Barra {
  readonly x: number;
  readonly y: number;
  readonly alto: number;
  readonly vacia: boolean;
  readonly actual: boolean;
}

const ANCHO_BARRA = 3;
const SEPARACION = 3;
const ALTO = 24;

/**
 * RF-094: el "renglón de semanas" de un indicador del resumen. Una barrita por semana, de la más vieja a la actual, que
 * va en el color de acción como la columna "Esta semana" de la boleta. Sin datos es una raya de base. No se anima.
 */
@Component({
  selector: 'app-tendencia',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg
      [attr.viewBox]="'0 0 ' + ancho() + ' ' + alto"
      [attr.width]="ancho()"
      [attr.height]="alto"
      role="img"
      [attr.aria-label]="lectura()"
    >
      @for (b of barras(); track $index) {
        <rect
          [attr.x]="b.x"
          [attr.y]="b.y"
          [attr.width]="anchoBarra"
          [attr.height]="b.alto"
          rx="1"
          [class.vacia]="b.vacia"
          [class.actual]="b.actual"
        />
      }
    </svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      line-height: 0;
    }

    rect {
      fill: var(--color-texto-tenue);
    }

    rect.vacia {
      fill: var(--color-divisoria);
    }

    rect.actual:not(.vacia) {
      fill: var(--color-accion);
    }

    @media (forced-colors: active) {
      rect {
        fill: CanvasText;
      }

      rect.actual:not(.vacia) {
        fill: Highlight;
      }
    }
  `,
})
export class Tendencia {
  /** Un valor por semana, de la más vieja a la actual. */
  readonly serie = input.required<readonly number[]>();
  /** Lo que leen los lectores de pantalla: el indicador y sus valores. */
  readonly lectura = input.required<string>();

  protected readonly alto = ALTO;
  protected readonly anchoBarra = ANCHO_BARRA;
  protected readonly ancho = computed(
    () => Math.max(1, this.serie().length) * (ANCHO_BARRA + SEPARACION) - SEPARACION,
  );
  protected readonly barras = computed<readonly Barra[]>(() => {
    const serie = this.serie();
    const maximo = Math.max(0, ...serie);
    return serie.map((v, i) => {
      const alto = maximo === 0 || v <= 0 ? 1 : Math.max(2, Math.round((v / maximo) * ALTO));
      return {
        x: i * (ANCHO_BARRA + SEPARACION),
        y: ALTO - alto,
        alto,
        vacia: v <= 0,
        actual: i === serie.length - 1,
      };
    });
  });
}
