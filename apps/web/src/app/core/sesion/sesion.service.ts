import { HttpClient, HttpContext, HttpContextToken } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiConfiguration } from '../../api/api-configuration';
import { cerrarSesion } from '../../api/fn/cuentas/cerrar-sesion';
import { cerrarTodasLasSesiones } from '../../api/fn/cuentas/cerrar-todas-las-sesiones';
import { renovarSesion } from '../../api/fn/cuentas/renovar-sesion';
import { RespuestaSesion } from '../../api/models/respuesta-sesion';

/** Marca las llamadas que no deben reintentarse con una sesión renovada (las de la propia sesión). */
export const SIN_RENOVAR = new HttpContextToken<boolean>(() => false);

const CANDADO_RENOVAR = 'aula-lista-renovar-sesion';

/**
 * Sesión del docente (SPEC-01). El token de acceso vive solo en memoria; el de actualización es una
 * cookie HttpOnly que el navegador envía a `/api/v1/cuentas`. La renovación se hace una sola vez a la
 * vez, también entre pestañas: dos renovaciones con la misma cookie se verían como un robo y la API
 * cerraría todas las sesiones.
 */
@Injectable({ providedIn: 'root' })
export class SesionService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ApiConfiguration);
  private readonly token = signal<string | null>(null);
  private readonly docente = signal<string | null>(null);
  private renovacion: Promise<boolean> | null = null;
  private sinCookie = false;

  readonly tokenAcceso = this.token.asReadonly();
  readonly docenteId = this.docente.asReadonly();
  readonly activa = computed(() => this.token() !== null);

  establecer(sesion: RespuestaSesion): void {
    this.sinCookie = false;
    this.token.set(sesion.tokenAcceso);
    this.docente.set(sesion.docenteId);
  }

  /** Asegura un token de acceso: si no hay en memoria, intenta renovar con la cookie. */
  async asegurar(): Promise<boolean> {
    if (this.token()) {
      return true;
    }
    return this.sinCookie ? false : this.renovar();
  }

  /** Rota el token de actualización. Las llamadas simultáneas comparten la misma renovación. */
  renovar(): Promise<boolean> {
    this.renovacion ??= this.conCandado(() => this.renovarAhora()).finally(() => {
      this.renovacion = null;
    });
    return this.renovacion;
  }

  async cerrar(todas = false): Promise<void> {
    const fn = todas ? cerrarTodasLasSesiones : cerrarSesion;
    try {
      await firstValueFrom(fn(this.http, this.config.rootUrl, undefined, this.contexto()));
    } finally {
      this.olvidar();
    }
  }

  /** Borra la sesión local, por ejemplo cuando la API ya la revocó. */
  olvidar(): void {
    this.token.set(null);
    this.docente.set(null);
    this.sinCookie = true;
  }

  private async renovarAhora(): Promise<boolean> {
    const tokenAntes = this.token();
    try {
      const respuesta = await firstValueFrom(
        renovarSesion(this.http, this.config.rootUrl, undefined, this.contexto()),
      );
      this.establecer(respuesta.body);
      return true;
    } catch {
      // Otra pestaña pudo renovar mientras esperábamos el candado; su token ya no es el nuestro.
      if (this.token() !== tokenAntes && this.token() !== null) {
        return true;
      }
      this.olvidar();
      return false;
    }
  }

  private conCandado<T>(accion: () => Promise<T>): Promise<T> {
    const candados = typeof navigator !== 'undefined' ? navigator.locks : undefined;
    return candados ? candados.request(CANDADO_RENOVAR, accion) : accion();
  }

  private contexto(): HttpContext {
    return new HttpContext().set(SIN_RENOVAR, true);
  }
}
