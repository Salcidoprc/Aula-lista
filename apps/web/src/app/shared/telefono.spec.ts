import { mostrarTelefono, normalizarTelefono } from './telefono';

describe('normalizarTelefono (RN-014)', () => {
  it.each([
    ['667 123 4567', '+526671234567'],
    ['(667) 123-45-67', '+526671234567'],
    ['52 667 123 4567', '+526671234567'],
    ['521 667 123 4567', '+526671234567'],
    ['044 667 123 4567', '+526671234567'],
    ['+52 1 667 123 4567', '+526671234567'],
    ['+1 415 555 0100', '+14155550100'],
  ])('%s queda %s', (entrada, esperado) => {
    expect(normalizarTelefono(entrada)).toBe(esperado);
  });

  it.each(['', '123', '667-123-456', 'seis seis siete', '+0 123 456 789', '66+71234567'])(
    'rechaza "%s"',
    (entrada) => {
      expect(normalizarTelefono(entrada)).toBeNull();
    },
  );

  it('muestra los de México en grupos de 3, 3 y 4', () => {
    expect(mostrarTelefono('+526671234567')).toBe('667 123 4567');
    expect(mostrarTelefono('+14155550100')).toBe('+14155550100');
  });
});
