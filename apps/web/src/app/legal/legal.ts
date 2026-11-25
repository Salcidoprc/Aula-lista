import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';

import { type Bloque, type ClaveDocumento, DOCUMENTOS_LEGALES, type Tabla } from './documentos';

const fecha = new Intl.DateTimeFormat('es-MX', {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
  timeZone: 'UTC',
});

/** Términos, aviso de privacidad, declaración de datos y aviso para tutores (RF-005, RF-006, RNF-006). */
@Component({
  selector: 'app-legal',
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article class="pantalla pantalla--ancha legal" *transloco="let t; prefix: 'legal'">
      <h1>{{ contenido().titulo }}</h1>
      <p class="actualizado">{{ t('actualizado', { fecha: actualizado() }) }}</p>
      @for (parrafo of contenido().entrada; track $index) {
        <p class="entrada">{{ parrafo }}</p>
      }
      @for (seccion of contenido().secciones; track seccion.id) {
        <section [attr.aria-labelledby]="'s-' + seccion.id">
          <h2 [id]="'s-' + seccion.id">{{ seccion.titulo }}</h2>
          @for (bloque of seccion.bloques; track $index) {
            @if (texto(bloque); as parrafo) {
              <p>{{ parrafo }}</p>
            } @else if (lista(bloque); as elementos) {
              <ul>
                @for (elemento of elementos; track $index) {
                  <li>{{ elemento }}</li>
                }
              </ul>
            } @else if (tabla(bloque); as datos) {
              <div class="tabla-legal">
                <table>
                  <thead>
                    <tr>
                      @for (encabezado of datos.encabezados; track $index) {
                        <th scope="col">{{ encabezado }}</th>
                      }
                    </tr>
                  </thead>
                  <tbody>
                    @for (fila of datos.filas; track $index) {
                      <tr>
                        @for (celda of fila; track $index; let primera = $first) {
                          @if (primera) {
                            <th scope="row">{{ celda }}</th>
                          } @else {
                            <td>{{ celda }}</td>
                          }
                        }
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }
          }
        </section>
      }
    </article>
  `,
  styles: `
    .legal {
      line-height: var(--interlineado);
    }
    /* Renglones de lectura cómoda también en escritorio. */
    .legal > * {
      max-width: 46rem;
    }
    .actualizado {
      color: var(--color-texto-tenue);
      font-size: var(--texto-sm);
    }
    h2 {
      font-size: var(--texto-lg);
      margin-block: var(--espacio-6) var(--espacio-2);
    }
    h2 + *,
    .entrada + .entrada {
      margin-block-start: 0;
    }
    section > * + *:not(h2 + *) {
      margin-block-start: var(--espacio-3);
    }
    ul {
      padding-inline-start: var(--espacio-5);
    }
    li + li {
      margin-block-start: var(--espacio-2);
    }
    .tabla-legal {
      overflow-x: auto;
    }
    table {
      border-collapse: collapse;
      width: 100%;
      font-size: var(--texto-sm);
    }
    th,
    td {
      border-block-end: 1px solid var(--color-divisoria);
      padding: var(--espacio-2) var(--espacio-3) var(--espacio-2) 0;
      text-align: start;
      vertical-align: top;
    }
    thead th {
      color: var(--color-texto-suave);
      font-weight: var(--peso-medio);
    }
    tbody th {
      font-weight: var(--peso-fuerte);
      min-width: 8rem;
    }
  `,
})
export class Legal {
  readonly documento = input.required<string>();

  protected readonly contenido = computed(() => {
    const clave = this.documento();
    return Object.hasOwn(DOCUMENTOS_LEGALES, clave)
      ? DOCUMENTOS_LEGALES[clave as ClaveDocumento]
      : DOCUMENTOS_LEGALES.terminos;
  });

  protected readonly actualizado = computed(() =>
    fecha.format(new Date(`${this.contenido().version}T00:00:00Z`)),
  );

  protected texto(bloque: Bloque): string | null {
    return typeof bloque === 'string' ? bloque : null;
  }

  protected lista(bloque: Bloque): readonly string[] | null {
    return typeof bloque !== 'string' && 'lista' in bloque ? bloque.lista : null;
  }

  protected tabla(bloque: Bloque): Tabla | null {
    return typeof bloque !== 'string' && 'tabla' in bloque ? bloque.tabla : null;
  }
}
