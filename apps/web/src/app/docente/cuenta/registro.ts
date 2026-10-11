import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { Api } from '../../api/api';
import { registrar } from '../../api/fn/cuentas/registrar';
import { codigoDeError } from '../../core/errores/problema';
import { SesionService } from '../../core/sesion/sesion.service';
import { BotonGoogle } from '../../shared/boton-google';
import { rutaLocal } from '../../shared/rutas';

/** Mínimo de RN-002; la lista de contraseñas comunes la revisa la API. */
const LONGITUD_MINIMA = 10;

/** SPEC-01, pantalla 1: crear cuenta con correo (RF-001) o con Google (RF-002), aceptando lo legal (RN-003). */
@Component({
  selector: 'app-registro',
  imports: [ReactiveFormsModule, RouterLink, TranslocoDirective, BotonGoogle],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="pantalla" *transloco="let t">
      <hgroup>
        <h1>{{ t('cuenta.registro.titulo') }}</h1>
        <p class="entrada">{{ t('cuenta.registro.entrada') }}</p>
      </hgroup>

      @if (error()) {
        <p class="aviso aviso--error" role="alert">
          {{ t('errores.' + error()) }}
          @if (error() === 'correo_registrado') {
            <a class="enlace" routerLink="/cuenta/entrar">{{ t('cuenta.registro.irAEntrar') }}</a>
          }
        </p>
      }

      <app-boton-google [texto]="t('cuenta.google')" [volver]="destino()" />
      <p class="separador">{{ t('cuenta.oCorreo') }}</p>

      <form class="formulario" [formGroup]="formulario" (ngSubmit)="crear()" novalidate>
        <div class="campo">
          <label for="correo">{{ t('cuenta.campos.correo') }}</label>
          <input
            id="correo"
            type="email"
            formControlName="correo"
            autocomplete="email"
            inputmode="email"
            [attr.aria-invalid]="invalido('correo')"
            [attr.aria-describedby]="invalido('correo') ? 'correo-error' : null"
          />
          @if (invalido('correo')) {
            <p id="correo-error" class="error">{{ t('cuenta.validacion.correo') }}</p>
          }
        </div>
        <div class="campo">
          <label for="contrasena">{{ t('cuenta.campos.contrasenaNueva') }}</label>
          <input
            id="contrasena"
            type="password"
            formControlName="contrasena"
            autocomplete="new-password"
            [attr.aria-invalid]="invalido('contrasena')"
            [attr.aria-describedby]="
              invalido('contrasena') ? 'contrasena-error contrasena-ayuda' : 'contrasena-ayuda'
            "
          />
          <p id="contrasena-ayuda" class="ayuda">
            {{ t('cuenta.ayudaContrasena', { minimo: minimo }) }}
          </p>
          @if (invalido('contrasena')) {
            <p id="contrasena-error" class="error">
              {{ t('cuenta.validacion.contrasena', { minimo: minimo }) }}
            </p>
          }
        </div>

        <label class="casilla">
          <input
            type="checkbox"
            formControlName="aceptaLegal"
            [attr.aria-invalid]="invalido('aceptaLegal')"
            [attr.aria-describedby]="invalido('aceptaLegal') ? 'legal-error' : null"
          />
          <span>
            {{ t('cuenta.registro.acepto') }}
            <a class="enlace" routerLink="/legal/terminos" target="_blank">{{
              t('legal.terminos.titulo')
            }}</a>
            {{ t('cuenta.registro.y') }}
            <a class="enlace" routerLink="/legal/privacidad" target="_blank">{{
              t('legal.privacidad.titulo')
            }}</a>
          </span>
        </label>
        @if (invalido('aceptaLegal')) {
          <p id="legal-error" class="error">{{ t('errores.aceptacion_requerida') }}</p>
        }

        <button class="boton boton--ancho" type="submit" [disabled]="enviando()">
          {{ enviando() ? t('cuenta.registro.enviando') : t('cuenta.registro.accion') }}
        </button>
      </form>

      <p class="pie">
        {{ t('cuenta.registro.conCuenta') }}
        <a class="enlace" routerLink="/cuenta/entrar">{{ t('cuenta.registro.entrar') }}</a>
      </p>
    </section>
  `,
  styles: `
    hgroup {
      display: grid;
      gap: var(--espacio-2);
    }

    .pie {
      color: var(--color-texto-suave);
    }
  `,
})
export class Registro {
  private readonly api = inject(Api);
  private readonly sesion = inject(SesionService);
  private readonly router = inject(Router);

  readonly volver = input<string>();

  protected readonly minimo = LONGITUD_MINIMA;
  protected readonly formulario = inject(FormBuilder).nonNullable.group({
    correo: ['', [Validators.required, Validators.email]],
    contrasena: ['', [Validators.required, Validators.minLength(LONGITUD_MINIMA)]],
    aceptaLegal: [false, Validators.requiredTrue],
  });
  protected readonly enviando = signal(false);
  protected readonly enviado = signal(false);
  protected readonly error = signal<string | null>(null);

  protected destino(): string {
    return rutaLocal(this.volver());
  }

  protected invalido(campo: 'correo' | 'contrasena' | 'aceptaLegal'): boolean {
    return this.enviado() && this.formulario.controls[campo].invalid;
  }

  protected async crear(): Promise<void> {
    this.enviado.set(true);
    if (this.formulario.invalid) {
      return;
    }
    const { correo, contrasena, aceptaLegal } = this.formulario.getRawValue();
    this.enviando.set(true);
    this.error.set(null);
    try {
      const sesion = await this.api.invoke(registrar, {
        body: { correo, contrasena, aceptaTerminos: aceptaLegal, aceptaPrivacidad: aceptaLegal },
      });
      this.sesion.establecer(sesion);
      await this.router.navigateByUrl('/bienvenida');
    } catch (e) {
      this.error.set(codigoDeError(e));
    } finally {
      this.enviando.set(false);
    }
  }
}
