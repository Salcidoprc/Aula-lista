import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Api } from '../api/api';
import { canjearEnlaceTutor } from '../api/fn/portal-del-tutor/canjear-enlace-tutor';
import { desuscribirPushTutor } from '../api/fn/portal-del-tutor/desuscribir-push-tutor';
import { guardarCorreoTutor } from '../api/fn/portal-del-tutor/guardar-correo-tutor';
import { leerAvisosHijoTutor } from '../api/fn/portal-del-tutor/leer-avisos-hijo-tutor';
import { listarAvisosHijoTutor } from '../api/fn/portal-del-tutor/listar-avisos-hijo-tutor';
import { listarHijosTutor } from '../api/fn/portal-del-tutor/listar-hijos-tutor';
import { obtenerContactoTutor } from '../api/fn/portal-del-tutor/obtener-contacto-tutor';
import { obtenerHijoTutor } from '../api/fn/portal-del-tutor/obtener-hijo-tutor';
import { suscribirPushTutor } from '../api/fn/portal-del-tutor/suscribir-push-tutor';
import { AvisoTutorDto } from '../api/models/aviso-tutor-dto';
import { ContactoTutorDto } from '../api/models/contacto-tutor-dto';
import { DetalleHijoDto } from '../api/models/detalle-hijo-dto';
import { MisHijosDto } from '../api/models/mis-hijos-dto';

/** La sesión del tutor ya no sirve (enlace revocado o vencido, o nunca abrió uno en este navegador). */
export function sinSesion(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 401;
}

/**
 * SPEC-06: el portal del tutor. La sesión es una cookie HttpOnly que el navegador manda sola; aquí solo se canjea
 * el enlace y se guardan en memoria las respuestas, para que pasar de la tarjeta a la boleta no vuelva a la red.
 */
@Injectable({ providedIn: 'root' })
export class PortalService {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private hijos: Promise<MisHijosDto> | null = null;
  private readonly detalles = new Map<string, Promise<DetalleHijoDto>>();

  async canjear(token: string): Promise<void> {
    this.olvidar();
    await this.api.invoke(canjearEnlaceTutor, { body: { token } });
  }

  misHijos(): Promise<MisHijosDto> {
    this.hijos ??= this.api.invoke(listarHijosTutor).catch((error: unknown) => {
      this.hijos = null;
      throw error;
    });
    return this.hijos;
  }

  detalle(id: string): Promise<DetalleHijoDto> {
    let detalle = this.detalles.get(id);
    if (!detalle) {
      detalle = this.api.invoke(obtenerHijoTutor, { id }).catch((error: unknown) => {
        this.detalles.delete(id);
        throw error;
      });
      this.detalles.set(id, detalle);
    }
    return detalle;
  }

  /** SPEC-07: los avisos del grupo de este hijo, del más nuevo al más viejo (RF-063). */
  avisos(id: string): Promise<AvisoTutorDto[]> {
    return this.api.invoke(listarAvisosHijoTutor, { id });
  }

  /** Abrir la pestaña cuenta como leído (RN-075); la portada y el detalle se vuelven a pedir con el conteo nuevo. */
  async leerAvisos(id: string): Promise<void> {
    await this.api.invoke(leerAvisosHijoTutor, { id });
    this.olvidar();
  }

  contacto(): Promise<ContactoTutorDto> {
    return this.api.invoke(obtenerContactoTutor);
  }

  /** Vacío lo quita (RF-064). */
  guardarCorreo(correo: string): Promise<ContactoTutorDto> {
    return this.api.invoke(guardarCorreoTutor, { body: { correo: correo.trim() || null } });
  }

  /** La suscripción del navegador; las claves viajan como las da `PushSubscription.toJSON()`. */
  async suscribirPush(suscripcion: PushSubscriptionJSON): Promise<void> {
    await this.api.invoke(suscribirPushTutor, {
      body: {
        endpoint: suscripcion.endpoint ?? null,
        p256dh: suscripcion.keys?.['p256dh'] ?? null,
        auth: suscripcion.keys?.['auth'] ?? null,
      },
    });
  }

  async desuscribirPush(endpoint: string): Promise<void> {
    await this.api.invoke(desuscribirPushTutor, { body: { endpoint } });
  }

  /** Con 401 lleva a "Enlace inválido" sin dejar la pantalla rota en el historial; devuelve si lo hizo. */
  manejarSinSesion(error: unknown): boolean {
    if (!sinSesion(error)) {
      return false;
    }
    this.olvidar();
    void this.router.navigate(['/tutor/enlace-invalido'], { replaceUrl: true });
    return true;
  }

  private olvidar(): void {
    this.hijos = null;
    this.detalles.clear();
  }
}

/** URL del PDF de una boleta: la cookie del tutor la autoriza, sin pasar por JavaScript. */
export function urlPdf(boletaId: string): string {
  return `/api/v1/boletas/${boletaId}/pdf`;
}
