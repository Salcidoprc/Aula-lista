import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { Icono } from '../shared/iconos';
import { CapturaApp } from './captura-app';
import { Capitulo } from './capitulo';
import { DemoCaptura } from './demo-captura';
import { PortadaLanding } from './portada-landing';
import { PreciosLanding } from './precios-landing';
import { PreguntasLanding } from './preguntas-landing';

/**
 * SPEC-01, pantalla 0 (RF-010): la página pública que ve quien llega sin sesión a la raíz. Explica Aulísima
 * con capturas reales de la app, deja probar la captura, muestra los planes del catálogo y lleva a crear
 * cuenta o entrar. Con sesión, la raíz abre Mis grupos y esta página no aparece.
 */
@Component({
  selector: 'app-landing',
  imports: [
    RouterLink,
    TranslocoDirective,
    Icono,
    CapturaApp,
    Capitulo,
    DemoCaptura,
    PortadaLanding,
    PreciosLanding,
    PreguntasLanding,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-container *transloco="let t; prefix: 'landing'">
      <app-portada-landing />

      <section class="bloque antes" aria-labelledby="antes-titulo">
        <div class="cabeza">
          <h2 id="antes-titulo">{{ t('antes.titulo') }}</h2>
          <p>{{ t('antes.entrada') }}</p>
        </div>
        <div class="comparacion" role="table" [attr.aria-label]="t('antes.titulo')">
          <div class="encabezado" role="row">
            <span role="columnheader">{{ t('antes.colAntes') }}</span>
            <span role="columnheader">{{ t('antes.colDespues') }}</span>
          </div>
          @for (i of cinco; track i) {
            <div class="renglon" role="row">
              <span class="hoy" role="cell">{{ t('antes.filas.' + i + '.0') }}</span>
              <span class="con" role="cell">
                <app-icono nombre="siguiente" />{{ t('antes.filas.' + i + '.1') }}
              </span>
            </div>
          }
        </div>
      </section>

      <section id="funciones" class="bloque" aria-labelledby="funciones-titulo">
        <div class="cabeza">
          <h2 id="funciones-titulo">{{ t('capitulos.titulo') }}</h2>
          <p>{{ t('capitulos.entrada') }} {{ t('capitulos.ejemplo') }}.</p>
        </div>
        <div class="capitulos">
          <app-capitulo
            [numero]="1"
            grado="p1"
            [titulo]="t('capitulos.uno.titulo')"
            [texto]="t('capitulos.uno.texto')"
            [puntos]="puntos(t, 'uno')"
          >
            <app-captura-app
              nombre="cel-importar-revision"
              tipo="cel"
              [alt]="t('capitulos.uno.alt')"
            />
          </app-capitulo>
          <app-capitulo
            [numero]="2"
            grado="p2"
            [invertido]="true"
            [titulo]="t('capitulos.dos.titulo')"
            [texto]="t('capitulos.dos.texto')"
            [puntos]="puntos(t, 'dos')"
          >
            <app-demo-captura />
          </app-capitulo>
          <app-capitulo
            [numero]="3"
            grado="p3"
            [titulo]="t('capitulos.tres.titulo')"
            [texto]="t('capitulos.tres.texto')"
            [puntos]="puntos(t, 'tres')"
          >
            <app-captura-app
              nombre="cel-asistencia"
              tipo="cel"
              [oscuro]="true"
              [alt]="t('capitulos.tres.alt')"
            />
          </app-capitulo>
          <app-capitulo
            [numero]="4"
            grado="p4"
            [invertido]="true"
            [titulo]="t('capitulos.cuatro.titulo')"
            [texto]="t('capitulos.cuatro.texto')"
            [puntos]="puntos(t, 'cuatro')"
          >
            <app-captura-app
              class="solo-angosto"
              nombre="cel-revisar"
              tipo="cel"
              [oscuro]="true"
              [alt]="t('capitulos.cuatro.alt')"
            />
            <app-captura-app
              class="solo-ancho"
              nombre="esc-revisar"
              tipo="esc"
              [alt]="t('capitulos.cuatro.alt')"
            />
          </app-capitulo>
          <app-capitulo
            [numero]="5"
            grado="p5"
            [titulo]="t('capitulos.cinco.titulo')"
            [texto]="t('capitulos.cinco.texto')"
            [puntos]="puntos(t, 'cinco')"
          >
            <div class="dupla">
              <app-captura-app
                class="solo-ancho"
                nombre="esc-boletas"
                tipo="esc"
                [alt]="t('capitulos.cinco.altEsc')"
              />
              <app-captura-app
                class="tel"
                nombre="cel-tutor-boleta"
                tipo="cel"
                [oscuro]="true"
                [alt]="t('capitulos.cinco.alt')"
              />
            </div>
          </app-capitulo>
          <app-capitulo
            [numero]="6"
            grado="p6"
            [invertido]="true"
            [titulo]="t('capitulos.seis.titulo')"
            [texto]="t('capitulos.seis.texto')"
            [puntos]="puntos(t, 'seis')"
          >
            <app-captura-app nombre="cel-aviso-nuevo" tipo="cel" [alt]="t('capitulos.seis.alt')" />
          </app-capitulo>
        </div>
      </section>

      <section class="bloque fuertes" aria-labelledby="fuertes-titulo">
        <h2 id="fuertes-titulo">{{ t('fuertes.titulo') }}</h2>
        <dl>
          @for (i of cinco; track i) {
            <div>
              <dt>{{ t('fuertes.filas.' + i + '.0') }}</dt>
              <dd>{{ t('fuertes.filas.' + i + '.1') }}</dd>
            </div>
          }
        </dl>
      </section>

      <section class="privacidad" aria-labelledby="privacidad-titulo">
        <div class="bloque">
          <h2 id="privacidad-titulo"><app-icono nombre="candado" />{{ t('privacidad.titulo') }}</h2>
          <ul>
            @for (i of [0, 1, 2]; track i) {
              <li>
                <strong>{{ t('privacidad.puntos.' + i + '.0') }}</strong>
                <span>{{ t('privacidad.puntos.' + i + '.1') }}</span>
              </li>
            }
          </ul>
          <a class="enlace" routerLink="/legal/privacidad">{{ t('privacidad.aviso') }}</a>
        </div>
      </section>

      <section id="precios" class="bloque" aria-labelledby="precios-titulo">
        <div class="cabeza">
          <h2 id="precios-titulo">{{ t('precios.titulo') }}</h2>
          <p>{{ t('precios.entrada') }}</p>
        </div>
        <app-precios-landing />
      </section>

      <section id="preguntas" class="bloque angosto" aria-labelledby="preguntas-titulo">
        <h2 id="preguntas-titulo">{{ t('preguntas.titulo') }}</h2>
        <app-preguntas-landing />
      </section>

      <section class="final" aria-labelledby="final-titulo">
        <div class="bloque">
          <h2 id="final-titulo">{{ t('final.titulo') }}</h2>
          <p>{{ t('final.entrada') }}</p>
          <div class="acciones">
            <a class="boton crear" routerLink="/cuenta/registro">{{ t('final.crear') }}</a>
            <a class="ya" routerLink="/cuenta/entrar">{{ t('final.entrar') }}</a>
          </div>
        </div>
      </section>

      <footer class="pie">
        <div class="bloque">
          <p>
            <strong>Aulísima</strong><br />
            {{ t('pie.lema') }}
          </p>
          <nav [attr.aria-label]="t('pie.lema')">
            <a routerLink="/legal/privacidad">{{ t('pie.privacidad') }}</a>
            <a routerLink="/legal/terminos">{{ t('pie.terminos') }}</a>
            <a routerLink="/cuenta/entrar">{{ t('pie.entrar') }}</a>
          </nav>
          <p class="derechos cifras">{{ t('pie.derechos', { anio: anio }) }}</p>
        </div>
      </footer>
    </ng-container>
  `,
  styleUrl: './landing.css',
})
export class Landing {
  protected readonly cinco = [0, 1, 2, 3, 4];
  protected readonly anio = new Date().getFullYear();

  /** Los tres puntos de un capítulo. */
  protected puntos(t: (clave: string) => string, capitulo: string): string[] {
    return [0, 1, 2].map((i) => t(`capitulos.${capitulo}.puntos.${i}`));
  }
}
