import {
  cantidadPropuesta,
  proponerUnidades,
  sumarMeses,
  validarCiclo,
  validarUnidades,
} from './unidades';

describe('Unidades del ciclo (RN-022 a RN-024)', () => {
  it('propone 3 trimestres consecutivos que cubren un ciclo de agosto a julio', () => {
    expect(cantidadPropuesta('2026-08-25', '2027-07-16', 'trimestre')).toBe(3);
    const unidades = proponerUnidades('2026-08-25', '2027-07-16', 'trimestre', 3);
    expect(unidades.map((u) => u.nombre)).toEqual(['Trimestre 1', 'Trimestre 2', 'Trimestre 3']);
    expect(unidades[0].inicio).toBe('2026-08-25');
    expect(unidades[2].fin).toBe('2027-07-16');
    expect(validarUnidades('2026-08-25', '2027-07-16', unidades)).toEqual([null, null, null]);
  });

  it('propone 5 bimestres, 2 semestres y una unidad personalizada', () => {
    expect(cantidadPropuesta('2026-08-25', '2027-07-16', 'bimestre')).toBe(5);
    expect(cantidadPropuesta('2026-08-25', '2027-07-16', 'semestre')).toBe(2);
    expect(cantidadPropuesta('2026-08-25', '2027-07-16', 'personalizada')).toBe(1);
  });

  it('rechaza ciclos al revés o de más de 13 meses', () => {
    expect(validarCiclo('2027-07-16', '2026-08-25')).toBe('ciclo_fechas_invalidas');
    expect(validarCiclo('2026-08-01', '2027-09-02')).toBe('ciclo_muy_largo');
    expect(validarCiclo('2026-08-01', '2027-09-01')).toBeNull();
  });

  it('suma meses como la API, sin pasarse al mes siguiente', () => {
    expect(sumarMeses('2026-01-31', 1)).toBe('2026-02-28');
    expect(sumarMeses('2026-08-25', 13)).toBe('2027-09-25');
  });

  it('marca unidades traslapadas y fuera del ciclo', () => {
    const errores = validarUnidades('2026-08-25', '2027-07-16', [
      { id: null, nombre: 'A', inicio: '2026-08-25', fin: '2026-12-01' },
      { id: null, nombre: 'B', inicio: '2026-11-30', fin: '2027-03-01' },
      { id: null, nombre: 'C', inicio: '2027-03-02', fin: '2027-08-01' },
    ]);
    expect(errores).toEqual([null, 'unidades_traslapadas', 'unidad_fuera_de_ciclo']);
    expect(
      validarUnidades('2026-08-25', '2027-07-16', [
        { id: null, nombre: ' ', inicio: '2026-08-25', fin: '2027-07-16' },
      ]),
    ).toEqual(['unidad_nombre_requerido']);
  });
});
