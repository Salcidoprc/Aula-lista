import { describe, expect, it } from 'vitest';
import { GrupoPlanDto } from '../../api/models/grupo-plan-dto';
import { PlanesDto } from '../../api/models/planes-dto';
import { SuscripcionDto } from '../../api/models/suscripcion-dto';
import {
  cambioGrupos,
  cifra,
  eleccionInicial,
  mensual,
  modoPlan,
  prepagoVigente,
  referenciaEnBloques,
  tienePro,
  totalAPagar,
} from './planes';

const planes: PlanesDto = {
  precioPrimerGrupo: 79,
  precioGrupoAdicional: 29,
  moneda: 'MXN',
  diasPrueba: 14,
  diasMaximoPrueba: 30,
  diasGracia: 7,
  mesesPrepago: [3, 6],
  oxxoHabilitado: true,
  montoMinimoOxxo: 10,
  montoMaximoOxxo: 10000,
  urgentesPorTutorPorMes: 4,
  boletasPorTutorPorPeriodo: 1,
  pagoSimulado: true,
};

const grupo = (id: string, enSuscripcion = false): GrupoPlanDto => ({
  grupoId: id,
  nombre: 'B',
  nivel: 'primaria',
  grado: 4,
  materia: null,
  cicloNombre: '2026-2027',
  alumnosActivos: 30,
  enSuscripcion,
  plan: {
    plan: enSuscripcion ? 'pro' : 'gratis',
    proHasta: null,
    diasPrueba: null,
    pruebaHastaPublicar: false,
    pruebaDisponible: false,
  },
});

const suscripcion = (extra: Partial<SuscripcionDto> = {}): SuscripcionDto => ({
  estado: 'ninguna',
  modalidad: null,
  grupos: [],
  totalMensual: 0,
  proximoCobro: null,
  pagadoHasta: null,
  graciaHasta: null,
  prueba: null,
  pruebaDisponible: true,
  aviso: null,
  puedeAdministrarTarjeta: false,
  renovarPrepago: false,
  agregarAPrepago: false,
  pagoPendiente: null,
  pagos: [],
  ...extra,
});

describe('planes y pagos (SPEC-08)', () => {
  it('RN-082: con 3 grupos Pro el cobro mensual es 79 + 2 × 29 = 137', () => {
    expect(mensual(planes, 3)).toBe(137);
    expect(mensual(planes, 1)).toBe(79);
    expect(mensual(planes, 0)).toBe(0);
  });

  it('RN-084: 2 grupos por 3 meses en OXXO son 3 × 108 = 324', () => {
    expect(totalAPagar(planes, 2, { modalidad: 'oxxo', meses: 3 })).toBe(324);
    expect(totalAPagar(planes, 2, { modalidad: 'tarjeta', meses: null })).toBe(108);
  });

  it('las cifras van sin decimales cuando son enteras y con dos cuando no', () => {
    expect(cifra(137)).toBe('137');
    expect(cifra('54.3')).toBe('54.30');
    expect(cifra(1000)).toBe('1,000');
  });

  it('RN-081: la prueba también da Pro; Gratis no', () => {
    expect(tienePro({ ...grupo('a').plan, plan: 'prueba' })).toBe(true);
    expect(tienePro(grupo('a').plan)).toBe(false);
    expect(tienePro(null)).toBe(false);
  });

  it('la referencia OXXO se lee en bloques de cuatro', () => {
    expect(referenciaEnBloques('12345678901234')).toBe('1234 5678 9012 34');
  });

  it('RN-083: con tarjeta activa o en gracia se cambian los grupos sin volver a pagar', () => {
    expect(modoPlan(suscripcion({ modalidad: 'tarjeta', estado: 'activa' }))).toBe('tarjeta');
    expect(modoPlan(suscripcion({ modalidad: 'tarjeta', estado: 'en_gracia' }))).toBe('tarjeta');
    expect(modoPlan(suscripcion({ modalidad: 'tarjeta', estado: 'cancelada' }))).toBe('contratar');
  });

  it('RN-089: durante el prepago se agregan grupos; al renovar se vuelve a contratar', () => {
    expect(
      modoPlan(suscripcion({ modalidad: 'prepago_oxxo', estado: 'activa', agregarAPrepago: true })),
    ).toBe('agregar');
    expect(
      modoPlan(suscripcion({ modalidad: 'prepago_oxxo', estado: 'activa', renovarPrepago: true })),
    ).toBe('contratar');
  });

  it('RN-089: con el prepago pagado la tarjeta espera a que venza', () => {
    const ahora = new Date('2026-10-18T12:00:00Z');
    const vigente = suscripcion({
      modalidad: 'prepago_oxxo',
      estado: 'activa',
      pagadoHasta: '2026-10-21T12:00:00Z',
    });
    expect(prepagoVigente(vigente, ahora)).toBe(true);
    expect(prepagoVigente({ ...vigente, estado: 'en_gracia' }, ahora)).toBe(false);
    expect(prepagoVigente(vigente, new Date('2026-10-22T00:00:00Z'))).toBe(false);
  });

  it('el cambio separa los grupos que entran de los que salen', () => {
    const s = suscripcion({ grupos: [grupo('a', true), grupo('b', true), grupo('c')] });
    expect(cambioGrupos(s, new Set(['a', 'c']))).toEqual({ agregados: ['c'], quitados: ['b'] });
  });

  it('se abre con lo que ya se paga o, si no hay nada, con el grupo del candado', () => {
    const pagando = suscripcion({ grupos: [grupo('a', true), grupo('b')] });
    expect([...eleccionInicial(pagando, 'b')]).toEqual(['a']);
    const nuevo = suscripcion({ grupos: [grupo('a'), grupo('b')] });
    expect([...eleccionInicial(nuevo, 'b')]).toEqual(['b']);
    expect([...eleccionInicial(nuevo, 'ajeno')]).toEqual([]);
  });
});
