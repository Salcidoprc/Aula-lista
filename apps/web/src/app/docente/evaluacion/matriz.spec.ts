import { TipoRubroDto } from '../../api/models/tipo-rubro-dto';
import { fraccion } from '../../core/calculo/decimal';
import { aEntrada, criterioNuevo, rubroNuevo, validar } from './borrador-rubros';
import { unidadDeHoy } from './calificaciones';
import { interpretar, leerPegado, numero, textoCelda, validarValor } from './matriz';

const tareas: TipoRubroDto = {
  id: 't1',
  nombre: 'Tareas',
  comportamiento: 'actividades',
  formaSugerida: 'cumplimiento',
  descripcion: '',
  icono: '',
  nombresSugeridos: ['Tareas'],
};
const tipos = new Map([[tareas.id, tareas]]);

describe('captura de una celda (SPEC-03)', () => {
  it('lee números con punto o coma, NE y EX', () => {
    expect(interpretar('8,5', 'numerica')).toEqual({ valor: '8.5', marca: 'ninguna' });
    expect(interpretar(' NE ', 'numerica')).toEqual({ valor: null, marca: 'no_entrego' });
    expect(interpretar('ex', 'cumplimiento')).toEqual({ valor: null, marca: 'exento' });
    expect(interpretar('', 'numerica')).toEqual({ valor: null, marca: 'ninguna' });
  });

  it('en cumplimiento acepta ✓ y ✗ y rechaza números', () => {
    expect(interpretar('✓', 'cumplimiento')).toEqual({ valor: null, marca: 'cumplio' });
    expect(interpretar('no', 'cumplimiento')).toEqual({ valor: null, marca: 'no_cumplio' });
    expect(interpretar('9', 'cumplimiento')).toBe('marca_invalida');
  });

  it('caso borde: 11 y -1 se rechazan con calificacion_fuera_de_rango', () => {
    expect(validarValor('11', 5)).toBe('calificacion_fuera_de_rango');
    expect(validarValor('-1', 0)).toBe('calificacion_fuera_de_rango');
  });

  it('criterio: un 3 con mínima 5 se rechaza con calificacion_menor_a_minima', () => {
    expect(validarValor('3', 5)).toBe('calificacion_menor_a_minima');
    expect(validarValor('5', 5)).toBeNull();
  });

  it('RN-030: hasta dos decimales', () => {
    expect(validarValor('8.75', 5)).toBeNull();
    expect(validarValor('8.755', 5)).toBe('calificacion_invalida');
    expect(validarValor('ocho', 5)).toBe('calificacion_invalida');
  });

  it('muestra la celda como la escribió el docente', () => {
    const base = {
      actividadId: 'a',
      alumnoId: 'b',
      criterios: [],
      origen: 'individual',
      version: 1,
    };
    expect(textoCelda({ ...base, valor: '8.50', marca: 'ninguna' })).toBe('8.5');
    expect(textoCelda({ ...base, valor: null, marca: 'no_entrego' })).toBe('NE');
    expect(textoCelda({ ...base, valor: null, marca: 'cumplio' })).toBe('✓');
    expect(numero('10.00')).toBe('10');
  });

  it('lee lo pegado desde Excel por renglones y tabuladores', () => {
    expect(leerPegado('8\t9\r\n10\t7\r\n')).toEqual([
      ['8', '9'],
      ['10', '7'],
    ]);
  });
});

describe('editor de rubros (RN-110, RN-111, RN-118)', () => {
  it('un rubro nuevo propone lo que falta para 100 y la forma sugerida del tipo', () => {
    const r = rubroNuevo(tareas, '5', fraccion('70'));
    expect(r.peso).toBe('30');
    expect(r.forma).toBe('cumplimiento');
    expect(r.califMinima).toBe('5');
  });

  it('criterios que suman 90 no se guardan (pesos_criterios_no_suman_100)', () => {
    const r = {
      ...rubroNuevo(tareas, '5', fraccion('0')),
      conCriterios: true,
      criterios: [
        { ...criterioNuevo('5'), nombre: 'Información', peso: '40' },
        { ...criterioNuevo('5'), nombre: 'Presentación', peso: '50' },
      ],
    };
    expect([...validar([r], tipos).values()]).toEqual(['sumaCriterios']);
  });

  it('pide de 2 a 10 criterios y pesos válidos', () => {
    const r = {
      ...rubroNuevo(tareas, '5', fraccion('0')),
      peso: '101',
      conCriterios: true,
      criterios: [{ ...criterioNuevo('5'), nombre: 'Único', peso: '100' }],
    };
    const errores = validar([r], tipos);
    expect(errores.get(`${r.clave}-peso`)).toBe('peso');
    expect(errores.get(`${r.clave}-criterios`)).toBe('cantidadCriterios');
  });

  it('manda los números como texto y sin criterios cuando no se usan', () => {
    const r = { ...rubroNuevo(tareas, '5', fraccion('0')), peso: '33,5' };
    const [entrada] = aEntrada([r], tipos);
    expect(entrada.peso).toBe('33.5');
    expect(entrada.criterios).toBeNull();
  });
});

describe('unidad que abre', () => {
  const p = (id: string, inicio: string, fin: string, estado = 'abierto') => ({
    id,
    inicio,
    fin,
    estado,
    nombre: id,
    orden: 1,
    unidadId: id,
  });

  it('abre la unidad de hoy; si no hay, la primera abierta', () => {
    const lista = [
      p('u1', '2026-08-25', '2026-10-10', 'cerrado'),
      p('u2', '2026-10-11', '2026-12-19'),
    ];
    expect(unidadDeHoy(lista, '2026-10-17')?.id).toBe('u2');
    expect(unidadDeHoy(lista, '2027-03-01')?.id).toBe('u2');
  });
});
