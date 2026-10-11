import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { SaludService } from '../../core/salud/salud.service';

/** Pantalla provisional de H0. La reemplaza el inicio del docente de SPEC-01/SPEC-02. */
@Component({
  selector: 'app-inicio',
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="inicio" *transloco="let t; prefix: 'inicio'">
      <h1>{{ t('titulo') }}</h1>
      <p class="descripcion">{{ t('descripcion') }}</p>

      <div class="estado" role="status" [attr.data-estado]="salud.estado()">
        <span class="estacion" aria-hidden="true"></span>
        <span>{{ t('estado.' + salud.estado()) }}</span>
        @if (salud.estado() === 'sinServidor') {
          <button type="button" class="reintentar" (click)="salud.revisar()">
            {{ t('estado.reintentar') }}
          </button>
        }
      </div>
    </section>
  `,
  styles: `
    .inicio {
      display: grid;
      gap: var(--espacio-4);
      max-width: var(--ancho-lectura);
      padding: var(--espacio-6) max(var(--espacio-4), env(safe-area-inset-left));
    }

    h1 {
      font-size: var(--texto-2xl);
    }

    .descripcion {
      color: var(--color-texto-suave);
      font-size: var(--texto-lg);
    }

    .estado {
      --color-estacion: var(--color-texto-tenue);
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--espacio-3);
      margin-top: var(--espacio-4);
      color: var(--color-texto-suave);
    }

    .estado[data-estado='enLinea'] {
      --color-estacion: var(--color-exito);
    }

    .estado[data-estado='sinServidor'] {
      --color-estacion: var(--color-peligro);
      color: var(--color-peligro-tinta);
    }

    .estacion {
      width: 0.875rem;
      height: 0.875rem;
      border-radius: var(--radio-estacion);
      border: 3px solid var(--color-estacion);
      background: var(--color-superficie);
      transition: border-color var(--dur-small) var(--ease-out);
    }

    .reintentar {
      min-height: var(--tactil-minimo);
      padding: 0 var(--espacio-4);
      border: 0;
      border-radius: var(--radio-md);
      background: var(--color-accion);
      color: var(--color-sobre-accion);
      font-weight: var(--peso-medio);
      cursor: pointer;
      transition: transform var(--dur-press) var(--ease-out);
    }

    .reintentar:active {
      transform: scale(var(--escala-presion));
    }

    @media (hover: hover) and (pointer: fine) {
      .reintentar:hover {
        background: var(--color-accion-presionada);
      }
    }
  `,
})
export class Inicio implements OnInit {
  protected readonly salud = inject(SaludService);

  ngOnInit(): void {
    void this.salud.revisar();
  }
}
