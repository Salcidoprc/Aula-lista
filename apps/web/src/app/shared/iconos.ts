import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Íconos de trazo, 1.75 px y puntas redondas, como la flecha del selector (formularios.css). */
export type NombreIcono =
  | 'volver'
  | 'mas'
  | 'archivo'
  | 'pegar'
  | 'persona'
  | 'descargar'
  | 'ajustes'
  | 'cerrar'
  | 'siguiente'
  | 'guardado'
  | 'pendiente'
  | 'sincronizando'
  | 'sinConexion'
  | 'borrar'
  | 'editar'
  | 'tabla'
  | 'copiar'
  | 'equipo'
  | 'nota'
  | 'enlace'
  | 'enlaceRoto'
  | 'mensaje'
  | 'campana'
  | 'correo';

const TRAZOS: Record<NombreIcono, string> = {
  volver: 'M10 3.5 5.5 8l4.5 4.5',
  siguiente: 'm6 3.5 4.5 4.5L6 12.5',
  mas: 'M8 3v10M3 8h10',
  archivo:
    'M9.5 1.75H4.25a1 1 0 0 0-1 1v10.5a1 1 0 0 0 1 1h7.5a1 1 0 0 0 1-1V5L9.5 1.75Zm0 0V5h3.25M5.75 8.5h4.5M5.75 11h4.5',
  pegar:
    'M10.5 2.75h1.25a1 1 0 0 1 1 1v9.5a1 1 0 0 1-1 1h-7.5a1 1 0 0 1-1-1v-9.5a1 1 0 0 1 1-1H5.5M6 1.75h4a.5.5 0 0 1 .5.5v1a.5.5 0 0 1-.5.5H6a.5.5 0 0 1-.5-.5v-1a.5.5 0 0 1 .5-.5ZM5.75 8h4.5M5.75 10.5h3',
  persona:
    'M8 7.5a2.75 2.75 0 1 0 0-5.5 2.75 2.75 0 0 0 0 5.5ZM2.75 14c.4-2.6 2.6-4.25 5.25-4.25s4.85 1.65 5.25 4.25',
  descargar: 'M8 2v8m0 0 3-3m-3 3L5 7M2.75 11v1.75a1 1 0 0 0 1 1h8.5a1 1 0 0 0 1-1V11',
  ajustes:
    'M2.5 4.5h6m3 0h2M2.5 11.5h2m3 0h6M11.5 4.5a1.5 1.5 0 1 1-3 0 1.5 1.5 0 0 1 3 0ZM7.5 11.5a1.5 1.5 0 1 1-3 0 1.5 1.5 0 0 1 3 0Z',
  cerrar: 'm4 4 8 8m0-8-8 8',
  guardado: 'm3.25 8.5 3.25 3.25 6.25-7',
  pendiente: 'M8 14.25A6.25 6.25 0 1 0 8 1.75a6.25 6.25 0 0 0 0 12.5ZM8 4.75V8l2.25 1.5',
  sincronizando:
    'M13.25 8a5.25 5.25 0 0 1-9.65 2.9M2.75 8a5.25 5.25 0 0 1 9.65-2.9M12.75 2.5v2.75H10M3.25 13.5v-2.75H6',
  sinConexion:
    'M5.25 5.1A4 4 0 0 1 12 6.85a2.75 2.75 0 0 1 .9 5.15M10.25 12.25H4.5A2.75 2.75 0 0 1 3.4 7M2 2l12 12',
  borrar:
    'M2.75 4.25h10.5M6.25 4.25V2.75h3.5v1.5M4.25 4.25l.6 8.6a1 1 0 0 0 1 .9h4.3a1 1 0 0 0 1-.9l.6-8.6',
  editar:
    'M9.75 3.25 12.75 6.25M2.75 13.25l.6-3.1 7.4-7.4a1.4 1.4 0 0 1 2 0l.75.75a1.4 1.4 0 0 1 0 2l-7.4 7.4-3.35.35Z',
  tabla: 'M2.75 2.75h10.5v10.5H2.75zM2.75 6.25h10.5M2.75 9.75h10.5M6.25 6.25v7',
  copiar: 'M5.75 5.75h7.5v7.5h-7.5zM10.25 5.75v-3h-7.5v7.5h3',
  equipo:
    'M6 7.25a2.25 2.25 0 1 0 0-4.5 2.25 2.25 0 0 0 0 4.5ZM1.75 13.25c.35-2.2 2.1-3.6 4.25-3.6s3.9 1.4 4.25 3.6M10.75 7.1a1.85 1.85 0 1 0 0-3.7M11.75 9.75c1.35.4 2.3 1.6 2.5 3.5',
  nota: 'M3.25 2.75h9.5v7.5l-3 3h-6.5zM12.75 10.25h-3v3M5.5 5.75h5M5.5 8.25h3',
  enlace:
    'M6.75 9.25a2.5 2.5 0 0 0 3.54 0l2-2a2.5 2.5 0 0 0-3.54-3.54l-.75.75M9.25 6.75a2.5 2.5 0 0 0-3.54 0l-2 2a2.5 2.5 0 0 0 3.54 3.54l.75-.75',
  enlaceRoto:
    'M9.5 4.25 10.25 3.5a2.5 2.5 0 0 1 3.54 3.54l-1.29 1.29M6.5 11.75l-.75.75a2.5 2.5 0 0 1-3.54-3.54L3.5 7.67M5.25 1.75v1.5M1.75 5.25h1.5M10.75 14.25v-1.5M14.25 10.75h-1.5',
  mensaje:
    'M8 2.75c-2.9 0-5.25 2-5.25 4.5 0 1.3.65 2.5 1.7 3.3L4 13.25l2.65-1.35c.43.07.88.1 1.35.1 2.9 0 5.25-2 5.25-4.5S10.9 2.75 8 2.75Z',
  correo: 'M2.75 3.75h10.5v8.5H2.75zM2.75 4.25 8 8.5l5.25-4.25',
  campana:
    'M4.25 6.5a3.75 3.75 0 0 1 7.5 0c0 2.5.75 3.75 1.5 4.5H2.75c.75-.75 1.5-2 1.5-4.5ZM6.5 13.25a1.6 1.6 0 0 0 3 0',
};

@Component({
  selector: 'app-icono',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'aria-hidden': 'true' },
  template: `
    <svg
      viewBox="0 0 16 16"
      fill="none"
      stroke="currentColor"
      stroke-width="1.75"
      stroke-linecap="round"
      stroke-linejoin="round"
    >
      <path [attr.d]="trazo()" />
    </svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      flex: none;
      width: 1.25rem;
      height: 1.25rem;
    }

    svg {
      width: 100%;
      height: 100%;
    }
  `,
})
export class Icono {
  readonly nombre = input.required<NombreIcono>();

  protected trazo(): string {
    return TRAZOS[this.nombre()];
  }
}
