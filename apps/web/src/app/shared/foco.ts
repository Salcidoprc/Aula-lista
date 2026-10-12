import { ElementRef, Injector, afterNextRender, inject } from '@angular/core';

/**
 * Mueve el foco después del próximo render: al primer campo con error, o al encabezado o aviso que
 * reemplazó al botón que tenía el foco (WCAG 2.4.3). Se llama desde el contexto de inyección.
 */
export function crearEnfoque(): (selector: string) => void {
  const injector = inject(Injector);
  const host = inject<ElementRef<HTMLElement>>(ElementRef);
  return (selector) =>
    afterNextRender(() => visibleOPrimero(host.nativeElement, selector)?.focus(), { injector });
}

/*
 * Con varios candidatos (la acción del celular y la del escritorio), el primero que se ve; si el entorno no
 * mide cajas, el primero del documento.
 */
function visibleOPrimero(raiz: HTMLElement, selector: string): HTMLElement | null {
  const candidatos = [...raiz.querySelectorAll<HTMLElement>(selector)];
  return candidatos.find((el) => el.getClientRects().length > 0) ?? candidatos[0] ?? null;
}

export const PRIMER_ERROR = '[aria-invalid="true"]';
