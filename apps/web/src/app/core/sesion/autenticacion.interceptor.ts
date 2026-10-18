import {
  HttpErrorResponse,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { SIN_RENOVAR, SesionService } from './sesion.service';

/**
 * Agrega el token de acceso a las llamadas a la API y, si vence, renueva la sesión y reintenta una vez. El portal
 * del tutor (SPEC-06) va con su propia cookie: ni lleva el token del docente ni un 401 suyo lo manda a entrar.
 */
export const autenticacionInterceptor: HttpInterceptorFn = (solicitud, siguiente) => {
  const sesion = inject(SesionService);
  const router = inject(Router);
  if (!solicitud.url.startsWith('/api/') || solicitud.url.startsWith('/api/v1/tutor/')) {
    return siguiente(solicitud);
  }

  return siguiente(conToken(solicitud, sesion.tokenAcceso())).pipe(
    catchError((error: unknown) => {
      if (
        !(error instanceof HttpErrorResponse) ||
        error.status !== 401 ||
        solicitud.context.get(SIN_RENOVAR)
      ) {
        return throwError(() => error);
      }
      return from(sesion.renovar()).pipe(
        switchMap((renovada) => {
          if (!renovada) {
            void router.navigate(['/cuenta/entrar'], { queryParams: { volver: router.url } });
            return throwError(() => error);
          }
          return reintentar(solicitud, siguiente, sesion.tokenAcceso());
        }),
      );
    }),
  );
};

function conToken(solicitud: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? solicitud.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : solicitud;
}

function reintentar(
  solicitud: HttpRequest<unknown>,
  siguiente: HttpHandlerFn,
  token: string | null,
) {
  return siguiente(conToken(solicitud, token));
}
