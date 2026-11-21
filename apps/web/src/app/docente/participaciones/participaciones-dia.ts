import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
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
import { AlumnoMatrizDto } from '../../api/models/alumno-matriz-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { ParticipacionDto } from '../../api/models/participacion-dto';
import { PeriodoDto } from '../../api/models/periodo-dto';
import { codigoDeError } from '../../core/errores/problema';
import { ParticipacionEnCola } from '../../core/sincronizacion/almacen-local';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { crearEnfoque } from '../../shared/foco';
import { claveGrado } from '../../shared/grados';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { controlarPortada } from '../../shared/portada/portada.service';
import { diaLargo, moverDia } from '../asistencia/asistencia';
import { hoy } from '../evaluacion/matriz';
import { leerValorParticipacion } from '../grupos/configuracion-grupo';
import { portadaDeGrupo } from '../grupos/grupos.service';
import { PestanasGrupo } from '../grupos/pestanas-grupo';
import {
  VALORES_RAPIDOS,
  buscar,
  conPendientes,
  contar,
  puntos,
  ultimaDelDia,
} from './participaciones';
import { ParticipacionesService } from './participaciones.service';

interface FilaParticipacion {
  readonly alumno: AlumnoMatrizDto;
  readonly hoy: number;
  readonly unidad: number;
}

/**
 * SPEC-03, participaciones extra (RF-110, RF-111): busca al alumno y un toque le suma una. Se guarda en el dispositivo
 * y la cola la envía; no hay botón "Guardar". Sumar es una acción repetida: no se anima, solo cambian el número y el
 * fondo de la fila (reglas/07).
 */
@Component({
  selector: 'app-participaciones-dia',
  imports: [TranslocoDirective, RouterLink, NgTemplateOutlet, PestanasGrupo, Icono, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="pantalla pantalla--ancha"
      [attr.data-grado]="clave() ?? null"
      *transloco="let t; prefix: 'participaciones'"
    >
      <a class="volver" routerLink="/">
        <app-icono nombre="volver" />
        {{ t('misGrupos') }}
      </a>

      <app-pestanas-grupo [id]="id()" [grado]="clave()" />

      <h1 tabindex="-1">{{ t('titulo') }}</h1>

      <div class="dia">
        <button
          class="boton-icono"
          type="button"
          [attr.aria-label]="texto('asistencia.diaAnterior')"
          (click)="irA(moverDia(dia(), -1))"
        >
          <app-icono nombre="volver" />
        </button>
        <div class="fecha">
          <p class="fecha-texto" aria-live="polite">{{ etiquetaDia() }}</p>
          <p class="dato-suave cifras">
            @if (unidad(); as u) {
              {{ u.nombre }}
              @if (u.estado !== 'abierto') {
                · {{ texto('asistencia.estado.' + u.estado) }}
              }
            } @else if (periodos().length) {
              {{ texto('asistencia.fueraDeUnidad.corto') }}
            }
          </p>
        </div>
        <button
          class="boton-icono"
          type="button"
          [attr.aria-label]="texto('asistencia.diaSiguiente')"
          (click)="irA(moverDia(dia(), 1))"
        >
          <app-icono nombre="siguiente" />
        </button>
        <label class="elegir">
          <span class="solo-lector">{{ texto('asistencia.elegirFecha') }}</span>
          <input type="date" [value]="dia()" (change)="elegir($event)" />
        </label>
      </div>

      @if (grupo()?.archivadoEn) {
        <p class="aviso aviso--pendiente">{{ t('archivado') }}</p>
      } @else if (unidad()?.estado === 'cerrado') {
        <p class="aviso aviso--pendiente">{{ t('cerrada') }}</p>
      }

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (!listo()) {
        <div class="lista" role="status" [attr.aria-label]="t('cargando')">
          @for (i of [1, 2, 3, 4]; track i) {
            <span class="esqueleto fila-esqueleto" aria-hidden="true"></span>
          }
        </div>
      } @else if (!unidad()) {
        <section class="vacio" aria-labelledby="fuera-titulo">
          <h2 id="fuera-titulo">{{ texto('asistencia.fueraDeUnidad.titulo') }}</h2>
          <p class="dato-suave">{{ texto('asistencia.fueraDeUnidad.texto') }}</p>
          <button class="boton boton--secundario" type="button" (click)="irA(hoyDia)">
            {{ texto('asistencia.irAHoy') }}
          </button>
        </section>
      } @else if (activos().length === 0) {
        <section class="vacio" aria-labelledby="sin-alumnos">
          <h2 id="sin-alumnos">{{ texto('asistencia.sinAlumnos.titulo') }}</h2>
          <a class="boton" [routerLink]="['/grupos', id(), 'alumnos']">
            {{ texto('asistencia.sinAlumnos.accion') }}
          </a>
        </section>
      } @else {
        <div class="columnas">
          <div class="principal">
            <div class="valor solo-celular">
              <ng-container [ngTemplateOutlet]="valorActual" />
            </div>

            <div class="buscador" role="search">
              <label class="solo-lector" for="buscar-alumno">{{ t('buscar') }}</label>
              <app-icono class="lupa" nombre="persona" />
              <input
                id="buscar-alumno"
                type="search"
                autocomplete="off"
                autocapitalize="off"
                spellcheck="false"
                enterkeyhint="done"
                [placeholder]="t('buscar')"
                [value]="busqueda()"
                aria-describedby="buscar-ayuda"
                (input)="busqueda.set(valor($event))"
                (keydown.enter)="enterEnBuscador($event)"
                (keydown.escape)="limpiar()"
              />
              @if (busqueda()) {
                <button
                  class="boton-icono limpiar"
                  type="button"
                  [attr.aria-label]="t('limpiar')"
                  (click)="limpiar()"
                >
                  <app-icono nombre="cerrar" />
                </button>
              }
            </div>
            <p id="buscar-ayuda" class="solo-lector">{{ t('buscarAyuda') }}</p>

            @if (filas().length === 0) {
              <p class="dato-suave sin-coincidencias">
                {{ t('sinCoincidencias', { texto: busqueda() }) }}
              </p>
            }

            <ol class="lista" [class.doble]="filas().length > 9" [attr.aria-label]="t('lista')">
              @for (f of filas(); track f.alumno.id) {
                <li [class.con-hoy]="f.hoy > 0">
                  <button
                    class="fila"
                    type="button"
                    [attr.data-alumno]="f.alumno.id"
                    [disabled]="soloLectura()"
                    [attr.aria-label]="t('dar', { alumno: nombre(f.alumno) })"
                    [attr.aria-describedby]="'cuenta-' + f.alumno.id"
                    (click)="dar(f)"
                  >
                    <span class="numero cifras" aria-hidden="true">{{ f.alumno.numeroLista }}</span>
                    <span class="nombre" aria-hidden="true"
                      ><span class="apellidos">{{ f.alumno.apellidos }}</span>
                      {{ f.alumno.nombres }}</span
                    >
                    <span class="cuentas" [id]="'cuenta-' + f.alumno.id">
                      @if (f.hoy > 0) {
                        <span class="hoy cifras">{{
                          f.hoy === 1 ? t('hoyCuenta.uno') : t('hoyCuenta.otros', { n: f.hoy })
                        }}</span>
                      }
                      @if (f.unidad > 0) {
                        <span class="unidad cifras">{{
                          t('unidadCuenta', { n: f.unidad, puntos: puntosDe(f.unidad) })
                        }}</span>
                      }
                    </span>
                    <span class="sumar" aria-hidden="true"><app-icono nombre="mas" /></span>
                  </button>
                  @if (f.hoy > 0 && !soloLectura()) {
                    <button
                      class="quitar"
                      type="button"
                      [attr.aria-label]="t('quitarA', { alumno: nombre(f.alumno) })"
                      (click)="quitar(f)"
                    >
                      <app-icono nombre="menos" />
                      <span class="texto-quitar" aria-hidden="true">{{ t('quitar') }}</span>
                    </button>
                  }
                </li>
              }
            </ol>
            <p class="solo-lector" role="status">{{ anuncio() }}</p>

            <div class="solo-celular resumen-celular">
              <ng-container [ngTemplateOutlet]="resumen" />
            </div>
          </div>

          <aside class="lateral solo-escritorio" aria-labelledby="resumen-titulo">
            <h2 id="resumen-titulo">{{ t('resumen') }}</h2>
            <ng-container [ngTemplateOutlet]="valorActual" />
            <ng-container [ngTemplateOutlet]="resumen" />
          </aside>
        </div>
      }

      <ng-template #valorActual>
        <p class="valor-texto">
          <span class="cifras">{{ t('valor', { valor: valorTexto() }) }}</span>
          @if (!grupo()?.archivadoEn) {
            <button class="enlace" type="button" (click)="abrirValor()">
              {{ t('cambiarValor') }}
            </button>
          }
        </p>
      </ng-template>

      <ng-template #resumen>
        <dl class="conteo cifras">
          <div>
            <dt>{{ t('totalHoy') }}</dt>
            <dd>{{ totalHoy() }}</dd>
          </div>
          <div>
            <dt>{{ t('alumnosHoy') }}</dt>
            <dd>{{ alumnosHoy() }}</dd>
          </div>
        </dl>
        <p class="dato-suave como-cuenta">{{ t('comoCuenta') }}</p>
      </ng-template>

      <dialog #dialogoValor class="dialogo" closedby="any" aria-labelledby="dialogo-valor-titulo">
        <form class="contenido" (submit)="$event.preventDefault(); guardarValor()" novalidate>
          <h2 id="dialogo-valor-titulo">{{ t('valorDialogo.titulo') }}</h2>
          <p>{{ t('valorDialogo.texto') }}</p>
          <fieldset class="rapidos">
            <legend class="solo-lector">{{ t('valorDialogo.etiqueta') }}</legend>
            @for (v of valoresRapidos; track v) {
              <button
                class="chip cifras"
                type="button"
                [attr.aria-pressed]="nuevoValor() === v"
                (click)="nuevoValor.set(v)"
              >
                {{ v }}
              </button>
            }
          </fieldset>
          <div class="campo">
            <label for="valor-otro">{{ t('valorDialogo.otro') }}</label>
            <input
              id="valor-otro"
              class="cifras"
              inputmode="decimal"
              autocomplete="off"
              [value]="nuevoValor()"
              (input)="nuevoValor.set(valor($event))"
              [attr.aria-invalid]="intentoValor() && !valorValido()"
              [attr.aria-describedby]="intentoValor() && !valorValido() ? 'valor-error' : null"
            />
            @if (intentoValor() && !valorValido()) {
              <p id="valor-error" class="error">
                {{ texto('configuracion.errores.participacion') }}
              </p>
            }
          </div>
          @if (errorValor(); as codigo) {
            <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
          } @else if (!enLinea()) {
            <p class="aviso aviso--pendiente">{{ t('valorDialogo.enLinea') }}</p>
          }
          <div class="acciones">
            <button class="boton boton--secundario" type="button" (click)="dialogoValor.close()">
              {{ t('valorDialogo.cancelar') }}
            </button>
            <button
              class="boton"
              type="submit"
              [disabled]="!enLinea()"
              [appOcupado]="guardandoValor()"
            >
              {{ t('valorDialogo.guardar') }}
            </button>
          </div>
        </form>
      </dialog>
    </div>
  `,
  styles: `
    :host {
      display: block;
      container-type: inline-size;
    }

    .dia {
      display: grid;
      grid-template-columns: auto minmax(0, 1fr) auto auto;
      align-items: center;
      gap: var(--espacio-2);
      padding: var(--espacio-2);
      border-radius: var(--radio-lg);
      background: var(--color-superficie-hundida);
    }

    .fecha {
      display: grid;
      gap: var(--espacio-1);
      text-align: center;
    }

    .fecha-texto {
      font-weight: var(--peso-fuerte);
    }

    .fecha .dato-suave {
      font-size: var(--texto-sm);
    }

    .elegir input {
      inline-size: var(--tactil-minimo);
      min-block-size: var(--tactil-minimo);
      padding: 0;
      border: 0;
      border-radius: var(--radio-md);
      background: var(--color-superficie);
      color: transparent;
      font: inherit;
      cursor: pointer;
    }

    .elegir input::-webkit-datetime-edit {
      display: none;
    }

    .elegir input::-webkit-calendar-picker-indicator {
      margin: auto;
      cursor: pointer;
    }

    .elegir input:focus-visible {
      box-shadow: var(--foco);
      outline: none;
    }

    @media (min-width: 48rem) {
      .dia {
        max-inline-size: 36rem;
      }
    }

    @container (max-width: 24rem) {
      .dia {
        grid-template-columns: auto auto 1fr auto;
      }

      .fecha {
        grid-column: 1 / -1;
        grid-row: 1;
      }

      .elegir {
        grid-column: 4;
        justify-self: end;
      }
    }

    .principal {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      align-content: start;
      gap: var(--espacio-4);
    }

    .valor-texto {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--espacio-1) var(--espacio-3);
      font-weight: var(--peso-medio);
    }

    /* El buscador se queda arriba al bajar por la lista: en clase se busca una y otra vez. */
    .buscador {
      position: sticky;
      top: 0;
      z-index: 1;
      display: grid;
      grid-template-columns: auto minmax(0, 1fr) auto;
      align-items: center;
      gap: var(--espacio-2);
      padding-inline: var(--espacio-3) var(--espacio-1);
      border-radius: var(--radio-lg);
      background: var(--color-superficie-hundida);
      box-shadow: 0 0 0 var(--espacio-2) var(--color-fondo);
    }

    .buscador:focus-within {
      background: var(--color-superficie);
      box-shadow:
        0 0 0 var(--espacio-2) var(--color-fondo),
        var(--foco);
    }

    .lupa {
      color: var(--color-texto-suave);
    }

    .buscador input {
      min-block-size: 3rem;
      inline-size: 100%;
      padding: 0;
      border: 0;
      background: transparent;
      color: inherit;
      /* 16 px o más: iOS no acerca la página al enfocar. */
      font: inherit;
      font-size: max(1rem, var(--texto-md, 1rem));
      outline: none;
    }

    .buscador input::-webkit-search-cancel-button {
      display: none;
    }

    .buscador:not(:has(.limpiar)) {
      padding-inline-end: var(--espacio-3);
    }

    .sin-coincidencias {
      padding: var(--espacio-2) 0;
    }

    .lista {
      display: grid;
      margin: 0;
      padding: 0;
      list-style: none;
      border-top: 1px solid var(--color-divisoria);
    }

    .lista > li {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      align-items: stretch;
      border-bottom: 1px solid var(--color-divisoria);
      /* Cada renglón mide su propio ancho: en dos columnas también se acomoda. */
      container: fila / inline-size;
    }

    /* Quien ya participó hoy se distingue por el fondo del grado (DESIGN.md: el grado tiñe su pantalla). Sin
       transición: sumar es una acción repetida y no se anima (docs/reglas/07). */
    .lista > li.con-hoy {
      background: var(--grado-tinte);
    }

    @media (min-width: 75rem) {
      .lista.doble {
        display: block;
        columns: 2;
        column-gap: var(--espacio-6);
      }

      .lista.doble > li {
        break-inside: avoid;
      }
    }

    /* La fila entera suma; el número cambia sin animación (reglas/07: acción repetida). */
    .fila {
      display: grid;
      grid-template-columns: 2ch minmax(0, 1fr) auto auto;
      align-items: center;
      gap: var(--espacio-3);
      inline-size: 100%;
      min-block-size: 3.5rem;
      padding: var(--espacio-2) var(--espacio-3);
      border: 0;
      background: transparent;
      color: inherit;
      font: inherit;
      text-align: start;
      cursor: pointer;
      touch-action: manipulation;
      user-select: none;
      -webkit-tap-highlight-color: transparent;
    }

    .fila:disabled {
      cursor: default;
    }

    .fila:focus-visible,
    .quitar:focus-visible {
      position: relative;
      box-shadow: var(--foco);
      outline: none;
    }

    .numero {
      color: var(--color-texto-suave);
      text-align: end;
    }

    .nombre {
      overflow-wrap: break-word;
    }

    .apellidos {
      font-weight: var(--peso-medio);
    }

    .cuentas {
      display: grid;
      justify-items: end;
      gap: 0.125rem;
      text-align: end;
    }

    .hoy {
      min-inline-size: 4.5rem;
      padding: var(--espacio-1) var(--espacio-3);
      border-radius: var(--radio-pleno);
      background: var(--grado);
      color: var(--color-sobre-grado);
      font-size: var(--texto-sm);
      font-weight: var(--peso-fuerte);
      text-align: center;
    }

    .unidad {
      color: var(--color-texto-suave);
      font-size: var(--texto-xs);
    }

    .sumar {
      display: grid;
      place-items: center;
      inline-size: 2.25rem;
      block-size: 2.25rem;
      border-radius: var(--radio-pleno);
      background: var(--color-superficie);
      color: var(--grado-tinta);
    }

    .fila:disabled .sumar {
      visibility: hidden;
    }

    .fila:not(:disabled):active .sumar {
      background: var(--grado);
      color: var(--color-sobre-grado);
    }

    .quitar {
      display: inline-flex;
      align-items: center;
      gap: var(--espacio-1);
      min-inline-size: var(--tactil-minimo);
      padding: 0 var(--espacio-3);
      border: 0;
      border-inline-start: 1px solid var(--color-divisoria);
      background: transparent;
      color: var(--color-peligro-tinta);
      font: inherit;
      font-size: var(--texto-sm);
      font-weight: var(--peso-medio);
      cursor: pointer;
      touch-action: manipulation;
    }

    @media (hover: hover) and (pointer: fine) {
      .fila:not(:disabled):hover {
        background: var(--color-superficie-hundida);
      }

      .quitar:hover {
        background: var(--color-peligro-tinte);
      }
    }

    /* Angosto o con texto grande: el conteo baja de renglón y "Quitar una" queda solo con su ícono. */
    @container fila (max-width: 30rem) {
      .fila {
        grid-template-columns: 2ch minmax(0, 1fr) auto;
      }

      .cuentas {
        grid-column: 2;
        grid-row: 2;
        justify-items: start;
        text-align: start;
      }

      .sumar {
        grid-column: 3;
        grid-row: 1 / span 2;
      }

      .texto-quitar {
        position: absolute;
        width: 1px;
        height: 1px;
        overflow: hidden;
        clip-path: inset(50%);
      }
    }

    .conteo {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--espacio-3);
      margin: 0;
    }

    .conteo div {
      display: grid;
      gap: var(--espacio-1);
    }

    .conteo dt {
      color: var(--color-texto-suave);
      font-size: var(--texto-xs);
    }

    .conteo dd {
      margin: 0;
      font-size: var(--texto-lg);
      font-weight: var(--peso-fuerte);
    }

    .como-cuenta {
      font-size: var(--texto-sm);
    }

    .resumen-celular {
      display: grid;
      gap: var(--espacio-3);
    }

    button.enlace {
      min-block-size: var(--tactil-minimo);
      padding: 0;
      border: 0;
      background: none;
      font: inherit;
      cursor: pointer;
    }

    .rapidos {
      display: flex;
      flex-wrap: wrap;
      gap: var(--espacio-2);
      margin: 0;
      padding: 0;
      border: 0;
    }

    .vacio {
      display: grid;
      justify-items: start;
      gap: var(--espacio-3);
    }

    .vacio h2 {
      font-size: var(--texto-lg);
    }

    .fila-esqueleto {
      inline-size: 100%;
      block-size: 3.5rem;
      margin-block-end: var(--espacio-1);
    }
  `,
})
export class ParticipacionesDia implements OnInit {
  private readonly servicio = inject(ParticipacionesService);
  private readonly i18n = inject(TranslocoService);
  private readonly router = inject(Router);
  private readonly avisos = inject(AvisosService);
  private readonly pintarPortada = controlarPortada();
  private readonly enfocar = crearEnfoque();
  private leyendo: string | null = null;
  protected readonly enLinea = this.servicio.enLinea;
  protected readonly moverDia = moverDia;
  protected readonly hoyDia = hoy();
  protected readonly valoresRapidos = VALORES_RAPIDOS;

  readonly id = input.required<string>();
  /** El día (?fecha=2026-10-17); sin él, hoy. */
  readonly fecha = input<string>();

  protected readonly dialogoValor =
    viewChild.required<ElementRef<HTMLDialogElement>>('dialogoValor');
  protected readonly grupo = signal<GrupoDto | null>(null);
  protected readonly periodos = signal<readonly PeriodoDto[]>([]);
  protected readonly alumnos = signal<readonly AlumnoMatrizDto[] | null>(null);
  private readonly servidor = signal<{ unidad: string; lista: ParticipacionDto[] } | null>(null);
  private readonly pendientes = signal<readonly ParticipacionEnCola[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly anuncio = signal('');
  protected readonly busqueda = signal('');
  protected readonly nuevoValor = signal('');
  protected readonly intentoValor = signal(false);
  protected readonly guardandoValor = signal(false);
  protected readonly errorValor = signal<string | null>(null);

  protected readonly dia = computed(() => {
    const f = this.fecha();
    return f && /^\d{4}-\d{2}-\d{2}$/.test(f) ? f : this.hoyDia;
  });
  protected readonly clave = computed(() => {
    const g = this.grupo();
    return g ? claveGrado(g.nivel, g.grado) : undefined;
  });
  /** RN-122: la unidad que contiene el día. */
  protected readonly unidad = computed(() =>
    this.periodos().find((p) => p.inicio <= this.dia() && this.dia() <= p.fin),
  );
  /** RN-125: con el grupo archivado o la unidad cerrada no se dan ni se quitan. */
  protected readonly soloLectura = computed(
    () => !!this.grupo()?.archivadoEn || this.unidad()?.estado === 'cerrado',
  );
  protected readonly listo = computed(() => {
    if (!this.grupo() || !this.alumnos()) {
      return false;
    }
    const u = this.unidad();
    return !u || this.servidor()?.unidad === u.id;
  });
  protected readonly activos = computed(() => this.alumnos()?.filter((a) => !a.dadoDeBaja) ?? []);
  protected readonly valorTexto = computed(() =>
    Number(this.grupo()?.valorParticipacion ?? 0.1).toFixed(2),
  );
  /** Las de la unidad con lo que falta enviar encima (RF-111). */
  private readonly visibles = computed(() => {
    const u = this.unidad();
    const s = this.servidor();
    const enUnidad = (f: string): boolean => !!u && u.inicio <= f && f <= u.fin;
    return conPendientes(
      s && u && s.unidad === u.id ? s.lista : [],
      this.pendientes().filter((p) => enUnidad(p.fecha)),
    );
  });
  private readonly delDia = computed(() => contar(this.visibles(), this.dia()));
  private readonly deUnidad = computed(() => contar(this.visibles()));
  protected readonly filas = computed<FilaParticipacion[]>(() => {
    const hoyCuenta = this.delDia();
    const unidadCuenta = this.deUnidad();
    return buscar(this.activos(), this.busqueda()).map((alumno) => ({
      alumno,
      hoy: hoyCuenta.get(alumno.id) ?? 0,
      unidad: unidadCuenta.get(alumno.id) ?? 0,
    }));
  });
  protected readonly totalHoy = computed(() =>
    [...this.delDia().values()].reduce((a, b) => a + b, 0),
  );
  protected readonly alumnosHoy = computed(() => this.delDia().size);
  protected readonly valorValido = computed(
    () => leerValorParticipacion(this.nuevoValor()) !== null,
  );
  protected readonly etiquetaDia = computed(() => {
    const texto = diaLargo(this.dia());
    return this.dia() === this.hoyDia
      ? this.i18n.translate('asistencia.hoy', { dia: texto.toLowerCase() })
      : texto;
  });

  constructor() {
    effect(() => {
      const u = this.unidad();
      if (u && this.grupo()) {
        untracked(() => void this.cargarUnidad(u));
      }
    });
    const dejar = this.servicio.alEnviar(({ pendiente, codigo }) => {
      if (pendiente.grupoId !== this.id()) {
        return;
      }
      this.pendientes.update((l) => l.filter((p) => p.id !== pendiente.id));
      const s = this.servidor();
      if (s && !codigo) {
        const lista =
          pendiente.operacion === 'dar'
            ? [
                ...s.lista.filter((p) => p.id !== pendiente.id),
                { id: pendiente.id, alumnoId: pendiente.alumnoId, fecha: pendiente.fecha },
              ]
            : s.lista.filter((p) => p.id !== pendiente.id);
        this.servidor.set({ unidad: s.unidad, lista });
      }
      if (codigo) {
        this.avisos.mostrar(this.texto('errores.' + codigo), 'error');
      }
    });
    inject(DestroyRef).onDestroy(dejar);
  }

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      const [grupo, periodos, alumnos, pendientes] = await Promise.all([
        this.servicio.grupo(this.id()),
        this.servicio.periodos(this.id()),
        this.servicio.alumnos(this.id()),
        this.servicio.pendientes(this.id()),
      ]);
      this.pintarPortada(portadaDeGrupo(grupo));
      this.periodos.set(periodos);
      this.alumnos.set(alumnos);
      this.pendientes.set(pendientes);
      this.grupo.set(grupo);
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  private async cargarUnidad(unidad: PeriodoDto): Promise<void> {
    if (this.leyendo === unidad.id || this.servidor()?.unidad === unidad.id) {
      return;
    }
    this.leyendo = unidad.id;
    try {
      const lista = await this.servicio.participaciones(this.id(), unidad);
      this.servidor.set({ unidad: unidad.id, lista });
    } catch (e) {
      this.error.set(codigoDeError(e));
    } finally {
      this.leyendo = null;
    }
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  protected valor(evento: Event): string {
    return (evento.target as HTMLInputElement).value;
  }

  protected nombre(a: AlumnoMatrizDto): string {
    return `${a.numeroLista}, ${a.apellidos} ${a.nombres}`;
  }

  protected puntosDe(cantidad: number): string {
    return puntos(cantidad, this.grupo()?.valorParticipacion ?? '0.10');
  }

  protected irA(fecha: string): void {
    void this.router.navigate([], {
      queryParams: { fecha: fecha === this.hoyDia ? null : fecha },
      replaceUrl: true,
    });
  }

  protected elegir(evento: Event): void {
    const valor = this.valor(evento);
    if (valor) {
      this.irA(valor);
    }
  }

  protected limpiar(): void {
    this.busqueda.set('');
    this.enfocar('#buscar-alumno');
  }

  /** En computadora: escribir y Enter suma una al único alumno que coincide, y deja el buscador listo para otro. */
  protected enterEnBuscador(evento: Event): void {
    evento.preventDefault();
    const filas = this.filas();
    if (filas.length === 1 && this.busqueda().trim()) {
      void this.dar(filas[0]).then(() => this.busqueda.set(''));
    }
  }

  /** RF-110: primero en pantalla y en el dispositivo; la cola la manda cuando hay red. */
  protected async dar(fila: FilaParticipacion): Promise<void> {
    if (this.soloLectura() || !this.unidad()) {
      return;
    }
    const provisional: ParticipacionEnCola = {
      id: crypto.randomUUID(),
      grupoId: this.id(),
      alumnoId: fila.alumno.id,
      fecha: this.dia(),
      creado: Date.now(),
      operacion: 'dar',
    };
    this.pendientes.update((l) => [...l, provisional]);
    this.anuncio.set(
      this.i18n.translate('participaciones.anuncioDar', {
        alumno: `${fila.alumno.apellidos} ${fila.alumno.nombres}`,
        n: fila.hoy + 1,
      }),
    );
    try {
      await this.servicio.dar(
        provisional.grupoId,
        provisional.alumnoId,
        provisional.fecha,
        provisional.id,
      );
    } catch {
      this.pendientes.update((l) => l.filter((p) => p.id !== provisional.id));
      this.avisos.mostrar(this.texto('participaciones.sinGuardar'), 'error');
    }
  }

  /** Quita la más reciente del día; si aún no se enviaba, solo sale de la cola. */
  protected async quitar(fila: FilaParticipacion): Promise<void> {
    const ultima = ultimaDelDia(this.visibles(), fila.alumno.id, this.dia());
    if (!ultima || this.soloLectura()) {
      return;
    }
    const antes = this.pendientes();
    this.pendientes.update((l) =>
      ultima.pendiente
        ? l.filter((p) => p.id !== ultima.id)
        : [
            ...l,
            {
              id: ultima.id,
              grupoId: this.id(),
              alumnoId: ultima.alumnoId,
              fecha: ultima.fecha,
              creado: Date.now(),
              operacion: 'quitar' as const,
            },
          ],
    );
    this.anuncio.set(
      this.i18n.translate('participaciones.anuncioQuitar', {
        alumno: `${fila.alumno.apellidos} ${fila.alumno.nombres}`,
        n: fila.hoy - 1,
      }),
    );
    if (fila.hoy === 1) {
      // El botón desaparece: el foco vuelve a la fila del alumno.
      this.enfocar(`button.fila[data-alumno="${fila.alumno.id}"]`);
    }
    try {
      await this.servicio.quitar({
        id: ultima.id,
        grupoId: this.id(),
        alumnoId: ultima.alumnoId,
        fecha: ultima.fecha,
      });
    } catch {
      this.pendientes.set(antes);
      this.avisos.mostrar(this.texto('participaciones.sinGuardar'), 'error');
    }
  }

  protected abrirValor(): void {
    this.nuevoValor.set(this.valorTexto());
    this.intentoValor.set(false);
    this.errorValor.set(null);
    this.dialogoValor().nativeElement.showModal();
  }

  protected async guardarValor(): Promise<void> {
    this.intentoValor.set(true);
    const valor = leerValorParticipacion(this.nuevoValor());
    const grupo = this.grupo();
    if (!valor || !grupo) {
      this.enfocar('#valor-otro');
      return;
    }
    this.guardandoValor.set(true);
    this.errorValor.set(null);
    try {
      this.grupo.set(await this.servicio.cambiarValor(grupo, valor));
      this.dialogoValor().nativeElement.close();
      this.avisos.mostrar(
        this.i18n.translate('participaciones.valorDialogo.listo', { valor: this.valorTexto() }),
      );
    } catch (e) {
      this.errorValor.set(codigoDeError(e));
    } finally {
      this.guardandoValor.set(false);
    }
  }
}
