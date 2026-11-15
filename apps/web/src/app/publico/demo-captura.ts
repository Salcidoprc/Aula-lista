import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';

/** Alumnos de ejemplo: [apellidos, nombre]. No son personas reales. */
const ALUMNOS: readonly (readonly [string, string])[] = [
  ['Aguilar Ramos', 'Valeria'],
  ['Beltrán Soto', 'Emiliano'],
  ['Castro Núñez', 'Ximena'],
  ['Domínguez Lara', 'Santiago'],
  ['Espinoza Villa', 'Camila'],
  ['Fuentes Ochoa', 'Diego'],
];
const TECLAS = ['10', '9', '8', '7', '6', '5'] as const;
const NO_ENTREGO = 'ne';
const GRUPO_TIPICO = 35;

/**
 * "Pruébalo aquí" (RF-010): el teclado rápido de la captura (SPEC-03) con seis alumnos de ejemplo, sin
 * guardar nada. Mide el tiempo real del visitante y lo lleva a un grupo de 35. Como en la app, capturar no
 * se anima: solo la celda cambia de fondo (regla 07).
 */
@Component({
  selector: 'app-demo-captura',
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(keydown)': 'tecla($event)' },
  template: `
    <div class="demo" *transloco="let t; prefix: 'landing.demo'">
      <div class="cabeza">
        <h4>{{ t('titulo') }}</h4>
        <p>{{ t('entrada') }}</p>
      </div>
      <p class="actividad">{{ t('actividad') }}</p>
      <ol class="lista">
        @for (alumno of alumnos; track $index) {
          <li>
            <button
              type="button"
              class="fila"
              [attr.aria-current]="$index === actual() && !terminado() ? 'true' : null"
              [attr.aria-label]="
                t('calificacionDe', { nombre: alumno[1] + ' ' + alumno[0] }) +
                ': ' +
                (valores()[$index] === '${NO_ENTREGO}'
                  ? t('noEntrego')
                  : (valores()[$index] ?? t('sinCalificar')))
              "
              (click)="actual.set($index)"
            >
              <span class="n cifras">{{ $index + 1 }}</span>
              <span class="nombre"
                ><strong>{{ alumno[0] }}</strong> {{ alumno[1] }}</span
              >
              <span class="celda cifras" [class.llena]="valores()[$index] !== null">{{
                valores()[$index] === '${NO_ENTREGO}' ? '—' : valores()[$index]
              }}</span>
            </button>
          </li>
        }
      </ol>
      <div class="teclado" role="group" [attr.aria-label]="t('teclado')">
        @for (v of teclas; track v) {
          <button type="button" class="cifras" (click)="calificar(v)">{{ v }}</button>
        }
        <button type="button" class="ne" (click)="calificar('${NO_ENTREGO}')">
          {{ t('noEntrego') }}
        </button>
      </div>
      <div class="resultado" aria-live="polite">
        @if (terminado()) {
          <p>
            <strong class="cifras">{{ t('listo', { n: alumnos.length, s: segundos() }) }}</strong>
            {{
              t('ritmo', {
                t: ritmo() < 60 ? t('segundos', { n: ritmo() }) : t('minutos', { n: minutos() }),
              })
            }}
          </p>
          <button type="button" class="otra" (click)="reiniciar()">{{ t('otraVez') }}</button>
        } @else {
          <p class="ayuda">{{ t('ayuda') }}</p>
        }
      </div>
    </div>
  `,
  styles: `
    :host {
      display: block;
      inline-size: min(100%, 24rem);
    }

    .demo {
      display: grid;
      gap: var(--espacio-3);
      padding: var(--espacio-4);
      border-radius: var(--radio-hoja);
      background: var(--color-superficie);
      box-shadow: var(--sombra-flotante);
    }

    .cabeza {
      display: grid;
      gap: var(--espacio-1);
    }

    h4 {
      margin: 0;
      font-size: var(--texto-lg);
    }

    p {
      margin: 0;
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
    }

    .actividad {
      padding: var(--espacio-2) var(--espacio-3);
      border-radius: var(--radio-md);
      background: var(--grado-tinte);
      color: var(--grado-tinta);
      font-weight: var(--peso-medio);
    }

    .lista {
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .fila {
      display: grid;
      grid-template-columns: 1.5rem 1fr 3rem;
      align-items: center;
      gap: var(--espacio-2);
      inline-size: 100%;
      min-block-size: var(--tactil-minimo);
      padding: var(--espacio-1) var(--espacio-2);
      border: 0;
      border-block-end: 1px solid var(--color-divisoria);
      background: none;
      color: var(--color-texto);
      font: inherit;
      text-align: start;
      cursor: pointer;
    }

    .fila[aria-current] {
      background: var(--grado-tinte);
    }

    .fila:focus-visible,
    .teclado button:focus-visible,
    .otra:focus-visible {
      outline: none;
      box-shadow: var(--foco);
    }

    .n {
      color: var(--color-texto-suave);
    }

    .nombre {
      overflow-wrap: anywhere;
    }

    .celda {
      display: grid;
      place-items: center;
      block-size: 2.25rem;
      border-radius: var(--radio-md);
      background: var(--color-superficie-hundida);
      font-weight: var(--peso-fuerte);
      transition: background-color var(--dur-small) var(--ease-out);
    }

    .celda.llena {
      background: var(--grado-tinte);
      color: var(--grado-tinta);
    }

    .teclado {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: var(--espacio-2);
    }

    .teclado button,
    .otra {
      min-block-size: 3rem;
      border: 0;
      border-radius: var(--radio-md);
      background: var(--color-superficie-hundida);
      color: var(--color-texto);
      font: inherit;
      font-size: var(--texto-lg);
      font-weight: var(--peso-fuerte);
      cursor: pointer;
      transition: transform var(--dur-press) var(--ease-out);
    }

    .teclado .ne {
      grid-column: span 2;
      font-size: var(--texto-sm);
    }

    .teclado button:active,
    .otra:active {
      transform: scale(var(--escala-presion));
    }

    .resultado {
      display: grid;
      gap: var(--espacio-2);
      min-block-size: 3.5rem;
    }

    .resultado strong {
      color: var(--color-texto);
    }

    .otra {
      justify-self: start;
      min-block-size: var(--tactil-minimo);
      padding-inline: var(--espacio-4);
      background: var(--color-accion);
      color: var(--color-sobre-accion);
      font-size: var(--texto-base);
    }
  `,
})
export class DemoCaptura {
  protected readonly alumnos = ALUMNOS;
  protected readonly teclas = TECLAS;
  protected readonly valores = signal<(string | null)[]>(ALUMNOS.map(() => null));
  protected readonly actual = signal(0);
  private readonly inicio = signal<number | null>(null);
  private readonly fin = signal<number | null>(null);

  protected readonly terminado = computed(() => this.fin() !== null);
  private readonly duracion = computed(() => ((this.fin() ?? 0) - (this.inicio() ?? 0)) / 1000);
  protected readonly segundos = computed(() =>
    this.duracion().toLocaleString('es-MX', { maximumFractionDigits: 1 }),
  );
  /** Segundos que tomaría un grupo típico al mismo ritmo. */
  protected readonly ritmo = computed(() =>
    Math.round((this.duracion() / ALUMNOS.length) * GRUPO_TIPICO),
  );
  protected readonly minutos = computed(() => Math.ceil(this.ritmo() / 60));

  protected calificar(valor: string): void {
    if (this.terminado()) {
      return;
    }
    this.inicio.update((i) => i ?? performance.now());
    const i = this.actual();
    this.valores.update((v) => v.map((x, j) => (j === i ? valor : x)));
    const vacio = this.valores().findIndex((x, j) => x === null && j > i);
    const siguiente = vacio >= 0 ? vacio : this.valores().indexOf(null);
    if (siguiente < 0) {
      this.fin.set(performance.now());
    } else {
      this.actual.set(siguiente);
    }
  }

  /** Teclas 5 a 9 y 0 (el 10), como en un teclado numérico. */
  protected tecla(evento: KeyboardEvent): void {
    const valor = evento.key === '0' ? '10' : evento.key;
    if ((TECLAS as readonly string[]).includes(valor) && !evento.ctrlKey && !evento.metaKey) {
      evento.preventDefault();
      this.calificar(valor);
    }
  }

  protected reiniciar(): void {
    this.valores.set(ALUMNOS.map(() => null));
    this.actual.set(0);
    this.inicio.set(null);
    this.fin.set(null);
  }
}
