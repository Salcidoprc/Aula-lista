import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { AlumnoMatrizDto } from '../../api/models/alumno-matriz-dto';
import { EquipoDto } from '../../api/models/equipo-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { codigoDeError } from '../../core/errores/problema';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { crearEnfoque } from '../../shared/foco';
import { claveGrado, nombreGrupo } from '../../shared/grados';
import { Hoja } from '../../shared/hoja';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { forma } from '../../shared/plural';
import { controlarPortada } from '../../shared/portada/portada.service';
import { ConCambiosSinGuardar } from '../../shared/rutas';
import { portadaDeGrupo } from '../grupos/grupos.service';
import { EvaluacionService } from './evaluacion.service';

/** RN-114: un equipo mientras se arma. `id` nulo es uno nuevo; `clave` sirve para enfocar y rastrear. */
interface EquipoBorrador {
  readonly clave: string;
  readonly id: string | null;
  readonly nombre: string;
  readonly alumnoIds: readonly string[];
}

const MAXIMO_EQUIPOS = 30;
const LONGITUD_NOMBRE = 40;

/**
 * SPEC-03, "Equipos del grupo": crear equipos, mover alumnos entre ellos y ver quién no tiene equipo (RN-114).
 * Mover es un toque: se toca al alumno y se elige a dónde va, sin arrastrar. Se guarda con un botón porque el
 * servidor reemplaza los equipos completos; reorganizar no cambia calificaciones ya capturadas (RN-116).
 */
@Component({
  selector: 'app-equipos',
  imports: [TranslocoDirective, RouterLink, NgTemplateOutlet, Hoja, Icono, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="pantalla pantalla--ancha"
      [attr.data-grado]="clave() ?? null"
      *transloco="let t; prefix: 'equipos'"
    >
      <a class="volver" [routerLink]="['/grupos', id(), 'alumnos']">
        <app-icono nombre="volver" />
        {{ t('volver') }}
      </a>

      <hgroup>
        <h1 tabindex="-1">{{ t('titulo') }}</h1>
        @if (grupo()) {
          <p class="dato-suave cifras">
            {{ nombreGrupo() }} ·
            {{ t('resumen.' + forma(borrador().length), { cantidad: borrador().length }) }}
          </p>
        }
      </hgroup>

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (alumnos() === null) {
        <div class="bloques" role="status" [attr.aria-label]="t('cargando')">
          @for (i of [1, 2]; track i) {
            <span class="esqueleto bloque-esqueleto" aria-hidden="true"></span>
          }
        </div>
      } @else {
        @if (!editable()) {
          <p class="aviso aviso--pendiente">{{ t('archivado') }}</p>
        }

        <form class="columnas" (submit)="$event.preventDefault(); guardar()" novalidate>
          <div class="principal">
            @if (activos().length === 0) {
              <section class="vacio" aria-labelledby="sin-alumnos-titulo">
                <h2 id="sin-alumnos-titulo">{{ t('sinAlumnos.titulo') }}</h2>
                <p class="dato-suave">{{ t('sinAlumnos.texto') }}</p>
                <a class="boton" [routerLink]="['/grupos', id(), 'alumnos']">
                  {{ t('sinAlumnos.accion') }}
                </a>
              </section>
            } @else if (borrador().length === 0) {
              <section class="vacio" aria-labelledby="vacio-titulo">
                <h2 id="vacio-titulo">{{ t('vacio.titulo') }}</h2>
                <p class="dato-suave">{{ t('vacio.texto') }}</p>
                @if (editable()) {
                  <button class="boton" type="button" (click)="nuevo()">
                    <app-icono nombre="mas" />
                    {{ t('nuevo') }}
                  </button>
                }
              </section>
            } @else {
              @if (sinEquipo().length) {
                <section class="sin-equipo" aria-labelledby="sin-equipo-titulo">
                  <h2 id="sin-equipo-titulo" class="cifras">
                    {{ t('sinEquipo', { cantidad: sinEquipo().length }) }}
                  </h2>
                  <ng-container
                    [ngTemplateOutlet]="integrantes"
                    [ngTemplateOutletContext]="{ $implicit: sinEquipo() }"
                  />
                </section>
              } @else {
                <p class="completo">
                  <app-icono nombre="guardado" />
                  {{ t('todosConEquipo') }}
                </p>
              }

              <div class="equipos">
                @for (e of borrador(); track e.clave; let i = $index) {
                  <section class="equipo" [attr.aria-labelledby]="e.clave + '-titulo'">
                    <div class="cabeza-equipo">
                      <h2 [id]="e.clave + '-titulo'" class="solo-lector">
                        {{ e.nombre || t('sinNombre') }}
                      </h2>
                      <div class="campo campo--nombre">
                        <label [for]="e.clave + '-nombre'">{{ t('nombre') }}</label>
                        <input
                          [id]="e.clave + '-nombre'"
                          autocomplete="off"
                          autocapitalize="sentences"
                          [attr.maxlength]="longitudNombre"
                          [value]="e.nombre"
                          [readOnly]="!editable()"
                          [attr.aria-invalid]="intento() && fallaNombre(e) ? true : null"
                          [attr.aria-describedby]="
                            intento() && fallaNombre(e) ? e.clave + '-error' : null
                          "
                          (input)="renombrar(e.clave, valor($event))"
                        />
                        @if (intento() && fallaNombre(e); as falla) {
                          <p class="error" [id]="e.clave + '-error'">{{ t('errores.' + falla) }}</p>
                        }
                      </div>
                      <span class="cuenta cifras dato-suave">
                        {{
                          t('integrantes.' + forma(e.alumnoIds.length), {
                            cantidad: e.alumnoIds.length,
                          })
                        }}
                      </span>
                      @if (editable()) {
                        <button
                          class="boton-icono peligro"
                          type="button"
                          [attr.aria-label]="t('quitar', { nombre: e.nombre || t('sinNombre') })"
                          (click)="quitar(e.clave)"
                        >
                          <app-icono nombre="borrar" />
                        </button>
                      }
                    </div>
                    @if (e.alumnoIds.length) {
                      <ng-container
                        [ngTemplateOutlet]="integrantes"
                        [ngTemplateOutletContext]="{ $implicit: alumnosDe(e) }"
                      />
                    } @else {
                      <p class="dato-suave ayuda-vacio">
                        {{ sinEquipo().length ? t('equipoVacio') : t('equipoVacioSinLibres') }}
                      </p>
                    }
                  </section>
                }
              </div>

              @if (editable() && borrador().length < maximoEquipos) {
                <button class="boton boton--secundario agregar" type="button" (click)="nuevo()">
                  <app-icono nombre="mas" />
                  {{ t('nuevo') }}
                </button>
              }
            }

            <p class="solo-lector" aria-live="polite">{{ anuncio() }}</p>

            @if (hayCambios()) {
              <div class="pie-accion solo-celular">
                <ng-container [ngTemplateOutlet]="pie" />
              </div>
            }
          </div>

          @if (borrador().length > 0 || hayCambios()) {
            <aside class="lateral solo-escritorio" aria-labelledby="resumen-equipos">
              <h2 id="resumen-equipos">{{ t('lateral') }}</h2>
              <p class="dato-suave">{{ t('lateralTexto') }}</p>
              <ng-container [ngTemplateOutlet]="pie" />
            </aside>
          }
        </form>
      }

      <ng-template #integrantes let-lista>
        <ul class="integrantes">
          @for (a of lista; track a.id) {
            <li>
              <button
                class="integrante"
                type="button"
                [disabled]="!editable()"
                [attr.data-alumno]="a.id"
                (click)="elegir(a)"
              >
                <span class="numero cifras">{{ a.numeroLista }}</span>
                <span class="nombre"
                  ><strong>{{ a.apellidos }}</strong> {{ a.nombres }}</span
                >
                @if (editable()) {
                  <span class="mover dato-suave">{{ t('mover') }}</span>
                }
              </button>
            </li>
          }
        </ul>
      </ng-template>

      <ng-template #pie>
        @if (errorGuardar(); as codigo) {
          <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
        }
        @if (editable()) {
          <button class="boton boton--ancho" type="submit" [appOcupado]="guardando()">
            {{ t('guardar') }}
          </button>
        }
      </ng-template>

      <app-hoja etiqueta="mover-titulo" (cerrada)="enfocarElegido()">
        @if (elegido(); as a) {
          <h2 id="mover-titulo">
            {{ t('moverTitulo', { nombre: a.nombres + ' ' + a.apellidos }) }}
          </h2>
          <ul class="destinos">
            @for (e of borrador(); track e.clave) {
              <li>
                <button
                  class="destino"
                  type="button"
                  [attr.aria-current]="e.alumnoIds.includes(a.id) ? 'true' : null"
                  (click)="mover(a, e.clave)"
                >
                  <app-icono nombre="equipo" />
                  <span class="nombre">{{ e.nombre || t('sinNombre') }}</span>
                  <span class="cifras dato-suave">{{ e.alumnoIds.length }}</span>
                </button>
              </li>
            }
            @if (borrador().length < maximoEquipos) {
              <li>
                <button class="destino" type="button" (click)="mover(a, null, true)">
                  <app-icono nombre="mas" />
                  <span class="nombre">{{ t('nuevoCon') }}</span>
                </button>
              </li>
            }
            <li>
              <button
                class="destino"
                type="button"
                [attr.aria-current]="libre(a.id) ? 'true' : null"
                (click)="mover(a, null)"
              >
                <app-icono nombre="persona" />
                <span class="nombre">{{ t('sinEquipoDestino') }}</span>
              </button>
            </li>
          </ul>
        }
      </app-hoja>

      <dialog
        #dialogoSalir
        class="dialogo"
        closedby="any"
        aria-labelledby="dialogo-salir-titulo"
        (close)="decidirSalida(dialogoSalir.returnValue === 'salir')"
      >
        <form class="contenido" method="dialog">
          <h2 id="dialogo-salir-titulo">{{ t('salir.titulo') }}</h2>
          <p>{{ t('salir.texto') }}</p>
          <div class="acciones">
            <button class="boton boton--secundario" value="quedarse">
              {{ t('salir.quedarse') }}
            </button>
            <button class="boton boton--peligro" value="salir">{{ t('salir.accion') }}</button>
          </div>
        </form>
      </dialog>
    </div>
  `,
  styles: `
    hgroup {
      display: grid;
      gap: var(--espacio-1);
    }

    h2 {
      font-size: var(--texto-lg);
    }

    .principal {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      align-content: start;
      gap: var(--espacio-5);
    }

    .bloques {
      display: grid;
      gap: var(--espacio-5);
    }

    .bloque-esqueleto {
      block-size: 8rem;
    }

    .completo {
      display: flex;
      align-items: center;
      gap: var(--espacio-2);
      color: var(--color-exito-tinta);
      font-weight: var(--peso-medio);
    }

    /* "Sin equipo" pide atención: superficie hundida, sin tarjeta ni borde de color. */
    .sin-equipo {
      display: grid;
      gap: var(--espacio-2);
      padding: var(--espacio-4);
      border-radius: var(--radio-lg);
      background: var(--color-superficie-hundida);
    }

    .equipos {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      gap: var(--espacio-5);
    }

    @media (min-width: 64rem) {
      .equipos {
        grid-template-columns: repeat(auto-fill, minmax(min(18rem, 100%), 1fr));
        gap: var(--espacio-5) var(--espacio-6);
      }
    }

    .equipo {
      display: grid;
      align-content: start;
      gap: var(--espacio-2);
      padding-top: var(--espacio-4);
      border-top: 1px solid var(--color-divisoria);
    }

    .cabeza-equipo {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto auto;
      align-items: end;
      gap: var(--espacio-2) var(--espacio-3);
    }

    .campo--nombre input {
      font-size: var(--texto-lg);
      font-weight: var(--peso-fuerte);
    }

    .campo--nombre .error {
      grid-column: 1 / -1;
    }

    .cuenta {
      padding-block-end: var(--espacio-3);
      font-size: var(--texto-sm);
      white-space: nowrap;
    }

    .ayuda-vacio {
      font-size: var(--texto-sm);
    }

    .integrantes,
    .destinos {
      display: grid;
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .integrantes > li + li {
      border-top: 1px solid var(--color-divisoria);
    }

    .integrante,
    .destino {
      display: flex;
      align-items: baseline;
      gap: var(--espacio-3);
      inline-size: 100%;
      min-block-size: var(--tactil-minimo);
      padding: var(--espacio-2) 0;
      border: 0;
      background: none;
      color: inherit;
      font: inherit;
      text-align: start;
      cursor: pointer;
      touch-action: manipulation;
    }

    .integrante:disabled {
      cursor: default;
    }

    .integrante .numero {
      min-inline-size: 2ch;
      color: var(--grado-tinta);
      font-weight: var(--peso-fuerte);
      text-align: end;
    }

    .integrante .nombre,
    .destino .nombre {
      flex: 1;
      min-inline-size: 0;
      overflow-wrap: anywhere;
    }

    .integrante strong {
      font-weight: var(--peso-medio);
    }

    .mover {
      font-size: var(--texto-sm);
    }

    .integrante:active:not(:disabled) .nombre,
    .destino:active .nombre {
      color: var(--color-accion);
    }

    @media (hover: hover) and (pointer: fine) {
      .integrante:hover:not(:disabled) .nombre {
        text-decoration: underline;
        text-underline-offset: 0.2em;
      }

      .destino:hover {
        background: var(--color-superficie-hundida);
      }
    }

    .integrante:focus-visible,
    .destino:focus-visible {
      border-radius: var(--radio-sm);
      box-shadow: var(--foco);
    }

    .destinos {
      gap: var(--espacio-1);
      margin-block-start: var(--espacio-3);
    }

    .destino {
      align-items: center;
      padding-inline: var(--espacio-3);
      border-radius: var(--radio-md);
    }

    .destino[aria-current='true'] {
      background: var(--grado-tinte);
      font-weight: var(--peso-medio);
    }

    .agregar {
      justify-self: start;
    }
  `,
})
export class Equipos implements OnInit, ConCambiosSinGuardar {
  private readonly servicio = inject(EvaluacionService);
  private readonly avisos = inject(AvisosService);
  private readonly i18n = inject(TranslocoService);
  private readonly pintarPortada = controlarPortada();
  private readonly enfocar = crearEnfoque();
  protected readonly forma = forma;
  protected readonly maximoEquipos = MAXIMO_EQUIPOS;
  protected readonly longitudNombre = LONGITUD_NOMBRE;

  readonly id = input.required<string>();

  protected readonly hoja = viewChild.required(Hoja);
  private readonly dialogoSalir = viewChild.required<ElementRef<HTMLDialogElement>>('dialogoSalir');
  private resolverSalida: ((salir: boolean) => void) | null = null;

  protected readonly grupo = signal<GrupoDto | null>(null);
  protected readonly alumnos = signal<readonly AlumnoMatrizDto[] | null>(null);
  protected readonly borrador = signal<readonly EquipoBorrador[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly intento = signal(false);
  protected readonly guardando = signal(false);
  protected readonly errorGuardar = signal<string | null>(null);
  protected readonly hayCambios = signal(false);
  protected readonly elegido = signal<AlumnoMatrizDto | null>(null);
  protected readonly anuncio = signal('');
  private consecutivo = 0;

  protected readonly clave = computed(() => {
    const g = this.grupo();
    return g ? claveGrado(g.nivel, g.grado) : undefined;
  });
  protected readonly nombreGrupo = computed(() => {
    const g = this.grupo();
    return g ? nombreGrupo(g.grado, g.nombre) : '';
  });
  protected readonly editable = computed(() => !this.grupo()?.archivadoEn);
  protected readonly activos = computed(() =>
    (this.alumnos() ?? [])
      .filter((a) => !a.dadoDeBaja)
      .sort((a, b) => Number(a.numeroLista) - Number(b.numeroLista)),
  );
  private readonly porId = computed(() => new Map(this.activos().map((a) => [a.id, a])));
  private readonly conEquipo = computed(() => new Set(this.borrador().flatMap((e) => e.alumnoIds)));
  protected readonly sinEquipo = computed(() =>
    this.activos().filter((a) => !this.conEquipo().has(a.id)),
  );

  puedeSalir(): boolean | Promise<boolean> {
    if (!this.hayCambios() || !this.editable()) {
      return true;
    }
    const dialogo = this.dialogoSalir().nativeElement;
    dialogo.returnValue = '';
    dialogo.showModal();
    return new Promise((resolver) => (this.resolverSalida = resolver));
  }

  protected decidirSalida(salir: boolean): void {
    this.resolverSalida?.(salir);
    this.resolverSalida = null;
  }

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      const [grupo, alumnos, equipos] = await Promise.all([
        this.servicio.grupo(this.id()),
        this.servicio.alumnos(this.id()),
        this.servicio.equipos(this.id()),
      ]);
      this.grupo.set(grupo);
      this.pintarPortada(portadaDeGrupo(grupo));
      this.alumnos.set(alumnos);
      this.aplicar(equipos);
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  private aplicar(equipos: readonly EquipoDto[]): void {
    this.borrador.set(
      equipos.map((e) => ({
        clave: `equipo-${++this.consecutivo}`,
        id: e.id,
        nombre: e.nombre,
        alumnoIds: e.alumnoIds,
      })),
    );
    this.intento.set(false);
    this.hayCambios.set(false);
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  protected valor(evento: Event): string {
    return (evento.target as HTMLInputElement).value;
  }

  protected alumnosDe(e: EquipoBorrador): AlumnoMatrizDto[] {
    return e.alumnoIds
      .map((id) => this.porId().get(id))
      .filter((a): a is AlumnoMatrizDto => !!a)
      .sort((a, b) => Number(a.numeroLista) - Number(b.numeroLista));
  }

  protected libre(alumnoId: string): boolean {
    return !this.conEquipo().has(alumnoId);
  }

  /** Nombre vacío, largo o repetido (sin importar mayúsculas), como lo valida el servidor. */
  protected fallaNombre(e: EquipoBorrador): 'nombre' | 'repetido' | null {
    const nombre = e.nombre.trim();
    if (!nombre || nombre.length > LONGITUD_NOMBRE) {
      return 'nombre';
    }
    const iguales = this.borrador().filter(
      (x) => x.nombre.trim().toLocaleLowerCase('es-MX') === nombre.toLocaleLowerCase('es-MX'),
    );
    return iguales.length > 1 ? 'repetido' : null;
  }

  private cambiar(lista: readonly EquipoBorrador[]): void {
    this.borrador.set(lista);
    this.hayCambios.set(true);
    this.errorGuardar.set(null);
  }

  /** "Equipo N" con el primer número libre, listo para renombrar. */
  private crear(): EquipoBorrador {
    const usados = new Set(this.borrador().map((e) => e.nombre.trim().toLocaleLowerCase('es-MX')));
    let n = this.borrador().length + 1;
    const base = this.i18n.translate('equipos.nombreSugerido');
    while (usados.has(`${base} ${n}`.toLocaleLowerCase('es-MX'))) {
      n++;
    }
    return {
      clave: `equipo-${++this.consecutivo}`,
      id: null,
      nombre: `${base} ${n}`,
      alumnoIds: [],
    };
  }

  protected nuevo(): void {
    const equipo = this.crear();
    this.cambiar([...this.borrador(), equipo]);
    this.enfocar(`#${equipo.clave}-nombre`);
  }

  protected renombrar(clave: string, nombre: string): void {
    this.cambiar(this.borrador().map((e) => (e.clave === clave ? { ...e, nombre } : e)));
  }

  /** Sus integrantes quedan sin equipo; nada se pierde hasta guardar. */
  protected quitar(clave: string): void {
    const lista = this.borrador();
    const i = lista.findIndex((e) => e.clave === clave);
    this.cambiar(lista.filter((e) => e.clave !== clave));
    const vecino = this.borrador()[Math.min(i, this.borrador().length - 1)];
    this.enfocar(vecino ? `#${vecino.clave}-nombre` : 'h1');
  }

  protected elegir(alumno: AlumnoMatrizDto): void {
    this.elegido.set(alumno);
    this.hoja().abrir();
  }

  /** Un toque: sale de su equipo y entra al elegido (o a uno nuevo, o a "Sin equipo"). Sin animación. */
  protected mover(alumno: AlumnoMatrizDto, clave: string | null, nuevo = false): void {
    let destino = clave;
    let lista: EquipoBorrador[] = this.borrador().map((e) => ({
      ...e,
      alumnoIds: e.alumnoIds.filter((id) => id !== alumno.id),
    }));
    if (nuevo) {
      const creado = this.crear();
      lista = [...lista, creado];
      destino = creado.clave;
    }
    lista = lista.map((e) =>
      e.clave === destino ? { ...e, alumnoIds: [...e.alumnoIds, alumno.id] } : e,
    );
    this.cambiar(lista);
    const equipo = lista.find((e) => e.clave === destino);
    this.anuncio.set(
      this.i18n.translate(equipo ? 'equipos.movido' : 'equipos.movidoSinEquipo', {
        nombre: alumno.nombres,
        equipo: equipo?.nombre ?? '',
      }),
    );
    this.hoja().cerrar();
  }

  /** Al cerrar la hoja el foco vuelve al alumno, que ya está en su nuevo lugar. */
  protected enfocarElegido(): void {
    const a = this.elegido();
    if (a) {
      this.enfocar(`[data-alumno="${a.id}"]`);
    }
  }

  protected async guardar(): Promise<void> {
    this.intento.set(true);
    if (this.borrador().some((e) => this.fallaNombre(e))) {
      const primero = this.borrador().find((e) => this.fallaNombre(e))!;
      this.enfocar(`#${primero.clave}-nombre`);
      return;
    }
    this.guardando.set(true);
    this.errorGuardar.set(null);
    try {
      const guardados = await this.servicio.reemplazarEquipos(
        this.id(),
        this.borrador().map((e) => ({
          id: e.id,
          nombre: e.nombre.trim(),
          alumnoIds: [...e.alumnoIds],
        })),
      );
      this.aplicar(guardados);
      this.avisos.mostrar(this.i18n.translate('equipos.guardados'), 'exito');
    } catch (e) {
      this.errorGuardar.set(codigoDeError(e));
    } finally {
      this.guardando.set(false);
    }
  }
}
