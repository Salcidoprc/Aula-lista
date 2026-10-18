import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { PlanesDto } from '../../api/models/planes-dto';
import { SesionSimuladaDto } from '../../api/models/sesion-simulada-dto';
import { codigoDeError } from '../../core/errores/problema';
import { fechaLarga } from '../../shared/fechas';
import { nombreGrupo } from '../../shared/grados';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { cifra, referenciaEnBloques } from './planes';
import { PlanesService } from './planes.service';

/**
 * "Pago de prueba" (SPEC-08, solo sin Stripe): una página de Aula Lista, no una imitación del proveedor. Confirma o
 * rechaza la sesión con los mismos eventos que mandaría Stripe; con OXXO muestra la ficha y deja simular el pago en
 * tienda o que venza.
 */
@Component({
  selector: 'app-pago-simulado',
  imports: [TranslocoDirective, RouterLink, Icono, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pantalla" *transloco="let t; prefix: 'plan.simulado'">
      <a class="volver" routerLink="/plan">
        <app-icono nombre="volver" />
        {{ t('volver') }}
      </a>
      <hgroup>
        <h1 tabindex="-1">{{ t('titulo') }}</h1>
        <p class="entrada">{{ t('entrada') }}</p>
      </hgroup>

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <a class="enlace enlace--solo" routerLink="/plan">{{ t('irAlPlan') }}</a>
        </div>
      } @else if (!sesion()) {
        <span
          class="esqueleto bloque-esqueleto"
          role="status"
          [attr.aria-label]="t('cargando')"
        ></span>
      } @else if (sesion(); as s) {
        <section class="recibo" aria-labelledby="recibo-titulo">
          <h2 id="recibo-titulo">{{ t(s.oxxo ? 'ficha' : 'tarjeta') }}</h2>
          <p class="monto cifras">
            <span class="cifra">{{ cifra(s.monto) }}</span>
            <span class="unidad">{{ moneda() }}</span>
          </p>
          <p class="concepto">{{ concepto() }}</p>
          @if (s.grupos.length) {
            <ul class="grupos">
              @for (g of s.grupos; track g.id) {
                <li>{{ nombre(g.grado, g.nombre) }}</li>
              }
            </ul>
          }
          @if (s.oxxo) {
            <dl class="datos">
              <div>
                <dt>{{ t('referencia') }}</dt>
                <dd class="referencia cifras">{{ referencia() }}</dd>
              </div>
              @if (s.venceEn) {
                <div>
                  <dt>{{ t('vence') }}</dt>
                  <dd>{{ fecha(s.venceEn) }}</dd>
                </div>
              }
            </dl>
          }
        </section>

        @if (s.estado === 'abierta') {
          <p class="nota">{{ t(s.oxxo ? 'notaOxxo' : 'notaTarjeta') }}</p>
          <div class="acciones">
            <button class="boton" type="button" [appOcupado]="enviando()" (click)="terminar(true)">
              {{
                s.oxxo
                  ? t('pagarEnTienda')
                  : t('pagar', { monto: cifra(s.monto), moneda: moneda() })
              }}
            </button>
            <button
              class="boton boton--secundario"
              type="button"
              [appOcupado]="enviando()"
              (click)="terminar(false)"
            >
              {{ s.oxxo ? t('vencer') : t('rechazar') }}
            </button>
          </div>
          @if (errorAccion(); as codigo) {
            <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
          }
        } @else {
          <div
            class="aviso"
            [class.aviso--exito]="s.estado === 'pagada'"
            [class.aviso--pendiente]="s.estado !== 'pagada'"
            role="status"
          >
            <p>{{ t('resultado.' + s.estado + (s.oxxo ? 'Oxxo' : '')) }}</p>
            <a class="enlace enlace--solo" routerLink="/plan">{{ t('irAlPlan') }}</a>
          </div>
        }
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .bloque-esqueleto {
      display: block;
      height: 14rem;
    }

    /* El recibo es papel: borde de la divisoria, sin sombra; no se parece al Checkout del proveedor. */
    .recibo {
      display: grid;
      gap: var(--espacio-3);
      padding: var(--espacio-5);
      border: 1px solid var(--color-divisoria);
      border-radius: var(--radio-lg);
      background: var(--color-superficie);
    }

    .recibo h2 {
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
      font-weight: var(--peso-medio);
    }

    .monto {
      display: flex;
      align-items: baseline;
      gap: var(--espacio-2);
    }

    .cifra {
      font-size: var(--texto-2xl);
      font-weight: var(--peso-fuerte);
      line-height: 1;
    }

    .unidad {
      color: var(--color-texto-suave);
      font-weight: var(--peso-medio);
    }

    .concepto {
      font-weight: var(--peso-medio);
    }

    .grupos {
      display: flex;
      flex-wrap: wrap;
      gap: var(--espacio-1) var(--espacio-3);
      margin: 0;
      padding: 0;
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
      list-style: none;
    }

    .datos {
      display: grid;
      gap: var(--espacio-3);
      margin: 0;
      padding-block-start: var(--espacio-3);
      border-top: 1px dashed var(--color-divisoria);
    }

    .datos dt {
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
    }

    .datos dd {
      margin: 0;
      font-weight: var(--peso-medio);
    }

    .referencia {
      font-size: var(--texto-xl);
      letter-spacing: 0.04em;
      font-weight: var(--peso-fuerte);
    }

    .nota {
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
    }

    .acciones {
      display: grid;
      gap: var(--espacio-2);
    }

    @media (min-width: 48rem) {
      .acciones {
        display: flex;
        flex-wrap: wrap;
      }
    }

    .aviso {
      justify-items: start;
    }
  `,
})
export class PagoSimulado implements OnInit {
  protected readonly cifra = cifra;
  private readonly servicio = inject(PlanesService);
  private readonly i18n = inject(TranslocoService);
  private readonly router = inject(Router);

  readonly id = input.required<string>();

  protected readonly sesion = signal<SesionSimuladaDto | null>(null);
  private readonly planes = signal<PlanesDto | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly errorAccion = signal<string | null>(null);
  protected readonly enviando = signal(false);

  protected readonly moneda = computed(() => this.planes()?.moneda ?? '');
  protected readonly referencia = computed(() =>
    referenciaEnBloques(this.sesion()?.referencia ?? ''),
  );
  protected readonly concepto = computed(() => {
    const s = this.sesion();
    if (!s) {
      return '';
    }
    if (s.tipo === 'suscripcion') {
      return this.texto('plan.simulado.concepto.suscripcion');
    }
    return s.tipo === 'prepago'
      ? this.texto('plan.simulado.concepto.prepago', { meses: s.meses })
      : this.texto('plan.simulado.concepto.agregar');
  });

  async ngOnInit(): Promise<void> {
    try {
      const [planes, sesion] = await Promise.all([
        this.servicio.planes(),
        this.servicio.pagoSimulado(this.id()),
      ]);
      this.planes.set(planes);
      this.sesion.set(sesion);
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected texto(clave: string, datos?: Record<string, unknown>): string {
    return this.i18n.translate(clave, datos);
  }

  protected nombre(grado: number | string, nombre: string): string {
    return nombreGrupo(grado, nombre);
  }

  protected fecha(instante: string): string {
    return fechaLarga(instante);
  }

  /** Pagar regresa al plan como lo haría el Checkout; rechazar o dejar vencer se queda a decir qué pasó. */
  protected async terminar(pagar: boolean): Promise<void> {
    this.errorAccion.set(null);
    this.enviando.set(true);
    try {
      const sesion = await this.servicio.terminarPagoSimulado(this.id(), pagar);
      if (pagar) {
        await this.router.navigate(['/plan'], { queryParams: { pago: 'listo' } });
        return;
      }
      this.sesion.set(sesion);
    } catch (e) {
      this.errorAccion.set(codigoDeError(e));
    } finally {
      this.enviando.set(false);
    }
  }
}
