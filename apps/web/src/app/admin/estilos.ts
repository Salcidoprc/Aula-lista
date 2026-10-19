/**
 * Piezas comunes de las secciones del panel (SPEC-09): cabeza, tablas de libreta y marcas de estado. Viajan con el
 * panel y no con la app del docente.
 */
export const ESTILOS_PANEL = `
  :host {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    align-content: start;
    gap: var(--espacio-5);
  }

  .cabeza {
    display: grid;
    gap: var(--espacio-2);
  }

  h1 {
    margin: 0;
    font-size: var(--texto-2xl);
    line-height: var(--interlineado-ajustado);
    letter-spacing: var(--tracking-titulo);
    text-wrap: balance;
    outline: none;
  }

  h2 {
    margin: 0;
    font-size: var(--texto-lg);
    line-height: var(--interlineado-ajustado);
  }

  .resumen {
    max-width: var(--ancho-lectura);
    margin: 0;
    color: var(--color-texto-suave);
    text-wrap: pretty;
  }

  .resumen strong {
    color: var(--color-texto);
    font-weight: var(--peso-fuerte);
  }

  .cargando {
    display: grid;
    gap: var(--espacio-2);
  }

  .cargando .esqueleto {
    display: block;
    block-size: 2.75rem;
  }

  .vacio {
    margin: 0;
    padding: var(--espacio-5) var(--espacio-4);
    border-radius: var(--radio-lg);
    background: var(--color-superficie-hundida);
    color: var(--color-texto-suave);
    text-align: start;
  }

  /* Libreta: renglones con divisoria, encabezados suaves y cifras tabulares alineadas a la derecha. */
  .libreta {
    inline-size: 100%;
    border-collapse: collapse;
    font-size: var(--texto-sm);
  }

  .libreta caption {
    padding-block-end: var(--espacio-2);
    color: var(--color-texto-suave);
    font-size: var(--texto-xs);
    text-align: start;
  }

  .libreta th,
  .libreta td {
    padding: var(--espacio-3) var(--espacio-2);
    border-block-end: 1px solid var(--color-divisoria);
    text-align: start;
    vertical-align: top;
  }

  .libreta thead th {
    padding-block: var(--espacio-2);
    color: var(--color-texto-suave);
    font-size: var(--texto-xs);
    font-weight: var(--peso-medio);
    vertical-align: bottom;
    white-space: nowrap;
  }

  .libreta tbody th {
    font-weight: var(--peso-medio);
  }

  .libreta .num {
    font-family: var(--fuente-cifras);
    font-variant-numeric: tabular-nums;
    text-align: end;
    white-space: nowrap;
  }

  .libreta tfoot td,
  .libreta tfoot th {
    border-block-end: 0;
    font-weight: var(--peso-fuerte);
  }

  .secundario {
    display: block;
    color: var(--color-texto-suave);
    font-size: var(--texto-xs);
    font-weight: var(--peso-normal);
    overflow-wrap: anywhere;
  }

  .sello {
    display: inline-flex;
    align-items: center;
    justify-self: start;
    padding: 0.125rem var(--espacio-2);
    border-radius: var(--radio-sm);
    background: var(--color-superficie-hundida);
    color: var(--color-texto);
    font-size: var(--texto-xs);
    font-weight: var(--peso-medio);
    white-space: nowrap;
  }

  /* Más específico que los span de las celdas, que se acomodan en rejilla. */
  .libreta td > .sello {
    display: inline-flex;
  }

  .sello[data-tono='exito'] {
    background: var(--color-exito-tinte);
    color: var(--color-exito-tinta);
  }

  .sello[data-tono='aviso'] {
    background: var(--color-aviso-tinte);
    color: var(--color-aviso-tinta);
  }

  .sello[data-tono='peligro'] {
    background: var(--color-peligro-tinte);
    color: var(--color-peligro-tinta);
  }

  .sello[data-tono='accion'] {
    background: var(--color-accion-tinte);
    color: var(--color-accion-tinta);
  }

  /* En el celular cada renglón de la libreta se vuelve una ficha de dos columnas. */
  @media (max-width: 47.99rem) {
    .libreta.apilable thead {
      position: absolute;
      inline-size: 1px;
      block-size: 1px;
      overflow: hidden;
      clip-path: inset(50%);
    }

    .libreta.apilable,
    .libreta.apilable tbody,
    .libreta.apilable tfoot,
    .libreta.apilable tr {
      display: block;
    }

    .libreta.apilable tr {
      padding-block: var(--espacio-3);
      border-block-end: 1px solid var(--color-divisoria);
    }

    .libreta.apilable caption {
      display: block;
    }

    /* La ficha va en dos columnas de datos para que una lista de 200 grupos no mida metros. */
    .libreta.apilable tbody tr {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      column-gap: var(--espacio-5);
    }

    .libreta.apilable tbody th {
      grid-column: 1 / -1;
    }

    .libreta.apilable th,
    .libreta.apilable td {
      display: block;
      padding: var(--espacio-1) 0;
      border: 0;
      text-align: start;
    }

    .libreta.apilable tbody th {
      display: block;
      padding-block-end: var(--espacio-2);
    }

    /* Etiqueta arriba y dato abajo, alineados a la izquierda: en media ficha no cabe uno al lado del otro. */
    .libreta.apilable td::before {
      display: block;
      content: attr(data-etiqueta);
      color: var(--color-texto-suave);
      font-family: var(--fuente);
      font-size: var(--texto-xs);
    }

    .libreta.apilable td:empty {
      display: none;
    }
  }
`;
