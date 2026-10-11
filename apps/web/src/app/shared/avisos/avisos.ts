import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AvisosService } from './avisos.service';

/**
 * Pila de avisos al pie, al alcance del pulgar. Entran y salen por abajo (catálogo de la regla 07);
 * la región es `status` para que el lector de pantalla los anuncie sin interrumpir.
 */
@Component({
  selector: 'app-avisos',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ol role="status" aria-live="polite">
      @for (aviso of servicio.avisos(); track aviso.id) {
        <li [attr.data-tipo]="aviso.tipo" animate.enter="entra" animate.leave="sale">
          {{ aviso.texto }}
        </li>
      }
    </ol>
  `,
  styles: `
    ol {
      position: fixed;
      inset-inline: 0;
      bottom: calc(var(--espacio-4) + env(safe-area-inset-bottom));
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

    li[data-tipo='error'] {
      background: var(--color-peligro-tinta);
    }

    .entra {
      @starting-style {
        opacity: 0;
        transform: translateY(var(--espacio-4));
      }
    }

    .sale {
      opacity: 0;
      transform: translateY(var(--espacio-4));
      transition-duration: var(--dur-small);
    }
  `,
})
export class Avisos {
  protected readonly servicio = inject(AvisosService);
}
