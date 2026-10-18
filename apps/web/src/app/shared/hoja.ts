import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  output,
  viewChild,
} from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { Icono } from './iconos';

/** Distancia o velocidad con que soltar la hoja la cierra (regla 07: cierre por velocidad). */
const UMBRAL_DISTANCIA = 0.3;
const UMBRAL_VELOCIDAD = 0.5; // px por ms

/**
 * Hoja inferior en el celular (catálogo de la regla 07): sube desde abajo con `--ease-drawer` y se cierra
 * deslizándola hacia abajo, por distancia o por velocidad. En pantalla ancha es un diálogo centrado. Estilos en
 * `styles/hoja.css` (los de `<dialog>` y `@starting-style` no pasan por el encapsulado). Usa
 * `<dialog>` modal: el foco queda dentro, Escape y tocar fuera la cierran. Lleva además un botón visible para
 * cerrar: Safari no cierra con `closedby` al tocar fuera y no todos pueden arrastrar.
 */
@Component({
  selector: 'app-hoja',
  imports: [Icono, TranslocoDirective],
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
        (pointercancel)="cancelar()"
      >
        <span></span>
      </div>
      <button class="cerrar-hoja" type="button" (click)="cerrar()" *transloco="let t">
        <app-icono nombre="cerrar" />
        <span class="solo-lector">{{ t('app.cerrar') }}</span>
      </button>
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
  /** Inicio del arrastre y la última muestra, para medir la velocidad al soltar y no el promedio. */
  private arrastre: { y: number; dy: number; ultimoY: number; ultimoT: number } | null = null;

  /** Si ya está abierta no hace nada: `showModal()` lanza un error sobre un diálogo abierto. */
  abrir(): void {
    const el = this.dialogo().nativeElement;
    if (!el.open) {
      el.showModal();
    }
  }

  cerrar(): void {
    const el = this.dialogo().nativeElement;
    el.style.removeProperty('transform');
    el.style.removeProperty('transition');
    el.close();
  }

  protected empezar(evento: PointerEvent): void {
    (evento.target as HTMLElement).setPointerCapture(evento.pointerId);
    this.arrastre = {
      y: evento.clientY,
      dy: 0,
      ultimoY: evento.clientY,
      ultimoT: evento.timeStamp,
    };
    this.dialogo().nativeElement.style.transition = 'none';
  }

  /** Sigue al dedo solo hacia abajo; hacia arriba la hoja no se despega. */
  protected mover(evento: PointerEvent): void {
    if (!this.arrastre) {
      return;
    }
    this.arrastre.dy = Math.max(0, evento.clientY - this.arrastre.y);
    if (evento.timeStamp - this.arrastre.ultimoT > 16) {
      this.arrastre.ultimoY = evento.clientY;
      this.arrastre.ultimoT = evento.timeStamp;
    }
    this.dialogo().nativeElement.style.transform = `translateY(${this.arrastre.dy}px)`;
  }

  protected soltar(evento: PointerEvent): void {
    const arrastre = this.arrastre;
    if (!arrastre) {
      return;
    }
    this.arrastre = null;
    const el = this.dialogo().nativeElement;
    const velocidad =
      (evento.clientY - arrastre.ultimoY) / Math.max(1, evento.timeStamp - arrastre.ultimoT);
    el.style.removeProperty('transition');
    const porVelocidad = velocidad > UMBRAL_VELOCIDAD && arrastre.dy > 0;
    if (arrastre.dy > el.offsetHeight * UMBRAL_DISTANCIA || porVelocidad) {
      this.cerrar();
      // Termina al ritmo del dedo: un tirón rápido no frena a 300 ms.
      if (porVelocidad) {
        const ms = Math.round(
          Math.min(300, Math.max(150, (el.offsetHeight - arrastre.dy) / velocidad)),
        );
        el.style.transitionDuration = `${ms}ms`;
        setTimeout(() => el.style.removeProperty('transition-duration'), ms);
      }
    } else {
      el.style.removeProperty('transform');
    }
  }

  /** Si el navegador cancela el gesto, la hoja vuelve a su lugar; nunca se cierra por eso. */
  protected cancelar(): void {
    this.arrastre = null;
    const el = this.dialogo().nativeElement;
    el.style.removeProperty('transition');
    el.style.removeProperty('transform');
  }
}
