import { Injectable, inject, signal } from '@angular/core';
import { Api } from '../../api/api';
import { actualizarPerfil } from '../../api/fn/docente/actualizar-perfil';
import { obtenerPerfil } from '../../api/fn/docente/obtener-perfil';
import { registrarAceptacion } from '../../api/fn/docente/registrar-aceptacion';
import { ActualizarPerfil } from '../../api/models/actualizar-perfil';
import { DocumentoPendiente } from '../../api/models/documento-pendiente';
import { PerfilDocente } from '../../api/models/perfil-docente';

/** Perfil del docente con sesión (RF-004), compartido por la bienvenida, el inicio y los ajustes. */
@Injectable({ providedIn: 'root' })
export class PerfilService {
  private readonly api = inject(Api);
  private readonly actual = signal<PerfilDocente | null>(null);

  readonly perfil = this.actual.asReadonly();

  /** Lee el perfil una vez por sesión; `forzar` lo vuelve a pedir. Devuelve null si la API no respondió. */
  async cargar(forzar = false): Promise<PerfilDocente | null> {
    if (this.actual() && !forzar) {
      return this.actual();
    }
    try {
      this.actual.set(await this.api.invoke(obtenerPerfil));
    } catch {
      return this.actual();
    }
    return this.actual();
  }

  async guardar(cambios: ActualizarPerfil): Promise<void> {
    await this.api.invoke(actualizarPerfil, { body: cambios });
    await this.cargar(true);
  }

  async aceptar(documentos: readonly DocumentoPendiente[]): Promise<void> {
    for (const d of documentos) {
      await this.api.invoke(registrarAceptacion, {
        body: { documento: d.documento, version: d.version },
      });
    }
    await this.cargar(true);
  }

  olvidar(): void {
    this.actual.set(null);
  }
}
