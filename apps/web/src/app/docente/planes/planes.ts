import { PlanGrupoDto } from '../../api/models/plan-grupo-dto';
import { PlanesDto } from '../../api/models/planes-dto';
import { SuscripcionDto } from '../../api/models/suscripcion-dto';

export type Modalidad = 'tarjeta' | 'oxxo';

/** Cómo paga el docente: tarjeta cada mes, u OXXO por adelantado (RN-084). */
export interface FormaDePago {
  readonly modalidad: Modalidad;
  readonly meses: number | null;
}

/** RN-082: el primer grupo a su precio y cada adicional al suyo. Ninguno no cuesta nada. */
export function mensual(planes: PlanesDto, grupos: number): number {
  if (grupos <= 0) {
    return 0;
  }
  return Number(planes.precioPrimerGrupo) + Number(planes.precioGrupoAdicional) * (grupos - 1);
}

/** Lo que se paga hoy: el mes con tarjeta o los meses del prepago OXXO. */
export function totalAPagar(planes: PlanesDto, grupos: number, forma: FormaDePago): number {
  return mensual(planes, grupos) * (forma.modalidad === 'oxxo' ? (forma.meses ?? 1) : 1);
}

/** 137 → "137"; 54.33 → "54.33". Sin símbolo: la moneda va escrita al lado ("137 MXN"). */
export function cifra(monto: number | string): string {
  const valor = Number(monto);
  return Number.isInteger(valor)
    ? valor.toLocaleString('es-MX')
    : valor.toLocaleString('es-MX', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

/** Si el grupo tiene Pro hoy: pagado o en su prueba (RN-081). */
export function tienePro(plan: PlanGrupoDto | null | undefined): boolean {
  return !!plan && plan.plan !== 'gratis';
}

/** "4433 2211 0099 88": la referencia OXXO en bloques de cuatro, como se dicta en caja. */
export function referenciaEnBloques(referencia: string): string {
  return referencia.replace(/\s+/g, '').replace(/(.{4})(?=.)/g, '$1 ');
}

/**
 * Qué hace la pantalla Plan con los grupos elegidos. `tarjeta`: cambia la suscripción (prorrateo, RN-083).
 * `agregar`: durante un prepago, lo nuevo se paga hasta su fin y quitar no reembolsa (RN-089). `contratar`: el
 * Checkout con tarjeta u OXXO; también al renovar un prepago o al volver tras la gracia.
 */
export type ModoPlan = 'contratar' | 'tarjeta' | 'agregar';

export function modoPlan(s: SuscripcionDto): ModoPlan {
  if (s.modalidad === 'tarjeta' && (s.estado === 'activa' || s.estado === 'en_gracia')) {
    return 'tarjeta';
  }
  return s.agregarAPrepago ? 'agregar' : 'contratar';
}

/** El prepago sigue pagado: con tarjeta se paga hasta que venza (RN-089). */
export function prepagoVigente(s: SuscripcionDto, ahora: Date = new Date()): boolean {
  return (
    s.modalidad === 'prepago_oxxo' &&
    s.estado === 'activa' &&
    !!s.pagadoHasta &&
    new Date(s.pagadoHasta) > ahora
  );
}

export interface CambioGrupos {
  readonly agregados: readonly string[];
  readonly quitados: readonly string[];
}

/** Contra lo que ya se paga: qué grupos entran y cuáles salen. */
export function cambioGrupos(s: SuscripcionDto, elegidos: ReadonlySet<string>): CambioGrupos {
  const pagados = new Set(s.grupos.filter((g) => g.enSuscripcion).map((g) => g.grupoId));
  return {
    agregados: [...elegidos].filter((id) => !pagados.has(id)),
    quitados: [...pagados].filter((id) => !elegidos.has(id)),
  };
}

/** Lo elegido al abrir: lo que ya se paga; si no hay nada, el grupo con el que llegó desde un candado. */
export function eleccionInicial(s: SuscripcionDto, grupo: string | null): Set<string> {
  const pagados = s.grupos.filter((g) => g.enSuscripcion).map((g) => g.grupoId);
  if (pagados.length > 0) {
    return new Set(pagados);
  }
  return new Set(grupo && s.grupos.some((g) => g.grupoId === grupo) ? [grupo] : []);
}
