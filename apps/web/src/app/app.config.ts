import { registerLocaleData } from '@angular/common';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import localeEsMx from '@angular/common/locales/es-MX';
import {
  ApplicationConfig,
  ErrorHandler,
  LOCALE_ID,
  isDevMode,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import {
  ActivatedRouteSnapshot,
  TitleStrategy,
  provideRouter,
  withComponentInputBinding,
  withViewTransitions,
} from '@angular/router';
import { provideServiceWorker } from '@angular/service-worker';
import { provideTransloco } from '@jsverse/transloco';
import { routes } from './app.routes';
import { TranslocoHttpLoader } from './core/i18n/transloco-loader';
import { TituloTraducido } from './core/i18n/titulo.strategy';
import { ErroresConFaro } from './core/observabilidad/faro';
import { autenticacionInterceptor } from './core/sesion/autenticacion.interceptor';

registerLocaleData(localeEsMx);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    { provide: ErrorHandler, useClass: ErroresConFaro },
    provideRouter(
      routes,
      withComponentInputBinding(),
      // Cambiar solo la consulta (?fecha=, ?unidad=) es pasar de día o de unidad: se repite y no se anima.
      withViewTransitions({
        onViewTransitionCreated: ({ transition, from, to }) => {
          if (mismaPantalla(from, to)) {
            transition.skipTransition();
          }
        },
      }),
    ),
    provideHttpClient(withFetch(), withInterceptors([autenticacionInterceptor])),
    { provide: LOCALE_ID, useValue: 'es-MX' },
    { provide: TitleStrategy, useClass: TituloTraducido },
    provideTransloco({
      config: {
        availableLangs: ['es-MX'],
        defaultLang: 'es-MX',
        reRenderOnLangChange: false,
        prodMode: !isDevMode(),
      },
      loader: TranslocoHttpLoader,
    }),
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
  ],
};

function hoja(ruta: ActivatedRouteSnapshot): ActivatedRouteSnapshot {
  return ruta.firstChild ? hoja(ruta.firstChild) : ruta;
}

function mismaPantalla(a: ActivatedRouteSnapshot, b: ActivatedRouteSnapshot): boolean {
  const [x, y] = [hoja(a), hoja(b)];
  return x.routeConfig === y.routeConfig && JSON.stringify(x.params) === JSON.stringify(y.params);
}
