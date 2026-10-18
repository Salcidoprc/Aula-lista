import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { PRIMER_ERROR, crearEnfoque } from '../../shared/foco';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { Api } from '../../api/api';
import { cambiarContrasena } from '../../api/fn/cuentas/cambiar-contrasena';
import { solicitarEliminacion } from '../../api/fn/docente/solicitar-eliminacion';
import { solicitarExportacion } from '../../api/fn/docente/solicitar-exportacion';
import { ActualizarPerfil } from '../../api/models/actualizar-perfil';
import { camposConError, codigoDeError } from '../../core/errores/problema';
import { PerfilService } from '../../core/sesion/perfil.service';
import { SesionService } from '../../core/sesion/sesion.service';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { BotonGoogle } from '../../shared/boton-google';
import { FormularioPerfil } from '../cuenta/formulario-perfil';
import { ReenviarVerificacion } from '../cuenta/reenviar-verificacion';
import { Ocupado } from '../../shared/ocupado';

const LONGITUD_MINIMA = 10;

/**
 * SPEC-01, pantalla 3: perfil, correo, contraseña, sesiones, exportar datos (RF-007) y eliminar la cuenta (RF-008).
 * También la entrada al estilo de las observaciones (SPEC-04, RF-043).
 */
@Component({
  selector: 'app-ajustes',
  imports: [
    ReactiveFormsModule,
    TranslocoDirective,
    BotonGoogle,
    FormularioPerfil,
    ReenviarVerificacion,
    Ocupado,
    RouterLink,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pantalla pantalla--ancha" *transloco="let t; prefix: 'ajustes'">
      <h1>{{ t('titulo') }}</h1>

      @if (!perfil()) {
        <p class="aviso" role="status">{{ t('cargando') }}</p>
      } @else {
        <div class="columnas columnas--pares">
          <div class="columna">
            <section aria-labelledby="ajustes-perfil">
              <h2 id="ajustes-perfil">{{ t('perfil.titulo') }}</h2>
              <app-formulario-perfil
                [perfil]="perfil()"
                [accion]="t('perfil.guardar')"
                [enviando]="guardandoPerfil()"
                (guardar)="guardarPerfil($event)"
              />
              @if (errorPerfil(); as codigo) {
                <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
              }
            </section>

            <section aria-labelledby="ajustes-correo">
              <h2 id="ajustes-correo">{{ t('correo.titulo') }}</h2>
              <p class="dato">{{ perfil()!.correo }}</p>
              @if (perfil()!.correoVerificado) {
                <p class="aviso aviso--exito">{{ t('correo.verificado') }}</p>
              } @else {
                <p class="aviso aviso--pendiente">{{ t('correo.pendiente') }}</p>
                <app-reenviar-verificacion />
              }
            </section>
          </div>

          <div class="columna">
            <section aria-labelledby="ajustes-contrasena">
              <h2 id="ajustes-contrasena">
                {{ perfil()!.tieneContrasena ? t('contrasena.titulo') : t('contrasena.definir') }}
              </h2>
              @if (!perfil()!.tieneContrasena) {
                <p class="nota">{{ t('contrasena.soloGoogle') }}</p>
              }
              <form
                class="formulario"
                [formGroup]="contrasena"
                (ngSubmit)="guardarContrasena()"
                novalidate
              >
                @if (perfil()!.tieneContrasena) {
                  <div class="campo">
                    <label for="actual">{{ t('contrasena.actual') }}</label>
                    <input
                      id="actual"
                      type="password"
                      formControlName="actual"
                      autocomplete="current-password"
                      [attr.aria-invalid]="errorContrasena() === 'credenciales_invalidas'"
                    />
                  </div>
                }
                <div class="campo">
                  <label for="nueva">{{ t('contrasena.nueva') }}</label>
                  <input
                    id="nueva"
                    type="password"
                    formControlName="nueva"
                    autocomplete="new-password"
                    [attr.aria-invalid]="contrasenaInvalida()"
                    [attr.aria-describedby]="
                      contrasenaInvalida() ? 'nueva-error nueva-ayuda' : 'nueva-ayuda'
                    "
                  />
                  <p id="nueva-ayuda" class="ayuda">
                    {{ t('contrasena.ayuda', { minimo: minimo }) }}
                  </p>
                  @if (contrasenaInvalida()) {
                    <p id="nueva-error" class="error">
                      {{ t('contrasena.corta', { minimo: minimo }) }}
                    </p>
                  }
                </div>
                @if (errorContrasena(); as codigo) {
                  <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
                }
                <button
                  class="boton boton--secundario"
                  type="submit"
                  [appOcupado]="guardandoContrasena()"
                >
                  {{ t('contrasena.guardar') }}
                </button>
              </form>
            </section>

            <section aria-labelledby="ajustes-sesiones">
              <h2 id="ajustes-sesiones">{{ t('sesiones.titulo') }}</h2>
              <p class="nota">{{ t('sesiones.nota') }}</p>
              <div class="acciones">
                <button class="boton boton--secundario" type="button" (click)="cerrarSesion(false)">
                  {{ t('sesiones.cerrar') }}
                </button>
                <button
                  class="boton boton--secundario"
                  type="button"
                  (click)="dialogoSesiones.showModal()"
                >
                  {{ t('sesiones.cerrarTodas') }}
                </button>
              </div>
            </section>

            <section aria-labelledby="ajustes-observaciones">
              <h2 id="ajustes-observaciones">{{ t('observaciones.titulo') }}</h2>
              <p class="nota">{{ t('observaciones.nota') }}</p>
              <a class="boton boton--secundario" routerLink="/ajustes/estilo">
                {{ t('observaciones.accion') }}
              </a>
            </section>

            <section aria-labelledby="ajustes-datos">
              <h2 id="ajustes-datos">{{ t('datos.titulo') }}</h2>
              <p class="nota">{{ t('datos.nota') }}</p>
              <button
                class="boton boton--secundario"
                type="button"
                [appOcupado]="exportando()"
                (click)="exportar()"
              >
                {{ t('datos.exportar') }}
              </button>
            </section>

            <section aria-labelledby="ajustes-eliminar">
              <h2 id="ajustes-eliminar">{{ t('eliminar.titulo') }}</h2>
              <p class="nota">{{ t('eliminar.nota') }}</p>
              <button
                class="boton boton--secundario peligro"
                type="button"
                (click)="abrirEliminar()"
              >
                {{ t('eliminar.accion') }}
              </button>
            </section>
          </div>
        </div>
      }

      <dialog
        #dialogoSesiones
        class="dialogo"
        closedby="any"
        aria-labelledby="dialogo-sesiones-titulo"
      >
        <div class="contenido">
          <h2 id="dialogo-sesiones-titulo">{{ t('sesiones.confirmarTitulo') }}</h2>
          <p>{{ t('sesiones.confirmarTexto') }}</p>
          <div class="acciones">
            <button class="boton boton--secundario" type="button" (click)="dialogoSesiones.close()">
              {{ t('cancelar') }}
            </button>
            <button class="boton" type="button" (click)="cerrarSesion(true)">
              {{ t('sesiones.cerrarTodas') }}
            </button>
          </div>
        </div>
      </dialog>

      <dialog
        #dialogoEliminar
        class="dialogo"
        closedby="any"
        aria-labelledby="dialogo-eliminar-titulo"
      >
        <form
          class="contenido"
          [formGroup]="eliminacion"
          (ngSubmit)="confirmarEliminacion()"
          novalidate
        >
          <h2 id="dialogo-eliminar-titulo">{{ t('eliminar.confirmarTitulo') }}</h2>
          <ul class="consecuencias">
            <li>{{ t('eliminar.consecuencia1') }}</li>
            <li>{{ t('eliminar.consecuencia2') }}</li>
            <li>{{ t('eliminar.consecuencia3') }}</li>
          </ul>
          @if (perfil()?.tieneContrasena) {
            <div class="campo">
              <label for="contrasena-eliminar">{{ t('eliminar.contrasena') }}</label>
              <input
                id="contrasena-eliminar"
                type="password"
                formControlName="contrasena"
                autocomplete="current-password"
                [attr.aria-invalid]="errorEliminar() === 'reautenticacion_requerida'"
              />
            </div>
          } @else if (!confirmadoConGoogle()) {
            <p>{{ t('eliminar.google') }}</p>
            <app-boton-google [texto]="t('eliminar.googleAccion')" volver="/ajustes?eliminar=1" />
          }
          @if (errorEliminar(); as codigo) {
            <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
          }
          <div class="acciones">
            <button class="boton boton--secundario" type="button" (click)="dialogoEliminar.close()">
              {{ t('cancelar') }}
            </button>
            @if (perfil()?.tieneContrasena || confirmadoConGoogle()) {
              <button class="boton boton--peligro" type="submit" [appOcupado]="eliminando()">
                {{ t('eliminar.confirmar') }}
              </button>
            }
          </div>
        </form>
      </dialog>
    </div>
  `,
  styles: `
    section {
      display: grid;
      justify-items: start;
      gap: var(--espacio-3);
      padding-top: var(--espacio-5);
      border-top: 1px solid var(--color-divisoria);
    }

    section > app-formulario-perfil,
    section > .formulario,
    section > .aviso {
      justify-self: stretch;
    }

    section > .formulario > .boton--secundario {
      justify-self: start;
    }

    h2 {
      font-size: var(--texto-lg);
    }

    .nota {
      max-width: var(--ancho-lectura);
      color: var(--color-texto-suave);
    }

    .dato {
      font-weight: var(--peso-medio);
      overflow-wrap: anywhere;
    }

    .acciones {
      display: flex;
      flex-wrap: wrap;
      gap: var(--espacio-2);
    }

    .peligro {
      color: var(--color-peligro-tinta);
    }

    .consecuencias {
      display: grid;
      gap: var(--espacio-2);
      margin: 0;
      padding-inline-start: var(--espacio-5);
    }
  `,
})
export class Ajustes implements OnInit {
  private readonly enfocar = crearEnfoque();
  private readonly api = inject(Api);
  private readonly servicio = inject(PerfilService);
  private readonly sesion = inject(SesionService);
  private readonly avisos = inject(AvisosService);
  private readonly i18n = inject(TranslocoService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder).nonNullable;

  /** `?eliminar=1`: regresa de confirmar con Google para eliminar la cuenta. */
  readonly eliminar = input<string>();

  protected readonly dialogoSesiones =
    viewChild.required<ElementRef<HTMLDialogElement>>('dialogoSesiones');
  protected readonly dialogoEliminar =
    viewChild.required<ElementRef<HTMLDialogElement>>('dialogoEliminar');

  protected readonly minimo = LONGITUD_MINIMA;
  protected readonly perfil = this.servicio.perfil;
  protected readonly reautenticado = computed(
    () => this.eliminar() === '1' && this.perfil()?.conGoogle === true,
  );

  protected readonly contrasena = this.fb.group({
    actual: [''],
    nueva: ['', [Validators.required, Validators.minLength(LONGITUD_MINIMA)]],
  });
  protected readonly eliminacion = this.fb.group({ contrasena: [''] });

  protected readonly guardandoPerfil = signal(false);
  protected readonly errorPerfil = signal<string | null>(null);
  /** Regresó de confirmar con Google; se guarda porque el parámetro se quita de la URL. */
  protected readonly confirmadoConGoogle = signal(false);
  private readonly formularioPerfil = viewChild(FormularioPerfil);
  protected readonly guardandoContrasena = signal(false);
  protected readonly intentoContrasena = signal(false);
  protected readonly errorContrasena = signal<string | null>(null);
  protected readonly exportando = signal(false);
  protected readonly eliminando = signal(false);
  protected readonly errorEliminar = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    await this.servicio.cargar(true);
    if (this.reautenticado()) {
      this.confirmadoConGoogle.set(true);
      this.abrirEliminar();
      // Que recargar la página no vuelva a abrir el diálogo.
      void this.router.navigate([], { queryParams: { eliminar: null }, replaceUrl: true });
    }
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  protected contrasenaInvalida(): boolean {
    return this.intentoContrasena() && this.contrasena.controls.nueva.invalid;
  }

  protected async guardarPerfil(cambios: ActualizarPerfil): Promise<void> {
    this.guardandoPerfil.set(true);
    this.errorPerfil.set(null);
    try {
      await this.servicio.guardar(cambios);
      this.avisos.mostrar(this.texto('ajustes.perfil.guardado'));
    } catch (e) {
      this.errorPerfil.set(codigoDeError(e));
      this.formularioPerfil()?.marcarErroresDelServidor(camposConError(e));
    } finally {
      this.guardandoPerfil.set(false);
    }
  }

  protected async guardarContrasena(): Promise<void> {
    this.intentoContrasena.set(true);
    if (this.contrasena.invalid) {
      this.enfocar(PRIMER_ERROR);
      return;
    }
    const { actual, nueva } = this.contrasena.getRawValue();
    this.guardandoContrasena.set(true);
    this.errorContrasena.set(null);
    try {
      await this.api.invoke(cambiarContrasena, { body: { actual: actual || null, nueva } });
      this.contrasena.reset();
      this.intentoContrasena.set(false);
      await this.servicio.cargar(true);
      this.avisos.mostrar(this.texto('ajustes.contrasena.guardada'));
    } catch (e) {
      this.errorContrasena.set(codigoDeError(e));
    } finally {
      this.guardandoContrasena.set(false);
    }
  }

  protected async cerrarSesion(todas: boolean): Promise<void> {
    this.dialogoSesiones().nativeElement.close();
    try {
      await this.sesion.cerrar(todas);
    } finally {
      this.servicio.olvidar();
      await this.router.navigateByUrl('/cuenta/entrar');
    }
  }

  protected async exportar(): Promise<void> {
    this.exportando.set(true);
    try {
      const respuesta = await this.api.invoke(solicitarExportacion);
      await this.router.navigate(['/ajustes/exportaciones', respuesta.id]);
    } catch (e) {
      this.avisos.mostrar(this.texto('errores.' + codigoDeError(e)), 'error');
    } finally {
      this.exportando.set(false);
    }
  }

  protected abrirEliminar(): void {
    this.errorEliminar.set(null);
    this.eliminacion.reset();
    this.dialogoEliminar().nativeElement.showModal();
  }

  protected async confirmarEliminacion(): Promise<void> {
    this.eliminando.set(true);
    this.errorEliminar.set(null);
    try {
      const contrasena = this.eliminacion.getRawValue().contrasena;
      await this.api.invoke(solicitarEliminacion, { body: { contrasena: contrasena || null } });
      this.sesion.olvidar();
      this.servicio.olvidar();
      this.dialogoEliminar().nativeElement.close();
      await this.router.navigate(['/cuenta/eliminada']);
    } catch (e) {
      this.errorEliminar.set(codigoDeError(e));
    } finally {
      this.eliminando.set(false);
    }
  }
}
