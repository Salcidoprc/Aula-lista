import { ElementRef, Injector, afterNextRender, inject } from '@angular/core';

/**
 * Mueve el foco después del próximo render: al primer campo con error, o al encabezado o aviso que
 * reemplazó al botón que tenía el foco (WCAG 2.4.3). Se llama desde el contexto de inyección.
 */
export function crearEnfoque(): (selector: string) => void {
  const injector = inject(Injector);
  const host = inject<ElementRef<HTMLElement>>(ElementRef);
  return (selector) =>
    afterNextRender(() => host.nativeElement.querySelector<HTMLElement>(selector)?.focus(), {
      injector,
    });
}

export const PRIMER_ERROR = '[aria-invalid="true"]';
