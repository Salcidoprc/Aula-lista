import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { forma } from '../../shared/plural';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { CicloDto } from '../../api/models/ciclo-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { codigoDeError } from '../../core/errores/problema';
import { crearEnfoque } from '../../shared/foco';
import { PerfilService } from '../../core/sesion/perfil.service';
import { nombreGrupo } from '../../shared/grados';
import { Icono } from '../../shared/iconos';
import { Tapa } from '../../shared/tapa';
import { ReenviarVerificacion } from '../cuenta/reenviar-verificacion';
import { GruposService } from '../grupos/grupos.service';

interface CicloConGrupos {
  readonly ciclo: CicloDto;
  readonly grupos: readonly GrupoDto[];
}

/**
 * "Mis grupos" (SPEC-02): cada grupo es una tapa de libro del color de su grado, agrupados por ciclo, el
 * más reciente primero. La primera vez explica los tres pasos para tener el grupo listo.
 */
@Component({
  selector: 'app-inicio',
  imports: [TranslocoDirective, RouterLink, ReenviarVerificacion, Tapa, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="pantalla pantalla--ancha" *transloco="let t; prefix: 'inicio'">
      <hgroup>
        <h1>{{ t('titulo') }}</h1>
        @if (perfil()?.nombre; as nombre) {
          <p class="entrada">{{ t('saludo', { nombre: primerNombre(nombre) }) }}</p>
        }
      </hgroup>

      @if (perfil() && !perfil()!.correoVerificado) {
        <div class="aviso aviso--pendiente">
          <p>
            <strong>{{ t('verifica.titulo') }}</strong>
            {{ t('verifica.texto', { correo: perfil()!.correo }) }}
          </p>
          <app-reenviar-verificacion />
        </div>
      }

      @switch (estado()) {
        @case ('cargando') {
          <ul class="filas" role="status" [attr.aria-label]="t('cargando')">
            @for (i of [1, 2, 3]; track i) {
              <li class="fila-grupo" aria-hidden="true">
                <span class="esqueleto tapa-esqueleto"></span>
                <span class="esqueleto linea-esqueleto"></span>
              </li>
            }
          </ul>
        }
        @case ('error') {
          <div class="aviso aviso--error" role="alert">
            <p>{{ texto('errores.' + error()) }}</p>
            <button class="boton boton--secundario" type="button" (click)="cargar()">
              {{ t('reintentar') }}
            </button>
          </div>
        }
        @case ('vacio') {
          <div class="primer-uso">
            <h2>{{ t('vacio.titulo') }}</h2>
            <ol class="pasos">
              <li>
                <strong>{{ t('vacio.paso1') }}</strong>
                <span>{{ t('vacio.paso1Texto') }}</span>
              </li>
              <li>
                <strong>{{ t('vacio.paso2') }}</strong>
                <span>{{ t('vacio.paso2Texto') }}</span>
              </li>
              <li>
                <strong>{{ t('vacio.paso3') }}</strong>
                <span>{{ t('vacio.paso3Texto') }}</span>
              </li>
            </ol>
          </div>
          <div class="pie-accion">
            <a class="boton" [routerLink]="ciclos().length ? '/grupos/nuevo' : '/ciclos/nuevo'">
              {{ ciclos().length ? t('crearPrimerGrupo') : t('empezar') }}
            </a>
          </div>
        }
        @case ('listo') {
          @for (bloque of porCiclo(); track bloque.ciclo.id) {
            <section class="ciclo" [attr.aria-labelledby]="'ciclo-' + bloque.ciclo.id">
              <div class="ciclo-cabeza">
                <h2 [id]="'ciclo-' + bloque.ciclo.id">
                  {{ t('ciclo', { nombre: bloque.ciclo.nombre }) }}
                </h2>
                <a class="enlace enlace--solo" [routerLink]="['/ciclos', bloque.ciclo.id]">
                  {{
                    t('unidades.' + forma(bloque.ciclo.unidades.length), {
                      cantidad: bloque.ciclo.unidades.length,
                    })
                  }}
                </a>
              </div>
              @if (bloque.grupos.length) {
                <ul class="filas">
                  @for (grupo of bloque.grupos; track grupo.id) {
                    <li>
                      <a class="fila-grupo" [routerLink]="['/grupos', grupo.id]">
                        <app-tapa [nivel]="grupo.nivel" [grado]="grupo.grado" />
                        <span class="texto">
                          <span class="nombre">{{ nombre(grupo) }}</span>
                          <span class="dato-suave detalle">
                            <span class="materia">{{
                              grupo.materia ?? t('nivel.' + grupo.nivel)
                            }}</span>
                            <span class="cifras cuenta">{{
                              t('alumnos.' + forma(grupo.alumnosActivos), {
                                cantidad: +grupo.alumnosActivos,
                              })
                            }}</span>
                          </span>
                        </span>
                        <app-icono nombre="siguiente" class="flecha" />
                      </a>
                    </li>
                  }
                </ul>
              } @else {
                <p class="nota">{{ t('cicloSinGrupos') }}</p>
              }
            </section>
          }

          @if (archivados(); as lista) {
            <section class="ciclo" aria-labelledby="archivados">
              <h2 id="archivados" tabindex="-1">{{ t('archivados') }}</h2>
              @if (lista.length) {
                <ul class="filas">
                  @for (grupo of lista; track grupo.id) {
                    <li>
                      <a class="fila-grupo archivado" [routerLink]="['/grupos', grupo.id]">
                        <app-tapa [nivel]="grupo.nivel" [grado]="grupo.grado" />
                        <span class="texto">
                          <span class="nombre">{{ nombre(grupo) }}</span>
                          <span class="dato-suave">{{ grupo.cicloNombre }}</span>
                        </span>
                        <app-icono nombre="siguiente" class="flecha" />
                      </a>
                    </li>
                  }
                </ul>
              } @else {
                <p class="nota">{{ t('sinArchivados') }}</p>
              }
            </section>
          } @else {
            <button
              class="enlace enlace--solo ver-archivados"
              type="button"
              (click)="verArchivados()"
            >
              {{ t('verArchivados') }}
            </button>
          }

          <div class="pie-accion">
            <a class="boton" routerLink="/grupos/nuevo">
              <app-icono nombre="mas" />
              {{ t('crearGrupo') }}
            </a>
          </div>
        }
      }
    </section>
  `,
  styles: `
    hgroup {
      display: grid;
      gap: var(--espacio-2);
    }

    .ciclo {
      display: grid;
      gap: var(--espacio-2);
    }

    .ciclo-cabeza {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      justify-content: space-between;
      column-gap: var(--espacio-4);
    }

    h2 {
      font-size: var(--texto-lg);
    }

    .fila-grupo {
      display: grid;
      grid-template-columns: auto minmax(0, 1fr) auto;
      align-items: center;
      gap: var(--espacio-4);
      min-height: 5rem;
      padding-block: var(--espacio-2);
      color: inherit;
      text-decoration: none;
    }

    a.fila-grupo {
      border-radius: var(--radio-lg);
    }

    a.fila-grupo:active .nombre {
      color: var(--color-accion);
    }

    @media (hover: hover) and (pointer: fine) {
      a.fila-grupo:hover .nombre {
        text-decoration: underline;
        text-underline-offset: 0.2em;
      }
    }

    .texto {
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

    /* Una materia larga se corta en una línea; la cantidad de alumnos nunca se parte. */
    .detalle {
      display: flex;
      flex-wrap: wrap;
      gap: 0.35em;
      min-width: 0;
    }

    .materia {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .materia::after {
      content: ' ·';
    }

    .cuenta {
      flex: none;
      white-space: nowrap;
    }

    .flecha {
      color: var(--color-texto-tenue);
    }

    .archivado app-tapa {
      opacity: 0.6;
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

    .nota {
      color: var(--color-texto-suave);
    }

    .ver-archivados {
      justify-self: start;
    }

    .primer-uso {
      display: grid;
      gap: var(--espacio-4);
    }

    .pasos {
      display: grid;
      gap: var(--espacio-4);
      margin: 0;
      padding: 0;
      list-style: none;
      counter-reset: paso;
    }

    .pasos li {
      display: grid;
      grid-template-columns: 2rem 1fr;
      column-gap: var(--espacio-3);
      counter-increment: paso;
    }

    .pasos li::before {
      content: counter(paso);
      grid-row: span 2;
      display: grid;
      place-items: center;
      width: 2rem;
      height: 2rem;
      border-radius: var(--radio-pleno);
      background: var(--color-accion-tinte);
      color: var(--color-accion-tinta);
      font-family: var(--fuente-cifras);
      font-weight: var(--peso-fuerte);
    }

    .pasos span {
      color: var(--color-texto-suave);
    }
  `,
})
export class Inicio implements OnInit {
  protected readonly forma = forma;
  private readonly servicio = inject(GruposService);
  private readonly i18n = inject(TranslocoService);
  private readonly avisos = inject(AvisosService);
  private readonly enfocar = crearEnfoque();
  protected readonly perfil = inject(PerfilService).perfil;

  protected readonly grupos = signal<readonly GrupoDto[] | null>(null);
  protected readonly archivados = signal<readonly GrupoDto[] | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly ciclos = computed(() => this.servicio.ciclos() ?? []);

  protected readonly estado = computed(() => {
    if (this.error()) {
      return 'error';
    }
    const grupos = this.grupos();
    if (grupos === null) {
      return 'cargando';
    }
    return grupos.length === 0 && !this.archivados()?.length ? 'vacio' : 'listo';
  });

  /** Ciclos con sus grupos, el más reciente primero; los grupos ya vienen ordenados por grado y nombre. */
  protected readonly porCiclo = computed<CicloConGrupos[]>(() => {
    const grupos = this.grupos() ?? [];
    return (
      [...this.ciclos()]
        .sort((a, b) => b.inicio.localeCompare(a.inicio))
        .map((ciclo) => ({ ciclo, grupos: grupos.filter((g) => g.cicloId === ciclo.id) }))
        // Los ciclos viejos sin grupos activos solo hacen ruido; el más reciente se queda aunque esté vacío.
        .filter((bloque, i) => i === 0 || bloque.grupos.length > 0)
    );
  });

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      const [grupos] = await Promise.all([this.servicio.listar(), this.servicio.cargarCiclos()]);
      // Sin grupos activos puede haber archivados: se muestran en lugar del primer uso.
      if (grupos.length === 0) {
        const archivados = (await this.servicio.listar(true)).filter((g) => g.archivadoEn);
        if (archivados.length) {
          this.archivados.set(archivados);
        }
      }
      this.grupos.set(grupos);
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected async verArchivados(): Promise<void> {
    try {
      const todos = await this.servicio.listar(true);
      this.archivados.set(todos.filter((g) => g.archivadoEn));
      // El enlace que tenía el foco se reemplaza por la sección.
      this.enfocar('#archivados');
    } catch (e) {
      // Un aviso y no el error de la pantalla: los grupos activos siguen a la vista.
      this.avisos.mostrar(this.texto('errores.' + codigoDeError(e)), 'error');
    }
  }

  /** El saludo usa solo el primer nombre: "Hola, Rosa." y no el nombre completo. */
  protected primerNombre(nombre: string): string {
    return nombre.trim().split(/\s+/)[0];
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  protected nombre(grupo: GrupoDto): string {
    return nombreGrupo(grupo.grado, grupo.nombre);
  }
}
