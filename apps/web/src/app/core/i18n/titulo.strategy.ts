import { Injectable, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { take } from 'rxjs';

/** Título de la pestaña por pantalla (WCAG 2.4.2): la ruta da la clave de traducción en `title`. */
@Injectable({ providedIn: 'root' })
export class TituloTraducido extends TitleStrategy {
  private readonly titulo = inject(Title);
  private readonly i18n = inject(TranslocoService);

  override updateTitle(estado: RouterStateSnapshot): void {
    const clave = this.buildTitle(estado);
    this.i18n
      .selectTranslate<string>('app.nombre')
      .pipe(take(1))
      .subscribe((app) => {
        if (!clave) {
          this.titulo.setTitle(app);
          return;
        }
        this.i18n
          .selectTranslate<string>(clave)
          .pipe(take(1))
          .subscribe((pantalla) => this.titulo.setTitle(`${pantalla} · ${app}`));
      });
  }
}
