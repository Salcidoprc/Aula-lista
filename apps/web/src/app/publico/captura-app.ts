import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { TemaService } from '../core/tema/tema.service';

/** Tamaño real de las capturas (se toman a 2x): celular 412 × 915 y computadora 1440 × 900. */
const TAMANOS = {
  cel: { ancho: 824, alto: 1830 },
  esc: { ancho: 2880, alto: 1800 },
} as const;

/**
 * Captura real de la app en la landing (RF-010), dentro de un marco de celular o de computadora. Si la
 * captura tiene versión oscura, se elige la del tema activo para que la página no se vea a medias.
 */
@Component({
  selector: 'app-captura-app',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class]': 'tipo()' },
  template: `
    <img
      [src]="fuente()"
      [alt]="alt()"
      [width]="tamano().ancho"
      [height]="tamano().alto"
      [attr.loading]="prioridad() ? null : 'lazy'"
      [attr.fetchpriority]="prioridad() ? 'high' : null"
      decoding="async"
    />
  `,
  styles: `
    :host {
      display: block;
      overflow: hidden;
      background: var(--color-superficie);
      box-shadow: var(--sombra-flotante);
    }

    img {
      display: block;
      inline-size: 100%;
      block-size: auto;
    }

    /* Celular: bisel delgado en tinta, esquinas de teléfono. */
    :host(.cel) {
      inline-size: min(100%, var(--ancho-captura, 17.5rem));
      border: 0.4375rem solid var(--color-texto);
      border-radius: 2.25rem;
    }

    /* Computadora: una barra de ventana sin adornos sobre la captura. */
    :host(.esc) {
      border-radius: var(--radio-lg);
      border-block-start: 1.375rem solid var(--color-superficie-hundida);
    }
  `,
})
export class CapturaApp {
  private readonly tema = inject(TemaService).tema;

  /** Nombre del archivo en /landing sin extensión. */
  readonly nombre = input.required<string>();
  readonly tipo = input.required<'cel' | 'esc'>();
  readonly alt = input.required<string>();
  /** Hay versión `-oscuro`. */
  readonly oscuro = input(false);
  /** Está en la primera pantalla: se carga de inmediato. */
  readonly prioridad = input(false);

  protected readonly tamano = computed(() => TAMANOS[this.tipo()]);
  protected readonly fuente = computed(
    () =>
      `/landing/${this.nombre()}${this.oscuro() && this.tema() === 'oscuro' ? '-oscuro' : ''}.webp`,
  );
}
