import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { Api } from '../../api/api';
import { verificarCorreo } from '../../api/fn/cuentas/verificar-correo';
import { codigoDeError } from '../../core/errores/problema';
import { PerfilService } from '../../core/sesion/perfil.service';
import { SesionService } from '../../core/sesion/sesion.service';
import { ReenviarVerificacion } from './reenviar-verificacion';

type Estado = 'verificando' | 'verificado' | 'token_vencido' | 'token_invalido' | 'error';

/** Enlace del correo de verificación (RF-001, RN-001). Si venció, ofrece reenviar (caso borde de SPEC-01). */
@Component({
  selector: 'app-verificar',
  imports: [RouterLink, TranslocoDirective, ReenviarVerificacion],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="pantalla" *transloco="let t; prefix: 'cuenta.verificar'" aria-live="polite">
      @switch (estado()) {
        @case ('verificando') {
          <h1>{{ t('verificando') }}</h1>
        }
        @case ('verificado') {
          <h1>{{ t('listo') }}</h1>
          <p class="entrada">{{ t('listoTexto') }}</p>
          <a class="boton boton--ancho" routerLink="/">{{ t('continuar') }}</a>
        }
        @default {
          <h1>{{ t('fallo') }}</h1>
          <p class="aviso aviso--error" role="alert">{{ t('motivo.' + estado()) }}</p>
          @if (conSesion()) {
            <app-reenviar-verificacion />
          } @else {
            <p class="entrada">{{ t('entraParaReenviar') }}</p>
            <a
              class="boton boton--ancho"
              routerLink="/cuenta/entrar"
              [queryParams]="{ volver: '/ajustes' }"
              >{{ t('entrar') }}</a
            >
          }
        }
      }
    </section>
  `,
})
export class Verificar implements OnInit {
  private readonly api = inject(Api);
  private readonly sesion = inject(SesionService);
  private readonly perfil = inject(PerfilService);

  readonly token = input<string>();

  protected readonly estado = signal<Estado>('verificando');
  protected readonly conSesion = signal(false);

  async ngOnInit(): Promise<void> {
    const token = this.token();
    if (!token) {
      this.estado.set('token_invalido');
    } else {
      try {
        await this.api.invoke(verificarCorreo, { body: { token } });
        this.estado.set('verificado');
      } catch (e) {
        const codigo = codigoDeError(e);
        this.estado.set(
          codigo === 'token_vencido' || codigo === 'token_invalido' ? codigo : 'error',
        );
      }
    }
    this.conSesion.set(await this.sesion.asegurar());
    if (this.conSesion() && this.estado() === 'verificado') {
      await this.perfil.cargar(true);
    }
  }
}
