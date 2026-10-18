import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { Icono } from '../shared/iconos';

/**
 * Enlace inválido o vencido (SPEC-06, pantalla 4). El mismo mensaje para un enlace alterado, revocado, vencido o
 * que nunca existió: nunca dice cuál de esos fue.
 */
@Component({
  selector: 'app-enlace-invalido',
  imports: [TranslocoDirective, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="pantalla" *transloco="let t; prefix: 'portal'">
      <span class="sello"><app-icono nombre="enlaceRoto" /></span>
      <h1>{{ t('invalidoTitulo') }}</h1>
      <p class="entrada">{{ t('invalidoTexto') }}</p>
      <p class="nota">{{ t('invalidoNota') }}</p>
    </section>
  `,
  styles: `
    .sello {
      display: inline-grid;
      place-items: center;
      width: 3.5rem;
      height: 3.5rem;
      border-radius: var(--radio-pleno);
      background: var(--color-accion-tinte);
      color: var(--color-accion-tinta);
    }

    .sello app-icono {
      width: 1.75rem;
      height: 1.75rem;
    }

    .nota {
      color: var(--color-texto-suave);
    }
  `,
})
export class EnlaceInvalido {}
