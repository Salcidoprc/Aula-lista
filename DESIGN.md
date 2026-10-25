---
name: Aula Lista
description: Herramienta escolar de captura, cierre y boletas con el lenguaje de los libros de texto gratuitos, un color por grado.
colors:
  fondo: "oklch(0.985 0.004 95)"
  superficie: "oklch(0.995 0.002 95)"
  superficie-hundida: "oklch(0.95 0.01 260)"
  divisoria: "oklch(0.9 0.01 260)"
  texto: "oklch(0.25 0.03 260)"
  texto-suave: "oklch(0.48 0.025 260)"
  texto-tenue: "oklch(0.6 0.02 260)"
  accion: "oklch(0.5 0.15 265)"
  accion-presionada: "oklch(0.44 0.14 265)"
  accion-tinte: "oklch(0.95 0.02 265)"
  accion-tinta: "oklch(0.38 0.12 265)"
  sobre-accion: "oklch(0.99 0.004 265)"
  exito: "oklch(0.55 0.13 150)"
  exito-tinte: "oklch(0.95 0.035 150)"
  exito-tinta: "oklch(0.36 0.09 150)"
  aviso: "oklch(0.64 0.13 70)"
  aviso-tinte: "oklch(0.96 0.04 85)"
  aviso-tinta: "oklch(0.42 0.09 65)"
  peligro: "oklch(0.55 0.19 27)"
  peligro-tinte: "oklch(0.95 0.025 27)"
  peligro-tinta: "oklch(0.4 0.14 27)"
  sobre-grado: "oklch(0.99 0.005 95)"
  grado-p1: "oklch(0.53 0.18 27)"
  grado-p1-tinta: "oklch(0.37 0.12 27)"
  grado-p1-tinte: "oklch(0.94 0.03 27)"
  grado-p2: "oklch(0.53 0.14 50)"
  grado-p2-tinta: "oklch(0.37 0.09 50)"
  grado-p2-tinte: "oklch(0.94 0.035 60)"
  grado-p3: "oklch(0.5 0.125 150)"
  grado-p3-tinta: "oklch(0.35 0.08 150)"
  grado-p3-tinte: "oklch(0.94 0.04 150)"
  grado-p4: "oklch(0.5 0.085 200)"
  grado-p4-tinta: "oklch(0.34 0.06 200)"
  grado-p4-tinte: "oklch(0.94 0.03 195)"
  grado-p5: "oklch(0.52 0.16 300)"
  grado-p5-tinta: "oklch(0.37 0.12 300)"
  grado-p5-tinte: "oklch(0.94 0.03 300)"
  grado-p6: "oklch(0.53 0.18 350)"
  grado-p6-tinta: "oklch(0.37 0.13 350)"
  grado-p6-tinte: "oklch(0.94 0.035 350)"
  grado-s1: "oklch(0.52 0.11 120)"
  grado-s1-tinta: "oklch(0.35 0.07 120)"
  grado-s1-tinte: "oklch(0.94 0.05 115)"
  grado-s2: "oklch(0.5 0.1 235)"
  grado-s2-tinta: "oklch(0.35 0.07 235)"
  grado-s2-tinte: "oklch(0.94 0.025 235)"
  grado-s3: "oklch(0.48 0.04 260)"
  grado-s3-tinta: "oklch(0.33 0.03 260)"
  grado-s3-tinte: "oklch(0.94 0.012 260)"
typography:
  numero-grado:
    fontFamily: "Lexend Variable, Lexend, ui-sans-serif, sans-serif"
    fontSize: "7.5rem"
    fontWeight: 700
    lineHeight: 1
  titulo:
    fontFamily: "Lexend Variable, Lexend, ui-sans-serif, sans-serif"
    fontSize: "2rem"
    fontWeight: 700
    lineHeight: 1.2
    letterSpacing: "-0.01em"
  titulo-portada:
    fontFamily: "Lexend Variable, Lexend, ui-sans-serif, sans-serif"
    fontSize: "1.1875rem"
    fontWeight: 700
    lineHeight: 1.2
    letterSpacing: "-0.01em"
  cuerpo:
    fontFamily: "Lexend Variable, Lexend, ui-sans-serif, sans-serif"
    fontSize: "1rem"
    fontWeight: 400
    lineHeight: 1.5
  etiqueta:
    fontFamily: "Lexend Variable, Lexend, ui-sans-serif, sans-serif"
    fontSize: "0.8125rem"
    fontWeight: 500
    lineHeight: 1.5
  dato:
    fontFamily: "Aula Cifras, Lexend Variable, ui-sans-serif, sans-serif"
    fontSize: "1rem"
    fontWeight: 500
    lineHeight: 1.2
    fontFeature: "tnum"
rounded:
  sm: "4px"
  md: "10px"
  lg: "12px"
  hoja: "16px"
  pleno: "999px"
  tapa: "4px 8px 8px 4px"
spacing:
  "1": "4px"
  "2": "8px"
  "3": "12px"
  "4": "16px"
  "5": "24px"
  "6": "32px"
  "7": "48px"
components:
  portada:
    backgroundColor: "{colors.accion}"
    textColor: "{colors.sobre-grado}"
    typography: "{typography.titulo-portada}"
    padding: "12px 16px 16px"
    height: "72px"
  tapa:
    backgroundColor: "{colors.grado-p4}"
    textColor: "{colors.sobre-grado}"
    rounded: "{rounded.tapa}"
    width: "56px"
    height: "64px"
  boton-primario:
    backgroundColor: "{colors.accion}"
    textColor: "{colors.sobre-accion}"
    rounded: "{rounded.lg}"
    padding: "0 24px"
    height: "48px"
  boton-primario-presionado:
    backgroundColor: "{colors.accion-presionada}"
    textColor: "{colors.sobre-accion}"
  boton-secundario:
    backgroundColor: "{colors.superficie-hundida}"
    textColor: "{colors.texto}"
    rounded: "{rounded.lg}"
    height: "44px"
  campo:
    backgroundColor: "{colors.superficie-hundida}"
    textColor: "{colors.texto}"
    typography: "{typography.cuerpo}"
    rounded: "{rounded.md}"
    padding: "0 12px"
    height: "48px"
  celda-captura:
    backgroundColor: "{colors.superficie-hundida}"
    textColor: "{colors.texto}"
    typography: "{typography.dato}"
    rounded: "{rounded.md}"
    width: "56px"
    height: "40px"
  celda-captura-guardada:
    backgroundColor: "{colors.grado-p4-tinte}"
    textColor: "{colors.grado-p4-tinta}"
  boton-rapido:
    backgroundColor: "{colors.superficie-hundida}"
    textColor: "{colors.texto}"
    rounded: "{rounded.lg}"
    height: "44px"
---

# Aula Lista: sistema de diseño

Los valores normativos viven en `apps/web/src/styles/tokens.css`; este archivo explica cómo usarlos. Las reglas de movimiento y el catálogo de transiciones están en `docs/reglas/07-diseno-y-movimiento.md` y ganan si algo aquí las contradice. El estilo lo eligió el dueño del producto el 11/10/2026 (ADR-0010).

## Overview

**Creative North Star: "Libros de texto"**

Cada grupo del docente tiene el color de su grado, como las portadas de los libros de texto gratuitos que todo docente en México reconoce. La cabecera de cada pantalla es una **portada**: un bloque del color del grado con el número del grado enorme y recortado contra el borde. En la lista, cada grupo es una **tapa de libro** de ese color, con su lomo. El docente sabe en qué grupo está antes de leer.

Fuera de la portada y las tapas, la app es una herramienta tranquila: fondo de papel cálido, tinta azul pizarra, controles redondeados y amables, una sola acción principal en azul. El color de grado identifica; nunca decora ni compite con la acción. El movimiento es corto y funcional: confirma, ubica y suaviza; lo que se repite no se anima.

**Key Characteristics:**
- Un color por grado (nueve: 1° a 6° de primaria y 1° a 3° de secundaria), cada uno con base, tinta y tinte.
- Portada con el número del grado grande y recortado; tapas de libro con lomo.
- Lexend para todo el texto; dígitos tabulares para todo dato.
- Controles con esquinas de 10 a 12 px y objetivos táctiles de 44 px o más.
- Plano por defecto; solo flota lo que de verdad flota.

## Colors

Papel cálido y tinta azul pizarra como neutros, un azul de acción y nueve colores de grado de luminosidad pareja (L 0.48 a 0.53) para que ningún grupo grite más que otro.

### Primary
- **Azul de cuaderno** (`accion`): botón principal, enlaces, foco y la portada cuando no hay grupo activo. Presionado: `accion-presionada`. Sobre él, el texto va en `sobre-accion`. Sobre su tinte (`accion-tinte`) el texto va en `accion-tinta`.

### Secondary: colores de grado
- **Rojo** (`grado-p1`), **naranja** (`grado-p2`), **verde** (`grado-p3`), **turquesa** (`grado-p4`), **morado** (`grado-p5`) y **magenta** (`grado-p6`) para 1° a 6° de primaria.
- **Olivo** (`grado-s1`), **azul petróleo** (`grado-s2`) y **pizarra** (`grado-s3`) para 1° a 3° de secundaria; más sobrios, como corresponde a alumnos mayores.
- Cada grado tiene `-tinta` (texto sobre su tinte o sobre el fondo), `-tinte` (fondo suave: actividad seleccionada, celda guardada) y `-rotulo` (texto suave sobre la base: subtítulo de la portada, datos de la tapa). En claro el rótulo es el mismo tinte; en oscuro el tinte se oscurece y el rótulo sigue claro. Sobre la base, el texto va en `sobre-grado` o en el rótulo.
- En los componentes no se escribe `grado-p4`: se pone `data-grado="p4"` en el contenedor y se usan `--grado`, `--grado-tinta`, `--grado-tinte`, `--grado-rotulo` y `--grado-trazo`. Sin `data-grado`, la base es `marca` (el azul de la portada sin grupo) y las demás toman la familia de la acción.
- **Trazo** (`--grado-trazo`): el color del grado cuando dibuja una barra de avance, la raya de la pestaña activa, la marca de la fila activa o el borde de un conteo. En claro es la base; en oscuro se mezcla 55% con la tinta para llegar a 3:1 sobre la pizarra.

### Tertiary: estados
- **Éxito**, **aviso** y **peligro** (`exito`, `aviso`, `peligro`), cada uno con tinte y tinta. Solo donde cargan significado: sincronizado, pendiente de enviar, error. La celda de captura guardada usa el tinte del grado, no el verde de éxito.

### Neutral
- **Papel** (`fondo`): fondo de la app. **Hoja** (`superficie`): campo con foco, menús y hojas que flotan. **Papel hundido** (`superficie-hundida`): campos, celdas de captura, botones secundarios, pista de la barra de avance. **Divisoria** (`divisoria`): líneas de 1 px entre filas.
- **Tinta** (`texto`) para todo texto; **tinta suave** (`texto-suave`) para datos secundarios y marcadores de posición; **tinta tenue** (`texto-tenue`) solo para íconos y bordes de controles, nunca para texto.

### Contraste (WCAG 2.1 AA, calculado)

| Texto o elemento | Fondo | Contraste | Mínimo |
| --- | --- | --- | --- |
| `texto` | `fondo` / `superficie` / `superficie-hundida` | 15.34 / 15.78 / 13.84 | 4.5 |
| `texto-suave` | `fondo` / `superficie-hundida` | 6.26 / 5.65 | 4.5 |
| `texto-tenue` (solo UI) | `fondo` | 3.78 | 3 |
| `sobre-accion` | `accion` / `accion-presionada` | 5.99 / 7.75 | 4.5 |
| `accion` (enlace, foco) | `fondo` | 5.91 | 4.5 |
| `accion-tinta` | `accion-tinte` | 8.84 | 4.5 |
| `accion-tinte` (subtítulo de portada sin grado) | `accion` | 5.33 | 4.5 |
| `exito-tinta` / `aviso-tinta` / `peligro-tinta` | su tinte | 9.13 / 7.71 / 8.47 | 4.5 |
| `exito-tinta` / `aviso-tinta` / `peligro-tinta` | `fondo` | 10.00 / 8.30 / 9.47 | 4.5 |
| `exito` / `aviso` / `peligro` (UI) | `fondo` | 4.38 / 3.31 / 5.12 | 3 |

| Grado | `sobre-grado` / base | tinte / base | tinta / tinte | tinta / fondo | base / fondo (UI) |
| --- | --- | --- | --- | --- | --- |
| p1 rojo | 5.62 | 4.81 | 9.18 | 10.59 | 5.54 |
| p2 naranja | 5.42 | 4.65 | 8.99 | 10.33 | 5.34 |
| p3 verde | 5.50 | 4.81 | 9.28 | 10.46 | 5.42 |
| p4 turquesa | 5.60 | 4.88 | 9.68 | 10.94 | 5.52 |
| p5 morado | 5.76 | 4.93 | 9.16 | 10.53 | 5.68 |
| p6 magenta | 5.68 | 4.83 | 9.25 | 10.71 | 5.60 |
| s1 olivo | 5.21 | 4.54 | 9.41 | 10.66 | 5.14 |
| s2 azul petróleo | 5.72 | 4.96 | 9.41 | 10.70 | 5.64 |
| s3 pizarra | 6.36 | 5.49 | 10.25 | 11.71 | 6.26 |

Todas las bases dan además 4.5:1 o más contra `superficie-hundida` como elemento de interfaz (barra de avance, borde de la celda activa). El número decorativo de la portada (base al 80% con `sobre-grado`) queda a 1.5:1 de la base a propósito: es textura, va con `aria-hidden` y el texto nunca se encima sobre él.

### Tema oscuro (RF-009)

**Escena:** la docente revisa observaciones de noche, en casa y con la luz baja; abre la app en el celular que ya tiene en modo oscuro. El tema oscuro se compone para esa escena, no se invierte el claro.

- **Pizarra y gis.** El papel se vuelve pizarra azul tinta (`fondo` L 0.20, hue 260) y la tinta se vuelve gis cálido (`texto` L 0.93, hue 95: el tono del papel del claro). La elevación sube un paso de luz por capa: `fondo` 0.20, `superficie` 0.235, `superficie-hundida` 0.28 (campos, celdas y botones secundarios se aclaran para seguir pareciendo tocables) y `divisoria` 0.35.
- **Acción.** El azul se aclara (L 0.74) y su texto se oscurece (`sobre-accion` L 0.20): el botón principal sigue siendo lo más visible de la pantalla. Los tintes de estado y de la acción bajan a L 0.30 a 0.32 y sus tintas suben a 0.87 o más.
- **Las tapas siguen siendo libros.** Portada, tapas y chips elegidos conservan el color del grado, 0.03 más bajo para no deslumbrar, con `sobre-grado` en L 0.97. La portada sin grupo usa `marca` y no la acción, que en oscuro es un azul claro.
- **Sombras.** En la pizarra la sombra entintada al 12% no se ve: `sombra-flotante`, `sombra-hoja` y el velo suben a 40-60%.
- **Mecánica.** `TemaService` pone `data-tema="claro"` u `"oscuro"` en `<html>` según la elección guardada en el dispositivo o el sistema; `tokens.css` redefine los mismos tokens en `:root[data-tema='oscuro']`. Antes de que arranque la app, una consulta `prefers-color-scheme` pinta el fondo de pizarra para que no haya destello claro. La barra de estado del celular toma el color de la portada del tema activo. El cambio de tema no se anima.
- **Fuera.** Las boletas en PDF quedan en claro: son para imprimir. El botón de Google conserva sus colores de marca.

| Texto o elemento (oscuro) | Fondo | Contraste | Mínimo |
| --- | --- | --- | --- |
| `texto` | `fondo` / `superficie` / `superficie-hundida` | 14.74 / 13.57 / 11.88 | 4.5 |
| `texto-suave` | `fondo` / `superficie-hundida` | 9.05 / 7.29 | 4.5 |
| `texto-tenue` (solo UI) | `fondo` / `superficie-hundida` | 5.38 / 4.34 | 3 |
| `sobre-accion` | `accion` / `accion-presionada` | 7.78 / 6.22 | 4.5 |
| `accion` (enlace, foco) | `fondo` | 7.77 | 4.5 |
| `accion-tinta` | `accion-tinte` | 8.59 | 4.5 |
| `sobre-grado` / `marca-rotulo` | `marca` | 6.42 / 5.52 | 4.5 |
| `exito-tinta` / `aviso-tinta` / `peligro-tinta` | su tinte | 9.29 / 9.16 / 8.92 | 4.5 |
| `exito-tinta` / `aviso-tinta` / `peligro-tinta` | `fondo` | 12.57 / 12.99 / 11.92 | 4.5 |
| `exito` / `aviso` / `peligro` (UI) | `fondo` | 7.69 / 9.53 / 6.59 | 3 |

| Grado (oscuro) | `sobre-grado` / base | rótulo / base | tinta / tinte | tinta / fondo | trazo / fondo | trazo / hundida |
| --- | --- | --- | --- | --- | --- | --- |
| p1 rojo | 6.01 | 5.44 | 8.30 | 10.76 | 6.12 | 4.94 |
| p2 naranja | 5.79 | 5.26 | 8.30 | 10.88 | 6.26 | 5.04 |
| p3 verde | 5.93 | 5.50 | 8.73 | 11.41 | 6.38 | 5.15 |
| p4 turquesa | 6.03 | 5.57 | 8.70 | 11.33 | 6.31 | 5.09 |
| p5 morado | 6.15 | 5.59 | 8.63 | 11.24 | 6.25 | 5.04 |
| p6 magenta | 6.06 | 5.47 | 8.57 | 11.07 | 6.21 | 5.00 |
| s1 olivo | 5.60 | 5.17 | 8.99 | 11.65 | 6.62 | 5.33 |
| s2 azul petróleo | 6.15 | 5.65 | 8.67 | 11.20 | 6.22 | 5.02 |
| s3 pizarra | 6.83 | 6.24 | 8.35 | 11.09 | 5.92 | 4.77 |

`npm run contraste:verificar` recalcula estas tablas (y las del claro) desde `tokens.css` en cada PR y falla si un par baja del mínimo. La base de un grado no se usa como trazo sobre la pizarra: ahí queda debajo de 3:1, por eso existe `--grado-trazo`.

### Named Rules
**La regla del color por grado.** El color del grado vive en la portada, la tapa, la barra de avance, la celda activa y los tintes de la pantalla del grupo. Nunca en botones de acción ni en texto largo.

**La regla del texto sobre color.** Sobre la base de un grado el texto va en `sobre-grado` o en el rótulo de ese grado; sobre un tinte, en la tinta del mismo grado. Nunca gris.

**La regla de la acción azul.** La acción principal siempre es `accion`, en cualquier grado. Si el botón cambiara de color con el grupo, el docente tendría que volver a buscarlo.

## Typography

**Fuente de texto:** Lexend Variable (con `Lexend, ui-sans-serif, sans-serif`), autoalojada con `@fontsource-variable/lexend` 5.3.0.
**Fuente de cifras:** "Aula Cifras": los dígitos tabulares de Figtree (`@fontsource-variable/figtree` 5.3.0), limitados por `unicode-range` a dígitos, punto y coma. Mismo alto de cifra que Lexend (700/1000).

**Character:** Lexend se diseñó para leer con facilidad; es abierta, ancha y amable, del mundo escolar sin ser infantil. Las cifras tabulares mantienen las columnas quietas.

### Hierarchy
- **Número de grado** (700, 7.5rem, 1): solo en la portada, recortado contra el borde superior derecho.
- **Título** (700, 2rem, 1.2, -0.01em): el `h1` de cada pantalla.
- **Título de portada** (700, 1.1875rem, 1.2): nombre del grupo o de la app en la portada.
- **Subtítulo** (500 a 700, 1.5rem o 1.1875rem): secciones.
- **Cuerpo** (400, 1rem, 1.5): texto de lectura, máximo 40rem de ancho.
- **Etiqueta** (500, 0.8125rem): datos secundarios bajo un título, nunca en mayúsculas sostenidas.
- **Dato** (500, 1rem, clase `.cifras`): calificaciones, promedios, conteos, fechas.

Tamaños disponibles: 13, 15, 16 (mínimo en campos), 19, 24 y 32 px, y 120 px para el número de grado.

### Named Rules
**La regla de las cifras.** Toda calificación, promedio, conteo o fecha lleva la clase `.cifras`. Sin ella, Lexend usa cifras proporcionales y las columnas bailan al capturar.

**La regla del grado escrito.** El grado se escribe con número y símbolo de grado ("4° B"), nunca con palabras.

## Layout

Primero celular (360 px), luego escritorio. Márgenes laterales de 16 px más el área segura (`env(safe-area-inset-*)`). Texto de lectura con ancho máximo de 40 rem. Objetivos táctiles de al menos 44 px. Alturas con `dvh`. Espaciado en múltiplos de 4 px (`--espacio-1` a `--espacio-7`): 12 px entre elementos de un grupo, 24 a 32 px entre secciones.

### Pantalla ancha

Desde 48 rem, las pantallas de cuenta (una sola columna corta) se centran y el resto ocupa un contenedor de 80 rem (`--ancho-app`, 1280 px) centrado, con el texto alineado al de la portada. Desde 64 rem cada pantalla reparte el ancho en lugar de estirar la columna del celular; ninguna pantalla del docente queda como una columna angosta centrada:

- Mis grupos: el ciclo es un estante y cada grupo, el libro completo: la tarjeta es la tapa del color del grado, con lomo, el número grande recortado como en la portada y el nombre abajo. "Crear grupo" sube a la cabeza.
- Grupo: la lista a la izquierda y, a la derecha, un panel con las formas de agregar alumnos, sin abrir la hoja.
- Calificaciones, rubros y captura: el contenido a la izquierda y un panel lateral fijo con el resumen de la unidad, la suma de pesos o el teclado rápido. En rubros, nombre, tipo, peso y mínima van en una sola fila.
- Listas largas de alumnos (más de 9) en Alumnos, Captura sin criterios y Pasar lista: desde 75 rem, donde cada columna mide al menos 22 rem, van en dos columnas que se leen de arriba abajo en el orden de la lista. El orden del DOM, el teclado y las flechas no cambian. Cierre y Boletas siguen en una columna: sus etiquetas de estado son largas.
- Tabla de calificaciones: solo escritorio, a todo el ancho del contenedor y con desplazamiento propio; el nombre de la actividad se lee hasta 10 rem.
- Crear grupo: campos a la izquierda y, a la derecha, los libros como quedarán en el estante.
- Ciclo: los datos del ciclo a la izquierda, fijos al desplazar, y las unidades a la derecha, con nombre, desde y hasta en una fila.
- Avisos: cada aviso es un renglón con título y fecha, "Leído por" y su barra lado a lado. Nuevo aviso: lo que se escribe a la izquierda y, en una columna fija, el urgente, cómo llegará y "Enviar aviso". Detalle: el aviso y la lista por tutor a la izquierda, y fijo a la derecha quién lo leyó y "Compártelo tú".
- Estilo de las observaciones: longitud, persona y Guardar a la izquierda; los ejemplos a la derecha.
- Configuración del grupo, ficha del alumno y ajustes: dos columnas que conservan el orden del celular al leerse de izquierda a derecha.

Cuando una pantalla pasa a dos columnas, el orden de lectura del celular se conserva: se acomodan las piezas con la rejilla (`grid-column`, `grid-row`) en lugar de mover el HTML, y lo que va fijo abajo en celular sigue dentro del mismo formulario.

La acción fija abajo (`.pie-accion`) es del celular y la tableta (menos de 64 rem, donde la página es de una columna). Lo que solo existe en un tamaño usa `.solo-celular` o `.solo-escritorio`, con el mismo corte de 64 rem que `.columnas`: el panel lateral solo aparece donde hay dos columnas. El alto real del pie se publica en `--alto-pie-accion` para que la fila o el campo con foco no queden debajo.

La portada ocupa todo el ancho arriba, de 72 px más el área segura como mínimo; crece con el título, no con adorno. La acción principal va abajo, al alcance del pulgar.

**La regla de la portada.** Toda pantalla del docente empieza con la portada. Dentro de un grupo, la portada lleva su color y su número; fuera de un grupo, el azul de la marca sin número.

## Elevation & Depth

Plano por defecto: las capas se separan con cambio de superficie (papel, hoja, papel hundido) o con la divisoria de 1 px. Solo lo que flota lleva sombra, semitransparente y entintada de azul pizarra; nunca bordes grises sólidos para separar lo que flota.

### Shadow Vocabulary
- **Lomo** (`--sombra-lomo`: `inset 5px 0 0` de tinta al 16%): solo en la tapa de libro. Es el lomo, no una sombra de elevación.
- **Flotante** (`--sombra-flotante`): menús, popovers, toasts y el enlace "Saltar al contenido".
- **Hoja** (`--sombra-hoja`): hojas inferiores en celular.
- **Foco** (`--foco`: anillo de 3 px de la acción al 40%): foco por teclado en todo control.

## Shapes

Esquinas redondeadas y amables: 4 px en chips y la barra de avance (`sm`), 10 px en campos y celdas de captura (`md`), 12 px en botones, avisos y paneles (`lg`), 16 px arriba en hojas inferiores y diálogos (`hoja`). La tapa de libro tiene el lomo más recto que el canto (`4px 8px 8px 4px`). Los círculos completos (`pleno`) se reservan para indicadores de estado. No hay franjas de color en los bordes.

## Components

### Portada (`app-portada`, `src/app/compartido/portada/portada.ts`)
La cabecera de la app y de cada pantalla de grupo. Entradas: `titulo` (obligatoria), `subtitulo`, `numero` ("4") y `grado` (`p1` … `s3`).
- **Fondo:** `--grado`; sin grado, `marca`. **Texto:** `sobre-grado`; subtítulo en `--grado-rotulo`.
- **Número:** 120 px, peso 700, en el color del grado mezclado al 80% con `sobre-grado`, pegado arriba a la derecha y recortado. Decorativo (`aria-hidden`); el título ya nombra el grupo.
- Con número, el texto reserva 5.5rem a la derecha para no encimarse.
- Cambiar de grupo cambia el color sin animación: es navegación frecuente.

### Tapa de libro
El grupo en la lista "Mis grupos". 56 × 64 px, fondo `--grado`, texto `sobre-grado` con el grado ("4°", 22 px, 700, `.cifras`), esquinas `--radio-tapa` y `--sombra-lomo`. A la derecha: nombre del grupo (700), datos en etiqueta `texto-suave` y una barra de avance de 6 px (pista `superficie-hundida`, relleno `--grado`, `--radio-sm`). La fila entera es el objetivo táctil. Dos grupos del mismo grado comparten color; los distingue la letra del grupo y la materia.

### Botones
- **Primario:** fondo `accion`, texto `sobre-accion`, 700, 48 px de alto, `--radio-lg`. Presionado: `scale(0.97)` en `--dur-press`; con puntero fino, el hover cambia a `accion-presionada`. Uno por pantalla.
- **Secundario:** fondo `superficie-hundida`, texto `texto`, 44 px, `--radio-lg`.
- **Foco:** anillo `--foco`.

### Campo
Fondo `superficie-hundida`, texto `texto` a 16 px (iOS no hace zoom), 48 px de alto, `--radio-md`, sin borde. Etiqueta visible arriba (500, 15 px). Con foco: fondo `superficie` y anillo `--foco`. Error: borde interior de 2 px `peligro` y mensaje en `peligro-tinta` debajo. Marcador de posición en `texto-suave`.

### Fila de captura
Número de lista (`texto-suave`, `.cifras`), nombre del alumno y la celda de calificación (56 × 40 px, `--radio-md`, `.cifras`, 500). Divisoria de 1 px entre filas.
- **Vacía:** fondo `superficie-hundida`.
- **Activa:** fondo `superficie` con borde interior de 2 px `--grado`.
- **Guardada:** fondo `--grado-tinte`, texto `--grado-tinta`; el cambio de fondo dura 160 ms (catálogo de la regla 07).
- **Botones rápidos** (10, 9, 8, 7, 6, "No entregó"): fondo `superficie-hundida`, 700, 44 px, `--radio-lg`, `scale(0.97)` al presionar. Pasar al siguiente alumno no se anima.

### Pestañas del grupo (`app-pestanas-grupo`)
Bajo "Mis grupos": Calificaciones (abre primero), Alumnos, Asistencia y Avisos, como navegación con `aria-current`, no como pestañas ARIA. La activa lleva texto `--grado-tinta` y una raya inferior de 3 px `--grado`; la configuración queda a la derecha (solo ícono bajo 30 rem).

### Calificaciones de la unidad
Chips de unidad (la elegida con fondo `--grado`), la barra "Pesos: n de 100" en `--grado-tinte` (en `aviso-tinte` si no suman 100) y los rubros como secciones con su peso en `--grado-tinta`; cada actividad es una fila con fecha y avance (capturadas/activos y barra de 6 px). En escritorio, el resumen de la unidad, "Nueva actividad", "Ver tabla" y "Editar rubros" van en el panel lateral.

### Editor de rubros
Un bloque por rubro separado por divisoria: número en `--grado-tinta`, nombre (con nombres sugeridos del tipo), tipo, peso con "%", mínima, forma (Con número / Cumplió ✓ / ✗) y la casilla de criterios. Los criterios van en un panel `superficie-hundida` con sus campos en `superficie`. La suma de pesos está siempre a la vista: pie fijo en celular, panel lateral en escritorio.

### Teclado rápido
Cuatro columnas: 10, 9, 8, 7 / 6, 5, "No entregó", "Exento"; arriba dice para quién ("Para 7 Jiménez Ríos Sebastián", hasta dos renglones). En pantalla baja (celular de lado o texto grande) las teclas van en un solo renglón que se desliza. Fijo abajo en celular, panel lateral en escritorio. Las teclas no roban el foco; con criterios, los números van al criterio con foco. La fila activa lleva fondo `--grado-tinte` y raya izquierda de 3 px `--grado`.

### Cumplimiento
Dos botones por alumno, ✓ y ✗, de 56 × 44 px. Elegido: tinte y borde interior de 2 px de `exito` o `peligro`, no solo color. Tocar de nuevo el elegido lo limpia.

### Tabla (escritorio)
Alumnos por actividades, agrupadas por rubro con su columna "Prom." en `superficie-hundida`, y al final Unidad y Final (Final en `--grado-tinta`). Encabezado y columna de nombres fijos al desplazar; celdas de 56 × 36 px con fondo `--grado-tinte` cuando tienen valor. Las actividades con criterios muestran su resultado en `texto-suave` y se capturan en su vista.

### Equipos del grupo
"Sin equipo" arriba sobre `superficie-hundida` y un bloque por equipo con su nombre editable, el conteo de integrantes y quitar; en escritorio los equipos van en cuadrícula y "Guardar equipos" en el panel lateral. Mover a un alumno es un toque: se toca su fila y la hoja "Mover a…" lista los equipos (el actual con `aria-current`), "Nuevo equipo" y "Sin equipo". Sin arrastre y sin animación al mover.

### Captura por equipo
La fila del equipo lleva el ícono de equipo, su nombre y la celda; debajo, sus integrantes con sangría, su valor como texto y "Ajustar". Un integrante ajustado se vuelve fila de captura normal con "Ajustado" en `aviso-tinta` y "Usar la del equipo". "Sin equipo" va al final con filas normales. En el teclado, "Para" dice "Equipo 1 (4)". En la tabla, un ícono de equipo de 14 px antes del nombre de la actividad.

### Ficha del alumno
Dos vistas con la misma pestaña subrayada en `--grado` que el grupo: Avance (abre) y Datos. Avance: chips de unidad, el promedio de la unidad en `texto-2xl` y `--grado-tinta`, rubros en desplegables con peso, promedio y una barra de proporción (promedio sobre 10), y Notas: área de texto con la pista de dictado del teclado, contador visible desde 800 caracteres, etiqueta única (Ninguna, Fortaleza, Área de mejora, Conducta) y la lista de la más nueva a la más vieja; una nota sin enviar dice "Por enviar" en `aviso-tinta`. El texto fijo "Solo tú ves estas notas" recuerda que nunca llegan a tutores.

### Pase de lista
Barra del día en `superficie-hundida`: día anterior, la fecha larga ("Hoy, sábado 17 de octubre") con su unidad debajo, día siguiente y el selector nativo de fecha. Sin lista ese día, un aviso en `--grado-tinte` y "Todos presentes" como acción principal (pie fijo en celular, panel lateral en escritorio); tocar a un alumno también crea la lista. La fila entera es el botón: número, nombre y la etiqueta del estado con la palabra completa. Presente: etiqueta en `--grado-tinte`; Falta, Retardo y Justificada tiñen la fila (`peligro-tinte`, `aviso-tinte`, `superficie-hundida`) y su etiqueta va en `superficie` con la tinta del estado. Cada toque recorre presente, falta, retardo y justificada; con teclado, flechas entre filas y P, F, R o J. Solo el fondo de la fila cambia, en 160 ms. Con lista, el resumen del día (presentes, faltas, retardos, justificadas), "Historial de la unidad" y "Borrar lista de este día" con confirmación.

### Historial de asistencia
Chips de unidad. En escritorio, tabla de alumnos por días con la letra (P, F, R, J) sobre el tinte de su estado y la columna "%" fija a la derecha en `--grado-tinta`; encabezado y nombres fijos, y cada fecha abre ese día. En celular, una fila por alumno con su porcentaje y solo lo que tuvo ("2 faltas · 1 retardo" o "Sin faltas"), y abajo "Días con lista" del más nuevo al más viejo.

### Cierre de la unidad
Sin iniciar: qué cambia al cerrar y los dos requisitos de RN-040 como renglones con check en `exito-tinte` o pendiente en `aviso-tinte`, cada pendiente con su enlace para resolverlo; "Iniciar cierre" solo cuando ambos se cumplen. Iniciado: "n de m aprobadas", una barra segmentada (aprobadas en `exito`, por revisar en `--grado`, sin texto en la pista hundida) y el conteo de cada segmento con su filete del mismo color. La lista es la de asistencia: número, nombre, calificación con 2 decimales y la etiqueta del estado con palabra completa (Sin generar hundido, Generando en `aviso-tinte` con ícono que gira, Borrador en `--grado-tinte`, Aprobada en `exito-tinte` con check); "Datos cambiaron" (`aviso-tinte`) y "No se pudo generar" (`peligro-tinte`) se suman a la etiqueta. "Generar observaciones (n)" va en el pie fijo del celular y en el panel lateral del escritorio. La consulta cada 3 s solo corre mientras hay alumnos generando; la etiqueta que llega entra con fundido y 4 px de subida en 240 ms, escalonada 40 ms (con movimiento reducido, solo el fundido).

### Revisar observación
"3 de 15" sobre el nombre, con anterior y siguiente como botones de ícono. Los datos (calificación de la unidad en `texto-xl`, promedio por rubro y notas de la unidad con su etiqueta) van en un desplegable hundido en celular y en el panel izquierdo fijo en escritorio. La observación se lee como texto de libro: papel en `superficie` con borde `divisoria` y renglón inferior, 18 px, medida máxima de 42 rem, y el campo crece con el texto. Debajo, el conteo "72 palabras · Normal: 60 a 90", en `aviso-tinta` si sale del rango. "Pedir otra versión" es un bloque hundido con las sugerencias como chips (la elegida en `--grado`), la instrucción libre y "Te quedan n". "Aprobar y siguiente" es la acción principal (pie fijo en celular) y no se anima; aprobar la última muestra el sello "Aprobada" que se descubre de izquierda a derecha con `clip-path` en 280 ms, con "Reabrir" y "Siguiente por revisar" al lado. Ctrl + Enter aprueba y sigue.

### Boletas
Arriba "n de m publicadas" y el conteo con filete (publicadas en `exito`, listas en `--grado`, sin boleta en `aviso`, sin aprobar hundido). Mientras se preparan los PDF, "Preparando 18 de 30" con una barra de avance real que crece con `transform` en 240 ms. La lista es la del cierre con casilla al inicio de las listas: Falta aprobar la observación y Preparando (con ícono que gira) en `aviso-tinte`, Lista en `--grado-tinte`, Publicada en `exito-tinte` con check; "Versión n", "Desactualizada" y "Cambió después de publicar" se suman a la etiqueta. Tocar una fila con boleta abre su PDF; "Emitir versión corregida" va a la derecha de la fila (debajo en celular). "Generar boletas (n)" y "Publicar y avisar a tutores (n)" van en el pie fijo del celular y en el panel lateral; abajo, como enlaces, el PDF del grupo, Excel y cerrar o reabrir la unidad, cada uno con confirmación. Publicar abre la hoja con los contactos (WhatsApp, correo, notificación, "Se los compartes tú" en `aviso-tinta` y sin tutor con sus nombres plegables). Si al publicar hay tutores sin ningún canal, sube la hoja "Compártela tú": una fila por tutor con sus hijos y "Enviar por WhatsApp", que pasa a "Enviada" secundario. La primera vez que el docente publica aparece junto al total el sello "Publicadas" con borde `exito`, descubierto con `clip-path` en 280 ms; con movimiento reducido solo aparece.

### Portal del tutor
Sin la navegación del docente: la portada es azul con "Hola, Verónica" en Mis hijos y toma el color del grado en el detalle y la boleta. Mis hijos es la lista del docente: tapa del grado, nombre completo en `texto-lg` (se parte, nunca se corta), "4° B · docente" y a la derecha la final más reciente en cifras con su periodo y fecha, o "Aún no hay boletas". Con un solo hijo se abre directo su detalle. En el detalle, con varios hijos, una fila de chips con el nombre de pila (con materia o grupo si se repite) cuya pastilla activa se desliza en 240 ms; pestañas Boletas, Calificaciones y Avisos del mismo ancho con subrayado que se desliza en 200 ms; Avisos lleva una pastilla `--grado` con el número sin leer. Cada aviso: "Urgente" en `aviso-tinte` y "Nuevo" en `--grado-tinte`, título en `texto-lg`, fecha y docente suaves, y el cuerpo completo con sus saltos de línea. En Mis hijos, bajo cada hijo y a todo lo ancho, "Aviso: título · fecha" en una línea (se corta con puntos suspensivos) con un punto de acción si no se ha leído. Notificaciones: dos bloques con ícono (campana y sobre): activar el push o su estado (activadas en `exito-tinta`, bloqueadas con cómo permitirlas, en iPhone los tres pasos para agregar a inicio) y el correo con Guardar. Cada periodo es una tarjeta con borde: unidad, ciclo, "Publicada el …" y la final en `--grado` grande; "Versión corregida el dd/mm/aaaa" en `aviso-tinte`. La tarjeta se abre en la cabecera de la boleta (`--grado-tinte`) con View Transition en 300 ms; con movimiento reducido solo funde. La boleta lleva la tabla de rubros (peso y promedio), la observación en `texto-lg` con interlineado 1.6, "Descargar PDF" como acción principal, el código de verificación y la leyenda de RN-055. Enlace inválido: ícono de enlace roto en un círculo, "Este enlace ya no funciona" y qué hacer; sin botones.

### Avisos del grupo
Lista sin animación de entrada: título fuerte con la fecha corta a la derecha, "Urgente" en `aviso-tinte` y "Leído por n de m" con una barra de 4 px en `--grado` (máximo 16 rem). "Nuevo aviso" en el pie fijo del celular y arriba a la derecha en escritorio. Sin tutores, el vacío lleva a Alumnos. Nuevo aviso: "Para" como elección de dos (Todos los tutores (n) o Elegir, que abre una lista con búsqueda y casillas), título y cuerpo con el contador solo cerca del tope, el interruptor "Urgente: también por WhatsApp" en una tarjeta hundida (encendido en `aviso-tinta`, apagado con su razón si no queda saldo) y "Cómo llegará" en cuatro cifras de dos en dos; "Lo compartes tú" en `aviso-tinte` si es mayor que cero. Detalle: el aviso como se lee (cuerpo en `texto-lg`), "Leído por n de m" con barra de 8 px que crece con `transform` en 240 ms solo cuando el conteo cambia, "Compártelo tú" en un panel `aviso-tinte` con un botón de WhatsApp por tutor ("Compartido el …" en `exito-tinta`), la lista por tutor con "Leído el …" en `exito-tinta` o "Sin leer" y los canales como etiquetas hundidas (WhatsApp fallido en `peligro-tinte`), y "Borrar aviso" como enlace de peligro al final.

### Enlace del tutor (ficha del docente)
Debajo de cada tutor, en texto suave, "Abrió su enlace el 3 de octubre de 2026" o "Aún no abre su enlace". "Enviar enlace" (con ícono de cadena) va primero; en celular ocupa su propio renglón. Abre una hoja con "Enviar por WhatsApp" (principal, `wa.me` con el mensaje escrito), "Copiar enlace" (cambia a "Enlace copiado") y abajo, como enlace, "Regenerar enlace", que pide confirmación explicando que el anterior deja de funcionar. Enlace sospechoso: aviso en `aviso-tinte` sobre las acciones del tutor con "Regenerar enlace".

### Estilo de las observaciones
En Ajustes. Longitud y persona como elección de tres y dos opciones con su rango o un ejemplo de frase debajo; hasta tres ejemplos en áreas de texto con contador; el aviso de no poner nombres va en `aviso-tinte` antes de los ejemplos.

### Plan y candados Pro

Plan es una pantalla de ajustes (modo operar) con la portada azul de la app y el título "Tu plan". Arriba, el estado en frases de `texto-xl` peso fuerte, una por hecho, sin tarjeta; si hay pago pendiente, la banda va antes. "Grupos en Pro" es la lista de Mis grupos con una casilla al frente (toda la fila es el objetivo táctil) y la etiqueta del plan a la derecha. El total es la cifra en `texto-2xl` y `accion-tinta` con "MXN al mes" al lado y el desglose en cifras debajo ("79 primer grupo + 2 × 29"); no se anima, porque cambia con cada casilla. La forma de pago son opciones apiladas en `superficie-hundida` (la elegida con tinte y borde interior de acción), cada una con su monto; una opción que no aplica se apaga y una nota dice por qué. Una sola acción primaria que nombra lo que cobra ("Pagar 137 MXN al mes", "Generar ficha OXXO de 324 MXN", "Guardar cambios"); en celular va fija abajo, en escritorio en el panel lateral, plano, sin sombra. "Qué incluye" es una tabla de seis renglones con ✓ en `exito-tinta` y una raya en `texto-tenue`, nunca una cuadrícula de íconos. El historial de pagos es una lista de filas con concepto, fecha, monto y estado en chip (`exito-tinte` pagado, `aviso-tinte` por pagar).

El candado Pro es un bloque plano en `--grado-tinte` del grupo con el ícono de candado, título `texto-lg`, una frase de lo que da y un solo camino: "Usar mi prueba Pro en este grupo" (primario) y "Ver planes" (enlace), o "Activar Pro en este grupo". La etiqueta del plan es un chip `sm` de 24 px: "Pro" en `accion-tinte`, "Prueba Pro · quedan n días" en `aviso-tinte`; Gratis no lleva etiqueta. La banda de pago usa el aviso en línea: `aviso-tinte` para el prepago por vencer y `peligro-tinte` (con `role=alert`) para el cobro fallido o el prepago vencido. "Pago de prueba" es papel con borde `divisoria` y sin sombra, con la referencia OXXO en bloques de cuatro cifras; no imita al proveedor. Sin movimiento nuevo.

### Panel de administración

`/admin` (SPEC-09, modo operar) es la libreta de control de la escuela: papel, tinta pizarra y cifras tabulares; el color solo dice estado (`exito`, `aviso`, `peligro`) y la tapa chica del grado donde aparece un grupo. La portada azul lleva "Administración". En escritorio las secciones van en una columna fija a la izquierda con la nota de privacidad debajo; en celular, en una fila de chips que se desliza. Métricas es la "boleta del piloto": una fila por métrica con su meta debajo, una columna por semana y la semana en curso al final sobre `superficie`, con la cifra en un tinte `exito` (cumple) o `aviso` (falta); la columna de la métrica queda fija y en celular la boleta abre en la semana actual. Sin datos es una raya, nunca 0 %. Las demás secciones son tablas `libreta` (divisoria por renglón, encabezados `texto-xs`, cifras a la derecha); en celular cada renglón es una ficha de dos columnas con la etiqueta arriba del dato. Los estados van en `sello` (chip `texto-xs` con tinte), distinto de la marca ✓/✗ de la captura. Tipos de rubro se edita en la hoja de la app; desactivar es un interruptor con su consecuencia escrita. Auditoría son registros con quién, cuándo y una tabla de antes y después. Sin gráficas, sin tarjetas de KPI y sin movimiento nuevo: solo el de la hoja y el de los mensajes.

### Indicador de sincronización
En la navegación de la portada: ícono y texto ("Guardado", "Por enviar: n", "Enviando…", "Sin conexión: guardado aquí"). El ícono entra con fundido y desenfoque de 2 px en 200 ms; el de "Enviando" gira, salvo con movimiento reducido.

### Actividad seleccionada
Bloque con fondo `--grado-tinte`, texto `--grado-tinta`, `--radio-lg`, 12 px de relleno. No es tarjeta: no lleva sombra ni borde.

## Do's and Don'ts

### Do:
- **Do** poner `data-grado` en el contenedor de la pantalla del grupo y usar `--grado`, `--grado-tinta` y `--grado-tinte`.
- **Do** usar la portada en toda pantalla del docente, con número solo cuando hay grupo.
- **Do** usar `.cifras` en toda calificación, promedio, conteo y fecha.
- **Do** usar controles estándar de la web (botones, pestañas, tablas, selects) con los tokens.

### Don't:
- **Don't** copiar logotipos, escudos, ilustraciones, tipografías ni textos de la SEP o de los libros de texto reales; solo se toma la idea del color por grado y la portada.
- **Don't** pintar botones de acción con el color del grado.
- **Don't** usar franjas de color en bordes laterales, tarjetas dentro de tarjetas ni cuadrículas idénticas de ícono, título y texto.
- **Don't** usar negro o blanco puros, degradados morado a azul ni neón sobre fondo oscuro.
- **Don't** animar la captura de calificaciones, el cambio de grupo ni nada iniciado con teclado.
- **Don't** hacerla infantil: sin ilustraciones de caricatura, letras redondas de juguete ni colores saturados de relleno fuera de la portada y las tapas.
