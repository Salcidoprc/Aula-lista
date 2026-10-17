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
- Cada grado tiene `-tinta` (texto oscuro sobre su tinte o sobre el fondo) y `-tinte` (fondo suave: actividad seleccionada, celda guardada). Sobre la base, el texto va en `sobre-grado` o en el tinte del mismo grado.
- En los componentes no se escribe `grado-p4`: se pone `data-grado="p4"` en el contenedor y se usan `--grado`, `--grado-tinta` y `--grado-tinte`. Sin `data-grado`, esas variables toman la familia de la acción.

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

### Named Rules
**La regla del color por grado.** El color del grado vive en la portada, la tapa, la barra de avance, la celda activa y los tintes de la pantalla del grupo. Nunca en botones de acción ni en texto largo.

**La regla del texto sobre color.** Sobre la base de un grado el texto va en `sobre-grado` o en el tinte de ese grado; sobre un tinte, en la tinta del mismo grado. Nunca gris.

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

Desde 48 rem, las pantallas de cuenta (una sola columna corta) se centran y el resto ocupa un contenedor de 72 rem (`--ancho-app`) centrado, con el texto alineado al de la portada. Desde 64 rem cada pantalla reparte el ancho en lugar de estirar la columna del celular:

- Mis grupos: los grupos de cada ciclo son tarjetas en cuadrícula, y "Crear grupo" sube a la cabeza.
- Grupo: la lista a la izquierda y, a la derecha, un panel con las formas de agregar alumnos, sin abrir la hoja.
- Calificaciones, rubros y captura: el contenido a la izquierda y un panel lateral fijo con el resumen de la unidad, la suma de pesos o el teclado rápido.
- Tabla de calificaciones: solo escritorio, a todo el ancho del contenedor y con desplazamiento propio.
- Crear grupo: campos a la izquierda y la vista de las tapas a la derecha.
- Ciclo: los datos del ciclo a la izquierda, fijos al desplazar, y las unidades a la derecha.
- Configuración del grupo, ficha del alumno y ajustes: dos columnas que conservan el orden del celular al leerse de izquierda a derecha.

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
- **Fondo:** `--grado`; sin grado, la acción. **Texto:** `sobre-grado`; subtítulo en `--grado-tinte`.
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
Bajo "Mis grupos": Calificaciones (abre primero) y Alumnos, como navegación con `aria-current`, no como pestañas ARIA. La activa lleva texto `--grado-tinta` y una raya inferior de 3 px `--grado`; la configuración queda a la derecha (solo ícono bajo 30 rem).

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
