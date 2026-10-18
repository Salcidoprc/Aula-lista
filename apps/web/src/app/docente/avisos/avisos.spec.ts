import { describe, expect, it } from 'vitest';
import { DestinatarioDto } from '../../api/models/destinatario-dto';
import { EntregaDto } from '../../api/models/entrega-dto';
import {
  canalesPrevios,
  coincide,
  fechaAviso,
  fraccionLeidos,
  mostrarContador,
  nombresAlumnos,
  porCompartir,
  sinSaldo,
} from './avisos';

const tutor = (id: string, extra: Partial<DestinatarioDto> = {}): DestinatarioDto => ({
  tutorId: id,
  nombre: `Tutor ${id}`,
  parentesco: null,
  alumnos: [{ id: `a${id}`, nombres: 'Ana', apellidos: 'López' }],
  push: false,
  correo: false,
  urgentesRestantes: 4,
  ...extra,
});

const entrega = (id: string, extra: Partial<EntregaDto> = {}): EntregaDto => ({
  tutorId: id,
  nombre: `Tutor ${id}`,
  telefono: '+526671234567',
  alumnos: [],
  push: false,
  correo: false,
  whatsapp: false,
  estadoWhatsapp: null,
  sinSaldoWhatsapp: false,
  porCompartir: true,
  compartidoEn: null,
  leidoEn: null,
  ...extra,
});

describe('avisos (SPEC-07)', () => {
  it('RN-072, RN-073: un aviso normal llega por push y correo; sin ninguno, lo comparte el docente', () => {
    const tutores = [
      tutor('1', { push: true }),
      tutor('2', { correo: true }),
      tutor('3', { push: true, correo: true }),
      tutor('4'),
    ];
    expect(canalesPrevios(tutores, false)).toEqual({
      push: 2,
      correo: 2,
      whatsapp: 0,
      compartir: 1,
    });
  });

  it('RN-086: urgente suma WhatsApp solo a quien le queda saldo este mes', () => {
    const tutores = [tutor('1'), tutor('2', { urgentesRestantes: 0 }), tutor('3', { push: true })];
    expect(canalesPrevios(tutores, true)).toEqual({
      push: 1,
      correo: 0,
      whatsapp: 2,
      compartir: 1,
    });
    expect(sinSaldo(tutores)).toBe(1);
  });

  it('RN-070: el contador aparece cerca del tope', () => {
    expect(mostrarContador(63, 80)).toBe(false);
    expect(mostrarContador(64, 80)).toBe(true);
  });

  it('busca por tutor o alumno sin acentos', () => {
    const t = tutor('1', { nombre: 'Rosa Martínez' });
    expect(coincide(t, 'martinez')).toBe(true);
    expect(coincide(t, 'LOPEZ')).toBe(true);
    expect(coincide(t, 'pedro')).toBe(false);
    expect(coincide(t, '  ')).toBe(true);
  });

  it('nombra a los hijos de un tutor como se dice', () => {
    expect(nombresAlumnos([])).toBe('');
    expect(
      nombresAlumnos([
        { id: '1', nombres: 'Ana', apellidos: 'López' },
        { id: '2', nombres: 'Mateo', apellidos: 'López' },
        { id: '3', nombres: 'Luz', apellidos: 'López' },
      ]),
    ).toBe('Ana López, Mateo López y Luz López');
  });

  it('pone primero lo que falta compartir', () => {
    const lista = porCompartir([
      entrega('1', { compartidoEn: '2026-10-12T10:00:00Z' }),
      entrega('2', { porCompartir: false }),
      entrega('3'),
    ]);
    expect(lista.map((e) => e.tutorId)).toEqual(['3', '1']);
  });

  it('mide "Leído por n de m" sin dividir entre cero', () => {
    expect(fraccionLeidos(18, 30)).toBe(0.6);
    expect(fraccionLeidos('0', '0')).toBe(0);
  });

  it('escribe la fecha corta y agrega el año solo si es otro', () => {
    const ahora = new Date('2026-10-18T12:00:00');
    expect(fechaAviso('2026-10-12T12:00:00', ahora)).toBe('12 oct');
    expect(fechaAviso('2025-10-12T12:00:00', ahora)).toBe('12 oct 2025');
  });
});
