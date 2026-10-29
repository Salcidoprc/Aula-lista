import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { controlarPortada } from '../shared/portada/portada.service';

/**
 * SPEC-09: el panel de administración. En escritorio, las secciones en una columna a la izquierda; en el celular,
 * en una fila que se desliza arriba. Ninguna sección muestra alumnos, tutores, notas ni observaciones (RN-090).
 */
@Component({
  selector: 'app-admin',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="panel" *transloco="let t; prefix: 'admin'">
      <nav class="secciones" [attr.aria-label]="t('secciones')">
        <ul>
          @for (s of secciones; track s.ruta) {
            <li>
              <a
                #enlace
                [routerLink]="s.ruta"
                routerLinkActive="activa"
                ariaCurrentWhenActive="page"
                (isActiveChange)="$event && alaVista(enlace)"
                >{{ t(s.clave + '.titulo') }}</a
              >
            </li>
          }
        </ul>
        <p class="privacidad">{{ t('privacidad') }}</p>
      </nav>
      <div class="contenido">
        <router-outlet />
      </div>
    </div>
  `,
  styles: `
    .panel {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      gap: var(--espacio-5);
      width: 100%;
      max-width: calc(var(--ancho-app) + 2 * var(--margen-app));
      margin-inline: auto;
      padding: var(--espacio-5) max(var(--espacio-4), env(safe-area-inset-right))
        calc(var(--espacio-6) + env(safe-area-inset-bottom))
        max(var(--espacio-4), env(safe-area-inset-left));
    }

    .secciones ul {
      display: flex;
      gap: var(--espacio-1);
      margin: 0 calc(-1 * var(--espacio-4));
      padding: 0 var(--espacio-4);
      overflow-x: auto;
      list-style: none;
      scrollbar-width: none;
      scroll-padding-inline: var(--espacio-4);
      overscroll-behavior-x: contain;
    }

    .secciones a {
      display: inline-flex;
      align-items: center;
      min-height: var(--tactil-minimo);
      padding: 0 var(--espacio-3);
      border-radius: var(--radio-lg);
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
      font-weight: var(--peso-medio);
      text-decoration: none;
      white-space: nowrap;
      touch-action: manipulation;
      user-select: none;
    }

    .secciones a:active:not(.activa) {
      background: var(--color-superficie-hundida);
    }

    .secciones a:focus-visible {
      outline: none;
      box-shadow: var(--foco);
    }

    .secciones a.activa {
      background: var(--color-accion-tinte);
      color: var(--color-accion-tinta);
    }

    .privacidad {
      display: none;
      margin: 0;
      color: var(--color-texto-suave);
      font-size: var(--texto-xs);
    }

    .contenido {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      align-content: start;
      gap: var(--espacio-5);
      min-width: 0;
    }

    @media (min-width: 64rem) {
      .panel {
        grid-template-columns: 12rem minmax(0, 1fr);
        gap: var(--espacio-6);
        padding-block-start: var(--espacio-7);
        padding-inline: var(--margen-app);
      }

      .secciones {
        position: sticky;
        inset-block-start: var(--espacio-5);
        align-self: start;
        display: grid;
        gap: var(--espacio-5);
      }

      .secciones ul {
        flex-direction: column;
        margin: 0;
        padding: 0;
        overflow: visible;
      }

      .secciones a {
        display: flex;
      }

      .privacidad {
        display: block;
        padding-inline: var(--espacio-3);
      }
    }

    @media (hover: hover) and (pointer: fine) {
      .secciones a:not(.activa):hover {
        background: var(--color-superficie-hundida);
        color: var(--color-texto);
      }
    }

    @media (forced-colors: active) {
      .secciones a.activa {
        outline: 2px solid Highlight;
      }
    }
  `,
})
export class Admin {
  protected readonly secciones = [
    { ruta: 'resumen', clave: 'resumen' },
    { ruta: 'metricas', clave: 'metricas' },
    { ruta: 'costos', clave: 'costos' },
    { ruta: 'docentes', clave: 'docentes' },
    { ruta: 'errores', clave: 'errores' },
    { ruta: 'tipos-rubro', clave: 'tiposRubro' },
    { ruta: 'auditoria', clave: 'auditoria' },
  ] as const;

  constructor() {
    controlarPortada()({
      titulo: inject(TranslocoService).translate('admin.titulo'),
      subtitulo: 'Aula Lista',
    });
  }

  /** En el celular la fila de secciones se desliza: la activa siempre queda a la vista. */
  protected alaVista(enlace: HTMLElement): void {
    enlace.scrollIntoView({ block: 'nearest', inline: 'center' });
  }
}
