import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t">
      <a class="saltar" href="#contenido">{{ t('app.saltar') }}</a>
      <header class="barra">
        <span class="marca">{{ t('app.nombre') }}</span>
      </header>
      <main id="contenido" tabindex="-1">
        <router-outlet />
      </main>
    </ng-container>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      min-height: 100dvh;
    }

    .barra {
      /* La franja inferior es la "línea" activa; sin grupo, la línea de la marca. */
      --linea-activa: var(--linea-1);
      display: flex;
      align-items: center;
      min-height: calc(3.5rem + env(safe-area-inset-top));
      padding: env(safe-area-inset-top) max(var(--espacio-4), env(safe-area-inset-right)) 0
        max(var(--espacio-4), env(safe-area-inset-left));
      background: var(--color-superficie);
      border-bottom: var(--grosor-linea) solid var(--linea-activa);
    }

    .marca {
      font-size: var(--texto-lg);
      font-weight: var(--peso-fuerte);
      letter-spacing: -0.01em;
    }

    main {
      flex: 1;
      outline: none;
    }

    .saltar {
      position: absolute;
      left: var(--espacio-2);
      top: var(--espacio-2);
      z-index: 10;
      padding: var(--espacio-2) var(--espacio-3);
      border-radius: var(--radio-md);
      background: var(--color-accion);
      color: var(--color-sobre-accion);
      transform: translateY(-200%);
    }

    .saltar:focus-visible {
      transform: none;
    }
  `,
})
export class App {}
