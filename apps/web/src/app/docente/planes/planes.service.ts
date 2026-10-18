import { Injectable, inject } from '@angular/core';
import { Api } from '../../api/api';
import { cambiarGruposPro } from '../../api/fn/planes/cambiar-grupos-pro';
import { crearCheckout } from '../../api/fn/planes/crear-checkout';
import { obtenerPagoSimulado } from '../../api/fn/planes/obtener-pago-simulado';
import { obtenerPlanes } from '../../api/fn/planes/obtener-planes';
import { obtenerSuscripcion } from '../../api/fn/planes/obtener-suscripcion';
import { pagarPagoSimulado } from '../../api/fn/planes/pagar-pago-simulado';
import { portalPagos } from '../../api/fn/planes/portal-pagos';
import { rechazarPagoSimulado } from '../../api/fn/planes/rechazar-pago-simulado';
import { renovarPagoSimulado } from '../../api/fn/planes/renovar-pago-simulado';
import { PlanesDto } from '../../api/models/planes-dto';
import { SesionSimuladaDto } from '../../api/models/sesion-simulada-dto';
import { SuscripcionDto } from '../../api/models/suscripcion-dto';
import { Modalidad } from './planes';

/**
 * SPEC-08: planes y pagos. Pagar siempre sale de la app al Checkout del proveedor (o a la página de pago de
 * prueba sin Stripe); el estado solo cambia cuando el proveedor lo confirma (RN-087).
 */
@Injectable({ providedIn: 'root' })
export class PlanesService {
  private readonly api = inject(Api);
  private precios: Promise<PlanesDto> | null = null;

  /** Los precios casi nunca cambian: se piden una vez por sesión de la app. */
  planes(): Promise<PlanesDto> {
    this.precios ??= this.api.invoke(obtenerPlanes).catch((e: unknown) => {
      this.precios = null;
      throw e;
    });
    return this.precios;
  }

  suscripcion(): Promise<SuscripcionDto> {
    return this.api.invoke(obtenerSuscripcion);
  }

  /** La URL del Checkout; con OXXO, `meses` es 3 o 6. */
  async checkout(
    grupoIds: readonly string[],
    modalidad: Modalidad,
    meses: number | null,
  ): Promise<string> {
    const { url } = await this.api.invoke(crearCheckout, {
      body: { grupoIds: [...grupoIds], modalidad, meses },
    });
    return url;
  }

  cambiarGrupos(grupoIds: readonly string[]): Promise<SuscripcionDto> {
    return this.api.invoke(cambiarGruposPro, { body: { grupoIds: [...grupoIds] } });
  }

  async portal(): Promise<string> {
    const { url } = await this.api.invoke(portalPagos);
    return url;
  }

  pagoSimulado(id: string): Promise<SesionSimuladaDto> {
    return this.api.invoke(obtenerPagoSimulado, { id });
  }

  terminarPagoSimulado(id: string, pagar: boolean): Promise<SesionSimuladaDto> {
    return this.api.invoke(pagar ? pagarPagoSimulado : rechazarPagoSimulado, { id });
  }

  renovarSimulado(pagada: boolean): Promise<void> {
    return this.api.invoke(renovarPagoSimulado, { body: { pagada } });
  }
}
