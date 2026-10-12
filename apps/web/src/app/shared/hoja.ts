import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  output,
  viewChild,
} from '@angular/core';

/** Distancia o velocidad con que soltar la hoja la cierra (regla 07: cierre por velocidad). */
const UMBRAL_DISTANCIA = 0.3;
const UMBRAL_VELOCIDAD = 0.5; // px por ms

/**
 * Hoja inferior en el celular (catálogo de la regla 07): sube desde abajo con `--ease-drawer` y se cierra
 * deslizándola hacia abajo, por distancia o por velocidad. En pantalla ancha es un diálogo centrado. Estilos en
 * `styles/hoja.css` (los de `<dialog>` y `@starting-style` no pasan por el encapsulado). Usa
 * `<dialog>` modal: el foco queda dentro, Escape y tocar fuera la cierran.
 */
@Component({
  selector: 'app-hoja',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog
      #dialogo
      class="hoja"
      closedby="any"
      [attr.aria-labelledby]="etiqueta()"
      (close)="cerrada.emit()"
    >
      <div
        class="asa"
        aria-hidden="true"
        (pointerdown)="empezar($event)"
        (pointermove)="mover($event)"
        (pointerup)="soltar($event)"
        (pointercancel)="soltar($event)"
      >
        <span></span>
      </div>
      <div class="cuerpo">
        <ng-content />
      </div>
    </dialog>
  `,
})
export class Hoja {
  /** Id del título de la hoja, para nombrar el diálogo. */
  readonly etiqueta = input.required<string>();
  readonly cerrada = output<void>();

  private readonly dialogo = viewChild.required<ElementRef<HTMLDialogElement>>('dialogo');
  private arrastre: { y: number; t: number; dy: number } | null = null;

  abrir(): void {
    this.dialogo().nativeElement.showModal();
  }

  cerrar(): void {
    const el = this.dialogo().nativeElement;
    el.style.removeProperty('transform');
    el.style.removeProperty('transition');
    el.close();
  }

  protected empezar(evento: PointerEvent): void {
    (evento.target as HTMLElement).setPointerCapture(evento.pointerId);
    this.arrastre = { y: evento.clientY, t: evento.timeStamp, dy: 0 };
    this.dialogo().nativeElement.style.transition = 'none';
  }

  /** Sigue al dedo solo hacia abajo; hacia arriba la hoja no se despega. */
  protected mover(evento: PointerEvent): void {
    if (!this.arrastre) {
      return;
    }
    this.arrastre.dy = Math.max(0, evento.clientY - this.arrastre.y);
    this.dialogo().nativeElement.style.transform = `translateY(${this.arrastre.dy}px)`;
  }

  protected soltar(evento: PointerEvent): void {
    const arrastre = this.arrastre;
    if (!arrastre) {
      return;
    }
    this.arrastre = null;
    const el = this.dialogo().nativeElement;
    const velocidad = arrastre.dy / Math.max(1, evento.timeStamp - arrastre.t);
    el.style.removeProperty('transition');
    if (arrastre.dy > el.offsetHeight * UMBRAL_DISTANCIA || velocidad > UMBRAL_VELOCIDAD) {
      this.cerrar();
    } else {
      el.style.removeProperty('transform');
    }
  }
}
