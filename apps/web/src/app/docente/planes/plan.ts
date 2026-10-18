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
import { Router } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { GrupoPlanDto } from '../../api/models/grupo-plan-dto';
import { PagoDto } from '../../api/models/pago-dto';
import { PlanesDto } from '../../api/models/planes-dto';
import { SuscripcionDto } from '../../api/models/suscripcion-dto';
import { codigoDeError } from '../../core/errores/problema';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { fechaLarga } from '../../shared/fechas';
import { nombreGrupo } from '../../shared/grados';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { forma } from '../../shared/plural';
import { Tapa } from '../../shared/tapa';
import { BandaPago } from './banda-pago';
import { EtiquetaPlan } from './etiqueta-plan';
import {
  FormaDePago,
  cambioGrupos,
  cifra,
  eleccionInicial,
  mensual,
  modoPlan,
  prepagoVigente,
  totalAPagar,
} from './planes';
import { PlanesService } from './planes.service';

/** Cuánto se espera antes de volver a leer el plan cuando el proveedor confirma el pago después del regreso. */
const RELECTURA_MS = 4000;

/**
 * SPEC-08, pantalla Plan (RF-080 a RF-086): el estado en una frase, los grupos en Pro con el total al mes en vivo, la
 * forma de pago y una sola acción. En celular la acción va fija abajo; en escritorio, en el panel lateral.
 * El total no se anima: se recalcula cada vez que se marca un grupo.
 */
@Component({
  selector: 'app-plan',
  imports: [TranslocoDirective, NgTemplateOutlet, Tapa, Icono, Ocupado, EtiquetaPlan, BandaPago],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pantalla pantalla--ancha" *transloco="let t; prefix: 'plan'">
      <div class="cabeza-pantalla">
        <h1 tabindex="-1">{{ t('titulo') }}</h1>
      </div>

      @if (planes()?.pagoSimulado) {
        <p class="aviso">{{ t('modoPrueba') }}</p>
      }
      @if (regreso(); as r) {
        <p class="aviso" [class.aviso--exito]="r === 'listo'" role="status">
          {{ t('regreso.' + r) }}
        </p>
      }

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (!suscripcion() || !planes()) {
        <div class="cargando" role="status" [attr.aria-label]="t('cargando')">
          @for (i of [1, 2, 3]; track i) {
            <span class="esqueleto fila-esqueleto" aria-hidden="true"></span>
          }
        </div>
      } @else if (suscripcion(); as s) {
        @if (s.aviso; as aviso) {
          <app-banda-pago [aviso]="aviso" [enPlan]="true" (actualizarTarjeta)="abrirPortal()" />
        }

        <section class="estado" aria-labelledby="estado-titulo">
          <h2 id="estado-titulo" class="solo-lector">{{ t('estado.titulo') }}</h2>
          @for (frase of frasesEstado(); track frase) {
            <p class="frase">{{ frase }}</p>
          }
          @if (s.pagoPendiente; as p) {
            <div class="ficha-pendiente">
              <p>
                {{
                  t('estado.fichaPendiente', {
                    monto: cifra(p.monto),
                    moneda: planes()!.moneda,
                    fecha: fecha(p.venceEn),
                  })
                }}
              </p>
              @if (p.urlFicha) {
                <a class="enlace enlace--solo" [href]="p.urlFicha">{{ t('estado.verFicha') }}</a>
              }
            </div>
          }
          @if (modo() === 'tarjeta') {
            <button
              class="boton boton--secundario"
              type="button"
              [appOcupado]="abriendoPortal()"
              (click)="abrirPortal()"
            >
              {{ t('tarjetaYFacturas') }}
            </button>
          }
        </section>

        <div class="columnas">
          <div class="principal">
            <section aria-labelledby="grupos-titulo">
              <h2 id="grupos-titulo">{{ t('grupos.titulo') }}</h2>
              <p class="nota">{{ t('grupos.nota.' + modo()) }}</p>
              @if (s.grupos.length === 0) {
                <p class="aviso">{{ t('grupos.vacio') }}</p>
              } @else {
                <ul class="filas grupos">
                  @for (g of s.grupos; track g.grupoId) {
                    <li>
                      <label class="fila-grupo">
                        <input
                          type="checkbox"
                          [checked]="elegidos().has(g.grupoId)"
                          (change)="alternar(g.grupoId)"
                          [attr.aria-describedby]="'detalle-' + g.grupoId"
                        />
                        <app-tapa [nivel]="g.nivel" [grado]="g.grado" />
                        <span class="texto">
                          <span class="nombre">{{ nombre(g) }}</span>
                          <span class="dato-suave detalle" [id]="'detalle-' + g.grupoId">
                            <span class="materia">{{
                              g.materia ?? texto('inicio.nivel.' + g.nivel)
                            }}</span>
                            <span class="cifras">{{
                              texto('inicio.alumnos.' + forma(g.alumnosActivos), {
                                cantidad: +g.alumnosActivos,
                              })
                            }}</span>
                          </span>
                        </span>
                        <app-etiqueta-plan
                          [plan]="g.plan.plan"
                          [diasPrueba]="g.plan.diasPrueba"
                          [hastaPublicar]="g.plan.pruebaHastaPublicar"
                        />
                      </label>
                    </li>
                  }
                </ul>
              }
            </section>

            @if (s.grupos.length > 0) {
              <div class="solo-celular">
                <ng-container
                  [ngTemplateOutlet]="pago"
                  [ngTemplateOutletContext]="{ $implicit: 'celular' }"
                />
              </div>
            }

            <section aria-labelledby="incluye-titulo">
              <h2 id="incluye-titulo">{{ t('incluye.titulo') }}</h2>
              <table class="incluye">
                <thead>
                  <tr>
                    <th scope="col">
                      <span class="solo-lector">{{ t('incluye.funcion') }}</span>
                    </th>
                    <th scope="col">{{ t('incluye.gratis') }}</th>
                    <th scope="col">{{ t('incluye.pro') }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (fila of filasIncluye; track fila.clave) {
                    <tr>
                      <th scope="row">
                        {{
                          t('incluye.filas.' + fila.clave, {
                            urgentes: planes()!.urgentesPorTutorPorMes,
                          })
                        }}
                      </th>
                      <td>
                        <ng-container
                          [ngTemplateOutlet]="marca"
                          [ngTemplateOutletContext]="{ $implicit: fila.gratis }"
                        />
                      </td>
                      <td>
                        <ng-container
                          [ngTemplateOutlet]="marca"
                          [ngTemplateOutletContext]="{ $implicit: true }"
                        />
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
              <p class="nota cifras">
                {{
                  t('incluye.precio', {
                    primero: cifra(planes()!.precioPrimerGrupo),
                    adicional: cifra(planes()!.precioGrupoAdicional),
                    moneda: planes()!.moneda,
                  })
                }}
              </p>
            </section>

            @if (s.pagos.length > 0) {
              <section aria-labelledby="pagos-titulo">
                <h2 id="pagos-titulo">{{ t('pagos.titulo') }}</h2>
                <ul class="filas pagos">
                  @for (p of s.pagos; track p.id) {
                    <li class="pago">
                      <span class="texto">
                        <span class="concepto">{{ concepto(p) }}</span>
                        <span class="dato-suave">{{ fecha(p.pagadoEn ?? p.creadoEn) }}</span>
                      </span>
                      <span class="monto cifras">{{ cifra(p.monto) }} {{ planes()!.moneda }}</span>
                      <span class="estado-pago" [attr.data-estado]="p.estado">{{
                        t('pagos.estados.' + p.estado)
                      }}</span>
                    </li>
                  }
                </ul>
              </section>
            }

            @if (planes()!.pagoSimulado && modo() === 'tarjeta') {
              <section class="pruebas" aria-labelledby="pruebas-titulo">
                <h2 id="pruebas-titulo">{{ t('pruebas.titulo') }}</h2>
                <p class="nota">{{ t('pruebas.nota') }}</p>
                <div class="acciones">
                  <button
                    class="boton boton--secundario"
                    type="button"
                    [appOcupado]="enviando()"
                    (click)="renovarSimulado(true)"
                  >
                    {{ t('pruebas.pagado') }}
                  </button>
                  <button
                    class="boton boton--secundario"
                    type="button"
                    [appOcupado]="enviando()"
                    (click)="renovarSimulado(false)"
                  >
                    {{ t('pruebas.rechazado') }}
                  </button>
                </div>
              </section>
            }

            @if (s.grupos.length > 0 && mostrarAccion()) {
              <div class="pie-accion solo-celular">
                <ng-container [ngTemplateOutlet]="accion" />
              </div>
            }
          </div>

          @if (s.grupos.length > 0) {
            <aside class="lateral solo-escritorio" aria-labelledby="pago-titulo-escritorio">
              <ng-container
                [ngTemplateOutlet]="pago"
                [ngTemplateOutletContext]="{ $implicit: 'escritorio' }"
              />
              @if (mostrarAccion()) {
                <ng-container [ngTemplateOutlet]="accion" />
              }
            </aside>
          }
        </div>
      }

      <!-- Total y forma de pago: una copia por tamaño, con nombres propios para que sus radios no se mezclen. -->
      <ng-template #pago let-lugar>
        <section class="pago-panel" [attr.aria-labelledby]="'pago-titulo-' + lugar">
          <h2 [id]="'pago-titulo-' + lugar">{{ t('total.titulo') }}</h2>
          <p class="total cifras">
            <span class="monto">{{ cifra(totalMes()) }}</span>
            <span class="unidad">{{ t('total.alMes', { moneda: planes()!.moneda }) }}</span>
          </p>
          <p class="desglose cifras dato-suave">{{ desglose() }}</p>

          @if (modo() === 'contratar' && formas().length > 1) {
            <fieldset class="formas">
              <legend>{{ t('forma.titulo') }}</legend>
              @for (f of formas(); track f.clave) {
                <label [class.fuera]="f.fuera">
                  <input
                    type="radio"
                    [name]="'forma-' + lugar"
                    [value]="f.clave"
                    [checked]="f.clave === claveForma()"
                    [disabled]="f.fuera"
                    (change)="elegirForma(f.forma)"
                  />
                  <span class="opcion">
                    <span class="nombre-forma">{{ t('forma.' + f.clave) }}</span>
                    <span class="cifras dato-suave">{{ f.detalle }}</span>
                  </span>
                </label>
              }
            </fieldset>
            @if (notaForma(); as nota) {
              <p class="nota">{{ nota }}</p>
            }
          } @else if (modo() === 'agregar' && cambio().agregados.length > 0) {
            <fieldset class="formas">
              <legend>
                {{ t('forma.agregarTitulo', { fecha: fecha(suscripcion()!.pagadoHasta) }) }}
              </legend>
              @for (clave of formasAgregar(); track clave) {
                <label>
                  <input
                    type="radio"
                    [name]="'forma-' + lugar"
                    [value]="clave"
                    [checked]="clave === formaPago().modalidad"
                    (change)="elegirForma({ modalidad: clave, meses: null })"
                  />
                  <span class="opcion">
                    <span class="nombre-forma">{{ t('forma.agregar.' + clave) }}</span>
                  </span>
                </label>
              }
            </fieldset>
          }
        </section>
      </ng-template>

      <ng-template #accion>
        @if (errorAccion(); as codigo) {
          <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
        }
        <button
          class="boton boton--ancho"
          type="button"
          [disabled]="!puedeActuar()"
          [appOcupado]="enviando()"
          (click)="actuar()"
        >
          {{ textoAccion() }}
        </button>
        @if (notaAccion(); as nota) {
          <p class="nota nota-accion">{{ nota }}</p>
        }
      </ng-template>

      <ng-template #marca let-incluye>
        @if (incluye) {
          <app-icono nombre="guardado" class="incluye-si" />
          <span class="solo-lector">{{ t('incluye.si') }}</span>
        } @else {
          <span class="incluye-no" aria-hidden="true">–</span>
          <span class="solo-lector">{{ t('incluye.no') }}</span>
        }
      </ng-template>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    h2 {
      font-size: var(--texto-lg);
    }

    section {
      display: grid;
      gap: var(--espacio-3);
    }

    .principal {
      display: grid;
      gap: var(--espacio-6);
      min-width: 0;
    }

    .cargando {
      display: grid;
      gap: var(--espacio-3);
    }

    .fila-esqueleto {
      height: 4.5rem;
    }

    /* El estado: una frase grande por hecho, sin tarjeta. */
    .estado {
      justify-items: start;
      gap: var(--espacio-2);
    }

    .frase {
      max-inline-size: 40ch;
      font-size: var(--texto-xl);
      font-weight: var(--peso-fuerte);
      line-height: var(--interlineado-ajustado);
      text-wrap: balance;
    }

    .ficha-pendiente {
      display: grid;
      justify-items: start;
      gap: var(--espacio-1);
      color: var(--color-aviso-tinta);
    }

    .nota {
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
      max-inline-size: 65ch;
    }

    .fila-grupo {
      display: grid;
      grid-template-columns: auto auto minmax(0, 1fr) auto;
      align-items: center;
      gap: var(--espacio-3);
      min-height: 5rem;
      padding-block: var(--espacio-2);
      cursor: pointer;
    }

    .fila-grupo input {
      width: 1.25rem;
      height: 1.25rem;
      margin: 0;
      accent-color: var(--color-accion);
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

    .detalle {
      display: flex;
      flex-wrap: wrap;
      gap: 0 0.75em;
      min-width: 0;
    }

    .materia {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    /* El total: la cifra grande y su unidad al lado; el desglose en cifras debajo. */
    .pago-panel {
      gap: var(--espacio-2);
    }

    .total {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: var(--espacio-2);
    }

    .total .monto {
      font-size: var(--texto-2xl);
      font-weight: var(--peso-fuerte);
      line-height: 1;
      color: var(--color-accion-tinta);
    }

    .total .unidad {
      color: var(--color-texto-suave);
      font-weight: var(--peso-medio);
    }

    .formas {
      display: grid;
      gap: var(--espacio-2);
      margin: var(--espacio-3) 0 0;
      padding: 0;
      border: 0;
    }

    .formas legend {
      margin-bottom: var(--espacio-2);
      padding: 0;
      font-size: var(--texto-sm);
      font-weight: var(--peso-medio);
    }

    .formas label {
      display: flex;
      align-items: center;
      gap: var(--espacio-3);
      min-height: 3.5rem;
      padding: var(--espacio-2) var(--espacio-3);
      border-radius: var(--radio-md);
      background: var(--color-superficie-hundida);
      cursor: pointer;
      transition: background-color var(--dur-small) var(--ease-out);
    }

    .formas label:has(input:checked) {
      background: var(--color-accion-tinte);
      box-shadow: inset 0 0 0 2px var(--color-accion);
      color: var(--color-accion-tinta);
    }

    .formas label:has(input:focus-visible) {
      box-shadow: var(--foco);
    }

    .formas label.fuera {
      color: var(--color-texto-suave);
      cursor: not-allowed;
    }

    .formas input {
      margin: 0;
      accent-color: var(--color-accion);
    }

    .opcion {
      display: grid;
      gap: 0.125rem;
    }

    .nombre-forma {
      font-weight: var(--peso-medio);
    }

    .nota-accion {
      margin-block-start: var(--espacio-1);
    }

    /* Gratis contra Pro: tabla corta, la función a la izquierda y una marca por plan. */
    .incluye {
      inline-size: 100%;
      border-collapse: collapse;
    }

    .incluye th,
    .incluye td {
      padding: var(--espacio-3) var(--espacio-2);
      border-bottom: 1px solid var(--color-divisoria);
      text-align: center;
      vertical-align: middle;
    }

    .incluye thead th {
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
      font-weight: var(--peso-medio);
    }

    .incluye tbody th {
      padding-inline-start: 0;
      font-weight: var(--peso-normal);
      text-align: start;
    }

    .incluye td {
      inline-size: 4.5rem;
    }

    .incluye-si {
      color: var(--color-exito-tinta);
    }

    .incluye-no {
      color: var(--color-texto-tenue);
    }

    .pago {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      align-items: center;
      gap: var(--espacio-1) var(--espacio-4);
      padding-block: var(--espacio-3);
    }

    .concepto {
      font-weight: var(--peso-medio);
    }

    .monto {
      font-weight: var(--peso-fuerte);
    }

    .estado-pago {
      grid-column: 2;
      justify-self: end;
      padding: 0 var(--espacio-2);
      border-radius: var(--radio-sm);
      background: var(--color-superficie-hundida);
      font-size: var(--texto-xs);
      font-weight: var(--peso-medio);
    }

    .estado-pago[data-estado='pagado'] {
      background: var(--color-exito-tinte);
      color: var(--color-exito-tinta);
    }

    .estado-pago[data-estado='pendiente'] {
      background: var(--color-aviso-tinte);
      color: var(--color-aviso-tinta);
    }

    .pruebas .acciones {
      display: flex;
      flex-wrap: wrap;
      gap: var(--espacio-2);
    }

    .lateral {
      display: grid;
      gap: var(--espacio-4);
    }

    @media (forced-colors: active) {
      .formas label:has(input:checked) {
        outline: 2px solid Highlight;
        outline-offset: -2px;
      }
    }
  `,
})
export class Plan implements OnInit {
  protected readonly cifra = cifra;
  protected readonly forma = forma;
  private readonly servicio = inject(PlanesService);
  private readonly i18n = inject(TranslocoService);
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);

  /** Desde un candado: el grupo que se quiere activar. */
  readonly grupo = input<string | null>(null);
  /** Al volver del Checkout: `listo` o `cancelado`. */
  readonly pago = input<string | null>(null);
  readonly portal = input<string | null>(null);

  protected readonly planes = signal<PlanesDto | null>(null);
  protected readonly suscripcion = signal<SuscripcionDto | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly errorAccion = signal<string | null>(null);
  protected readonly enviando = signal(false);
  protected readonly abriendoPortal = signal(false);
  protected readonly elegidos = signal<ReadonlySet<string>>(new Set());
  protected readonly formaPago = signal<FormaDePago>({ modalidad: 'tarjeta', meses: null });
  private temporizador: ReturnType<typeof setTimeout> | null = null;

  protected readonly filasIncluye = [
    { clave: 'captura', gratis: true },
    { clave: 'excel', gratis: true },
    { clave: 'avisos', gratis: true },
    { clave: 'cierre', gratis: false },
    { clave: 'boletas', gratis: false },
    { clave: 'whatsapp', gratis: false },
  ] as const;

  /** El aviso del regreso del Checkout: se lee de la dirección una vez y se va en cuanto el docente hace otra cosa. */
  protected readonly regreso = signal<'listo' | 'cancelado' | 'portalSimulado' | null>(null);
  protected readonly modo = computed(() => {
    const s = this.suscripcion();
    return s ? modoPlan(s) : 'contratar';
  });
  protected readonly cambio = computed(() => {
    const s = this.suscripcion();
    return s ? cambioGrupos(s, this.elegidos()) : { agregados: [], quitados: [] };
  });
  protected readonly totalMes = computed(() => {
    const p = this.planes();
    return p ? mensual(p, this.elegidos().size) : 0;
  });
  /** "79 primer grupo + 2 × 29": de dónde sale el total, en cifras. */
  protected readonly desglose = computed(() => {
    const p = this.planes();
    const n = this.elegidos().size;
    if (!p || n === 0) {
      return this.texto('plan.total.ninguno');
    }
    const primero = this.texto('plan.total.primero', { precio: cifra(p.precioPrimerGrupo) });
    return n === 1
      ? primero
      : `${primero} + ${this.texto('plan.total.adicionales', {
          n: n - 1,
          precio: cifra(p.precioGrupoAdicional),
        })}`;
  });

  /** Tarjeta cada mes y, si está habilitado, OXXO por los meses del prepago; con su monto. */
  protected readonly formas = computed(() => {
    const p = this.planes();
    if (!p) {
      return [];
    }
    const n = this.elegidos().size;
    const lista: {
      clave: string;
      forma: FormaDePago;
      detalle: string;
      fuera: boolean;
    }[] = [
      {
        clave: 'tarjeta',
        forma: { modalidad: 'tarjeta', meses: null },
        detalle: this.texto('plan.forma.detalleTarjeta', {
          monto: cifra(mensual(p, n)),
          moneda: p.moneda,
        }),
        fuera: this.tarjetaBloqueada(),
      },
    ];
    if (p.oxxoHabilitado) {
      for (const meses of p.mesesPrepago.map(Number)) {
        const opcion: FormaDePago = { modalidad: 'oxxo', meses };
        const monto = totalAPagar(p, n, opcion);
        lista.push({
          clave: `oxxo${meses}`,
          forma: opcion,
          detalle: this.texto('plan.forma.detalleOxxo', { monto: cifra(monto), moneda: p.moneda }),
          fuera: n > 0 && (monto < Number(p.montoMinimoOxxo) || monto > Number(p.montoMaximoOxxo)),
        });
      }
    }
    return lista;
  });
  protected readonly formasAgregar = computed(() =>
    this.planes()?.oxxoHabilitado ? (['tarjeta', 'oxxo'] as const) : (['tarjeta'] as const),
  );
  protected readonly claveForma = computed(() => {
    const f = this.formaPago();
    return f.modalidad === 'oxxo' ? `oxxo${f.meses}` : 'tarjeta';
  });
  /** RN-089: con el prepago todavía pagado, la tarjeta espera a que venza. */
  private readonly tarjetaBloqueada = computed(() => {
    const s = this.suscripcion();
    return !!s && prepagoVigente(s);
  });
  protected readonly notaForma = computed(() => {
    const p = this.planes();
    if (this.tarjetaBloqueada()) {
      return this.texto('plan.forma.tarjetaAlVencer', {
        fecha: this.fecha(this.suscripcion()!.pagadoHasta),
      });
    }
    if (p && this.formas().some((f) => f.fuera && f.forma.modalidad === 'oxxo')) {
      return this.texto('plan.forma.fueraDeRango', {
        minimo: cifra(p.montoMinimoOxxo),
        maximo: cifra(p.montoMaximoOxxo),
        moneda: p.moneda,
      });
    }
    if (this.formaPago().modalidad === 'oxxo' && p) {
      return this.texto('plan.forma.notaOxxo');
    }
    return null;
  });

  /** Con suscripción, la acción solo aparece cuando hay un cambio que guardar o pagar. */
  protected readonly mostrarAccion = computed(
    () =>
      this.modo() === 'contratar' ||
      this.cambio().agregados.length > 0 ||
      this.cambio().quitados.length > 0,
  );
  protected readonly puedeActuar = computed(() => {
    const n = this.elegidos().size;
    const { agregados, quitados } = this.cambio();
    switch (this.modo()) {
      case 'tarjeta':
        return n > 0 && (agregados.length > 0 || quitados.length > 0);
      case 'agregar':
        return n > 0 && (agregados.length > 0 || quitados.length > 0);
      default:
        return n > 0 && !this.formas().find((f) => f.clave === this.claveForma())?.fuera;
    }
  });

  protected readonly textoAccion = computed(() => {
    const p = this.planes();
    if (!p) {
      return '';
    }
    const moneda = p.moneda;
    const { agregados } = this.cambio();
    switch (this.modo()) {
      case 'tarjeta':
        return this.texto('plan.accion.guardar');
      case 'agregar':
        return agregados.length > 0
          ? this.texto(`plan.accion.agregar.${forma(agregados.length)}`, { n: agregados.length })
          : this.texto('plan.accion.guardar');
      default: {
        if (this.elegidos().size === 0) {
          return this.texto('plan.accion.elige');
        }
        const f = this.formaPago();
        const monto = cifra(totalAPagar(p, this.elegidos().size, f));
        return f.modalidad === 'oxxo'
          ? this.texto('plan.accion.oxxo', { monto, moneda })
          : this.texto('plan.accion.tarjeta', { monto, moneda });
      }
    }
  });

  protected readonly notaAccion = computed(() => {
    const s = this.suscripcion();
    if (!s) {
      return null;
    }
    const { agregados, quitados } = this.cambio();
    if (this.modo() === 'tarjeta') {
      if (this.elegidos().size === 0) {
        return this.texto('plan.nota.cancelar');
      }
      const partes: string[] = [];
      if (agregados.length > 0) {
        partes.push(this.texto('plan.nota.prorrateo'));
      }
      if (quitados.length > 0) {
        partes.push(this.texto('plan.nota.alCorte', { fecha: this.fecha(s.proximoCobro) }));
      }
      return partes.join(' ') || null;
    }
    if (this.modo() === 'agregar') {
      const partes: string[] = [];
      if (agregados.length > 0) {
        partes.push(this.texto('plan.nota.agregar', { fecha: this.fecha(s.pagadoHasta) }));
      }
      if (quitados.length > 0) {
        partes.push(this.texto('plan.nota.quitarPrepago', { fecha: this.fecha(s.pagadoHasta) }));
      }
      return partes.join(' ') || null;
    }
    return this.formaPago().modalidad === 'tarjeta' && this.elegidos().size > 0
      ? this.texto('plan.nota.tarjeta')
      : null;
  });

  /** Una frase por hecho: lo pagado y, aparte, la prueba. */
  protected readonly frasesEstado = computed(() => {
    const s = this.suscripcion();
    const p = this.planes();
    if (!s || !p) {
      return [];
    }
    const frases: string[] = [];
    const pagados = s.grupos.filter((g) => g.enSuscripcion).length;
    const total = cifra(s.totalMensual);
    if (s.modalidad === 'tarjeta' && s.estado === 'activa' && pagados > 0) {
      frases.push(
        s.proximoCobro
          ? this.texto(`plan.estado.tarjeta.${forma(pagados)}`, {
              n: pagados,
              monto: total,
              moneda: p.moneda,
              fecha: this.fecha(s.proximoCobro),
            })
          : this.texto(`plan.estado.pro.${forma(pagados)}`, { n: pagados }),
      );
    } else if (s.modalidad === 'prepago_oxxo' && s.estado === 'activa' && s.pagadoHasta) {
      frases.push(
        this.texto(`plan.estado.prepago.${forma(pagados)}`, {
          n: pagados,
          fecha: this.fecha(s.pagadoHasta),
        }),
      );
    } else if (s.estado === 'en_gracia' && pagados > 0) {
      frases.push(this.texto(`plan.estado.pro.${forma(pagados)}`, { n: pagados }));
    }
    if (s.prueba?.vigente) {
      const grupo = s.grupos.find((g) => g.grupoId === s.prueba!.grupoId);
      const nombre = grupo ? nombreGrupo(grupo.grado, grupo.nombre) : s.prueba.grupoNombre;
      const dias = Number(s.prueba.diasRestantes);
      frases.push(
        s.prueba.hastaPublicar
          ? this.texto('plan.estado.pruebaHastaPublicar', { grupo: nombre, dias })
          : this.texto(`plan.estado.prueba.${forma(dias)}`, { grupo: nombre, dias }),
      );
    }
    if (frases.length === 0) {
      frases.push(this.texto('plan.estado.gratis'));
    }
    return frases;
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      if (this.temporizador) {
        clearTimeout(this.temporizador);
      }
    });
  }

  async ngOnInit(): Promise<void> {
    const pago = this.pago();
    this.regreso.set(
      this.portal() === 'simulado'
        ? 'portalSimulado'
        : pago === 'listo' || pago === 'cancelado'
          ? pago
          : null,
    );
    await this.cargar();
    // Recargar la página no vuelve a decir "recibimos tu pago".
    if (this.regreso()) {
      void this.router.navigate([], { replaceUrl: true });
    }
    // El proveedor puede confirmar unos segundos después de volver del Checkout: se lee una vez más.
    if (pago === 'listo') {
      this.temporizador = setTimeout(() => void this.releer(), RELECTURA_MS);
    }
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      const [planes, s] = await Promise.all([this.servicio.planes(), this.servicio.suscripcion()]);
      this.planes.set(planes);
      this.mostrar(s);
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected texto(clave: string, datos?: Record<string, unknown>): string {
    return this.i18n.translate(clave, datos);
  }

  protected nombre(g: GrupoPlanDto): string {
    return nombreGrupo(g.grado, g.nombre);
  }

  protected fecha(instante: string | null | undefined): string {
    return instante ? fechaLarga(instante) : '';
  }

  protected concepto(p: PagoDto): string {
    const grupos = Number(p.grupos);
    return p.tipo === 'prepago'
      ? this.texto(`plan.pagos.prepago.${forma(grupos)}`, { meses: p.meses, n: grupos })
      : this.texto(`plan.pagos.agregar.${forma(grupos)}`, { n: grupos });
  }

  protected alternar(id: string): void {
    this.errorAccion.set(null);
    this.elegidos.update((actual) => {
      const nuevo = new Set(actual);
      if (!nuevo.delete(id)) {
        nuevo.add(id);
      }
      return nuevo;
    });
  }

  protected elegirForma(elegida: FormaDePago): void {
    this.errorAccion.set(null);
    this.formaPago.set(elegida);
  }

  protected async actuar(): Promise<void> {
    this.regreso.set(null);
    const s = this.suscripcion();
    if (!s || !this.puedeActuar()) {
      return;
    }
    this.errorAccion.set(null);
    this.enviando.set(true);
    try {
      const elegidos = [...this.elegidos()];
      const { agregados, quitados } = this.cambio();
      if (this.modo() === 'tarjeta') {
        this.mostrar(await this.servicio.cambiarGrupos(elegidos));
        this.avisos.mostrar(this.texto('plan.guardado'), 'exito');
        return;
      }
      if (this.modo() === 'agregar') {
        // Quitar no pasa por el proveedor; agregar se paga en el Checkout.
        if (quitados.length > 0) {
          const quedan = elegidos.filter((id) => !agregados.includes(id));
          this.mostrar(await this.servicio.cambiarGrupos(quedan));
        }
        if (agregados.length === 0) {
          this.avisos.mostrar(this.texto('plan.guardado'), 'exito');
          return;
        }
        this.irA(await this.servicio.checkout(elegidos, this.formaPago().modalidad, null));
        return;
      }
      const f = this.formaPago();
      this.irA(await this.servicio.checkout(elegidos, f.modalidad, f.meses));
    } catch (e) {
      this.errorAccion.set(codigoDeError(e));
    } finally {
      this.enviando.set(false);
    }
  }

  protected async abrirPortal(): Promise<void> {
    this.abriendoPortal.set(true);
    try {
      this.irA(await this.servicio.portal());
    } catch (e) {
      this.avisos.mostrar(this.texto('errores.' + codigoDeError(e)), 'error');
    } finally {
      this.abriendoPortal.set(false);
    }
  }

  protected async renovarSimulado(pagada: boolean): Promise<void> {
    this.regreso.set(null);
    this.enviando.set(true);
    try {
      await this.servicio.renovarSimulado(pagada);
      this.mostrar(await this.servicio.suscripcion());
    } catch (e) {
      this.avisos.mostrar(this.texto('errores.' + codigoDeError(e)), 'error');
    } finally {
      this.enviando.set(false);
    }
  }

  private mostrar(s: SuscripcionDto): void {
    this.suscripcion.set(s);
    this.elegidos.set(eleccionInicial(s, this.grupo()));
    if (this.tarjetaBloqueada() && this.formaPago().modalidad === 'tarjeta') {
      const meses = Number(this.planes()?.mesesPrepago[0] ?? 0);
      if (meses > 0 && this.planes()?.oxxoHabilitado) {
        this.formaPago.set({ modalidad: 'oxxo', meses });
      }
    }
  }

  private async releer(): Promise<void> {
    try {
      this.mostrar(await this.servicio.suscripcion());
    } catch {
      // Lo que ya se ve sigue siendo válido; la próxima visita lo vuelve a leer.
    }
  }

  /** El Checkout y el portal del proveedor son de otro sitio; la página de prueba es de la app. */
  private irA(url: string): void {
    const propia = new URL(url, location.origin);
    if (propia.origin === location.origin) {
      void this.router.navigateByUrl(propia.pathname + propia.search);
    } else {
      location.assign(url);
    }
  }
}
