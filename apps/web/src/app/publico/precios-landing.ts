import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { Api } from '../api/api';
import { obtenerPlanes } from '../api/fn/planes/obtener-planes';
import { PlanesDto } from '../api/models/planes-dto';
import { Icono } from '../shared/iconos';

const MAXIMO_GRUPOS = 12;

/**
 * Planes de la landing (RF-010): Gratis contra Pro con los precios del producto a la venta en el catálogo
 * (`GET /planes`, ADR-0017), nunca escritos en el código. "¿Cuántos grupos?" calcula el total como la
 * pantalla Plan: primer grupo + adicionales. El total no se anima porque cambia con cada toque.
 */
@Component({
  selector: 'app-precios-landing',
  imports: [TranslocoDirective, RouterLink, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="planes" *transloco="let t; prefix: 'landing.precios'">
      <section class="plan gratis" aria-labelledby="plan-gratis">
        <h3 id="plan-gratis">{{ t('gratis') }}</h3>
        <p class="precio cifras">{{ t('gratisPrecio') }}</p>
        <p class="nota">{{ t('gratisNota') }}</p>
        <ul>
          @for (i of incluye; track i) {
            <li><app-icono nombre="guardado" />{{ t('gratisIncluye.' + i) }}</li>
          }
        </ul>
        <a class="boton boton--secundario" routerLink="/cuenta/registro">{{ t('accion') }}</a>
      </section>

      <section class="plan pro" aria-labelledby="plan-pro">
        <h3 id="plan-pro">{{ planes()?.producto ?? t('pro') }}</h3>
        @if (planes(); as p) {
          @if (p.aLaVenta) {
            <p class="precio">
              <span class="cifras">{{ t('total', { total: total(), moneda: p.moneda }) }}</span>
            </p>
            <p class="nota cifras">
              {{ t('primerGrupo', { precio: precio(p.precioPrimerGrupo), moneda: p.moneda }) }}
              {{ t('adicional', { precio: precio(p.precioGrupoAdicional), moneda: p.moneda }) }}
            </p>
            <div class="calculadora">
              <span id="cuantos">{{ t('calcular') }}</span>
              <div class="paso" role="group" aria-labelledby="cuantos">
                <button
                  type="button"
                  [attr.aria-label]="t('menos')"
                  [disabled]="grupos() <= 1"
                  (click)="grupos.set(grupos() - 1)"
                >
                  <app-icono nombre="menos" />
                </button>
                <output class="cifras" aria-live="polite">{{
                  grupos() === 1 ? t('grupos.uno', { n: 1 }) : t('grupos.otros', { n: grupos() })
                }}</output>
                <button
                  type="button"
                  [attr.aria-label]="t('mas')"
                  [disabled]="grupos() >= maximo"
                  (click)="grupos.set(grupos() + 1)"
                >
                  <app-icono nombre="mas" />
                </button>
              </div>
            </div>
          } @else {
            <p class="nota">{{ t('sinPrecio') }}</p>
          }
          <p class="todo">{{ t('todoGratis') }}</p>
          <ul>
            @for (f of p.funciones; track f) {
              <li><app-icono nombre="guardado" />{{ t('funcion.' + f) }}</li>
            }
          </ul>
          @if (p.aLaVenta) {
            <p class="nota">
              {{
                p.oxxoHabilitado && p.mesesPrepago.length
                  ? t('pago', { meses: meses(p) })
                  : t('pagoTarjeta')
              }}
            </p>
            <p class="prueba">{{ t('prueba') }}</p>
          }
        } @else if (fallo()) {
          <p class="nota">{{ t('sinPrecio') }}</p>
        } @else {
          <p class="nota">{{ t('cargando') }}</p>
        }
        <a class="boton" routerLink="/cuenta/registro">{{ t('accion') }}</a>
      </section>
    </div>
  `,
  styles: `
    .planes {
      display: grid;
      gap: var(--espacio-5);
    }

    .plan {
      display: flex;
      flex-direction: column;
      gap: var(--espacio-3);
      padding: var(--espacio-6) var(--espacio-5);
      border-radius: var(--radio-tapa);
    }

    .gratis {
      background: var(--color-superficie);
      box-shadow: var(--sombra-lomo), var(--sombra-flotante);
    }

    .pro {
      background: var(--color-marca);
      box-shadow: var(--sombra-lomo), var(--sombra-flotante);
      color: var(--color-sobre-grado);
    }

    h3 {
      margin: 0;
      font-size: var(--texto-xl);
    }

    .precio {
      margin: 0;
      font-size: clamp(var(--texto-2xl), 5vw, 3rem);
      font-weight: var(--peso-fuerte);
      line-height: 1.05;
      letter-spacing: -0.02em;
    }

    p {
      margin: 0;
    }

    .nota,
    .todo {
      color: var(--color-texto-suave);
    }

    .pro .nota,
    .pro .todo,
    .pro li {
      color: var(--color-marca-rotulo);
    }

    .prueba {
      padding: var(--espacio-3);
      border-radius: var(--radio-md);
      background: var(--color-sobre-grado);
      color: var(--color-sobre-portada-tinta);
      font-weight: var(--peso-medio);
    }

    ul {
      display: grid;
      gap: var(--espacio-2);
      margin: 0;
      padding: 0;
      list-style: none;
    }

    li {
      display: flex;
      gap: var(--espacio-2);
    }

    li app-icono {
      color: var(--color-exito-tinta);
    }

    .pro li app-icono {
      color: var(--color-sobre-grado);
    }

    .calculadora {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--espacio-3);
      padding: var(--espacio-3);
      border-radius: var(--radio-md);
      background: oklch(from var(--color-marca) calc(l - 0.07) c h);
    }

    .paso {
      display: flex;
      align-items: center;
      gap: var(--espacio-2);
    }

    .paso button {
      inline-size: var(--tactil-minimo);
      block-size: var(--tactil-minimo);
      border: 0;
      border-radius: var(--radio-pleno);
      background: var(--color-sobre-grado);
      color: var(--color-sobre-portada-tinta);
      display: grid;
      place-items: center;
      cursor: pointer;
      transition: transform var(--dur-press) var(--ease-out);
    }

    .paso button:active:not(:disabled) {
      transform: scale(var(--escala-presion));
    }

    .paso button:disabled {
      opacity: 0.45;
      cursor: default;
    }

    .paso button:focus-visible {
      outline: 2px solid var(--color-sobre-grado);
      outline-offset: 2px;
    }

    output {
      min-inline-size: 5.5rem;
      text-align: center;
      font-weight: var(--peso-fuerte);
    }

    .boton {
      margin-block-start: auto;
      text-decoration: none;
    }

    .pro .boton {
      background: var(--color-sobre-grado);
      color: var(--color-sobre-portada-tinta);
    }

    @media (min-width: 52rem) {
      .planes {
        grid-template-columns: 1fr 1.25fr;
        align-items: start;
      }
    }
  `,
})
export class PreciosLanding {
  private readonly api = inject(Api);

  protected readonly incluye = [0, 1, 2, 3, 4];
  protected readonly maximo = MAXIMO_GRUPOS;
  protected readonly planes = signal<PlanesDto | null>(null);
  protected readonly fallo = signal(false);
  protected readonly grupos = signal(1);

  protected readonly total = computed(() => {
    const p = this.planes();
    if (!p) {
      return '';
    }
    const total =
      Number(p.precioPrimerGrupo) + Number(p.precioGrupoAdicional) * (this.grupos() - 1);
    return this.precio(total);
  });

  constructor() {
    this.api
      .invoke(obtenerPlanes)
      .then((p) => this.planes.set(p))
      .catch(() => this.fallo.set(true));
  }

  protected precio(valor: number | string): string {
    return Number(valor).toLocaleString('es-MX', { maximumFractionDigits: 2 });
  }

  /** "1, 3 o 6" con los meses del catálogo. */
  protected meses(p: PlanesDto): string {
    const m = p.mesesPrepago.map(String);
    return m.length > 1 ? `${m.slice(0, -1).join(', ')} o ${m[m.length - 1]}` : (m[0] ?? '');
  }
}
