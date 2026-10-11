import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  inject,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, skip } from 'rxjs';
import { TranslocoDirective } from '@jsverse/transloco';
import { SesionService } from './core/sesion/sesion.service';
import { Avisos } from './shared/avisos/avisos';
import { Portada } from './shared/portada/portada';

@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, TranslocoDirective, Avisos, Portada],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t">
      <a class="saltar" href="#contenido">{{ t('app.saltar') }}</a>
      <header>
        <app-portada [titulo]="t('app.nombre')">
          @if (sesion.activa()) {
            <nav class="navegacion" [attr.aria-label]="t('app.navegacion')">
              <a
                routerLink="/"
                routerLinkActive="activo"
                [routerLinkActiveOptions]="{ exact: true }"
                ariaCurrentWhenActive="page"
                >{{ t('app.inicio') }}</a
              >
              <a routerLink="/ajustes" routerLinkActive="activo" ariaCurrentWhenActive="page">{{
                t('app.ajustes')
              }}</a>
            </nav>
          }
        </app-portada>
      </header>
      <main id="contenido" tabindex="-1">
        <router-outlet />
      </main>
      <app-avisos />
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

    /* Navegación sobre la portada: texto claro sobre la acción, arriba a la derecha, al alcance del pulgar. */
    .navegacion {
      position: absolute;
      inset-block-start: env(safe-area-inset-top);
      inset-inline-end: max(var(--espacio-2), env(safe-area-inset-right));
      display: flex;
      gap: var(--espacio-1);
    }

    /* En pantalla ancha, la esquina queda lejos de la columna: va bajo el título, alineada con el texto. */
    @media (min-width: 48rem) {
      .navegacion {
        position: static;
        margin-inline-start: calc(-1 * var(--espacio-2));
      }
    }

    .navegacion a {
      display: inline-flex;
      align-items: center;
      min-height: var(--tactil-minimo);
      padding: 0 var(--espacio-2);
      border-radius: var(--radio-lg);
      color: var(--grado-tinte);
      font-size: var(--texto-sm);
      font-weight: var(--peso-medio);
      text-decoration: none;
    }

    .navegacion a:focus-visible {
      box-shadow: 0 0 0 2px var(--color-sobre-grado);
    }

    .navegacion a:active {
      color: var(--color-sobre-grado);
    }

    .navegacion a.activo {
      color: var(--color-sobre-grado);
      text-decoration: underline;
      text-decoration-thickness: 2px;
      text-underline-offset: 0.3em;
    }

    /* Aparece sobre la portada: superficie clara con sombra para que se lea en cualquier grado. */
    .saltar:focus-visible {
      transform: none;
      box-shadow: var(--sombra-flotante), var(--foco);
    }
  `,
})
export class App {
  protected readonly sesion = inject(SesionService);

  constructor() {
    const injector = inject(Injector);
    const host = inject<ElementRef<HTMLElement>>(ElementRef);
    // Al cambiar de pantalla, el foco va al contenido nuevo para que teclado y lector de pantalla no
    // se queden en un elemento que ya no existe (WCAG 2.4.3). La primera carga no se toca.
    inject(Router)
      .events.pipe(
        filter((e) => e instanceof NavigationEnd),
        skip(1),
        takeUntilDestroyed(),
      )
      .subscribe(() =>
        afterNextRender(
          () =>
            host.nativeElement
              .querySelector<HTMLElement>('#contenido')
              ?.focus({ preventScroll: false }),
          { injector },
        ),
      );
  }
}
