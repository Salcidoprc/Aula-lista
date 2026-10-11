import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { PRIMER_ERROR, crearEnfoque } from '../../shared/foco';
import { TranslocoDirective } from '@jsverse/transloco';
import { Api } from '../../api/api';
import { restablecerContrasena } from '../../api/fn/cuentas/restablecer-contrasena';
import { codigoDeError } from '../../core/errores/problema';
import { SesionService } from '../../core/sesion/sesion.service';

const LONGITUD_MINIMA = 10;

/** Elegir contraseña nueva desde el enlace del correo (RF-003). Cierra las demás sesiones. */
@Component({
  selector: 'app-restablecer',
  imports: [ReactiveFormsModule, RouterLink, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="pantalla" *transloco="let t">
      @if (listo()) {
        <h1 tabindex="-1">{{ t('cuenta.restablecer.listo') }}</h1>
        <p class="entrada">{{ t('cuenta.restablecer.listoTexto') }}</p>
        <a class="boton boton--ancho" routerLink="/cuenta/entrar">{{
          t('cuenta.restablecer.entrar')
        }}</a>
      } @else {
        <h1>{{ t('cuenta.restablecer.titulo') }}</h1>
        @if (error(); as codigo) {
          <div class="aviso aviso--error" role="alert">
            <p>{{ t('errores.' + codigo) }}</p>
            @if (codigo === 'token_vencido' || codigo === 'token_invalido') {
              <a class="enlace" routerLink="/cuenta/recuperar">{{
                t('cuenta.restablecer.pedirOtro')
              }}</a>
            }
          </div>
        }
        <form class="formulario" [formGroup]="formulario" (ngSubmit)="guardar()" novalidate>
          <div class="campo">
            <label for="contrasena">{{ t('cuenta.campos.contrasenaNueva') }}</label>
            <input
              id="contrasena"
              type="password"
              formControlName="contrasena"
              autocomplete="new-password"
              [attr.aria-invalid]="invalido()"
              [attr.aria-describedby]="
                invalido() ? 'contrasena-error contrasena-ayuda' : 'contrasena-ayuda'
              "
            />
            <p id="contrasena-ayuda" class="ayuda">
              {{ t('cuenta.ayudaContrasena', { minimo: minimo }) }}
            </p>
            @if (invalido()) {
              <p id="contrasena-error" class="error">
                {{ t('cuenta.validacion.contrasena', { minimo: minimo }) }}
              </p>
            }
          </div>
          <button class="boton boton--ancho" type="submit" [disabled]="enviando()">
            {{ t('cuenta.restablecer.accion') }}
          </button>
        </form>
      }
    </section>
  `,
})
export class Restablecer {
  private readonly enfocar = crearEnfoque();
  private readonly api = inject(Api);
  private readonly sesion = inject(SesionService);

  readonly token = input<string>();

  protected readonly minimo = LONGITUD_MINIMA;
  protected readonly formulario = inject(FormBuilder).nonNullable.group({
    contrasena: ['', [Validators.required, Validators.minLength(LONGITUD_MINIMA)]],
  });
  protected readonly enviando = signal(false);
  protected readonly intentado = signal(false);
  protected readonly listo = signal(false);
  protected readonly error = signal<string | null>(null);

  protected invalido(): boolean {
    return this.intentado() && this.formulario.invalid;
  }

  protected async guardar(): Promise<void> {
    this.intentado.set(true);
    const token = this.token();
    if (!token) {
      this.error.set('token_invalido');
      return;
    }
    if (this.formulario.invalid) {
      this.enfocar(PRIMER_ERROR);
      return;
    }
    this.enviando.set(true);
    this.error.set(null);
    try {
      await this.api.invoke(restablecerContrasena, {
        body: { token, contrasena: this.formulario.getRawValue().contrasena },
      });
      // La API cerró todas las sesiones de la cuenta, también la de este dispositivo.
      this.sesion.olvidar();
      this.listo.set(true);
      this.enfocar('h1');
    } catch (e) {
      this.error.set(codigoDeError(e));
    } finally {
      this.enviando.set(false);
    }
  }
}
