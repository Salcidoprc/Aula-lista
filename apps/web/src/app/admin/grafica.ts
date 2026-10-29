import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { entero, semanaCorta } from './panel';

export type FormaGrafica = 'linea' | 'columnas';

interface Marca {
  readonly x: number;
  readonly y: number;
  readonly d: string;
  readonly actual: boolean;
}

interface Linea {
  readonly y: number;
  readonly texto: string;
}

interface Rotulo {
  readonly x: number;
  readonly texto: string;
  readonly ancla: 'start' | 'middle' | 'end';
}

const MARGEN = { izq: 32, der: 4, arr: 8, aba: 24 } as const;
const ANCHO_MAXIMO_COLUMNA = 24;

/** El paso del eje: 1, 2 o 5 por potencia de 10, de modo que dos pasos cubran el máximo. */
export function pasoDelEje(maximo: number): number {
  const mitad = Math.max(1, maximo / 2);
  const potencia = 10 ** Math.floor(Math.log10(mitad));
  return [1, 2, 5, 10].map((m) => m * potencia).find((p) => p >= mitad) ?? 10 * potencia;
}

/**
 * RF-094: la tendencia de un indicador en las últimas semanas, en SVG propio y una sola tinta (`accion`). La semana en
 * curso está incompleta: su columna va hueca y su tramo de línea, punteado. El cursor o las flechas eligen una semana y
 * el tooltip dice su cifra; nada depende de él, porque la tabla del Resumen tiene los mismos datos. No se anima.
 */
@Component({
  selector: 'app-grafica',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    tabindex: '0',
    role: 'group',
    '[attr.aria-label]': 'lectura()',
    '(keydown)': 'tecla($event)',
    '(focus)': 'enfocar()',
    '(blur)': 'elegida.set(null)',
  },
  template: `
    <svg
      [attr.viewBox]="'0 0 ' + ancho() + ' ' + alto()"
      [attr.width]="ancho()"
      [attr.height]="alto()"
      aria-hidden="true"
      (pointermove)="mover($event)"
      (pointerleave)="salir()"
    >
      @for (l of lineas(); track l.y) {
        <line
          class="rejilla"
          [attr.x1]="margen.izq"
          [attr.x2]="ancho() - margen.der"
          [attr.y1]="l.y"
          [attr.y2]="l.y"
        />
        <text
          class="eje"
          [attr.x]="margen.izq - 6"
          [attr.y]="l.y"
          text-anchor="end"
          dominant-baseline="middle"
        >
          {{ l.texto }}
        </text>
      }
      @for (r of rotulos(); track r.x) {
        <text class="eje" [attr.x]="r.x" [attr.y]="alto() - 6" [attr.text-anchor]="r.ancla">
          {{ r.texto }}
        </text>
      }
      @if (forma() === 'linea') {
        <path class="lavado" [attr.d]="lavado()" />
        <path class="trazo" [attr.d]="trazoCompleto()" />
        <path class="trazo trazo--curso" [attr.d]="trazoEnCurso()" />
      } @else {
        @for (m of marcas(); track $index) {
          <path
            class="columna"
            [class.actual]="m.actual"
            [class.elegida]="elegida() === $index"
            [attr.d]="m.d"
          />
        }
      }
      @if (elegida() !== null) {
        <line
          class="guia"
          [attr.x1]="centro(elegida()!)"
          [attr.x2]="centro(elegida()!)"
          [attr.y1]="margen.arr"
          [attr.y2]="base()"
        />
        @if (forma() === 'linea') {
          <circle
            class="punto"
            [attr.cx]="centro(elegida()!)"
            [attr.cy]="marcas()[elegida()!].y"
            r="4"
          />
        }
      }
    </svg>
    @if (elegida() !== null) {
      <div
        class="tooltip"
        aria-hidden="true"
        [class.tooltip--izquierda]="tooltipALaIzquierda()"
        [style.left.px]="centro(elegida()!)"
      >
        <strong>{{ formato()(valores()[elegida()!]) }}</strong>
        <span>{{ fechaDe(elegida()!) }}</span>
      </div>
    }
    <span class="solo-lector" aria-live="polite">{{ anuncio() }}</span>
  `,
  styles: `
    :host {
      position: relative;
      display: block;
      min-width: 0;
      border-radius: var(--radio-md);
      outline: none;
      touch-action: pan-y;
    }

    :host(:focus-visible) {
      box-shadow: var(--foco);
    }

    svg {
      display: block;
      overflow: visible;
    }

    .rejilla {
      stroke: var(--color-divisoria);
      stroke-width: 1;
      shape-rendering: crispEdges;
    }

    .eje {
      fill: var(--color-texto-suave);
      font-family: var(--fuente-cifras);
      font-size: 0.75rem;
      font-variant-numeric: tabular-nums;
    }

    .columna {
      fill: var(--color-accion);
    }

    .columna.actual {
      fill: var(--color-accion-tinte);
      stroke: var(--color-accion);
      stroke-width: 1.5;
    }

    .columna.elegida:not(.actual) {
      fill: var(--color-accion-presionada);
    }

    .lavado {
      fill: var(--color-accion);
      opacity: 0.1;
    }

    .trazo {
      fill: none;
      stroke: var(--color-accion);
      stroke-width: 2;
      stroke-linecap: round;
      stroke-linejoin: round;
    }

    .trazo--curso {
      stroke-dasharray: 3 4;
    }

    .guia {
      stroke: var(--color-texto-tenue);
      stroke-width: 1;
      shape-rendering: crispEdges;
    }

    .punto {
      fill: var(--color-accion);
      stroke: var(--color-fondo);
      stroke-width: 2;
    }

    .tooltip {
      position: absolute;
      top: 0;
      display: grid;
      gap: 2px;
      min-inline-size: max-content;
      padding: var(--espacio-1) var(--espacio-2);
      border-radius: var(--radio-sm);
      background: var(--color-superficie);
      box-shadow: var(--sombra-flotante);
      font-size: var(--texto-xs);
      line-height: 1.3;
      pointer-events: none;
      transform: translateX(var(--espacio-2));
    }

    .tooltip--izquierda {
      transform: translateX(calc(-100% - var(--espacio-2)));
    }

    .tooltip strong {
      font-family: var(--fuente-cifras);
      font-size: var(--texto-sm);
      font-variant-numeric: tabular-nums;
    }

    .tooltip span {
      color: var(--color-texto-suave);
    }

    @media (forced-colors: active) {
      .columna,
      .punto {
        fill: Highlight;
      }

      .columna.actual {
        fill: Canvas;
        stroke: Highlight;
      }

      .trazo,
      .guia {
        stroke: Highlight;
      }

      .rejilla {
        stroke: GrayText;
      }

      .eje {
        fill: CanvasText;
      }

      .tooltip {
        border: 1px solid CanvasText;
      }
    }
  `,
})
export class Grafica {
  /** Un valor por semana, de la más vieja a la actual. */
  readonly valores = input.required<readonly number[]>();
  /** El lunes (aaaa-mm-dd) de cada semana, en el mismo orden. */
  readonly semanas = input.required<readonly string[]>();
  readonly forma = input<FormaGrafica>('columnas');
  /** Lo que leen los lectores de pantalla al entrar a la gráfica: el indicador y todas sus cifras. */
  readonly lectura = input.required<string>();
  /** "Semana del 13 oct" y "Esta semana, en curso": vienen traducidas del Resumen. */
  readonly textoSemana = input.required<(semana: string, enCurso: boolean) => string>();
  readonly formato = input<(valor: number) => string>((v) => entero(v));
  readonly alto = input(160);

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  protected readonly margen = MARGEN;
  protected readonly ancho = signal(320);
  protected readonly elegida = signal<number | null>(null);
  private readonly porTeclado = signal(false);

  private readonly n = computed(() => this.valores().length);
  private readonly banda = computed(
    () => (this.ancho() - MARGEN.izq - MARGEN.der) / Math.max(1, this.n()),
  );
  protected readonly base = computed(() => this.alto() - MARGEN.aba);
  private readonly paso = computed(() => pasoDelEje(Math.max(0, ...this.valores())));
  private readonly tope = computed(() => this.paso() * 2);

  protected readonly lineas = computed<readonly Linea[]>(() =>
    [0, 1, 2].map((k) => ({ y: this.y(k * this.paso()), texto: entero(k * this.paso()) })),
  );

  protected readonly rotulos = computed<readonly Rotulo[]>(() => {
    const n = this.n();
    if (n === 0) {
      return [];
    }
    const semanas = this.semanas();
    const rotulos: Rotulo[] = [
      { x: this.centro(0) - this.banda() / 2, texto: semanaCorta(semanas[0]), ancla: 'start' },
    ];
    if (this.ancho() >= 360 && n > 2) {
      const medio = Math.floor((n - 1) / 2);
      rotulos.push({ x: this.centro(medio), texto: semanaCorta(semanas[medio]), ancla: 'middle' });
    }
    if (n > 1) {
      rotulos.push({
        x: this.centro(n - 1) + this.banda() / 2,
        texto: semanaCorta(semanas[n - 1]),
        ancla: 'end',
      });
    }
    return rotulos;
  });

  protected readonly marcas = computed<readonly Marca[]>(() => {
    const base = this.base();
    const ancho = Math.min(ANCHO_MAXIMO_COLUMNA, this.banda() * 0.62);
    const ultima = this.n() - 1;
    return this.valores().map((v, i) => {
      const x = this.centro(i);
      const y = this.y(v);
      const alto = base - y;
      const r = Math.min(4, ancho / 2, alto);
      const izq = x - ancho / 2;
      const d =
        alto <= 0
          ? ''
          : `M${izq},${base}V${y + r}Q${izq},${y} ${izq + r},${y}H${izq + ancho - r}` +
            `Q${izq + ancho},${y} ${izq + ancho},${y + r}V${base}Z`;
      return { x, y, d, actual: i === ultima };
    });
  });

  protected readonly trazoCompleto = computed(() =>
    this.camino(this.marcas().slice(0, Math.max(1, this.n() - 1))),
  );
  protected readonly trazoEnCurso = computed(() =>
    this.n() < 2 ? '' : this.camino(this.marcas().slice(-2)),
  );
  protected readonly lavado = computed(() => {
    const m = this.marcas();
    if (m.length === 0) {
      return '';
    }
    return `${this.camino(m)}L${m[m.length - 1].x},${this.base()}L${m[0].x},${this.base()}Z`;
  });

  /** El tooltip va al lado de la guía, del lado donde hay más espacio, para no tapar el título de la gráfica. */
  protected readonly tooltipALaIzquierda = computed(() => {
    const i = this.elegida();
    return i !== null && this.centro(i) > this.ancho() / 2;
  });

  protected readonly anuncio = computed(() => {
    const i = this.elegida();
    return i === null || !this.porTeclado()
      ? ''
      : `${this.formato()(this.valores()[i])}, ${this.fechaDe(i)}`;
  });

  constructor() {
    const destruir = inject(DestroyRef);
    afterNextRender(() => {
      const elemento = this.host.nativeElement;
      const medir = () => this.ancho.set(Math.max(160, Math.floor(elemento.clientWidth)));
      medir();
      const observador = new ResizeObserver(medir);
      observador.observe(elemento);
      destruir.onDestroy(() => observador.disconnect());
    });
  }

  protected centro(i: number): number {
    return MARGEN.izq + this.banda() * (i + 0.5);
  }

  protected fechaDe(i: number): string {
    return this.textoSemana()(this.semanas()[i], i === this.n() - 1);
  }

  protected mover(evento: PointerEvent): void {
    const svg = evento.currentTarget as SVGSVGElement;
    const x = evento.clientX - svg.getBoundingClientRect().left;
    this.porTeclado.set(false);
    this.elegida.set(this.indiceEn(x));
  }

  protected salir(): void {
    if (!this.porTeclado()) {
      this.elegida.set(null);
    }
  }

  protected enfocar(): void {
    this.porTeclado.set(true);
    this.elegida.set(this.n() - 1);
  }

  protected tecla(evento: KeyboardEvent): void {
    const actual = this.elegida() ?? this.n() - 1;
    const destino: Record<string, number> = {
      ArrowLeft: actual - 1,
      ArrowRight: actual + 1,
      Home: 0,
      End: this.n() - 1,
    };
    if (!(evento.key in destino)) {
      return;
    }
    evento.preventDefault();
    this.porTeclado.set(true);
    this.elegida.set(Math.min(Math.max(destino[evento.key], 0), this.n() - 1));
  }

  private indiceEn(x: number): number {
    const i = Math.floor((x - MARGEN.izq) / this.banda());
    return Math.min(Math.max(i, 0), this.n() - 1);
  }

  private y(valor: number): number {
    const alto = this.base() - MARGEN.arr;
    return this.base() - (Math.max(0, valor) / this.tope()) * alto;
  }

  private camino(marcas: readonly Marca[]): string {
    return marcas.map((m, i) => `${i === 0 ? 'M' : 'L'}${m.x},${m.y}`).join('');
  }
}
