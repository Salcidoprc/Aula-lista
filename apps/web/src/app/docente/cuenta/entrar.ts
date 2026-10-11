import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { PRIMER_ERROR, crearEnfoque } from '../../shared/foco';
import { TranslocoDirective } from '@jsverse/transloco';
import { Api } from '../../api/api';
import { iniciarSesion } from '../../api/fn/cuentas/iniciar-sesion';
import { codigoDeError } from '../../core/errores/problema';
import { SesionService } from '../../core/sesion/sesion.service';
import { BotonGoogle } from '../../shared/boton-google';
import { rutaLocal } from '../../shared/rutas';
import { Ocupado } from '../../shared/ocupado';

const ERRORES_GOOGLE = ['google', 'google_no_disponible', 'google_sin_correo_verificado'];

/** SPEC-01, pantalla 1: entrar con correo y contraseña o con Google. */
@Component({
  selector: 'app-entrar',
  imports: [ReactiveFormsModule, RouterLink, TranslocoDirective, BotonGoogle, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="pantalla" *transloco="let t">
      <hgroup>
        <h1>{{ t('cuenta.entrar.titulo') }}</h1>
        <p class="entrada">{{ t('cuenta.entrar.entrada') }}</p>
      </hgroup>

      @if (mensajeError()) {
        <p class="aviso aviso--error" role="alert">{{ t('errores.' + mensajeError()) }}</p>
      }

      <app-boton-google [texto]="t('cuenta.google')" [volver]="destino()" />
      <p class="separador">{{ t('cuenta.oCorreo') }}</p>

      <form class="formulario" [formGroup]="formulario" (ngSubmit)="entrar()" novalidate>
        <div class="campo">
          <label for="correo">{{ t('cuenta.campos.correo') }}</label>
          <input
            id="correo"
            type="email"
            formControlName="correo"
            autocomplete="username"
            inputmode="email"
            [attr.aria-invalid]="invalido('correo')"
            [attr.aria-describedby]="invalido('correo') ? 'correo-error' : null"
          />
          @if (invalido('correo')) {
            <p id="correo-error" class="error">{{ t('cuenta.validacion.correo') }}</p>
          }
        </div>
        <div class="campo">
          <label for="contrasena">{{ t('cuenta.campos.contrasena') }}</label>
          <input
            id="contrasena"
            type="password"
            formControlName="contrasena"
            autocomplete="current-password"
            [attr.aria-invalid]="invalido('contrasena')"
            [attr.aria-describedby]="invalido('contrasena') ? 'contrasena-error' : null"
          />
          @if (invalido('contrasena')) {
            <p id="contrasena-error" class="error">{{ t('cuenta.validacion.requerida') }}</p>
          }
        </div>
        <button class="boton boton--ancho" type="submit" [appOcupado]="enviando()">
          {{ enviando() ? t('cuenta.entrar.enviando') : t('cuenta.entrar.accion') }}
        </button>
      </form>

      <div class="pie">
        <a class="enlace enlace--solo" routerLink="/cuenta/recuperar">{{
          t('cuenta.entrar.olvide')
        }}</a>
        <p>
          {{ t('cuenta.entrar.sinCuenta') }}
          <a
            class="enlace"
            routerLink="/cuenta/registro"
            [queryParams]="volver() ? { volver: volver() } : {}"
            >{{ t('cuenta.entrar.crear') }}</a
          >
        </p>
      </div>
    </section>
  `,
  styles: `
    hgroup {
      display: grid;
      gap: var(--espacio-2);
    }

    .pie {
      display: grid;
      gap: var(--espacio-2);
      color: var(--color-texto-suave);
    }
  `,
})
export class Entrar {
  private readonly enfocar = crearEnfoque();
  private readonly api = inject(Api);
  private readonly sesion = inject(SesionService);
  private readonly router = inject(Router);

  /** Ruta a la que se regresa después de entrar (`?volver=`). */
  readonly volver = input<string>();
  /** Error que trae Google al regresar (`?error=`). */
  readonly error = input<string>();

  protected readonly formulario = inject(FormBuilder).nonNullable.group({
    correo: ['', [Validators.required, Validators.email]],
    contrasena: ['', Validators.required],
  });
  protected readonly enviando = signal(false);
  protected readonly enviado = signal(false);
  private readonly errorEnvio = signal<string | null>(null);

  protected mensajeError(): string | null {
    const externo = this.error();
    // Solo se muestran los errores que Google puede traer; cualquier otro valor del enlace se ignora.
    const deGoogle =
      externo && ERRORES_GOOGLE.includes(externo) ? externo : externo ? 'google' : null;
    return this.errorEnvio() ?? deGoogle;
  }

  protected destino(): string {
    return rutaLocal(this.volver());
  }

  protected invalido(campo: 'correo' | 'contrasena'): boolean {
    return this.enviado() && this.formulario.controls[campo].invalid;
  }

  protected async entrar(): Promise<void> {
    this.enviado.set(true);
    if (this.formulario.invalid) {
      this.enfocar(PRIMER_ERROR);
      return;
    }
    this.enviando.set(true);
    this.errorEnvio.set(null);
    try {
      this.sesion.establecer(
        await this.api.invoke(iniciarSesion, { body: this.formulario.getRawValue() }),
      );
      await this.router.navigateByUrl(this.destino());
    } catch (e) {
      this.errorEnvio.set(codigoDeError(e));
    } finally {
      this.enviando.set(false);
    }
  }
}
