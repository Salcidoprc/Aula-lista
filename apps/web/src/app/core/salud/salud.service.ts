import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type EstadoServicio = 'revisando' | 'enLinea' | 'sinServidor';

/** Consulta `/salud` de la API para mostrar si el servicio responde. */
@Injectable({ providedIn: 'root' })
export class SaludService {
  private readonly http = inject(HttpClient);
  private readonly estadoActual = signal<EstadoServicio>('revisando');

  readonly estado = this.estadoActual.asReadonly();

  async revisar(): Promise<void> {
    this.estadoActual.set('revisando');
    try {
      await firstValueFrom(this.http.get('/salud', { responseType: 'text' }));
      this.estadoActual.set('enLinea');
    } catch {
      this.estadoActual.set('sinServidor');
    }
  }
}
