import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ActividadDto } from '../../api/models/actividad-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { MatrizCalificacionesDto } from '../../api/models/matriz-calificaciones-dto';
import { PeriodoDto } from '../../api/models/periodo-dto';
import { codigoDeError } from '../../core/errores/problema';
import { mostrarFecha } from '../../shared/fechas';
import { crearEnfoque, PRIMER_ERROR } from '../../shared/foco';
import { claveGrado } from '../../shared/grados';
import { Hoja } from '../../shared/hoja';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { controlarPortada } from '../../shared/portada/portada.service';
import { portadaDeGrupo } from '../grupos/grupos.service';
import { PestanasGrupo } from '../grupos/pestanas-grupo';
import { EvaluacionService } from './evaluacion.service';
import { numero } from './matriz';

/** Hoy en la zona del docente, como "2026-10-17". */
function hoy(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/** La unidad que abre: la que tiene a hoy entre sus fechas; si no, la primera abierta; si no, la última. */
export function unidadDeHoy(
  periodos: readonly PeriodoDto[],
  fecha = hoy(),
): PeriodoDto | undefined {
  return (
    periodos.find((p) => p.inicio <= fecha && fecha <= p.fin) ??
    periodos.find((p) => p.estado === 'abierto') ??
    periodos.at(-1)
  );
}

/**
 * SPEC-03, "Rubros del periodo" y la puerta a la captura: la vista que abre el grupo. Unidad elegida, suma de
 * pesos siempre a la vista (RN-031), rubros con sus actividades y su avance, y "Nueva actividad" para capturar
 * la de hoy en un minuto.
 */
@Component({
  selector: 'app-calificaciones',
  imports: [TranslocoDirective, RouterLink, NgTemplateOutlet, PestanasGrupo, Hoja, Icono, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="pantalla pantalla--ancha"
      [attr.data-grado]="clave() ?? null"
      *transloco="let t; prefix: 'calificaciones'"
    >
      <a class="volver" routerLink="/">
        <app-icono nombre="volver" />
        {{ t('misGrupos') }}
      </a>

      <app-pestanas-grupo [id]="id()" [grado]="clave()" />

      <div class="cabeza-pantalla">
        <hgroup>
          <h1 tabindex="-1">{{ t('titulo') }}</h1>
          @if (periodo(); as p) {
            <p class="dato-suave cifras">
              {{ p.nombre }} · {{ fecha(p.inicio) }} a {{ fecha(p.fin) }}
            </p>
          }
        </hgroup>
      </div>

      @if (periodos().length > 1) {
        <nav class="unidades" [attr.aria-label]="t('unidades')">
          @for (p of periodos(); track p.id) {
            <a
              class="chip"
              [routerLink]="[]"
              [queryParams]="{ unidad: p.id }"
              [attr.aria-current]="p.id === periodo()?.id ? 'page' : null"
            >
              {{ p.nombre }}
              @if (p.estado !== 'abierto') {
                <span class="solo-lector">({{ t('estado.' + p.estado) }})</span>
                <span class="estado" aria-hidden="true">{{ t('estadoCorto.' + p.estado) }}</span>
              }
            </a>
          }
        </nav>
      }

      @if (grupo()?.archivadoEn) {
        <p class="aviso aviso--pendiente">{{ t('archivado') }}</p>
      } @else if (matriz()?.soloLectura) {
        <p class="aviso aviso--pendiente">{{ t('soloLectura') }}</p>
      } @else if (matriz()?.estado === 'en_cierre') {
        <p class="aviso aviso--pendiente">{{ t('enCierre') }}</p>
      }

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (!matriz()) {
        <div class="rubros" role="status" [attr.aria-label]="t('cargando')">
          @for (i of [1, 2]; track i) {
            <div class="rubro" aria-hidden="true">
              <span class="esqueleto linea-esqueleto"></span>
              <span class="esqueleto fila-esqueleto"></span>
              <span class="esqueleto fila-esqueleto"></span>
            </div>
          }
        </div>
      } @else if (matriz(); as m) {
        <div class="columnas">
          <div class="principal">
            @if (m.alumnos.length === 0) {
              <section class="vacio" aria-labelledby="sin-alumnos">
                <h2 id="sin-alumnos">{{ t('sinAlumnos.titulo') }}</h2>
                <p class="dato-suave">{{ t('sinAlumnos.texto') }}</p>
                <a class="boton" [routerLink]="['/grupos', id(), 'alumnos']">
                  {{ t('sinAlumnos.accion') }}
                </a>
              </section>
            }

            @if (m.rubros.length === 0) {
              <section class="vacio" aria-labelledby="sin-rubros">
                <h2 id="sin-rubros">{{ t('sinRubros.titulo') }}</h2>
                <p class="dato-suave">{{ t('sinRubros.texto') }}</p>
                @if (editable()) {
                  <a class="boton" [routerLink]="rutaRubros()">{{ t('sinRubros.accion') }}</a>
                }
              </section>
            } @else {
              <div class="solo-celular">
                <ng-container [ngTemplateOutlet]="pesos" />
              </div>

              @for (r of m.rubros; track r.rubro.id) {
                <section class="rubro" [attr.aria-labelledby]="'rubro-' + r.rubro.id">
                  <header class="cabeza-rubro">
                    <h2 [id]="'rubro-' + r.rubro.id">{{ r.rubro.nombre }}</h2>
                    <span class="peso cifras">{{ numero(r.rubro.peso) }} %</span>
                    <p class="dato-suave detalle">
                      {{ t('forma.' + r.rubro.forma) }}
                      @if (r.rubro.criterios.length) {
                        · {{ t('criterios', { cantidad: r.rubro.criterios.length }) }}
                      }
                    </p>
                  </header>
                  @if (r.actividades.length === 0) {
                    <p class="dato-suave sin-actividades">{{ t('sinActividades') }}</p>
                  } @else {
                    <ol class="filas">
                      @for (a of r.actividades; track a.id) {
                        <li>
                          <a
                            class="actividad"
                            [routerLink]="[
                              '/grupos',
                              id(),
                              'periodos',
                              m.periodoId,
                              'actividades',
                              a.id,
                            ]"
                          >
                            <span class="nombre">{{ a.nombre }}</span>
                            @if (a.fecha) {
                              <span class="fecha dato-suave cifras">{{ fecha(a.fecha) }}</span>
                            }
                            <span class="avance cifras">
                              <span class="solo-lector">{{ t('avance') }}</span>
                              {{ capturadas(a) }}/{{ activos() }}
                            </span>
                            <span class="progreso" aria-hidden="true">
                              <span [style.inline-size.%]="porcentaje(a)"></span>
                            </span>
                          </a>
                        </li>
                      }
                    </ol>
                  }
                </section>
              }
            }

            @if (puedeCrear()) {
              <div class="pie-accion solo-celular">
                <button id="nueva-actividad" class="boton" type="button" (click)="abrirNueva()">
                  <app-icono nombre="mas" />
                  {{ t('nueva.titulo') }}
                </button>
              </div>
            }
          </div>

          @if (m.rubros.length) {
            <aside class="lateral solo-escritorio" aria-labelledby="resumen-titulo">
              <h2 id="resumen-titulo">{{ periodo()?.nombre }}</h2>
              <ng-container [ngTemplateOutlet]="pesos" />
              <p class="dato-suave cifras">
                {{ t('resumen', { alumnos: activos(), actividades: totalActividades() }) }}
              </p>
              @if (puedeCrear()) {
                <button class="boton" type="button" (click)="abrirNueva()">
                  <app-icono nombre="mas" />
                  {{ t('nueva.titulo') }}
                </button>
              }
              <a
                class="boton boton--secundario"
                [routerLink]="['/grupos', id(), 'periodos', m.periodoId, 'tabla']"
              >
                <app-icono nombre="tabla" />
                {{ t('verTabla') }}
              </a>
              @if (editable()) {
                <a class="enlace" [routerLink]="rutaRubros()">{{ t('editarRubros') }}</a>
              }
            </aside>
          }
        </div>
      }

      <ng-template #pesos>
        @if (matriz(); as m) {
          <div class="pesos" [class.incompletos]="!sumanCien()">
            <div class="linea-pesos">
              <span class="cifras">{{ t('pesos', { suma: numero(m.sumaPesos) }) }}</span>
              @if (editable()) {
                <a class="enlace solo-celular" [routerLink]="rutaRubros()">{{
                  t('editarRubros')
                }}</a>
              }
            </div>
            <span class="progreso" aria-hidden="true">
              <span [style.inline-size.%]="pesoBarra()"></span>
            </span>
            @if (!sumanCien()) {
              <p class="nota-pesos">{{ t('pesosIncompletos') }}</p>
            }
          </div>
        }
      </ng-template>

      <app-hoja etiqueta="nueva-titulo" (cerrada)="intento.set(false)">
        <h2 id="nueva-titulo">{{ t('nueva.titulo') }}</h2>
        <form class="formulario" (submit)="$event.preventDefault(); crear()" novalidate>
          <div class="campo">
            <label for="nueva-rubro">{{ t('nueva.rubro') }}</label>
            <span class="selector">
              <select id="nueva-rubro" [value]="rubroNueva()" (change)="elegirRubro(valor($event))">
                @for (r of rubrosConActividades(); track r.rubro.id) {
                  <option
                    [value]="r.rubro.id"
                    [selected]="r.rubro.id === rubroNueva()"
                    [textContent]="r.rubro.nombre"
                  ></option>
                }
              </select>
            </span>
          </div>
          <div class="campo">
            <label for="nueva-nombre">{{ t('nueva.nombre') }}</label>
            <input
              id="nueva-nombre"
              maxlength="60"
              autocomplete="off"
              [value]="nombreNueva()"
              (input)="nombreNueva.set(valor($event))"
              [attr.aria-invalid]="intento() && !nombreNueva().trim()"
              [attr.aria-describedby]="intento() && !nombreNueva().trim() ? 'nueva-error' : null"
            />
            @if (intento() && !nombreNueva().trim()) {
              <p id="nueva-error" class="error">{{ t('nueva.requerido') }}</p>
            }
          </div>
          <div class="campo">
            <label for="nueva-fecha">{{ t('nueva.fecha') }}</label>
            <input
              id="nueva-fecha"
              type="date"
              [min]="periodo()?.inicio ?? null"
              [max]="periodo()?.fin ?? null"
              [value]="fechaNueva()"
              (input)="fechaNueva.set(valor($event))"
            />
          </div>
          @if (errorNueva(); as codigo) {
            <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
          }
          <button class="boton boton--ancho" type="submit" [appOcupado]="creando()">
            {{ t('nueva.capturar') }}
          </button>
        </form>
      </app-hoja>
    </div>
  `,
  styles: `
    hgroup {
      display: grid;
      gap: var(--espacio-1);
    }

    .unidades {
      display: flex;
      gap: var(--espacio-2);
      overflow-x: auto;
      margin-inline: calc(-1 * var(--espacio-4));
      padding: var(--espacio-1) var(--espacio-4);
      scrollbar-width: none;
    }

    @media (min-width: 48rem) {
      .unidades {
        flex-wrap: wrap;
        margin-inline: 0;
        padding-inline: 0;
      }
    }

    .chip {
      display: inline-flex;
      flex: none;
      align-items: center;
      gap: var(--espacio-2);
      min-height: var(--tactil-minimo);
      padding: 0 var(--espacio-4);
      border-radius: var(--radio-pleno);
      background: var(--color-superficie-hundida);
      color: var(--color-texto);
      font-weight: var(--peso-medio);
      text-decoration: none;
    }

    .chip[aria-current='page'] {
      background: var(--grado);
      color: var(--color-sobre-grado);
    }

    .chip .estado {
      font-size: var(--texto-xs);
    }

    .principal {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      align-content: start;
      gap: var(--espacio-5);
    }

    .rubros {
      display: grid;
      gap: var(--espacio-5);
    }

    .rubro {
      display: grid;
      gap: var(--espacio-2);
    }

    .cabeza-rubro {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      align-items: baseline;
      column-gap: var(--espacio-3);
    }

    .cabeza-rubro h2 {
      font-size: var(--texto-lg);
    }

    .peso {
      font-weight: var(--peso-fuerte);
      color: var(--grado-tinta);
    }

    .detalle {
      grid-column: 1 / -1;
      font-size: var(--texto-sm);
    }

    .sin-actividades {
      padding-block: var(--espacio-2);
      border-top: 1px solid var(--color-divisoria);
    }

    .filas {
      border-top: 1px solid var(--color-divisoria);
    }

    .actividad {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      align-items: baseline;
      column-gap: var(--espacio-3);
      row-gap: var(--espacio-1);
      min-height: 3.5rem;
      padding-block: var(--espacio-3);
      color: inherit;
      text-decoration: none;
    }

    .actividad .nombre {
      font-weight: var(--peso-medio);
      overflow-wrap: anywhere;
    }

    .actividad .fecha {
      grid-row: 2;
      font-size: var(--texto-sm);
    }

    .actividad .avance {
      grid-column: 2;
      grid-row: 1;
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
    }

    .actividad .progreso {
      grid-column: 2;
      grid-row: 2;
      align-self: center;
      inline-size: 4rem;
    }

    .actividad:active .nombre {
      color: var(--color-accion);
    }

    @media (hover: hover) and (pointer: fine) {
      .actividad:hover .nombre {
        text-decoration: underline;
        text-underline-offset: 0.2em;
      }
    }

    .pesos {
      display: grid;
      gap: var(--espacio-2);
      padding: var(--espacio-3) var(--espacio-4);
      border-radius: var(--radio-lg);
      background: var(--grado-tinte);
      color: var(--grado-tinta);
      font-weight: var(--peso-medio);
    }

    .pesos .progreso {
      background: var(--color-superficie);
    }

    .pesos.incompletos {
      background: var(--color-aviso-tinte);
      color: var(--color-aviso-tinta);
    }

    .pesos.incompletos .progreso > span {
      background: var(--color-aviso);
    }

    .linea-pesos {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      gap: var(--espacio-2);
    }

    .nota-pesos {
      font-size: var(--texto-sm);
      font-weight: var(--peso-normal);
    }

    .vacio {
      display: grid;
      justify-items: start;
      gap: var(--espacio-3);
    }

    .vacio h2 {
      font-size: var(--texto-lg);
    }

    .linea-esqueleto {
      inline-size: 10rem;
      block-size: 1.5rem;
    }

    .fila-esqueleto {
      inline-size: 100%;
      block-size: 3rem;
    }
  `,
})
export class Calificaciones implements OnInit {
  private readonly servicio = inject(EvaluacionService);
  private readonly i18n = inject(TranslocoService);
  private readonly router = inject(Router);
  private readonly pintarPortada = controlarPortada();
  private readonly enfocar = crearEnfoque();
  private leyendo: string | null = null;
  protected readonly numero = numero;

  readonly id = input.required<string>();
  /** La unidad elegida (?unidad=), el id de su periodo. */
  readonly unidad = input<string>();

  protected readonly hoja = viewChild.required(Hoja);
  protected readonly grupo = signal<GrupoDto | null>(null);
  protected readonly periodos = signal<readonly PeriodoDto[]>([]);
  protected readonly matriz = signal<MatrizCalificacionesDto | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly rubroNueva = signal('');
  protected readonly nombreNueva = signal('');
  protected readonly fechaNueva = signal('');
  protected readonly intento = signal(false);
  protected readonly creando = signal(false);
  protected readonly errorNueva = signal<string | null>(null);

  protected readonly clave = computed(() => {
    const g = this.grupo();
    return g ? claveGrado(g.nivel, g.grado) : undefined;
  });
  protected readonly periodo = computed(() => {
    const lista = this.periodos();
    return lista.find((p) => p.id === this.unidad()) ?? unidadDeHoy(lista);
  });
  protected readonly activos = computed(
    () => this.matriz()?.alumnos.filter((a) => !a.dadoDeBaja).length ?? 0,
  );
  protected readonly totalActividades = computed(
    () => this.matriz()?.rubros.reduce((n, r) => n + r.actividades.length, 0) ?? 0,
  );
  protected readonly sumanCien = computed(() => Number(this.matriz()?.sumaPesos ?? 0) === 100);
  protected readonly pesoBarra = computed(() =>
    Math.min(100, Number(this.matriz()?.sumaPesos ?? 0)),
  );
  protected readonly editable = computed(() => {
    const m = this.matriz();
    return !!m && !this.grupo()?.archivadoEn && m.estado === 'abierto';
  });
  protected readonly rubrosConActividades = computed(
    () => this.matriz()?.rubros.filter((r) => r.rubro.comportamiento === 'actividades') ?? [],
  );
  protected readonly puedeCrear = computed(
    () =>
      !!this.matriz() &&
      !this.matriz()!.soloLectura &&
      !this.grupo()?.archivadoEn &&
      this.rubrosConActividades().length > 0,
  );
  protected readonly rutaRubros = computed(() => [
    '/grupos',
    this.id(),
    'periodos',
    this.periodo()?.id ?? '',
    'rubros',
  ]);

  constructor() {
    // Al cambiar de unidad se lee su matriz; el resto de la pantalla se queda.
    effect(() => {
      const periodo = this.periodo();
      if (periodo) {
        untracked(() => void this.cargarMatriz(periodo.id));
      }
    });
  }

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      const [grupo, periodos] = await Promise.all([
        this.servicio.grupo(this.id()),
        this.servicio.periodos(this.id()),
      ]);
      this.grupo.set(grupo);
      this.pintarPortada(portadaDeGrupo(grupo));
      this.periodos.set(periodos);
      const p = this.periodo();
      if (p && this.matriz()?.periodoId !== p.id) {
        await this.cargarMatriz(p.id);
      }
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  private async cargarMatriz(periodoId: string): Promise<void> {
    if (this.leyendo === periodoId) {
      return; // el efecto y la carga inicial piden la misma unidad: una sola lectura
    }
    if (this.matriz()?.periodoId !== periodoId) {
      this.matriz.set(null);
    }
    this.leyendo = periodoId;
    try {
      const matriz = await this.servicio.matriz(periodoId);
      if (this.periodo()?.id === periodoId) {
        this.matriz.set(matriz);
      }
    } catch (e) {
      this.error.set(codigoDeError(e));
    } finally {
      if (this.leyendo === periodoId) {
        this.leyendo = null;
      }
    }
  }

  protected fecha(valor: string): string {
    return mostrarFecha(valor);
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  protected valor(evento: Event): string {
    return (evento.target as HTMLInputElement).value;
  }

  /** Celdas con algo capturado por actividad, solo de alumnos activos; se calcula una vez por matriz. */
  private readonly conteo = computed(() => {
    const m = this.matriz();
    const mapa = new Map<string, number>();
    if (!m) {
      return mapa;
    }
    const activos = new Set(m.alumnos.filter((a) => !a.dadoDeBaja).map((a) => a.id));
    for (const c of m.celdas) {
      if (
        activos.has(c.alumnoId) &&
        (c.valor !== null || c.marca !== 'ninguna' || c.criterios.length > 0)
      ) {
        mapa.set(c.actividadId, (mapa.get(c.actividadId) ?? 0) + 1);
      }
    }
    return mapa;
  });

  protected capturadas(actividad: ActividadDto): number {
    return this.conteo().get(actividad.id) ?? 0;
  }

  protected porcentaje(actividad: ActividadDto): number {
    const total = this.activos();
    return total === 0 ? 0 : Math.round((this.capturadas(actividad) / total) * 100);
  }

  protected abrirNueva(): void {
    const rubros = this.rubrosConActividades();
    const elegido = rubros.find((r) => r.rubro.id === this.rubroNueva()) ?? rubros[0];
    this.elegirRubro(elegido.rubro.id);
    const p = this.periodo();
    const d = hoy();
    this.fechaNueva.set(p && p.inicio <= d && d <= p.fin ? d : '');
    this.intento.set(false);
    this.errorNueva.set(null);
    this.hoja().abrir();
  }

  /** El nombre propone el siguiente número del rubro ("Tareas 4"); el docente puede cambiarlo. */
  protected elegirRubro(id: string): void {
    this.rubroNueva.set(id);
    const r = this.rubrosConActividades().find((x) => x.rubro.id === id);
    if (r) {
      this.nombreNueva.set(`${r.rubro.nombre} ${r.actividades.length + 1}`);
    }
  }

  protected async crear(): Promise<void> {
    this.intento.set(true);
    this.errorNueva.set(null);
    if (!this.nombreNueva().trim()) {
      this.enfocar(PRIMER_ERROR);
      return;
    }
    this.creando.set(true);
    try {
      const actividad = await this.servicio.crearActividad(this.rubroNueva(), {
        nombre: this.nombreNueva().trim(),
        fecha: this.fechaNueva() || null,
      });
      this.hoja().cerrar();
      await this.router.navigate([
        '/grupos',
        this.id(),
        'periodos',
        this.periodo()!.id,
        'actividades',
        actividad.id,
      ]);
    } catch (e) {
      this.errorNueva.set(codigoDeError(e));
    } finally {
      this.creando.set(false);
    }
  }
}
