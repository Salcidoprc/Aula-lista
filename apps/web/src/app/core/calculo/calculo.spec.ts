import vectores from '../../../../../api/tests/vectores-calculo.json';
import { RubroCalculo, calcularPeriodo, valorConCriterios } from './calculo';
import { formatear, fraccion, redondear } from './decimal';

interface Caso {
  nombre: string;
  configuracion: {
    califMinima: string;
    decimalesFinal: number;
    redondeo: 'arriba' | 'abajo' | 'cercano';
  };
  rubros: RubroCalculo[];
  esperado: {
    rubros: Record<string, string | null>;
    periodo: string | null;
    final: string | null;
    sinDatos: string[];
  };
}

/** Los vectores dan los criterios de la celda con `id`; el cálculo los llama `criterioId`. */
function rubros(caso: Caso): RubroCalculo[] {
  return caso.rubros.map((r) => ({
    ...r,
    celdas: r.celdas.map((c) => ({
      ...c,
      criterios: (c.criterios as unknown as { id: string }[] | undefined)?.map((cc) => ({
        ...cc,
        criterioId: cc.id,
      })),
    })),
  }));
}

const comparar = (valor: ReturnType<typeof fraccion> | null, esperado: string | null): void => {
  if (esperado === null) {
    expect(valor).toBeNull();
  } else {
    expect(valor).not.toBeNull();
    const decimales = esperado.split('.')[1]?.length ?? 0;
    expect(formatear(valor!, decimales)).toBe(esperado);
  }
};

describe('Cálculo de calificaciones con los vectores compartidos (RN-036)', () => {
  for (const caso of vectores.casos as unknown as Caso[]) {
    it(caso.nombre, () => {
      const resultado = calcularPeriodo(rubros(caso), caso.configuracion);
      for (const [id, esperado] of Object.entries(caso.esperado.rubros)) {
        const promedio = resultado.rubros.get(id) ?? null;
        comparar(promedio && redondear(promedio, 2, 'cercano'), esperado);
      }
      comparar(resultado.periodo, caso.esperado.periodo);
      comparar(resultado.final, caso.esperado.final);
      expect([...resultado.sinDatos].sort()).toEqual([...caso.esperado.sinDatos].sort());
    });
  }

  it('SPEC-03: Exposición 40/30/30 con 9, 8 y 10 da 9.00', () => {
    const criterios = [
      { id: 'a', peso: 40, forma: 'numerica' as const, califMinima: 5 },
      { id: 'b', peso: 30, forma: 'numerica' as const, califMinima: 5 },
      { id: 'c', peso: 30, forma: 'numerica' as const, califMinima: 5 },
    ];
    const valor = valorConCriterios(criterios, [
      { criterioId: 'a', valor: 9 },
      { criterioId: 'b', valor: 8 },
      { criterioId: 'c', valor: 10 },
    ]);
    expect(formatear(valor!, 2)).toBe('9.00');
  });

  it('RN-035: lee los números por su texto, sin errores binarios', () => {
    expect(formatear(fraccion(0.1 + 0.2), 2)).toBe('0.30');
    expect(formatear(redondear(fraccion('8.125'), 2, 'cercano'), 2)).toBe('8.13');
    expect(formatear(redondear(fraccion('7.456'), 1, 'abajo'), 1)).toBe('7.4');
  });
});
