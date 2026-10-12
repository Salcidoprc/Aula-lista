import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  computed,
  inject,
  input,
  linkedSignal,
  signal,
  viewChild,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { forma } from '../../shared/plural';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { Api } from '../../api/api';
import { crearImportacion } from '../../api/fn/grupos/crear-importacion';
import { confirmarImportacion } from '../../api/fn/importaciones/confirmar-importacion';
import { IFormFile } from '../../api/models/i-form-file';
import { VistaPreviaImportacion } from '../../api/models/vista-previa-importacion';
import { codigoDeError } from '../../core/errores/problema';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { descargarArchivo } from '../../shared/descarga';
import { crearEnfoque } from '../../shared/foco';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { controlarPortada } from '../../shared/portada/portada.service';
import { mostrarTelefono, normalizarTelefono } from '../../shared/telefono';
import { GruposService, portadaDeGrupo } from '../grupos/grupos.service';
import {
  CAMPOS,
  EstadoFila,
  FilaVista,
  Orden,
  TutorFila,
  aConfirmar,
  estados,
  filasDesdeTabla,
  repetidos,
  usaNombreCompleto,
  validarFila,
} from './importacion';

type Origen = 'excel' | 'texto';
const TAMANO_MAXIMO = 2 * 1024 * 1024;

/**
 * SPEC-02, "Importar": pestañas Excel o CSV y Pegar lista (la foto llega en H6, RF-015), con una vista previa
 * común que el docente corrige antes de confirmar (RF-013, RF-014). Los nombres viven en la web hasta
 * confirmar; la API no guarda el archivo (ADR-0014).
 */
@Component({
  selector: 'app-importar',
  imports: [TranslocoDirective, RouterLink, Icono, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pantalla pantalla--ancha" *transloco="let t; prefix: 'importar'">
      <a class="volver" [routerLink]="['/grupos', id()]" (click)="salir($event)">
        <app-icono nombre="volver" />
        {{ t('volver') }}
      </a>
      <h1>{{ t('titulo') }}</h1>

      @if (!vista()) {
        <div class="pestanas" role="tablist" [attr.aria-label]="t('formas')">
          <button
            id="pestana-excel"
            role="tab"
            type="button"
            [attr.aria-controls]="pestana() === 'excel' ? 'panel-excel' : null"
            [attr.aria-selected]="pestana() === 'excel'"
            [attr.tabindex]="pestana() === 'excel' ? 0 : -1"
            (click)="pestana.set('excel')"
            (keydown)="moverPestana($event)"
          >
            {{ t('excel') }}
          </button>
          <button
            id="pestana-texto"
            role="tab"
            type="button"
            [attr.aria-controls]="pestana() === 'texto' ? 'panel-texto' : null"
            [attr.aria-selected]="pestana() === 'texto'"
            [attr.tabindex]="pestana() === 'texto' ? 0 : -1"
            (click)="pestana.set('texto')"
            (keydown)="moverPestana($event)"
          >
            {{ t('pegar') }}
          </button>
        </div>

        @if (pestana() === 'excel') {
          <div id="panel-excel" class="panel" role="tabpanel" aria-labelledby="pestana-excel">
            <p class="nota">{{ t('excelNota') }}</p>
            <label class="boton boton--secundario archivo" [class.ocupado]="leyendo()">
              <app-icono nombre="archivo" />
              {{ leyendo() ? t('leyendo') : t('elegir') }}
              <input
                type="file"
                accept=".xlsx,.csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/csv"
                [disabled]="leyendo()"
                (change)="elegirArchivo($event)"
              />
            </label>
            <button
              class="enlace enlace--solo plantilla"
              type="button"
              [appOcupado]="descargando()"
              (click)="descargarPlantilla()"
            >
              <app-icono nombre="descargar" />
              {{ t('plantilla') }}
            </button>
          </div>
        } @else {
          <form
            id="panel-texto"
            class="panel formulario"
            role="tabpanel"
            aria-labelledby="pestana-texto"
            (submit)="$event.preventDefault(); leerTexto()"
            novalidate
          >
            <div class="campo">
              <label for="importar-texto">{{ t('lista') }}</label>
              <textarea
                id="importar-texto"
                name="texto"
                rows="10"
                maxlength="20000"
                autocapitalize="words"
                [placeholder]="t('listaEjemplo')"
                [value]="texto()"
                (input)="texto.set(valor($event))"
                aria-describedby="importar-texto-ayuda"
              ></textarea>
              <p id="importar-texto-ayuda" class="ayuda">{{ t('listaAyuda') }}</p>
            </div>
            <button
              class="boton"
              type="submit"
              [appOcupado]="leyendo()"
              [disabled]="!texto().trim()"
            >
              {{ t('verVista') }}
            </button>
          </form>
        }

        @if (error(); as codigo) {
          <p class="aviso aviso--error" role="alert">{{ traducir('errores.' + codigo) }}</p>
        }
      } @else {
        <section class="vista" aria-labelledby="vista-titulo">
          <div>
            <h2 id="vista-titulo" tabindex="-1">{{ t('vista.titulo') }}</h2>
            <p class="dato-suave">
              {{ t('vista.nota', { disponibles: disponibles() }) }}
            </p>
            <button class="enlace enlace--solo" type="button" (click)="pedirEmpezarDeNuevo()">
              {{ t('vista.otra') }}
            </button>
          </div>

          @if (vista()!.origen !== 'texto') {
            <details class="columnas" [open]="!tieneNombre()">
              <summary>
                {{ t('vista.columnas', { cantidad: vista()!.encabezados.length }) }}
              </summary>
              <ul class="filas">
                @for (encabezado of vista()!.encabezados; track $index; let j = $index) {
                  <li class="columna">
                    <label [for]="'columna-' + j">{{
                      encabezado || t('vista.sinEncabezado', { n: j + 1 })
                    }}</label>
                    <div class="selector">
                      <select [id]="'columna-' + j" (change)="cambiarMapeo(j, valor($event))">
                        @for (campo of campos; track campo) {
                          <option [value]="campo" [selected]="mapeo()[j] === campo">
                            {{ etiquetaCampo(campo) }}
                          </option>
                        }
                      </select>
                    </div>
                  </li>
                }
              </ul>
            </details>
            @if (!tieneNombre()) {
              <p class="aviso aviso--pendiente" role="status">{{ t('vista.faltaNombre') }}</p>
            }
          }

          @if (pideOrden()) {
            <fieldset class="eleccion">
              <legend>{{ t('vista.orden') }}</legend>
              <label>
                <input
                  type="radio"
                  name="orden"
                  value="apellidos"
                  [checked]="orden() === 'apellidos'"
                  (change)="cambiarOrden('apellidos')"
                />
                {{ t('vista.apellidosPrimero') }}
              </label>
              <label>
                <input
                  type="radio"
                  name="orden"
                  value="nombres"
                  [checked]="orden() === 'nombres'"
                  (change)="cambiarOrden('nombres')"
                />
                {{ t('vista.nombresPrimero') }}
              </label>
            </fieldset>
          }

          <p class="resumen cifras" aria-live="polite">
            <span>{{
              t('vista.listas.' + forma(cuenta().lista), { cantidad: cuenta().lista })
            }}</span>
            @if (cuenta().error) {
              <span class="con-error">{{ t('vista.conError', { cantidad: cuenta().error }) }}</span>
            }
            @if (cuenta().no_cabe) {
              <span class="no-cabe">{{
                t('vista.noCaben.' + forma(cuenta().no_cabe), { cantidad: cuenta().no_cabe })
              }}</span>
            }
          </p>

          <ol class="filas renglones">
            @for (f of filas(); track f.renglon; let i = $index) {
              <li class="renglon" [attr.data-estado]="estado()[i]">
                <div class="renglon-cabeza">
                  <span class="numero cifras" aria-hidden="true">{{ f.renglon }}</span>
                  <div class="renglon-datos">
                    <span class="nombre">
                      @if (f.apellidos) {
                        <span class="apellidos">{{ f.apellidos }}</span>
                      }
                      {{ f.nombres || t('vista.sinNombre') }}
                    </span>
                    @for (tutor of f.tutores; track $index) {
                      <span class="dato-suave tutor">
                        {{ tutor.nombre || t('vista.tutorSinNombre') }}
                        @if (tutor.telefono) {
                          · <span class="cifras telefono">{{ telefono(tutor.telefono) }}</span>
                        }
                      </span>
                    }
                    @switch (estado()[i]) {
                      @case ('error') {
                        <span class="motivo motivo--error" [id]="'motivo-' + f.renglon">{{
                          t('problemas.' + problema(f))
                        }}</span>
                      }
                      @case ('no_cabe') {
                        <span class="motivo">{{ t('vista.noCabe') }}</span>
                      }
                      @case ('excluida') {
                        <span class="motivo">{{ t('vista.excluida') }}</span>
                      }
                      @default {
                        @if (repetidas().has(f.renglon)) {
                          <span class="motivo">{{ t('vista.repetido') }}</span>
                        }
                      }
                    }
                  </div>
                  <div class="renglon-acciones">
                    @if (!f.excluida) {
                      <!-- Solo los renglones con problema llevan botón; los demás, un enlace discreto. -->
                      <button
                        [class]="
                          estado()[i] === 'error'
                            ? 'boton boton--secundario'
                            : 'enlace enlace--solo corregir-enlace'
                        "
                        type="button"
                        [attr.aria-expanded]="abierta() === f.renglon"
                        [attr.aria-controls]="'corregir-' + f.renglon"
                        (click)="alternar(f.renglon)"
                      >
                        {{ t('vista.corregir') }}
                        <span class="solo-lector">{{ t('vista.renglon', { n: f.renglon }) }}</span>
                      </button>
                      <button class="quitar" type="button" (click)="excluir(i)">
                        <app-icono nombre="cerrar" />
                        <span class="solo-lector"
                          >{{ t('vista.quitar') }} {{ t('vista.renglon', { n: f.renglon }) }}</span
                        >
                      </button>
                    } @else {
                      <button class="enlace enlace--solo" type="button" (click)="excluir(i)">
                        {{ t('vista.incluir') }}
                        <span class="solo-lector">{{ t('vista.renglon', { n: f.renglon }) }}</span>
                      </button>
                    }
                  </div>
                </div>
                @if (abierta() === f.renglon && !f.excluida) {
                  <div class="corregir" [id]="'corregir-' + f.renglon">
                    <div class="campos-en-fila">
                      <div class="campo">
                        <label [for]="'r' + f.renglon + '-apellidos'">{{
                          t('vista.apellidos')
                        }}</label>
                        <input
                          [id]="'r' + f.renglon + '-apellidos'"
                          maxlength="80"
                          autocomplete="off"
                          [value]="f.apellidos"
                          (input)="editar(i, { apellidos: valor($event) })"
                        />
                      </div>
                      <div class="campo">
                        <label [for]="'r' + f.renglon + '-nombres'">{{ t('vista.nombres') }}</label>
                        <input
                          [id]="'r' + f.renglon + '-nombres'"
                          maxlength="80"
                          autocomplete="off"
                          [value]="f.nombres"
                          (input)="editar(i, { nombres: valor($event) })"
                          [attr.aria-invalid]="!f.nombres.trim()"
                          [attr.aria-describedby]="!f.nombres.trim() ? 'motivo-' + f.renglon : null"
                        />
                      </div>
                    </div>
                    @for (tutor of tutoresEditables(f); track $index; let k = $index) {
                      <div class="campos-en-fila">
                        <div class="campo">
                          <label [for]="'r' + f.renglon + '-tutor-' + k">{{
                            t('vista.tutor', { n: k + 1 })
                          }}</label>
                          <input
                            [id]="'r' + f.renglon + '-tutor-' + k"
                            maxlength="120"
                            autocomplete="off"
                            [value]="tutor.nombre"
                            (input)="editarTutor(i, k, { nombre: valor($event) })"
                          />
                        </div>
                        <div class="campo">
                          <label [for]="'r' + f.renglon + '-telefono-' + k">{{
                            t('vista.telefono', { n: k + 1 })
                          }}</label>
                          <input
                            [id]="'r' + f.renglon + '-telefono-' + k"
                            type="tel"
                            inputmode="tel"
                            autocomplete="off"
                            class="cifras"
                            [value]="tutor.telefono"
                            (input)="editarTutor(i, k, { telefono: valor($event) })"
                            [attr.aria-invalid]="
                              !!tutor.telefono.trim() && !telefonoValido(tutor.telefono)
                            "
                            [attr.aria-describedby]="
                              tutor.telefono.trim() && !telefonoValido(tutor.telefono)
                                ? 'motivo-' + f.renglon
                                : null
                            "
                          />
                        </div>
                      </div>
                    }
                  </div>
                }
              </li>
            }
          </ol>

          @if (errorConfirmar(); as codigo) {
            <p class="aviso aviso--error" role="alert">{{ traducir('errores.' + codigo) }}</p>
          }

          <div class="pie-accion">
            <button
              class="boton"
              type="button"
              [appOcupado]="confirmando()"
              [disabled]="cuenta().lista === 0"
              (click)="confirmar()"
            >
              {{ t('vista.importar.' + forma(cuenta().lista), { cantidad: cuenta().lista }) }}
            </button>
          </div>
        </section>
      }

      <dialog
        #dialogoDescartar
        class="dialogo"
        closedby="any"
        aria-labelledby="dialogo-descartar-titulo"
      >
        <div class="contenido">
          <h2 id="dialogo-descartar-titulo">{{ t('descartar.titulo') }}</h2>
          <p>{{ t('descartar.texto') }}</p>
          <div class="acciones">
            <button
              class="boton boton--secundario"
              type="button"
              (click)="dialogoDescartar.close()"
            >
              {{ t('descartar.seguir') }}
            </button>
            <button
              class="boton boton--peligro"
              type="button"
              (click)="dialogoDescartar.close(); descartar()"
            >
              {{ t('descartar.accion') }}
            </button>
          </div>
        </div>
      </dialog>
    </div>
  `,
  styles: `
    h2 {
      font-size: var(--texto-lg);
    }

    .panel {
      display: grid;
      justify-items: start;
      gap: var(--espacio-4);
    }

    .panel.formulario {
      justify-items: stretch;
    }

    .panel.formulario > .boton {
      justify-self: start;
    }

    .nota {
      max-width: var(--ancho-lectura);
      color: var(--color-texto-suave);
    }

    .archivo {
      position: relative;
      cursor: pointer;
    }

    .archivo input {
      position: absolute;
      inset: 0;
      opacity: 0;
      cursor: pointer;
    }

    .archivo:has(input:focus-visible) {
      box-shadow: var(--foco);
    }

    .archivo.ocupado {
      cursor: progress;
      opacity: 0.7;
    }

    .plantilla {
      gap: var(--espacio-2);
    }

    .plantilla app-icono {
      width: 1rem;
      height: 1rem;
    }

    .vista {
      display: grid;
      gap: var(--espacio-4);
    }

    .vista h2:focus {
      outline: none;
    }

    .columnas summary {
      display: flex;
      align-items: center;
      min-height: var(--tactil-minimo);
      font-weight: var(--peso-medio);
      cursor: pointer;
    }

    .columna {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(0, 1.2fr);
      align-items: center;
      gap: var(--espacio-3);
      padding-block: var(--espacio-2);
    }

    .columna label {
      overflow-wrap: anywhere;
      font-weight: var(--peso-medio);
    }

    .columna select {
      width: 100%;
      min-height: var(--tactil-minimo);
      padding: 0 var(--espacio-6) 0 var(--espacio-3);
      border: 0;
      border-radius: var(--radio-md);
      background: var(--color-superficie-hundida);
      appearance: none;
    }

    .resumen {
      display: flex;
      flex-wrap: wrap;
      gap: var(--espacio-1) var(--espacio-4);
      font-weight: var(--peso-medio);
    }

    .con-error {
      color: var(--color-peligro-tinta);
    }

    .no-cabe {
      color: var(--color-aviso-tinta);
    }

    .renglon {
      display: grid;
      gap: var(--espacio-3);
      padding-block: var(--espacio-3);
    }

    .renglon-cabeza {
      display: grid;
      grid-template-columns: 2rem 1fr;
      gap: var(--espacio-3);
    }

    .renglon-acciones {
      grid-column: 2;
      display: flex;
      flex-wrap: wrap;
      gap: var(--espacio-2);
    }

    @media (min-width: 40rem) {
      .renglon-cabeza {
        grid-template-columns: 2rem 1fr auto;
        align-items: start;
      }

      .renglon-acciones {
        grid-column: 3;
      }
    }

    .renglon-acciones {
      align-items: center;
    }

    .renglon-acciones .boton {
      min-height: var(--tactil-minimo);
      padding-inline: var(--espacio-3);
      font-size: var(--texto-sm);
    }

    .corregir-enlace {
      padding-inline: var(--espacio-1);
      font-size: var(--texto-sm);
    }

    .quitar {
      display: inline-grid;
      place-items: center;
      width: var(--tactil-minimo);
      height: var(--tactil-minimo);
      border: 0;
      border-radius: var(--radio-md);
      background: none;
      color: var(--color-texto-suave);
      cursor: pointer;
    }

    .quitar:active {
      background: var(--color-superficie-hundida);
    }

    .numero {
      padding-top: 0.125rem;
      color: var(--color-texto-suave);
      text-align: end;
    }

    .renglon-datos {
      display: grid;
      gap: var(--espacio-1);
      min-width: 0;
    }

    .nombre {
      overflow-wrap: anywhere;
    }

    .apellidos {
      font-weight: var(--peso-medio);
    }

    .tutor {
      overflow-wrap: anywhere;
    }

    .motivo {
      justify-self: start;
      padding: 0 var(--espacio-2);
      border-radius: var(--radio-sm);
      background: var(--color-aviso-tinte);
      color: var(--color-aviso-tinta);
      font-size: var(--texto-xs);
      font-weight: var(--peso-medio);
    }

    .motivo--error {
      background: var(--color-peligro-tinte);
      color: var(--color-peligro-tinta);
    }

    [data-estado='excluida'] .renglon-datos,
    [data-estado='no_cabe'] .renglon-datos {
      color: var(--color-texto-suave);
    }

    [data-estado='excluida'] .nombre {
      text-decoration: line-through;
    }

    .corregir {
      display: grid;
      gap: var(--espacio-3);
      margin-inline-start: calc(2rem + var(--espacio-3));
      padding: var(--espacio-3);
      border-radius: var(--radio-lg);
      background: var(--color-superficie-hundida);
    }

    .corregir input {
      background: var(--color-superficie);
    }
  `,
})
export class Importar implements OnInit {
  protected readonly forma = forma;
  private readonly api = inject(Api);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly avisos = inject(AvisosService);
  private readonly i18n = inject(TranslocoService);
  private readonly grupos = inject(GruposService);
  private readonly pintarPortada = controlarPortada();
  private readonly enfocar = crearEnfoque();

  readonly id = input.required<string>();
  /** `?origen=excel` o `?origen=texto`: con qué pestaña abre. */
  readonly origen = input<string>();

  protected readonly campos = CAMPOS;
  protected readonly pestana = linkedSignal<Origen>(() =>
    this.origen() === 'texto' ? 'texto' : 'excel',
  );
  protected readonly texto = signal('');
  protected readonly leyendo = signal(false);
  protected readonly descargando = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly vista = signal<VistaPreviaImportacion | null>(null);
  protected readonly mapeo = signal<readonly string[]>([]);
  protected readonly orden = signal<Orden>('apellidos');
  protected readonly filas = signal<readonly FilaVista[]>([]);
  protected readonly abierta = signal<number | null>(null);
  /** Hubo correcciones o renglones quitados: salir o empezar de nuevo los pierde, así que se pregunta. */
  private readonly editado = signal(false);
  private readonly dialogoDescartar =
    viewChild.required<ElementRef<HTMLDialogElement>>('dialogoDescartar');
  private destinoAlDescartar: 'grupo' | 'otra' = 'otra';
  protected readonly confirmando = signal(false);
  protected readonly errorConfirmar = signal<string | null>(null);

  protected readonly disponibles = computed(() =>
    Math.max(0, Number(this.vista()?.disponibles ?? 0)),
  );
  protected readonly estado = computed<EstadoFila[]>(() =>
    estados(this.filas(), this.disponibles()),
  );
  protected readonly repetidas = computed(() => repetidos(this.filas()));
  protected readonly cuenta = computed(() => {
    const cuenta: Record<EstadoFila, number> = { lista: 0, error: 0, no_cabe: 0, excluida: 0 };
    for (const e of this.estado()) {
      cuenta[e]++;
    }
    return cuenta;
  });
  protected readonly pideOrden = computed(
    () => this.vista()?.origen === 'texto' || usaNombreCompleto(this.mapeo()),
  );
  protected readonly tieneNombre = computed(() =>
    this.mapeo().some((c) => c === 'nombres' || c === 'nombre_completo'),
  );

  async ngOnInit(): Promise<void> {
    const enCache = this.grupos.enCache(this.id());
    if (enCache) {
      this.pintarPortada(portadaDeGrupo(enCache));
    }
    try {
      this.pintarPortada(portadaDeGrupo(await this.grupos.obtener(this.id())));
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected valor(evento: Event): string {
    return (evento.target as HTMLInputElement).value;
  }

  protected traducir(clave: string): string {
    return this.i18n.translate(clave);
  }

  protected telefono(texto: string): string {
    const e164 = normalizarTelefono(texto);
    return e164 ? mostrarTelefono(e164) : texto;
  }

  protected telefonoValido(texto: string): boolean {
    return normalizarTelefono(texto) !== null;
  }

  protected problema(fila: FilaVista): string {
    return validarFila(fila) ?? '';
  }

  protected etiquetaCampo(campo: string): string {
    const tutor = /^tutor_(\w+)_(\d)$/.exec(campo);
    return tutor
      ? this.i18n.translate(`importar.camposTutor.${tutor[1]}`, { n: tutor[2] })
      : this.i18n.translate(`importar.campos.${campo}`);
  }

  /** Flechas izquierda y derecha entre pestañas (patrón de pestañas de WAI-ARIA). */
  protected moverPestana(evento: KeyboardEvent): void {
    const otra: Origen = this.pestana() === 'excel' ? 'texto' : 'excel';
    const destinos: Record<string, Origen> = {
      ArrowLeft: otra,
      ArrowRight: otra,
      Home: 'excel',
      End: 'texto',
    };
    const siguiente = destinos[evento.key];
    if (!siguiente) {
      return;
    }
    evento.preventDefault();
    this.pestana.set(siguiente);
    this.enfocar(`#pestana-${siguiente}`);
  }

  protected async elegirArchivo(evento: Event): Promise<void> {
    const entrada = evento.target as HTMLInputElement;
    const archivo = entrada.files?.[0];
    entrada.value = '';
    if (!archivo) {
      return;
    }
    if (!/\.(xlsx|csv)$/i.test(archivo.name) || archivo.size > TAMANO_MAXIMO) {
      this.error.set('archivo_invalido');
      return;
    }
    await this.leer({ archivo: archivo as unknown as IFormFile });
  }

  protected async leerTexto(): Promise<void> {
    if (this.texto().trim()) {
      await this.leer({ texto: this.texto() });
    }
  }

  protected async descargarPlantilla(): Promise<void> {
    this.descargando.set(true);
    try {
      await descargarArchivo(
        this.http,
        '/api/v1/importaciones/plantilla',
        'plantilla-alumnos.xlsx',
      );
    } catch (e) {
      this.error.set(codigoDeError(e));
    } finally {
      this.descargando.set(false);
    }
  }

  protected cambiarMapeo(columna: number, campo: string): void {
    this.mapeo.update((m) => m.map((c, j) => (j === columna ? campo : c)));
    this.armarFilas();
  }

  protected cambiarOrden(orden: Orden): void {
    this.orden.set(orden);
    this.armarFilas();
  }

  protected alternar(renglon: number): void {
    this.abierta.update((r) => (r === renglon ? null : renglon));
    if (this.abierta() === renglon) {
      this.enfocar(`#r${renglon}-apellidos`);
    }
  }

  protected excluir(indice: number): void {
    this.editado.set(true);
    this.filas.update((lista) =>
      lista.map((f, i) => (i === indice ? { ...f, excluida: !f.excluida } : f)),
    );
  }

  protected editar(indice: number, cambios: Partial<FilaVista>): void {
    this.editado.set(true);
    this.filas.update((lista) => lista.map((f, i) => (i === indice ? { ...f, ...cambios } : f)));
  }

  /** Corrige el primer tutor (o lo agrega si no había); los demás se editan después en la ficha. */
  protected tutoresEditables(fila: FilaVista): readonly TutorFila[] {
    return fila.tutores.length
      ? fila.tutores
      : [{ nombre: '', telefono: '', correo: '', parentesco: '' }];
  }

  protected editarTutor(indice: number, tutor: number, cambios: Partial<TutorFila>): void {
    this.editado.set(true);
    this.filas.update((lista) =>
      lista.map((f, i) => {
        if (i !== indice) {
          return f;
        }
        const tutores = [...this.tutoresEditables(f)];
        tutores[tutor] = { ...tutores[tutor], ...cambios };
        return { ...f, tutores };
      }),
    );
  }

  protected pedirEmpezarDeNuevo(): void {
    if (this.editado()) {
      this.destinoAlDescartar = 'otra';
      this.dialogoDescartar().nativeElement.showModal();
    } else {
      this.empezarDeNuevo();
    }
  }

  protected salir(evento: MouseEvent): void {
    if (this.vista() && this.editado()) {
      evento.preventDefault();
      this.destinoAlDescartar = 'grupo';
      this.dialogoDescartar().nativeElement.showModal();
    }
  }

  protected async descartar(): Promise<void> {
    if (this.destinoAlDescartar === 'grupo') {
      this.editado.set(false);
      await this.router.navigate(['/grupos', this.id()]);
    } else {
      this.empezarDeNuevo();
    }
  }

  protected empezarDeNuevo(): void {
    this.editado.set(false);
    this.vista.set(null);
    this.filas.set([]);
    this.abierta.set(null);
    this.errorConfirmar.set(null);
    this.enfocar(`#pestana-${this.pestana()}`);
  }

  protected async confirmar(): Promise<void> {
    const vista = this.vista();
    if (!vista) {
      return;
    }
    this.confirmando.set(true);
    this.errorConfirmar.set(null);
    try {
      const resultado = await this.api.invoke(confirmarImportacion, {
        id: vista.id,
        body: { filas: aConfirmar(this.filas(), this.disponibles()) },
      });
      const guardados = Number(resultado.guardados);
      const omitidos =
        Number(resultado.omitidosPorError) +
        Number(resultado.omitidosPorLimite) +
        this.cuenta().error +
        this.cuenta().no_cabe;
      this.avisos.mostrar(
        omitidos
          ? this.i18n.translate('importar.hechoConOmitidos.' + forma(guardados), {
              guardados,
              omitidos,
            })
          : this.i18n.translate('importar.hecho.' + forma(guardados), { guardados }),
      );
      await this.router.navigate(['/grupos', this.id()]);
    } catch (e) {
      this.errorConfirmar.set(codigoDeError(e));
    } finally {
      this.confirmando.set(false);
    }
  }

  private async leer(cuerpo: { archivo?: IFormFile; texto?: string }): Promise<void> {
    this.leyendo.set(true);
    this.error.set(null);
    try {
      const vista = await this.api.invoke(crearImportacion, { id: this.id(), body: cuerpo });
      this.vista.set(vista);
      this.mapeo.set(vista.origen === 'texto' ? ['nombre_completo'] : vista.mapeo);
      this.armarFilas();
      this.enfocar('#vista-titulo');
    } catch (e) {
      this.error.set(codigoDeError(e));
    } finally {
      this.leyendo.set(false);
    }
  }

  private armarFilas(): void {
    const vista = this.vista();
    if (vista) {
      this.filas.set(filasDesdeTabla(vista.filas, this.mapeo(), this.orden()));
      this.abierta.set(null);
      this.editado.set(false);
    }
  }
}
