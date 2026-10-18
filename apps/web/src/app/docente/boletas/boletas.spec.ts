import { describe, expect, it } from 'vitest';
import { BoletaAlumnoDto } from '../../api/models/boleta-alumno-dto';
import {
  avancePreparacion,
  calificacionFinal,
  porCorregir,
  porGenerar,
  publicables,
  resumenPublicacion,
} from './boletas';

const alumno = (
  id: string,
  estado: string,
  extra: Partial<BoletaAlumnoDto> = {},
): BoletaAlumnoDto => ({
  alumnoId: id,
  numeroLista: id,
  nombres: 'N',
  apellidos: 'A',
  calificacion: 9,
  estado,
  observacionId: `o${id}`,
  boletaId: estado === 'sin_aprobar' || estado === 'sin_boleta' ? null : `b${id}`,
  version: 1,
  publicadaEn: null,
  desactualizada: false,
  falloPdf: false,
  tutoresWhatsapp: 1,
  tutoresCorreo: 0,
  ...extra,
});

describe('boletas (SPEC-05)', () => {
  it('genera las que faltan y las listas desactualizadas, no las publicadas (RF-050, RN-053)', () => {
    const alumnos = [
      alumno('1', 'sin_boleta'),
      alumno('2', 'lista', { desactualizada: true }),
      alumno('3', 'lista'),
      alumno('4', 'publicada', { desactualizada: true }),
      alumno('5', 'sin_aprobar'),
    ];
    expect(porGenerar(alumnos).map((a) => a.alumnoId)).toEqual(['1', '2']);
    expect(publicables(alumnos).map((a) => a.alumnoId)).toEqual(['3']);
  });

  it('solo ofrece corregir una publicada que cambió (RN-052)', () => {
    expect(porCorregir(alumno('1', 'publicada', { desactualizada: true }))).toBe(true);
    expect(porCorregir(alumno('1', 'publicada'))).toBe(false);
    expect(porCorregir(alumno('1', 'lista', { desactualizada: true }))).toBe(false);
  });

  it('resume tutores por WhatsApp, por correo y alumnos sin contacto antes de publicar', () => {
    const r = resumenPublicacion([
      alumno('1', 'lista', { tutoresWhatsapp: 2, tutoresCorreo: 1 }),
      alumno('2', 'lista', { tutoresWhatsapp: 0, tutoresCorreo: 0 }),
      alumno('3', 'lista'),
    ]);
    expect(r).toMatchObject({ boletas: 3, whatsapp: 3, correo: 1 });
    expect(r.sinContacto.map((a) => a.alumnoId)).toEqual(['2']);
  });

  it('mide el avance real de la tanda que se prepara', () => {
    expect(avancePreparacion(30, 12)).toEqual({ listas: 18, fraccion: 0.6 });
    expect(avancePreparacion(0, 0).fraccion).toBe(1);
    expect(avancePreparacion(3, 5).listas).toBe(0);
  });

  it('muestra la calificación final con los decimales del grupo', () => {
    expect(calificacionFinal(9.2, 1)).toBe('9.2');
    expect(calificacionFinal('8.456', 2)).toBe('8.46');
    expect(calificacionFinal(null, 1)).toBe('—');
  });
});
