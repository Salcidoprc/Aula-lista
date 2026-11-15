import { afterNextRender, DestroyRef, Directive, ElementRef, inject } from '@angular/core';

/**
 * Landing (RF-010): marca el elemento con data-aparece y, la primera vez que entra en pantalla, con data-visto.
 * Los estilos de src/styles/revela.css hacen subir a sus hijos .revela. Solo ocurre una vez por visita.
 */
@Directive({ selector: '[appAparece]', host: { 'data-aparece': '' } })
export class Aparece {
  constructor() {
    const el = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    const destroy = inject(DestroyRef);
    afterNextRender(() => {
      if (typeof IntersectionObserver === 'undefined') {
        el.setAttribute('data-visto', '');
        return;
      }
      const observador = new IntersectionObserver(
        (entradas) => {
          if (entradas.some((e) => e.isIntersecting)) {
            el.setAttribute('data-visto', '');
            observador.disconnect();
          }
        },
        { rootMargin: '0px 0px -10% 0px' },
      );
      observador.observe(el);
      destroy.onDestroy(() => observador.disconnect());
    });
  }
}
