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
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { Api } from '../../api/api';
import { archivarGrupo } from '../../api/fn/grupos/archivar-grupo';
import { borrarGrupo } from '../../api/fn/grupos/borrar-grupo';
import { editarGrupo } from '../../api/fn/grupos/editar-grupo';
import { GrupoDto } from '../../api/models/grupo-dto';
import { codigoDeError } from '../../core/errores/problema';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { PRIMER_ERROR, crearEnfoque } from '../../shared/foco';
import { GRADOS, Nivel, nombreGrupo } from '../../shared/grados';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { controlarPortada } from '../../shared/portada/portada.service';
import { Tapa } from '../../shared/tapa';
import { GruposService, portadaDeGrupo } from './grupos.service';

export type Redondeo = 'cercano' | 'arriba' | 'abajo';
const REDONDEOS: readonly Redondeo[] = ['cercano', 'arriba', 'abajo'];
const DECIMALES = [0, 1, 2] as const;

/**
 * Calificación de 0 a 10 con hasta dos decimales (RN-030), escrita con punto o coma. Devuelve el texto
 * normalizado: se manda así a la API para que llegue exacta a `decimal`, sin pasar por punto flotante.
 */
export function leerCalificacion(texto: string): string | null {
  const limpio = texto.trim().replace(',', '.');
  if (!/^\d{1,2}(\.\d{1,2})?$/.test(limpio)) {
    return null;
  }
  return centesimas(limpio) <= 1000 ? limpio : null;
}

/** "7.5" son 750 centésimas: compara calificaciones con enteros. */
export function centesimas(calificacion: string): number {
  const [enteros, decimales = ''] = calificacion.split('.');
  return Number(enteros) * 100 + Number(decimales.padEnd(2, '0'));
}

/**
 * Ejemplo de RN-035 con aritmética entera (milésimas): 7.456 con 1 decimal da 7.5 hacia arriba, 7.4 hacia
 * abajo y 7.5 al más cercano con medio hacia arriba.
 */
export function redondearEjemplo(milesimas: number, decimales: number, redondeo: Redondeo): string {
  const paso = 10 ** (3 - decimales);
  const cociente = Math.floor(milesimas / paso);
  const resto = milesimas % paso;
  const unidades =
    redondeo === 'arriba'
      ? cociente + (resto > 0 ? 1 : 0)
      : redondeo === 'abajo'
        ? cociente
        : cociente + (resto * 2 >= paso ? 1 : 0);
  const texto = String(unidades).padStart(decimales + 1, '0');
  return decimales ? `${texto.slice(0, -decimales)}.${texto.slice(-decimales)}` : texto;
}

/**
 * Configuración del grupo: nombre, grado y materia; calificación mínima, aprobatoria, decimales y
 * redondeo de la final (RN-030, RN-035); archivar (RN-018) y borrar.
 */
@Component({
  selector: 'app-configuracion-grupo',
  imports: [TranslocoDirective, RouterLink, Icono, Ocupado, Tapa],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pantalla pantalla--ancha" *transloco="let t; prefix: 'configuracion'">
      <a class="volver" [routerLink]="['/grupos', id()]">
        <app-icono nombre="volver" />
        {{ t('volver') }}
      </a>
      <h1>{{ t('titulo') }}</h1>

      @if (grupo(); as g) {
        @if (g.archivadoEn) {
          <p class="aviso aviso--pendiente">{{ t('archivado') }}</p>
        }
        <div class="columnas">
          <form class="formulario columna" (submit)="$event.preventDefault(); guardar()" novalidate>
            <section aria-labelledby="datos-titulo">
              <h2 id="datos-titulo">{{ t('datos') }}</h2>
              <div class="campo">
                <label for="config-nombre">{{ t('nombre') }}</label>
                <input
                  id="config-nombre"
                  name="nombre"
                  maxlength="40"
                  autocomplete="off"
                  [readonly]="!!g.archivadoEn"
                  [value]="nombre()"
                  (input)="nombre.set(valor($event))"
                  [attr.aria-invalid]="intento() && !nombre().trim()"
                  [attr.aria-describedby]="
                    intento() && !nombre().trim() ? 'config-nombre-error' : null
                  "
                />
                @if (intento() && !nombre().trim()) {
                  <p id="config-nombre-error" class="error">{{ t('nombreRequerido') }}</p>
                }
              </div>
              <fieldset class="eleccion grados" [disabled]="!!g.archivadoEn">
                <legend>{{ t('grado', { nivel: t('niveles.' + g.nivel) }) }}</legend>
                @for (n of grados(); track n) {
                  <label>
                    <input
                      type="radio"
                      name="grado"
                      [value]="n"
                      [checked]="grado() === n"
                      (change)="grado.set(n)"
                    />
                    <app-tapa [nivel]="g.nivel" [grado]="n" tamano="chica" />
                    <span class="cifras">{{ n }}°</span>
                  </label>
                }
              </fieldset>
              @if (g.nivel === 'secundaria') {
                <div class="campo">
                  <label for="config-materia">{{ t('materia') }}</label>
                  <input
                    id="config-materia"
                    name="materia"
                    maxlength="60"
                    autocomplete="off"
                    [readonly]="!!g.archivadoEn"
                    [value]="materia()"
                    (input)="materia.set(valor($event))"
                  />
                </div>
              }
            </section>

            <section aria-labelledby="calificacion-titulo">
              <div>
                <h2 id="calificacion-titulo">{{ t('calificacion') }}</h2>
                <p class="dato-suave">{{ t('calificacionNota') }}</p>
              </div>
              <div class="campos-en-fila">
                <div class="campo">
                  <label for="config-minima">{{ t('minima') }}</label>
                  <input
                    id="config-minima"
                    name="minima"
                    class="cifras"
                    inputmode="decimal"
                    autocomplete="off"
                    [readonly]="!!g.archivadoEn"
                    [value]="minima()"
                    (input)="minima.set(valor($event))"
                    [attr.aria-invalid]="intento() && !!errorCalificacion()"
                    [attr.aria-describedby]="
                      intento() && errorCalificacion()
                        ? 'config-calificacion-error config-minima-ayuda'
                        : 'config-minima-ayuda'
                    "
                  />
                  <p id="config-minima-ayuda" class="ayuda">{{ t('minimaAyuda') }}</p>
                </div>
                <div class="campo">
                  <label for="config-aprobatoria">{{ t('aprobatoria') }}</label>
                  <input
                    id="config-aprobatoria"
                    name="aprobatoria"
                    class="cifras"
                    inputmode="decimal"
                    autocomplete="off"
                    [readonly]="!!g.archivadoEn"
                    [value]="aprobatoria()"
                    (input)="aprobatoria.set(valor($event))"
                    [attr.aria-invalid]="intento() && !!errorCalificacion()"
                    [attr.aria-describedby]="
                      intento() && errorCalificacion() ? 'config-calificacion-error' : null
                    "
                  />
                </div>
              </div>
              @if (intento() && errorCalificacion(); as codigo) {
                <p id="config-calificacion-error" class="error">{{ t('errores.' + codigo) }}</p>
              }
              <fieldset class="eleccion en-tres" [disabled]="!!g.archivadoEn">
                <legend>{{ t('decimales') }}</legend>
                @for (d of decimalesOpciones; track d) {
                  <label>
                    <input
                      type="radio"
                      name="decimales"
                      [value]="d"
                      [checked]="decimales() === d"
                      (change)="decimales.set(d)"
                    />
                    {{ t('decimalesN', { cantidad: d }) }}
                  </label>
                }
              </fieldset>
              <fieldset class="eleccion en-columna" [disabled]="!!g.archivadoEn">
                <legend>{{ t('redondeo') }}</legend>
                @for (r of redondeos; track r) {
                  <label>
                    <input
                      type="radio"
                      name="redondeo"
                      [value]="r"
                      [checked]="redondeo() === r"
                      (change)="redondeo.set(r)"
                    />
                    {{ t('redondeos.' + r) }}
                  </label>
                }
              </fieldset>
              <p class="ejemplo" aria-live="polite">
                {{ t('ejemplo') }}
                <span class="cifras">7.456 → {{ ejemplo() }}</span>
              </p>
              <fieldset class="eleccion en-columna" [disabled]="!!g.archivadoEn">
                <legend>{{ t('retardo') }}</legend>
                @for (r of retardos; track r) {
                  <label>
                    <input
                      type="radio"
                      name="retardo"
                      [value]="r"
                      [checked]="retardo() === r"
                      (change)="retardo.set(r)"
                    />
                    {{ t('retardos.' + r.replace('.', '_')) }}
                  </label>
                }
              </fieldset>
            </section>

            @if (error(); as codigo) {
              <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
            }
            @if (!g.archivadoEn) {
              <button class="boton" type="submit" [appOcupado]="guardando()">
                {{ t('guardar') }}
              </button>
            }
          </form>

          <div class="columna lateral">
            @if (!g.archivadoEn) {
              <section aria-labelledby="archivar-titulo">
                <h2 id="archivar-titulo">{{ t('archivar.titulo') }}</h2>
                <p class="nota">{{ t('archivar.nota') }}</p>
                <button
                  class="boton boton--secundario"
                  type="button"
                  (click)="dialogoArchivar.showModal()"
                >
                  {{ t('archivar.accion') }}
                </button>
              </section>
            }

            <section aria-labelledby="borrar-titulo">
              <h2 id="borrar-titulo">{{ t('borrar.titulo') }}</h2>
              <p class="nota">{{ t('borrar.nota') }}</p>
              <button
                class="boton boton--secundario peligro"
                type="button"
                (click)="dialogoBorrar.showModal()"
              >
                {{ t('borrar.accion') }}
              </button>
            </section>
          </div>
        </div>
      } @else if (error(); as codigo) {
        <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
      } @else {
        <p class="aviso" role="status">{{ t('cargando') }}</p>
      }

      <dialog
        #dialogoArchivar
        class="dialogo"
        closedby="any"
        aria-labelledby="dialogo-archivar-titulo"
      >
        <div class="contenido">
          <h2 id="dialogo-archivar-titulo">
            {{ t('archivar.confirmarTitulo', { grupo: nombreDelGrupo() }) }}
          </h2>
          <p>{{ t('archivar.confirmarTexto') }}</p>
          <div class="acciones">
            <button class="boton boton--secundario" type="button" (click)="dialogoArchivar.close()">
              {{ t('cancelar') }}
            </button>
            <button class="boton" type="button" [appOcupado]="ocupado()" (click)="archivar()">
              {{ t('archivar.accion') }}
            </button>
          </div>
        </div>
      </dialog>

      <dialog #dialogoBorrar class="dialogo" closedby="any" aria-labelledby="dialogo-borrar-titulo">
        <div class="contenido">
          <h2 id="dialogo-borrar-titulo">
            {{ t('borrar.confirmarTitulo', { grupo: nombreDelGrupo() }) }}
          </h2>
          <p>{{ t('borrar.confirmarTexto') }}</p>
          <div class="acciones">
            <button class="boton boton--secundario" type="button" (click)="dialogoBorrar.close()">
              {{ t('cancelar') }}
            </button>
            <button
              class="boton boton--peligro"
              type="button"
              [appOcupado]="ocupado()"
              (click)="borrar()"
            >
              {{ t('borrar.accion') }}
            </button>
          </div>
        </div>
      </dialog>
    </div>
  `,
  styles: `
    /* Tres opciones cortas caben en una fila; las del redondeo, largas, van una bajo otra. */
    .en-tres {
      grid-template-columns: repeat(3, minmax(0, 1fr));
    }

    .en-columna {
      grid-template-columns: minmax(0, 1fr);
    }

    .formulario {
      gap: var(--espacio-5);
    }

    section {
      display: grid;
      justify-items: start;
      gap: var(--espacio-3);
      padding-top: var(--espacio-5);
      border-top: 1px solid var(--color-divisoria);
    }

    section > * {
      justify-self: stretch;
    }

    section > .boton {
      justify-self: start;
    }

    .formulario > .boton {
      justify-self: start;
    }

    h2 {
      font-size: var(--texto-lg);
    }

    .grados {
      grid-template-columns: repeat(auto-fill, minmax(4.5rem, 1fr));
    }

    .grados label {
      position: relative;
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

    .ejemplo {
      color: var(--color-texto-suave);
    }

    .ejemplo .cifras {
      color: var(--color-texto);
      font-weight: var(--peso-medio);
    }

    .nota {
      max-width: var(--ancho-lectura);
      color: var(--color-texto-suave);
    }

    .peligro {
      color: var(--color-peligro-tinta);
    }
  `,
})
export class ConfiguracionGrupo implements OnInit {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly avisos = inject(AvisosService);
  private readonly i18n = inject(TranslocoService);
  private readonly servicio = inject(GruposService);
  private readonly pintarPortada = controlarPortada();
  private readonly enfocar = crearEnfoque();

  readonly id = input.required<string>();

  protected readonly dialogoArchivar =
    viewChild.required<ElementRef<HTMLDialogElement>>('dialogoArchivar');
  protected readonly dialogoBorrar =
    viewChild.required<ElementRef<HTMLDialogElement>>('dialogoBorrar');

  protected readonly redondeos = REDONDEOS;
  protected readonly decimalesOpciones = DECIMALES;
  protected readonly grupo = signal<GrupoDto | null>(null);
  protected readonly nombre = signal('');
  protected readonly grado = signal(1);
  protected readonly materia = signal('');
  protected readonly minima = signal('5');
  protected readonly aprobatoria = signal('6');
  protected readonly decimales = signal(2);
  protected readonly redondeo = signal<Redondeo>('cercano');
  /** RN-102: cuánto vale un retardo en el promedio de asistencia. */
  protected readonly retardos = ['1', '0.5', '0'] as const;
  protected readonly retardo = signal<string>('1');
  protected readonly intento = signal(false);
  protected readonly guardando = signal(false);
  protected readonly ocupado = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly grados = computed(() => GRADOS[(this.grupo()?.nivel ?? 'primaria') as Nivel]);
  protected readonly nombreDelGrupo = computed(() => {
    const g = this.grupo();
    return g ? nombreGrupo(g.grado, g.nombre) : '';
  });
  protected readonly errorCalificacion = computed(() => {
    const minima = leerCalificacion(this.minima());
    const aprobatoria = leerCalificacion(this.aprobatoria());
    if (minima === null || aprobatoria === null) {
      return 'fueraDeEscala';
    }
    return centesimas(aprobatoria) < centesimas(minima) ? 'aprobatoriaMenor' : null;
  });
  protected readonly ejemplo = computed(() =>
    redondearEjemplo(7456, this.decimales(), this.redondeo()),
  );

  async ngOnInit(): Promise<void> {
    const enCache = this.servicio.enCache(this.id());
    if (enCache) {
      this.mostrar(enCache);
    }
    try {
      this.mostrar(await this.servicio.obtener(this.id()));
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected valor(evento: Event): string {
    return (evento.target as HTMLInputElement).value;
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  protected async guardar(): Promise<void> {
    this.intento.set(true);
    this.error.set(null);
    if (!this.nombre().trim() || this.errorCalificacion()) {
      this.enfocar(PRIMER_ERROR);
      return;
    }
    this.guardando.set(true);
    try {
      const grupo = await this.api.invoke(editarGrupo, {
        id: this.id(),
        body: {
          nombre: this.nombre().trim(),
          grado: this.grado(),
          materia: this.materia().trim() || null,
          califMinima: leerCalificacion(this.minima())!,
          califAprobatoria: leerCalificacion(this.aprobatoria())!,
          decimalesFinal: this.decimales(),
          redondeo: this.redondeo(),
          valorRetardo: this.retardo(),
        },
      });
      this.servicio.recordar(grupo);
      this.mostrar(grupo);
      this.intento.set(false);
      this.avisos.mostrar(this.texto('configuracion.guardado'));
    } catch (e) {
      this.error.set(codigoDeError(e));
    } finally {
      this.guardando.set(false);
    }
  }

  protected async archivar(): Promise<void> {
    this.ocupado.set(true);
    try {
      await this.api.invoke(archivarGrupo, { id: this.id() });
      this.servicio.olvidar(this.id());
      this.dialogoArchivar().nativeElement.close();
      this.avisos.mostrar(
        this.i18n.translate('configuracion.archivar.hecho', { grupo: this.nombreDelGrupo() }),
      );
      await this.router.navigateByUrl('/');
    } catch (e) {
      this.dialogoArchivar().nativeElement.close();
      this.error.set(codigoDeError(e));
    } finally {
      this.ocupado.set(false);
    }
  }

  protected async borrar(): Promise<void> {
    this.ocupado.set(true);
    try {
      await this.api.invoke(borrarGrupo, { id: this.id() });
      this.servicio.olvidar(this.id());
      this.dialogoBorrar().nativeElement.close();
      this.avisos.mostrar(
        this.i18n.translate('configuracion.borrar.hecho', { grupo: this.nombreDelGrupo() }),
      );
      await this.router.navigateByUrl('/');
    } catch (e) {
      this.dialogoBorrar().nativeElement.close();
      this.error.set(codigoDeError(e));
    } finally {
      this.ocupado.set(false);
    }
  }

  private mostrar(grupo: GrupoDto): void {
    this.grupo.set(grupo);
    this.pintarPortada(portadaDeGrupo(grupo));
    this.nombre.set(grupo.nombre);
    this.grado.set(Number(grupo.grado));
    this.materia.set(grupo.materia ?? '');
    this.minima.set(String(Number(grupo.califMinima)));
    this.aprobatoria.set(String(Number(grupo.califAprobatoria)));
    this.decimales.set(Number(grupo.decimalesFinal));
    this.redondeo.set(grupo.redondeo as Redondeo);
    this.retardo.set(String(Number(grupo.valorRetardo)));
  }
}
