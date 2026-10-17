import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { Icono } from '../../shared/iconos';
import { ColaSincronizacion, EstadoSincronizacion } from './cola-sincronizacion';

/** Avisa a partir del tercer día sin enviar (SPEC-03, casos borde). */
const DIAS_AVISO = 3;
/** Lo que debe durar un estado para mostrarse. */
const ESPERA_MS = 400;

/**
 * El único indicador de sincronización (reglas/02, 02-arquitectura): "Guardado", "Pendiente (n)" o
 * "Sin conexión". Va en la navegación de la portada. El ícono cambia con fundido y desenfoque de 200 ms; el de
 * "enviando" gira mientras dura (regla 07). Con movimiento reducido no gira.
 */
@Component({
  selector: 'app-indicador-sincronizacion',
  imports: [Icono, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[attr.data-estado]': 'estado()',
    '[class.atrasado]': 'atrasado()',
    '[class.listo]': 'listo()',
  },
  template: `
    <ng-container *transloco="let t; prefix: 'sincronizacion'">
      <!-- Un caso por estado: al cambiar, el ícono se crea de nuevo y entra con su fundido. -->
      @switch (estado()) {
        @case ('guardado') {
          <app-icono class="icono" nombre="guardado" />
        }
        @case ('pendiente') {
          <app-icono class="icono" nombre="pendiente" />
        }
        @case ('enviando') {
          <app-icono class="icono" nombre="sincronizando" />
        }
        @case ('sin_conexion') {
          <app-icono class="icono" nombre="sinConexion" />
        }
      }
      <span class="texto">
        @if (atrasado()) {
          {{ t('atrasado', { dias: cola.diasSinEnviar(), cantidad: cola.pendientes() }) }}
        } @else {
          {{ t(estado(), { cantidad: cola.pendientes() }) }}
        }
      </span>
      <!-- Solo se anuncia lo que pide atención: cada captura pasa por pendiente y guardado sin hablar. -->
      <span class="solo-lector" role="status">
        @if (atrasado()) {
          {{ t('atrasado', { dias: cola.diasSinEnviar(), cantidad: cola.pendientes() }) }}
        } @else if (estado() === 'sin_conexion') {
          {{ t('sin_conexion', { cantidad: cola.pendientes() }) }}
        }
      </span>
    </ng-container>
  `,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: var(--espacio-1);
      min-height: var(--tactil-minimo);
      padding: 0 var(--espacio-2);
      color: var(--grado-tinte);
      font-size: var(--texto-sm);
      font-weight: var(--peso-medio);
    }

    /* En 320 px con texto grande el aviso puede ocupar dos renglones; no empuja la página de lado. */
    .texto {
      min-width: 0;
      overflow-wrap: anywhere;
    }

    :host([data-estado='sin_conexion']),
    :host(.atrasado) {
      color: var(--color-sobre-grado);
    }

    .icono {
      width: 1rem;
      height: 1rem;
    }

    /* Solo al cambiar de estado, no al cargar la app. */
    :host(.listo) .icono {
      transition:
        opacity var(--dur-icono) var(--ease-out),
        filter var(--dur-icono) var(--ease-out);

      @starting-style {
        opacity: 0;
        filter: blur(2px);
      }
    }

    :host([data-estado='enviando']) .icono {
      animation: girar var(--dur-giro) linear infinite;
    }

    @keyframes girar {
      to {
        rotate: 1turn;
      }
    }

    /* Movimiento reducido: queda el fundido, sin desenfoque ni giro. */
    @media (prefers-reduced-motion: reduce) {
      :host(.listo) .icono {
        transition: opacity var(--dur-icono) var(--ease-out);

        @starting-style {
          opacity: 0;
          filter: none;
        }
      }

      :host([data-estado='enviando']) .icono {
        animation: none;
      }
    }
  `,
})
export class IndicadorSincronizacion {
  protected readonly cola = inject(ColaSincronizacion);
  /**
   * El estado que se muestra. Cada captura pasa por "por enviar" y "enviando" en milisegundos; mostrarlo todo
   * animaría una acción repetida (regla 07). Un estado se muestra si dura más de ESPERA_MS; "sin conexión", al
   * instante.
   */
  protected readonly estado = signal<EstadoSincronizacion>(this.cola.estado());
  protected readonly listo = signal(false);
  protected readonly atrasado = computed(
    () => this.cola.pendientes() > 0 && this.cola.diasSinEnviar() >= DIAS_AVISO,
  );

  constructor() {
    effect((alLimpiar) => {
      const nuevo = this.cola.estado();
      if (nuevo === untracked(this.estado)) {
        return;
      }
      const mostrar = (): void => {
        this.estado.set(nuevo);
        this.listo.set(true);
      };
      if (nuevo === 'sin_conexion') {
        mostrar();
        return;
      }
      const espera = setTimeout(mostrar, ESPERA_MS);
      alLimpiar(() => clearTimeout(espera));
    });
  }
}
