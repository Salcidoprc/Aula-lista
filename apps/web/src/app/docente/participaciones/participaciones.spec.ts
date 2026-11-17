import { AlumnoMatrizDto } from '../../api/models/alumno-matriz-dto';
import { ParticipacionEnCola } from '../../core/sincronizacion/almacen-local';
import { leerValorParticipacion } from '../grupos/configuracion-grupo';
import { buscar, conPendientes, contar, puntos, ultimaDelDia } from './participaciones';

const alumno = (
  id: string,
  numeroLista: number,
  apellidos: string,
  nombres: string,
): AlumnoMatrizDto => ({
  id,
  numeroLista,
  apellidos,
  nombres,
  dadoDeBaja: false,
});

const cola = (
  id: string,
  alumnoId: string,
  operacion: 'dar' | 'quitar',
  fecha = '2026-11-17',
): ParticipacionEnCola => ({ id, grupoId: 'g', alumnoId, fecha, creado: 1, operacion });

describe('participaciones extra (SPEC-03)', () => {
  const alumnos = [
    alumno('a', 1, 'López Ruiz', 'Ana'),
    alumno('b', 2, 'Muñoz Pérez', 'Bruno'),
    alumno('c', 12, 'Ortega Muñoz', 'Carla'),
  ];

  it('RF-110: busca por palabras del nombre en cualquier orden, sin acentos', () => {
    expect(buscar(alumnos, 'ana lop').map((a) => a.id)).toEqual(['a']);
    expect(buscar(alumnos, 'munoz').map((a) => a.id)).toEqual(['b', 'c']);
    expect(buscar(alumnos, 'zz')).toEqual([]);
    expect(buscar(alumnos, '  ')).toEqual(alumnos);
  });

  it('RF-110: un número busca el número de lista exacto', () => {
    expect(buscar(alumnos, '1').map((a) => a.id)).toEqual(['a']);
    expect(buscar(alumnos, '12').map((a) => a.id)).toEqual(['c']);
  });

  it('RF-111: lo dado sin señal se suma y lo quitado sin señal desaparece, sin duplicar', () => {
    const servidor = [
      { id: 's1', alumnoId: 'a', fecha: '2026-11-17' },
      { id: 's2', alumnoId: 'a', fecha: '2026-11-16' },
    ];
    const vistas = conPendientes(servidor, [
      cola('s1', 'a', 'quitar'),
      cola('p1', 'b', 'dar'),
      cola('s2', 'a', 'dar'),
    ]);
    expect(vistas.map((p) => [p.id, p.pendiente])).toEqual([
      ['s2', false],
      ['p1', true],
    ]);
    expect(contar(vistas).get('a')).toBe(1);
    expect(contar(vistas, '2026-11-17').get('a')).toBeUndefined();
    expect(contar(vistas, '2026-11-17').get('b')).toBe(1);
  });

  it('RF-111: "Quitar una" quita la última del día', () => {
    const vistas = conPendientes(
      [
        { id: 'x', alumnoId: 'a', fecha: '2026-11-17' },
        { id: 'y', alumnoId: 'a', fecha: '2026-11-17' },
      ],
      [],
    );
    expect(ultimaDelDia(vistas, 'a', '2026-11-17')?.id).toBe('y');
    expect(ultimaDelDia(vistas, 'a', '2026-11-16')).toBeUndefined();
  });

  it('RN-122: los puntos son participaciones por el valor, con dos decimales', () => {
    expect(puntos(3, '0.1')).toBe('0.30');
    expect(puntos(7, '0.15')).toBe('1.05');
    expect(puntos(0, '0.25')).toBe('0.00');
  });

  it('RN-121: el valor va de 0.01 a 1.00 con hasta dos decimales', () => {
    expect(leerValorParticipacion('0.1')).toBe('0.1');
    expect(leerValorParticipacion('0,25')).toBe('0.25');
    expect(leerValorParticipacion('.5')).toBe('0.5');
    expect(leerValorParticipacion('1')).toBe('1');
    expect(leerValorParticipacion('1.00')).toBe('1.00');
    expect(leerValorParticipacion('0.01')).toBe('0.01');
    expect(leerValorParticipacion('0')).toBeNull();
    expect(leerValorParticipacion('1.01')).toBeNull();
    expect(leerValorParticipacion('0.005')).toBeNull();
    expect(leerValorParticipacion('abc')).toBeNull();
  });
});
