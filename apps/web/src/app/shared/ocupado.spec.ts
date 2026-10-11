import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Ocupado } from './ocupado';

@Component({
  imports: [Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form (submit)="$event.preventDefault(); envios = envios + 1">
      <button type="submit" [appOcupado]="ocupado()" (click)="clics = clics + 1">Guardar</button>
    </form>
  `,
})
class Prueba {
  readonly ocupado = signal(false);
  clics = 0;
  envios = 0;
}

describe('Ocupado', () => {
  function crear() {
    const fixture = TestBed.createComponent(Prueba);
    fixture.detectChanges();
    const boton = (fixture.nativeElement as HTMLElement).querySelector('button')!;
    return { fixture, boton, prueba: fixture.componentInstance };
  }

  it('libre: el botón no lleva aria-disabled y el clic envía', () => {
    const { boton, prueba } = crear();
    expect(boton.hasAttribute('aria-disabled')).toBe(false);
    boton.click();
    expect(prueba.clics).toBe(1);
    expect(prueba.envios).toBe(1);
  });

  it('ocupado: sigue enfocable, se anuncia como no disponible y descarta el clic', () => {
    const { fixture, boton, prueba } = crear();
    prueba.ocupado.set(true);
    fixture.detectChanges();
    expect(boton.disabled).toBe(false);
    expect(boton.getAttribute('aria-disabled')).toBe('true');
    expect(boton.getAttribute('aria-busy')).toBe('true');
    boton.click();
    expect(prueba.clics).toBe(0);
    expect(prueba.envios).toBe(0);
  });
});
