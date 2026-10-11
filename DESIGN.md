---
name: Aula Lista
description: Herramienta escolar de captura, cierre y boletas, con la claridad de la señalética de transporte público.
colors:
  fondo: "oklch(0.972 0.006 75)"
  superficie: "oklch(0.992 0.003 75)"
  superficie-hundida: "oklch(0.945 0.008 75)"
  linea-divisoria: "oklch(0.885 0.01 75)"
  texto: "oklch(0.255 0.018 60)"
  texto-suave: "oklch(0.46 0.016 60)"
  accion: "oklch(0.53 0.185 358)"
  accion-tinte: "oklch(0.95 0.03 358)"
  accion-tinta: "oklch(0.38 0.13 358)"
  exito: "oklch(0.55 0.13 150)"
  aviso: "oklch(0.74 0.15 75)"
  peligro: "oklch(0.55 0.19 27)"
  linea-1: "oklch(0.6 0.17 358)"
  linea-2: "oklch(0.52 0.15 255)"
  linea-3: "oklch(0.6 0.12 125)"
  linea-4: "oklch(0.62 0.11 210)"
  linea-5: "oklch(0.78 0.15 90)"
  linea-6: "oklch(0.56 0.18 30)"
  linea-7: "oklch(0.68 0.16 55)"
  linea-8: "oklch(0.55 0.13 160)"
typography:
  titulo:
    fontFamily: "Overpass Variable, ui-sans-serif, sans-serif"
    fontSize: "2rem"
    fontWeight: 720
    lineHeight: 1.15
  cuerpo:
    fontFamily: "Overpass Variable, ui-sans-serif, sans-serif"
    fontSize: "1rem"
    fontWeight: 420
    lineHeight: 1.5
  dato:
    fontFamily: "Overpass Variable, ui-sans-serif, sans-serif"
    fontSize: "1rem"
    fontWeight: 560
    lineHeight: 1.15
rounded:
  sm: "4px"
  md: "6px"
  lg: "10px"
  hoja: "16px"
  estacion: "999px"
spacing:
  "1": "4px"
  "2": "8px"
  "3": "12px"
  "4": "16px"
  "5": "24px"
  "6": "32px"
  "7": "48px"
---

<!-- SEED: established with the user before implementation; re-run $impeccable document once there's code to capture the actual tokens and components. -->

# Aula Lista: sistema de diseño

Los valores normativos viven en `apps/web/src/styles/tokens.css`; este archivo explica cómo usarlos. Las reglas de movimiento y el catálogo de transiciones están en `docs/reglas/07-diseno-y-movimiento.md` y ganan si algo aquí las contradice.

## Overview

Aula Lista toma prestado el lenguaje de la señalética de transporte público mexicana: cada grupo del docente es una **línea** con su color, y el periodo se recorre como una sucesión de **estaciones** (actividades, cierre, boletas). Es una herramienta de trabajo: la navegación, los controles y la composición son los estándar de una app web; el mundo solo aporta cuatro cosas: tipografía de letrero, paleta de líneas, densidad clara y la línea con estaciones como gesto propio.

El fondo es un gris cálido de concreto claro; la superficie de trabajo es casi blanca y entintada. El color fuerte aparece en franjas (la línea del grupo activo bajo la barra superior) y en las estaciones, nunca como relleno decorativo. El movimiento es corto y funcional: confirma, ubica y suaviza; lo que se repite no se anima.

## Colors

Estrategia **restringida con líneas**: neutros entintados más un acento de acción (rosa de línea) y una paleta de ocho colores de línea para identificar grupos.

- **Fondo de concreto** (`fondo`): fondo de la app. **Superficie** (`superficie`): barras y zonas de trabajo. **Superficie hundida**: filas alternas y campos.
- **Acción** (`accion`, rosa de línea): botones principales y foco. Sobre su tinte (`accion-tinte`) el texto va en `accion-tinta`, nunca en gris.
- **Estados** (`exito`, `aviso`, `peligro`): solo cuando cargan significado (guardado, pendiente de enviar, error), cada uno con tinte y tinta.
- **Líneas** (`linea-1` a `linea-8`): una por grupo, asignada al crearlo. Misma luminosidad aproximada para que ningún grupo grite más que otro.

**La regla de la franja.** El color de un grupo vive en franjas y estaciones (bordes de 6 px, círculos), no en fondos completos ni en texto largo.

**La regla del texto sobre color.** Sobre un fondo de color, el texto usa la tinta oscura de ese mismo color.

## Typography

Una sola familia: **Overpass** (variable), heredera de la tipografía de señalética de carreteras, con cifras tabulares y español completo. Títulos en peso 720 con interlineado 1.15; cuerpo en 420; datos y calificaciones en 560 con `font-variant-numeric: tabular-nums` (clase `.cifras`).

**La regla de las cifras.** Toda calificación, promedio o conteo usa cifras tabulares para que las columnas no bailen al capturar.

Tamaños: 13, 15, 16 (mínimo en campos), 19, 24 y 32 px.

## Layout

Primero celular (360 px), luego escritorio. Márgenes laterales de 16 px más el área segura. Texto de lectura con ancho máximo de 40 rem. Objetivos táctiles de al menos 44 px. Alturas con `dvh`.

**La regla de la barra con línea.** La barra superior siempre lleva bajo ella la franja de la línea activa: el docente sabe en qué grupo está sin leer.

## Elevation & Depth

Plano por defecto. Solo lo que flota (menús, hojas inferiores, diálogos) lleva sombra semitransparente entintada (`--sombra-flotante`, `--sombra-hoja`); nunca bordes grises sólidos para separar lo que flota. Las divisiones en el plano usan `linea-divisoria` de 1 px o cambio de superficie.

## Shapes

Rectángulos con esquinas de letrero: 4 px en chips, 6 px en controles, 10 px en paneles, 16 px arriba en hojas inferiores. Los círculos completos se reservan para las **estaciones** (estado de una actividad, del periodo o del servicio).

## Do's and Don'ts

- Sí: identificar cada grupo por su línea y mostrar el avance del periodo como estaciones.
- Sí: controles estándar de la web (botones, pestañas, tablas, selects) con los tokens.
- No: copiar logotipos, pictogramas ni nombres de un sistema de transporte real; solo se toma el lenguaje.
- No: tarjetas dentro de tarjetas, ni cuadrículas idénticas de ícono, título y texto.
- No: negro o blanco puros, degradados morado a azul, neón sobre fondo oscuro.
- No: animar la captura de calificaciones ni nada iniciado con teclado.
