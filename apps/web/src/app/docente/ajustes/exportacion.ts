import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  input,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { Api } from '../../api/api';
import { obtenerExportacion } from '../../api/fn/docente/obtener-exportacion';
import { RespuestaExportacion } from '../../api/models/respuesta-exportacion';
import { codigoDeError } from '../../core/errores/problema';
import { Ocupado } from '../../shared/ocupado';

const ESPERA_MS = 3000;
const MAXIMO_CONSULTAS = 40;

type Formato = 'excel' | 'pdf';

/**
 * Descarga de la exportación de datos (RF-007). Mientras se genera, consulta su estado; también es
 * la página a la que lleva el enlace del correo. Los archivos se piden con la sesión del docente.
 */
@Component({
  selector: 'app-exportacion',
  imports: [DatePipe, RouterLink, TranslocoDirective, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="pantalla" *transloco="let t; prefix: 'ajustes.exportacion'">
      <h1>{{ t('titulo') }}</h1>
      @switch (estado()) {
        @case ('pendiente') {
          <p class="aviso" role="status">{{ t('generando') }}</p>
          <p class="entrada">{{ t('generandoNota') }}</p>
        }
        @case ('lista') {
          <p class="entrada">
            {{ t('lista') }}
            <span class="cifras">{{ exportacion()?.expiraEn | date: 'dd/MM/yyyy, HH:mm' }}</span
            >.
          </p>
          <div class="descargas">
            <button
              class="boton"
              type="button"
              [appOcupado]="descargando() !== null"
              (click)="descargar('excel')"
            >
              {{ t('excel') }}
            </button>
            <button
              class="boton boton--secundario"
              type="button"
              [appOcupado]="descargando() !== null"
              (click)="descargar('pdf')"
            >
              {{ t('pdf') }}
            </button>
          </div>
          @if (errorDescarga()) {
            <p class="aviso aviso--error" role="alert">{{ t('errorDescarga') }}</p>
          }
        }
        @case ('lenta') {
          <p class="aviso aviso--pendiente" role="status">{{ t('lenta') }}</p>
        }
        @case ('fallida') {
          <p class="aviso aviso--error" role="alert">{{ t('fallida') }}</p>
        }
        @default {
          <p class="aviso aviso--error" role="alert">{{ t('noDisponible') }}</p>
        }
      }
      <a class="enlace enlace--solo" routerLink="/ajustes">{{ t('volver') }}</a>
    </section>
  `,
  styles: `
    .descargas {
      display: flex;
      flex-wrap: wrap;
      gap: var(--espacio-2);
    }
  `,
})
export class Exportacion implements OnInit {
  private readonly api = inject(Api);
  private readonly http = inject(HttpClient);
  private readonly destruida = signal(false);

  readonly id = input.required<string>();

  protected readonly exportacion = signal<RespuestaExportacion | null>(null);
  protected readonly estado = signal<string>('pendiente');
  protected readonly descargando = signal<Formato | null>(null);
  protected readonly errorDescarga = signal(false);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.destruida.set(true));
  }

  async ngOnInit(): Promise<void> {
    for (let consulta = 0; consulta < MAXIMO_CONSULTAS && !this.destruida(); consulta++) {
      try {
        const e = await this.api.invoke(obtenerExportacion, { id: this.id() });
        this.exportacion.set(e);
        this.estado.set(e.estado);
        if (e.estado !== 'pendiente') {
          return;
        }
      } catch (e) {
        this.estado.set(codigoDeError(e) === 'sin_conexion' ? 'pendiente' : 'no_disponible');
        if (this.estado() !== 'pendiente') {
          return;
        }
      }
      await new Promise((r) => setTimeout(r, ESPERA_MS));
    }
    if (this.estado() === 'pendiente' && !this.destruida()) {
      this.estado.set('lenta');
    }
  }

  protected async descargar(formato: Formato): Promise<void> {
    this.descargando.set(formato);
    this.errorDescarga.set(false);
    try {
      const respuesta = await firstValueFrom(
        this.http.get(`/api/v1/docente/exportaciones/${encodeURIComponent(this.id())}/${formato}`, {
          observe: 'response',
          responseType: 'blob',
        }),
      );
      const nombre = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(
        respuesta.headers.get('Content-Disposition') ?? '',
      )?.[1];
      const url = URL.createObjectURL(respuesta.body!);
      const enlace = document.createElement('a');
      enlace.href = url;
      enlace.download = nombre
        ? decodeURIComponent(nombre)
        : `aula-lista-mis-datos.${formato === 'excel' ? 'xlsx' : 'pdf'}`;
      enlace.click();
      // Algunos navegadores (Safari en iOS) leen el archivo después del clic.
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch {
      this.errorDescarga.set(true);
    } finally {
      this.descargando.set(null);
    }
  }
}
