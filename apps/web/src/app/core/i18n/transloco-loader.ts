import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Translation, TranslocoLoader } from '@jsverse/transloco';

/** Carga los textos de `public/i18n/<idioma>.json`. Ningún texto visible vive en las plantillas. */
@Injectable({ providedIn: 'root' })
export class TranslocoHttpLoader implements TranslocoLoader {
  private readonly http = inject(HttpClient);

  getTranslation(idioma: string) {
    return this.http.get<Translation>(`/i18n/${idioma}.json`);
  }
}
