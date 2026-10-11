import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { PerfilService } from '../../core/sesion/perfil.service';
import { ReenviarVerificacion } from '../cuenta/reenviar-verificacion';

/**
 * Inicio del docente. Por ahora solo recuerda verificar el correo (RN-001); la lista "Mis grupos"
 * llega con SPEC-02.
 */
@Component({
  selector: 'app-inicio',
  imports: [TranslocoDirective, ReenviarVerificacion],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="pantalla pantalla--ancha" *transloco="let t; prefix: 'inicio'">
      <hgroup>
        <h1>{{ t('titulo') }}</h1>
        @if (perfil()?.nombre; as nombre) {
          <p class="entrada">{{ t('saludo', { nombre }) }}</p>
        }
      </hgroup>

      @if (perfil() && !perfil()!.correoVerificado) {
        <div class="aviso aviso--pendiente">
          <p>
            <strong>{{ t('verifica.titulo') }}</strong>
            {{ t('verifica.texto', { correo: perfil()!.correo }) }}
          </p>
          <app-reenviar-verificacion />
        </div>
      }

      <div class="vacio">
        <p class="vacio-titulo">{{ t('vacio.titulo') }}</p>
        <p>{{ t('vacio.texto') }}</p>
      </div>
    </section>
  `,
  styles: `
    hgroup {
      display: grid;
      gap: var(--espacio-2);
    }

    .vacio {
      display: grid;
      gap: var(--espacio-2);
      padding-block: var(--espacio-6);
      color: var(--color-texto-suave);
    }

    .vacio-titulo {
      color: var(--color-texto);
      font-size: var(--texto-lg);
      font-weight: var(--peso-fuerte);
    }
  `,
})
export class Inicio {
  protected readonly perfil = inject(PerfilService).perfil;
}
