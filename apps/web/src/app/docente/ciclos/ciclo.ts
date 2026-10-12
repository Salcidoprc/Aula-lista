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
import { Api } from '../../api/api';
import { crearCiclo } from '../../api/fn/ciclos/crear-ciclo';
import { editarCiclo } from '../../api/fn/ciclos/editar-ciclo';
import { obtenerCiclo } from '../../api/fn/ciclos/obtener-ciclo';
import { reemplazarUnidades } from '../../api/fn/ciclos/reemplazar-unidades';
import { CicloDto } from '../../api/models/ciclo-dto';
import { codigoDeError } from '../../core/errores/problema';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { PRIMER_ERROR, crearEnfoque } from '../../shared/foco';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { rutaLocal } from '../../shared/rutas';
import { GruposService } from '../grupos/grupos.service';
import { LineaTiempo } from './linea-tiempo';
import {
  Fecha,
  MAXIMO_UNIDADES,
  TIPOS_UNIDAD,
  TipoUnidad,
  UnidadEnEdicion,
  cantidadPropuesta,
  deDiaNumero,
  diaNumero,
  esFecha,
  proponerUnidades,
  validarCiclo,
  validarUnidades,
} from './unidades';

/** Ciclo que propone la primera vez: agosto a julio, como el calendario escolar de la SEP. */
function cicloPorDefecto(hoy = new Date()): { nombre: string; inicio: Fecha; fin: Fecha } {
  const anio = hoy.getMonth() >= 6 ? hoy.getFullYear() : hoy.getFullYear() - 1;
  return { nombre: `${anio}-${anio + 1}`, inicio: `${anio}-08-25`, fin: `${anio + 1}-07-16` };
}

/**
 * SPEC-02, "Ciclo escolar": nombre, fechas, tipo de unidad y cantidad, con las unidades propuestas sobre una
 * línea de tiempo y sus fechas editables (RN-022 a RN-027). Sin `id` crea el ciclo; con `id` lo edita.
 */
@Component({
  selector: 'app-ciclo',
  imports: [TranslocoDirective, RouterLink, LineaTiempo, Icono, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pantalla pantalla--ancha" *transloco="let t; prefix: 'ciclo'">
      <a class="volver" [routerLink]="regreso()">
        <app-icono nombre="volver" />
        {{ volver() ? t('volver') : t('misGrupos') }}
      </a>
      <hgroup>
        <h1>{{ id() ? t('tituloEditar') : t('titulo') }}</h1>
        @if (!id()) {
          <p class="entrada">{{ t('entrada') }}</p>
        }
      </hgroup>

      @if (cargando()) {
        <p class="aviso" role="status">{{ t('cargando') }}</p>
      } @else {
        <form class="formulario" (submit)="$event.preventDefault(); guardar()" novalidate>
          <div class="campo">
            <label for="ciclo-nombre">{{ t('nombre') }}</label>
            <input
              id="ciclo-nombre"
              name="nombre"
              maxlength="20"
              autocomplete="off"
              [value]="nombre()"
              (input)="nombre.set(valor($event))"
              [attr.aria-invalid]="intento() && !nombre().trim()"
            />
            @if (intento() && !nombre().trim()) {
              <p class="error">{{ t('nombreRequerido') }}</p>
            }
          </div>

          <div class="campos-en-fila">
            <div class="campo">
              <label for="ciclo-inicio">{{ t('inicio') }}</label>
              <input
                id="ciclo-inicio"
                type="date"
                name="inicio"
                [value]="inicio()"
                (change)="cambiarFechas(valor($event), fin())"
                [attr.aria-invalid]="intento() && !!errorCiclo()"
                [attr.aria-describedby]="intento() && errorCiclo() ? 'ciclo-error' : null"
              />
            </div>
            <div class="campo">
              <label for="ciclo-fin">{{ t('fin') }}</label>
              <input
                id="ciclo-fin"
                type="date"
                name="fin"
                [value]="fin()"
                (change)="cambiarFechas(inicio(), valor($event))"
                [attr.aria-invalid]="intento() && !!errorCiclo()"
                [attr.aria-describedby]="intento() && errorCiclo() ? 'ciclo-error' : null"
              />
            </div>
          </div>
          @if (intento() && errorCiclo(); as codigo) {
            <p id="ciclo-error" class="error">{{ texto('errores.' + codigo) }}</p>
          }

          @if (!id()) {
            <div class="campos-en-fila">
              <div class="campo">
                <label for="ciclo-tipo">{{ t('tipo') }}</label>
                <div class="selector">
                  <select id="ciclo-tipo" name="tipo" (change)="cambiarTipo(valor($event))">
                    @for (opcion of tipos; track opcion) {
                      <option [value]="opcion" [selected]="opcion === tipo()">
                        {{ t('tipos.' + opcion) }}
                      </option>
                    }
                  </select>
                </div>
              </div>
              <div class="campo">
                <label for="ciclo-cantidad">{{ t('cantidad') }}</label>
                <div class="selector">
                  <select
                    id="ciclo-cantidad"
                    name="cantidad"
                    class="cifras"
                    (change)="cambiarCantidad(+valor($event))"
                  >
                    @for (n of cantidades; track n) {
                      <option [value]="n" [selected]="n === unidades().length">{{ n }}</option>
                    }
                  </select>
                </div>
              </div>
            </div>
          }

          <section class="unidades" aria-labelledby="unidades-titulo">
            <div>
              <h2 id="unidades-titulo">{{ t('unidades') }}</h2>
              <p class="dato-suave">{{ t('unidadesNota') }}</p>
            </div>
            <app-linea-tiempo
              [inicio]="inicio()"
              [fin]="fin()"
              [unidades]="unidades()"
              [errores]="erroresUnidades()"
            />
            <ol class="filas">
              @for (unidad of unidades(); track $index; let i = $index) {
                <li class="unidad">
                  <span class="orden cifras" aria-hidden="true">{{ i + 1 }}</span>
                  <fieldset class="datos-unidad">
                    <legend class="solo-lector">{{ t('unidadN', { n: i + 1 }) }}</legend>
                    <div class="campo">
                      <label [for]="'unidad-nombre-' + i">{{ t('nombreUnidad') }}</label>
                      <input
                        [id]="'unidad-nombre-' + i"
                        [name]="'unidad-nombre-' + i"
                        maxlength="40"
                        autocomplete="off"
                        [value]="unidad.nombre"
                        (input)="editar(i, { nombre: valor($event) })"
                        [attr.aria-invalid]="intento() && !unidad.nombre.trim()"
                      />
                    </div>
                    <div class="campos-en-fila">
                      <div class="campo">
                        <label [for]="'unidad-inicio-' + i">{{ t('desde') }}</label>
                        <input
                          [id]="'unidad-inicio-' + i"
                          type="date"
                          [name]="'unidad-inicio-' + i"
                          [min]="inicio()"
                          [max]="fin()"
                          [value]="unidad.inicio"
                          (change)="editar(i, { inicio: valor($event) })"
                          [attr.aria-invalid]="intento() && !!erroresUnidades()[i]"
                          [attr.aria-describedby]="
                            intento() && erroresUnidades()[i] ? 'unidad-error-' + i : null
                          "
                        />
                      </div>
                      <div class="campo">
                        <label [for]="'unidad-fin-' + i">{{ t('hasta') }}</label>
                        <input
                          [id]="'unidad-fin-' + i"
                          type="date"
                          [name]="'unidad-fin-' + i"
                          [min]="inicio()"
                          [max]="fin()"
                          [value]="unidad.fin"
                          (change)="editar(i, { fin: valor($event) })"
                          [attr.aria-invalid]="intento() && !!erroresUnidades()[i]"
                          [attr.aria-describedby]="
                            intento() && erroresUnidades()[i] ? 'unidad-error-' + i : null
                          "
                        />
                      </div>
                    </div>
                    @if (intento() && erroresUnidades()[i]; as codigo) {
                      <p class="error" [id]="'unidad-error-' + i">
                        {{ texto('errores.' + codigo) }}
                      </p>
                    }
                  </fieldset>
                  @if (unidades().length > 1) {
                    <button
                      class="quitar"
                      type="button"
                      [attr.aria-label]="
                        t('quitar', { nombre: unidad.nombre || t('unidadN', { n: i + 1 }) })
                      "
                      (click)="quitar(i)"
                    >
                      <app-icono nombre="cerrar" />
                    </button>
                  }
                </li>
              }
            </ol>
            @if (unidades().length < maximo) {
              <button
                class="boton boton--secundario agregar"
                type="button"
                (click)="agregarAlFinal()"
              >
                <app-icono nombre="mas" />
                {{ t('agregar') }}
              </button>
            }
          </section>

          @if (error(); as codigo) {
            <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
          }

          <div class="pie-accion">
            <button class="boton" type="submit" [appOcupado]="guardando()">
              {{ id() ? t('guardarCambios') : t('guardar') }}
            </button>
          </div>
        </form>
      }
    </div>
  `,
  styles: `
    hgroup {
      display: grid;
      gap: var(--espacio-2);
    }

    h2 {
      font-size: var(--texto-lg);
    }

    .unidades {
      display: grid;
      gap: var(--espacio-3);
      padding-top: var(--espacio-4);
      border-top: 1px solid var(--color-divisoria);
    }

    .unidad {
      display: grid;
      grid-template-columns: 2rem 1fr auto;
      align-items: start;
      gap: var(--espacio-3);
      padding-block: var(--espacio-4);
    }

    .orden {
      display: grid;
      place-items: center;
      width: 2rem;
      height: 2rem;
      margin-top: 1.625rem;
      border-radius: var(--radio-md);
      background: var(--grado-tinte);
      color: var(--grado-tinta);
      font-weight: var(--peso-fuerte);
    }

    .datos-unidad {
      display: grid;
      gap: var(--espacio-3);
      min-width: 0;
      margin: 0;
      padding: 0;
      border: 0;
    }

    .quitar {
      display: grid;
      place-items: center;
      width: var(--tactil-minimo);
      height: var(--tactil-minimo);
      margin-top: 1.25rem;
      border: 0;
      border-radius: var(--radio-lg);
      background: none;
      color: var(--color-texto-suave);
      cursor: pointer;
    }

    .quitar:active {
      background: var(--color-superficie-hundida);
    }

    @media (hover: hover) and (pointer: fine) {
      .quitar:hover {
        background: var(--color-superficie-hundida);
        color: var(--color-peligro-tinta);
      }
    }

    .agregar {
      justify-self: start;
    }
  `,
})
export class Ciclo implements OnInit {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly avisos = inject(AvisosService);
  private readonly i18n = inject(TranslocoService);
  private readonly grupos = inject(GruposService);
  private readonly enfocar = crearEnfoque();

  /** Ciclo a editar (ruta `/ciclos/:id`). */
  readonly id = input<string>();
  /** A dónde regresar al guardar, por ejemplo a crear el grupo que lo pidió. */
  readonly volver = input<string>();

  protected readonly tipos = TIPOS_UNIDAD;
  protected readonly maximo = MAXIMO_UNIDADES;
  protected readonly cantidades = Array.from({ length: MAXIMO_UNIDADES }, (_, i) => i + 1);

  private readonly porDefecto = cicloPorDefecto();
  protected readonly nombre = signal(this.porDefecto.nombre);
  protected readonly inicio = signal<Fecha>(this.porDefecto.inicio);
  protected readonly fin = signal<Fecha>(this.porDefecto.fin);
  protected readonly tipo = signal<TipoUnidad>('trimestre');
  protected readonly unidades = signal<readonly UnidadEnEdicion[]>(
    proponerUnidades(
      this.porDefecto.inicio,
      this.porDefecto.fin,
      'trimestre',
      cantidadPropuesta(this.porDefecto.inicio, this.porDefecto.fin, 'trimestre'),
    ),
  );

  private original: CicloDto | null = null;
  protected readonly cargando = signal(false);
  protected readonly intento = signal(false);
  protected readonly guardando = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly errorCiclo = computed(() => validarCiclo(this.inicio(), this.fin()));
  protected readonly erroresUnidades = computed(() =>
    this.errorCiclo() ? [] : validarUnidades(this.inicio(), this.fin(), this.unidades()),
  );
  protected readonly regreso = computed(() => (this.volver() ? rutaLocal(this.volver()) : '/'));

  async ngOnInit(): Promise<void> {
    const id = this.id();
    if (!id) {
      return;
    }
    this.cargando.set(true);
    try {
      this.cargar(await this.api.invoke(obtenerCiclo, { id }));
    } catch (e) {
      this.error.set(codigoDeError(e));
    } finally {
      this.cargando.set(false);
    }
  }

  protected valor(evento: Event): string {
    return (evento.target as HTMLInputElement | HTMLSelectElement).value;
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  /** Al crear, cambiar fechas, tipo o cantidad vuelve a repartir las unidades; al editar, no las toca. */
  protected cambiarFechas(inicio: string, fin: string): void {
    this.inicio.set(inicio);
    this.fin.set(fin);
    if (!this.id() && !validarCiclo(inicio, fin)) {
      this.unidades.set(
        proponerUnidades(inicio, fin, this.tipo(), cantidadPropuesta(inicio, fin, this.tipo())),
      );
    }
  }

  protected cambiarTipo(tipo: string): void {
    const nuevo = tipo as TipoUnidad;
    this.tipo.set(nuevo);
    if (!this.errorCiclo()) {
      this.unidades.set(
        proponerUnidades(
          this.inicio(),
          this.fin(),
          nuevo,
          cantidadPropuesta(this.inicio(), this.fin(), nuevo),
        ),
      );
    }
  }

  protected cambiarCantidad(cantidad: number): void {
    if (!this.errorCiclo()) {
      this.unidades.set(proponerUnidades(this.inicio(), this.fin(), this.tipo(), cantidad));
    }
  }

  protected editar(indice: number, cambios: Partial<UnidadEnEdicion>): void {
    this.unidades.update((lista) => lista.map((u, i) => (i === indice ? { ...u, ...cambios } : u)));
  }

  protected quitar(indice: number): void {
    this.unidades.update((lista) => lista.filter((_, i) => i !== indice));
  }

  /** RN-027: la nueva empieza el día después de la última y llega al fin del ciclo. */
  protected agregarAlFinal(): void {
    const lista = this.unidades();
    const ultima = lista.at(-1);
    const inicio =
      ultima && esFecha(ultima.fin) ? deDiaNumero(diaNumero(ultima.fin) + 1) : this.inicio();
    const numero = lista.length + 1;
    this.unidades.set([
      ...lista,
      {
        id: null,
        nombre: this.i18n.translate('ciclo.unidadN', { n: numero }),
        inicio,
        fin: this.fin(),
      },
    ]);
    this.enfocar(`#unidad-nombre-${lista.length}`);
  }

  protected async guardar(): Promise<void> {
    this.intento.set(true);
    this.error.set(null);
    if (!this.nombre().trim() || this.errorCiclo() || this.erroresUnidades().some((e) => e)) {
      this.enfocar(PRIMER_ERROR);
      return;
    }
    this.guardando.set(true);
    try {
      const id = this.id();
      if (id) {
        await this.guardarCambios(id);
      } else {
        await this.crear();
      }
      await this.grupos.cargarCiclos();
      this.avisos.mostrar(this.texto('ciclo.guardado'));
      await this.router.navigateByUrl(this.regreso());
    } catch (e) {
      this.error.set(codigoDeError(e));
    } finally {
      this.guardando.set(false);
    }
  }

  private async crear(): Promise<void> {
    const creado = await this.api.invoke(crearCiclo, {
      body: {
        nombre: this.nombre().trim(),
        inicio: this.inicio(),
        fin: this.fin(),
        tipoUnidad: this.tipo(),
        cantidadUnidades: this.unidades().length,
      },
    });
    // Si el docente editó lo propuesto, se guardan sus nombres y fechas sobre las unidades creadas.
    const propias = this.unidades();
    const iguales = creado.unidades.every(
      (u, i) =>
        u.nombre === propias[i]?.nombre &&
        u.inicio === propias[i]?.inicio &&
        u.fin === propias[i]?.fin,
    );
    if (!iguales) {
      await this.api.invoke(reemplazarUnidades, {
        id: creado.id,
        body: { unidades: propias.map((u, i) => ({ ...u, id: creado.unidades[i]?.id ?? null })) },
      });
    }
  }

  /** Si el ciclo crece, primero el ciclo y luego las unidades; si se acorta, al revés (RN-023). */
  private async guardarCambios(id: string): Promise<void> {
    const original = this.original!;
    const datos = { nombre: this.nombre().trim(), inicio: this.inicio(), fin: this.fin() };
    const cambioCiclo =
      datos.nombre !== original.nombre ||
      datos.inicio !== original.inicio ||
      datos.fin !== original.fin;
    const unidades = this.unidades();
    const cambioUnidades =
      unidades.length !== original.unidades.length ||
      unidades.some((u, i) => {
        const o = original.unidades[i];
        return u.id !== o.id || u.nombre !== o.nombre || u.inicio !== o.inicio || u.fin !== o.fin;
      });
    const crece = datos.inicio <= original.inicio && datos.fin >= original.fin;
    const ciclo = () => this.api.invoke(editarCiclo, { id, body: datos });
    const lista = () =>
      this.api.invoke(reemplazarUnidades, { id, body: { unidades: [...unidades] } });
    const pasos = crece
      ? [cambioCiclo && ciclo, cambioUnidades && lista]
      : [cambioUnidades && lista, cambioCiclo && ciclo];
    for (const paso of pasos) {
      if (paso) {
        await paso();
      }
    }
  }

  private cargar(ciclo: CicloDto): void {
    const unidades = [...ciclo.unidades].sort((a, b) => Number(a.orden) - Number(b.orden));
    this.original = { ...ciclo, unidades };
    this.nombre.set(ciclo.nombre);
    this.inicio.set(ciclo.inicio);
    this.fin.set(ciclo.fin);
    this.tipo.set(ciclo.tipoUnidad as TipoUnidad);
    this.unidades.set(
      unidades.map((u) => ({ id: u.id, nombre: u.nombre, inicio: u.inicio, fin: u.fin })),
    );
  }
}
