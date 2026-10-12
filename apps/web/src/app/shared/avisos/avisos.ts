import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { AvisosService } from './avisos.service';

/**
 * Pila de avisos al pie, al alcance del pulgar. Entran y salen por abajo (catálogo de la regla 07);
 * la región es `status` para que el lector de pantalla los anuncie sin interrumpir.
 */
@Component({
  selector: 'app-avisos',
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div role="status" aria-live="polite" *transloco="let t">
      <ol>
        @for (aviso of servicio.avisos(); track aviso.id) {
          <li [attr.data-tipo]="aviso.tipo" animate.enter="entra" animate.leave="sale">
            <span>{{ aviso.texto }}</span>
            @if (aviso.tipo === 'error') {
              <button
                type="button"
                [attr.aria-label]="t('app.cerrarAviso')"
                (click)="servicio.cerrar(aviso.id)"
              >
                <svg viewBox="0 0 16 16" aria-hidden="true">
                  <path
                    d="m4 4 8 8M12 4l-8 8"
                    stroke="currentColor"
                    stroke-width="1.75"
                    stroke-linecap="round"
                  />
                </svg>
              </button>
            }
          </li>
        }
      </ol>
    </div>
  `,
  styles: `
    ol {
      position: fixed;
      inset-inline: 0;
      /* Sobre la barra de acción fija del celular, si la pantalla tiene una (--alto-pie-accion). */
      bottom: calc(var(--espacio-4) + var(--alto-pie-accion, 0px) + env(safe-area-inset-bottom));
      z-index: 20;
      display: grid;
      justify-items: center;
      gap: var(--espacio-2);
      margin: 0;
      padding: 0 var(--espacio-4);
      list-style: none;
      pointer-events: none;
    }

    li {
      max-width: 26rem;
      padding: var(--espacio-3) var(--espacio-4);
      border-radius: var(--radio-lg);
      background: var(--color-texto);
      color: var(--color-fondo);
      box-shadow: var(--sombra-flotante);
      font-weight: var(--peso-medio);
      transition:
        opacity var(--dur-medium) var(--ease-out),
        transform var(--dur-medium) var(--ease-out);
    }

    li {
      display: flex;
      align-items: center;
      gap: var(--espacio-2);
    }

    button {
      display: inline-grid;
      place-items: center;
      flex: none;
      width: var(--tactil-minimo);
      height: var(--tactil-minimo);
      margin: calc(-1 * var(--espacio-2)) calc(-1 * var(--espacio-3)) calc(-1 * var(--espacio-2)) 0;
      border: 0;
      border-radius: var(--radio-lg);
      background: none;
      color: inherit;
      cursor: pointer;
      pointer-events: auto;
    }

    button svg {
      width: 1rem;
      height: 1rem;
    }

    li[data-tipo='error'] {
      background: var(--color-peligro-tinta);
    }

    .entra {
      @starting-style {
        opacity: 0;
        transform: translateY(var(--desplazamiento-aviso));
      }
    }

    .sale {
      opacity: 0;
      transform: translateY(var(--desplazamiento-aviso));
      transition-duration: var(--dur-small);
    }
  `,
})
export class Avisos {
  protected readonly servicio = inject(AvisosService);
}
