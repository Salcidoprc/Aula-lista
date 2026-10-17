import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { ClaveGrado } from './portada';

export interface DatosPortada {
  readonly titulo: string;
  readonly subtitulo?: string;
  readonly numero?: string;
  readonly grado?: ClaveGrado;
}

/**
 * Qué muestra la portada de la app. Fuera de un grupo, el nombre de la app en azul; dentro de un grupo,
 * su nombre, su color y su número (DESIGN.md, "La regla de la portada").
 */
@Injectable({ providedIn: 'root' })
export class PortadaService {
  private readonly actual = signal<DatosPortada | null>(null);

  readonly datos = this.actual.asReadonly();

  fijar(datos: DatosPortada | null): void {
    this.actual.set(datos);
  }
}

/**
 * Para las pantallas de un grupo: devuelve con qué pintar la portada y la regresa a la de la app al salir.
 * Se llama en el contexto de inyección del componente.
 */
export function controlarPortada(): (datos: DatosPortada) => void {
  const servicio = inject(PortadaService);
  let propios: DatosPortada | null = null;
  inject(DestroyRef).onDestroy(() => {
    if (servicio.datos() === propios) {
      servicio.fijar(null);
    }
  });
  return (datos) => {
    propios = datos;
    servicio.fijar(datos);
  };
}
