import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  OnInit,
  afterNextRender,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TipoRubroAdminDto } from '../api/models/tipo-rubro-admin-dto';
import { TipoRubroEntrada } from '../api/models/tipo-rubro-entrada';
import { codigoDeError } from '../core/errores/problema';
import { AvisosService } from '../shared/avisos/avisos.service';
import { Hoja } from '../shared/hoja';
import { Ocupado } from '../shared/ocupado';
import { forma } from '../shared/plural';
import { AdminService } from './admin.service';
import { ESTILOS_PANEL } from './estilos';
import { entero, nombresSugeridos } from './panel';

interface Borrador {
  readonly nombre: string;
  readonly descripcion: string;
  readonly icono: string;
  readonly comportamiento: string;
  readonly formaSugerida: string;
  readonly nombres: string;
  readonly orden: string;
  readonly activo: boolean;
}

type Campo = 'nombre' | 'descripcion' | 'icono' | 'nombres' | 'orden';

const MAXIMOS = { nombre: 40, descripcion: 200, icono: 40 } as const;

/**
 * RF-092: el catálogo de tipos de rubro que ven los docentes. Un tipo nuevo usa un comportamiento del código
 * desplegado y queda disponible al instante (RN-092). Con rubros que lo usan no se borra ni cambia de
 * comportamiento: se desactiva y sus rubros siguen calculando (RN-094). Cada cambio queda en la auditoría.
 */
@Component({
  selector: 'app-tipos-rubro',
  imports: [TranslocoDirective, Hoja, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t; prefix: 'admin.tiposRubro'">
      <div class="cabeza cabeza--accion">
        <div class="cabeza">
          <h1 tabindex="-1">{{ t('titulo') }}</h1>
          <p class="resumen">{{ t('resumen') }}</p>
        </div>
        @if (tipos()) {
          <button class="boton" type="button" (click)="nuevo()">{{ t('nuevo') }}</button>
        }
      </div>

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (!tipos()) {
        <div class="cargando" role="status" [attr.aria-label]="t('cargando')">
          @for (i of [1, 2, 3, 4]; track i) {
            <span class="esqueleto" aria-hidden="true"></span>
          }
        </div>
      } @else if (tipos(); as lista) {
        <table class="libreta apilable" role="table">
          <thead role="rowgroup">
            <tr role="row">
              <th scope="col" role="columnheader" class="num">{{ t('orden') }}</th>
              <th scope="col" role="columnheader">{{ t('tipo') }}</th>
              <th scope="col" role="columnheader">{{ t('comportamiento') }}</th>
              <th scope="col" role="columnheader" class="num">{{ t('rubros') }}</th>
              <th scope="col" role="columnheader">{{ t('estado') }}</th>
              <th scope="col" role="columnheader">
                <span class="solo-lector">{{ t('acciones') }}</span>
              </th>
            </tr>
          </thead>
          <tbody role="rowgroup">
            @for (tipo of lista; track tipo.id) {
              <tr role="row" [class.inactivo]="!tipo.activo">
                <td role="cell" class="num orden" [attr.data-etiqueta]="t('orden')">
                  {{ tipo.orden }}
                </td>
                <th scope="row" role="rowheader">
                  {{ tipo.nombre }}
                  <span class="secundario">{{ tipo.descripcion }}</span>
                </th>
                <td role="cell" [attr.data-etiqueta]="t('comportamiento')">
                  <span>
                    {{ t('comportamientos.' + tipo.comportamiento) }}
                    <span class="secundario">{{ t('formas.' + tipo.formaSugerida) }}</span>
                  </span>
                </td>
                <td role="cell" class="num" [attr.data-etiqueta]="t('rubros')">
                  {{ entero(tipo.rubros) }}
                </td>
                <td role="cell" class="estado" [attr.data-etiqueta]="t('estado')">
                  <span class="sello" [attr.data-tono]="tipo.activo ? 'exito' : ''">{{
                    t(tipo.activo ? 'activo' : 'inactivo')
                  }}</span>
                </td>
                <td role="cell" class="accion">
                  <button
                    class="boton boton--secundario"
                    type="button"
                    (click)="editar(tipo)"
                    [attr.aria-label]="t('editarTipo', { nombre: tipo.nombre })"
                  >
                    {{ t('editar') }}
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      }

      <app-hoja etiqueta="tipo-titulo" (cerrada)="errorGuardar.set(null)">
        @if (borrador(); as b) {
          <form #formulario class="formulario" (submit)="guardar($event)" novalidate>
            <h2 id="tipo-titulo">
              {{ editando() ? t('editarTipo', { nombre: editando()!.nombre }) : t('nuevo') }}
            </h2>

            <div class="campo">
              <label for="tipo-nombre">{{ t('campos.nombre') }}</label>
              <input
                id="tipo-nombre"
                [attr.maxlength]="maximos.nombre"
                [value]="b.nombre"
                (input)="cambiar('nombre', valor($event))"
                [attr.aria-invalid]="falla('nombre')"
                [attr.aria-describedby]="falla('nombre') ? 'tipo-nombre-error' : null"
                autocomplete="off"
              />
              @if (falla('nombre')) {
                <p class="error" id="tipo-nombre-error">{{ t('validacion.nombre') }}</p>
              }
            </div>

            <div class="campo">
              <label for="tipo-descripcion">{{ t('campos.descripcion') }}</label>
              <textarea
                id="tipo-descripcion"
                rows="2"
                [attr.maxlength]="maximos.descripcion"
                [value]="b.descripcion"
                (input)="cambiar('descripcion', valor($event))"
                [attr.aria-invalid]="falla('descripcion')"
                aria-describedby="tipo-descripcion-ayuda"
              ></textarea>
              @if (falla('descripcion')) {
                <p class="error" id="tipo-descripcion-ayuda">
                  {{ t('validacion.descripcion', { maximo: maximos.descripcion }) }}
                </p>
              } @else {
                <p class="ayuda" id="tipo-descripcion-ayuda">
                  {{ t('ayudas.descripcion', { maximo: maximos.descripcion }) }}
                </p>
              }
            </div>

            <div class="campo">
              <label for="tipo-comportamiento">{{ t('campos.comportamiento') }}</label>
              <span class="selector">
                <select
                  id="tipo-comportamiento"
                  [value]="b.comportamiento"
                  [disabled]="enUso()"
                  (change)="cambiar('comportamiento', valor($event))"
                  aria-describedby="tipo-comportamiento-ayuda"
                >
                  @for (c of comportamientos(); track c) {
                    <option
                      [value]="c"
                      [selected]="c === b.comportamiento"
                      [textContent]="t('comportamientos.' + c)"
                    ></option>
                  }
                </select>
              </span>
              <p class="ayuda" id="tipo-comportamiento-ayuda">
                {{
                  enUso()
                    ? t('ayudas.comportamientoEnUso.' + forma(editando()!.rubros), {
                        n: editando()!.rubros,
                      })
                    : t('ayudas.comportamiento')
                }}
              </p>
            </div>

            <fieldset class="eleccion">
              <legend>{{ t('campos.formaSugerida') }}</legend>
              @for (f of formas; track f) {
                <label>
                  <input
                    type="radio"
                    name="forma-sugerida"
                    [value]="f"
                    [checked]="b.formaSugerida === f"
                    (change)="cambiar('formaSugerida', f)"
                  />
                  {{ t('formas.' + f) }}
                </label>
              }
            </fieldset>

            <div class="campo">
              <label for="tipo-nombres">{{ t('campos.nombres') }}</label>
              <textarea
                id="tipo-nombres"
                rows="3"
                [value]="b.nombres"
                (input)="cambiar('nombres', valor($event))"
                [attr.aria-invalid]="falla('nombres')"
                aria-describedby="tipo-nombres-ayuda"
              ></textarea>
              @if (falla('nombres')) {
                <p class="error" id="tipo-nombres-ayuda">{{ t('validacion.nombres') }}</p>
              } @else {
                <p class="ayuda" id="tipo-nombres-ayuda">{{ t('ayudas.nombres') }}</p>
              }
            </div>

            <div class="campos-en-fila">
              <div class="campo">
                <label for="tipo-orden">{{ t('campos.orden') }}</label>
                <input
                  id="tipo-orden"
                  class="cifras"
                  inputmode="numeric"
                  [value]="b.orden"
                  (input)="cambiar('orden', valor($event))"
                  [attr.aria-invalid]="falla('orden')"
                  aria-describedby="tipo-orden-ayuda"
                />
                <p class="ayuda" [class.error]="falla('orden')" id="tipo-orden-ayuda">
                  {{ t('ayudas.orden') }}
                </p>
              </div>
              <div class="campo">
                <label for="tipo-icono">{{ t('campos.icono') }}</label>
                <input
                  id="tipo-icono"
                  [attr.maxlength]="maximos.icono"
                  [value]="b.icono"
                  (input)="cambiar('icono', valor($event))"
                  [attr.aria-invalid]="falla('icono')"
                  aria-describedby="tipo-icono-ayuda"
                  autocapitalize="off"
                  spellcheck="false"
                />
                <p class="ayuda" [class.error]="falla('icono')" id="tipo-icono-ayuda">
                  {{ t('ayudas.icono') }}
                </p>
              </div>
            </div>

            <label class="interruptor">
              <span class="texto-interruptor">
                <span class="nombre">{{ t('campos.activo') }}</span>
                <span class="dato-suave" id="tipo-activo-ayuda">{{
                  t(b.activo ? 'ayudas.activo' : 'ayudas.inactivo')
                }}</span>
              </span>
              <input
                type="checkbox"
                role="switch"
                [checked]="b.activo"
                (change)="cambiar('activo', marcado($event))"
                aria-describedby="tipo-activo-ayuda"
              />
            </label>

            @if (errorGuardar(); as codigo) {
              <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
            }
            <div class="acciones-hoja">
              <button class="boton boton--ancho" type="submit" [appOcupado]="guardando()">
                {{ t(editando() ? 'guardar' : 'crear') }}
              </button>
              <button
                class="boton boton--secundario boton--ancho"
                type="button"
                (click)="hoja().cerrar()"
              >
                {{ t('cancelar') }}
              </button>
            </div>
          </form>
        }
      </app-hoja>
    </ng-container>
  `,
  styles: [
    ESTILOS_PANEL,
    `
      .cabeza--accion {
        display: flex;
        flex-wrap: wrap;
        align-items: end;
        justify-content: space-between;
        gap: var(--espacio-4);
      }

      tr.inactivo th,
      tr.inactivo td {
        color: var(--color-texto-suave);
      }

      td > span {
        display: inline-grid;
        gap: var(--espacio-1);
      }

      .orden {
        inline-size: 3.5rem;
      }

      .estado {
        white-space: nowrap;
      }

      .accion {
        text-align: end;
        vertical-align: middle;
      }

      .formulario {
        display: grid;
        gap: var(--espacio-4);
      }

      .formulario h2 {
        margin: 0;
        font-size: var(--texto-lg);
      }

      .eleccion {
        margin: 0;
      }

      .acciones-hoja {
        display: grid;
        gap: var(--espacio-2);
      }

      .interruptor {
        display: grid;
        grid-template-columns: minmax(0, 1fr) auto;
        align-items: center;
        gap: var(--espacio-4);
        min-block-size: var(--tactil-minimo);
        padding: var(--espacio-3) var(--espacio-4);
        border-radius: var(--radio-lg);
        background: var(--color-superficie-hundida);
        cursor: pointer;
      }

      .texto-interruptor {
        display: grid;
        gap: var(--espacio-1);
      }

      .texto-interruptor .nombre {
        font-weight: var(--peso-medio);
      }

      .interruptor input {
        position: relative;
        inline-size: 3rem;
        block-size: 1.75rem;
        margin: 0;
        border-radius: 1rem;
        background: var(--color-texto-tenue);
        appearance: none;
        cursor: inherit;
        transition: background-color var(--dur-small) var(--ease-out);
      }

      .interruptor input::before {
        content: '';
        position: absolute;
        inset-block-start: 0.25rem;
        inset-inline-start: 0.25rem;
        inline-size: 1.25rem;
        block-size: 1.25rem;
        border-radius: 50%;
        background: var(--color-superficie);
        box-shadow: 0 1px 2px oklch(0.25 0.03 260 / 0.2);
        transition: translate var(--dur-small) var(--ease-out);
      }

      @media (prefers-reduced-motion: reduce) {
        .interruptor input::before {
          transition: none;
        }
      }

      .formulario textarea {
        min-block-size: auto;
      }

      .interruptor input:checked {
        background: var(--color-exito);
      }

      .interruptor input:checked::before {
        translate: 1.25rem 0;
      }

      .interruptor input:focus-visible {
        box-shadow: var(--foco);
        outline: none;
      }

      @media (max-width: 47.99rem) {
        .libreta.apilable td.orden {
          display: none;
        }

        .libreta.apilable td.accion {
          display: block;
          padding-block-start: var(--espacio-2);
        }

        .libreta.apilable td.accion .boton {
          inline-size: 100%;
        }

        td > span {
          justify-items: start;
          text-align: start;
        }
      }

      @media (forced-colors: active) {
        .interruptor input {
          border: 2px solid ButtonText;
        }
      }
    `,
  ],
})
export class TiposRubro implements OnInit {
  private readonly servicio = inject(AdminService);
  private readonly i18n = inject(TranslocoService);
  private readonly avisos = inject(AvisosService);
  private readonly injector = inject(Injector);
  private readonly formulario = viewChild<ElementRef<HTMLFormElement>>('formulario');
  protected readonly hoja = viewChild.required(Hoja);

  protected readonly formas = ['numerica', 'cumplimiento'] as const;
  protected readonly maximos = MAXIMOS;
  protected readonly entero = entero;
  protected readonly forma = forma;

  protected readonly tipos = signal<TipoRubroAdminDto[] | null>(null);
  protected readonly comportamientos = signal<readonly string[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly editando = signal<TipoRubroAdminDto | null>(null);
  protected readonly borrador = signal<Borrador | null>(null);
  protected readonly intento = signal(false);
  protected readonly guardando = signal(false);
  protected readonly errorGuardar = signal<string | null>(null);

  /** RN-094: con rubros que lo usan, el comportamiento queda fijo. */
  protected readonly enUso = computed(() => Number(this.editando()?.rubros ?? 0) > 0);
  protected readonly fallas = computed<ReadonlySet<Campo>>(() => {
    const b = this.borrador();
    if (!b || !this.intento()) {
      return new Set();
    }
    const fallas = new Set<Campo>();
    const nombre = b.nombre.trim();
    if (!nombre || nombre.length > MAXIMOS.nombre) {
      fallas.add('nombre');
    }
    if (b.descripcion.trim().length > MAXIMOS.descripcion) {
      fallas.add('descripcion');
    }
    if (!/^[a-z_]{1,40}$/.test(b.icono.trim())) {
      fallas.add('icono');
    }
    const nombres = nombresSugeridos(b.nombres);
    if (
      nombres.length < 1 ||
      nombres.length > 5 ||
      nombres.some((n) => n.length > MAXIMOS.nombre)
    ) {
      fallas.add('nombres');
    }
    const orden = Number(b.orden);
    if (!/^\d+$/.test(b.orden.trim()) || orden < 1 || orden > 999) {
      fallas.add('orden');
    }
    return fallas;
  });

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      const [tipos, comportamientos] = await Promise.all([
        this.servicio.tipos(),
        this.servicio.comportamientos(),
      ]);
      this.tipos.set(tipos);
      this.comportamientos.set(comportamientos);
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected nuevo(): void {
    const ultimo = Math.max(0, ...(this.tipos() ?? []).map((t) => Number(t.orden)));
    this.abrir(null, {
      nombre: '',
      descripcion: '',
      icono: 'actividades',
      comportamiento: this.comportamientos()[0] ?? 'actividades',
      formaSugerida: 'numerica',
      nombres: '',
      orden: String(Math.min(999, ultimo + 1)),
      activo: true,
    });
  }

  protected editar(tipo: TipoRubroAdminDto): void {
    this.abrir(tipo, {
      nombre: tipo.nombre,
      descripcion: tipo.descripcion,
      icono: tipo.icono,
      comportamiento: tipo.comportamiento,
      formaSugerida: tipo.formaSugerida,
      nombres: tipo.nombresSugeridos.join('\n'),
      orden: String(tipo.orden),
      activo: tipo.activo,
    });
  }

  private abrir(tipo: TipoRubroAdminDto | null, borrador: Borrador): void {
    this.editando.set(tipo);
    this.borrador.set(borrador);
    this.intento.set(false);
    this.errorGuardar.set(null);
    this.hoja().abrir();
  }

  protected cambiar<K extends keyof Borrador>(campo: K, valor: Borrador[K]): void {
    this.borrador.update((b) => (b ? { ...b, [campo]: valor } : b));
  }

  protected falla(campo: Campo): boolean {
    return this.fallas().has(campo);
  }

  protected async guardar(evento: Event): Promise<void> {
    evento.preventDefault();
    const b = this.borrador();
    this.intento.set(true);
    if (this.fallas().size > 0) {
      // WCAG 3.3.1: en la hoja del celular el error puede quedar fuera de la vista; el foco lo lleva ahí.
      afterNextRender(
        () =>
          this.formulario()
            ?.nativeElement.querySelector<HTMLElement>('[aria-invalid="true"]')
            ?.focus(),
        { injector: this.injector },
      );
      return;
    }
    if (!b || this.guardando()) {
      return;
    }
    const entrada: TipoRubroEntrada = {
      nombre: b.nombre.trim().replace(/\s+/g, ' '),
      descripcion: b.descripcion.trim(),
      icono: b.icono.trim(),
      comportamiento: b.comportamiento,
      formaSugerida: b.formaSugerida,
      nombresSugeridos: nombresSugeridos(b.nombres),
      orden: Number(b.orden),
      activo: b.activo,
    };
    this.guardando.set(true);
    this.errorGuardar.set(null);
    try {
      const actual = this.editando();
      const guardado = actual
        ? await this.servicio.editarTipo(actual.id, entrada)
        : await this.servicio.crearTipo(entrada);
      this.tipos.update((lista) =>
        [...(lista ?? []).filter((t) => t.id !== guardado.id), guardado].sort(
          (x, y) => Number(x.orden) - Number(y.orden) || x.nombre.localeCompare(y.nombre, 'es-MX'),
        ),
      );
      this.hoja().cerrar();
      this.avisos.mostrar(
        this.i18n.translate(actual ? 'admin.tiposRubro.guardado' : 'admin.tiposRubro.creado', {
          nombre: guardado.nombre,
        }),
      );
    } catch (e) {
      this.errorGuardar.set(codigoDeError(e));
    } finally {
      this.guardando.set(false);
    }
  }

  protected valor(evento: Event): string {
    return (evento.target as HTMLInputElement).value;
  }

  protected marcado(evento: Event): boolean {
    return (evento.target as HTMLInputElement).checked;
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }
}
