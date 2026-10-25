import { LADO_MAXIMO, esFotoAceptada, medidaReducida } from './foto';

describe('Foto de la lista (RF-015)', () => {
  it('acepta solo JPG y PNG', () => {
    expect(esFotoAceptada({ type: 'image/jpeg' })).toBe(true);
    expect(esFotoAceptada({ type: 'image/png' })).toBe(true);
    expect(esFotoAceptada({ type: 'image/heic' })).toBe(false);
    expect(esFotoAceptada({ type: 'application/pdf' })).toBe(false);
  });

  it('reduce el lado largo sin deformar la foto', () => {
    expect(medidaReducida(4000, 3000)).toEqual({ ancho: LADO_MAXIMO, alto: 1176 });
    expect(medidaReducida(3000, 4000)).toEqual({ ancho: 1176, alto: LADO_MAXIMO });
  });

  it('deja igual una foto que ya es chica', () => {
    expect(medidaReducida(800, 600)).toEqual({ ancho: 800, alto: 600 });
  });
});
