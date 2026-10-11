import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { Portada } from './compartido/portada/portada';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, TranslocoDirective, Portada],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t">
      <a class="saltar" href="#contenido">{{ t('app.saltar') }}</a>
      <header>
        <app-portada [titulo]="t('app.nombre')" />
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
      border-radius: var(--radio-lg);
      background: var(--color-superficie);
      color: var(--color-accion-tinta);
      font-weight: var(--peso-medio);
      transform: translateY(-200%);
    }

    /* Aparece sobre la portada: superficie clara con sombra para que se lea en cualquier grado. */
    .saltar:focus-visible {
      transform: none;
      box-shadow: var(--sombra-flotante), var(--foco);
    }
  `,
})
export class App {}
