import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { Api } from '../../api/api';
import { reenviarVerificacion } from '../../api/fn/cuentas/reenviar-verificacion';
import { codigoDeError } from '../../core/errores/problema';

/** Botón para reenviar el correo de verificación (máximo 3 por hora, lo cuida la API). */
@Component({
  selector: 'app-reenviar-verificacion',
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="reenviar" *transloco="let t">
      @switch (estado()) {
        @case ('enviado') {
          <p class="aviso aviso--exito" role="status">{{ t('cuenta.reenviar.enviado') }}</p>
        }
        @case ('limite') {
          <p class="aviso aviso--pendiente" role="status">{{ t('errores.demasiados_intentos') }}</p>
        }
        @default {
          @if (estado() === 'error') {
            <p class="aviso aviso--error" role="alert">{{ t('errores.desconocido') }}</p>
          }
          <button
            class="boton boton--secundario"
            type="button"
            [disabled]="estado() === 'enviando'"
            (click)="reenviar()"
          >
            {{ t('cuenta.reenviar.accion') }}
          </button>
        }
      }
    </div>
  `,
  styles: `
    .reenviar {
      display: grid;
      justify-items: start;
      gap: var(--espacio-3);
    }
  `,
})
export class ReenviarVerificacion {
  private readonly api = inject(Api);
  protected readonly estado = signal<'listo' | 'enviando' | 'enviado' | 'limite' | 'error'>(
    'listo',
  );

  protected async reenviar(): Promise<void> {
    this.estado.set('enviando');
    try {
      await this.api.invoke(reenviarVerificacion);
      this.estado.set('enviado');
    } catch (e) {
      this.estado.set(codigoDeError(e) === 'demasiados_intentos' ? 'limite' : 'error');
    }
  }
}
