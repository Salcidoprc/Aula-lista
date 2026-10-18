import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { MisHijosDto } from '../api/models/mis-hijos-dto';
import { fechaAviso } from '../shared/fechas';
import { controlarPortada } from '../shared/portada/portada.service';
import { Tapa } from '../shared/tapa';
import {
  calificacion,
  fechaLarga,
  grupoDelHijo,
  nombreCompleto,
  primerNombre,
  destinoDelEnlace,
  tokenDelFragmento,
} from './portal';
import { PortalService } from './portal.service';

/**
 * "Mis hijos" (SPEC-06, pantalla 1). Abre con el enlace del tutor (`/tutor#token`): canjea el token por la sesión y
 * lo borra de la barra de direcciones. Con un solo hijo pasa directo a su detalle. Cada hijo es una tapa del color
 * de su grado, como los grupos del docente.
 */
@Component({
  selector: 'app-mis-hijos',
  imports: [TranslocoDirective, RouterLink, Tapa],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="pantalla pantalla--ancha" *transloco="let t; prefix: 'portal'">
      <h1>{{ t('misHijos') }}</h1>
      @switch (estado()) {
        @case ('cargando') {
          <ul class="filas" role="status" [attr.aria-label]="t('cargando')">
            @for (i of [1, 2]; track i) {
              <li class="hijo hijo--esqueleto" aria-hidden="true">
                <span class="esqueleto tapa-esqueleto"></span>
                <span class="esqueleto linea-esqueleto"></span>
              </li>
            }
          </ul>
        }
        @case ('error') {
          <div class="aviso aviso--error" role="alert">
            <p>{{ t('errorRed') }}</p>
            <button class="boton boton--secundario" type="button" (click)="cargar()">
              {{ t('reintentar') }}
            </button>
          </div>
        }
        @case ('vacio') {
          <p class="vacio">{{ t('sinHijos') }}</p>
        }
        @case ('listo') {
          <ul class="filas lista">
            @for (hijo of datos()!.hijos; track hijo.id) {
              <li>
                <a class="hijo" [routerLink]="['/tutor/alumnos', hijo.id]">
                  <app-tapa [nivel]="hijo.nivel" [grado]="hijo.grado" />
                  <span class="datos">
                    <span class="nombre">{{ nombre(hijo) }}</span>
                    <span class="etiqueta">
                      <span class="cifras">{{ grupo(hijo) }}</span>
                      @if (hijo.docente) {
                        · {{ hijo.docente }}
                      }
                    </span>
                    @if (hijo.dadoDeBaja) {
                      <span class="etiqueta">{{ t('dadoDeBaja') }}</span>
                    }
                  </span>
                  @if (hijo.ultimaBoleta; as b) {
                    <span class="boleta">
                      <span class="final cifras">{{
                        final(b.calificacion, b.decimalesFinal)
                      }}</span>
                      <span class="etiqueta">{{ b.unidad }}</span>
                      <span class="etiqueta cifras">{{ fecha(b.publicadaEn) }}</span>
                    </span>
                  } @else {
                    <span class="boleta boleta--sin etiqueta">{{ t('sinBoletas') }}</span>
                  }
                  @if (hijo.ultimoAviso; as a) {
                    <span class="ultimo-aviso" [class.sin-leer]="!a.leido">
                      @if (!a.leido) {
                        <span class="punto" aria-hidden="true"></span>
                        <span class="solo-lector">{{ t('nuevo') }}:</span>
                      }
                      <span class="texto-aviso">{{
                        t('ultimoAviso', { titulo: a.titulo, fecha: fechaAviso(a.creadoEn) })
                      }}</span>
                    </span>
                  }
                </a>
              </li>
            }
          </ul>
          <a class="enlace enlace--solo" routerLink="/tutor/notificaciones">
            {{ t('notificaciones.enlace') }}
          </a>
        }
      }
    </section>
  `,
  styles: `
    .lista {
      max-width: var(--ancho-lectura);
    }

    /* La fila entera es el objetivo táctil: tapa, datos del hijo y su boleta más reciente. */
    .hijo {
      display: grid;
      grid-template-columns: auto minmax(0, 1fr) auto;
      align-items: center;
      gap: var(--espacio-3) var(--espacio-4);
      min-height: 5rem;
      padding: var(--espacio-3) 0;
      color: inherit;
      text-decoration: none;
      border-radius: var(--radio-lg);
      transition: background-color var(--dur-small) var(--ease-out);
    }

    a.hijo:active {
      background: var(--color-superficie-hundida);
    }

    .datos {
      display: grid;
      gap: var(--espacio-1);
      min-width: 0;
    }

    .nombre {
      font-size: var(--texto-lg);
      font-weight: var(--peso-fuerte);
      line-height: var(--interlineado-ajustado);
      overflow-wrap: anywhere;
    }

    .etiqueta {
      color: var(--color-texto-suave);
      font-size: var(--texto-xs);
      font-weight: var(--peso-medio);
    }

    .boleta {
      display: grid;
      justify-items: end;
      text-align: end;
    }

    .final {
      font-size: var(--texto-xl);
      font-weight: var(--peso-fuerte);
      line-height: 1;
    }

    /* El último aviso ocupa todo el ancho bajo los datos: el título se lee aunque la boleta esté al lado. */
    .ultimo-aviso {
      grid-column: 2 / -1;
      display: flex;
      align-items: center;
      gap: var(--espacio-2);
      min-width: 0;
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
    }

    .ultimo-aviso.sin-leer {
      color: var(--color-texto);
      font-weight: var(--peso-medio);
    }

    .texto-aviso {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .punto {
      flex: none;
      inline-size: 0.5rem;
      block-size: 0.5rem;
      border-radius: 50%;
      background: var(--color-accion);
    }

    .boleta--sin {
      max-width: 7rem;
    }

    /* En 320 px o con letra grande la boleta baja bajo los datos, alineada con ellos. */
    @media (max-width: 23.99rem) {
      .hijo {
        grid-template-columns: auto minmax(0, 1fr);
      }

      .boleta {
        grid-column: 2;
        justify-items: start;
        text-align: start;
      }
    }

    .tapa-esqueleto {
      width: 3.5rem;
      height: 4rem;
    }

    .linea-esqueleto {
      width: 10rem;
      height: 1.25rem;
      grid-column: 2 / -1;
    }

    .vacio {
      color: var(--color-texto-suave);
      max-width: var(--ancho-lectura);
    }

    a.hijo:focus-visible {
      box-shadow: var(--foco);
    }

    @media (hover: hover) and (pointer: fine) {
      a.hijo:hover .nombre {
        text-decoration: underline;
        text-underline-offset: 0.2em;
      }
    }
  `,
})
export class MisHijos implements OnInit {
  private readonly portal = inject(PortalService);
  private readonly router = inject(Router);
  private readonly i18n = inject(TranslocoService);
  private readonly portada = controlarPortada();
  private destino: { alumno: string; ver: string | null } | null = null;

  protected readonly datos = signal<MisHijosDto | null>(null);
  protected readonly estado = signal<'cargando' | 'error' | 'vacio' | 'listo'>('cargando');
  protected readonly nombre = nombreCompleto;
  protected readonly grupo = grupoDelHijo;
  protected readonly final = calificacion;
  protected readonly fecha = fechaLarga;
  protected readonly fechaAviso = fechaAviso;

  async ngOnInit(): Promise<void> {
    // El token llega en el fragmento para no quedar en bitácoras; se quita de la barra en cuanto se canjea.
    const token = tokenDelFragmento(location.hash);
    // El enlace de un aviso o una boleta (SPEC-07) dice a qué hijo y pestaña ir: ?alumno=…&ver=avisos.
    this.destino = destinoDelEnlace(location.search);
    if (location.hash || location.search) {
      history.replaceState(history.state, '', location.pathname);
    }
    if (token) {
      try {
        await this.portal.canjear(token);
      } catch (error) {
        if (!this.portal.manejarSinSesion(error)) {
          this.estado.set('error');
        }
        return;
      }
    }
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.estado.set('cargando');
    try {
      const datos = await this.portal.misHijos();
      const destino = this.destino;
      this.destino = null;
      const elegido =
        datos.hijos.find((h) => h.id === destino?.alumno) ??
        (datos.hijos.length === 1 ? datos.hijos[0] : null);
      if (elegido) {
        await this.router.navigate(['/tutor/alumnos', elegido.id], {
          replaceUrl: true,
          queryParams: destino?.ver ? { ver: destino.ver } : undefined,
        });
        return;
      }
      this.datos.set(datos);
      this.portada({ titulo: this.saludo(datos.nombre) });
      this.estado.set(datos.hijos.length === 0 ? 'vacio' : 'listo');
    } catch (error) {
      if (!this.portal.manejarSinSesion(error)) {
        this.estado.set('error');
      }
    }
  }

  private saludo(nombre: string | null | undefined): string {
    const primero = primerNombre(nombre);
    return primero
      ? this.i18n.translate('portal.hola', { nombre: primero })
      : this.i18n.translate('portal.holaSinNombre');
  }
}
