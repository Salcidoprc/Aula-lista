import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { AlumnoMatrizDto } from '../../api/models/alumno-matriz-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { PeriodoDto } from '../../api/models/periodo-dto';
import { TomaAsistenciaDto } from '../../api/models/toma-asistencia-dto';
import { EstadoAsistencia } from '../../core/calculo/calculo';
import { codigoDeError } from '../../core/errores/problema';
import { AsistenciaEnCola } from '../../core/sincronizacion/almacen-local';
import { claveGrado } from '../../shared/grados';
import { Icono } from '../../shared/iconos';
import { forma } from '../../shared/plural';
import { controlarPortada } from '../../shared/portada/portada.service';
import { hoy, unidadDeHoy } from '../evaluacion/matriz';
import { portadaDeGrupo } from '../grupos/grupos.service';
import { PestanasGrupo } from '../grupos/pestanas-grupo';
import { AsistenciaService } from './asistencia.service';
import { Resumen, conPendientes, diaCorto, diaLargo, porcentaje, resumen } from './asistencia';

interface RenglonHistorial {
  readonly alumno: AlumnoMatrizDto;
  /** Estado por fecha; `null` si el alumno no contaba ese día (RN-105). */
  readonly estados: readonly (EstadoAsistencia | null)[];
  readonly porcentaje: number | null;
  readonly cuenta: Resumen;
}

/**
 * SPEC-10, historial de la unidad (RF-101, RN-103): en escritorio, alumnos por fechas con la letra de cada día y
 * su porcentaje; en el celular, una lista por alumno y los días con lista para abrir uno y corregirlo.
 */
@Component({
  selector: 'app-historial-asistencia',
  imports: [TranslocoDirective, RouterLink, PestanasGrupo, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="pantalla pantalla--ancha"
      [attr.data-grado]="clave() ?? null"
      *transloco="let t; prefix: 'asistencia'"
    >
      <a class="volver" [routerLink]="['/grupos', id(), 'asistencia']">
        <app-icono nombre="volver" />
        {{ t('titulo') }}
      </a>

      <app-pestanas-grupo [id]="id()" [grado]="clave()" />

      <hgroup>
        <h1 tabindex="-1">{{ t('historialTitulo') }}</h1>
        @if (periodo(); as p) {
          <p class="dato-suave cifras">
            {{ p.nombre }} · {{ t('dias', { cantidad: tomas().length }) }}
          </p>
        }
      </hgroup>

      @if (periodos().length > 1) {
        <nav class="unidades" [attr.aria-label]="t('unidades')">
          @for (p of periodos(); track p.id) {
            <a
              class="chip"
              [routerLink]="[]"
              [queryParams]="{ unidad: p.id }"
              [attr.aria-current]="p.id === periodo()?.id ? 'page' : null"
              >{{ p.nombre }}</a
            >
          }
        </nav>
      }

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (!listo()) {
        <span
          class="esqueleto marco-esqueleto"
          role="status"
          [attr.aria-label]="t('cargando')"
        ></span>
      } @else if (tomas().length === 0) {
        <section class="vacio" aria-labelledby="sin-dias">
          <h2 id="sin-dias">{{ t('sinDias.titulo') }}</h2>
          <p class="dato-suave">{{ t('sinDias.texto') }}</p>
          <a class="boton" [routerLink]="['/grupos', id(), 'asistencia']">{{ t('pasarLista') }}</a>
        </section>
      } @else {
        <p class="dato-suave leyenda solo-escritorio">{{ t('leyenda') }}</p>

        <div class="marco solo-escritorio" tabindex="0" [attr.aria-label]="t('historialTitulo')">
          <table>
            <caption class="solo-lector">
              {{
                t('historialTitulo')
              }}
              ·
              {{
                periodo()?.nombre
              }}
            </caption>
            <thead>
              <tr>
                <th scope="col" class="fija">{{ t('alumno') }}</th>
                @for (toma of tomas(); track toma.fecha) {
                  <th scope="col" class="dia-columna">
                    <a
                      class="cifras"
                      [routerLink]="['/grupos', id(), 'asistencia']"
                      [queryParams]="{ fecha: toma.fecha }"
                      >{{ corto(toma.fecha)
                      }}<span class="solo-lector">, {{ largo(toma.fecha) }}</span></a
                    >
                  </th>
                }
                <th scope="col" class="total">%</th>
              </tr>
            </thead>
            <tbody>
              @for (r of renglones(); track r.alumno.id) {
                <tr [class.baja]="r.alumno.dadoDeBaja">
                  <th scope="row" class="fija">
                    <span class="quien">
                      <span class="numero cifras">{{ r.alumno.numeroLista }}</span>
                      <span
                        class="nombre"
                        [attr.title]="r.alumno.apellidos + ' ' + r.alumno.nombres"
                        >{{ r.alumno.apellidos }} {{ r.alumno.nombres }}</span
                      >
                    </span>
                  </th>
                  @for (e of r.estados; track $index) {
                    <td [attr.data-estado]="e">
                      @if (e) {
                        <span aria-hidden="true">{{ t('letras.' + e) }}</span>
                        <span class="solo-lector">{{ t('estados.' + e) }}</span>
                      }
                    </td>
                  }
                  <td class="total cifras">{{ textoPorcentaje(r.porcentaje) }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <div class="solo-celular celular">
          <ol class="alumnos">
            @for (r of renglones(); track r.alumno.id) {
              <li [class.baja]="r.alumno.dadoDeBaja">
                <span class="numero cifras">{{ r.alumno.numeroLista }}</span>
                <span class="nombre">
                  <span class="apellidos">{{ r.alumno.apellidos }}</span>
                  {{ r.alumno.nombres }}
                  <span class="detalle dato-suave cifras">{{ detalle(r.cuenta) }}</span>
                </span>
                <span class="porcentaje cifras">{{ textoPorcentaje(r.porcentaje) }}</span>
              </li>
            }
          </ol>

          <section class="dias" aria-labelledby="dias-titulo">
            <h2 id="dias-titulo">{{ t('diasConLista') }}</h2>
            <ol class="filas">
              @for (toma of tomasRecientes(); track toma.fecha) {
                <li>
                  <a
                    class="dia-enlace"
                    [routerLink]="['/grupos', id(), 'asistencia']"
                    [queryParams]="{ fecha: toma.fecha }"
                  >
                    <span class="nombre-dia">{{ largo(toma.fecha) }}</span>
                    <span class="dato-suave cifras">{{
                      t('detalleDia', {
                        presentes: toma.cuenta.presente,
                        faltas: toma.cuenta.falta,
                      })
                    }}</span>
                    <app-icono nombre="siguiente" />
                  </a>
                </li>
              }
            </ol>
          </section>
        </div>
      }
    </div>
  `,
  styles: `
    hgroup {
      display: grid;
      gap: var(--espacio-1);
    }

    .leyenda {
      font-size: var(--texto-sm);
    }

    .marco {
      max-block-size: max(20rem, 100dvh - 16rem);
      overflow: auto;
      border: 1px solid var(--color-divisoria);
      border-radius: var(--radio-lg);
      background: var(--color-superficie);
      overscroll-behavior: contain;
    }

    .marco:focus-visible {
      box-shadow: var(--foco);
      outline: none;
    }

    table {
      border-collapse: separate;
      border-spacing: 0;
      min-inline-size: 100%;
      font-size: var(--texto-sm);
    }

    th,
    td {
      padding: 0;
      border-bottom: 1px solid var(--color-divisoria);
      text-align: center;
      white-space: nowrap;
    }

    thead th {
      position: sticky;
      top: 0;
      z-index: 2;
      block-size: 2.75rem;
      background: var(--color-superficie);
      font-weight: var(--peso-medio);
    }

    .dia-columna a {
      display: grid;
      place-items: center;
      min-inline-size: 3rem;
      min-block-size: var(--tactil-minimo);
      padding-inline: var(--espacio-1);
      border-radius: var(--radio-sm);
      color: inherit;
      text-decoration: none;
    }

    .dia-columna a:focus-visible {
      box-shadow: var(--foco);
    }

    @media (hover: hover) and (pointer: fine) {
      .dia-columna a:hover {
        text-decoration: underline;
      }
    }

    .fija {
      position: sticky;
      inset-inline-start: 0;
      z-index: 1;
      min-inline-size: 14rem;
      max-inline-size: 18rem;
      padding-inline: var(--espacio-3);
      background: var(--color-superficie);
      box-shadow: 1px 0 0 var(--color-divisoria);
      text-align: start;
    }

    thead .fija {
      z-index: 3;
    }

    tbody .fija {
      font-weight: var(--peso-normal);
    }

    .quien {
      display: flex;
      align-items: center;
      gap: var(--espacio-2);
    }

    .numero {
      inline-size: 2ch;
      flex: none;
      color: var(--color-texto-suave);
      text-align: end;
    }

    .quien .nombre {
      overflow: hidden;
      text-overflow: ellipsis;
    }

    tr.baja .nombre,
    li.baja .nombre {
      color: var(--color-texto-suave);
    }

    td {
      block-size: 2.5rem;
      min-inline-size: 3rem;
      font-weight: var(--peso-fuerte);
    }

    /* La letra sobre su tinte: el color acompaña, la letra dice el estado (WCAG 1.4.1). */
    td[data-estado='presente'] {
      color: var(--grado-tinta);
    }

    td[data-estado='falta'] {
      background: var(--color-peligro-tinte);
      color: var(--color-peligro-tinta);
    }

    td[data-estado='retardo'] {
      background: var(--color-aviso-tinte);
      color: var(--color-aviso-tinta);
    }

    td[data-estado='justificada'] {
      background: var(--color-superficie-hundida);
      color: var(--color-texto-suave);
    }

    .total {
      position: sticky;
      inset-inline-end: 0;
      padding-inline: var(--espacio-3);
      background: var(--color-superficie);
      box-shadow: -1px 0 0 var(--color-divisoria);
      color: var(--grado-tinta);
      font-weight: var(--peso-fuerte);
    }

    thead .total {
      z-index: 3;
    }

    .celular {
      display: grid;
      gap: var(--espacio-6);
    }

    .alumnos {
      display: grid;
      margin: 0;
      padding: 0;
      list-style: none;
      border-top: 1px solid var(--color-divisoria);
    }

    .alumnos li {
      display: grid;
      grid-template-columns: 2ch minmax(0, 1fr) auto;
      align-items: baseline;
      gap: var(--espacio-3);
      padding: var(--espacio-3) var(--espacio-2);
      border-bottom: 1px solid var(--color-divisoria);
    }

    .alumnos .nombre {
      overflow-wrap: anywhere;
    }

    .apellidos {
      font-weight: var(--peso-medio);
    }

    .detalle {
      display: block;
      font-size: var(--texto-sm);
    }

    .porcentaje {
      color: var(--grado-tinta);
      font-size: var(--texto-lg);
      font-weight: var(--peso-fuerte);
    }

    .dias {
      display: grid;
      gap: var(--espacio-2);
    }

    .dias h2 {
      font-size: var(--texto-lg);
    }

    .filas {
      border-top: 1px solid var(--color-divisoria);
    }

    .dia-enlace {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      align-items: center;
      column-gap: var(--espacio-3);
      min-block-size: 3.5rem;
      padding-block: var(--espacio-2);
      color: inherit;
      text-decoration: none;
    }

    .dia-enlace .nombre-dia {
      font-weight: var(--peso-medio);
    }

    .dia-enlace .dato-suave {
      grid-row: 2;
      font-size: var(--texto-sm);
    }

    .dia-enlace app-icono {
      grid-column: 2;
      grid-row: 1 / 3;
      color: var(--color-texto-suave);
    }

    .dia-enlace:active .nombre-dia {
      color: var(--color-accion);
    }

    .vacio {
      display: grid;
      justify-items: start;
      gap: var(--espacio-3);
    }

    .vacio h2 {
      font-size: var(--texto-lg);
    }

    .marco-esqueleto {
      display: block;
      inline-size: 100%;
      block-size: 20rem;
    }
  `,
})
export class HistorialAsistencia implements OnInit {
  private readonly servicio = inject(AsistenciaService);
  private readonly i18n = inject(TranslocoService);
  private readonly pintarPortada = controlarPortada();
  private leyendo: string | null = null;

  readonly id = input.required<string>();
  /** La unidad elegida (?unidad=), el id de su periodo. */
  readonly unidad = input<string>();

  protected readonly grupo = signal<GrupoDto | null>(null);
  protected readonly periodos = signal<readonly PeriodoDto[]>([]);
  private readonly alumnos = signal<readonly AlumnoMatrizDto[]>([]);
  private readonly servidor = signal<{ unidad: string; tomas: TomaAsistenciaDto[] } | null>(null);
  private readonly pendientes = signal<readonly AsistenciaEnCola[]>([]);
  protected readonly error = signal<string | null>(null);

  protected readonly clave = computed(() => {
    const g = this.grupo();
    return g ? claveGrado(g.nivel, g.grado) : undefined;
  });
  protected readonly periodo = computed(() => {
    const lista = this.periodos();
    return lista.find((p) => p.id === this.unidad()) ?? unidadDeHoy(lista, hoy());
  });
  protected readonly listo = computed(() => {
    const p = this.periodo();
    return !!p && this.servidor()?.unidad === p.id;
  });
  /** RF-103: con lo que aún no se envía; solo los días de la unidad. */
  protected readonly tomas = computed(() => {
    const p = this.periodo();
    if (!p) {
      return [];
    }
    return conPendientes(
      this.servidor()?.tomas ?? [],
      this.pendientes().filter((x) => p.inicio <= x.fecha && x.fecha <= p.fin),
      this.alumnos()
        .filter((a) => !a.dadoDeBaja)
        .map((a) => a.id),
    );
  });
  protected readonly tomasRecientes = computed(() =>
    [...this.tomas()]
      .reverse()
      .map((t) => ({ fecha: t.fecha, cuenta: resumen(t.estados.values()) })),
  );
  /** Los alumnos que cuentan en alguna toma, en orden de lista; un alumno sin tomas no sale. */
  protected readonly renglones = computed<RenglonHistorial[]>(() => {
    const tomas = this.tomas();
    const valorRetardo = this.grupo()?.valorRetardo ?? 1;
    return this.alumnos()
      .filter((a) => tomas.some((t) => t.estados.has(a.id)))
      .map((alumno) => {
        const estados = tomas.map((t) => t.estados.get(alumno.id) ?? null);
        const contados = estados.filter((e): e is EstadoAsistencia => e !== null);
        return {
          alumno,
          estados,
          porcentaje: porcentaje(contados, valorRetardo),
          cuenta: resumen(contados),
        };
      });
  });

  constructor() {
    effect(() => {
      const p = this.periodo();
      if (p && this.grupo()) {
        untracked(() => void this.cargarTomas(p));
      }
    });
    const dejar = this.servicio.alEnviar(({ pendiente, toma }) => {
      if (pendiente.grupoId !== this.id()) {
        return;
      }
      this.pendientes.update((l) => l.filter((p) => p.id !== pendiente.id));
      const s = this.servidor();
      if (toma && s) {
        this.servidor.set({
          unidad: s.unidad,
          tomas: [...s.tomas.filter((t) => t.fecha !== toma.fecha), toma],
        });
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

  private async cargarTomas(periodo: PeriodoDto): Promise<void> {
    if (this.leyendo === periodo.id || this.servidor()?.unidad === periodo.id) {
      return;
    }
    this.leyendo = periodo.id;
    try {
      const tomas = await this.servicio.tomas(this.id(), periodo);
      if (this.periodo()?.id === periodo.id) {
        this.servidor.set({ unidad: periodo.id, tomas });
      }
    } catch (e) {
      this.error.set(codigoDeError(e));
    } finally {
      this.leyendo = null;
    }
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  protected corto(fecha: string): string {
    return diaCorto(fecha);
  }

  protected largo(fecha: string): string {
    return diaLargo(fecha);
  }

  /** "2 faltas · 1 retardo": solo lo que hubo; sin nada, "Sin faltas". */
  protected detalle(cuenta: Resumen): string {
    const partes = (['falta', 'retardo', 'justificada'] as const)
      .filter((e) => cuenta[e] > 0)
      .map((e) =>
        this.i18n.translate(`asistencia.detalle.${e}.${forma(cuenta[e])}`, { n: cuenta[e] }),
      );
    return partes.length ? partes.join(' · ') : this.i18n.translate('asistencia.detalle.nada');
  }

  protected textoPorcentaje(valor: number | null): string {
    return valor === null ? '—' : `${valor} %`;
  }
}
