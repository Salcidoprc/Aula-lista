import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { SwPush } from '@angular/service-worker';
import { RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { codigoDeError } from '../core/errores/problema';
import { correoValido } from '../docente/alumnos/ficha-alumno';
import { Icono } from '../shared/iconos';
import { Ocupado } from '../shared/ocupado';
import { controlarPortada } from '../shared/portada/portada.service';
import { esIos, estadoPush } from './portal';
import { PortalService } from './portal.service';

/**
 * Notificaciones del tutor (SPEC-07, RF-064): activar los avisos en este celular (Web Push) y registrar un correo.
 * En iPhone el push solo existe con Aula Lista en la pantalla de inicio (RN-066), así que primero se explica eso.
 */
@Component({
  selector: 'app-notificaciones',
  imports: [TranslocoDirective, RouterLink, Icono, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="pantalla" *transloco="let t; prefix: 'portal.notificaciones'">
      <a class="volver" routerLink="/tutor">
        <app-icono nombre="volver" />
        {{ t('volver') }}
      </a>
      <hgroup>
        <h1 tabindex="-1">{{ t('titulo') }}</h1>
        <p class="dato-suave">{{ t('subtitulo') }}</p>
      </hgroup>

      @if (errorCarga()) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ t('errorRed') }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (cargado()) {
        <section class="bloque" aria-labelledby="push-titulo">
          <h2 id="push-titulo">
            <app-icono nombre="campana" />
            {{ t('push.titulo') }}
          </h2>
          @switch (estado()) {
            @case ('activado') {
              <p class="estado estado--activo" role="status">
                <app-icono nombre="guardado" />
                {{ t('push.activadas') }}
              </p>
              <button
                class="boton boton--secundario"
                type="button"
                [appOcupado]="trabajando() === 'push'"
                (click)="desactivar()"
              >
                {{ t('push.desactivar') }}
              </button>
            }
            @case ('apagado') {
              <p>{{ t('push.texto') }}</p>
              <button
                class="boton"
                type="button"
                [appOcupado]="trabajando() === 'push'"
                (click)="activar()"
              >
                {{ t('push.activar') }}
              </button>
            }
            @case ('denegado') {
              <p>{{ t('push.denegado') }}</p>
              <p class="dato-suave">{{ t('push.denegadoComo') }}</p>
            }
            @case ('agregarInicio') {
              <p>{{ t('push.iphone') }}</p>
              <ol class="pasos">
                <li>{{ t('push.iphonePaso1') }}</li>
                <li>{{ t('push.iphonePaso2') }}</li>
                <li>{{ t('push.iphonePaso3') }}</li>
              </ol>
            }
            @default {
              <p>{{ t('push.sinSoporte') }}</p>
            }
          }
          @if (errorPush()) {
            <p class="aviso aviso--error" role="alert">{{ t('push.error') }}</p>
          }
        </section>

        <section class="bloque" aria-labelledby="correo-titulo">
          <h2 id="correo-titulo">
            <app-icono nombre="correo" />
            {{ t('correo.titulo') }}
          </h2>
          <form class="formulario" novalidate (submit)="$event.preventDefault(); guardarCorreo()">
            <div class="campo">
              <label for="correo">{{ t('correo.campo') }}</label>
              <input
                id="correo"
                type="email"
                inputmode="email"
                autocomplete="email"
                enterkeyhint="done"
                [value]="correo()"
                (input)="correo.set(valor($event)); guardado.set(false)"
                [attr.aria-invalid]="correoInvalido() ? 'true' : null"
                aria-describedby="correo-ayuda"
              />
              <p id="correo-ayuda" class="ayuda" [class.error]="correoInvalido()">
                {{ correoInvalido() ? t('correo.invalido') : t('correo.ayuda') }}
              </p>
            </div>
            @if (errorCorreo()) {
              <p class="aviso aviso--error" role="alert">{{ t('correo.error') }}</p>
            }
            <div class="acciones">
              <button class="boton" type="submit" [appOcupado]="trabajando() === 'correo'">
                {{ t('correo.guardar') }}
              </button>
              @if (guardado()) {
                <p class="estado estado--activo" role="status">
                  <app-icono nombre="guardado" />
                  {{ correo() ? t('correo.guardado') : t('correo.quitado') }}
                </p>
              }
            </div>
          </form>
        </section>
      } @else {
        <div class="esqueletos" role="status" [attr.aria-label]="t('cargando')">
          <span class="esqueleto bloque-esqueleto"></span>
          <span class="esqueleto bloque-esqueleto"></span>
        </div>
      }
    </section>
  `,
  styles: `
    .pantalla {
      max-width: calc(var(--ancho-lectura) + 2 * var(--espacio-4));
    }

    hgroup {
      display: grid;
      gap: var(--espacio-1);
    }

    .bloque {
      display: grid;
      justify-items: start;
      gap: var(--espacio-3);
      padding-block: var(--espacio-5);
      border-top: 1px solid var(--color-divisoria);
    }

    .bloque h2 {
      display: flex;
      align-items: center;
      gap: var(--espacio-2);
      font-size: var(--texto-lg);
    }

    .bloque h2 app-icono {
      color: var(--color-texto-suave);
    }

    .estado {
      display: inline-flex;
      align-items: center;
      gap: var(--espacio-2);
      font-weight: var(--peso-medio);
    }

    .estado--activo {
      color: var(--color-exito-tinta);
    }

    .pasos {
      display: grid;
      gap: var(--espacio-2);
      margin: 0;
      padding-inline-start: var(--espacio-5);
    }

    .formulario {
      display: grid;
      gap: var(--espacio-3);
      inline-size: 100%;
    }

    .acciones {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--espacio-3);
    }

    .esqueletos {
      display: grid;
      gap: var(--espacio-3);
    }

    .bloque-esqueleto {
      display: block;
      block-size: 9rem;
    }
  `,
})
export class Notificaciones implements OnInit {
  private readonly portal = inject(PortalService);
  private readonly swPush = inject(SwPush);
  private readonly portada = controlarPortada();
  private readonly i18n = inject(TranslocoService);

  private readonly suscripcion = toSignal(this.swPush.subscription, { initialValue: null });
  private readonly permiso = signal<NotificationPermission | null>(
    typeof Notification === 'undefined' ? null : Notification.permission,
  );
  private clavePush = '';

  protected readonly cargado = signal(false);
  protected readonly errorCarga = signal(false);
  protected readonly errorPush = signal<string | null>(null);
  protected readonly errorCorreo = signal<string | null>(null);
  protected readonly trabajando = signal<'push' | 'correo' | null>(null);
  protected readonly correo = signal('');
  protected readonly intentado = signal(false);
  protected readonly guardado = signal(false);

  protected readonly correoInvalido = computed(
    () => this.intentado() && !correoValido(this.correo()),
  );
  protected readonly estado = computed(() =>
    estadoPush({
      soportado: this.swPush.isEnabled && 'PushManager' in window,
      ios: esIos(navigator.userAgent, navigator.platform, navigator.maxTouchPoints),
      instalada:
        matchMedia('(display-mode: standalone)').matches ||
        (navigator as Navigator & { standalone?: boolean }).standalone === true,
      permiso: this.permiso(),
      suscrito: this.suscripcion() !== null,
    }),
  );

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.errorCarga.set(false);
    try {
      const contacto = await this.portal.contacto();
      this.clavePush = contacto.clavePush;
      this.correo.set(contacto.correo ?? '');
      this.portada({ titulo: this.i18n.translate('portal.notificaciones.titulo') });
      this.cargado.set(true);
    } catch (error) {
      if (!this.portal.manejarSinSesion(error)) {
        this.errorCarga.set(true);
      }
    }
  }

  protected valor(evento: Event): string {
    return (evento.target as HTMLInputElement).value;
  }

  /** El navegador pregunta el permiso aquí, en respuesta al toque; nunca al abrir la pantalla. */
  protected async activar(): Promise<void> {
    this.errorPush.set(null);
    this.trabajando.set('push');
    try {
      const suscripcion = await this.swPush.requestSubscription({
        serverPublicKey: this.clavePush,
      });
      await this.portal.suscribirPush(suscripcion.toJSON());
    } catch (error) {
      if (!this.portal.manejarSinSesion(error)) {
        this.errorPush.set(codigoDeError(error));
      }
    } finally {
      this.permiso.set(typeof Notification === 'undefined' ? null : Notification.permission);
      this.trabajando.set(null);
    }
  }

  protected async desactivar(): Promise<void> {
    this.errorPush.set(null);
    this.trabajando.set('push');
    try {
      const actual = await firstValueFrom(this.swPush.subscription);
      if (actual) {
        await this.portal.desuscribirPush(actual.endpoint);
      }
      await this.swPush.unsubscribe();
    } catch (error) {
      if (!this.portal.manejarSinSesion(error)) {
        this.errorPush.set(codigoDeError(error));
      }
    } finally {
      this.trabajando.set(null);
    }
  }

  protected async guardarCorreo(): Promise<void> {
    this.intentado.set(true);
    this.errorCorreo.set(null);
    this.guardado.set(false);
    if (!correoValido(this.correo())) {
      document.getElementById('correo')?.focus();
      return;
    }
    this.trabajando.set('correo');
    try {
      const contacto = await this.portal.guardarCorreo(this.correo());
      this.correo.set(contacto.correo ?? '');
      this.guardado.set(true);
    } catch (error) {
      if (!this.portal.manejarSinSesion(error)) {
        this.errorCorreo.set(codigoDeError(error));
      }
    } finally {
      this.trabajando.set(null);
    }
  }
}
