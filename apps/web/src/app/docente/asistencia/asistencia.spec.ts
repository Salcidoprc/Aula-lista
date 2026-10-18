import {
  conPendientes,
  diaLargo,
  moverDia,
  porcentaje,
  resumen,
  siguienteEstado,
} from './asistencia';

describe('pasar lista (SPEC-10)', () => {
  it('RN-101: cada toque recorre presente, falta, retardo y justificada', () => {
    expect(siguienteEstado('presente')).toBe('falta');
    expect(siguienteEstado('falta')).toBe('retardo');
    expect(siguienteEstado('retardo')).toBe('justificada');
    expect(siguienteEstado('justificada')).toBe('presente');
  });

  it('RF-103: lo pendiente va encima de la toma del servidor y por alumno gana lo último', () => {
    const [toma] = conPendientes(
      [
        {
          fecha: '2026-10-15',
          alumnos: [
            { alumnoId: 'a', estado: 'presente', version: 1 },
            { alumnoId: 'b', estado: 'falta', version: 2 },
          ],
        },
      ],
      [
        {
          id: '1',
          grupoId: 'g',
          fecha: '2026-10-15',
          creado: 1,
          alumnos: [{ alumnoId: 'a', estado: 'falta' }],
        },
        {
          id: '2',
          grupoId: 'g',
          fecha: '2026-10-15',
          creado: 2,
          alumnos: [{ alumnoId: 'a', estado: 'retardo' }],
        },
      ],
      ['a', 'b'],
    );
    expect(Object.fromEntries(toma.estados)).toEqual({ a: 'retardo', b: 'falta' });
  });

  it('RN-101: un día que solo está en la cola parte de todos los activos presentes', () => {
    const tomas = conPendientes(
      [],
      [
        {
          id: '1',
          grupoId: 'g',
          fecha: '2026-10-17',
          creado: 1,
          alumnos: [{ alumnoId: 'b', estado: 'falta' }],
        },
      ],
      ['a', 'b', 'c'],
    );
    expect(tomas).toHaveLength(1);
    expect(Object.fromEntries(tomas[0].estados)).toEqual({
      a: 'presente',
      b: 'falta',
      c: 'presente',
    });
  });

  it('RN-105: un alumno de baja que no estaba en la toma no aparece', () => {
    const [toma] = conPendientes(
      [{ fecha: '2026-10-15', alumnos: [{ alumnoId: 'a', estado: 'presente', version: 1 }] }],
      [
        {
          id: '1',
          grupoId: 'g',
          fecha: '2026-10-15',
          creado: 1,
          alumnos: [{ alumnoId: 'x', estado: 'falta' }],
        },
      ],
      ['a'],
    );
    expect([...toma.estados.keys()]).toEqual(['a']);
  });

  it('resume los estados del día', () => {
    expect(resumen(['presente', 'presente', 'falta', 'justificada'])).toEqual({
      presente: 2,
      falta: 1,
      retardo: 0,
      justificada: 1,
    });
  });

  it('RN-102 y RN-103: el porcentaje no cuenta la justificada y el retardo vale lo que diga el grupo', () => {
    const estados = [...Array<'presente'>(18).fill('presente'), 'falta', 'justificada'] as const;
    expect(porcentaje(estados)).toBe(95);
    expect(porcentaje(['presente', 'retardo'], '0.5')).toBe(75);
    expect(porcentaje(['presente', 'retardo'], 0)).toBe(50);
    expect(porcentaje(['justificada'])).toBeNull();
  });

  it('mueve la fecha y la muestra en español de México', () => {
    expect(moverDia('2026-10-31', 1)).toBe('2026-11-01');
    expect(moverDia('2026-03-01', -1)).toBe('2026-02-28');
    expect(diaLargo('2026-10-17')).toBe('Sábado 17 de octubre');
  });
});
