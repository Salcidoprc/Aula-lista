import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  effect,
  inject,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { TranslocoDirective } from '@jsverse/transloco';
import { SesionService } from './core/sesion/sesion.service';
import { IndicadorSincronizacion } from './core/sincronizacion/indicador-sincronizacion';
import { Avisos } from './shared/avisos/avisos';
import { Portada } from './shared/portada/portada';
import { PortadaService } from './shared/portada/portada.service';

@Component({
  selector: 'app-root',
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    TranslocoDirective,
    Avisos,
    Portada,
    IndicadorSincronizacion,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t">
      <a class="saltar" href="#contenido">{{ t('app.saltar') }}</a>
      <header>
        <app-portada
          [titulo]="portada()?.titulo ?? t('app.nombre')"
          [subtitulo]="portada()?.subtitulo"
          [numero]="portada()?.numero"
          [grado]="portada()?.grado"
        >
          @if (sesion.activa()) {
            <nav
              class="navegacion"
              [class.bajo-titulo]="!!portada()?.numero"
              [attr.aria-label]="t('app.navegacion')"
            >
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
              <app-indicador-sincronizacion />
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
      flex-wrap: wrap;
      gap: var(--espacio-1);
      max-inline-size: calc(100% - 2 * var(--espacio-2));
    }

    /* La esquina es del número del grado: con número, la navegación va bajo el título para no pisarlo. */
    .navegacion.bajo-titulo {
      position: static;
      margin-inline-start: calc(-1 * var(--espacio-2));
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
  protected readonly portada = inject(PortadaService).datos;

  constructor() {
    const injector = inject(Injector);
    const host = inject<ElementRef<HTMLElement>>(ElementRef);
    // Al cambiar de pantalla, el foco va al contenido nuevo para que teclado y lector de pantalla no
    // se queden en un elemento que ya no existe (WCAG 2.4.3). La primera carga no se toca.
    // Si solo cambia la consulta (otro día, otra unidad), la pantalla es la misma y el foco se queda donde está.
    let ruta = '';
    const sinConsulta = (url: string) => url.split(/[?#]/)[0];
    inject(Router)
      .events.pipe(
        filter((e) => e instanceof NavigationEnd),
        filter((e) => {
          const anterior = ruta;
          ruta = sinConsulta(e.urlAfterRedirects);
          return anterior !== '' && anterior !== ruta;
        }),
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

    // La barra de estado del celular (theme-color) toma el color de la portada: azul fuera de un grupo,
    // el del grado dentro. El color se lee ya pintado y se pasa a rgb, que todo navegador entiende.
    effect(() => {
      this.portada();
      afterNextRender(
        () => {
          const portada = host.nativeElement.querySelector('app-portada');
          const meta = document.querySelector('meta[name="theme-color"]');
          const color = portada && aRgb(getComputedStyle(portada).backgroundColor);
          if (meta && color) {
            meta.setAttribute('content', color);
          }
        },
        { injector },
      );
    });
  }
}

/** Convierte cualquier color CSS (oklch incluido) a `rgb(...)` pintándolo en un lienzo de 1 px. */
function aRgb(color: string): string | null {
  const contexto = document.createElement('canvas').getContext('2d');
  if (!contexto) {
    return null;
  }
  contexto.fillStyle = color;
  contexto.fillRect(0, 0, 1, 1);
  const [r, g, b] = contexto.getImageData(0, 0, 1, 1).data;
  return `rgb(${r}, ${g}, ${b})`;
}
