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
import { forma } from '../../shared/plural';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { Api } from '../../api/api';
import { CicloDto } from '../../api/models/ciclo-dto';
import { crearGrupo } from '../../api/fn/grupos/crear-grupo';
import { crearGruposLote } from '../../api/fn/grupos/crear-grupos-lote';
import { codigoDeError } from '../../core/errores/problema';
import { PerfilService } from '../../core/sesion/perfil.service';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { PRIMER_ERROR, crearEnfoque } from '../../shared/foco';
import { GRADOS, Nivel, nombreGrupo } from '../../shared/grados';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { Tapa } from '../../shared/tapa';
import { GruposService } from './grupos.service';

const MAXIMO_LOTE = 15;
const LONGITUD_NOMBRE = 40;

/** Nombres escritos para "Crear varios": separados por comas, punto y coma o saltos de línea. */
export function separarNombres(texto: string): string[] {
  return texto
    .split(/[,;\n]/)
    .map((n) => n.trim())
    .filter((n) => n.length > 0);
}

/** RN-021: de 1 a 15 nombres, sin repetir y de 40 caracteres o menos. */
export function errorDeNombres(nombres: readonly string[]): string | null {
  if (nombres.length === 0) {
    return 'nombreRequerido';
  }
  if (nombres.length > MAXIMO_LOTE) {
    return 'demasiados';
  }
  if (nombres.some((n) => n.length > LONGITUD_NOMBRE)) {
    return 'nombreLargo';
  }
  const vistos = new Set(nombres.map((n) => n.toLocaleLowerCase('es-MX')));
  return vistos.size === nombres.length ? null : 'repetidos';
}

/**
 * SPEC-02, "Crear grupo": nivel, grado, materia (solo secundaria), ciclo y nombre. Con "Crear varios" el
 * docente escribe A, B, C y se crean todos con los mismos datos (RF-020, RN-021). Mientras escribe ve las
 * tapas de los grupos que va a tener.
 */
@Component({
  selector: 'app-crear-grupo',
  imports: [TranslocoDirective, RouterLink, Tapa, Icono, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pantalla pantalla--ancha" *transloco="let t; prefix: 'crearGrupo'">
      <a class="volver" routerLink="/">
        <app-icono nombre="volver" />
        {{ t('misGrupos') }}
      </a>
      <h1>{{ varios() ? t('tituloVarios') : t('titulo') }}</h1>

      @if (errorCarga(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ errorTexto(codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="ngOnInit()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (ciclos().length === 0) {
        <p class="aviso" role="status">{{ t('cargando') }}</p>
      } @else {
        <form class="formulario crear" (submit)="$event.preventDefault(); crear()" novalidate>
          <div class="formulario campos">
            <fieldset class="eleccion">
              <legend>{{ t('nivel') }}</legend>
              @for (opcion of niveles; track opcion) {
                <label>
                  <input
                    type="radio"
                    name="nivel"
                    [value]="opcion"
                    [checked]="nivel() === opcion"
                    (change)="cambiarNivel(opcion)"
                  />
                  {{ t('niveles.' + opcion) }}
                </label>
              }
            </fieldset>

            <fieldset class="eleccion grados">
              <legend>{{ t('grado') }}</legend>
              @for (g of grados(); track g) {
                <label>
                  <input
                    type="radio"
                    name="grado"
                    [value]="g"
                    [checked]="grado() === g"
                    (change)="grado.set(g)"
                  />
                  <app-tapa [nivel]="nivel()" [grado]="g" tamano="chica" />
                  <span class="cifras">{{ g }}°</span>
                </label>
              }
            </fieldset>

            @if (nivel() === 'secundaria') {
              <div class="campo">
                <label for="grupo-materia">{{ t('materia') }}</label>
                <input
                  id="grupo-materia"
                  name="materia"
                  list="materias"
                  maxlength="60"
                  autocomplete="off"
                  [value]="materia()"
                  (input)="materia.set(valor($event))"
                  aria-describedby="grupo-materia-ayuda"
                />
                <datalist id="materias">
                  @for (m of materias(); track m) {
                    <option [value]="m"></option>
                  }
                </datalist>
                <p id="grupo-materia-ayuda" class="ayuda">{{ t('materiaAyuda') }}</p>
              </div>
            }

            @if (ciclos().length > 1) {
              <div class="campo">
                <label for="grupo-ciclo">{{ t('ciclo') }}</label>
                <div class="selector">
                  <select id="grupo-ciclo" name="ciclo" (change)="cicloId.set(valor($event))">
                    @for (c of ciclos(); track c.id) {
                      <option [value]="c.id" [selected]="c.id === cicloId()">{{ c.nombre }}</option>
                    }
                  </select>
                </div>
              </div>
            } @else {
              <p class="dato-ciclo">
                {{
                  t('cicloUnico', {
                    nombre: ciclos()[0].nombre,
                    cantidad: ciclos()[0].unidades.length,
                  })
                }}
                <a
                  class="enlace"
                  routerLink="/ciclos/nuevo"
                  [queryParams]="{ volver: '/grupos/nuevo' }"
                  >{{ t('otroCiclo') }}</a
                >
              </p>
            }

            <label class="casilla">
              <input
                type="checkbox"
                name="varios"
                [checked]="varios()"
                (change)="cambiarVarios()"
              />
              <span>
                <strong>{{ t('varios') }}</strong>
                <span class="dato-suave">{{ t('variosAyuda') }}</span>
              </span>
            </label>

            <div class="campo">
              <label for="grupo-nombre">{{ varios() ? t('nombres') : t('nombre') }}</label>
              <input
                id="grupo-nombre"
                name="nombre"
                autocomplete="off"
                [attr.maxlength]="varios() ? null : 40"
                [placeholder]="varios() ? t('nombresEjemplo') : t('nombreEjemplo')"
                [value]="texto()"
                (input)="texto.set(valor($event))"
                [attr.aria-invalid]="!!errorVisible()"
                [attr.aria-describedby]="
                  errorVisible() ? 'grupo-nombre-error grupo-nombre-ayuda' : 'grupo-nombre-ayuda'
                "
              />
              <p id="grupo-nombre-ayuda" class="ayuda">
                {{ varios() ? t('nombresAyuda') : t('nombreAyuda') }}
              </p>
              @if (errorVisible(); as codigo) {
                <p id="grupo-nombre-error" class="error">{{ t('errores.' + codigo) }}</p>
              }
            </div>
          </div>

          @if (nombres().length) {
            <section class="vista" aria-labelledby="vista-titulo">
              <h2 id="vista-titulo" class="dato-suave">
                {{ t('vista.' + forma(nombres().length), { cantidad: nombres().length }) }}
              </h2>
              @if (materia().trim() && nivel() === 'secundaria') {
                <p class="dato-suave materia-vista">{{ materia().trim() }}</p>
              }
              <ul class="tapas">
                @for (n of nombres(); track $index) {
                  <li>
                    <app-tapa [nivel]="nivel()" [grado]="grado()" />
                    <span class="nombre-tapa">{{ nombreCompleto(n) }}</span>
                  </li>
                }
              </ul>
            </section>
          } @else {
            <!-- Solo en escritorio: la columna de la vista no se queda vacía antes de escribir. -->
            <section class="vista vista--vacia" aria-hidden="true">
              <h2 class="dato-suave">{{ t('vista.uno') }}</h2>
              <ul class="tapas">
                <li>
                  <app-tapa [nivel]="nivel()" [grado]="grado()" />
                </li>
              </ul>
              <p class="dato-suave">{{ t('vista.vacia') }}</p>
            </section>
          }

          @if (error(); as codigo) {
            <p class="aviso aviso--error" role="alert">{{ errorTexto(codigo) }}</p>
          }

          <div class="pie-accion">
            <button class="boton" type="submit" [appOcupado]="guardando()">
              {{ nombres().length > 1 ? t('crearN', { cantidad: nombres().length }) : t('crear') }}
            </button>
          </div>
        </form>
      }
    </div>
  `,
  styles: `
    .grados {
      grid-template-columns: repeat(auto-fill, minmax(4.5rem, 1fr));
    }

    .grados label {
      gap: var(--espacio-2);
      padding-inline: var(--espacio-2);
    }

    .grados input {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip-path: inset(50%);
    }

    .grados label {
      position: relative;
    }

    .casilla > span {
      display: grid;
      gap: var(--espacio-1);
    }

    .dato-ciclo {
      color: var(--color-texto-suave);
    }

    .vista--vacia {
      display: none;
    }

    /* Escritorio: los campos a la izquierda y, a la derecha, las tapas de lo que se va a crear. */
    @media (min-width: 64rem) {
      .crear {
        grid-template-columns: minmax(0, 32rem) minmax(0, 1fr);
        column-gap: var(--espacio-7);
        align-items: start;
      }

      .crear > :not(.vista) {
        grid-column: 1;
      }

      .crear > .vista {
        grid-column: 2;
        grid-row: 1;
        position: sticky;
        top: var(--espacio-5);
        padding: var(--espacio-5);
        border: 1px solid var(--color-divisoria);
        border-radius: var(--radio-lg);
        background: var(--color-superficie);
      }

      .crear > .vista--vacia {
        display: grid;
      }
    }

    .vista {
      display: grid;
      gap: var(--espacio-3);
    }

    .vista h2 {
      font-size: var(--texto-xs);
      letter-spacing: 0;
    }

    .tapas {
      display: flex;
      flex-wrap: wrap;
      gap: var(--espacio-4) var(--espacio-3);
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .tapas li {
      display: grid;
      justify-items: start;
      gap: var(--espacio-1);
      width: 5.5rem;
    }

    .nombre-tapa {
      font-weight: var(--peso-fuerte);
      overflow-wrap: anywhere;
    }

    .tapas .dato-suave {
      overflow-wrap: anywhere;
    }
  `,
})
export class CrearGrupo implements OnInit {
  protected readonly forma = forma;
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly avisos = inject(AvisosService);
  private readonly i18n = inject(TranslocoService);
  private readonly servicio = inject(GruposService);
  private readonly perfil = inject(PerfilService);
  private readonly enfocar = crearEnfoque();

  /** Ciclo preseleccionado (por ejemplo, al volver de crear uno). */
  readonly ciclo = input<string>();

  protected readonly niveles: readonly Nivel[] = ['primaria', 'secundaria'];
  protected readonly nivel = signal<Nivel>('primaria');
  protected readonly grado = signal(1);
  protected readonly materia = signal('');
  protected readonly cicloId = signal('');
  protected readonly varios = signal(false);
  protected readonly texto = signal('');
  protected readonly intento = signal(false);
  protected readonly guardando = signal(false);
  protected readonly error = signal<string | null>(null);
  /** Sin red no se sabe si hay ciclos: se ofrece reintentar en lugar de mandar a crear otro. */
  protected readonly errorCarga = signal<string | null>(null);

  protected readonly ciclos = computed(() =>
    [...(this.servicio.ciclos() ?? [])].sort((a, b) => b.inicio.localeCompare(a.inicio)),
  );
  protected readonly grados = computed(() => GRADOS[this.nivel()]);
  protected readonly nombres = computed(() =>
    this.varios() ? separarNombres(this.texto()) : this.texto().trim() ? [this.texto().trim()] : [],
  );
  protected readonly errorNombres = computed(() => errorDeNombres(this.nombres()));
  /** "Falta el nombre" espera al envío; pasar de 15, repetir o alargar un nombre se avisa al escribir. */
  protected readonly errorVisible = computed(() => {
    const codigo = this.errorNombres();
    return this.intento() || codigo !== 'nombreRequerido' ? codigo : null;
  });
  protected readonly materias = computed(
    () => (this.i18n.translateObject('crearGrupo.materias') as string[] | undefined) ?? [],
  );

  async ngOnInit(): Promise<void> {
    this.errorCarga.set(null);
    let ciclos: readonly CicloDto[];
    let perfil;
    try {
      [ciclos, perfil] = await Promise.all([this.servicio.cargarCiclos(), this.perfil.cargar()]);
    } catch (e) {
      this.errorCarga.set(codigoDeError(e));
      return;
    }
    if (ciclos.length === 0) {
      await this.router.navigate(['/ciclos/nuevo'], {
        queryParams: { volver: '/grupos/nuevo' },
        replaceUrl: true,
      });
      return;
    }
    if (perfil?.nivel === 'secundaria') {
      this.nivel.set('secundaria');
    }
    const elegido = ciclos.find((c) => c.id === this.ciclo());
    this.cicloId.set(elegido?.id ?? this.ciclos()[0].id);
  }

  protected valor(evento: Event): string {
    return (evento.target as HTMLInputElement | HTMLSelectElement).value;
  }

  protected errorTexto(codigo: string): string {
    return this.i18n.translate('errores.' + codigo);
  }

  protected nombreCompleto(nombre: string): string {
    return nombreGrupo(this.grado(), nombre);
  }

  protected cambiarNivel(nivel: Nivel): void {
    this.nivel.set(nivel);
    if (!GRADOS[nivel].includes(this.grado())) {
      this.grado.set(1);
    }
  }

  protected cambiarVarios(): void {
    this.varios.update((v) => !v);
    this.enfocar('#grupo-nombre');
  }

  protected async crear(): Promise<void> {
    this.intento.set(true);
    this.error.set(null);
    if (this.errorNombres()) {
      this.enfocar(PRIMER_ERROR);
      return;
    }
    const comunes = {
      nivel: this.nivel(),
      grado: this.grado(),
      materia:
        this.nivel() === 'secundaria' && this.materia().trim() ? this.materia().trim() : null,
      cicloId: this.cicloId(),
    };
    this.guardando.set(true);
    try {
      if (this.varios()) {
        const creados = await this.api.invoke(crearGruposLote, {
          body: { ...comunes, nombres: this.nombres() },
        });
        creados.forEach((g) => this.servicio.recordar(g));
        this.avisos.mostrar(
          this.i18n.translate('crearGrupo.creados', { cantidad: creados.length }),
        );
        await this.router.navigateByUrl('/');
      } else {
        const creado = await this.api.invoke(crearGrupo, {
          body: { ...comunes, nombre: this.nombres()[0] },
        });
        this.servicio.recordar(creado);
        await this.router.navigate(['/grupos', creado.id]);
      }
    } catch (e) {
      this.error.set(codigoDeError(e));
    } finally {
      this.guardando.set(false);
    }
  }
}
