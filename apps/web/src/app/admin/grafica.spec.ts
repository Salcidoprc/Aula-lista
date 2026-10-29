import { pasoDelEje } from './grafica';
import { pasoDeCalor } from './resumen';

describe('RF-094: escalas de las gráficas del Resumen', () => {
  it('el eje sube en pasos de 1, 2 o 5 y dos pasos cubren el máximo', () => {
    expect(pasoDelEje(0)).toBe(1);
    expect(pasoDelEje(2)).toBe(1);
    expect(pasoDelEje(3)).toBe(2);
    expect(pasoDelEje(7)).toBe(5);
    expect(pasoDelEje(13)).toBe(10);
    expect(pasoDelEje(18)).toBe(10);
    expect(pasoDelEje(240)).toBe(200);
  });

  it('el mapa de calor separa el cero, cuatro tramos de 25 % y la semana sin datos', () => {
    expect(pasoDeCalor(null)).toBeNull();
    expect(pasoDeCalor(0)).toBe(0);
    expect(pasoDeCalor('10.5')).toBe(1);
    expect(pasoDeCalor(25)).toBe(1);
    expect(pasoDeCalor(50)).toBe(2);
    expect(pasoDeCalor(75.01)).toBe(4);
    expect(pasoDeCalor(100)).toBe(4);
  });
});
