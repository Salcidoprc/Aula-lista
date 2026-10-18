import { describe, expect, it } from 'vitest';
import { AlumnoCierreDto } from '../../api/models/alumno-cierre-dto';
import { contarPalabras, segmentos, siguientePendiente, vecinos } from './cierre';

const alumno = (id: string, estado: string): AlumnoCierreDto => ({
  alumnoId: id,
  numeroLista: id,
  nombres: 'N',
  apellidos: 'A',
  calificacion: 9,
  estado,
  observacionId: `o${id}`,
  datosCambiaron: false,
  fallo: null,
});

describe('cierre (SPEC-04)', () => {
  it('cuenta palabras como el servidor (RN-046)', () => {
    expect(contarPalabras('  Ana   mostró\nconstancia ')).toBe(3);
    expect(contarPalabras('   ')).toBe(0);
  });

  it('RF-044: la barra suma aprobadas y publicadas, y junta sin generar con generando', () => {
    expect(
      segmentos({ sinGenerar: 2, generando: 1, borrador: 3, aprobada: 3, publicada: 1 }).map(
        (s) => [s.estado, s.cantidad, s.porcentaje],
      ),
    ).toEqual([
      ['aprobada', 4, 40],
      ['borrador', 3, 30],
      ['pendiente', 3, 30],
    ]);
    expect(
      segmentos({ sinGenerar: 0, generando: 0, borrador: 0, aprobada: 0, publicada: 0 })[0]
        .porcentaje,
    ).toBe(0);
  });

  it('"Aprobar y siguiente" salta a las aprobadas y da la vuelta', () => {
    const lista = [
      alumno('1', 'borrador'),
      alumno('2', 'aprobada'),
      alumno('3', 'borrador'),
      alumno('4', 'aprobada'),
    ];

    expect(siguientePendiente(lista, '1')?.alumnoId).toBe('3');
    expect(siguientePendiente(lista, '3')?.alumnoId).toBe('1');
    expect(siguientePendiente([alumno('1', 'aprobada'), alumno('2', 'aprobada')], '1')).toBeNull();
  });

  it('anterior y siguiente en orden de lista, con la posición', () => {
    const lista = [alumno('1', 'borrador'), alumno('2', 'borrador'), alumno('3', 'borrador')];

    expect(vecinos(lista, '1')).toMatchObject({ anterior: null, posicion: 1 });
    expect(vecinos(lista, '2').siguiente?.alumnoId).toBe('3');
    expect(vecinos(lista, '3').siguiente).toBeNull();
  });
});
