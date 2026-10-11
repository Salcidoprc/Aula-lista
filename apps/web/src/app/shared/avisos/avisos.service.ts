import { Injectable, signal } from '@angular/core';

export type TipoAviso = 'exito' | 'error';

export interface Aviso {
  readonly id: number;
  readonly texto: string;
  readonly tipo: TipoAviso;
}

const DURACION_MS = 4000;

/** Avisos breves al pie (guardado, enviado, error). El texto ya viene traducido. */
@Injectable({ providedIn: 'root' })
export class AvisosService {
  private readonly lista = signal<readonly Aviso[]>([]);
  private siguiente = 1;

  readonly avisos = this.lista.asReadonly();

  mostrar(texto: string, tipo: TipoAviso = 'exito'): void {
    const aviso: Aviso = { id: this.siguiente++, texto, tipo };
    this.lista.update((l) => [...l.slice(-2), aviso]);
    setTimeout(() => this.cerrar(aviso.id), DURACION_MS);
  }

  cerrar(id: number): void {
    this.lista.update((l) => l.filter((a) => a.id !== id));
  }
}
