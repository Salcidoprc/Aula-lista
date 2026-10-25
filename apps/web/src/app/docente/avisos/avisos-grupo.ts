import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { AvisosDto } from '../../api/models/avisos-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { codigoDeError } from '../../core/errores/problema';
import { claveGrado } from '../../shared/grados';
import { Icono } from '../../shared/iconos';
import { forma } from '../../shared/plural';
import { controlarPortada } from '../../shared/portada/portada.service';
import { portadaDeGrupo } from '../grupos/grupos.service';
import { PestanasGrupo } from '../grupos/pestanas-grupo';
import { fechaAviso, fraccionLeidos } from './avisos';
import { AvisosGrupoService } from './avisos.service';

/**
 * SPEC-07, "Avisos del grupo" (RF-070): los avisos enviados, del más nuevo al más viejo, con cuántos tutores ya lo
 * leyeron. La lista no se anima al entrar: el docente la abre muchas veces.
 */
@Component({
  selector: 'app-avisos-grupo',
  imports: [TranslocoDirective, RouterLink, PestanasGrupo, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="pantalla pantalla--ancha"
      [attr.data-grado]="clave() ?? null"
      *transloco="let t; prefix: 'avisos'"
    >
      <a class="volver" routerLink="/">
        <app-icono nombre="volver" />
        {{ t('misGrupos') }}
      </a>

      <app-pestanas-grupo [id]="id()" [grado]="clave()" />

      <div class="cabeza-pantalla">
        <hgroup>
          <h1 tabindex="-1">{{ t('titulo') }}</h1>
          @if (datos(); as d) {
            <p class="dato-suave cifras">
              {{ t('tutores.' + forma(d.tutores), { n: d.tutores }) }}
            </p>
          }
        </hgroup>
        @if (puedeEnviar()) {
          <a class="boton solo-escritorio" [routerLink]="['/grupos', id(), 'avisos', 'nuevo']">
            <app-icono nombre="mas" />
            {{ t('nuevo') }}
          </a>
        }
      </div>

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (datos(); as d) {
        @if (Number(d.tutores) === 0) {
          <section class="vacio" aria-labelledby="sin-tutores">
            <h2 id="sin-tutores">{{ t('sinTutores.titulo') }}</h2>
            <p>{{ t('sinTutores.texto') }}</p>
            <a class="boton boton--secundario" [routerLink]="['/grupos', id(), 'alumnos']">
              {{ t('sinTutores.accion') }}
            </a>
          </section>
        } @else if (d.avisos.length === 0) {
          <section class="vacio" aria-labelledby="sin-avisos">
            <h2 id="sin-avisos">{{ t('vacio.titulo') }}</h2>
            <p>{{ t('vacio.texto') }}</p>
          </section>
        } @else {
          <ol class="filas avisos">
            @for (a of d.avisos; track a.id) {
              <li>
                <a class="aviso-fila" [routerLink]="['/avisos', a.id]">
                  <span class="cabeza">
                    <span class="titulo-aviso">{{ a.titulo }}</span>
                    <span class="fecha dato-suave cifras">{{ fecha(a.creadoEn) }}</span>
                  </span>
                  <span class="lectura">
                    @if (a.urgente) {
                      <span class="etiqueta-urgente">{{ t('urgente') }}</span>
                    }
                    <span class="cifras">{{
                      t('leidoPor', { leidos: a.leidos, total: a.destinatarios })
                    }}</span>
                  </span>
                  <span class="barra" aria-hidden="true">
                    <span
                      [style.transform]="'scaleX(' + fraccion(a.leidos, a.destinatarios) + ')'"
                    ></span>
                  </span>
                  <app-icono nombre="siguiente" />
                </a>
              </li>
            }
          </ol>
        }

        @if (puedeEnviar()) {
          <div class="pie-accion solo-celular">
            <a class="boton boton--ancho" [routerLink]="['/grupos', id(), 'avisos', 'nuevo']">
              <app-icono nombre="mas" />
              {{ t('nuevo') }}
            </a>
          </div>
        }
      } @else {
        <div class="filas" role="status" [attr.aria-label]="t('cargando')">
          @for (i of [1, 2, 3]; track i) {
            <span class="esqueleto fila-esqueleto"></span>
          }
        </div>
      }
    </div>
  `,
  styles: `
    hgroup {
      display: grid;
      gap: var(--espacio-1);
    }

    .avisos {
      border-block: 1px solid var(--color-divisoria);
    }

    .aviso-fila {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      gap: var(--espacio-2) var(--espacio-3);
      align-items: center;
      padding: var(--espacio-4) var(--espacio-1);
      color: inherit;
      text-decoration: none;
    }

    .aviso-fila:focus-visible {
      border-radius: var(--radio-md);
      box-shadow: var(--foco);
    }

    .cabeza {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      justify-content: space-between;
      gap: var(--espacio-1) var(--espacio-3);
    }

    .titulo-aviso {
      font-weight: var(--peso-fuerte);
      overflow-wrap: anywhere;
    }

    .lectura {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--espacio-2);
      grid-column: 1;
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
    }

    .etiqueta-urgente {
      padding: 0.125rem var(--espacio-2);
      border-radius: var(--radio-sm);
      background: var(--color-aviso-tinte);
      color: var(--color-aviso-tinta);
      font-size: var(--texto-xs);
      font-weight: var(--peso-fuerte);
    }

    .barra {
      grid-column: 1;
      display: block;
      block-size: 0.25rem;
      max-inline-size: 16rem;
      border-radius: var(--radio-sm);
      background: var(--color-superficie-hundida);
      overflow: hidden;
    }

    .barra span {
      display: block;
      block-size: 100%;
      background: var(--grado-trazo);
      transform-origin: left;
    }

    .aviso-fila app-icono {
      grid-column: 2;
      grid-row: 1 / 4;
      color: var(--color-texto-suave);
    }

    /* Escritorio: cada aviso es un renglón de tabla; título y fecha, quién lo leyó y la barra, lado a lado. */
    @media (min-width: 64rem) {
      .aviso-fila {
        grid-template-columns: minmax(0, 1fr) 11rem 12rem auto;
        column-gap: var(--espacio-6);
        padding-inline: var(--espacio-3);
      }

      .cabeza {
        justify-content: start;
        column-gap: var(--espacio-4);
      }

      .lectura {
        grid-column: 2;
        grid-row: 1;
        justify-content: end;
      }

      .barra {
        grid-column: 3;
        grid-row: 1;
        inline-size: 100%;
      }

      .aviso-fila app-icono {
        grid-column: 4;
        grid-row: 1;
      }
    }

    .aviso-fila:active .titulo-aviso {
      color: var(--color-accion);
    }

    @media (hover: hover) and (pointer: fine) {
      .aviso-fila:hover .titulo-aviso {
        text-decoration: underline;
        text-underline-offset: 0.2em;
      }
    }

    .vacio {
      display: grid;
      justify-items: start;
      gap: var(--espacio-3);
      max-inline-size: 36rem;
    }

    .vacio h2 {
      font-size: var(--texto-lg);
    }

    .fila-esqueleto {
      display: block;
      block-size: 4.5rem;
      margin-block: var(--espacio-2);
    }
  `,
})
export class AvisosGrupo implements OnInit {
  private readonly servicio = inject(AvisosGrupoService);
  private readonly i18n = inject(TranslocoService);
  private readonly pintarPortada = controlarPortada();
  protected readonly Number = Number;
  protected readonly forma = forma;

  readonly id = input.required<string>();

  protected readonly grupo = signal<GrupoDto | null>(null);
  protected readonly datos = signal<AvisosDto | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly clave = computed(() => {
    const g = this.grupo();
    return g ? claveGrado(g.nivel, g.grado) : undefined;
  });
  /** RN-071: solo con tutores y si el grupo no está archivado. */
  protected readonly puedeEnviar = computed(() => {
    const d = this.datos();
    return !!d && !d.soloLectura && Number(d.tutores) > 0;
  });

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      const [grupo, datos] = await Promise.all([
        this.servicio.grupo(this.id()),
        this.servicio.avisos(this.id()),
      ]);
      this.pintarPortada(portadaDeGrupo(grupo));
      this.grupo.set(grupo);
      this.datos.set(datos);
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  protected fecha(instante: string): string {
    return fechaAviso(instante);
  }

  protected fraccion(leidos: number | string, total: number | string): number {
    return fraccionLeidos(leidos, total);
  }
}
