import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { DemoCaptura } from './demo-captura';

describe('DemoCaptura (RF-010)', () => {
  function crear() {
    TestBed.configureTestingModule({
      imports: [
        DemoCaptura,
        TranslocoTestingModule.forRoot({
          langs: { 'es-MX': {} },
          translocoConfig: { availableLangs: ['es-MX'], defaultLang: 'es-MX' },
        }),
      ],
    });
    const fixture = TestBed.createComponent(DemoCaptura);
    fixture.detectChanges();
    const raiz = fixture.nativeElement as HTMLElement;
    const tecla = (texto: string) =>
      [...raiz.querySelectorAll<HTMLButtonElement>('.teclado button')].find(
        (b) => b.textContent?.trim() === texto,
      )!;
    const celdas = () => [...raiz.querySelectorAll('.celda')].map((c) => c.textContent?.trim());
    const actual = () =>
      [...raiz.querySelectorAll('.fila')].findIndex((f) => f.hasAttribute('aria-current'));
    return { fixture, raiz, tecla, celdas, actual };
  }

  it('cada toque califica al alumno actual y pasa al siguiente sin calificar', () => {
    const { fixture, tecla, celdas, actual } = crear();
    expect(actual()).toBe(0);
    tecla('9').click();
    fixture.detectChanges();
    expect(celdas()[0]).toBe('9');
    expect(actual()).toBe(1);
  });

  it('al calificar a los seis muestra el tiempo y "Otra vez" vuelve a empezar', () => {
    const { fixture, raiz, tecla, celdas, actual } = crear();
    for (const v of ['10', '9', '8', '7', '6', '5']) {
      tecla(v).click();
      fixture.detectChanges();
    }
    expect(celdas()).toEqual(['10', '9', '8', '7', '6', '5']);
    expect(actual()).toBe(-1);
    expect(raiz.querySelector('.resultado strong')?.textContent).toContain('landing.demo.listo');
    raiz.querySelector<HTMLButtonElement>('.otra')!.click();
    fixture.detectChanges();
    expect(celdas().every((c) => c === '')).toBe(true);
    expect(actual()).toBe(0);
  });

  it('tocar una fila la elige; con teclado, el 0 es 10 y "No entregó" se ve como raya', () => {
    const { fixture, raiz, tecla, celdas } = crear();
    raiz.querySelectorAll<HTMLButtonElement>('.fila')[3].click();
    fixture.detectChanges();
    raiz.dispatchEvent(new KeyboardEvent('keydown', { key: '0', bubbles: true }));
    fixture.detectChanges();
    expect(celdas()[3]).toBe('10');
    tecla('landing.demo.noEntrego').click();
    fixture.detectChanges();
    expect(celdas()[4]).toBe('—');
  });
});
