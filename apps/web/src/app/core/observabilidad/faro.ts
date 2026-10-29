import { ErrorHandler, Injectable } from '@angular/core';
import { limpiarEnvio } from './limpieza';

/** Lo que nginx publica en `/observabilidad.json` cuando el servidor web tiene `FARO_URL` (ADR-0015). */
interface ConfiguracionObservabilidad {
  faro: boolean;
  entorno?: string;
}

type ReportarError = (error: Error) => void;

let reportar: ReportarError | null = null;

/**
 * SPEC-09: errores y rendimiento del navegador con Grafana Faro, solo si el servidor lo activa. Se carga con `import()`
 * para no pesar en el bundle inicial (RNF-011) y manda a `/faro`, del mismo origen (CSP `connect-src 'self'`). Sin
 * consola, sin usuario y sin tiempos de cada recurso; cada envío pasa por {@link limpiarEnvio} (RN-019).
 */
export async function iniciarFaro(): Promise<void> {
  let configuracion: ConfiguracionObservabilidad;
  try {
    const respuesta = await fetch('/observabilidad.json', { cache: 'no-store' });
    configuracion = respuesta.ok ? await respuesta.json() : { faro: false };
  } catch {
    return; // sin servidor que diga sí (desarrollo, pruebas), Faro no se carga
  }
  if (configuracion?.faro !== true) {
    return;
  }

  const { initializeFaro, SessionInstrumentation, ViewInstrumentation, WebVitalsInstrumentation } =
    await import('@grafana/faro-web-sdk');
  const faro = initializeFaro({
    url: '/faro',
    app: { name: 'aula-lista-web', environment: configuracion.entorno ?? 'produccion' },
    // Los errores llegan por el ErrorHandler de Angular, que también recibe los globales: así no se cuentan dos veces.
    instrumentations: [
      new SessionInstrumentation(),
      new ViewInstrumentation(),
      new WebVitalsInstrumentation(),
    ],
    beforeSend: (item) => limpiarEnvio(item),
  });
  reportar = (error) => faro.api.pushError(error);
}

/** Manda a Faro los errores que Angular atrapa (y los globales, por provideBrowserGlobalErrorListeners). */
@Injectable()
export class ErroresConFaro extends ErrorHandler {
  override handleError(error: unknown): void {
    if (reportar) {
      try {
        reportar(error instanceof Error ? error : new Error(String(error)));
      } catch {
        // Faro nunca debe romper la app.
      }
    }
    super.handleError(error);
  }
}
