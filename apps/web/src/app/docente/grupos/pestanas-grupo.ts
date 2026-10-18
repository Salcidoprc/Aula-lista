import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { IsActiveMatchOptions, RouterLink, RouterLinkActive } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { Icono } from '../../shared/iconos';
import { ClaveGrado } from '../../shared/portada/portada';

/**
 * Las vistas del grupo: Calificaciones (la que abre, por decisión del docente), Alumnos, Asistencia y Avisos. Son páginas, no
 * paneles: van como navegación con `aria-current`, no como pestañas ARIA. La configuración queda a la derecha.
 */
@Component({
  selector: 'app-pestanas-grupo',
  imports: [RouterLink, RouterLinkActive, TranslocoDirective, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-grado]': 'grado() ?? null' },
  template: `
    <div class="barra" *transloco="let t; prefix: 'pestanasGrupo'">
      <nav class="vistas" [attr.aria-label]="t('nombre')">
        <a
          [routerLink]="['/grupos', id()]"
          routerLinkActive="activa"
          #v1
          (isActiveChange)="aLaVista($event, v1)"
          [routerLinkActiveOptions]="soloRuta"
          ariaCurrentWhenActive="page"
          >{{ t('calificaciones') }}</a
        >
        <a
          [routerLink]="['/grupos', id(), 'alumnos']"
          routerLinkActive="activa"
          #v2
          (isActiveChange)="aLaVista($event, v2)"
          ariaCurrentWhenActive="page"
          >{{ t('alumnos') }}</a
        >
        <a
          [routerLink]="['/grupos', id(), 'asistencia']"
          routerLinkActive="activa"
          #v3
          (isActiveChange)="aLaVista($event, v3)"
          ariaCurrentWhenActive="page"
          >{{ t('asistencia') }}</a
        >
        <a
          [routerLink]="['/grupos', id(), 'avisos']"
          routerLinkActive="activa"
          #v4
          (isActiveChange)="aLaVista($event, v4)"
          ariaCurrentWhenActive="page"
          >{{ t('avisos') }}</a
        >
      </nav>
      <a class="ajustes" [routerLink]="['/grupos', id(), 'ajustes']">
        <app-icono nombre="ajustes" />
        <span class="texto">{{ t('configuracion') }}</span>
      </a>
    </div>
  `,
  styles: `
    .barra {
      display: flex;
      align-items: end;
      justify-content: space-between;
      gap: var(--espacio-2);
      border-bottom: 1px solid var(--color-divisoria);
    }

    /* Con cuatro vistas, en 320 px o con texto grande la fila se desliza sola; la página no se desborda. */
    .vistas {
      display: flex;
      gap: var(--espacio-1);
      min-width: 0;
      overflow-x: auto;
      overscroll-behavior-x: contain;
      scrollbar-width: none;
    }

    .ajustes {
      flex: none;
    }

    @media (max-width: 29.99rem) {
      .vistas a {
        padding: 0 var(--espacio-2);
      }
    }

    .vistas a {
      display: inline-flex;
      align-items: center;
      min-height: var(--tactil-minimo);
      padding: 0 var(--espacio-3);
      border-radius: var(--radio-md) var(--radio-md) 0 0;
      color: var(--color-texto-suave);
      font-weight: var(--peso-medio);
      flex: none;
      text-decoration: none;
      box-shadow: inset 0 -3px 0 transparent;
    }

    /* La pestaña activa toma el color del grado: es parte de la pantalla del grupo (DESIGN.md). */
    .vistas a.activa {
      color: var(--grado-tinta);
      box-shadow: inset 0 -3px 0 var(--grado);
    }

    .vistas a:focus-visible,
    .ajustes:focus-visible {
      box-shadow: var(--foco);
    }

    /* Con foco, la activa conserva su raya: el foco no borra cuál está elegida. */
    .vistas a.activa:focus-visible {
      box-shadow:
        var(--foco),
        inset 0 -3px 0 var(--grado);
    }

    .ajustes {
      display: inline-flex;
      align-items: center;
      gap: var(--espacio-2);
      min-height: var(--tactil-minimo);
      min-width: var(--tactil-minimo);
      justify-content: center;
      padding: 0 var(--espacio-2);
      border-radius: var(--radio-lg);
      color: var(--color-accion-tinta);
      font-weight: var(--peso-medio);
      text-decoration: none;
    }

    .ajustes:active {
      color: var(--color-accion-presionada);
    }

    /* En el celular solo el ícono, con su nombre para el lector de pantalla. */
    @media (max-width: 29.99rem) {
      .ajustes .texto {
        position: absolute;
        width: 1px;
        height: 1px;
        overflow: hidden;
        clip-path: inset(50%);
        white-space: nowrap;
      }
    }

    @media (hover: hover) and (pointer: fine) {
      .vistas a:not(.activa):hover {
        color: var(--color-texto);
      }

      .ajustes:hover {
        text-decoration: underline;
        text-underline-offset: 0.2em;
      }
    }
  `,
})
export class PestanasGrupo {
  /** La unidad elegida va en la consulta (?unidad=); la pestaña sigue activa. */
  protected readonly soloRuta: IsActiveMatchOptions = {
    paths: 'exact',
    queryParams: 'ignored',
    matrixParams: 'ignored',
    fragment: 'ignored',
  };
  readonly id = input.required<string>();
  /** Color del grado para la pestaña activa. */
  readonly grado = input<ClaveGrado>();

  /** Si la fila se desliza (320 px o texto grande), la vista activa queda a la vista. */
  protected aLaVista(activa: boolean, enlace: HTMLElement): void {
    if (activa) {
      requestAnimationFrame(() => enlace.scrollIntoView({ block: 'nearest', inline: 'nearest' }));
    }
  }
}
