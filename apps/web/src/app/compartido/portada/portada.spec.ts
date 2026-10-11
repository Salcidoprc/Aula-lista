import { TestBed } from '@angular/core/testing';
import { Portada } from './portada';

describe('Portada', () => {
  function crear(entradas: Record<string, string>) {
    const fixture = TestBed.createComponent(Portada);
    for (const [nombre, valor] of Object.entries(entradas)) {
      fixture.componentRef.setInput(nombre, valor);
    }
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('sin grado usa el color de la marca y no muestra número', () => {
    const el = crear({ titulo: 'Aula Lista' });
    expect(el.textContent).toContain('Aula Lista');
    expect(el.hasAttribute('data-grado')).toBe(false);
    expect(el.querySelector('.numero')).toBeNull();
  });

  it('con grado pinta su color y oculta el número a lectores de pantalla', () => {
    const el = crear({ titulo: '4° B · Matemáticas', numero: '4', grado: 'p4' });
    expect(el.getAttribute('data-grado')).toBe('p4');
    expect(el.classList).toContain('con-numero');
    const numero = el.querySelector('.numero');
    expect(numero?.textContent?.trim()).toBe('4');
    expect(numero?.getAttribute('aria-hidden')).toBe('true');
  });
});
