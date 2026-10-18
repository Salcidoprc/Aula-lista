import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { AvisoTutorDto } from '../api/models/aviso-tutor-dto';
import { DetalleHijoDto } from '../api/models/detalle-hijo-dto';
import { HijoDto } from '../api/models/hijo-dto';
import { claveGrado } from '../shared/grados';
import { forma } from '../shared/plural';
import { controlarPortada } from '../shared/portada/portada.service';
import {
  calificacion,
  esCorregida,
  fechaCorta,
  enOrdenDelCiclo,
  etiquetaHijo,
  fechaLarga,
  grupoDelHijo,
  nombreCompleto,
  peso,
  transicionBoleta,
} from './portal';
import { PortalService } from './portal.service';

type Vista = 'boletas' | 'calificaciones' | 'avisos';
const VISTAS: readonly Vista[] = ['boletas', 'calificaciones', 'avisos'];

/**
 * Detalle del hijo (SPEC-06, pantalla 2): pestañas Boletas y Calificaciones, solo con lo publicado (RN-064), y
 * Avisos del grupo (SPEC-07, RF-063): abrirla los marca leídos (RN-075). Con
 * varios hijos, arriba se cambia de hijo; la marca del elegido se desliza con la transición de pantalla (catálogo:
 * "cambiar entre hijos"). La portada toma el color y el número de su grado.
 */
@Component({
  selector: 'app-detalle-hijo',
  imports: [TranslocoDirective, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="pantalla pantalla--ancha" *transloco="let t; prefix: 'portal'">
      @if (hermanos().length > 1) {
        <nav class="hijos" [attr.aria-label]="t('cambiarHijo')">
          @for (h of hermanos(); track h.id) {
            <a
              class="chip"
              [class.activo]="h.id === id()"
              [routerLink]="['/tutor/alumnos', h.id]"
              [attr.aria-current]="h.id === id() ? 'page' : null"
              >{{ etiqueta(h) }}</a
            >
          }
        </nav>
      }

      @switch (estado()) {
        @case ('cargando') {
          <div class="esqueletos" role="status" [attr.aria-label]="t('cargando')">
            <span class="esqueleto titulo-esqueleto"></span>
            <span class="esqueleto tarjeta-esqueleto"></span>
            <span class="esqueleto tarjeta-esqueleto"></span>
          </div>
        }
        @case ('error') {
          <div class="aviso aviso--error" role="alert">
            <p>{{ t('errorRed') }}</p>
            <button class="boton boton--secundario" type="button" (click)="cargar(id())">
              {{ t('reintentar') }}
            </button>
          </div>
        }
        @case ('listo') {
          @let d = detalle()!;
          <hgroup class="cabeza">
            <h1>{{ nombre(d.hijo) }}</h1>
            @if (d.hijo.dadoDeBaja) {
              <p class="etiqueta">{{ t('dadoDeBajaDetalle') }}</p>
            }
          </hgroup>

          <div
            class="pestanas"
            role="tablist"
            [attr.aria-label]="t('secciones')"
            [style.--indice]="indice()"
          >
            @for (v of vistas; track v) {
              <button
                [id]="'pestana-' + v"
                role="tab"
                type="button"
                [attr.aria-selected]="vista() === v"
                [attr.aria-controls]="vista() === v ? 'panel-' + v : null"
                [attr.tabindex]="vista() === v ? 0 : -1"
                (click)="vista.set(v)"
                (keydown)="mover($event)"
              >
                {{ t(v) }}
                @if (v === 'avisos' && sinLeer() > 0) {
                  <span class="sin-leer cifras">
                    <span aria-hidden="true">{{ sinLeer() }}</span>
                    <span class="solo-lector">{{
                      t('avisosSinLeer.' + forma(sinLeer()), { n: sinLeer() })
                    }}</span>
                  </span>
                }
              </button>
            }
            <span class="indicador" aria-hidden="true"></span>
          </div>

          @if (vista() === 'boletas') {
            <div id="panel-boletas" class="panel" role="tabpanel" aria-labelledby="pestana-boletas">
              @if (d.boletas.length) {
                <ul class="periodos">
                  @for (b of d.boletas; track b.id) {
                    <li>
                      <a
                        class="periodo"
                        [routerLink]="['/tutor/alumnos', d.hijo.id, 'boletas', b.id]"
                        [style.view-transition-name]="transicion(b.id)"
                        style="view-transition-class: boleta"
                      >
                        <span class="periodo-datos">
                          <span class="unidad">{{ b.unidad }}</span>
                          <span class="etiqueta">{{ t('ciclo', { ciclo: b.ciclo }) }}</span>
                          <span class="etiqueta">
                            {{ t('publicada', { fecha: fecha(b.publicadaEn) }) }}
                          </span>
                          @if (corregida(b.version)) {
                            <span class="corregida">{{
                              t('versionCorregida', { fecha: fechaCorta(b.publicadaEn) })
                            }}</span>
                          }
                        </span>
                        <span class="final cifras">{{
                          final(b.calificacion, b.decimalesFinal)
                        }}</span>
                      </a>
                    </li>
                  }
                </ul>
              } @else {
                <p class="vacio">{{ t('sinBoletasDetalle') }}</p>
              }
            </div>
          } @else if (vista() === 'avisos') {
            <div id="panel-avisos" class="panel" role="tabpanel" aria-labelledby="pestana-avisos">
              @switch (estadoAvisos()) {
                @case ('cargando') {
                  <div class="esqueletos" role="status" [attr.aria-label]="t('cargando')">
                    <span class="esqueleto tarjeta-esqueleto"></span>
                  </div>
                }
                @case ('error') {
                  <div class="aviso aviso--error" role="alert">
                    <p>{{ t('errorRed') }}</p>
                    <button class="boton boton--secundario" type="button" (click)="cargarAvisos()">
                      {{ t('reintentar') }}
                    </button>
                  </div>
                }
                @default {
                  @if (avisos().length) {
                    <ul class="filas avisos-hijo">
                      @for (a of avisos(); track a.id) {
                        <li>
                          <article [attr.aria-labelledby]="'aviso-' + a.id">
                            <div class="aviso-cabeza">
                              @if (a.urgente) {
                                <span class="etiqueta-urgente">{{ t('urgente') }}</span>
                              }
                              @if (!a.leidoEn) {
                                <span class="nuevo">{{ t('nuevo') }}</span>
                              }
                            </div>
                            <h2 class="aviso-titulo" [id]="'aviso-' + a.id">{{ a.titulo }}</h2>
                            <p class="etiqueta cifras">
                              {{ fecha(a.creadoEn) }}
                              @if (a.docente) {
                                · {{ a.docente }}
                              }
                            </p>
                            <p class="aviso-cuerpo">{{ a.cuerpo }}</p>
                          </article>
                        </li>
                      }
                    </ul>
                  } @else {
                    <p class="vacio">{{ t('sinAvisos') }}</p>
                  }
                  <a class="enlace enlace--solo notificaciones" routerLink="/tutor/notificaciones">
                    {{ t('notificaciones.enlace') }}
                  </a>
                }
              }
            </div>
          } @else {
            <div
              id="panel-calificaciones"
              class="panel"
              role="tabpanel"
              aria-labelledby="pestana-calificaciones"
            >
              @if (d.boletas.length) {
                <ul class="filas calificaciones">
                  @for (b of ciclo(); track b.id) {
                    <li>
                      <div class="renglon">
                        <h2 class="unidad">{{ b.unidad }}</h2>
                        <span class="final cifras">{{
                          final(b.calificacion, b.decimalesFinal)
                        }}</span>
                      </div>
                      <table class="rubros">
                        <caption class="solo-lector">
                          {{
                            t('rubrosDe', { unidad: b.unidad })
                          }}
                        </caption>
                        <thead class="solo-lector">
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
                              <td class="cifras peso">{{ porcentaje(r.peso) }}</td>
                              <td class="cifras">{{ final(r.promedio, 2) }}</td>
                            </tr>
                          }
                        </tbody>
                      </table>
                    </li>
                  }
                </ul>
              } @else {
                <p class="vacio">{{ t('sinBoletasDetalle') }}</p>
              }
            </div>
          }
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

    /* Cambiar de hijo: chips que se deslizan solos en 320 px; la marca del activo viaja con la transición. */
    .hijos {
      display: flex;
      gap: var(--espacio-2);
      overflow-x: auto;
      overscroll-behavior-x: contain;
      scrollbar-width: none;
      margin-block-end: calc(-1 * var(--espacio-2));
      view-transition-name: hijos;
    }

    .chip {
      position: relative;
      isolation: isolate;
      display: inline-flex;
      align-items: center;
      flex: none;
      min-height: var(--tactil-minimo);
      padding: 0 var(--espacio-4);
      border-radius: var(--radio-pleno);
      color: var(--color-texto-suave);
      font-weight: var(--peso-medio);
      text-decoration: none;
      box-shadow: inset 0 0 0 1px var(--color-divisoria);
    }

    .chip.activo {
      color: var(--grado-tinta);
      box-shadow: none;
    }

    .chip.activo::before {
      content: '';
      position: absolute;
      inset: 0;
      z-index: -1;
      border-radius: inherit;
      background: var(--grado-tinte);
      view-transition-name: hijo-activo;
    }

    .chip:active:not(.activo) {
      background: var(--color-superficie-hundida);
    }

    .chip:focus-visible {
      box-shadow: var(--foco);
    }

    .cabeza {
      display: grid;
      gap: var(--espacio-1);
    }

    .etiqueta {
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
    }

    /* Pestañas de ancho parejo: la raya del activo se desliza con transform (catálogo de la regla 07). */
    .pestanas {
      position: relative;
      display: grid;
      grid-auto-columns: minmax(0, 1fr);
      grid-auto-flow: column;
    }

    .pestanas [role='tab'],
    .pestanas [role='tab'][aria-selected='true'] {
      box-shadow: none;
    }

    .pestanas [role='tab'][aria-selected='true'] {
      color: var(--grado-tinta);
    }

    .pestanas [role='tab']:focus-visible {
      box-shadow: var(--foco);
    }

    .indicador {
      position: absolute;
      inset-block-end: -1px;
      inset-inline-start: 0;
      width: calc(100% / 3);
      height: 3px;
      border-radius: var(--radio-sm) var(--radio-sm) 0 0;
      background: var(--grado);
      transform: translateX(calc(var(--indice, 0) * 100%));
      transition: transform var(--dur-icono) var(--ease-out);
    }

    :host-context([dir='rtl']) .indicador {
      transform: translateX(calc(var(--indice, 0) * -100%));
    }

    .panel {
      animation: fundido var(--dur-small) var(--ease-out);
    }

    @keyframes fundido {
      from {
        opacity: 0;
      }
    }

    .periodos {
      display: grid;
      gap: var(--espacio-3);
      margin: 0;
      padding: 0;
      list-style: none;
    }

    /* Cada periodo es una tarjeta que se expande hacia su boleta (View Transition compartida). */
    .periodo {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      align-items: center;
      gap: var(--espacio-4);
      padding: var(--espacio-4);
      border-radius: var(--radio-lg);
      background: var(--color-superficie);
      box-shadow: inset 0 0 0 1px var(--color-divisoria);
      color: inherit;
      text-decoration: none;
      transition: transform var(--dur-press) var(--ease-out);
    }

    .periodo:active {
      transform: scale(var(--escala-presion));
    }

    .periodo:focus-visible {
      box-shadow: var(--foco);
    }

    .periodo-datos {
      display: grid;
      gap: var(--espacio-1);
      min-width: 0;
    }

    .unidad {
      font-size: var(--texto-lg);
      font-weight: var(--peso-fuerte);
      line-height: var(--interlineado-ajustado);
      overflow-wrap: anywhere;
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

    .final {
      font-size: var(--texto-2xl);
      font-weight: var(--peso-fuerte);
      line-height: 1;
      color: var(--grado-tinta);
    }

    .calificaciones > li {
      display: grid;
      gap: var(--espacio-2);
      padding-block: var(--espacio-4);
    }

    .renglon {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      gap: var(--espacio-4);
    }

    .renglon .final {
      font-size: var(--texto-xl);
    }

    .rubros {
      width: 100%;
      border-collapse: collapse;
      font-size: var(--texto-sm);
    }

    .rubros th,
    .rubros td {
      padding: var(--espacio-1) 0;
      text-align: end;
      font-weight: var(--peso-normal);
    }

    .rubros th[scope='row'] {
      text-align: start;
      color: var(--color-texto-suave);
      overflow-wrap: anywhere;
    }

    .rubros .peso {
      width: 4.5rem;
      color: var(--color-texto-suave);
    }

    .rubros td:last-child {
      width: 3.5rem;
      font-weight: var(--peso-medio);
    }

    .vacio {
      color: var(--color-texto-suave);
    }

    .sin-leer {
      display: inline-grid;
      place-items: center;
      min-inline-size: 1.25rem;
      block-size: 1.25rem;
      margin-inline-start: var(--espacio-1);
      padding-inline: 0.3rem;
      border-radius: var(--radio-pleno);
      background: var(--grado);
      color: var(--color-sobre-grado);
      font-size: var(--texto-xs);
      font-weight: var(--peso-fuerte);
    }

    .avisos-hijo > li {
      padding-block: var(--espacio-4);
    }

    .avisos-hijo article {
      display: grid;
      gap: var(--espacio-1);
    }

    .aviso-cabeza {
      display: flex;
      gap: var(--espacio-2);
    }

    .aviso-cabeza:empty {
      display: none;
    }

    .etiqueta-urgente,
    .nuevo {
      padding: 0.125rem var(--espacio-2);
      border-radius: var(--radio-sm);
      font-size: var(--texto-xs);
      font-weight: var(--peso-fuerte);
    }

    .etiqueta-urgente {
      background: var(--color-aviso-tinte);
      color: var(--color-aviso-tinta);
    }

    .nuevo {
      background: var(--grado-tinte);
      color: var(--grado-tinta);
    }

    .aviso-titulo {
      font-size: var(--texto-lg);
      overflow-wrap: anywhere;
    }

    /* Son cortos y se leen completos: con los saltos de línea del docente. */
    .aviso-cuerpo {
      margin-block-start: var(--espacio-1);
      line-height: var(--interlineado);
      white-space: pre-line;
      overflow-wrap: anywhere;
    }

    .notificaciones {
      margin-block-start: var(--espacio-2);
    }

    .esqueletos {
      display: grid;
      gap: var(--espacio-3);
    }

    .titulo-esqueleto {
      width: 14rem;
      height: 2rem;
    }

    .tarjeta-esqueleto {
      height: 6rem;
      border-radius: var(--radio-lg);
    }

    @media (prefers-reduced-motion: reduce) {
      .indicador {
        transition: none;
      }
    }

    @media (hover: hover) and (pointer: fine) {
      .chip:not(.activo):hover {
        color: var(--color-texto);
      }

      .periodo:hover .unidad {
        text-decoration: underline;
        text-underline-offset: 0.2em;
      }
    }
  `,
})
export class DetalleHijo {
  private readonly portal = inject(PortalService);
  private readonly portada = controlarPortada();

  readonly id = input.required<string>();
  /** `?ver=avisos`: el push y el enlace del aviso abren directo en la pestaña Avisos. */
  readonly ver = input<string>();

  protected readonly vistas = VISTAS;
  protected readonly vista = signal<Vista>('boletas');
  protected readonly indice = computed(() => VISTAS.indexOf(this.vista()));
  protected readonly detalle = signal<DetalleHijoDto | null>(null);
  protected readonly hermanos = signal<readonly HijoDto[]>([]);
  protected readonly estado = signal<'cargando' | 'error' | 'listo'>('cargando');
  protected readonly ciclo = computed(() => enOrdenDelCiclo(this.detalle()?.boletas ?? []));
  protected readonly avisos = signal<readonly AvisoTutorDto[]>([]);
  protected readonly estadoAvisos = signal<'cargando' | 'error' | 'listo'>('cargando');
  private avisosDe: string | null = null;
  /** El número de la pestaña: se apaga al abrirla, aunque cada aviso conserve su "Nuevo" mientras se lee. */
  protected readonly sinLeer = signal(0);
  protected readonly forma = forma;

  protected readonly nombre = nombreCompleto;
  protected readonly grupo = grupoDelHijo;
  protected readonly final = calificacion;
  protected readonly fecha = fechaLarga;
  protected readonly porcentaje = peso;
  protected readonly transicion = transicionBoleta;
  protected readonly corregida = esCorregida;
  protected readonly fechaCorta = fechaCorta;

  constructor() {
    // Cambiar de hijo reutiliza la pantalla: cada id nuevo se carga y vuelve a la pestaña Boletas.
    effect(() => {
      const id = this.id();
      untracked(() => void this.cargar(id));
    });
    effect(() => {
      if (this.vista() === 'avisos' && this.estado() === 'listo') {
        untracked(() => void this.cargarAvisos());
      }
    });
  }

  protected etiqueta(hijo: HijoDto): string {
    return etiquetaHijo(hijo, this.hermanos());
  }

  protected async cargar(id: string): Promise<void> {
    this.vista.set(this.ver() === 'avisos' ? 'avisos' : 'boletas');
    this.avisosDe = null;
    this.avisos.set([]);
    this.estadoAvisos.set('cargando');
    if (this.detalle()?.hijo.id !== id) {
      this.estado.set('cargando');
    }
    try {
      const [detalle, hijos] = await Promise.all([this.portal.detalle(id), this.portal.misHijos()]);
      if (id !== this.id()) {
        return;
      }
      this.hermanos.set(hijos.hijos);
      this.detalle.set(detalle);
      this.sinLeer.set(Number(detalle.hijo.avisosSinLeer));
      const h = detalle.hijo;
      // Como en la app del docente: la portada es el grupo (su color y su número) y el título, la persona.
      this.portada({
        titulo: grupoDelHijo(h),
        subtitulo: h.docente ?? undefined,
        numero: String(Number(h.grado)),
        grado: claveGrado(h.nivel, h.grado),
      });
      this.estado.set('listo');
    } catch (error) {
      if (!this.portal.manejarSinSesion(error)) {
        this.estado.set('error');
      }
    }
  }

  protected async cargarAvisos(): Promise<void> {
    const id = this.id();
    if (this.avisosDe === id) {
      return;
    }
    this.avisosDe = id;
    this.estadoAvisos.set('cargando');
    try {
      const avisos = await this.portal.avisos(id);
      if (id !== this.id()) {
        return;
      }
      this.avisos.set(avisos);
      this.estadoAvisos.set('listo');
    } catch (error) {
      if (id === this.id()) {
        this.avisosDe = null;
        if (!this.portal.manejarSinSesion(error)) {
          this.estadoAvisos.set('error');
        }
      }
      return;
    }
    if (this.avisos().some((a) => !a.leidoEn)) {
      try {
        await this.portal.leerAvisos(id);
        this.sinLeer.set(0);
      } catch (error) {
        // Ya los leyó en pantalla; la próxima vez que abra la pestaña se vuelve a marcar.
        this.portal.manejarSinSesion(error);
      }
    }
  }

  protected mover(evento: KeyboardEvent): void {
    const actual = this.indice();
    const destinos: Record<string, number> = {
      ArrowLeft: (actual + VISTAS.length - 1) % VISTAS.length,
      ArrowRight: (actual + 1) % VISTAS.length,
      Home: 0,
      End: VISTAS.length - 1,
    };
    const siguiente = destinos[evento.key];
    if (siguiente === undefined) {
      return;
    }
    evento.preventDefault();
    this.vista.set(VISTAS[siguiente]);
    document.getElementById(`pestana-${VISTAS[siguiente]}`)?.focus();
  }
}
