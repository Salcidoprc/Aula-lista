import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ActividadDto } from '../../api/models/actividad-dto';
import { AlumnoMatrizDto } from '../../api/models/alumno-matriz-dto';
import { CambioCelda } from '../../api/models/cambio-celda';
import { CeldaDto } from '../../api/models/celda-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { MatrizCalificacionesDto } from '../../api/models/matriz-calificaciones-dto';
import { RubroDto } from '../../api/models/rubro-dto';
import { Forma, ResultadoPeriodo } from '../../core/calculo/calculo';
import { formatear } from '../../core/calculo/decimal';
import { codigoDeError } from '../../core/errores/problema';
import { conCambios, conCeldas, llaveCelda } from '../../core/sincronizacion/celdas';
import { ColaSincronizacion } from '../../core/sincronizacion/cola-sincronizacion';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { claveGrado, nombreGrupo } from '../../shared/grados';
import { Icono } from '../../shared/iconos';
import { controlarPortada } from '../../shared/portada/portada.service';
import { portadaDeGrupo } from '../grupos/grupos.service';
import { EvaluacionService } from './evaluacion.service';
import {
  cambio,
  celdaVacia,
  dosDecimales,
  indexar,
  interpretar,
  leerPegado,
  numero,
  resultadoAlumno,
  textoCelda,
  validarValor,
  valorDeCelda,
} from './matriz';

/** Una columna de la tabla: una actividad de un rubro, o el promedio del rubro al final de su grupo. */
type Columna =
  | {
      readonly tipo: 'actividad';
      readonly actividad: ActividadDto;
      readonly rubro: RubroDto;
      /** Índice entre las columnas que se escriben; null si se califica por criterios. */
      readonly editable: number | null;
    }
  | { readonly tipo: 'promedio'; readonly rubro: RubroDto };

/** El último problema al escribir o pegar, para leerlo arriba de la tabla. */
interface Problema {
  readonly alumno: string;
  readonly actividad: string;
  readonly codigo: string;
}

/**
 * SPEC-03, "Vista de tabla (computadora)": alumnos por actividades, agrupadas por rubro con su promedio y la
 * calificación del periodo. Flechas, Enter y Tab para moverse; pegar desde Excel llena desde la celda con foco.
 * Las actividades con criterios se ven aquí y se capturan en su vista por actividad.
 */
@Component({
  selector: 'app-tabla',
  imports: [TranslocoDirective, RouterLink, Icono],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="pantalla pantalla--ancha"
      [attr.data-grado]="clave() ?? null"
      *transloco="let t; prefix: 'tabla'"
    >
      <a class="volver" [routerLink]="['/grupos', id()]" [queryParams]="{ unidad: periodoId() }">
        <app-icono nombre="volver" />
        {{ t('volver') }}
      </a>

      <hgroup>
        <h1 tabindex="-1">{{ t('titulo') }}</h1>
        <p class="dato-suave">{{ nombreGrupo() }} · {{ nombrePeriodo() }}</p>
      </hgroup>

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (!matriz()) {
        <span
          class="esqueleto tabla-esqueleto"
          role="status"
          [attr.aria-label]="t('cargando')"
        ></span>
      } @else if (columnas().length === 0) {
        <p class="dato-suave">{{ t('sinActividades') }}</p>
      } @else {
        @if (matriz()!.soloLectura || grupo()?.archivadoEn) {
          <p class="aviso aviso--pendiente">{{ t('soloLectura') }}</p>
        } @else {
          <p class="ayuda-teclado dato-suave">{{ t('ayuda') }}</p>
        }

        <p class="problema" role="status">
          @if (problema(); as p) {
            {{ t('problema', { alumno: p.alumno, actividad: p.actividad }) }}
            {{ texto('errores.' + p.codigo) }}
          }
        </p>

        <div class="marco" tabindex="-1" (keydown)="teclear($event)" (paste)="pegar($event)">
          <table>
            <caption class="solo-lector">
              {{
                t('titulo')
              }}
              ·
              {{
                nombrePeriodo()
              }}
            </caption>
            <thead>
              <tr class="rubros">
                <th scope="col" rowspan="2" class="fija">{{ t('alumno') }}</th>
                @for (r of matriz()!.rubros; track r.rubro.id) {
                  <th scope="colgroup" [attr.colspan]="r.actividades.length + 1" class="rubro">
                    {{ r.rubro.nombre }}
                    <span class="peso cifras">{{ numero(r.rubro.peso) }} %</span>
                  </th>
                }
                <th scope="col" rowspan="2" class="total">{{ t('periodo') }}</th>
                <th scope="col" rowspan="2" class="total">{{ t('final') }}</th>
              </tr>
              <tr class="actividades">
                @for (c of columnas(); track $index) {
                  @if (c.tipo === 'actividad') {
                    <th scope="col" class="actividad" [class.inicio]="esInicio(c)">
                      <a
                        [routerLink]="[
                          '/grupos',
                          id(),
                          'periodos',
                          periodoId(),
                          'actividades',
                          c.actividad.id,
                        ]"
                        [attr.title]="c.actividad.nombre"
                        >{{ c.actividad.nombre }}</a
                      >
                    </th>
                  } @else {
                    <th scope="col" class="promedio">{{ t('promedio') }}</th>
                  }
                }
              </tr>
            </thead>
            <tbody>
              @for (a of alumnos(); track a.id; let f = $index) {
                @let res = resultados().get(a.id);
                <tr [class.baja]="a.dadoDeBaja">
                  <th scope="row" class="fija">
                    <span class="quien">
                      <span class="numero cifras">{{ a.numeroLista }}</span>
                      <span class="nombre">{{ a.apellidos }} {{ a.nombres }}</span>
                      @if (a.dadoDeBaja) {
                        <span class="etiqueta-baja">{{ t('baja') }}</span>
                      }
                    </span>
                  </th>
                  @for (c of columnas(); track $index) {
                    @if (c.tipo === 'actividad') {
                      @let celda = celdaDe(c.actividad.id, a.id);
                      <td [class.inicio]="esInicio(c)">
                        @if (c.editable !== null && !bloqueada() && !a.dadoDeBaja) {
                          <input
                            class="celda cifras"
                            [attr.data-f]="f"
                            [attr.data-c]="c.editable"
                            autocomplete="off"
                            inputmode="decimal"
                            [attr.aria-label]="
                              t('celda', {
                                alumno: a.apellidos + ' ' + a.nombres,
                                actividad: c.actividad.nombre,
                              })
                            "
                            [class.con-valor]="!celdaVacia(celda)"
                            [attr.aria-invalid]="
                              fallas().has(llave(c.actividad.id, a.id)) ? true : null
                            "
                            [value]="textoCelda(celda)"
                            (change)="escribir(a, c.actividad, c.rubro, valor($event))"
                          />
                        } @else {
                          <span class="cifras solo-ver">{{
                            c.editable === null ? calculada(c.rubro, celda) : textoCelda(celda)
                          }}</span>
                        }
                      </td>
                    } @else {
                      <td class="promedio cifras">{{ dos(res?.rubros?.get(c.rubro.id)) }}</td>
                    }
                  }
                  <td class="total cifras">{{ dos(res?.periodo) }}</td>
                  <td class="total final cifras">{{ final(res) }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: `
    hgroup {
      display: grid;
      gap: var(--espacio-1);
    }

    .ayuda-teclado {
      font-size: var(--texto-sm);
    }

    .problema {
      min-block-size: 1.5em;
      color: var(--color-peligro-tinta);
      font-size: var(--texto-sm);
    }

    /* La tabla se desplaza dentro de su marco; el encabezado y la columna de nombres se quedan. */
    .marco {
      max-block-size: calc(100dvh - 14rem);
      overflow: auto;
      border: 1px solid var(--color-divisoria);
      border-radius: var(--radio-lg);
      background: var(--color-superficie);
      overscroll-behavior: contain;
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
      z-index: 2;
      background: var(--color-superficie);
      font-weight: var(--peso-medio);
    }

    thead tr.rubros th {
      top: 0;
      block-size: 2.5rem;
      padding-inline: var(--espacio-3);
    }

    thead tr.actividades th {
      top: 2.5rem;
      block-size: 2.5rem;
      padding-inline: var(--espacio-2);
    }

    th.rubro {
      border-inline-start: 2px solid var(--color-divisoria);
      color: var(--grado-tinta);
      text-align: start;
    }

    .peso {
      color: var(--color-texto-suave);
      font-weight: var(--peso-normal);
    }

    th.actividad a {
      display: block;
      max-inline-size: 6.5rem;
      overflow: hidden;
      color: inherit;
      text-overflow: ellipsis;
      text-decoration: none;
    }

    @media (hover: hover) and (pointer: fine) {
      th.actividad a:hover {
        text-decoration: underline;
      }
    }

    th.actividad a:focus-visible {
      border-radius: var(--radio-sm);
      box-shadow: var(--foco);
    }

    .inicio {
      border-inline-start: 2px solid var(--color-divisoria);
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
      min-width: 0;
    }

    .numero {
      inline-size: 2ch;
      flex: none;
      color: var(--color-texto-suave);
      text-align: end;
    }

    .nombre {
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .etiqueta-baja {
      color: var(--color-texto-suave);
      font-size: var(--texto-xs);
    }

    tr.baja .nombre {
      color: var(--color-texto-suave);
    }

    td {
      block-size: 2.75rem;
      min-inline-size: 3.5rem;
    }

    .celda {
      inline-size: 3.5rem;
      block-size: 2.25rem;
      margin: 0.25rem;
      padding: 0;
      border: 0;
      border-radius: var(--radio-sm);
      background: transparent;
      color: var(--color-texto);
      font: inherit;
      font-weight: var(--peso-medio);
      text-align: center;
      transition: background-color var(--dur-small) var(--ease-out);
    }

    .celda.con-valor {
      background: var(--grado-tinte);
    }

    /* El foco entra al instante (regla 07: sin animación del foco); solo la celda guardada hace su fundido. */
    .celda:focus-visible {
      background: var(--color-superficie);
      box-shadow: var(--foco);
      outline: none;
      transition: none;
    }

    .celda[aria-invalid='true'] {
      box-shadow: inset 0 0 0 2px var(--color-peligro);
    }

    .solo-ver {
      color: var(--color-texto-suave);
    }

    th.promedio,
    td.promedio {
      padding-inline: var(--espacio-3);
      background: var(--color-superficie-hundida);
      font-weight: var(--peso-medio);
    }

    thead th.promedio {
      background: var(--color-superficie-hundida);
    }

    .total {
      padding-inline: var(--espacio-3);
      border-inline-start: 2px solid var(--color-divisoria);
      font-weight: var(--peso-fuerte);
    }

    td.final {
      color: var(--grado-tinta);
    }

    .tabla-esqueleto {
      display: block;
      block-size: 20rem;
    }
  `,
})
export class Tabla implements OnInit {
  private readonly servicio = inject(EvaluacionService);
  private readonly cola = inject(ColaSincronizacion);
  private readonly avisos = inject(AvisosService);
  private readonly i18n = inject(TranslocoService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly pintarPortada = controlarPortada();
  protected readonly textoCelda = textoCelda;
  protected readonly celdaVacia = celdaVacia;
  protected readonly numero = numero;

  readonly id = input.required<string>();
  readonly periodoId = input.required<string>();

  protected readonly grupo = signal<GrupoDto | null>(null);
  protected readonly nombrePeriodo = signal('');
  protected readonly matriz = signal<MatrizCalificacionesDto | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly problema = signal<Problema | null>(null);
  protected readonly fallas = signal<ReadonlySet<string>>(new Set());

  protected readonly clave = computed(() => {
    const g = this.grupo();
    return g ? claveGrado(g.nivel, g.grado) : undefined;
  });
  protected readonly nombreGrupo = computed(() => {
    const g = this.grupo();
    return g ? nombreGrupo(g.grado, g.nombre) : '';
  });
  protected readonly indice = computed(() => {
    const m = this.matriz();
    return m ? indexar(m) : null;
  });
  protected readonly bloqueada = computed(
    () => !!this.matriz()?.soloLectura || !!this.grupo()?.archivadoEn,
  );
  /** Activos por número de lista y, al final, las bajas que tienen calificaciones (se ven, no se editan). */
  protected readonly alumnos = computed(() => {
    const m = this.matriz();
    if (!m) {
      return [];
    }
    const conDatos = new Set(m.celdas.filter((c) => !celdaVacia(c)).map((c) => c.alumnoId));
    const orden = (a: AlumnoMatrizDto, b: AlumnoMatrizDto) =>
      Number(a.numeroLista) - Number(b.numeroLista);
    return [
      ...m.alumnos.filter((a) => !a.dadoDeBaja).sort(orden),
      ...m.alumnos.filter((a) => a.dadoDeBaja && conDatos.has(a.id)).sort(orden),
    ];
  });
  protected readonly columnas = computed<Columna[]>(() => {
    const m = this.matriz();
    if (!m) {
      return [];
    }
    const lista: Columna[] = [];
    let editable = 0;
    for (const { rubro, actividades } of m.rubros) {
      if (actividades.length === 0) {
        continue;
      }
      for (const actividad of actividades) {
        lista.push({
          tipo: 'actividad',
          actividad,
          rubro,
          editable: rubro.criterios.length > 0 ? null : editable++,
        });
      }
      lista.push({ tipo: 'promedio', rubro });
    }
    return lista;
  });
  /** Columnas que se escriben, en orden, para el teclado y el pegado. */
  private readonly editables = computed(() =>
    this.columnas().filter(
      (c): c is Extract<Columna, { tipo: 'actividad' }> =>
        c.tipo === 'actividad' && c.editable !== null,
    ),
  );
  protected readonly resultados = computed(() => {
    const m = this.matriz();
    const i = this.indice();
    const mapa = new Map<string, ResultadoPeriodo>();
    if (m && i) {
      for (const a of this.alumnos()) {
        mapa.set(a.id, resultadoAlumno(m, i, a.id));
      }
    }
    return mapa;
  });

  constructor() {
    const dejar = this.cola.alConfirmar(({ vigentes, rechazadas }) => {
      const m = this.matriz();
      if (!m) {
        return;
      }
      const propias = new Set(this.indice()?.actividades.map((a) => a.id));
      const deAqui = new Map([...vigentes].filter(([llave]) => propias.has(llave.split(':')[0])));
      this.matriz.set(conCeldas(m, deAqui));
      const rechazo = rechazadas.find((r) => propias.has(r.actividadId));
      if (rechazo) {
        this.marcar(rechazo.actividadId, rechazo.alumnoId, rechazo.codigo);
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
      const [grupo, periodos, matriz] = await Promise.all([
        this.servicio.grupo(this.id()),
        this.servicio.periodos(this.id()),
        this.servicio.matriz(this.periodoId()),
      ]);
      this.grupo.set(grupo);
      this.pintarPortada(portadaDeGrupo(grupo));
      this.nombrePeriodo.set(periodos.find((p) => p.id === this.periodoId())?.nombre ?? '');
      this.matriz.set(matriz);
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  protected valor(evento: Event): string {
    return (evento.target as HTMLInputElement).value;
  }

  protected llave(actividadId: string, alumnoId: string): string {
    return llaveCelda({ actividadId, alumnoId });
  }

  protected celdaDe(actividadId: string, alumnoId: string): CeldaDto | undefined {
    return this.indice()?.celdas.get(this.llave(actividadId, alumnoId));
  }

  protected esInicio(c: Columna): boolean {
    if (c.tipo !== 'actividad') {
      return false;
    }
    const r = this.matriz()?.rubros.find((x) => x.rubro.id === c.rubro.id);
    return r?.actividades[0]?.id === c.actividad.id;
  }

  protected calculada(rubro: RubroDto, celda: CeldaDto | undefined): string {
    return celdaVacia(celda) ? '' : dosDecimales(valorDeCelda(rubro, celda));
  }

  protected dos(valor: ResultadoPeriodo['periodo'] | undefined): string {
    return valor === undefined ? '—' : dosDecimales(valor);
  }

  /** La calificación final con los decimales y el redondeo del grupo (RN-035). */
  protected final(res: ResultadoPeriodo | undefined): string {
    const decimales = Number(this.matriz()?.configuracion.decimalesFinal ?? 2);
    if (!res?.final) {
      return '—';
    }
    return formatear(res.final, decimales);
  }

  private marcar(actividadId: string, alumnoId: string, codigo: string | null): void {
    const llave = this.llave(actividadId, alumnoId);
    const fallas = new Set(this.fallas());
    if (codigo) {
      fallas.add(llave);
      const a = this.matriz()?.alumnos.find((x) => x.id === alumnoId);
      const act = this.indice()?.actividades.find((x) => x.id === actividadId);
      this.problema.set({
        alumno: a ? `${a.apellidos} ${a.nombres}` : '',
        actividad: act?.nombre ?? '',
        codigo,
      });
    } else {
      fallas.delete(llave);
      if (fallas.size === 0) {
        this.problema.set(null);
      }
    }
    this.fallas.set(fallas);
  }

  /** Interpreta y valida un texto para una celda; devuelve el cambio, o null si no hay nada que mandar. */
  private cambioPara(
    alumno: AlumnoMatrizDto,
    actividad: ActividadDto,
    rubro: RubroDto,
    texto: string,
  ): CambioCelda | null {
    const captura = interpretar(texto, rubro.forma as Forma);
    const problema =
      captura === 'marca_invalida' ? captura : validarValor(captura.valor, rubro.califMinima);
    if (problema || captura === 'marca_invalida') {
      this.marcar(actividad.id, alumno.id, problema);
      return null;
    }
    this.marcar(actividad.id, alumno.id, null);
    const anterior = this.celdaDe(actividad.id, alumno.id);
    if (
      (anterior?.marca ?? 'ninguna') === captura.marca &&
      String(anterior?.valor ?? '') === String(captura.valor ?? '')
    ) {
      return null;
    }
    return cambio(actividad.id, alumno.id, captura, anterior);
  }

  protected escribir(
    alumno: AlumnoMatrizDto,
    actividad: ActividadDto,
    rubro: RubroDto,
    texto: string,
  ): void {
    const c = this.cambioPara(alumno, actividad, rubro, texto);
    if (c) {
      this.encolar([c]);
    }
  }

  private encolar(cambios: CambioCelda[]): void {
    const m = this.matriz();
    if (!m || cambios.length === 0) {
      return;
    }
    const nueva = conCambios(m, cambios);
    this.matriz.set(nueva);
    this.servicio.capturar(nueva, cambios).catch(() => {
      this.avisos.mostrar(this.i18n.translate('captura.noSeGuardo'), 'error');
    });
  }

  private celda(f: number, c: number): HTMLInputElement | null {
    return this.host.nativeElement.querySelector<HTMLInputElement>(
      `input[data-f="${f}"][data-c="${c}"]`,
    );
  }

  /** Flechas para moverse como en una hoja de cálculo; Enter baja (Mayús+Enter sube). Tab es el del navegador. */
  protected teclear(evento: KeyboardEvent): void {
    const el = evento.target as HTMLElement;
    if (!(el instanceof HTMLInputElement) || el.dataset['f'] === undefined) {
      return;
    }
    const f = Number(el.dataset['f']);
    const c = Number(el.dataset['c']);
    const destinos: Record<string, [number, number]> = {
      ArrowDown: [f + 1, c],
      ArrowUp: [f - 1, c],
      Enter: evento.shiftKey ? [f - 1, c] : [f + 1, c],
    };
    // Izquierda y derecha mueven de celda solo si el cursor está en la orilla del texto.
    if (evento.key === 'ArrowLeft' && el.selectionStart === 0 && el.selectionEnd === 0) {
      destinos['ArrowLeft'] = [f, c - 1];
    }
    if (evento.key === 'ArrowRight' && el.selectionStart === el.value.length) {
      destinos['ArrowRight'] = [f, c + 1];
    }
    const destino = destinos[evento.key];
    if (!destino) {
      return;
    }
    evento.preventDefault();
    if (evento.key === 'Enter') {
      el.dispatchEvent(new Event('change'));
    }
    const siguiente = this.celda(destino[0], destino[1]);
    if (siguiente) {
      siguiente.focus();
      siguiente.select();
    }
  }

  /** Pegar desde Excel: renglones y columnas desde la celda con foco, solo en columnas que se escriben. */
  protected pegar(evento: ClipboardEvent): void {
    const el = evento.target as HTMLElement;
    const texto = evento.clipboardData?.getData('text/plain') ?? '';
    if (
      !(el instanceof HTMLInputElement) ||
      el.dataset['f'] === undefined ||
      !/[\t\n]/.test(texto)
    ) {
      return;
    }
    evento.preventDefault();
    const f0 = Number(el.dataset['f']);
    const c0 = Number(el.dataset['c']);
    const alumnos = this.alumnos().filter((a) => !a.dadoDeBaja);
    const columnas = this.editables();
    const cambios: CambioCelda[] = [];
    let rechazadas = 0;
    leerPegado(texto).forEach((renglon, i) => {
      const alumno = alumnos[f0 + i];
      renglon.forEach((valor, j) => {
        const col = columnas[c0 + j];
        if (!alumno || !col) {
          return;
        }
        const antes = this.fallas().size;
        const c = this.cambioPara(alumno, col.actividad, col.rubro, valor);
        if (c) {
          cambios.push(c);
        } else if (this.fallas().size > antes) {
          rechazadas++;
        }
      });
    });
    this.encolar(cambios);
    this.avisos.mostrar(
      this.i18n.translate(rechazadas ? 'tabla.pegadoConErrores' : 'tabla.pegado', {
        cantidad: cambios.length,
        errores: rechazadas,
      }),
      rechazadas ? 'error' : 'exito',
    );
  }
}
