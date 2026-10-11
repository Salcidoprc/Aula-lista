import { DestroyRef, Directive, ElementRef, inject, input } from '@angular/core';

/**
 * Botón ocupado mientras se envía. A diferencia de `disabled`, el botón sigue en el orden del
 * teclado y el lector de pantalla lo anuncia como no disponible y ocupado (WCAG 4.1.2). El clic,
 * incluido el envío implícito del formulario con Enter, se descarta antes que cualquier otro manejador.
 */
@Directive({
  selector: 'button[appOcupado]',
  host: {
    '[attr.aria-disabled]': 'appOcupado() || null',
    '[attr.aria-busy]': 'appOcupado() || null',
  },
})
export class Ocupado {
  readonly appOcupado = input(false);

  constructor() {
    const boton = inject<ElementRef<HTMLButtonElement>>(ElementRef).nativeElement;
    const descartar = (evento: Event): void => {
      if (this.appOcupado()) {
        evento.preventDefault();
        evento.stopImmediatePropagation();
      }
    };
    // En fase de captura, para correr antes que el (click) del componente y que el envío del formulario.
    boton.addEventListener('click', descartar, { capture: true });
    inject(DestroyRef).onDestroy(() =>
      boton.removeEventListener('click', descartar, { capture: true }),
    );
  }
}
