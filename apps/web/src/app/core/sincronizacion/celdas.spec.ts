import { CambioCelda } from '../../api/models/cambio-celda';
import { MatrizCalificacionesDto } from '../../api/models/matriz-calificaciones-dto';
import { SIN_ALUMNO, composicion, conCambios, llavesDeCambio } from './celdas';

const alumno = (id: string, dadoDeBaja = false) => ({
  id,
  numeroLista: 1,
  nombres: id,
  apellidos: id,
  dadoDeBaja,
});

const matriz = (): MatrizCalificacionesDto =>
  ({
    periodoId: 'p1',
    rubros: [{ rubro: {}, actividades: [{ id: 'a1', modo: 'por_equipo' }] }],
    alumnos: ['al1', 'al2', 'al3', 'al4', 'al5'].map((id) => alumno(id)),
    celdas: [],
    equipos: [{ id: 'e1', nombre: 'Halcones', alumnoIds: ['al1', 'al2', 'al3', 'al4'] }],
    composiciones: [],
  }) as unknown as MatrizCalificacionesDto;

const alEquipo = (valor: number): CambioCelda => ({
  id: null,
  actividadId: 'a1',
  alumnoId: SIN_ALUMNO,
  equipoId: 'e1',
  valor,
  marca: 'ninguna',
  criterios: null,
  version: 0,
});

const aUno = (alumnoId: string, valor: number): CambioCelda => ({
  ...alEquipo(valor),
  alumnoId,
  equipoId: null,
});

const valores = (m: MatrizCalificacionesDto) =>
  ['al1', 'al2', 'al3', 'al4', 'al5'].map((id) => {
    const c = m.celdas.find((x) => x.alumnoId === id);
    return c ? `${c.valor} ${c.origen}` : '—';
  });

describe('captura por equipo en el dispositivo (RN-115, RN-116)', () => {
  it('9 al equipo, 7 a uno y 8 al equipo: tres con 8 y el ajustado conserva 7', () => {
    const m = conCambios(matriz(), [alEquipo(9), aUno('al2', 7), alEquipo(8)]);
    expect(valores(m)).toEqual([
      '8 equipo',
      '7 ajustada',
      '8 equipo',
      '8 equipo',
      '—', // sin equipo
    ]);
  });

  it('vaciar la celda de un ajustado la regresa al equipo', () => {
    const m = conCambios(matriz(), [
      alEquipo(9),
      aUno('al2', 7),
      { ...aUno('al2', 0), valor: null },
      alEquipo(6),
    ]);
    expect(valores(m)[1]).toBe('6 equipo');
  });

  it('con composición guardada sigue esa, aunque los equipos de hoy sean otros', () => {
    const base = {
      ...matriz(),
      composiciones: [
        { actividadId: 'a1', equipoId: 'e1', nombre: 'Halcones', alumnoIds: ['al5'] },
      ],
    } as MatrizCalificacionesDto;
    expect(composicion(base, 'a1')).toEqual([
      { equipoId: 'e1', nombre: 'Halcones', alumnoIds: ['al5'] },
    ]);
    expect(llavesDeCambio(base, alEquipo(9))).toEqual(['a1:al5']);
  });

  it('las bajas no reciben la calificación del equipo', () => {
    const base = {
      ...matriz(),
      alumnos: [alumno('al1'), alumno('al2', true), alumno('al3'), alumno('al4'), alumno('al5')],
    } as MatrizCalificacionesDto;
    expect(llavesDeCambio(base, alEquipo(9))).toEqual(['a1:al1', 'a1:al3', 'a1:al4']);
  });
});
