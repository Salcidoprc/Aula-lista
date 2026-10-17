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
import { NgTemplateOutlet } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { Api } from '../../api/api';
import { darDeBajaAlumno } from '../../api/fn/alumnos/dar-de-baja-alumno';
import { desvincularTutor } from '../../api/fn/alumnos/desvincular-tutor';
import { editarAlumno } from '../../api/fn/alumnos/editar-alumno';
import { editarTutor } from '../../api/fn/alumnos/editar-tutor';
import { obtenerAlumno } from '../../api/fn/alumnos/obtener-alumno';
import { vincularTutor } from '../../api/fn/alumnos/vincular-tutor';
import { AlumnoDto } from '../../api/models/alumno-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { TutorDto } from '../../api/models/tutor-dto';
import { codigoDeError } from '../../core/errores/problema';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { mostrarFecha } from '../../shared/fechas';
import { PRIMER_ERROR, crearEnfoque } from '../../shared/foco';
import { nombreGrupo } from '../../shared/grados';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { controlarPortada } from '../../shared/portada/portada.service';
import { mostrarTelefono, normalizarTelefono } from '../../shared/telefono';
import { GruposService, portadaDeGrupo } from '../grupos/grupos.service';

const MAXIMO_TUTORES = 3;
const TRATOS = ['neutro', 'el', 'ella'] as const;

export function correoValido(correo: string): boolean {
  return !correo.trim() || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(correo.trim());
}

interface TutorEnEdicion {
  nombre: string;
  telefono: string;
  correo: string;
  parentesco: string;
}

const TUTOR_VACIO: TutorEnEdicion = { nombre: '', telefono: '', correo: '', parentesco: '' };

/**
 * SPEC-02, "Ficha del alumno (sección datos)": nombre, trato (RN-020), número de lista (RN-013), tutores
 * con WhatsApp (RN-014 a RN-016) y baja (RN-017).
 */
@Component({
  selector: 'app-ficha-alumno',
  imports: [TranslocoDirective, RouterLink, NgTemplateOutlet, Icono, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pantalla pantalla--ancha" *transloco="let t; prefix: 'alumno'">
      @if (alumno(); as a) {
        <a class="volver" [routerLink]="['/grupos', a.grupoId]">
          <app-icono nombre="volver" />
          {{ grupo() ? t('volverA', { grupo: nombreDelGrupo() }) : t('volver') }}
        </a>
        <hgroup>
          <h1 [class.largo]="a.nombres.length + a.apellidos.length > 40">
            {{ a.nombres }} {{ a.apellidos }}
          </h1>
          <p class="dato-suave cifras">{{ t('numero', { numero: a.numeroLista }) }}</p>
        </hgroup>

        @if (a.bajaEn) {
          <p class="aviso aviso--pendiente">{{ t('baja.estado', { fecha: fecha(a.bajaEn) }) }}</p>
        }

        <div class="columnas columnas--pares">
          <div class="columna">
            <section aria-labelledby="datos-titulo">
              <h2 id="datos-titulo">{{ t('datos.titulo') }}</h2>
              <form
                class="formulario"
                (submit)="$event.preventDefault(); guardarDatos()"
                novalidate
              >
                <div class="campos-en-fila">
                  <div class="campo">
                    <label for="alumno-apellidos">{{ t('datos.apellidos') }}</label>
                    <input
                      id="alumno-apellidos"
                      name="apellidos"
                      maxlength="80"
                      autocomplete="off"
                      autocapitalize="words"
                      [readonly]="soloLectura()"
                      [value]="apellidos()"
                      (input)="apellidos.set(valor($event))"
                    />
                  </div>
                  <div class="campo">
                    <label for="alumno-nombres">{{ t('datos.nombres') }}</label>
                    <input
                      id="alumno-nombres"
                      name="nombres"
                      maxlength="80"
                      autocomplete="off"
                      autocapitalize="words"
                      [readonly]="soloLectura()"
                      [value]="nombres()"
                      (input)="nombres.set(valor($event))"
                      [attr.aria-invalid]="intentoDatos() && !nombres().trim()"
                      [attr.aria-describedby]="
                        intentoDatos() && !nombres().trim() ? 'alumno-nombres-error' : null
                      "
                    />
                    @if (intentoDatos() && !nombres().trim()) {
                      <p id="alumno-nombres-error" class="error">
                        {{ t('datos.nombresRequerido') }}
                      </p>
                    }
                  </div>
                </div>
                <div class="campo numero-lista">
                  <label for="alumno-numero">{{ t('datos.numero') }}</label>
                  <input
                    id="alumno-numero"
                    name="numero"
                    class="cifras"
                    type="number"
                    inputmode="numeric"
                    min="1"
                    max="99"
                    [readonly]="soloLectura()"
                    [value]="numero()"
                    (input)="numero.set(+valor($event))"
                    [attr.aria-invalid]="intentoDatos() && !numeroValido()"
                    [attr.aria-describedby]="
                      intentoDatos() && !numeroValido()
                        ? 'alumno-numero-error alumno-numero-ayuda'
                        : 'alumno-numero-ayuda'
                    "
                  />
                  @if (intentoDatos() && !numeroValido()) {
                    <p id="alumno-numero-error" class="error">{{ t('datos.numeroInvalido') }}</p>
                  }
                  <p id="alumno-numero-ayuda" class="ayuda">{{ t('datos.numeroAyuda') }}</p>
                </div>
                <fieldset class="eleccion" [disabled]="soloLectura()">
                  <legend>{{ t('datos.trato') }}</legend>
                  @for (opcion of tratos; track opcion) {
                    <label>
                      <input
                        type="radio"
                        name="trato"
                        [value]="opcion"
                        [checked]="trato() === opcion"
                        (change)="trato.set(opcion)"
                      />
                      {{ t('tratos.' + opcion) }}
                    </label>
                  }
                  <p class="ayuda ayuda--eleccion">{{ t('datos.tratoAyuda') }}</p>
                </fieldset>
                @if (errorDatos(); as codigo) {
                  <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
                }
                @if (!soloLectura()) {
                  <button
                    class="boton boton--secundario"
                    type="submit"
                    [appOcupado]="guardandoDatos()"
                  >
                    {{ t('datos.guardar') }}
                  </button>
                }
              </form>
            </section>
          </div>

          <div class="columna">
            <section aria-labelledby="tutores-titulo">
              <div>
                <h2 id="tutores-titulo" tabindex="-1">{{ t('tutores.titulo') }}</h2>
                <p class="dato-suave">{{ t('tutores.nota') }}</p>
              </div>
              @if (a.tutores.length) {
                <ul class="filas tutores">
                  @for (tutor of a.tutores; track tutor.id) {
                    <li class="tutor">
                      @if (editandoTutor() === tutor.id) {
                        <form
                          class="formulario"
                          (submit)="$event.preventDefault(); guardarTutor(tutor)"
                          novalidate
                        >
                          <ng-container
                            *ngTemplateOutlet="
                              camposTutor;
                              context: { $implicit: 'editar', conTelefono: false }
                            "
                          />
                          <div class="acciones">
                            <button
                              class="boton boton--secundario"
                              type="submit"
                              [appOcupado]="guardandoTutor()"
                            >
                              {{ t('tutores.guardar') }}
                            </button>
                            <button
                              class="boton boton--secundario"
                              type="button"
                              (click)="cancelarEdicion()"
                            >
                              {{ t('cancelar') }}
                            </button>
                          </div>
                        </form>
                      } @else {
                        <div class="tutor-datos">
                          <span class="tutor-nombre">{{ tutor.nombre }}</span>
                          <span class="dato-suave">
                            @if (tutor.parentesco) {
                              {{ tutor.parentesco }} ·
                            }
                            <span class="cifras telefono">{{ telefono(tutor.telefono) }}</span>
                          </span>
                          @if (tutor.correo) {
                            <span class="dato-suave correo">{{ tutor.correo }}</span>
                          }
                        </div>
                        @if (!soloLectura()) {
                          <div class="acciones">
                            <button
                              class="boton boton--secundario"
                              type="button"
                              [id]="'editar-' + tutor.id"
                              (click)="editar(tutor)"
                            >
                              {{ t('tutores.editar') }}
                              <span class="solo-lector">{{ tutor.nombre }}</span>
                            </button>
                            <button
                              class="boton boton--secundario peligro"
                              type="button"
                              (click)="pedirQuitar(tutor)"
                            >
                              {{ t('tutores.quitar') }}
                              <span class="solo-lector">{{ tutor.nombre }}</span>
                            </button>
                          </div>
                        }
                      }
                    </li>
                  }
                </ul>
              } @else {
                <p class="aviso aviso--pendiente">{{ t('tutores.ninguno') }}</p>
              }

              <!-- Región siempre montada: el lector anuncia el aviso cuando aparece su texto. -->
              <div role="status">
                @if (avisoRegistrado()) {
                  <p class="aviso">{{ t('tutores.yaRegistrado') }}</p>
                }
              </div>

              @if (!soloLectura() && !editandoTutor() && a.tutores.length < maximoTutores) {
                <form
                  class="formulario nuevo-tutor"
                  (submit)="$event.preventDefault(); agregarTutor()"
                  novalidate
                >
                  <h3>{{ t('tutores.agregar') }}</h3>
                  <ng-container
                    *ngTemplateOutlet="
                      camposTutor;
                      context: { $implicit: 'nuevo', conTelefono: true }
                    "
                  />
                  @if (errorTutor(); as codigo) {
                    <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
                  }
                  <button
                    class="boton boton--secundario"
                    type="submit"
                    [appOcupado]="guardandoTutor()"
                  >
                    {{ t('tutores.agregarAccion') }}
                  </button>
                </form>
              }
            </section>

            @if (!a.bajaEn && !soloLectura()) {
              <section aria-labelledby="baja-titulo">
                <h2 id="baja-titulo">{{ t('baja.titulo') }}</h2>
                <p class="nota">{{ t('baja.nota') }}</p>
                <button
                  class="boton boton--secundario peligro"
                  type="button"
                  (click)="dialogoBaja.showModal()"
                >
                  {{ t('baja.accion') }}
                </button>
              </section>
            }
          </div>
        </div>

        <ng-template #camposTutor let-prefijo let-conTelefono="conTelefono">
          <div class="campo">
            <label [for]="prefijo + '-tutor-nombre'">{{ t('tutores.nombre') }}</label>
            <input
              [id]="prefijo + '-tutor-nombre'"
              name="tutor-nombre"
              maxlength="120"
              autocomplete="off"
              autocapitalize="words"
              [value]="tutor().nombre"
              (input)="cambiarTutor({ nombre: valor($event) })"
              [attr.aria-invalid]="intentoTutor() && !tutor().nombre.trim()"
              [attr.aria-describedby]="
                intentoTutor() && !tutor().nombre.trim() ? prefijo + '-tutor-nombre-error' : null
              "
            />
            @if (intentoTutor() && !tutor().nombre.trim()) {
              <p [id]="prefijo + '-tutor-nombre-error'" class="error">
                {{ t('tutores.nombreRequerido') }}
              </p>
            }
          </div>
          @if (conTelefono) {
            <div class="campo">
              <label [for]="prefijo + '-tutor-telefono'">{{ t('tutores.telefono') }}</label>
              <input
                [id]="prefijo + '-tutor-telefono'"
                name="tutor-telefono"
                type="tel"
                inputmode="tel"
                autocomplete="off"
                class="cifras"
                [value]="tutor().telefono"
                (input)="cambiarTutor({ telefono: valor($event) })"
                [attr.aria-invalid]="intentoTutor() && !telefonoValido()"
                [attr.aria-describedby]="
                  intentoTutor() && !telefonoValido()
                    ? prefijo + '-tutor-telefono-error ' + prefijo + '-tutor-telefono-ayuda'
                    : prefijo + '-tutor-telefono-ayuda'
                "
              />
              @if (intentoTutor() && !telefonoValido()) {
                <p [id]="prefijo + '-tutor-telefono-error'" class="error">
                  {{ texto('errores.telefono_invalido') }}
                </p>
              }
              <p [id]="prefijo + '-tutor-telefono-ayuda'" class="ayuda">
                {{ t('tutores.telefonoAyuda') }}
              </p>
            </div>
          }
          <div class="campos-en-fila">
            <div class="campo">
              <label [for]="prefijo + '-tutor-parentesco'">{{ t('tutores.parentesco') }}</label>
              <input
                [id]="prefijo + '-tutor-parentesco'"
                name="tutor-parentesco"
                maxlength="30"
                autocomplete="off"
                list="parentescos"
                [value]="tutor().parentesco"
                (input)="cambiarTutor({ parentesco: valor($event) })"
              />
            </div>
            <div class="campo">
              <label [for]="prefijo + '-tutor-correo'">{{ t('tutores.correo') }}</label>
              <input
                [id]="prefijo + '-tutor-correo'"
                name="tutor-correo"
                type="email"
                inputmode="email"
                maxlength="254"
                autocomplete="off"
                [value]="tutor().correo"
                (input)="cambiarTutor({ correo: valor($event) })"
                [attr.aria-invalid]="intentoTutor() && !correoOk()"
                [attr.aria-describedby]="
                  intentoTutor() && !correoOk() ? prefijo + '-tutor-correo-error' : null
                "
              />
              @if (intentoTutor() && !correoOk()) {
                <p [id]="prefijo + '-tutor-correo-error'" class="error">
                  {{ t('tutores.correoInvalido') }}
                </p>
              }
            </div>
          </div>
        </ng-template>
        <datalist id="parentescos">
          @for (p of parentescos(); track p) {
            <option [value]="p"></option>
          }
        </datalist>
      } @else if (error(); as codigo) {
        <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
      } @else {
        <p class="aviso" role="status">{{ t('cargando') }}</p>
      }

      <dialog #dialogoBaja class="dialogo" closedby="any" aria-labelledby="dialogo-baja-titulo">
        <div class="contenido">
          <h2 id="dialogo-baja-titulo">
            {{ t('baja.confirmarTitulo', { nombre: alumno()?.nombres }) }}
          </h2>
          <p>{{ t('baja.confirmarTexto') }}</p>
          <div class="acciones">
            <button class="boton boton--secundario" type="button" (click)="dialogoBaja.close()">
              {{ t('cancelar') }}
            </button>
            <button
              class="boton boton--peligro"
              type="button"
              [appOcupado]="dandoDeBaja()"
              (click)="darDeBaja()"
            >
              {{ t('baja.accion') }}
            </button>
          </div>
        </div>
      </dialog>

      <dialog #dialogoQuitar class="dialogo" closedby="any" aria-labelledby="dialogo-quitar-titulo">
        <div class="contenido">
          <h2 id="dialogo-quitar-titulo">
            {{ t('tutores.quitarTitulo', { nombre: porQuitar()?.nombre }) }}
          </h2>
          <p>{{ t('tutores.quitarTexto') }}</p>
          <div class="acciones">
            <button class="boton boton--secundario" type="button" (click)="dialogoQuitar.close()">
              {{ t('cancelar') }}
            </button>
            <button
              class="boton boton--peligro"
              type="button"
              [appOcupado]="guardandoTutor()"
              (click)="quitar()"
            >
              {{ t('tutores.quitar') }}
            </button>
          </div>
        </div>
      </dialog>
    </div>
  `,
  styles: `
    /* Un nombre muy largo baja de tamaño para que los datos queden a la vista. */
    h1.largo {
      font-size: var(--texto-xl);
    }

    hgroup {
      display: grid;
      gap: var(--espacio-1);
    }

    h1 {
      overflow-wrap: anywhere;
    }

    section {
      display: grid;
      justify-items: start;
      gap: var(--espacio-3);
      padding-top: var(--espacio-5);
      border-top: 1px solid var(--color-divisoria);
    }

    section > .formulario,
    section > .filas,
    section > .aviso {
      justify-self: stretch;
    }

    .formulario > .boton--secundario {
      justify-self: start;
    }

    h2 {
      font-size: var(--texto-lg);
    }

    h3 {
      font-size: var(--texto-base);
    }

    .numero-lista {
      max-width: 9rem;
    }

    .ayuda--eleccion {
      grid-column: 1 / -1;
      color: var(--color-texto-suave);
      font-size: var(--texto-xs);
    }

    .tutor {
      display: grid;
      gap: var(--espacio-3);
      padding-block: var(--espacio-3);
    }

    .tutor-datos {
      display: grid;
      gap: var(--espacio-1);
    }

    .tutor-nombre {
      font-weight: var(--peso-medio);
      overflow-wrap: anywhere;
    }

    .correo {
      overflow-wrap: anywhere;
    }

    .acciones {
      display: flex;
      flex-wrap: wrap;
      gap: var(--espacio-2);
    }

    .nuevo-tutor {
      padding-top: var(--espacio-3);
    }

    .peligro {
      color: var(--color-peligro-tinta);
    }

    .nota {
      max-width: var(--ancho-lectura);
      color: var(--color-texto-suave);
    }
  `,
})
export class FichaAlumno implements OnInit {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly avisos = inject(AvisosService);
  private readonly i18n = inject(TranslocoService);
  private readonly grupos = inject(GruposService);
  private readonly pintarPortada = controlarPortada();
  private readonly enfocar = crearEnfoque();

  readonly id = input.required<string>();

  protected readonly dialogoBaja = viewChild.required<ElementRef<HTMLDialogElement>>('dialogoBaja');
  protected readonly dialogoQuitar =
    viewChild.required<ElementRef<HTMLDialogElement>>('dialogoQuitar');

  protected readonly tratos = TRATOS;
  protected readonly maximoTutores = MAXIMO_TUTORES;
  protected readonly alumno = signal<AlumnoDto | null>(null);
  protected readonly grupo = signal<GrupoDto | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly nombres = signal('');
  protected readonly apellidos = signal('');
  protected readonly numero = signal(1);
  protected readonly trato = signal<string>('neutro');
  protected readonly intentoDatos = signal(false);
  protected readonly guardandoDatos = signal(false);
  protected readonly errorDatos = signal<string | null>(null);

  protected readonly tutor = signal<TutorEnEdicion>(TUTOR_VACIO);
  protected readonly editandoTutor = signal<string | null>(null);
  protected readonly intentoTutor = signal(false);
  protected readonly guardandoTutor = signal(false);
  protected readonly errorTutor = signal<string | null>(null);
  protected readonly avisoRegistrado = signal(false);
  protected readonly porQuitar = signal<TutorDto | null>(null);
  protected readonly dandoDeBaja = signal(false);

  protected readonly soloLectura = computed(
    () => !!this.alumno()?.bajaEn || !!this.grupo()?.archivadoEn,
  );
  protected readonly numeroValido = computed(
    () => Number.isInteger(this.numero()) && this.numero() >= 1 && this.numero() <= 99,
  );
  protected readonly telefonoValido = computed(
    () => normalizarTelefono(this.tutor().telefono) !== null,
  );
  protected readonly correoOk = computed(() => correoValido(this.tutor().correo));
  protected readonly nombreDelGrupo = computed(() => {
    const g = this.grupo();
    return g ? nombreGrupo(g.grado, g.nombre) : '';
  });
  protected readonly parentescos = computed(
    () => (this.i18n.translateObject('alumno.parentescos') as string[] | undefined) ?? [],
  );

  async ngOnInit(): Promise<void> {
    try {
      const alumno = await this.api.invoke(obtenerAlumno, { id: this.id() });
      const enCache = this.grupos.enCache(alumno.grupoId);
      if (enCache) {
        this.mostrarGrupo(enCache);
      }
      this.mostrar(alumno);
      this.mostrarGrupo(await this.grupos.obtener(alumno.grupoId));
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

  protected fecha(valor: string): string {
    return mostrarFecha(valor);
  }

  protected telefono(e164: string): string {
    return mostrarTelefono(e164);
  }

  protected cambiarTutor(cambios: Partial<TutorEnEdicion>): void {
    this.tutor.update((t) => ({ ...t, ...cambios }));
  }

  protected async guardarDatos(): Promise<void> {
    this.intentoDatos.set(true);
    this.errorDatos.set(null);
    if (!this.nombres().trim() || !this.numeroValido()) {
      this.enfocar(PRIMER_ERROR);
      return;
    }
    this.guardandoDatos.set(true);
    try {
      const alumno = await this.api.invoke(editarAlumno, {
        id: this.id(),
        body: {
          nombres: this.nombres().trim(),
          apellidos: this.apellidos().trim() || null,
          numeroLista: this.numero(),
          trato: this.trato(),
        },
      });
      this.mostrar(alumno);
      this.intentoDatos.set(false);
      this.avisos.mostrar(this.texto('alumno.datos.guardados'));
    } catch (e) {
      this.errorDatos.set(codigoDeError(e));
    } finally {
      this.guardandoDatos.set(false);
    }
  }

  protected editar(tutor: TutorDto): void {
    this.intentoTutor.set(false);
    this.errorTutor.set(null);
    this.tutor.set({
      nombre: tutor.nombre,
      telefono: tutor.telefono,
      correo: tutor.correo ?? '',
      parentesco: tutor.parentesco ?? '',
    });
    this.editandoTutor.set(tutor.id);
    this.enfocar('#editar-tutor-nombre');
  }

  protected async guardarTutor(original: TutorDto): Promise<void> {
    this.intentoTutor.set(true);
    const datos = this.tutor();
    if (!datos.nombre.trim() || !this.correoOk()) {
      this.enfocar(PRIMER_ERROR);
      return;
    }
    this.guardandoTutor.set(true);
    try {
      const actualizado = await this.api.invoke(editarTutor, {
        id: this.id(),
        tutorId: original.id,
        body: {
          nombre: datos.nombre.trim(),
          correo: datos.correo.trim() || null,
          parentesco: datos.parentesco.trim() || null,
        },
      });
      this.reemplazarTutores((lista) => lista.map((t) => (t.id === original.id ? actualizado : t)));
      this.editandoTutor.set(null);
      this.reiniciarTutor();
      this.enfocar(`#editar-${original.id}`);
    } catch (e) {
      this.avisos.mostrar(this.texto('errores.' + codigoDeError(e)), 'error');
    } finally {
      this.guardandoTutor.set(false);
    }
  }

  /** RN-015: si el teléfono ya es de un tutor, se vincula y se avisa sin mostrar su otro nombre. */
  protected async agregarTutor(): Promise<void> {
    this.intentoTutor.set(true);
    this.errorTutor.set(null);
    this.avisoRegistrado.set(false);
    const datos = this.tutor();
    if (!datos.nombre.trim() || !this.telefonoValido() || !this.correoOk()) {
      this.enfocar(PRIMER_ERROR);
      return;
    }
    this.guardandoTutor.set(true);
    try {
      const vinculado = await this.api.invoke(vincularTutor, {
        id: this.id(),
        body: {
          nombre: datos.nombre.trim(),
          telefono: normalizarTelefono(datos.telefono)!,
          correo: datos.correo.trim() || null,
          parentesco: datos.parentesco.trim() || null,
        },
      });
      this.reemplazarTutores((lista) => [...lista, vinculado.tutor]);
      this.avisoRegistrado.set(vinculado.yaRegistrado);
      this.reiniciarTutor();
      this.avisos.mostrar(
        this.i18n.translate('alumno.tutores.agregado', { nombre: vinculado.tutor.nombre }),
      );
    } catch (e) {
      this.errorTutor.set(codigoDeError(e));
    } finally {
      this.guardandoTutor.set(false);
    }
  }

  protected pedirQuitar(tutor: TutorDto): void {
    this.porQuitar.set(tutor);
    this.dialogoQuitar().nativeElement.showModal();
  }

  protected async quitar(): Promise<void> {
    const tutor = this.porQuitar();
    if (!tutor) {
      return;
    }
    this.guardandoTutor.set(true);
    try {
      await this.api.invoke(desvincularTutor, { id: this.id(), tutorId: tutor.id });
      this.reemplazarTutores((lista) => lista.filter((t) => t.id !== tutor.id));
      this.dialogoQuitar().nativeElement.close();
      // El botón que abrió el diálogo ya no existe: el foco va al título de la sección.
      this.enfocar('#tutores-titulo');
    } catch (e) {
      // Fuera del diálogo modal el aviso queda inerte: se cierra antes de mostrarlo.
      this.dialogoQuitar().nativeElement.close();
      this.avisos.mostrar(this.texto('errores.' + codigoDeError(e)), 'error');
    } finally {
      this.guardandoTutor.set(false);
    }
  }

  protected async darDeBaja(): Promise<void> {
    this.dandoDeBaja.set(true);
    try {
      await this.api.invoke(darDeBajaAlumno, { id: this.id() });
      const grupoId = this.alumno()!.grupoId;
      this.dialogoBaja().nativeElement.close();
      this.avisos.mostrar(
        this.i18n.translate('alumno.baja.hecha', { nombre: this.alumno()!.nombres }),
      );
      await this.router.navigate(['/grupos', grupoId]);
    } catch (e) {
      this.dialogoBaja().nativeElement.close();
      this.avisos.mostrar(this.texto('errores.' + codigoDeError(e)), 'error');
    } finally {
      this.dandoDeBaja.set(false);
    }
  }

  protected cancelarEdicion(): void {
    const id = this.editandoTutor();
    this.editandoTutor.set(null);
    this.reiniciarTutor();
    this.enfocar(`#editar-${id}`);
  }

  private reemplazarTutores(cambio: (lista: TutorDto[]) => TutorDto[]): void {
    this.alumno.update((a) => (a ? { ...a, tutores: cambio([...a.tutores]) } : a));
  }

  private reiniciarTutor(): void {
    this.tutor.set(TUTOR_VACIO);
    this.intentoTutor.set(false);
  }

  private mostrar(alumno: AlumnoDto): void {
    this.alumno.set(alumno);
    this.nombres.set(alumno.nombres);
    this.apellidos.set(alumno.apellidos);
    this.numero.set(Number(alumno.numeroLista));
    this.trato.set(alumno.trato);
  }

  private mostrarGrupo(grupo: GrupoDto): void {
    this.grupo.set(grupo);
    this.pintarPortada(portadaDeGrupo(grupo));
  }
}
