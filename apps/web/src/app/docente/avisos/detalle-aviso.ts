import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { AvisoDto } from '../../api/models/aviso-dto';
import { EntregaDto } from '../../api/models/entrega-dto';
import { GrupoDto } from '../../api/models/grupo-dto';
import { codigoDeError } from '../../core/errores/problema';
import { urlWhatsApp } from '../alumnos/ficha-alumno';
import { AvisosService } from '../../shared/avisos/avisos.service';
import { claveGrado, nombreGrupo } from '../../shared/grados';
import { Icono } from '../../shared/iconos';
import { Ocupado } from '../../shared/ocupado';
import { forma } from '../../shared/plural';
import { controlarPortada } from '../../shared/portada/portada.service';
import { portadaDeGrupo } from '../grupos/grupos.service';
import { fechaHoraAviso, fraccionLeidos, nombresAlumnos, porCompartir } from './avisos';
import { AvisosGrupoService } from './avisos.service';

/** RF-073: mientras la pantalla está a la vista, las lecturas se actualizan solas. */
const REFRESCO_MS = 20_000;

/**
 * SPEC-07, "Detalle del aviso" (RF-072, RF-073): el aviso como lo leen los tutores, quién ya lo leyó y por dónde le
 * llegó a cada uno. Arriba, los tutores sin ningún canal para que el docente se los comparta desde su WhatsApp
 * (RN-073). Borrar lo quita del portal y queda en la auditoría (RN-076).
 */
@Component({
  selector: 'app-detalle-aviso',
  imports: [TranslocoDirective, RouterLink, Icono, Ocupado],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pantalla" [attr.data-grado]="clave() ?? null" *transloco="let t; prefix: 'avisos'">
      @if (aviso(); as a) {
        <a class="volver" [routerLink]="['/grupos', a.grupoId, 'avisos']">
          <app-icono nombre="volver" />
          {{ t('titulo') }}
        </a>
      } @else {
        <span class="volver-esqueleto"></span>
      }

      @if (error(); as codigo) {
        <div class="aviso aviso--error" role="alert">
          <p>{{ texto('errores.' + codigo) }}</p>
          <button class="boton boton--secundario" type="button" (click)="cargar()">
            {{ t('reintentar') }}
          </button>
        </div>
      } @else if (aviso(); as a) {
        <article class="leido-aviso" aria-labelledby="aviso-titulo">
          <header>
            @if (a.urgente) {
              <span class="etiqueta-urgente">{{ t('urgente') }}</span>
            }
            <h1 id="aviso-titulo" tabindex="-1">{{ a.titulo }}</h1>
            <p class="dato-suave cifras">
              {{ t('enviadoEl', { fecha: fechaHora(a.creadoEn) }) }} ·
              {{
                a.paraTodos
                  ? t('paraTodos')
                  : t('paraElegidos.' + forma(a.destinatarios), { n: a.destinatarios })
              }}
            </p>
          </header>
          <p class="cuerpo">{{ a.cuerpo }}</p>
        </article>

        <section class="lectura" aria-labelledby="lectura-titulo">
          <h2 id="lectura-titulo" class="cifras">
            {{ t('leidoPor', { leidos: a.leidos, total: a.destinatarios }) }}
          </h2>
          <span class="barra" aria-hidden="true">
            <span [style.transform]="'scaleX(' + fraccion() + ')'"></span>
          </span>
        </section>

        @if (aCompartir().length > 0) {
          <section class="compartir" aria-labelledby="compartir-titulo">
            <h2 id="compartir-titulo">{{ t('compartelo.titulo') }}</h2>
            <p class="dato-suave">{{ t('compartelo.texto') }}</p>
            <ul class="filas">
              @for (e of aCompartir(); track e.tutorId) {
                <li class="fila-compartir">
                  <span class="quien">
                    <span class="nombre">{{ e.nombre }}</span>
                    <span class="dato-suave">{{ alumnos(e) }}</span>
                    @if (e.compartidoEn) {
                      <span class="compartido cifras">
                        <app-icono nombre="guardado" />
                        {{ t('compartelo.compartido', { fecha: fechaHora(e.compartidoEn) }) }}
                      </span>
                    }
                  </span>
                  <button
                    class="boton"
                    type="button"
                    [class.boton--secundario]="!!e.compartidoEn"
                    [disabled]="!enLinea() || !e.telefono"
                    [appOcupado]="compartiendo() === e.tutorId"
                    (click)="compartir(a, e)"
                  >
                    <app-icono nombre="mensaje" />
                    {{ e.compartidoEn ? t('compartelo.otraVez') : t('compartelo.accion') }}
                    <span class="solo-lector">{{ e.nombre }}</span>
                  </button>
                </li>
              }
            </ul>
            @if (errorCompartir(); as codigo) {
              <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
            }
          </section>
        }

        <section class="entregas" aria-labelledby="entregas-titulo">
          <h2 id="entregas-titulo">{{ t('porTutor') }}</h2>
          <ul class="filas">
            @for (e of a.entregas; track e.tutorId) {
              <li class="entrega">
                <span class="quien">
                  <span class="nombre">{{ e.nombre ?? t('tutorSinDatos') }}</span>
                  <span class="dato-suave">{{ alumnos(e) }}</span>
                </span>
                <span class="estado cifras" [class.leido]="!!e.leidoEn">
                  @if (e.leidoEn) {
                    {{ t('leidoEl', { fecha: fechaHora(e.leidoEn) }) }}
                  } @else {
                    {{ t('sinLeer') }}
                  }
                </span>
                <span class="canales" [attr.aria-label]="t('canales.nombre')">
                  @if (e.push) {
                    <span class="canal">{{ t('canales.push') }}</span>
                  }
                  @if (e.correo) {
                    <span class="canal">{{ t('canales.correo') }}</span>
                  }
                  @if (e.whatsapp) {
                    <span class="canal" [class.fallido]="e.estadoWhatsapp === 'fallido'">
                      {{ t('canales.whatsapp') }}
                      @if (e.estadoWhatsapp; as estado) {
                        · {{ t('estadosWhatsapp.' + estado) }}
                      }
                    </span>
                  }
                  @if (e.sinSaldoWhatsapp) {
                    <span class="canal canal--falta">{{ t('canales.sinSaldo') }}</span>
                  }
                  @if (e.porCompartir) {
                    <span class="canal canal--falta">
                      {{ e.compartidoEn ? t('canales.compartido') : t('canales.compartir') }}
                    </span>
                  }
                </span>
              </li>
            }
          </ul>
        </section>

        <div class="borrar">
          <button
            class="enlace enlace--solo peligro"
            type="button"
            [disabled]="!enLinea()"
            (click)="dialogoBorrar.showModal()"
          >
            <app-icono nombre="borrar" />
            {{ t('borrar.accion') }}
          </button>
        </div>

        <dialog #dialogoBorrar class="dialogo" closedby="any" aria-labelledby="borrar-titulo">
          <div class="contenido">
            <h2 id="borrar-titulo">{{ t('borrar.titulo') }}</h2>
            <p>{{ t('borrar.texto') }}</p>
            @if (errorBorrar(); as codigo) {
              <p class="aviso aviso--error" role="alert">{{ texto('errores.' + codigo) }}</p>
            }
            <div class="acciones">
              <button class="boton boton--secundario" type="button" (click)="dialogoBorrar.close()">
                {{ t('cancelar') }}
              </button>
              <button
                class="boton boton--peligro"
                type="button"
                [disabled]="!enLinea()"
                [appOcupado]="borrando()"
                (click)="borrar(a)"
              >
                {{ t('borrar.confirmar') }}
              </button>
            </div>
          </div>
        </dialog>
      } @else {
        <div class="esqueletos" role="status" [attr.aria-label]="t('cargando')">
          <span class="esqueleto titulo-esqueleto"></span>
          <span class="esqueleto cuerpo-esqueleto"></span>
          <span class="esqueleto cuerpo-esqueleto"></span>
        </div>
      }
    </div>
  `,
  styles: `
    .leido-aviso {
      display: grid;
      gap: var(--espacio-4);
      max-inline-size: 40rem;
    }

    header {
      display: grid;
      justify-items: start;
      gap: var(--espacio-2);
    }

    h1 {
      overflow-wrap: anywhere;
    }

    /* El cuerpo se lee como lo escribió el docente: con sus saltos de línea y sin desbordar. */
    .cuerpo {
      font-size: var(--texto-lg);
      line-height: var(--interlineado);
      white-space: pre-line;
      overflow-wrap: anywhere;
    }

    .etiqueta-urgente {
      padding: 0.125rem var(--espacio-2);
      border-radius: var(--radio-sm);
      background: var(--color-aviso-tinte);
      color: var(--color-aviso-tinta);
      font-size: var(--texto-xs);
      font-weight: var(--peso-fuerte);
    }

    .lectura {
      display: grid;
      gap: var(--espacio-2);
      max-inline-size: 24rem;
    }

    .lectura h2 {
      font-size: var(--texto-lg);
    }

    .barra {
      display: block;
      block-size: 0.5rem;
      border-radius: var(--radio-sm);
      background: var(--color-superficie-hundida);
      overflow: hidden;
    }

    /*
     * Catálogo de reglas/07, "Leído por": crece cuando cambia el conteo (el refresco trajo lecturas nuevas). Al
     * llegar a la pantalla ya está en su lugar: la barra nace con su valor y no hay de dónde animar.
     */
    .barra span {
      display: block;
      block-size: 100%;
      background: var(--grado);
      transform-origin: left;
      transition: transform 240ms var(--ease-out);
    }

    @media (prefers-reduced-motion: reduce) {
      .barra span {
        transition: none;
      }
    }

    .compartir,
    .entregas {
      display: grid;
      gap: var(--espacio-2);
    }

    .compartir {
      padding: var(--espacio-4);
      border-radius: var(--radio-lg);
      background: var(--color-aviso-tinte);
    }

    .compartir .dato-suave {
      color: var(--color-aviso-tinta);
    }

    .compartir h2,
    .entregas h2 {
      font-size: var(--texto-lg);
    }

    .compartir .filas > li + li {
      border-top-color: oklch(from var(--color-aviso-tinta) l c h / 0.2);
    }

    .fila-compartir {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--espacio-2) var(--espacio-4);
      padding-block: var(--espacio-3);
    }

    .quien {
      display: grid;
      gap: 0.125rem;
      min-inline-size: 0;
    }

    .quien .nombre {
      font-weight: var(--peso-medio);
      overflow-wrap: anywhere;
    }

    .compartido {
      display: inline-flex;
      align-items: center;
      gap: var(--espacio-1);
      color: var(--color-exito-tinta);
      font-size: var(--texto-sm);
      font-weight: var(--peso-medio);
    }

    .entregas .filas {
      border-block: 1px solid var(--color-divisoria);
    }

    .entrega {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      gap: var(--espacio-2) var(--espacio-3);
      padding-block: var(--espacio-3);
    }

    .estado {
      color: var(--color-texto-suave);
      font-size: var(--texto-sm);
      text-align: end;
    }

    .estado.leido {
      color: var(--color-exito-tinta);
      font-weight: var(--peso-medio);
    }

    .canales {
      display: flex;
      flex-wrap: wrap;
      gap: var(--espacio-1);
      grid-column: 1 / -1;
    }

    .canal {
      padding: 0.125rem var(--espacio-2);
      border-radius: var(--radio-sm);
      background: var(--color-superficie-hundida);
      color: var(--color-texto-suave);
      font-size: var(--texto-xs);
      font-weight: var(--peso-medio);
    }

    .canal--falta {
      background: var(--color-aviso-tinte);
      color: var(--color-aviso-tinta);
    }

    .canal.fallido {
      background: var(--color-peligro-tinte);
      color: var(--color-peligro-tinta);
    }

    .borrar {
      padding-block-start: var(--espacio-2);
    }

    .enlace.peligro {
      gap: var(--espacio-2);
      color: var(--color-peligro-tinta);
    }

    .volver-esqueleto {
      display: block;
      block-size: var(--tactil-minimo);
    }

    .esqueletos {
      display: grid;
      gap: var(--espacio-3);
      max-inline-size: 40rem;
    }

    .titulo-esqueleto {
      display: block;
      block-size: 2.5rem;
      inline-size: 70%;
    }

    .cuerpo-esqueleto {
      display: block;
      block-size: 5rem;
    }
  `,
})
export class DetalleAviso implements OnInit {
  private readonly servicio = inject(AvisosGrupoService);
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);
  private readonly i18n = inject(TranslocoService);
  private readonly pintarPortada = controlarPortada();
  private temporizador: ReturnType<typeof setInterval> | null = null;
  protected readonly forma = forma;
  protected readonly enLinea = this.servicio.enLinea;

  readonly id = input.required<string>();

  protected readonly dialogoBorrar = viewChild<ElementRef<HTMLDialogElement>>('dialogoBorrar');
  protected readonly grupo = signal<GrupoDto | null>(null);
  protected readonly aviso = signal<AvisoDto | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly errorCompartir = signal<string | null>(null);
  protected readonly errorBorrar = signal<string | null>(null);
  protected readonly compartiendo = signal<string | null>(null);
  protected readonly borrando = signal(false);

  protected readonly clave = computed(() => {
    const g = this.grupo();
    return g ? claveGrado(g.nivel, g.grado) : undefined;
  });
  protected readonly fraccion = computed(() => {
    const a = this.aviso();
    return a ? fraccionLeidos(a.leidos, a.destinatarios) : 0;
  });
  protected readonly aCompartir = computed(() => porCompartir(this.aviso()?.entregas ?? []));

  constructor() {
    const refrescar = () => {
      if (document.visibilityState === 'visible') {
        void this.refrescar();
      }
    };
    this.temporizador = setInterval(refrescar, REFRESCO_MS);
    document.addEventListener('visibilitychange', refrescar);
    inject(DestroyRef).onDestroy(() => {
      if (this.temporizador) {
        clearInterval(this.temporizador);
      }
      document.removeEventListener('visibilitychange', refrescar);
    });
  }

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  protected async cargar(): Promise<void> {
    this.error.set(null);
    try {
      const aviso = await this.servicio.aviso(this.id());
      const grupo = await this.servicio.grupo(aviso.grupoId);
      this.pintarPortada(portadaDeGrupo(grupo));
      this.grupo.set(grupo);
      this.aviso.set(aviso);
    } catch (e) {
      this.error.set(codigoDeError(e));
    }
  }

  /** Sin red no se toca lo que ya se ve: el siguiente refresco lo intenta de nuevo. */
  private async refrescar(): Promise<void> {
    if (!this.aviso() || !this.enLinea()) {
      return;
    }
    try {
      this.aviso.set(await this.servicio.aviso(this.id()));
    } catch {
      // Se queda la última lectura.
    }
  }

  protected texto(clave: string): string {
    return this.i18n.translate(clave);
  }

  protected fechaHora(instante: string): string {
    return fechaHoraAviso(instante);
  }

  protected alumnos(e: EntregaDto): string {
    return nombresAlumnos(e.alumnos);
  }

  /**
   * La ventana se abre en el mismo toque (si no, el navegador la bloquea) y recibe wa.me cuando llega el enlace
   * personal del tutor.
   */
  protected async compartir(aviso: AvisoDto, entrega: EntregaDto): Promise<void> {
    if (!entrega.telefono) {
      return;
    }
    const ventana = window.open('', '_blank');
    this.errorCompartir.set(null);
    this.compartiendo.set(entrega.tutorId);
    try {
      const enlace = await this.servicio.compartirAviso(aviso.id, entrega.tutorId);
      const g = this.grupo();
      const mensaje = this.i18n.translate('avisos.compartelo.mensaje', {
        tutor: entrega.nombre ?? '',
        grupo: g ? nombreGrupo(g.grado, g.nombre) : '',
        titulo: aviso.titulo,
        url: enlace.url,
      });
      const destino = urlWhatsApp(entrega.telefono, mensaje);
      if (ventana) {
        ventana.opener = null;
        ventana.location.href = destino;
      } else {
        window.location.href = destino;
      }
      await this.refrescar();
    } catch (e) {
      ventana?.close();
      this.errorCompartir.set(codigoDeError(e));
    } finally {
      this.compartiendo.set(null);
    }
  }

  protected async borrar(aviso: AvisoDto): Promise<void> {
    this.errorBorrar.set(null);
    this.borrando.set(true);
    try {
      await this.servicio.borrar(aviso.id);
      this.dialogoBorrar()?.nativeElement.close();
      this.avisos.mostrar(this.i18n.translate('avisos.borrar.hecho'));
      await this.router.navigate(['/grupos', aviso.grupoId, 'avisos'], { replaceUrl: true });
    } catch (e) {
      this.errorBorrar.set(codigoDeError(e));
    } finally {
      this.borrando.set(false);
    }
  }
}
