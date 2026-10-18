import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { BoletaPublicadaDto } from '../api/models/boleta-publicada-dto';
import { HijoDto } from '../api/models/hijo-dto';
import { claveGrado } from '../shared/grados';
import { Icono } from '../shared/iconos';
import { controlarPortada } from '../shared/portada/portada.service';
import {
  calificacion,
  esCorregida,
  fechaCorta,
  fechaLarga,
  grupoDelHijo,
  nombreCompleto,
  peso,
  primerNombre,
  transicionBoleta,
} from './portal';
import { PortalService, urlPdf } from './portal.service';

/**
 * La boleta de un periodo en el portal (SPEC-06): lo mismo que el PDF, en HTML para leerla sin descargar nada. La
 * tarjeta del periodo se expande hacia la cabecera con la View Transition compartida (catálogo de la regla 07).
 */
@Component({
  selector: 'app-boleta-tutor',
  imports: [TranslocoDirective, RouterLink, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="pantalla pantalla--ancha" *transloco="let t; prefix: 'portal'">
      @if (hijo(); as h) {
        <a class="volver" [routerLink]="['/tutor/alumnos', h.id]">
          <app-icono nombre="volver" />
          {{ t('volverA', { nombre: corto(h) }) }}
        </a>
      }
      @switch (estado()) {
        @case ('cargando') {
          <span
            class="esqueleto cabeza-esqueleto"
            role="status"
            [attr.aria-label]="t('cargando')"
          ></span>
        }
        @case ('error') {
          <div class="aviso aviso--error" role="alert">
            <p>{{ t('errorRed') }}</p>
            <button class="boton boton--secundario" type="button" (click)="cargar()">
              {{ t('reintentar') }}
            </button>
          </div>
        }
        @case ('no_encontrada') {
          <p class="vacio">{{ t('boletaNoEncontrada') }}</p>
        }
        @case ('lista') {
          @let b = boleta()!;
          <header
            class="cabeza"
            [style.view-transition-name]="transicion(b.id)"
            style="view-transition-class: boleta"
          >
            <div class="cabeza-datos">
              <h1>{{ b.unidad }}</h1>
              <p class="etiqueta">{{ t('ciclo', { ciclo: b.ciclo }) }}</p>
              <p class="etiqueta">{{ t('publicada', { fecha: fecha(b.publicadaEn) }) }}</p>
              @if (corregida(b.version)) {
                <p class="corregida">
                  {{ t('versionCorregida', { fecha: fechaCorta(b.publicadaEn) }) }}
                </p>
              }
            </div>
            <p class="final">
              <span class="solo-lector">{{ t('calificacionFinal') }}</span>
              <span class="cifras">{{ final(b.calificacion, b.decimalesFinal) }}</span>
            </p>
          </header>

          <section aria-labelledby="rubros-titulo" class="seccion">
            <h2 id="rubros-titulo">{{ t('comoSeCalculo') }}</h2>
            <table class="rubros">
              <thead>
                <tr>
                  <th scope="col">{{ t('rubro') }}</th>
                  <th scope="col">{{ t('peso') }}</th>
                  <th scope="col">{{ t('promedio') }}</th>
                </tr>
              </thead>
              <tbody>
                @for (r of b.rubros; track $index) {
                  <tr>
                    <th scope="row">{{ r.nombre }}</th>
                    <td class="cifras">{{ porcentaje(r.peso) }}</td>
                    <td class="cifras">{{ final(r.promedio, 2) }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </section>

          <section aria-labelledby="observacion-titulo" class="seccion">
            <h2 id="observacion-titulo">{{ t('observacion') }}</h2>
            <p class="observacion">{{ b.observacion }}</p>
          </section>

          <div class="pie">
            <a class="boton boton--ancho" [href]="pdf(b.id)" download>
              <app-icono nombre="descargar" />
              {{ t('descargarPdf') }}
            </a>
            <p class="etiqueta">
              {{ t('codigo') }} <span class="codigo">{{ b.codigo }}</span>
            </p>
            <p class="etiqueta">{{ t('noOficial') }}</p>
          </div>
        }
      }
    </section>
  `,
  styles: `
    .pantalla {
      max-width: calc(var(--ancho-lectura) + 2 * var(--espacio-4));
    }

    @media (min-width: 48rem) {
      .pantalla {
        max-width: calc(var(--ancho-lectura) + 2 * var(--margen-app));
      }
    }

    /* Misma forma que la tarjeta del periodo: es ella, ya abierta. */
    .cabeza {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      align-items: center;
      gap: var(--espacio-4);
      padding: var(--espacio-5) var(--espacio-4);
      border-radius: var(--radio-lg);
      background: var(--grado-tinte);
      color: var(--grado-tinta);
    }

    .cabeza-datos {
      display: grid;
      gap: var(--espacio-1);
      min-width: 0;
    }

    .cabeza h1 {
      font-size: var(--texto-xl);
    }

    .cabeza .etiqueta {
      color: inherit;
    }

    .final {
      font-size: 3rem;
      font-weight: var(--peso-fuerte);
      line-height: 1;
    }

    .etiqueta {
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
    }

    .corregida {
      justify-self: start;
      padding: 0 var(--espacio-2);
      border-radius: var(--radio-sm);
      background: var(--color-aviso-tinte);
      color: var(--color-aviso-tinta);
      font-size: var(--texto-xs);
      font-weight: var(--peso-medio);
    }

    .seccion {
      display: grid;
      gap: var(--espacio-3);
    }

    .seccion h2 {
      font-size: var(--texto-lg);
    }

    .rubros {
      width: 100%;
      border-collapse: collapse;
    }

    .rubros th,
    .rubros td {
      padding: var(--espacio-2) 0;
      border-bottom: 1px solid var(--color-divisoria);
      text-align: end;
      font-weight: var(--peso-normal);
    }

    .rubros thead th {
      color: var(--color-texto-suave);
      font-size: var(--texto-xs);
      font-weight: var(--peso-medio);
    }

    .rubros th:first-child {
      text-align: start;
      overflow-wrap: anywhere;
    }

    .rubros td:last-child {
      width: 5.5rem;
      font-weight: var(--peso-medio);
    }

    .rubros td:nth-child(2) {
      width: 4.5rem;
      color: var(--color-texto-suave);
    }

    .observacion {
      max-width: var(--ancho-lectura);
      font-size: var(--texto-lg);
      line-height: 1.6;
      white-space: pre-line;
    }

    .pie {
      display: grid;
      gap: var(--espacio-2);
    }

    @media (min-width: 48rem) {
      .pie .boton {
        justify-self: start;
        width: auto;
      }
    }

    .codigo {
      color: var(--color-texto);
      font-weight: var(--peso-medio);
      letter-spacing: 0.08em;
    }

    .cabeza-esqueleto {
      display: block;
      height: 8rem;
      border-radius: var(--radio-lg);
    }

    .vacio {
      color: var(--color-texto-suave);
    }
  `,
})
export class BoletaTutor {
  private readonly portal = inject(PortalService);
  private readonly portada = controlarPortada();

  readonly id = input.required<string>();
  readonly boletaId = input.required<string>();

  protected readonly boleta = signal<BoletaPublicadaDto | null>(null);
  protected readonly hijo = signal<HijoDto | null>(null);
  protected readonly estado = signal<'cargando' | 'error' | 'no_encontrada' | 'lista'>('cargando');

  protected readonly final = calificacion;
  protected readonly fecha = fechaLarga;
  protected readonly porcentaje = peso;
  protected readonly transicion = transicionBoleta;
  protected readonly corregida = esCorregida;
  protected readonly fechaCorta = fechaCorta;
  protected readonly pdf = urlPdf;

  constructor() {
    effect(() => {
      this.id();
      this.boletaId();
      untracked(() => void this.cargar());
    });
  }

  protected corto(hijo: HijoDto): string {
    return primerNombre(hijo.nombres) ?? nombreCompleto(hijo);
  }

  protected async cargar(): Promise<void> {
    this.estado.set('cargando');
    try {
      const detalle = await this.portal.detalle(this.id());
      const h = detalle.hijo;
      this.hijo.set(h);
      this.portada({
        titulo: grupoDelHijo(h),
        subtitulo: nombreCompleto(h),
        numero: String(Number(h.grado)),
        grado: claveGrado(h.nivel, h.grado),
      });
      const boleta = detalle.boletas.find((b) => b.id === this.boletaId()) ?? null;
      this.boleta.set(boleta);
      this.estado.set(boleta ? 'lista' : 'no_encontrada');
    } catch (error) {
      if (!this.portal.manejarSinSesion(error)) {
        this.estado.set('error');
      }
    }
  }
}
