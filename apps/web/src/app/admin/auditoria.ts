import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { AuditoriaAdminDto } from '../api/models/auditoria-admin-dto';
import { codigoDeError } from '../core/errores/problema';
import { Ocupado } from '../shared/ocupado';
import { AdminService } from './admin.service';
import { ESTILOS_PANEL } from './estilos';
import { instante, valorAuditoria } from './panel';

/**
 * RF-093 y RN-095: lo que hicieron los administradores, del más reciente al más viejo, con cada campo que cambió y
 * su valor antes y después. De 50 en 50.
 */
@Component({
  selector: 'app-auditoria',
  imports: [TranslocoDirective, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t; prefix: 'admin.auditoria'">
      <div class="cabeza">
        <h1 tabindex="-1">{{ t('titulo') }}</h1>
        <p class="resumen">{{ t('resumen') }}</p>
      </div>

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (!registros()) {
        <div class="cargando" role="status" [attr.aria-label]="t('cargando')">
          @for (i of [1, 2, 3]; track i) {
            <span class="esqueleto" aria-hidden="true"></span>
          }
        </div>
      } @else if (registros(); as lista) {
        @if (lista.length === 0) {
          <p class="vacio">{{ t('vacio') }}</p>
        } @else {
          <ol class="registros">
            @for (r of lista; track r.id) {
              <li>
                <article [attr.aria-labelledby]="'registro-' + r.id">
                  <header>
                    <h2 [id]="'registro-' + r.id">
                      {{ t('acciones.' + r.accion) }}
                      @if (nombreDe(r); as nombre) {
                        <span class="objeto">{{ nombre }}</span>
                      }
                    </h2>
                    <p class="quien">
                      {{ r.correo ?? t('sinCorreo') }}
                      <span aria-hidden="true">&nbsp;·&nbsp;</span>
                      <time [attr.datetime]="r.fecha">{{ instante(r.fecha) }}</time>
                    </p>
                  </header>
                  @if (r.cambios.length > 0) {
                    <table class="libreta cambios">
                      <caption class="solo-lector">
                        {{
                          t('cambios')
                        }}
                      </caption>
                      <thead>
                        <tr>
                          <th scope="col">{{ t('campo') }}</th>
                          <th scope="col">{{ t('antes') }}</th>
                          <th scope="col">{{ t('despues') }}</th>
                        </tr>
                      </thead>
                      <tbody>
                        @for (c of r.cambios; track c.campo) {
                          <tr>
                            <th scope="row">{{ campo(c.campo) }}</th>
                            <td class="antes" [class.nulo]="c.antes === null">
                              {{ valor(c.campo, c.antes) }}
                            </td>
                            <td class="despues">{{ valor(c.campo, c.despues) }}</td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  }
                </article>
              </li>
            }
          </ol>
          @if (siguiente()) {
            <button
              class="boton boton--secundario mas"
              type="button"
              [appOcupado]="cargandoMas()"
              (click)="mas()"
            >
              {{ t('mas') }}
            </button>
          }
          @if (errorMas(); as codigo) {
            <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
          }
        }
      }
    </ng-container>
  `,
  styles: [
    ESTILOS_PANEL,
    `
      .registros {
        display: grid;
        gap: var(--espacio-4);
        margin: 0;
        padding: 0;
        list-style: none;
      }

      article {
        display: grid;
        gap: var(--espacio-3);
        padding: var(--espacio-4);
        border-radius: var(--radio-lg);
        background: var(--color-superficie);
        box-shadow: 0 0 0 1px var(--color-divisoria);
      }

      header {
        display: flex;
        flex-wrap: wrap;
        align-items: baseline;
        justify-content: space-between;
        gap: var(--espacio-1) var(--espacio-4);
      }

      h2 {
        font-size: var(--texto-base);
      }

      .objeto {
        font-weight: var(--peso-fuerte);
      }

      .quien {
        margin: 0;
        color: var(--color-texto-suave);
        font-size: var(--texto-xs);
        overflow-wrap: anywhere;
      }

      .cambios {
        table-layout: fixed;
      }

      .cambios th,
      .cambios td {
        padding-block: var(--espacio-2);
        overflow-wrap: anywhere;
      }

      .cambios tbody tr:last-child > * {
        border-block-end: 0;
      }

      .cambios thead th:first-child {
        inline-size: 30%;
      }

      .cambios thead th:not(:first-child) {
        inline-size: 35%;
      }

      .antes {
        color: var(--color-texto-suave);
        text-decoration: line-through;
        text-decoration-color: var(--color-texto-tenue);
      }

      .antes.nulo {
        text-decoration: none;
      }

      .despues {
        font-weight: var(--peso-medio);
      }

      .mas {
        justify-self: start;
      }
    `,
  ],
})
export class Auditoria implements OnInit {
  private readonly servicio = inject(AdminService);
  private readonly i18n = inject(TranslocoService);

  protected readonly registros = signal<AuditoriaAdminDto[] | null>(null);
  protected readonly siguiente = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly errorMas = signal<string | null>(null);
  protected readonly cargandoMas = signal(false);
  protected readonly instante = instante;
  /** El nombre de cada tipo de rubro, para decir de cuál se trata aunque el cambio no tocara el nombre. */
  private readonly nombres = signal<ReadonlyMap<string, string>>(new Map());

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      const [pagina, tipos] = await Promise.all([
        this.servicio.auditoria(null),
        this.servicio.tipos(),
      ]);
      this.nombres.set(new Map(tipos.map((t) => [t.id, t.nombre])));
      this.registros.set(pagina.registros);
      this.siguiente.set(pagina.siguiente);
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  protected async mas(): Promise<void> {
    const antes = this.siguiente();
    if (!antes || this.cargandoMas()) {
      return;
    }
    this.cargandoMas.set(true);
    this.errorMas.set(null);
    try {
      const pagina = await this.servicio.auditoria(antes);
      this.registros.update((r) => [...(r ?? []), ...pagina.registros]);
      this.siguiente.set(pagina.siguiente);
    } catch (e) {
      this.errorMas.set(codigoDeError(e));
    } finally {
      this.cargandoMas.set(false);
    }
  }

  protected nombreDe(r: AuditoriaAdminDto): string | null {
    return r.entidadId ? (this.nombres().get(r.entidadId) ?? null) : null;
  }

  protected campo(nombre: string): string {
    const clave = `admin.auditoria.campos.${nombre}`;
    const texto = this.i18n.translate(clave);
    return texto === clave ? nombre : texto;
  }

  /** Cálculo y forma sugerida se guardan como claves; se leen con el mismo nombre que en Tipos de rubro. */
  protected valor(campo: string, v: string | null): string {
    const grupo = { comportamiento: 'comportamientos', formaSugerida: 'formas' }[campo];
    if (grupo && v !== null) {
      const clave = `admin.tiposRubro.${grupo}.${v}`;
      const texto = this.i18n.translate(clave);
      return texto === clave ? v : texto;
    }
    if (v === '') {
      return this.i18n.translate('admin.auditoria.vacioValor');
    }
    return valorAuditoria(
      v,
      this.i18n.translate('admin.auditoria.si'),
      this.i18n.translate('admin.auditoria.no'),
    );
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }
}
