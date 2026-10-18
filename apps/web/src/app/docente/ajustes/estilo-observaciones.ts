import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { Location } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { codigoDeError } from '../../core/errores/problema';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { crearEnfoque } from '../../shared/foco';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { CierreService } from '../cierre/cierre.service';

const LONGITUDES = ['breve', 'normal', 'detallada'] as const;
const PERSONAS = ['tercera', 'segunda'] as const;
const MAXIMO_EJEMPLOS = 3;
const LONGITUD_EJEMPLO = 800;

/**
 * SPEC-04, "Estilo" (RF-043): longitud, persona gramatical y hasta 3 ejemplos propios para que las observaciones
 * suenen como el docente. Los ejemplos viajan a la IA: se pide no poner nombres, y el servidor quita los de su
 * grupo antes de enviarlos (RN-019).
 */
@Component({
  selector: 'app-estilo-observaciones',
  imports: [TranslocoDirective, RouterLink, Icono, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pantalla" *transloco="let t; prefix: 'estilo'">
      <a class="volver" routerLink="/ajustes" (click)="regresar($event)">
        <app-icono nombre="volver" />
        {{ t('volver') }}
      </a>

      <hgroup>
        <h1 tabindex="-1">{{ t('titulo') }}</h1>
        <p class="dato-suave">{{ t('subtitulo') }}</p>
      </hgroup>

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (!listo()) {
        <div role="status" [attr.aria-label]="t('cargando')">
          <span class="esqueleto bloque-esqueleto" aria-hidden="true"></span>
        </div>
      } @else {
        <form class="formulario" (submit)="guardar($event)">
          <fieldset class="eleccion">
            <legend>{{ t('longitud') }}</legend>
            @for (l of longitudes; track l) {
              <label>
                <input
                  type="radio"
                  name="longitud"
                  [value]="l"
                  [checked]="longitud() === l"
                  (change)="longitud.set(l)"
                />
                <span>
                  {{ t('longitudes.' + l) }}
                  <span class="detalle cifras">{{ t('rangos.' + l) }}</span>
                </span>
              </label>
            }
          </fieldset>

          <fieldset class="eleccion">
            <legend>{{ t('persona') }}</legend>
            @for (p of personas; track p) {
              <label>
                <input
                  type="radio"
                  name="persona"
                  [value]="p"
                  [checked]="persona() === p"
                  (change)="persona.set(p)"
                />
                <span>
                  {{ t('personas.' + p) }}
                  <span class="detalle">{{ t('muestras.' + p) }}</span>
                </span>
              </label>
            }
          </fieldset>

          <section class="ejemplos" aria-labelledby="ejemplos-titulo">
            <h2 id="ejemplos-titulo">{{ t('ejemplos.titulo') }}</h2>
            <p class="dato-suave">{{ t('ejemplos.ayuda') }}</p>
            <p class="aviso aviso--pendiente">{{ t('ejemplos.sinNombres') }}</p>
            @for (e of ejemplos(); track $index; let i = $index) {
              <div class="campo">
                <div class="cabeza-ejemplo">
                  <label [for]="'ejemplo-' + i">{{
                    t('ejemplos.numero', { numero: i + 1 })
                  }}</label>
                  <button
                    class="enlace peligro"
                    type="button"
                    [attr.aria-label]="t('ejemplos.quitarNumero', { numero: i + 1 })"
                    (click)="quitar(i)"
                  >
                    {{ t('ejemplos.quitar') }}
                  </button>
                </div>
                <textarea
                  [id]="'ejemplo-' + i"
                  rows="4"
                  [maxLength]="longitudEjemplo"
                  [value]="e"
                  (input)="cambiar(i, $event)"
                ></textarea>
                <p class="ayuda cifras">
                  {{ t('ejemplos.contador', { usados: e.length, maximo: longitudEjemplo }) }}
                </p>
              </div>
            }
            @if (ejemplos().length < maximoEjemplos) {
              <button class="boton boton--secundario agregar" type="button" (click)="agregar()">
                <app-icono nombre="mas" />
                {{ t('ejemplos.agregar') }}
              </button>
            }
          </section>

          @if (errorGuardar(); as codigo) {
            <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
          }
          <div class="pie-accion">
            <button
              class="boton boton--ancho"
              type="submit"
              [disabled]="!enLinea()"
              [appOcupado]="guardando()"
            >
              {{ t('guardar') }}
            </button>
          </div>
        </form>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .eleccion {
      grid-template-columns: repeat(auto-fit, minmax(min(100%, 7rem), 1fr));
    }

    /* Angosto o con texto grande: las opciones una debajo de otra, nunca dos y una. */
    @media (max-width: 26rem) {
      .eleccion {
        grid-template-columns: minmax(0, 1fr);
      }
    }

    .eleccion label {
      min-block-size: 3.5rem;
      padding-block: var(--espacio-2);
    }

    .detalle {
      display: block;
      color: var(--color-texto-suave);
      font-size: var(--texto-xs);
      font-weight: var(--peso-normal);
    }

    .ejemplos {
      display: grid;
      justify-items: start;
      gap: var(--espacio-3);
    }

    .ejemplos > * {
      inline-size: 100%;
    }

    .ejemplos > .boton {
      inline-size: auto;
    }

    .ejemplos h2 {
      font-size: var(--texto-lg);
    }

    .cabeza-ejemplo {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--espacio-3);
    }

    .enlace {
      min-block-size: var(--tactil-minimo);
      padding: 0;
      border: 0;
      background: none;
      font: inherit;
      cursor: pointer;
    }

    .enlace.peligro {
      color: var(--color-peligro-tinta);
    }

    .bloque-esqueleto {
      display: block;
      block-size: 16rem;
    }

    @media (min-width: 64rem) {
      .pie-accion .boton--ancho {
        inline-size: auto;
      }
    }
  `,
})
export class EstiloObservaciones implements OnInit {
  private readonly servicio = inject(CierreService);
  private readonly i18n = inject(TranslocoService);
  private readonly avisos = inject(AvisosService);
  private readonly location = inject(Location);
  private readonly enfocar = crearEnfoque();
  protected readonly enLinea = this.servicio.enLinea;
  protected readonly longitudes = LONGITUDES;
  protected readonly personas = PERSONAS;
  protected readonly maximoEjemplos = MAXIMO_EJEMPLOS;
  protected readonly longitudEjemplo = LONGITUD_EJEMPLO;

  protected readonly listo = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly errorGuardar = signal<string | null>(null);
  protected readonly guardando = signal(false);
  protected readonly longitud = signal<string>('normal');
  protected readonly persona = signal<string>('tercera');
  protected readonly ejemplos = signal<readonly string[]>([]);

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      const estilo = await this.servicio.estilo();
      this.longitud.set(estilo.longitud);
      this.persona.set(estilo.persona);
      this.ejemplos.set(estilo.ejemplos);
      this.listo.set(true);
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  /** Desde el cierre se llega aquí: volver regresa a donde estaba el docente. */
  protected regresar(evento: Event): void {
    if (history.length > 1) {
      evento.preventDefault();
      this.location.back();
    }
  }

  /** El foco va al ejemplo nuevo. */
  protected agregar(): void {
    this.ejemplos.update((l) => [...l, '']);
    this.enfocar(`#ejemplo-${this.ejemplos().length - 1}`);
  }

  /** El botón se va con su ejemplo: el foco pasa al ejemplo que queda en su lugar, o a "Agregar ejemplo". */
  protected quitar(i: number): void {
    this.ejemplos.update((l) => l.filter((_, j) => j !== i));
    const quedan = this.ejemplos().length;
    this.enfocar(quedan ? `#ejemplo-${Math.min(i, quedan - 1)}` : '.agregar');
  }

  protected cambiar(i: number, evento: Event): void {
    const valor = (evento.target as HTMLTextAreaElement).value;
    this.ejemplos.update((l) => l.map((e, j) => (j === i ? valor : e)));
  }

  protected async guardar(evento: Event): Promise<void> {
    evento.preventDefault();
    this.errorGuardar.set(null);
    this.guardando.set(true);
    try {
      const estilo = await this.servicio.guardarEstilo({
        longitud: this.longitud(),
        persona: this.persona(),
        ejemplos: this.ejemplos()
          .map((e) => e.trim())
          .filter(Boolean),
      });
      this.ejemplos.set(estilo.ejemplos);
      this.avisos.mostrar(this.texto('estilo.guardado'), 'exito');
    } catch (e) {
      this.errorGuardar.set(codigoDeError(e));
    } finally {
      this.guardando.set(false);
    }
  }
}
