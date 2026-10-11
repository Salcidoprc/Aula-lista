import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { Api } from '../../api/api';
import { solicitarRecuperacion } from '../../api/fn/cuentas/solicitar-recuperacion';
import { codigoDeError } from '../../core/errores/problema';

/**
 * Recuperar contraseña (RF-003). La respuesta es la misma exista o no la cuenta, para no revelar qué
 * correos están registrados.
 */
@Component({
  selector: 'app-recuperar',
  imports: [ReactiveFormsModule, RouterLink, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="pantalla" *transloco="let t">
      <h1>{{ t('cuenta.recuperar.titulo') }}</h1>
      @if (enviadoA(); as correo) {
        <div class="aviso aviso--exito" role="status">
          <p>{{ t('cuenta.recuperar.enviado', { correo }) }}</p>
          <p>{{ t('cuenta.recuperar.revisaSpam') }}</p>
        </div>
        <a class="boton boton--secundario" routerLink="/cuenta/entrar">{{
          t('cuenta.recuperar.volver')
        }}</a>
      } @else {
        <p class="entrada">{{ t('cuenta.recuperar.entrada') }}</p>
        @if (error()) {
          <p class="aviso aviso--error" role="alert">{{ t('errores.' + error()) }}</p>
        }
        <form class="formulario" [formGroup]="formulario" (ngSubmit)="enviar()" novalidate>
          <div class="campo">
            <label for="correo">{{ t('cuenta.campos.correo') }}</label>
            <input
              id="correo"
              type="email"
              formControlName="correo"
              autocomplete="email"
              inputmode="email"
              [attr.aria-invalid]="invalido()"
              [attr.aria-describedby]="invalido() ? 'correo-error' : null"
            />
            @if (invalido()) {
              <p id="correo-error" class="error">{{ t('cuenta.validacion.correo') }}</p>
            }
          </div>
          <button class="boton boton--ancho" type="submit" [disabled]="enviando()">
            {{ t('cuenta.recuperar.accion') }}
          </button>
        </form>
        <a class="enlace enlace--solo" routerLink="/cuenta/entrar">{{
          t('cuenta.recuperar.volver')
        }}</a>
      }
    </section>
  `,
})
export class Recuperar {
  private readonly api = inject(Api);

  protected readonly formulario = inject(FormBuilder).nonNullable.group({
    correo: ['', [Validators.required, Validators.email]],
  });
  protected readonly enviando = signal(false);
  protected readonly intentado = signal(false);
  protected readonly enviadoA = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  protected invalido(): boolean {
    return this.intentado() && this.formulario.invalid;
  }

  protected async enviar(): Promise<void> {
    this.intentado.set(true);
    if (this.formulario.invalid) {
      return;
    }
    this.enviando.set(true);
    this.error.set(null);
    try {
      const { correo } = this.formulario.getRawValue();
      await this.api.invoke(solicitarRecuperacion, { body: { correo } });
      this.enviadoA.set(correo);
    } catch (e) {
      this.error.set(codigoDeError(e));
    } finally {
      this.enviando.set(false);
    }
  }
}
