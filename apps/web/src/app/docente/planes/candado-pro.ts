import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { PlanGrupoDto } from '../../api/models/plan-grupo-dto';
import { PlanesDto } from '../../api/models/planes-dto';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { cifra } from './planes';
import { PlanesService } from './planes.service';

/**
 * El momento de decidir en un grupo Gratis (SPEC-08, RN-080, RN-081): qué da Pro en esta pantalla y un camino claro.
 * En el cierre, si el docente no ha usado su prueba, la acción principal la activa en este grupo; si ya la usó, lleva a
 * su plan con el grupo elegido.
 */
@Component({
  selector: 'app-candado-pro',
  imports: [TranslocoDirective, RouterLink, Icono, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section
      class="candado"
      [attr.aria-labelledby]="'candado-' + funcion()"
      *transloco="let t; prefix: 'plan.candado'"
    >
      <div class="cabeza">
        <app-icono nombre="candado" />
        <h2 [id]="'candado-' + funcion()">{{ t(funcion() + '.titulo') }}</h2>
      </div>
      <p>{{ t(funcion() + '.texto') }}</p>

      @if (usarPrueba()) {
        @if (precios(); as p) {
          <p class="nota">{{ t('prueba', { dias: p.diasPrueba }) }}</p>
        }
        <div class="acciones">
          <button
            class="boton"
            type="button"
            [disabled]="!puedeUsarPrueba()"
            [appOcupado]="ocupado()"
            (click)="activarPrueba.emit()"
          >
            {{ t('usarPrueba') }}
          </button>
          <a class="enlace enlace--solo" routerLink="/plan" [queryParams]="{ grupo: grupoId() }">{{
            t('verPlanes')
          }}</a>
        </div>
      } @else {
        @if (precios(); as p) {
          <p class="nota cifras">
            {{
              t('precio', {
                primero: cifra(p.precioPrimerGrupo),
                adicional: cifra(p.precioGrupoAdicional),
                moneda: p.moneda,
              })
            }}
          </p>
        }
        @if (funcion() === 'boletas' && plan().pruebaDisponible) {
          <p class="nota">{{ t('pruebaEnCierre') }}</p>
        }
        <div class="acciones">
          <a class="boton" routerLink="/plan" [queryParams]="{ grupo: grupoId() }">{{
            t('activar')
          }}</a>
        </div>
      }
    </section>
  `,
  styles: `
    :host {
      display: block;
    }

    /* Bloque del color del grupo: es la pantalla del grupo la que pide Pro. Plano, sin sombra. */
    .candado {
      display: grid;
      justify-items: start;
      gap: var(--espacio-3);
      max-inline-size: 40rem;
      padding: var(--espacio-5);
      border-radius: var(--radio-lg);
      background: var(--grado-tinte);
      color: var(--color-texto);
    }

    .cabeza {
      display: flex;
      align-items: center;
      gap: var(--espacio-2);
      color: var(--grado-tinta);
    }

    h2 {
      font-size: var(--texto-lg);
    }

    p {
      max-inline-size: 65ch;
    }

    .nota {
      color: var(--grado-tinta);
      font-size: var(--texto-sm);
    }

    .acciones {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--espacio-2) var(--espacio-5);
      margin-block-start: var(--espacio-2);
    }

    @media (forced-colors: active) {
      .candado {
        border: 1px solid CanvasText;
      }
    }
  `,
})
export class CandadoPro implements OnInit {
  protected readonly cifra = cifra;
  private readonly planes = inject(PlanesService);

  readonly funcion = input.required<'cierre' | 'boletas'>();
  readonly plan = input.required<PlanGrupoDto>();
  readonly grupoId = input.required<string>();
  /** Se cumplen los requisitos del cierre y hay red: la prueba se activa al iniciarlo. */
  readonly puedeUsarPrueba = input(true);
  readonly ocupado = input(false);
  readonly activarPrueba = output();

  protected readonly precios = signal<PlanesDto | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      this.precios.set(await this.planes.planes());
    } catch {
      // Sin precios el candado sigue sirviendo: la pantalla Plan los vuelve a pedir.
    }
  }

  protected usarPrueba(): boolean {
    return this.funcion() === 'cierre' && this.plan().pruebaDisponible;
  }
}
