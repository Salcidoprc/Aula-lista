import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { Icono } from '../shared/iconos';

/**
 * Preguntas frecuentes de la landing (RF-010). Cada respuesta se despliega con `grid-template-rows` de 0fr a
 * 1fr (regla 07: nunca se anima la altura) y la flecha gira; con movimiento reducido solo aparece.
 */
@Component({
  selector: 'app-preguntas-landing',
  imports: [TranslocoDirective, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ul *transloco="let t; prefix: 'landing.preguntas'">
      @for (i of preguntas; track i) {
        <li [class.abierta]="abierta() === i">
          <h3>
            <button
              type="button"
              [id]="'pregunta-' + i"
              [attr.aria-expanded]="abierta() === i"
              [attr.aria-controls]="'respuesta-' + i"
              (click)="abierta.set(abierta() === i ? null : i)"
            >
              <span>{{ t('lista.' + i + '.0') }}</span>
              <app-icono nombre="siguiente" />
            </button>
          </h3>
          <div
            class="respuesta"
            role="region"
            [id]="'respuesta-' + i"
            [attr.aria-labelledby]="'pregunta-' + i"
            [attr.inert]="abierta() === i ? null : ''"
          >
            <div>
              <p>{{ t('lista.' + i + '.1') }}</p>
            </div>
          </div>
        </li>
      }
    </ul>
  `,
  styles: `
    ul {
      margin: 0;
      padding: 0;
      list-style: none;
      border-block-start: 1px solid var(--color-divisoria);
    }

    li {
      border-block-end: 1px solid var(--color-divisoria);
    }

    h3 {
      margin: 0;
      font-size: var(--texto-lg);
    }

    button {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--espacio-4);
      inline-size: 100%;
      min-block-size: 3.75rem;
      padding: var(--espacio-3) 0;
      border: 0;
      background: none;
      color: var(--color-texto);
      font: inherit;
      text-align: start;
      cursor: pointer;
    }

    button:focus-visible {
      outline: none;
      box-shadow: var(--foco);
      border-radius: var(--radio-sm);
    }

    app-icono {
      color: var(--color-accion);
      transform: rotate(90deg);
      transition: transform var(--dur-medium) var(--ease-out);
    }

    .abierta app-icono {
      transform: rotate(-90deg);
    }

    .respuesta {
      display: grid;
      grid-template-rows: 0fr;
      transition: grid-template-rows var(--dur-medium) var(--ease-out);
    }

    .abierta .respuesta {
      grid-template-rows: 1fr;
    }

    .respuesta > div {
      overflow: hidden;
      min-block-size: 0;
    }

    p {
      margin: 0;
      padding-block-end: var(--espacio-4);
      max-inline-size: 60ch;
      color: var(--color-texto-suave);
    }

    @media (prefers-reduced-motion: reduce) {
      app-icono,
      .respuesta {
        transition: none;
      }
    }
  `,
})
export class PreguntasLanding {
  protected readonly preguntas = [0, 1, 2, 3, 4, 5];
  protected readonly abierta = signal<number | null>(0);
}
