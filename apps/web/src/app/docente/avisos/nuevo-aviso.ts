import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { DestinatarioDto } from '../../api/models/destinatario-dto';
import { DestinatariosDto } from '../../api/models/destinatarios-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { codigoDeError } from '../../core/errores/problema';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { PRIMER_ERROR, crearEnfoque } from '../../shared/foco';
import { claveGrado } from '../../shared/grados';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { forma } from '../../shared/plural';
import { controlarPortada } from '../../shared/portada/portada.service';
import { portadaDeGrupo } from '../grupos/grupos.service';
import {
  CUERPO_MAXIMO,
  TITULO_MAXIMO,
  canalesPrevios,
  coincide,
  mostrarContador,
  nombresAlumnos,
  sinSaldo,
} from './avisos';
import { AvisosGrupoService } from './avisos.service';

type Para = 'todos' | 'elegir';

/**
 * SPEC-07, "Nuevo aviso" (RF-070, RF-071): a todos los tutores del grupo o a los elegidos, con título y cuerpo, y
 * urgente por WhatsApp mientras quede saldo (RN-086). Antes de enviar se ve por dónde le llegará a cada quien. Ya
 * enviado no se edita (RN-076).
 */
@Component({
  selector: 'app-nuevo-aviso',
  imports: [TranslocoDirective, RouterLink, Icono, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pantalla" [attr.data-grado]="clave() ?? null" *transloco="let t; prefix: 'avisos'">
      <a class="volver" [routerLink]="['/grupos', id(), 'avisos']">
        <app-icono nombre="volver" />
        {{ t('titulo') }}
      </a>

      <h1 tabindex="-1">{{ t('nuevo') }}</h1>

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (datos(); as d) {
        @if (d.soloLectura || d.tutores.length === 0) {
          <section class="vacio" aria-labelledby="sin-tutores">
            <h2 id="sin-tutores">
              {{ d.soloLectura ? t('archivado') : t('sinTutores.titulo') }}
            </h2>
            @if (!d.soloLectura) {
              <p>{{ t('sinTutores.texto') }}</p>
              <a class="boton boton--secundario" [routerLink]="['/grupos', id(), 'alumnos']">
                {{ t('sinTutores.accion') }}
              </a>
            }
          </section>
        } @else {
          <form class="formulario" novalidate (submit)="$event.preventDefault(); enviar()">
            <fieldset class="eleccion">
              <legend>{{ t('para') }}</legend>
              <label>
                <input
                  type="radio"
                  name="para"
                  value="todos"
                  [checked]="para() === 'todos'"
                  (change)="para.set('todos')"
                />
                <span class="cifras">{{ t('todos', { n: d.tutores.length }) }}</span>
              </label>
              <label>
                <input
                  type="radio"
                  name="para"
                  value="elegir"
                  [checked]="para() === 'elegir'"
                  (change)="para.set('elegir')"
                />
                {{ t('elegir') }}
              </label>
            </fieldset>

            @if (para() === 'elegir') {
              <fieldset class="elegidos" aria-describedby="elegidos-cuenta">
                <legend class="solo-lector">{{ t('elegirTutores') }}</legend>
                <div class="campo">
                  <label for="buscar">{{ t('buscar') }}</label>
                  <input
                    id="buscar"
                    type="search"
                    autocomplete="off"
                    enterkeyhint="search"
                    [value]="busqueda()"
                    (input)="busqueda.set(valor($event))"
                  />
                </div>
                <ul class="tutores">
                  @for (tutor of visibles(); track tutor.tutorId) {
                    <li>
                      <label class="casilla">
                        <input
                          type="checkbox"
                          [checked]="elegidos().has(tutor.tutorId)"
                          (change)="elegir(tutor.tutorId, marcado($event))"
                        />
                        <span>
                          <span class="nombre">{{ tutor.nombre }}</span>
                          @if (tutor.parentesco) {
                            <span class="dato-suave"> · {{ tutor.parentesco }}</span>
                          }
                          <span class="alumnos dato-suave">{{ alumnos(tutor) }}</span>
                        </span>
                      </label>
                    </li>
                  } @empty {
                    <li class="dato-suave sin-resultados">{{ t('sinResultados') }}</li>
                  }
                </ul>
                <p
                  id="elegidos-cuenta"
                  class="cifras"
                  [class.error]="intentado() && elegidos().size === 0"
                >
                  {{
                    intentado() && elegidos().size === 0
                      ? t('errorElegir')
                      : t('elegidosCuenta.' + forma(elegidos().size), { n: elegidos().size })
                  }}
                </p>
              </fieldset>
            }

            <div class="campo">
              <label for="titulo">{{ t('tituloCampo') }}</label>
              <input
                id="titulo"
                type="text"
                autocomplete="off"
                enterkeyhint="next"
                [maxLength]="tituloMaximo"
                [value]="titulo()"
                (input)="titulo.set(valor($event))"
                [attr.aria-invalid]="intentado() && !titulo().trim() ? 'true' : null"
                aria-describedby="titulo-ayuda"
              />
              <p
                id="titulo-ayuda"
                class="ayuda cifras"
                [class.error]="intentado() && !titulo().trim()"
              >
                @if (intentado() && !titulo().trim()) {
                  {{ t('errorTitulo') }}
                } @else if (contador(titulo(), tituloMaximo)) {
                  {{ t('contador', { usados: titulo().length, maximo: tituloMaximo }) }}
                } @else {
                  {{ t('tituloAyuda') }}
                }
              </p>
            </div>

            <div class="campo">
              <label for="cuerpo">{{ t('cuerpo') }}</label>
              <textarea
                id="cuerpo"
                rows="6"
                [maxLength]="cuerpoMaximo"
                [value]="cuerpo()"
                (input)="cuerpo.set(valor($event))"
                [attr.aria-invalid]="intentado() && !cuerpo().trim() ? 'true' : null"
                aria-describedby="cuerpo-ayuda"
              ></textarea>
              @if (intentado() && !cuerpo().trim()) {
                <p id="cuerpo-ayuda" class="ayuda error">{{ t('errorCuerpo') }}</p>
              } @else if (contador(cuerpo(), cuerpoMaximo)) {
                <p id="cuerpo-ayuda" class="ayuda cifras">
                  {{ t('contador', { usados: cuerpo().length, maximo: cuerpoMaximo }) }}
                </p>
              } @else {
                <p id="cuerpo-ayuda" class="solo-lector">
                  {{ t('contador', { usados: cuerpo().length, maximo: cuerpoMaximo }) }}
                </p>
              }
            </div>

            <div class="urgente">
              <label class="interruptor">
                <span class="texto-interruptor">
                  <span class="nombre">{{ t('urgenteCampo') }}</span>
                  <span class="ayuda dato-suave cifras" id="urgente-ayuda">{{
                    ayudaUrgente()
                  }}</span>
                </span>
                <input
                  type="checkbox"
                  role="switch"
                  [checked]="urgente()"
                  [disabled]="sinPro() || sinSaldoTodos()"
                  (change)="urgente.set(marcado($event))"
                  aria-describedby="urgente-ayuda"
                />
              </label>
              @if (sinPro()) {
                <a class="enlace enlace--solo" routerLink="/plan" [queryParams]="{ grupo: id() }">{{
                  t('urgenteVerPlanes')
                }}</a>
              }
            </div>

            <section class="llegada" aria-labelledby="llegada-titulo">
              <h2 id="llegada-titulo">{{ t('llegara') }}</h2>
              <dl class="canales cifras" aria-live="polite">
                <div>
                  <dt>{{ t('canales.push') }}</dt>
                  <dd>{{ canales().push }}</dd>
                </div>
                <div>
                  <dt>{{ t('canales.correo') }}</dt>
                  <dd>{{ canales().correo }}</dd>
                </div>
                <div>
                  <dt>{{ t('canales.whatsapp') }}</dt>
                  <dd>{{ canales().whatsapp }}</dd>
                </div>
                <div [class.falta]="canales().compartir > 0">
                  <dt>{{ t('canales.compartir') }}</dt>
                  <dd>{{ canales().compartir }}</dd>
                </div>
              </dl>
              @if (canales().compartir > 0) {
                <p class="dato-suave">{{ t('compartirNota') }}</p>
              }
            </section>

            @if (errorEnviar(); as codigo) {
              <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
            }

            <p class="dato-suave nota-envio">
              {{ enLinea() ? t('sinEditar') : t('sinConexion') }}
            </p>

            <div class="pie-accion">
              <button
                class="boton boton--ancho"
                type="submit"
                [disabled]="!enLinea()"
                [appOcupado]="enviando()"
              >
                {{ t('enviar') }}
              </button>
            </div>
          </form>
        }
      } @else {
        <span
          class="esqueleto marco-esqueleto"
          role="status"
          [attr.aria-label]="t('cargando')"
        ></span>
      }
    </div>
  `,
  styles: `
    .formulario {
      display: grid;
      gap: var(--espacio-5);
      max-inline-size: 40rem;
    }

    .elegidos {
      display: grid;
      gap: var(--espacio-3);
      margin: 0;
      padding: 0;
      border: 0;
    }

    .tutores {
      max-block-size: 22rem;
      margin: 0;
      padding: 0 var(--espacio-1);
      overflow-y: auto;
      overscroll-behavior: contain;
      border-block: 1px solid var(--color-divisoria);
      list-style: none;
    }

    .tutores li + li {
      border-top: 1px solid var(--color-divisoria);
    }

    .tutores .nombre {
      font-weight: var(--peso-medio);
    }

    .tutores .alumnos {
      display: block;
      overflow-wrap: anywhere;
    }

    .sin-resultados {
      padding-block: var(--espacio-3);
    }

    .elegidos > p {
      font-size: var(--texto-sm);
    }

    .elegidos > p.error {
      color: var(--color-peligro-tinta);
    }

    /* Interruptor: todo el renglón es el objetivo táctil; el estado se dice con la posición, no solo con color. */
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

    .interruptor:has(input:disabled) {
      cursor: not-allowed;
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
      box-shadow: 0 1px 2px oklch(0 0 0 / 0.2);
      transition: translate var(--dur-small) var(--ease-out);
    }

    .interruptor input:checked {
      background: var(--color-aviso-tinta);
    }

    .interruptor input:checked::before {
      translate: 1.25rem 0;
    }

    .interruptor input:disabled {
      opacity: 0.5;
    }

    .interruptor input:focus-visible {
      box-shadow: var(--foco);
      outline: none;
    }

    .llegada {
      display: grid;
      gap: var(--espacio-2);
    }

    .llegada h2 {
      font-size: var(--texto-sm);
      font-weight: var(--peso-medio);
    }

    .canales {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--espacio-2);
      margin: 0;
    }

    .canales div {
      display: grid;
      gap: var(--espacio-1);
      padding: var(--espacio-3);
      border-radius: var(--radio-md);
      background: var(--color-superficie-hundida);
    }

    .canales dt {
      color: var(--color-texto-suave);
      font-size: var(--texto-xs);
      font-weight: var(--peso-medio);
    }

    .canales dd {
      margin: 0;
      font-size: var(--texto-lg);
      font-weight: var(--peso-fuerte);
    }

    .canales .falta {
      background: var(--color-aviso-tinte);
      color: var(--color-aviso-tinta);
    }

    .canales .falta dt {
      color: inherit;
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
      max-inline-size: 40rem;
      block-size: 28rem;
    }
  `,
})
export class NuevoAviso implements OnInit {
  private readonly servicio = inject(AvisosGrupoService);
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);
  private readonly i18n = inject(TranslocoService);
  private readonly pintarPortada = controlarPortada();
  private readonly enfocar = crearEnfoque();
  protected readonly forma = forma;
  protected readonly tituloMaximo = TITULO_MAXIMO;
  protected readonly cuerpoMaximo = CUERPO_MAXIMO;
  protected readonly enLinea = this.servicio.enLinea;

  readonly id = input.required<string>();

  protected readonly grupo = signal<GrupoDto | null>(null);
  protected readonly datos = signal<DestinatariosDto | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly errorEnviar = signal<string | null>(null);
  protected readonly enviando = signal(false);
  protected readonly intentado = signal(false);

  protected readonly para = signal<Para>('todos');
  protected readonly elegidos = signal<ReadonlySet<string>>(new Set());
  protected readonly busqueda = signal('');
  protected readonly titulo = signal('');
  protected readonly cuerpo = signal('');
  protected readonly urgente = signal(false);

  protected readonly clave = computed(() => {
    const g = this.grupo();
    return g ? claveGrado(g.nivel, g.grado) : undefined;
  });
  private readonly tutores = computed(() => this.datos()?.tutores ?? []);
  protected readonly visibles = computed(() =>
    this.tutores().filter((t) => coincide(t, this.busqueda())),
  );
  protected readonly destinatarios = computed(() => {
    const todos = this.tutores();
    if (this.para() === 'todos') {
      return todos;
    }
    const elegidos = this.elegidos();
    return todos.filter((t) => elegidos.has(t.tutorId));
  });
  protected readonly canales = computed(() =>
    canalesPrevios(this.destinatarios(), this.urgente() && !this.sinPro()),
  );
  /** RN-086: el WhatsApp de los urgentes es de Pro; en Gratis el interruptor se apaga y dice por qué. */
  protected readonly sinPro = computed(() => this.datos()?.urgentesDisponibles === false);
  /** Sin saldo para nadie del grupo, el interruptor se apaga con su razón. */
  protected readonly sinSaldoTodos = computed(() => {
    const todos = this.tutores();
    return todos.length > 0 && sinSaldo(todos) === todos.length;
  });
  protected readonly ayudaUrgente = computed(() => {
    const limite = Number(this.datos()?.urgentesPorTutor ?? 0);
    if (this.sinPro()) {
      return this.i18n.translate('avisos.urgenteSinPro');
    }
    if (this.sinSaldoTodos()) {
      return this.i18n.translate('avisos.urgenteSinSaldo', { n: limite });
    }
    const agotados = sinSaldo(this.destinatarios());
    return agotados > 0
      ? this.i18n.translate(`avisos.urgenteAgotados.${forma(agotados)}`, { n: agotados, limite })
      : this.i18n.translate('avisos.urgenteAyuda', { n: limite });
  });

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      const [grupo, datos] = await Promise.all([
        this.servicio.grupo(this.id()),
        this.servicio.destinatarios(this.id()),
      ]);
      this.pintarPortada(portadaDeGrupo(grupo));
      this.grupo.set(grupo);
      this.datos.set(datos);
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  protected valor(evento: Event): string {
    return (evento.target as HTMLInputElement | HTMLTextAreaElement).value;
  }

  protected marcado(evento: Event): boolean {
    return (evento.target as HTMLInputElement).checked;
  }

  protected contador(texto: string, maximo: number): boolean {
    return mostrarContador(texto.length, maximo);
  }

  protected alumnos(tutor: DestinatarioDto): string {
    return nombresAlumnos(tutor.alumnos);
  }

  protected elegir(tutorId: string, elegido: boolean): void {
    const nuevos = new Set(this.elegidos());
    if (elegido) {
      nuevos.add(tutorId);
    } else {
      nuevos.delete(tutorId);
    }
    this.elegidos.set(nuevos);
  }

  protected async enviar(): Promise<void> {
    this.intentado.set(true);
    this.errorEnviar.set(null);
    const elegir = this.para() === 'elegir';
    if (elegir && this.elegidos().size === 0) {
      this.enfocar('#buscar');
      return;
    }
    if (!this.titulo().trim() || !this.cuerpo().trim()) {
      this.enfocar(PRIMER_ERROR);
      return;
    }
    this.enviando.set(true);
    try {
      const aviso = await this.servicio.crear(this.id(), {
        titulo: this.titulo().trim(),
        cuerpo: this.cuerpo().trim(),
        urgente: this.urgente() && !this.sinPro() && !this.sinSaldoTodos(),
        tutorIds: elegir ? [...this.elegidos()] : null,
      });
      this.avisos.mostrar(
        this.i18n.translate(`avisos.enviado.${forma(aviso.destinatarios)}`, {
          n: aviso.destinatarios,
        }),
      );
      await this.router.navigate(['/avisos', aviso.id], { replaceUrl: true });
    } catch (e) {
      this.errorEnviar.set(codigoDeError(e));
    } finally {
      this.enviando.set(false);
    }
  }
}
