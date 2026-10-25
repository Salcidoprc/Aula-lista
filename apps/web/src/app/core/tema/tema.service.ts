import { DOCUMENT } from '@angular/common';
import { Injectable, computed, effect, inject, signal } from '@angular/core';

export type PreferenciaTema = 'sistema' | 'claro' | 'oscuro';
export type Tema = 'claro' | 'oscuro';

export const PREFERENCIAS_TEMA: readonly PreferenciaTema[] = ['sistema', 'claro', 'oscuro'];
const CLAVE = 'aula-lista.tema';

/**
 * RF-009: tema claro u oscuro. Por defecto sigue al sistema; la elección se guarda en este
 * dispositivo y vale para toda la app (docente, tutores y administración). El tema efectivo se
 * marca con data-tema en <html> y tokens.css hace el resto.
 */
@Injectable({ providedIn: 'root' })
export class TemaService {
  private readonly documento = inject(DOCUMENT);
  private readonly consulta = this.documento.defaultView?.matchMedia?.(
    '(prefers-color-scheme: dark)',
  );
  private readonly sistemaOscuro = signal(this.consulta?.matches ?? false);
  private readonly elegida = signal<PreferenciaTema>(leer());

  readonly preferencia = this.elegida.asReadonly();
  readonly tema = computed<Tema>(() => {
    const p = this.elegida();
    return p === 'sistema' ? (this.sistemaOscuro() ? 'oscuro' : 'claro') : p;
  });

  constructor() {
    this.consulta?.addEventListener('change', (e) => this.sistemaOscuro.set(e.matches));
    effect(() => this.documento.documentElement.setAttribute('data-tema', this.tema()));
  }

  elegir(preferencia: PreferenciaTema): void {
    this.elegida.set(preferencia);
    try {
      if (preferencia === 'sistema') {
        localStorage.removeItem(CLAVE);
      } else {
        localStorage.setItem(CLAVE, preferencia);
      }
    } catch {
      // Sin almacenamiento (navegación privada): la elección dura lo que la pestaña.
    }
  }
}

function leer(): PreferenciaTema {
  try {
    const guardada = localStorage.getItem(CLAVE);
    return guardada === 'claro' || guardada === 'oscuro' ? guardada : 'sistema';
  } catch {
    return 'sistema';
  }
}
