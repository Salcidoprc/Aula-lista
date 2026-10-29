import { SemanaMetricasDto } from '../api/models/semana-metricas-dto';
import {
  cifraMetrica,
  estadoMeta,
  nombresSugeridos,
  pesos,
  porciento,
  semanaCorta,
  valorAuditoria,
  valorMetrica,
} from './panel';

describe('panel de administración (SPEC-09)', () => {
  it('una métrica sin datos no se cuenta como 0 % ni como meta fallada', () => {
    const semana = { porcentajeCierran: null, docentesActivos: 0 } as unknown as SemanaMetricasDto;

    expect(valorMetrica(semana, 'porcentajeCierran')).toBeNull();
    expect(estadoMeta(null, 60, 'porcentajeCierran')).toBe('sinDatos');
    expect(cifraMetrica(null, 'porcentajeCierran')).toBe('—');
  });

  it('el tiempo de cierre cumple por debajo de la meta; las demás, desde la meta', () => {
    expect(estadoMeta(45, 60, 'minutosCierre')).toBe('cumple');
    expect(estadoMeta(60, 60, 'minutosCierre')).toBe('falta');
    expect(estadoMeta(60, 60, 'porcentajeCierran')).toBe('cumple');
    expect(estadoMeta(29, 30, 'docentesActivos')).toBe('falta');
  });

  it('escribe porcentajes con su signo y conteos sin él', () => {
    expect(cifraMetrica(62.5, 'porcentajeObservacionesAceptadas')).toBe('62.5 %');
    expect(cifraMetrica(30, 'docentesActivos')).toBe('30');
  });

  it('un costo de menos de un centavo no se ve como cero', () => {
    expect(pesos(0.0017)).toBe('< 0.01');
    expect(pesos(0)).toBe('0.00');
    expect(pesos('8.456')).toBe('8.46');
  });

  it('lee los nombres sugeridos uno por renglón, sin vacíos ni repetidos', () => {
    expect(nombresSugeridos(' Laboratorio \n\nlaboratorio\nPráctica  de  campo\n')).toEqual([
      'Laboratorio',
      'Práctica de campo',
    ]);
  });

  it('nombra la semana por su lunes', () => {
    expect(semanaCorta('2026-10-12')).toBe('12 oct');
  });

  it('la auditoría dice sí y no en palabras', () => {
    expect(valorAuditoria('true', 'Sí', 'No')).toBe('Sí');
    expect(valorAuditoria(null, 'Sí', 'No')).toBe('—');
    expect(valorAuditoria('Laboratorio', 'Sí', 'No')).toBe('Laboratorio');
  });

  it('RF-094: el resumen escribe el porcentaje sin datos como raya, nunca como 0 %', () => {
    expect(porciento(null)).toBe('—');
    expect(porciento(0)).toBe('0 %');
    expect(porciento(7.5)).toBe('7.5 %');
    expect(porciento('100')).toBe('100 %');
  });
});
