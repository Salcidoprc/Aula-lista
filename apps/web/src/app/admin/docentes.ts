import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { DocentesAdminDto } from '../api/models/docentes-admin-dto';
import { codigoDeError } from '../core/errores/problema';
import { forma } from '../shared/plural';
import { AdminService } from './admin.service';
import { ESTILOS_PANEL } from './estilos';
import { entero, instante, mesLargo, pesos } from './panel';

const POR_PAGINA = 50;

/** RF-091: los docentes con su plan, sus grupos, su último acceso y lo que consumieron en el mes (RN-090). */
@Component({
  selector: 'app-docentes',
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t; prefix: 'admin.docentes'">
      <div class="cabeza">
        <h1 tabindex="-1">{{ t('titulo') }}</h1>
        @if (datos(); as d) {
          <p class="resumen">
            {{
              t('resumen.' + forma(d.docentes.length), {
                n: entero(d.docentes.length),
                mes: mes(d.mes),
              })
            }}
          </p>
        }
      </div>

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (!datos()) {
        <div class="cargando" role="status" [attr.aria-label]="t('cargando')">
          @for (i of [1, 2, 3, 4]; track i) {
            <span class="esqueleto" aria-hidden="true"></span>
          }
        </div>
      } @else if (datos(); as d) {
        @if (d.docentes.length === 0) {
          <p class="vacio">{{ t('vacio') }}</p>
        } @else {
          <div class="campo buscar">
            <label for="buscar-docente">{{ t('buscar') }}</label>
            <input
              id="buscar-docente"
              type="search"
              enterkeyhint="search"
              autocomplete="off"
              spellcheck="false"
              [value]="busqueda()"
              (input)="buscar(valor($event))"
            />
          </div>
          <p class="solo-lector" role="status">
            {{ t('encontrados.' + forma(visibles().length), { n: entero(visibles().length) }) }}
          </p>
          @if (visibles().length === 0) {
            <p class="vacio">{{ t('sinCoincidencias') }}</p>
          } @else {
            <table class="libreta apilable" role="table">
              <thead role="rowgroup">
                <tr role="row">
                  <th scope="col" role="columnheader">{{ t('docente') }}</th>
                  <th scope="col" role="columnheader">{{ t('plan') }}</th>
                  <th scope="col" role="columnheader" class="num">{{ t('grupos') }}</th>
                  <th scope="col" role="columnheader">{{ t('ultimoAcceso') }}</th>
                  <th scope="col" role="columnheader" class="num">{{ t('consumo') }}</th>
                </tr>
              </thead>
              <tbody role="rowgroup">
                @for (dc of mostrados(); track dc.id) {
                  <tr role="row">
                    <th scope="row" role="rowheader">
                      {{ dc.nombre || t('sinNombre') }}
                      <span class="secundario">{{ dc.correo }}</span>
                      @if (!dc.cuentaActiva) {
                        <span class="sello" data-tono="peligro">{{ t('desactivada') }}</span>
                      }
                    </th>
                    <td role="cell" [attr.data-etiqueta]="t('plan')">
                      <span class="sello" [attr.data-tono]="tono(dc.plan)">{{
                        t('planes.' + dc.plan)
                      }}</span>
                    </td>
                    <td role="cell" class="num" [attr.data-etiqueta]="t('grupos')">
                      <span>
                        {{ entero(dc.grupos) }}
                        @if (+dc.gruposPro > 0) {
                          <span class="secundario">{{ t('enPro', { n: dc.gruposPro }) }}</span>
                        }
                      </span>
                    </td>
                    <td role="cell" [attr.data-etiqueta]="t('ultimoAcceso')">
                      <span>
                        {{ dc.ultimoAcceso ? instante(dc.ultimoAcceso) : t('nunca') }}
                        <span class="secundario">{{
                          t('registro', { fecha: instante(dc.registradoEn) })
                        }}</span>
                      </span>
                    </td>
                    <td role="cell" class="num" [attr.data-etiqueta]="t('consumo')">
                      <span>
                        {{ pesos(dc.consumo.costoMxn) }}
                        <span class="secundario">{{
                          t('detalleConsumo', {
                            ia: entero(dc.consumo.llamadasIa),
                            whatsapp: entero(dc.consumo.mensajesWhatsapp),
                          })
                        }}</span>
                      </span>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
            @if (visibles().length > mostrados().length) {
              <button class="boton boton--secundario mas" type="button" (click)="verMas()">
                {{ t('verMas', { n: entero(visibles().length - mostrados().length) }) }}
              </button>
            }
          }
        }
      }
    </ng-container>
  `,
  styles: [
    ESTILOS_PANEL,
    `
      .buscar {
        max-width: 24rem;
      }

      td > span {
        display: inline-grid;
        gap: var(--espacio-1);
      }

      td.num > span {
        justify-items: end;
      }

      .mas {
        justify-self: start;
      }

      th .sello {
        margin-block-start: var(--espacio-1);
      }

      @media (max-width: 47.99rem) {
        td > span,
        td.num > span {
          justify-items: start;
          text-align: start;
        }
      }
    `,
  ],
})
export class Docentes implements OnInit {
  private readonly servicio = inject(AdminService);
  private readonly i18n = inject(TranslocoService);

  protected readonly datos = signal<DocentesAdminDto | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busqueda = signal('');
  /** Se pintan de 50 en 50: el piloto tiene decenas, pero la lista no debe pesar si crece. */
  private readonly limite = signal(POR_PAGINA);
  protected readonly forma = forma;
  protected readonly entero = entero;
  protected readonly pesos = pesos;
  protected readonly instante = instante;
  protected readonly mes = mesLargo;

  protected readonly visibles = computed(() => {
    const docentes = this.datos()?.docentes ?? [];
    const q = this.busqueda().trim().toLocaleLowerCase('es-MX');
    return q
      ? docentes.filter(
          (d) =>
            d.correo.toLocaleLowerCase('es-MX').includes(q) ||
            (d.nombre ?? '').toLocaleLowerCase('es-MX').includes(q),
        )
      : docentes;
  });

  protected readonly mostrados = computed(() => this.visibles().slice(0, this.limite()));

  protected buscar(texto: string): void {
    this.busqueda.set(texto);
    this.limite.set(POR_PAGINA);
  }

  protected verMas(): void {
    this.limite.update((n) => n + POR_PAGINA);
  }

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      this.datos.set(await this.servicio.docentes());
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected tono(plan: string): string {
    return { tarjeta: 'accion', oxxo: 'accion', prueba: 'aviso', en_gracia: 'peligro' }[plan] ?? '';
  }

  protected valor(evento: Event): string {
    return (evento.target as HTMLInputElement).value;
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }
}
