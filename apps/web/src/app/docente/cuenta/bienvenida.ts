import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { ActualizarPerfil } from '../../api/models/actualizar-perfil';
import { camposConError, codigoDeError } from '../../core/errores/problema';
import { PerfilService } from '../../core/sesion/perfil.service';
import { FormularioPerfil } from './formulario-perfil';
import { Ocupado } from '../../shared/ocupado';

/**
 * SPEC-01, pantalla 2: perfil y aceptación legal en un solo paso (RF-004, RN-003). Si el perfil ya
 * está completo y solo cambió la versión de un documento, pide únicamente la nueva aceptación.
 */
@Component({
  selector: 'app-bienvenida',
  imports: [NgTemplateOutlet, RouterLink, TranslocoDirective, FormularioPerfil, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="pantalla" *transloco="let t">
      @if (!perfil() && error()) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ t('errores.' + error()) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('comun.reintentar') }}
          </button>
        </div>
      } @else if (!perfil()) {
        <p class="aviso" role="status">{{ t('comun.cargando') }}</p>
      } @else {
        <hgroup>
          <h1>
            {{ soloLegal() ? t('cuenta.bienvenida.tituloLegal') : t('cuenta.bienvenida.titulo') }}
          </h1>
          <p class="entrada">
            {{ soloLegal() ? t('cuenta.bienvenida.entradaLegal') : t('cuenta.bienvenida.entrada') }}
          </p>
        </hgroup>

        @if (error()) {
          <p class="aviso aviso--error" role="alert">{{ t('errores.' + error()) }}</p>
        }

        <ng-template #legal>
          @if (pendientes().length > 0) {
            <label class="casilla">
              <input
                type="checkbox"
                [checked]="acepta()"
                (change)="acepta.set(!acepta())"
                [attr.aria-invalid]="intentado() && !acepta()"
                [attr.aria-describedby]="intentado() && !acepta() ? 'legal-error' : null"
              />
              <span>
                {{ t('cuenta.bienvenida.acepto') }}
                @for (d of pendientes(); track d.documento; let ultimo = $last) {
                  <a class="enlace" [routerLink]="'/legal/' + d.documento" target="_blank">{{
                    t('legal.' + d.documento + '.titulo')
                  }}</a>
                  <span class="cifras">
                    ({{ t('cuenta.bienvenida.version', { version: d.version }) }})</span
                  >{{ ultimo ? '' : ',' }}
                }
              </span>
            </label>
            @if (intentado() && !acepta()) {
              <p id="legal-error" class="error">{{ t('errores.aceptacion_requerida') }}</p>
            }
          }
        </ng-template>

        @if (soloLegal()) {
          <div class="formulario">
            <ng-container *ngTemplateOutlet="legal" />
            <button
              class="boton boton--ancho"
              type="button"
              [appOcupado]="enviando()"
              (click)="continuar(null)"
            >
              {{ t('cuenta.bienvenida.aceptar') }}
            </button>
          </div>
        } @else {
          <app-formulario-perfil
            [perfil]="perfil()"
            [accion]="t('cuenta.bienvenida.accion')"
            [enviando]="enviando()"
            [valido]="pendientes().length === 0 || acepta()"
            (guardar)="continuar($event)"
            (intento)="intentado.set(true)"
          >
            <ng-container *ngTemplateOutlet="legal" />
          </app-formulario-perfil>
        }
      }
    </section>
  `,
  styles: `
    hgroup {
      display: grid;
      gap: var(--espacio-2);
    }
  `,
})
export class Bienvenida implements OnInit {
  private readonly servicio = inject(PerfilService);
  private readonly router = inject(Router);

  private readonly formularioPerfil = viewChild(FormularioPerfil);
  protected readonly perfil = this.servicio.perfil;
  protected readonly pendientes = computed(() => this.perfil()?.aceptacionesPendientes ?? []);
  protected readonly soloLegal = computed(() => this.perfil()?.perfilCompleto === true);
  protected readonly acepta = signal(false);
  protected readonly intentado = signal(false);
  protected readonly enviando = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): Promise<void> {
    return this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    const perfil = await this.servicio.cargar(true);
    if (perfil === null) {
      this.error.set('sin_conexion');
    } else if (perfil.perfilCompleto && perfil.aceptacionesPendientes.length === 0) {
      await this.router.navigateByUrl('/');
    }
  }

  protected async continuar(cambios: ActualizarPerfil | null): Promise<void> {
    this.intentado.set(true);
    if (this.pendientes().length > 0 && !this.acepta()) {
      return;
    }
    this.enviando.set(true);
    this.error.set(null);
    try {
      await this.servicio.aceptar(this.pendientes());
      if (cambios) {
        await this.servicio.guardar(cambios);
      }
      await this.router.navigateByUrl('/');
    } catch (e) {
      this.error.set(codigoDeError(e));
      this.formularioPerfil()?.marcarErroresDelServidor(camposConError(e));
    } finally {
      this.enviando.set(false);
    }
  }
}
