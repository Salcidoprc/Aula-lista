import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { AlumnoCierreDto } from '../../api/models/alumno-cierre-dto';
import { CierreDto } from '../../api/models/cierre-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { PeriodoDto } from '../../api/models/periodo-dto';
import { codigoDeError } from '../../core/errores/problema';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { crearEnfoque } from '../../shared/foco';
import { claveGrado, nombreGrupo } from '../../shared/grados';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { controlarPortada } from '../../shared/portada/portada.service';
import { EvaluacionService } from '../evaluacion/evaluacion.service';
import { portadaDeGrupo } from '../grupos/grupos.service';
import { CierreService } from './cierre.service';
import { calificacion, segmentos } from './cierre';

/** SPEC-04: la UI consulta el avance cada 3 segundos mientras la IA escribe. */
const CONSULTA_MS = 3000;

/**
 * SPEC-04, "Cierre de periodo" (RF-040, RF-041, RF-044): requisitos para iniciar, la barra de avance y la lista de
 * alumnos con su estado. Generar corre en segundo plano; la pantalla consulta cada 3 s y cada observación que
 * llega entra con un fundido corto (catálogo de reglas/07). Todo es en línea.
 */
@Component({
  selector: 'app-cierre-periodo',
  imports: [TranslocoDirective, RouterLink, NgTemplateOutlet, Icono, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="pantalla pantalla--ancha"
      [attr.data-grado]="clave() ?? null"
      *transloco="let t; prefix: 'cierre'"
    >
      <a class="volver" [routerLink]="['/grupos', id()]" [queryParams]="{ unidad: periodoId() }">
        <app-icono nombre="volver" />
        {{ t('volver') }}
      </a>

      <div class="cabeza-pantalla">
        <hgroup>
          <h1 tabindex="-1">{{ t('titulo') }}</h1>
          @if (periodo(); as p) {
            <p class="dato-suave">{{ nombre() }} · {{ p.nombre }}</p>
          }
        </hgroup>
      </div>

      @if (!enLinea()) {
        <p class="aviso aviso--pendiente" role="status">{{ t('sinConexion') }}</p>
      }
      @if (cierre()?.soloLectura) {
        <p class="aviso aviso--pendiente">
          {{ grupo()?.archivadoEn ? t('archivado') : t('cerrado') }}
        </p>
      }

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (!cierre()) {
        <div class="lista" role="status" [attr.aria-label]="t('cargando')">
          @for (i of [1, 2, 3, 4]; track i) {
            <span class="esqueleto fila-esqueleto" aria-hidden="true"></span>
          }
        </div>
      } @else if (cierre(); as c) {
        @if (c.estado === 'abierto') {
          <section class="iniciar" aria-labelledby="iniciar-titulo">
            <h2 id="iniciar-titulo">{{ t('iniciar.titulo') }}</h2>
            <p>{{ t('iniciar.texto') }}</p>
            <ul class="requisitos">
              <li [attr.data-cumple]="c.requisitos.rubrosSuman100">
                <app-icono [nombre]="c.requisitos.rubrosSuman100 ? 'guardado' : 'pendiente'" />
                <span>
                  {{ t('iniciar.rubros', { suma: c.requisitos.sumaPesos }) }}
                  <span class="solo-lector">{{
                    c.requisitos.rubrosSuman100 ? t('iniciar.listo') : t('iniciar.falta')
                  }}</span>
                </span>
                @if (!c.requisitos.rubrosSuman100) {
                  <a
                    class="enlace"
                    [routerLink]="['/grupos', id(), 'periodos', periodoId(), 'rubros']"
                    >{{ t('iniciar.resolverRubros') }}</a
                  >
                }
              </li>
              <li [attr.data-cumple]="c.requisitos.hayCalificaciones">
                <app-icono [nombre]="c.requisitos.hayCalificaciones ? 'guardado' : 'pendiente'" />
                <span>
                  {{ t('iniciar.calificaciones') }}
                  <span class="solo-lector">{{
                    c.requisitos.hayCalificaciones ? t('iniciar.listo') : t('iniciar.falta')
                  }}</span>
                </span>
                @if (!c.requisitos.hayCalificaciones) {
                  <a
                    class="enlace"
                    [routerLink]="['/grupos', id()]"
                    [queryParams]="{ unidad: periodoId() }"
                    >{{ t('iniciar.resolverCalificaciones') }}</a
                  >
                }
              </li>
            </ul>
            @if (errorAccion(); as codigo) {
              <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
            }
            <button
              class="boton"
              type="button"
              [disabled]="!puedeIniciar()"
              [appOcupado]="trabajando()"
              (click)="iniciar()"
            >
              {{ t('iniciar.accion') }}
            </button>
          </section>
        } @else {
          <div class="columnas">
            <div class="principal">
              <div class="solo-celular">
                <ng-container [ngTemplateOutlet]="avance" />
              </div>

              @if (errorAccion(); as codigo) {
                <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
              }

              <ol class="lista" [attr.aria-label]="t('lista')">
                @for (a of c.alumnos; track a.alumnoId) {
                  <li>
                    <a
                      class="fila"
                      [class.llega]="llegadas().has(a.alumnoId)"
                      [style.--orden]="llegadas().get(a.alumnoId) ?? 0"
                      [attr.data-estado]="a.estado"
                      [routerLink]="[
                        '/grupos',
                        id(),
                        'periodos',
                        periodoId(),
                        'cierre',
                        a.alumnoId,
                      ]"
                    >
                      <span class="numero cifras">{{ a.numeroLista }}</span>
                      <span class="nombre"
                        ><span class="apellidos">{{ a.apellidos }}</span> {{ a.nombres }}</span
                      >
                      <span class="calificacion cifras">
                        <span class="solo-lector">{{ t('calificacion') }}</span>
                        {{ calificacion(a.calificacion) }}
                      </span>
                      <span class="marcas">
                        <span class="estado" [attr.data-estado]="a.estado">
                          @if (a.estado === 'generando') {
                            <app-icono nombre="sincronizando" />
                          } @else if (a.estado === 'aprobada' || a.estado === 'publicada') {
                            <app-icono nombre="guardado" />
                          }
                          {{ t('estados.' + a.estado) }}
                        </span>
                        @if (a.datosCambiaron) {
                          <span class="marca-extra aviso-tinte">{{ t('datosCambiaron') }}</span>
                        }
                        @if (a.fallo && a.estado !== 'generando') {
                          <span class="marca-extra peligro-tinte">{{ t('noSeGenero') }}</span>
                        }
                      </span>
                    </a>
                  </li>
                }
              </ol>
              <p class="solo-lector" role="status">{{ anuncio() }}</p>

              @if (porGenerar() > 0 && !c.soloLectura) {
                <div class="pie-accion solo-celular">
                  <ng-container [ngTemplateOutlet]="generar" />
                </div>
              }
              <div class="solo-celular">
                <ng-container [ngTemplateOutlet]="estilo" />
              </div>
            </div>

            <aside class="lateral solo-escritorio" aria-labelledby="avance-titulo">
              <h2 id="avance-titulo">{{ t('avance') }}</h2>
              <ng-container [ngTemplateOutlet]="avance" />
              @if (porGenerar() > 0 && !c.soloLectura) {
                <ng-container [ngTemplateOutlet]="generar" />
              }
              <ng-container [ngTemplateOutlet]="estilo" />
            </aside>
          </div>
        }
      }

      <ng-template #avance>
        @if (cierre(); as c) {
          <div class="avance">
            <p class="cifras resumen" tabindex="-1">
              {{ t('aprobadas', { aprobadas: aprobadas(), total: c.alumnos.length }) }}
            </p>
            <span class="barra" aria-hidden="true">
              @for (s of barra(); track s.estado) {
                <span [attr.data-segmento]="s.estado" [style.inline-size.%]="s.porcentaje"></span>
              }
            </span>
            <dl class="conteo cifras">
              @for (s of barra(); track s.estado) {
                <div [attr.data-segmento]="s.estado">
                  <dt>{{ t('segmentos.' + s.estado) }}</dt>
                  <dd>{{ s.cantidad }}</dd>
                </div>
              }
            </dl>
            @if (generando() > 0) {
              <p class="dato-suave generando">
                <app-icono nombre="sincronizando" />
                {{ t('generandoAhora', { cantidad: generando() }) }}
              </p>
            }
          </div>
        }
      </ng-template>

      <ng-template #generar>
        <button
          class="boton boton--ancho generar"
          type="button"
          [disabled]="!enLinea()"
          [appOcupado]="trabajando()"
          (click)="generarTodas()"
        >
          {{ t('generar', { cantidad: porGenerar() }) }}
        </button>
      </ng-template>

      <ng-template #estilo>
        <a class="enlace" routerLink="/ajustes/estilo">
          <app-icono nombre="ajustes" />
          {{ t('estilo') }}
        </a>
      </ng-template>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .iniciar {
      display: grid;
      justify-items: start;
      gap: var(--espacio-4);
      max-inline-size: 40rem;
    }

    .iniciar h2 {
      font-size: var(--texto-lg);
    }

    .requisitos {
      display: grid;
      gap: var(--espacio-2);
      inline-size: 100%;
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .requisitos li {
      display: grid;
      grid-template-columns: auto minmax(0, 1fr);
      align-items: center;
      gap: var(--espacio-1) var(--espacio-3);
      padding: var(--espacio-3) var(--espacio-4);
      border-radius: var(--radio-lg);
      background: var(--color-exito-tinte);
      color: var(--color-exito-tinta);
    }

    .requisitos li[data-cumple='false'] {
      background: var(--color-aviso-tinte);
      color: var(--color-aviso-tinta);
    }

    .requisitos .enlace {
      grid-column: 2;
      justify-self: start;
    }

    .principal {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      align-content: start;
      gap: var(--espacio-4);
    }

    .avance {
      display: grid;
      gap: var(--espacio-3);
    }

    .resumen {
      font-weight: var(--peso-fuerte);
    }

    /* Barra segmentada: aprobadas en éxito, borradores en el tinte del grado, lo que falta hundido. */
    .barra {
      display: flex;
      block-size: 0.625rem;
      border-radius: var(--radio-pleno);
      background: var(--color-superficie-hundida);
      overflow: hidden;
    }

    .barra span {
      block-size: 100%;
    }

    .barra [data-segmento='aprobada'] {
      background: var(--color-exito);
    }

    .barra [data-segmento='borrador'] {
      background: var(--grado);
    }

    .barra [data-segmento='pendiente'] {
      background: transparent;
    }

    .conteo {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(min(100%, 7rem), 1fr));
      gap: var(--espacio-2);
      margin: 0;
    }

    .conteo div {
      display: grid;
      gap: var(--espacio-1);
      padding-inline-start: var(--espacio-2);
      border-inline-start: 3px solid var(--color-superficie-hundida);
    }

    .conteo [data-segmento='aprobada'] {
      border-color: var(--color-exito);
    }

    .conteo [data-segmento='borrador'] {
      border-color: var(--grado);
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

    .generando {
      display: flex;
      align-items: center;
      gap: var(--espacio-2);
      font-size: var(--texto-sm);
    }

    .lista {
      display: grid;
      margin: 0;
      padding: 0;
      list-style: none;
      border-top: 1px solid var(--color-divisoria);
      container-type: inline-size;
    }

    .lista > li {
      border-bottom: 1px solid var(--color-divisoria);
    }

    .fila {
      display: grid;
      grid-template-columns: 2ch minmax(0, 1fr) auto auto;
      align-items: center;
      gap: var(--espacio-3);
      min-block-size: 3.5rem;
      padding: var(--espacio-2) var(--espacio-3);
      color: inherit;
      text-decoration: none;
      transition: background-color var(--dur-small) var(--ease-out);
      scroll-margin-block-end: calc(var(--alto-pie-accion, 0px) + var(--espacio-4));
    }

    .fila:focus-visible {
      position: relative;
      box-shadow: var(--foco);
      outline: none;
    }

    @media (hover: hover) and (pointer: fine) {
      .fila:hover {
        background: var(--color-superficie-hundida);
      }
    }

    /* Catálogo de reglas/07: la observación que llega de la IA entra con fundido, escalonado corto. */
    .fila.llega .marcas {
      animation: llegar 240ms var(--ease-out) both;
      animation-delay: calc(var(--orden, 0) * 40ms);
    }

    @keyframes llegar {
      from {
        opacity: 0;
        transform: translateY(0.25rem);
      }
    }

    @media (prefers-reduced-motion: reduce) {
      @keyframes llegar {
        from {
          opacity: 0;
        }
      }
    }

    .numero {
      color: var(--color-texto-suave);
      text-align: end;
    }

    .nombre {
      overflow-wrap: break-word;
      min-inline-size: 0;
    }

    .apellidos {
      font-weight: var(--peso-medio);
    }

    .calificacion {
      color: var(--color-texto-suave);
    }

    .marcas {
      display: flex;
      flex-wrap: wrap;
      justify-content: end;
      gap: var(--espacio-1);
    }

    .estado,
    .marca-extra {
      display: inline-flex;
      align-items: center;
      gap: var(--espacio-1);
      padding: var(--espacio-1) var(--espacio-3);
      border-radius: var(--radio-pleno);
      background: var(--color-superficie-hundida);
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
      font-weight: var(--peso-medio);
      white-space: nowrap;
    }

    .estado[data-estado='generando'] {
      background: var(--color-aviso-tinte);
      color: var(--color-aviso-tinta);
    }

    .estado[data-estado='generando'] app-icono {
      animation: girar 1s linear infinite;
    }

    @media (prefers-reduced-motion: reduce) {
      .estado[data-estado='generando'] app-icono {
        animation: none;
      }
    }

    @keyframes girar {
      to {
        transform: rotate(1turn);
      }
    }

    .estado[data-estado='borrador'] {
      background: var(--grado-tinte);
      color: var(--grado-tinta);
    }

    .estado[data-estado='aprobada'],
    .estado[data-estado='publicada'] {
      background: var(--color-exito-tinte);
      color: var(--color-exito-tinta);
    }

    .aviso-tinte {
      background: var(--color-aviso-tinte);
      color: var(--color-aviso-tinta);
    }

    .peligro-tinte {
      background: var(--color-peligro-tinte);
      color: var(--color-peligro-tinta);
    }

    /* Angosto o con texto grande: calificación y estado bajan de renglón bajo el nombre. */
    @container (max-width: 30rem) {
      .fila {
        grid-template-columns: 2ch minmax(0, 1fr);
      }

      .calificacion,
      .marcas {
        grid-column: 2;
        justify-self: start;
        justify-content: start;
      }
    }

    .enlace {
      display: inline-flex;
      align-items: center;
      gap: var(--espacio-2);
      min-block-size: var(--tactil-minimo);
    }

    .fila-esqueleto {
      display: block;
      inline-size: 100%;
      block-size: 3.5rem;
      margin-block-end: var(--espacio-1);
    }
  `,
})
export class CierrePeriodo implements OnInit {
  private readonly servicio = inject(CierreService);
  private readonly evaluacion = inject(EvaluacionService);
  private readonly i18n = inject(TranslocoService);
  private readonly avisos = inject(AvisosService);
  private readonly pintarPortada = controlarPortada();
  private readonly enfocar = crearEnfoque();
  protected readonly enLinea = this.servicio.enLinea;
  protected readonly calificacion = calificacion;
  private temporizador: ReturnType<typeof setTimeout> | null = null;

  readonly id = input.required<string>();
  readonly periodoId = input.required<string>();

  protected readonly grupo = signal<GrupoDto | null>(null);
  protected readonly periodo = signal<PeriodoDto | null>(null);
  protected readonly cierre = signal<CierreDto | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly errorAccion = signal<string | null>(null);
  protected readonly trabajando = signal(false);
  protected readonly anuncio = signal('');
  /** Las que acaban de llegar de la IA en esta consulta, con su orden para el escalonado. */
  protected readonly llegadas = signal<ReadonlyMap<string, number>>(new Map());

  protected readonly clave = computed(() => {
    const g = this.grupo();
    return g ? claveGrado(g.nivel, g.grado) : undefined;
  });
  protected readonly nombre = computed(() => {
    const g = this.grupo();
    return g ? nombreGrupo(g.grado, g.nombre) : '';
  });
  protected readonly barra = computed(() => {
    const c = this.cierre();
    return c ? segmentos(c.conteos) : [];
  });
  protected readonly aprobadas = computed(() => this.barra()[0]?.cantidad ?? 0);
  protected readonly generando = computed(() => Number(this.cierre()?.conteos.generando ?? 0));
  protected readonly porGenerar = computed(() => Number(this.cierre()?.conteos.sinGenerar ?? 0));
  protected readonly puedeIniciar = computed(() => {
    const c = this.cierre();
    return (
      !!c &&
      !c.soloLectura &&
      !this.grupo()?.archivadoEn &&
      c.requisitos.rubrosSuman100 &&
      c.requisitos.hayCalificaciones &&
      this.enLinea()
    );
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => this.detener());
  }

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      const [grupo, periodos, cierre] = await Promise.all([
        this.evaluacion.grupo(this.id()),
        this.evaluacion.periodos(this.id()),
        this.servicio.cierre(this.periodoId()),
      ]);
      this.pintarPortada(portadaDeGrupo(grupo));
      this.grupo.set(grupo);
      this.periodo.set(periodos.find((p) => p.id === this.periodoId()) ?? null);
      this.mostrar(cierre);
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected texto(clave: string, datos?: Record<string, unknown>): string {
    return this.i18n.translate(clave, datos);
  }

  protected async iniciar(): Promise<void> {
    await this.accion(() => this.servicio.iniciar(this.periodoId()));
    this.enfocar('h1');
  }

  /** El botón desaparece cuando ya no queda nada por generar: el foco pasa al avance. */
  protected async generarTodas(): Promise<void> {
    await this.accion(() => this.servicio.generar(this.periodoId()));
    this.enfocar('.resumen');
  }

  private async accion(llamar: () => Promise<CierreDto>): Promise<void> {
    this.errorAccion.set(null);
    this.trabajando.set(true);
    try {
      this.mostrar(await llamar());
    } catch (e) {
      this.errorAccion.set(codigoDeError(e));
    } finally {
      this.trabajando.set(false);
    }
  }

  /** Pinta el cierre, marca las que llegaron desde la consulta anterior y agenda la siguiente si falta. */
  private mostrar(nuevo: CierreDto): void {
    const anterior = this.cierre();
    if (anterior) {
      const antes = new Map(anterior.alumnos.map((a) => [a.alumnoId, a.estado]));
      const llegaron = nuevo.alumnos.filter(
        (a) => antes.get(a.alumnoId) === 'generando' && a.estado !== 'generando',
      );
      this.llegadas.set(new Map(llegaron.map((a, i) => [a.alumnoId, Math.min(i, 8)])));
      if (Number(anterior.conteos.generando) > 0 && Number(nuevo.conteos.generando) === 0) {
        this.anunciarFin(nuevo.alumnos);
      }
    }
    this.cierre.set(nuevo);
    this.detener();
    if (Number(nuevo.conteos.generando) > 0) {
      this.temporizador = setTimeout(() => void this.consultar(), CONSULTA_MS);
    }
  }

  private async consultar(): Promise<void> {
    // En una pestaña oculta no se consulta; se retoma al volver.
    if (document.visibilityState === 'hidden') {
      this.temporizador = setTimeout(() => void this.consultar(), CONSULTA_MS);
      return;
    }
    try {
      this.mostrar(await this.servicio.cierre(this.periodoId()));
    } catch {
      // Sin red la consulta se vuelve a intentar; la pantalla conserva lo último que supo.
      this.temporizador = setTimeout(() => void this.consultar(), CONSULTA_MS);
    }
  }

  /** El lector de pantalla oye el resultado una vez, al terminar, no en cada consulta. */
  private anunciarFin(alumnos: readonly AlumnoCierreDto[]): void {
    const fallidas = alumnos.filter((a) => a.estado === 'sin_generar' && a.fallo).length;
    if (fallidas > 0) {
      this.avisos.mostrar(this.texto('cierre.terminadoConFallas', { fallidas }), 'error');
    } else {
      this.anuncio.set(this.texto('cierre.terminado'));
    }
  }

  private detener(): void {
    if (this.temporizador) {
      clearTimeout(this.temporizador);
      this.temporizador = null;
    }
  }
}
