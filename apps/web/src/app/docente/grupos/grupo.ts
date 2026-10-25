import { NgTemplateOutlet } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { forma } from '../../shared/plural';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { Api } from '../../api/api';
import { agregarAlumno } from '../../api/fn/grupos/agregar-alumno';
import { listarAlumnos } from '../../api/fn/grupos/listar-alumnos';
import { AlumnoDto } from '../../api/models/alumno-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { codigoDeError } from '../../core/errores/problema';
import { PerfilService } from '../../core/sesion/perfil.service';
import { descargarArchivo } from '../../shared/descarga';
import { crearEnfoque } from '../../shared/foco';
import { Hoja } from '../../shared/hoja';
import { claveGrado } from '../../shared/grados';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { controlarPortada } from '../../shared/portada/portada.service';
import { ReenviarVerificacion } from '../cuenta/reenviar-verificacion';
import { GruposService, portadaDeGrupo } from './grupos.service';
import { PestanasGrupo } from './pestanas-grupo';

const MAXIMO_ALUMNOS = 50;

/**
 * SPEC-02, "Alumnos del grupo" (segunda vista del grupo): lista con número, nombre, tutores y estado, y las formas de agregar
 * (importar de Excel, pegar lista o uno por uno). La portada toma el color y el número del grado.
 */
@Component({
  selector: 'app-grupo',
  imports: [
    TranslocoDirective,
    RouterLink,
    NgTemplateOutlet,
    Hoja,
    Icono,
    Ocupado,
    ReenviarVerificacion,
    PestanasGrupo,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pantalla pantalla--ancha" *transloco="let t; prefix: 'grupo'">
      <a class="volver" routerLink="/">
        <app-icono nombre="volver" />
        {{ t('misGrupos') }}
      </a>

      <app-pestanas-grupo [id]="id()" [grado]="clave()" />

      <div class="encabezado">
        <hgroup>
          <h1 tabindex="-1">{{ t('titulo') }}</h1>
          @if (grupo()) {
            <p class="dato-suave cifras">
              {{ t('conteo', { activos: activos().length, maximo: maximo }) }}
            </p>
          }
        </hgroup>
        @if (activos().length > 0) {
          <a class="boton boton--secundario" [routerLink]="['/grupos', id(), 'equipos']">
            <app-icono nombre="equipo" />
            {{ t('equipos') }}
          </a>
        }
      </div>

      @if (grupo()?.archivadoEn) {
        <p class="aviso aviso--pendiente">{{ t('archivado') }}</p>
      }

      @if (sinVerificar()) {
        <div class="aviso aviso--pendiente">
          <p>
            <strong>{{ t('verifica.titulo') }}</strong> {{ t('verifica.texto') }}
          </p>
          <app-reenviar-verificacion />
        </div>
      }

      <div class="columnas">
        <div class="principal">
          @if (error(); as codigo) {
            <div class="aviso aviso--error" role="alert">
              <p>{{ texto('errores.' + codigo) }}</p>
              <button class="boton boton--secundario" type="button" (click)="cargar()">
                {{ t('reintentar') }}
              </button>
            </div>
          } @else if (alumnos() === null) {
            <ul class="filas" role="status" [attr.aria-label]="t('cargando')">
              @for (i of [1, 2, 3, 4]; track i) {
                <li class="fila-alumno" aria-hidden="true">
                  <span class="esqueleto numero-esqueleto"></span>
                  <span class="esqueleto linea-esqueleto"></span>
                </li>
              }
            </ul>
          } @else {
            @if (activos().length === 0 && !agregando()) {
              <section class="vacio" aria-labelledby="vacio-titulo">
                <h2 id="vacio-titulo">{{ t('vacio.titulo') }}</h2>
                <p class="dato-suave">{{ t('vacio.texto') }}</p>
                @if (puedeAgregar()) {
                  <ng-container [ngTemplateOutlet]="opciones" />
                }
              </section>
            } @else {
              <ol
                class="filas lista"
                [class.doble]="activos().length > 9"
                [attr.aria-label]="t('lista')"
              >
                @for (alumno of activos(); track alumno.id) {
                  <li>
                    <a class="fila-alumno" [routerLink]="['/alumnos', alumno.id]">
                      <span class="numero cifras">{{ alumno.numeroLista }}</span>
                      <span class="nombre">
                        <span class="apellidos">{{ alumno.apellidos }}</span> {{ alumno.nombres }}
                      </span>
                      @if (alumno.tutores.length) {
                        <span class="tutores dato-suave cifras">{{
                          t('tutores.' + forma(alumno.tutores.length), {
                            cantidad: alumno.tutores.length,
                          })
                        }}</span>
                      } @else {
                        <span class="tutores sin-tutor">{{ t('sinTutor') }}</span>
                      }
                    </a>
                  </li>
                }
              </ol>
            }

            @if (agregando()) {
              <form
                class="nuevo"
                (submit)="$event.preventDefault(); agregar()"
                novalidate
                [attr.aria-label]="t('nuevo.titulo')"
              >
                <span class="numero cifras" aria-hidden="true">{{ siguienteNumero() }}</span>
                <div class="campos-en-fila">
                  <div class="campo">
                    <label for="nuevo-apellidos">{{ t('nuevo.apellidos') }}</label>
                    <input
                      id="nuevo-apellidos"
                      name="apellidos"
                      maxlength="80"
                      autocomplete="off"
                      autocapitalize="words"
                      [value]="apellidos()"
                      (input)="apellidos.set(valor($event))"
                    />
                  </div>
                  <div class="campo">
                    <label for="nuevo-nombres">{{ t('nuevo.nombres') }}</label>
                    <input
                      id="nuevo-nombres"
                      name="nombres"
                      maxlength="80"
                      autocomplete="off"
                      autocapitalize="words"
                      [value]="nombres()"
                      (input)="nombres.set(valor($event))"
                      [attr.aria-invalid]="intento() && !nombres().trim()"
                      [attr.aria-describedby]="
                        intento() && !nombres().trim() ? 'nuevo-error' : null
                      "
                    />
                  </div>
                </div>
                @if (intento() && !nombres().trim()) {
                  <p id="nuevo-error" class="error">{{ t('nuevo.requerido') }}</p>
                }
                @if (errorNuevo(); as codigo) {
                  <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
                }
                <div class="acciones">
                  <button class="boton" type="submit" [appOcupado]="guardando()">
                    {{ t('nuevo.agregar') }}
                  </button>
                  <button
                    class="boton boton--secundario"
                    type="button"
                    (click)="terminarDeAgregar()"
                  >
                    {{ t('nuevo.listo') }}
                  </button>
                </div>
              </form>
            }
            <p class="solo-lector" aria-live="polite">{{ anuncio() }}</p>

            @if (bajas().length) {
              <details class="bajas">
                <summary>{{ t('bajas', { cantidad: bajas().length }) }}</summary>
                <ol class="filas">
                  @for (alumno of bajas(); track alumno.id) {
                    <li>
                      <a class="fila-alumno baja" [routerLink]="['/alumnos', alumno.id]">
                        <span class="numero cifras">{{ alumno.numeroLista }}</span>
                        <span class="nombre">
                          <span class="apellidos">{{ alumno.apellidos }}</span> {{ alumno.nombres }}
                        </span>
                        <span class="tutores dato-suave">{{ t('dadoDeBaja') }}</span>
                      </a>
                    </li>
                  }
                </ol>
              </details>
            }

            @if (puedeAgregar() && activos().length > 0 && !agregando()) {
              <!-- Lleno, no hay acción que fijar abajo: la nota va en línea y no tapa la lista. -->
              @if (lleno()) {
                <p class="dato-suave">{{ t('lleno') }}</p>
              } @else {
                <div class="pie-accion solo-celular">
                  <button id="agregar-alumnos" class="boton" type="button" (click)="hoja().abrir()">
                    <app-icono nombre="mas" />
                    {{ t('agregarAlumnos') }}
                  </button>
                </div>
              }
            }
          }
        </div>

        <!-- En escritorio no hace falta la hoja: las formas de agregar quedan a la vista junto a la lista. -->
        @if (puedeAgregar() && activos().length > 0 && !lleno()) {
          <aside class="lateral solo-escritorio" aria-labelledby="lateral-titulo">
            <h2 id="lateral-titulo">{{ t('agregarAlumnos') }}</h2>
            <ng-container [ngTemplateOutlet]="opciones" />
          </aside>
        }
      </div>

      <app-hoja etiqueta="hoja-agregar-titulo">
        <h2 id="hoja-agregar-titulo">{{ t('agregarAlumnos') }}</h2>
        <ng-container [ngTemplateOutlet]="opciones" />
      </app-hoja>

      <ng-template #opciones>
        <ul class="opciones">
          <li>
            <a
              class="opcion"
              [routerLink]="['/grupos', id(), 'importar']"
              [queryParams]="{ origen: 'excel' }"
            >
              <app-icono nombre="archivo" />
              <span>
                <strong>{{ t('opciones.excel') }}</strong>
                <span class="dato-suave">{{ t('opciones.excelTexto') }}</span>
              </span>
            </a>
          </li>
          <li>
            <a
              class="opcion"
              [routerLink]="['/grupos', id(), 'importar']"
              [queryParams]="{ origen: 'texto' }"
            >
              <app-icono nombre="pegar" />
              <span>
                <strong>{{ t('opciones.pegar') }}</strong>
                <span class="dato-suave">{{ t('opciones.pegarTexto') }}</span>
              </span>
            </a>
          </li>
          <li>
            <a
              class="opcion"
              [routerLink]="['/grupos', id(), 'importar']"
              [queryParams]="{ origen: 'foto' }"
            >
              <app-icono nombre="camara" />
              <span>
                <strong>{{ t('opciones.foto') }}</strong>
                <span class="dato-suave">{{ t('opciones.fotoTexto') }}</span>
              </span>
            </a>
          </li>
          <li>
            <button class="opcion" type="button" (click)="empezarAAgregar()">
              <app-icono nombre="persona" />
              <span>
                <strong>{{ t('opciones.uno') }}</strong>
                <span class="dato-suave">{{ t('opciones.unoTexto') }}</span>
              </span>
            </button>
          </li>
        </ul>
        <button
          class="enlace enlace--solo plantilla"
          type="button"
          [appOcupado]="descargando()"
          (click)="descargarPlantilla()"
        >
          <app-icono nombre="descargar" />
          {{ t('opciones.plantilla') }}
        </button>
      </ng-template>
    </div>
  `,
  styles: `
    hgroup {
      display: grid;
      gap: var(--espacio-1);
    }

    .encabezado {
      display: flex;
      flex-wrap: wrap;
      align-items: end;
      justify-content: space-between;
      gap: var(--espacio-3);
    }

    h2 {
      font-size: var(--texto-lg);
    }

    /*
     * Número, nombre y tutores en una fila alineados a la primera línea; con letra grande o pantalla angosta,
     * los tutores bajan a su propia línea en lugar de empujar la pantalla de lado.
     */
    .fila-alumno {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      align-content: center;
      column-gap: var(--espacio-3);
      row-gap: var(--espacio-1);
      min-height: 3.5rem;
      padding-block: var(--espacio-2);
      border-radius: var(--radio-md);
      color: inherit;
      text-decoration: none;
    }

    a.fila-alumno:active .nombre {
      color: var(--color-accion);
    }

    @media (hover: hover) and (pointer: fine) {
      a.fila-alumno:hover .nombre {
        text-decoration: underline;
        text-underline-offset: 0.2em;
      }
    }

    .fila-alumno .numero {
      flex: none;
      width: 2rem;
    }

    .numero {
      color: var(--color-texto-suave);
      font-weight: var(--peso-medio);
      text-align: end;
    }

    .nombre {
      flex: 1 1 8rem;
      min-width: 0;
      overflow-wrap: anywhere;
    }

    .apellidos {
      font-weight: var(--peso-medio);
    }

    .tutores {
      margin-inline-start: auto;
      text-align: end;
      white-space: nowrap;
    }

    .sin-tutor {
      color: var(--color-aviso-tinta);
      font-size: var(--texto-xs);
      font-weight: var(--peso-medio);
    }

    .baja .nombre {
      color: var(--color-texto-suave);
    }

    .numero-esqueleto {
      flex: none;
      width: 1.5rem;
      margin-inline-start: 0.5rem;
      height: 1.25rem;
      justify-self: end;
    }

    .linea-esqueleto {
      width: 12rem;
      max-width: 100%;
      height: 1.25rem;
    }

    .nuevo {
      display: grid;
      grid-template-columns: 2rem 1fr;
      gap: var(--espacio-3);
      padding-block: var(--espacio-3);
      border-top: 1px solid var(--color-divisoria);
    }

    .nuevo > .numero {
      margin-top: 2.25rem;
    }

    .nuevo > :not(.numero) {
      grid-column: 2;
    }

    .acciones {
      display: flex;
      flex-wrap: wrap;
      gap: var(--espacio-2);
    }

    .bajas summary {
      display: flex;
      align-items: center;
      min-height: var(--tactil-minimo);
      color: var(--color-texto-suave);
      font-weight: var(--peso-medio);
      cursor: pointer;
    }

    .principal {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      align-content: start;
      gap: var(--espacio-5);
    }

    .lateral {
      display: grid;
      gap: var(--espacio-3);
      padding: var(--espacio-5);
      border-radius: var(--radio-lg);
      background: var(--color-superficie);
      border: 1px solid var(--color-divisoria);
    }

    /* En escritorio, la vista vacía reparte las tres formas de agregar en una fila. */
    @media (min-width: 64rem) {
      .vacio .opciones {
        grid-template-columns: repeat(3, minmax(0, 1fr));
      }
    }

    /*
     * Escritorio ancho (con el panel al lado, cada columna mide al menos 22 rem): la lista larga va en dos
     * columnas que se leen de arriba abajo, en el orden de la lista.
     */
    @media (min-width: 75rem) {
      .lista.doble {
        display: block;
        columns: 2;
        column-gap: var(--espacio-6);
        border-top: 1px solid var(--color-divisoria);
      }

      .lista.doble > li {
        break-inside: avoid;
        border-top: 0;
        border-bottom: 1px solid var(--color-divisoria);
      }
    }

    .vacio {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      gap: var(--espacio-3);
    }

    .opciones {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      gap: var(--espacio-2);
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .opcion {
      display: grid;
      grid-template-columns: auto minmax(0, 1fr);
      align-items: start;
      gap: var(--espacio-3);
      width: 100%;
      min-height: 4rem;
      padding: var(--espacio-3) var(--espacio-4);
      border: 0;
      border-radius: var(--radio-lg);
      background: var(--color-superficie-hundida);
      color: var(--color-texto);
      font: inherit;
      text-align: start;
      text-decoration: none;
      cursor: pointer;
      transition:
        transform var(--dur-press) var(--ease-out),
        background-color var(--dur-small) var(--ease-out);
    }

    .opcion app-icono {
      margin-top: 0.125rem;
      color: var(--color-accion);
    }

    .opcion > span {
      display: grid;
      gap: var(--espacio-1);
    }

    .opcion:active {
      transform: scale(var(--escala-presion));
    }

    @media (hover: hover) and (pointer: fine) {
      .opcion:hover {
        background: var(--color-divisoria);
      }
    }

    .plantilla {
      gap: var(--espacio-2);
      justify-self: start;
    }

    .plantilla app-icono {
      width: 1rem;
      height: 1rem;
    }
  `,
})
export class Grupo implements OnInit {
  protected readonly forma = forma;
  private readonly api = inject(Api);
  private readonly http = inject(HttpClient);
  private readonly avisos = inject(AvisosService);
  private readonly i18n = inject(TranslocoService);
  private readonly servicio = inject(GruposService);
  private readonly perfil = inject(PerfilService).perfil;
  private readonly pintarPortada = controlarPortada();
  private readonly enfocar = crearEnfoque();

  readonly id = input.required<string>();

  protected readonly hoja = viewChild.required(Hoja);
  protected readonly maximo = MAXIMO_ALUMNOS;
  protected readonly grupo = signal<GrupoDto | null>(null);
  protected readonly alumnos = signal<readonly AlumnoDto[] | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly agregando = signal(false);
  protected readonly nombres = signal('');
  protected readonly apellidos = signal('');
  protected readonly intento = signal(false);
  protected readonly guardando = signal(false);
  protected readonly errorNuevo = signal<string | null>(null);
  protected readonly anuncio = signal('');
  protected readonly descargando = signal(false);

  protected readonly activos = computed(() =>
    (this.alumnos() ?? [])
      .filter((a) => !a.bajaEn)
      .sort((a, b) => Number(a.numeroLista) - Number(b.numeroLista)),
  );
  protected readonly clave = computed(() => {
    const g = this.grupo();
    return g ? claveGrado(g.nivel, g.grado) : undefined;
  });
  protected readonly bajas = computed(() => (this.alumnos() ?? []).filter((a) => a.bajaEn));
  protected readonly lleno = computed(() => this.activos().length >= MAXIMO_ALUMNOS);
  protected readonly sinVerificar = computed(() => this.perfil()?.correoVerificado === false);
  protected readonly puedeAgregar = computed(
    () => !!this.grupo() && !this.grupo()!.archivadoEn && !this.sinVerificar(),
  );
  protected readonly siguienteNumero = computed(
    () => Math.max(0, ...(this.alumnos() ?? []).map((a) => Number(a.numeroLista))) + 1,
  );

  async ngOnInit(): Promise<void> {
    const enCache = this.servicio.enCache(this.id());
    if (enCache) {
      this.mostrar(enCache);
    }
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      const [grupo, alumnos] = await Promise.all([
        this.servicio.obtener(this.id()),
        this.api.invoke(listarAlumnos, { id: this.id() }),
      ]);
      this.mostrar(grupo);
      this.alumnos.set(alumnos);
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

  protected empezarAAgregar(): void {
    this.hoja().cerrar();
    this.agregando.set(true);
    this.enfocar('#nuevo-apellidos');
  }

  protected terminarDeAgregar(): void {
    this.agregando.set(false);
    this.intento.set(false);
    this.errorNuevo.set(null);
    this.nombres.set('');
    this.apellidos.set('');
    // El formulario desaparece: el foco va a la acción que lo reemplaza o al título.
    this.enfocar(this.lleno() ? 'h1' : '#agregar-alumnos, .lateral .opcion');
  }

  /** Captura repetida: sin animación; se limpia y el foco vuelve a apellidos para el siguiente. */
  protected async agregar(): Promise<void> {
    this.intento.set(true);
    this.errorNuevo.set(null);
    if (!this.nombres().trim()) {
      this.enfocar('#nuevo-nombres');
      return;
    }
    this.guardando.set(true);
    try {
      const alumno = await this.api.invoke(agregarAlumno, {
        id: this.id(),
        body: { nombres: this.nombres().trim(), apellidos: this.apellidos().trim() || null },
      });
      this.alumnos.update((lista) => [...(lista ?? []), alumno]);
      this.anuncio.set(
        this.i18n.translate('grupo.nuevo.agregado', {
          nombre: `${alumno.nombres} ${alumno.apellidos}`.trim(),
          numero: alumno.numeroLista,
        }),
      );
      this.nombres.set('');
      this.apellidos.set('');
      this.intento.set(false);
      if (this.lleno()) {
        this.terminarDeAgregar();
      } else {
        this.enfocar('#nuevo-apellidos');
      }
    } catch (e) {
      this.errorNuevo.set(codigoDeError(e));
    } finally {
      this.guardando.set(false);
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
      // Un aviso y no el error de la pantalla: la lista del grupo sigue a la vista.
      this.avisos.mostrar(this.texto('errores.' + codigoDeError(e)), 'error');
    } finally {
      this.descargando.set(false);
    }
  }

  private mostrar(grupo: GrupoDto): void {
    this.grupo.set(grupo);
    this.pintarPortada(portadaDeGrupo(grupo));
  }
}
