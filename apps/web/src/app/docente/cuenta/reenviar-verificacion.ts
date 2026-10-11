import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { Api } from '../../api/api';
import { reenviarVerificacion } from '../../api/fn/cuentas/reenviar-verificacion';
import { codigoDeError } from '../../core/errores/problema';

const MENSAJES: Partial<Record<string, string>> = {
  enviado: 'cuenta.reenviar.enviado',
  limite: 'errores.demasiados_intentos',
  error: 'errores.desconocido',
};

/** Botón para reenviar el correo de verificación (máximo 3 por hora, lo cuida la API). */
@Component({
  selector: 'app-reenviar-verificacion',
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="reenviar" *transloco="let t">
      <p class="aviso" role="status" [attr.data-estado]="estado()" [hidden]="!mensaje()">
        {{ mensaje() ? t(mensaje()!) : '' }}
      </p>
      @if (estado() !== 'enviado' && estado() !== 'limite') {
        <button
          class="boton boton--secundario"
          type="button"
          [disabled]="estado() === 'enviando'"
          (click)="reenviar()"
        >
          {{ t('cuenta.reenviar.accion') }}
        </button>
      }
    </div>
  `,
  styles: `
    /* La región de estado siempre existe para que el lector de pantalla anuncie el cambio. */
    .aviso[data-estado='enviado'] {
      background: var(--color-exito-tinte);
      color: var(--color-exito-tinta);
    }

    .aviso[data-estado='limite'] {
      background: var(--color-aviso-tinte);
      color: var(--color-aviso-tinta);
    }

    .aviso[data-estado='error'] {
      background: var(--color-peligro-tinte);
      color: var(--color-peligro-tinta);
    }

    .reenviar {
      display: grid;
      justify-items: start;
      gap: var(--espacio-3);
    }
  `,
})
export class ReenviarVerificacion {
  private readonly api = inject(Api);
  protected readonly mensaje = computed(() => MENSAJES[this.estado()] ?? null);
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
