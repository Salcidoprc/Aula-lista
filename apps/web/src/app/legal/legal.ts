import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';

const DOCUMENTOS = ['terminos', 'privacidad', 'declaracion_datos'] as const;

/**
 * Términos, aviso de privacidad y declaración de datos. El texto definitivo lo entrega el abogado
 * externo (PA-11); mientras tanto la página dice que está en revisión, sin inventar cláusulas.
 */
@Component({
  selector: 'app-legal',
  imports: [TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article class="pantalla pantalla--ancha" *transloco="let t; prefix: 'legal'">
      <h1>{{ t(clave() + '.titulo') }}</h1>
      <p class="entrada">{{ t(clave() + '.resumen') }}</p>
      <p class="aviso aviso--pendiente">{{ t('enRevision') }}</p>
    </article>
  `,
})
export class Legal {
  readonly documento = input.required<string>();

  protected readonly clave = computed(() =>
    (DOCUMENTOS as readonly string[]).includes(this.documento()) ? this.documento() : 'terminos',
  );
}
