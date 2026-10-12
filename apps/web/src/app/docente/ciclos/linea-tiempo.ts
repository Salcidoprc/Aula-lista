import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { mesCorto } from '../../shared/fechas';
import { ErrorCiclo, Fecha, UnidadEnEdicion, deDiaNumero, diaNumero, esFecha } from './unidades';

interface Tramo {
  readonly orden: number;
  readonly izquierda: number;
  readonly ancho: number;
  readonly error: boolean;
}

interface Marca {
  readonly texto: string;
  readonly izquierda: number;
}

/**
 * Línea de tiempo del ciclo: un tramo por unidad sobre la duración del ciclo, con los meses abajo. Los días
 * entre unidades (vacaciones) quedan en blanco. Es una vista: las fechas se editan en la lista de unidades.
 */
@Component({
  selector: 'app-linea-tiempo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pista">
      @for (tramo of tramos(); track tramo.orden) {
        <span
          class="tramo cifras"
          [class.error]="tramo.error"
          [style.inset-inline-start.%]="tramo.izquierda"
          [style.width.%]="tramo.ancho"
          >{{ tramo.orden }}</span
        >
      }
    </div>
    <div class="meses" aria-hidden="true">
      @for (marca of meses(); track marca.izquierda) {
        <span [style.inset-inline-start.%]="marca.izquierda">{{ marca.texto }}</span>
      }
    </div>
  `,
  styles: `
    :host {
      display: grid;
      gap: var(--espacio-1);
    }

    .pista {
      position: relative;
      height: 2.5rem;
      border-radius: var(--radio-md);
      box-shadow: inset 0 0 0 1px var(--color-divisoria);
    }

    .tramo {
      position: absolute;
      inset-block: 0;
      display: grid;
      place-items: center;
      min-width: 1.25rem;
      border-radius: var(--radio-md);
      background: var(--grado-tinte);
      color: var(--grado-tinta);
      font-size: var(--texto-sm);
      font-weight: var(--peso-fuerte);
      box-shadow: inset 0 0 0 1px var(--color-fondo);
      transition: background-color var(--dur-small) var(--ease-out);
    }

    .tramo.error {
      background: var(--color-peligro-tinte);
      color: var(--color-peligro-tinta);
      box-shadow: inset 0 0 0 2px var(--color-peligro);
    }

    .meses {
      position: relative;
      height: 1.25rem;
      color: var(--color-texto-suave);
      font-size: var(--texto-xs);
    }

    .meses span {
      position: absolute;
      top: 0;
      padding-inline-start: 2px;
      border-inline-start: 1px solid var(--color-divisoria);
      line-height: 1.25rem;
    }

    @media (prefers-reduced-motion: reduce) {
      .tramo {
        transition: none;
      }
    }
  `,
})
export class LineaTiempo {
  readonly inicio = input.required<Fecha>();
  readonly fin = input.required<Fecha>();
  readonly unidades = input.required<readonly UnidadEnEdicion[]>();
  readonly errores = input<readonly (ErrorCiclo | null)[]>([]);

  private readonly rango = computed(() => {
    if (!esFecha(this.inicio()) || !esFecha(this.fin())) {
      return null;
    }
    const desde = diaNumero(this.inicio());
    const dias = diaNumero(this.fin()) - desde + 1;
    return dias > 0 ? { desde, dias } : null;
  });

  protected readonly tramos = computed<Tramo[]>(() => {
    const rango = this.rango();
    if (!rango) {
      return [];
    }
    const pos = (dia: number) =>
      Math.min(100, Math.max(0, ((dia - rango.desde) / rango.dias) * 100));
    return this.unidades()
      .map((u, i) => ({ u, i }))
      .filter(({ u }) => esFecha(u.inicio) && esFecha(u.fin) && u.fin >= u.inicio)
      .map(({ u, i }) => {
        const izquierda = pos(diaNumero(u.inicio));
        return {
          orden: i + 1,
          izquierda,
          ancho: Math.max(0, pos(diaNumero(u.fin) + 1) - izquierda),
          error: !!this.errores()[i],
        };
      });
  });

  /** Una marca al inicio de cada mes; si el ciclo es largo, el texto de los meses alternos se omite. */
  protected readonly meses = computed<Marca[]>(() => {
    const rango = this.rango();
    if (!rango) {
      return [];
    }
    const marcas: Marca[] = [];
    const inicio = this.inicio();
    const [a, mesInicio] = inicio.split('-').map(Number);
    let m = Number(inicio.slice(8, 10)) > 1 ? mesInicio + 1 : mesInicio;
    for (;;) {
      const fecha = deDiaNumero(Math.round(Date.UTC(a, m - 1, 1) / 86_400_000));
      const dia = diaNumero(fecha);
      if (dia > rango.desde + rango.dias - 1) {
        break;
      }
      marcas.push({ texto: mesCorto(fecha), izquierda: ((dia - rango.desde) / rango.dias) * 100 });
      m += 1;
    }
    return marcas.length > 12 ? marcas.map((mk, i) => (i % 2 ? { ...mk, texto: '' } : mk)) : marcas;
  });
}
