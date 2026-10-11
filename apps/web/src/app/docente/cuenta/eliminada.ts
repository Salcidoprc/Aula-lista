import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';

/** Confirmación después de pedir la eliminación (RN-006): la cuenta ya no entra y los datos se borran a los 30 días. */
@Component({
  selector: 'app-cuenta-eliminada',
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="pantalla" *transloco="let t; prefix: 'cuenta.eliminada'">
      <h1>{{ t('titulo') }}</h1>
      <p class="entrada">{{ t('texto') }}</p>
      <p>{{ t('nota') }}</p>
    </section>
  `,
})
export class CuentaEliminada {}
