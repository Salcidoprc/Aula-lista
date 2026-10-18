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
import { PeriodoDto } from '../../api/models/periodo-dto';
import { TomaAsistenciaDto } from '../../api/models/toma-asistencia-dto';
import { EstadoAsistencia } from '../../core/calculo/calculo';
import { codigoDeError } from '../../core/errores/problema';
import { AsistenciaEnCola } from '../../core/sincronizacion/almacen-local';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { claveGrado } from '../../shared/grados';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { controlarPortada } from '../../shared/portada/portada.service';
import { hoy } from '../evaluacion/matriz';
import { portadaDeGrupo } from '../grupos/grupos.service';
import { PestanasGrupo } from '../grupos/pestanas-grupo';
import { AsistenciaService } from './asistencia.service';
import {
  ESTADOS,
  TECLAS,
  conPendientes,
  diaLargo,
  moverDia,
  resumen,
  siguienteEstado,
} from './asistencia';

interface FilaLista {
  readonly alumno: AlumnoMatrizDto;
  readonly estado: EstadoAsistencia | null;
}

/**
 * SPEC-10, pasar lista (RF-100, RF-103): la lista del día con un toque por alumno. "Todos presentes" crea la
 * toma (RN-101) y cada toque recorre presente, falta, retardo y justificada. Se guarda en el dispositivo y la
 * cola la envía; no hay botón "Guardar". Nada se anima al marcar salvo el fondo de la fila (reglas/07).
 */
@Component({
  selector: 'app-pase-lista',
  imports: [TranslocoDirective, RouterLink, NgTemplateOutlet, PestanasGrupo, Icono, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="pantalla pantalla--ancha"
      [attr.data-grado]="clave() ?? null"
      *transloco="let t; prefix: 'asistencia'"
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
          [attr.aria-label]="t('diaAnterior')"
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
                · {{ t('estado.' + u.estado) }}
              }
            } @else if (periodos().length) {
              {{ t('fueraDeUnidad.corto') }}
            }
          </p>
        </div>
        <button
          class="boton-icono"
          type="button"
          [attr.aria-label]="t('diaSiguiente')"
          (click)="irA(moverDia(dia(), 1))"
        >
          <app-icono nombre="siguiente" />
        </button>
        <label class="elegir">
          <span class="solo-lector">{{ t('elegirFecha') }}</span>
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
          <h2 id="fuera-titulo">{{ t('fueraDeUnidad.titulo') }}</h2>
          <p class="dato-suave">{{ t('fueraDeUnidad.texto') }}</p>
          <button class="boton boton--secundario" type="button" (click)="irA(hoyDia)">
            {{ t('irAHoy') }}
          </button>
        </section>
      } @else if (filas().length === 0) {
        <section class="vacio" aria-labelledby="sin-alumnos">
          <h2 id="sin-alumnos">{{ t('sinAlumnos.titulo') }}</h2>
          <p class="dato-suave">{{ t('sinAlumnos.texto') }}</p>
          <a class="boton" [routerLink]="['/grupos', id(), 'alumnos']">
            {{ t('sinAlumnos.accion') }}
          </a>
        </section>
      } @else {
        <div class="columnas">
          <div class="principal">
            @if (!hayToma()) {
              <div class="sin-toma">
                <p>
                  {{ dia() === hoyDia ? t('sinToma.hoy') : t('sinToma.otroDia') }}
                  <span class="dato-suave">{{ t('sinToma.ayuda') }}</span>
                </p>
              </div>
            }

            <ol class="lista" [attr.aria-label]="t('lista')">
              @for (f of filas(); track f.alumno.id) {
                <li>
                  <button
                    class="fila"
                    type="button"
                    [attr.data-estado]="f.estado"
                    [attr.data-alumno]="f.alumno.id"
                    [disabled]="soloLectura()"
                    [attr.aria-describedby]="'ayuda-fila'"
                    (click)="tocar(f)"
                    (keydown)="tecla($event, f)"
                  >
                    <span class="numero cifras">{{ f.alumno.numeroLista }}</span>
                    <span class="nombre"
                      ><span class="apellidos">{{ f.alumno.apellidos }}</span>
                      {{ f.alumno.nombres }}</span
                    >
                    <span class="solo-lector">,</span>
                    @if (f.estado) {
                      <span class="estado">{{ t('estados.' + f.estado) }}</span>
                    } @else {
                      <span class="solo-lector">{{ t('sinMarcar') }}</span>
                    }
                  </button>
                </li>
              }
            </ol>
            <p id="ayuda-fila" class="solo-lector">{{ t('ayudaFila') }}</p>
            <p class="solo-lector" role="status">{{ anuncio() }}</p>

            <div class="pie-accion solo-celular">
              <ng-container [ngTemplateOutlet]="conteo" />
              @if (!hayToma() && !soloLectura()) {
                <button
                  class="boton boton--ancho"
                  type="button"
                  [appOcupado]="marcando()"
                  (click)="todosPresentes()"
                >
                  {{ t('todosPresentes') }}
                </button>
              }
            </div>
          </div>

          <aside class="lateral solo-escritorio" aria-labelledby="resumen-titulo">
            <h2 id="resumen-titulo">{{ t('resumen') }}</h2>
            <ng-container [ngTemplateOutlet]="conteo" />
            @if (!hayToma() && !soloLectura()) {
              <button
                class="boton"
                type="button"
                [appOcupado]="marcando()"
                (click)="todosPresentes()"
              >
                {{ t('todosPresentes') }}
              </button>
            }
            <ng-container [ngTemplateOutlet]="acciones" />
          </aside>
        </div>

        <div class="solo-celular acciones-celular">
          <ng-container [ngTemplateOutlet]="acciones" />
        </div>
      }

      <ng-template #conteo>
        @if (hayToma()) {
          @let r = cuenta();
          <dl class="conteo cifras">
            @for (e of estados; track e) {
              <div [attr.data-estado]="e">
                <dt>{{ t('plural.' + e) }}</dt>
                <dd>{{ r[e] }}</dd>
              </div>
            }
          </dl>
        }
      </ng-template>

      <ng-template #acciones>
        <a
          class="enlace"
          [routerLink]="['/grupos', id(), 'asistencia', 'historial']"
          [queryParams]="{ unidad: unidad()?.id }"
          >{{ t('historial') }}</a
        >
        @if (tomaServidor() && !soloLectura()) {
          <button class="enlace peligro" type="button" (click)="dialogoBorrar.showModal()">
            {{ t('borrar.accion') }}
          </button>
        }
      </ng-template>

      <dialog #dialogoBorrar class="dialogo" closedby="any" aria-labelledby="dialogo-borrar-titulo">
        <div class="contenido">
          <h2 id="dialogo-borrar-titulo">{{ t('borrar.titulo', { dia: etiquetaDia() }) }}</h2>
          <p>{{ t('borrar.texto') }}</p>
          @if (errorBorrar(); as codigo) {
            <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
          }
          <div class="acciones">
            <button class="boton boton--secundario" type="button" (click)="dialogoBorrar.close()">
              {{ t('cancelar') }}
            </button>
            <button
              class="boton boton--peligro"
              type="button"
              [appOcupado]="borrando()"
              (click)="borrar()"
            >
              {{ t('borrar.confirmar') }}
            </button>
          </div>
        </div>
      </dialog>
    </div>
  `,
  styles: `
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

    /* El selector nativo: un cuadro de 44 px con el ícono del calendario del sistema. */
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

    .principal {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      align-content: start;
      gap: var(--espacio-4);
    }

    .sin-toma {
      padding: var(--espacio-3) var(--espacio-4);
      border-radius: var(--radio-lg);
      background: var(--grado-tinte);
      color: var(--grado-tinta);
      font-weight: var(--peso-medio);
    }

    .sin-toma .dato-suave {
      display: block;
      color: inherit;
      font-weight: var(--peso-normal);
      font-size: var(--texto-sm);
    }

    .lista {
      display: grid;
      margin: 0;
      padding: 0;
      list-style: none;
      border-top: 1px solid var(--color-divisoria);
    }

    .lista > li {
      border-bottom: 1px solid var(--color-divisoria);
    }

    /* La fila entera es el botón; al marcar solo cambia su fondo (DESIGN.md, pase de lista). */
    .fila {
      display: grid;
      grid-template-columns: 2ch minmax(0, 1fr) auto;
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
      transition: background-color var(--dur-small) var(--ease-out);
      scroll-margin-block-end: calc(var(--alto-pie-accion, 0px) + var(--espacio-4));
    }

    .fila:disabled {
      cursor: default;
    }

    .fila:focus-visible {
      position: relative;
      box-shadow: var(--foco);
      outline: none;
    }

    .numero {
      color: var(--color-texto-suave);
      text-align: end;
    }

    .nombre {
      overflow-wrap: anywhere;
    }

    .apellidos {
      font-weight: var(--peso-medio);
    }

    .estado {
      min-inline-size: 6.5rem;
      padding: var(--espacio-1) var(--espacio-3);
      border-radius: var(--radio-pleno);
      background: var(--color-superficie-hundida);
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
      font-weight: var(--peso-medio);
      text-align: center;
    }

    .fila[data-estado='presente'] .estado {
      background: var(--grado-tinte);
      color: var(--grado-tinta);
    }

    .fila[data-estado='falta'] {
      background: var(--color-peligro-tinte);
    }

    .fila[data-estado='retardo'] {
      background: var(--color-aviso-tinte);
    }

    .fila[data-estado='justificada'] {
      background: var(--color-superficie-hundida);
    }

    .fila[data-estado='falta'] .estado {
      background: var(--color-superficie);
      color: var(--color-peligro-tinta);
      font-weight: var(--peso-fuerte);
    }

    .fila[data-estado='retardo'] .estado {
      background: var(--color-superficie);
      color: var(--color-aviso-tinta);
      font-weight: var(--peso-fuerte);
    }

    .fila[data-estado='justificada'] .estado {
      background: var(--color-superficie);
      color: var(--color-texto-suave);
    }

    @media (hover: hover) and (pointer: fine) {
      .fila:not(:disabled):not([data-estado]):hover,
      .fila:not(:disabled)[data-estado='presente']:hover {
        background: var(--color-superficie-hundida);
      }
    }

    /* 320 px o texto al 200 %: la etiqueta baja de renglón y el nombre conserva el ancho. */
    .lista {
      container-type: inline-size;
    }

    @container (max-width: 20rem) {
      .fila {
        grid-template-columns: 2ch minmax(0, 1fr);
      }

      .estado {
        grid-column: 2;
        justify-self: start;
      }
    }

    .conteo {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: var(--espacio-2);
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

    .conteo [data-estado='falta'] dd {
      color: var(--color-peligro-tinta);
    }

    .conteo [data-estado='retardo'] dd {
      color: var(--color-aviso-tinta);
    }

    .acciones-celular {
      display: grid;
      justify-items: start;
      gap: var(--espacio-3);
    }

    button.enlace {
      padding: 0;
      border: 0;
      background: none;
      font: inherit;
      cursor: pointer;
      min-block-size: var(--tactil-minimo);
      text-align: start;
    }

    .enlace.peligro {
      color: var(--color-peligro-tinta);
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
export class PaseLista implements OnInit {
  private readonly servicio = inject(AsistenciaService);
  private readonly i18n = inject(TranslocoService);
  private readonly router = inject(Router);
  private readonly avisos = inject(AvisosService);
  private readonly anfitrion = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly pintarPortada = controlarPortada();
  private leyendo: string | null = null;
  protected readonly moverDia = moverDia;
  protected readonly estados = ESTADOS;
  protected readonly hoyDia = hoy();

  readonly id = input.required<string>();
  /** El día de la lista (?fecha=2026-10-17); sin él, hoy. */
  readonly fecha = input<string>();

  protected readonly dialogoBorrar =
    viewChild.required<ElementRef<HTMLDialogElement>>('dialogoBorrar');
  protected readonly grupo = signal<GrupoDto | null>(null);
  protected readonly periodos = signal<readonly PeriodoDto[]>([]);
  protected readonly alumnos = signal<readonly AlumnoMatrizDto[] | null>(null);
  /** Tomas del servidor de la unidad leída, y cuál es. */
  private readonly servidor = signal<{ unidad: string; tomas: TomaAsistenciaDto[] } | null>(null);
  private readonly pendientes = signal<readonly AsistenciaEnCola[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly marcando = signal(false);
  protected readonly borrando = signal(false);
  protected readonly errorBorrar = signal<string | null>(null);
  protected readonly anuncio = signal('');

  protected readonly dia = computed(() => {
    const f = this.fecha();
    return f && /^\d{4}-\d{2}-\d{2}$/.test(f) ? f : this.hoyDia;
  });
  protected readonly clave = computed(() => {
    const g = this.grupo();
    return g ? claveGrado(g.nivel, g.grado) : undefined;
  });
  /** RN-100: la unidad que contiene el día; sin ella no hay lista. */
  protected readonly unidad = computed(() =>
    this.periodos().find((p) => p.inicio <= this.dia() && this.dia() <= p.fin),
  );
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
  private readonly activos = computed(() => this.alumnos()?.filter((a) => !a.dadoDeBaja) ?? []);
  protected readonly tomaServidor = computed(() =>
    this.servidor()?.tomas.find((t) => t.fecha === this.dia()),
  );
  /** La lista del día como queda con lo que falta enviar (RF-103). */
  private readonly toma = computed(() => {
    const dia = this.dia();
    const servidor = this.tomaServidor();
    return conPendientes(
      servidor ? [servidor] : [],
      this.pendientes().filter((p) => p.fecha === dia),
      this.activos().map((a) => a.id),
    )[0];
  });
  protected readonly hayToma = computed(() => !!this.toma());
  /** Con toma, sus alumnos (RN-105); sin ella, los activos, en orden de lista. */
  protected readonly filas = computed<FilaLista[]>(() => {
    const toma = this.toma();
    const todos = this.alumnos() ?? [];
    const lista = toma ? todos.filter((a) => toma.estados.has(a.id)) : this.activos();
    return lista.map((alumno) => ({ alumno, estado: toma?.estados.get(alumno.id) ?? null }));
  });
  protected readonly cuenta = computed(() => resumen(this.toma()?.estados.values() ?? []));
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
        untracked(() => void this.cargarTomas(u));
      }
    });
    const dejar = this.servicio.alEnviar(({ pendiente, toma, codigo }) => {
      if (pendiente.grupoId !== this.id()) {
        return;
      }
      this.pendientes.update((l) => l.filter((p) => p.id !== pendiente.id));
      const s = this.servidor();
      if (toma && s) {
        const u = this.periodos().find((p) => p.id === s.unidad);
        if (u && u.inicio <= toma.fecha && toma.fecha <= u.fin) {
          this.servidor.set({
            unidad: s.unidad,
            tomas: [...s.tomas.filter((t) => t.fecha !== toma.fecha), toma],
          });
        }
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

  private async cargarTomas(unidad: PeriodoDto): Promise<void> {
    if (this.leyendo === unidad.id || this.servidor()?.unidad === unidad.id) {
      return;
    }
    this.leyendo = unidad.id;
    try {
      const tomas = await this.servicio.tomas(this.id(), unidad);
      this.servidor.set({ unidad: unidad.id, tomas });
    } catch (e) {
      this.error.set(codigoDeError(e));
    } finally {
      this.leyendo = null;
    }
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  protected irA(fecha: string): void {
    void this.router.navigate([], {
      queryParams: { fecha: fecha === this.hoyDia ? null : fecha },
      replaceUrl: true,
    });
  }

  protected elegir(evento: Event): void {
    const valor = (evento.target as HTMLInputElement).value;
    if (valor) {
      this.irA(valor);
    }
  }

  protected todosPresentes(): Promise<void> {
    return this.marcar([]);
  }

  /** RN-101: el toque recorre el ciclo; sin toma, la crea con todos presentes y ese alumno en falta. */
  protected tocar(fila: FilaLista): Promise<void> {
    return this.poner(fila, siguienteEstado(fila.estado ?? 'presente'));
  }

  private poner(fila: FilaLista, estado: EstadoAsistencia): Promise<void> {
    const a = fila.alumno;
    this.anuncio.set(
      `${a.numeroLista}, ${a.apellidos} ${a.nombres}, ${this.texto('asistencia.estados.' + estado)}`,
    );
    return this.marcar([{ alumnoId: a.id, estado }]);
  }

  /** Primero en pantalla y en el dispositivo; la cola lo manda cuando hay red. */
  private async marcar(alumnos: { alumnoId: string; estado: EstadoAsistencia }[]): Promise<void> {
    if (this.soloLectura() || !this.unidad()) {
      return;
    }
    const provisional: AsistenciaEnCola = {
      id: crypto.randomUUID(),
      grupoId: this.id(),
      fecha: this.dia(),
      creado: Date.now(),
      alumnos,
    };
    this.pendientes.update((l) => [...l, provisional]);
    this.marcando.set(true);
    try {
      await this.servicio.marcar(provisional.grupoId, provisional.fecha, alumnos, provisional.id);
    } catch {
      this.pendientes.update((l) => l.filter((p) => p.id !== provisional.id));
      this.avisos.mostrar(this.texto('asistencia.sinGuardar'), 'error');
    } finally {
      this.marcando.set(false);
    }
  }

  /** Flechas entre filas; P, F, R y J ponen el estado directo. */
  protected tecla(evento: KeyboardEvent, fila: FilaLista): void {
    if (evento.ctrlKey || evento.metaKey || evento.altKey) {
      return;
    }
    const actual = evento.currentTarget as HTMLButtonElement;
    const botones = [
      ...this.anfitrion.nativeElement.querySelectorAll<HTMLButtonElement>('button.fila'),
    ];
    const i = botones.indexOf(actual);
    const destino =
      evento.key === 'ArrowDown'
        ? botones[i + 1]
        : evento.key === 'ArrowUp'
          ? botones[i - 1]
          : evento.key === 'Home'
            ? botones[0]
            : evento.key === 'End'
              ? botones.at(-1)
              : undefined;
    if (destino) {
      evento.preventDefault();
      destino.focus();
      return;
    }
    const estado = TECLAS[evento.key.toLowerCase()];
    if (estado && !actual.disabled) {
      evento.preventDefault();
      void this.poner(fila, estado);
    }
  }

  protected async borrar(): Promise<void> {
    const dia = this.dia();
    this.borrando.set(true);
    this.errorBorrar.set(null);
    try {
      await this.servicio.borrar(this.id(), dia);
      this.servidor.update((s) => s && { ...s, tomas: s.tomas.filter((t) => t.fecha !== dia) });
      this.dialogoBorrar().nativeElement.close();
      this.avisos.mostrar(this.texto('asistencia.borrar.listo'));
    } catch (e) {
      this.errorBorrar.set(codigoDeError(e));
    } finally {
      this.borrando.set(false);
    }
  }
}
