import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { Icono } from '../shared/iconos';
import { Marca } from '../shared/marca';
import { ClaveGrado } from '../shared/portada/portada';
import { Aparece } from './aparece';
import { CapturaApp } from './captura-app';

/** Los nueve grados con el alto de su libro en el estante, para que no parezcan una gráfica de barras. */
const LIBROS: readonly {
  grado: ClaveGrado;
  n: number;
  nivel: 'primaria' | 'secundaria';
  alto: number;
}[] = [
  { grado: 'p1', n: 1, nivel: 'primaria', alto: 100 },
  { grado: 'p2', n: 2, nivel: 'primaria', alto: 91 },
  { grado: 'p3', n: 3, nivel: 'primaria', alto: 96 },
  { grado: 'p4', n: 4, nivel: 'primaria', alto: 87 },
  { grado: 'p5', n: 5, nivel: 'primaria', alto: 99 },
  { grado: 'p6', n: 6, nivel: 'primaria', alto: 93 },
  { grado: 's1', n: 1, nivel: 'secundaria', alto: 89 },
  { grado: 's2', n: 2, nivel: 'secundaria', alto: 97 },
  { grado: 's3', n: 3, nivel: 'secundaria', alto: 92 },
];

/**
 * Primera pantalla de la landing (RF-010): la portada de la app crecida a toda la página, con el 10 enorme y
 * recortado en lugar del número del grado, el registro a la vista y la app real cruzando el borde de la
 * portada. Debajo, el estante con un libro por grado. El único momento de movimiento de la página: el texto
 * entra escalonado y la app sube un poco después (`@starting-style`); con movimiento reducido, solo funde.
 */
@Component({
  selector: 'app-portada-landing',
  imports: [RouterLink, TranslocoDirective, Icono, Marca, Aparece, CapturaApp],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t; prefix: 'landing'">
      <div class="portada">
        <nav class="barra" [attr.aria-label]="t('nav.etiqueta')">
          <a class="logo" routerLink="/"><app-marca /></a>
          <div class="enlaces">
            <a href="#funciones">{{ t('nav.funciones') }}</a>
            <a href="#precios">{{ t('nav.precios') }}</a>
            <a href="#preguntas">{{ t('nav.preguntas') }}</a>
          </div>
          <a class="entrar" routerLink="/cuenta/entrar">{{ t('nav.entrar') }}</a>
        </nav>

        <div class="titular">
          <span class="diez cifras" aria-hidden="true">10</span>
          <h1>
            <span>{{ t('hero.titulo1') }}</span> <span>{{ t('hero.titulo2') }}</span>
          </h1>
          <p class="entrada">{{ t('hero.entrada') }}</p>
          <div class="acciones">
            <a class="boton crear" routerLink="/cuenta/registro">{{ t('hero.crear') }}</a>
            <a class="ya" routerLink="/cuenta/entrar">{{ t('hero.entrar') }}</a>
          </div>
          <ul class="datos">
            @for (i of [0, 1, 2]; track i) {
              <li><app-icono nombre="guardado" />{{ t('hero.datos.' + i) }}</li>
            }
          </ul>
        </div>

        <div class="escena">
          <app-captura-app
            class="compu"
            nombre="esc-mis-grupos"
            tipo="esc"
            [oscuro]="true"
            [prioridad]="true"
            [alt]="t('hero.capturaEsc')"
          />
          <app-captura-app
            class="tel"
            nombre="cel-captura"
            tipo="cel"
            [oscuro]="true"
            [prioridad]="true"
            [alt]="t('hero.capturaCel')"
          />
        </div>
      </div>

      <section class="estante" aria-labelledby="estante-titulo" appAparece>
        <h2 id="estante-titulo" class="revela">{{ t('estante.titulo') }}</h2>
        <ul>
          @for (libro of libros; track libro.grado; let n = $index) {
            <li
              class="revela"
              [attr.data-grado]="libro.grado"
              [style.--alto.%]="libro.alto"
              [style.--i]="n + 1"
            >
              <span class="cifras">{{
                t('estante.grado', { n: libro.n, nivel: t('estante.' + libro.nivel) })
              }}</span>
            </li>
          }
        </ul>
      </section>
    </ng-container>
  `,
  styleUrl: './portada-landing.css',
})
export class PortadaLanding {
  protected readonly libros = LIBROS;
}
