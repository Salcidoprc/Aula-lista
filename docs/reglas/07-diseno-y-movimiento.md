# Reglas de diseño visual y movimiento

Aula Lista se diseña con **Impeccable como base** y se mejora con **todos los skills de animación y diseño disponibles**, combinados según el momento (ADR-0010, RNF-025, RNF-026). Impeccable decide el diseño visual; los demás skills afinan el movimiento, el pulido, la accesibilidad y la sensación nativa.

El movimiento es de transiciones y desplazamientos en 2D. No hay 3D, ni partículas, ni efectos decorativos que el docente vea decenas de veces al día.

## Skills que se combinan

| Capa | Skill | Instalación | Cuándo se usa |
| --- | --- | --- | --- |
| Base del diseño | **Impeccable** (`pbakaus/impeccable`) | `npx impeccable install` y después `/impeccable init` | `init`, `shape`, `craft`, `layout`, `typeset`, `colorize`, `clarify`, `harden`, `onboard`, `adapt`, `audit`, `critique`, `polish`, `animate`, `delight` en todas las pantallas |
| Movimiento | **Skills de Emil Kowalski** (`emilkowalski/skills`) | `npx skills@latest add emilkowalski/skills` | `emil-design-eng` y `animate` para cada transición. `animation-vocabulary` para pedir el movimiento con precisión. `apple-design` para gestos y hojas fluidas. `find-animation-opportunities` al cerrar cada pantalla. `review-animations` en cada PR. `improve-animations` al cerrar cada hito. `animation-performance` si algo baja de 60 fps. `mobile-native` en las pantallas del celular. `break-ui` con datos extremos. `prototype` para comparar variantes de una pieza clave antes de decidir |
| Movimiento con tres criterios | **Design Motion Principles** (`kylezantos/design-motion-principles`) | `npx skills add kylezantos/design-motion-principles` | Auditoría de movimiento con tres criterios: Emil Kowalski (contención), Jakub Krehel (pulido de producción) y Jhey Tompkins (juego y creatividad), con el peso de abajo |
| Sistema y entrega | **Skills de diseño de Claude** (design-system, design-critique, design-handoff, accessibility-review, ux-copy) | Ya disponibles en las sesiones de Claude del proyecto | `design-system` para auditar tokens y variantes. `design-handoff` para especificar cada pantalla con estados y animaciones antes de programarla. `accessibility-review` como segunda revisión WCAG. `ux-copy` para botones, errores y estados vacíos |
| Referencia | **frontend-design** de Anthropic | Incluido en el origen de Impeccable | Solo como referencia; si contradice a Impeccable, gana Impeccable |

No se usan `animate-expo`, `write-swift` ni `ask-sonner`, que son para React Native, Swift y React. Si aparece un skill de animación nuevo que valga la pena, se agrega a esta tabla por PR.

### Cómo se combinan y quién gana

Peso de los tres criterios de movimiento según la parte de la app:

| Parte de la app | Criterio principal | Secundario | Ocasional |
| --- | --- | --- | --- |
| App del docente (captura, cierre, grupos) | Emil: contención y velocidad | Krehel: pulido | Tompkins: solo en el primer cierre publicado |
| Portal del tutor | Krehel: pulido | Emil | Tompkins: no se usa |
| Bienvenida y estados vacíos | Krehel | Tompkins: un detalle con juego, sin rebote | Emil |

Cuando dos skills se contradicen, gana el primero de esta lista:
1. Accesibilidad (WCAG 2.1 AA, movimiento reducido) y rendimiento (RNF-010, 60 fps).
2. Contención de Emil en acciones frecuentes: lo que se repite no se anima.
3. Impeccable en todo lo visual: color, tipografía, composición, jerarquía.
4. Emil y Apple en curvas, duraciones y gestos.
5. Krehel y luego Tompkins para pulido y momentos especiales.

## Flujo por pantalla

1. **Antes de diseñar:**
   - `/impeccable shape <pantalla>` con la spec del módulo.
   - `design-handoff` para dejar estados, tamaños y animaciones por escrito.
   - `prototype` si hay dudas entre variantes.
2. **Al construir:**
   - `/impeccable craft`.
   - `animate` de Emil para cada transición del catálogo, con `animation-vocabulary` para nombrar el movimiento.
   - `apple-design` en hojas y gestos.
   - `/impeccable delight` solo donde el catálogo lo permite.
3. **Antes del PR:**
   - `/impeccable audit` y `/impeccable critique`.
   - `review-animations` y auditoría de `design-motion-principles`.
   - `find-animation-opportunities`.
   - `break-ui` con datos extremos: nombres largos, 50 alumnos, grupo vacío.
   - `mobile-native` y `accessibility-review`.
   - `ux-copy` sobre textos nuevos.
4. **Al cerrar el hito:**
   - `/impeccable polish` sobre las pantallas del hito.
   - `improve-animations` sobre todo el frontend.
   - `design-system` para detectar tokens sueltos.

El PR anota qué skills se corrieron y qué hallazgos quedaron pendientes.

## Identidad visual

- **Fuente de verdad:** `PRODUCT.md` y `DESIGN.md` en la raíz del repo, generados con Impeccable en H0. Los tokens (color, tipografía, espaciado, radios, sombras, curvas y duraciones) viven en variables CSS en `src/styles/tokens.css`; ningún componente escribe colores ni duraciones sueltos.
- **Tono:** cálido y claro, de herramienta escolar confiable. No es una app de tecnología ni de juegos.
- **Tipografía:** no usar Inter, Roboto, Arial ni la fuente del sistema como fuente principal (regla de Impeccable). La familia se elige en H0 entre fuentes con buena lectura de números (cifras tabulares para calificaciones) y soporte completo de español.
- **Color:**
  - Neutros entintados, nunca negro ni blanco puros.
  - Nada de degradados morado a azul ni de neón sobre fondo oscuro.
  - Sobre un fondo de color, el texto va en un tono más oscuro de ese color, no en gris.
  - Colores definidos en OKLCH.
- **Diseño:**
  - No meter todo en tarjetas y nunca tarjetas dentro de tarjetas.
  - Evitar las cuadrículas idénticas de ícono, título y texto.
  - Texto alineado a la izquierda.
- **Bordes y profundidad:** sombras semitransparentes en lugar de bordes sólidos grises cuando el elemento flota (menús, hojas, diálogos).
- **Componentes:** Angular CDK para comportamiento (overlay, foco, accesibilidad) y componentes propios con los tokens del sistema. Angular Material solo en piezas complejas que no conviene rehacer (por ejemplo el selector de fecha), siempre con el tema de los tokens.

## Movimiento

### Decidir si algo se anima

| Frecuencia | Decisión |
| --- | --- |
| Cientos de veces al día (atajos de teclado, pasar a la siguiente celda al capturar) | Sin animación |
| Decenas de veces al día (hover, cambiar de alumno o de actividad) | Mínima o ninguna |
| Ocasional (diálogos, hojas inferiores, toasts, cambio de pantalla) | Animación estándar |
| Rara o primera vez (primer cierre publicado, bienvenida) | Puede llevar un detalle especial |

Toda animación responde "¿para qué?": consistencia espacial, indicar un cambio de estado, retroalimentación de un toque o evitar un cambio brusco. "Se ve bonito" no basta si se repite mucho. Las acciones iniciadas con teclado nunca se animan.

### Curvas y duraciones (tokens)

| Token | Valor | Uso |
| --- | --- | --- |
| `--ease-out` | `cubic-bezier(0.23, 1, 0.32, 1)` | Todo lo que entra o responde al usuario (por defecto) |
| `--ease-in-out` | `cubic-bezier(0.77, 0, 0.175, 1)` | Elementos que se mueven de un lugar a otro en pantalla |
| `--ease-drawer` | `cubic-bezier(0.32, 0.72, 0, 1)` | Hojas inferiores y paneles laterales |
| `--dur-press` | 120 ms | Retroalimentación al presionar |
| `--dur-small` | 160 ms | Tooltips, popovers, chips |
| `--dur-medium` | 220 ms | Menús, selects, toasts |
| `--dur-large` | 300 ms | Diálogos, hojas, cambio de pantalla |

- Las animaciones de interfaz duran menos de 300 ms; las hojas inferiores pueden llegar a 400 ms.
- Nunca `ease-in` para la interfaz, ni curvas con rebote o elásticas.
- La salida es más rápida que la entrada.
- Solo se animan `transform`, `opacity` y, para revelar, `clip-path`. Nunca `width`, `height`, `top`, `left`, `margin` ni `padding`. Para alturas se usa `grid-template-rows: 0fr → 1fr`.
- Se prohíbe `transition: all`; cada transición nombra sus propiedades.
- Nada aparece desde `scale(0)`: se entra desde `scale(0.95)` con opacidad 0.
- Popovers y menús crecen desde su disparador (`transform-origin` del lado del botón).
- Botones y elementos tocables: `scale(0.97)` en `:active` con `--dur-press`.
- Tooltips: con retraso la primera vez; los siguientes, mientras el usuario recorre otros, aparecen sin retraso ni animación.
- Para la interfaz se usan transiciones CSS, que se pueden interrumpir, y no keyframes. Las entradas al montar usan `@starting-style` o `animate.enter` / `animate.leave` de Angular. No se usa `@angular/animations`.
- Escalonado de listas: de 30 a 50 ms entre elementos, máximo los primeros 8, y nunca bloquea la interacción.
- Se respeta `prefers-reduced-motion`: se quitan desplazamientos y escalas, y se conservan cambios suaves de opacidad y color que ayudan a entender. Las pruebas end-to-end corren también con movimiento reducido.

### Catálogo de transiciones del MVP

| Momento | Movimiento | Spec |
| --- | --- | --- |
| Capturar calificación y pasar al siguiente alumno | Sin animación del foco. La celda guardada cambia de color de fondo en 160 ms | SPEC-03 |
| Botones rápidos 10, 9, 8… y "No entregó" | `scale(0.97)` al presionar | SPEC-03 |
| Indicador de sincronización | Cambio de ícono con fundido y desenfoque breve (blur de 2 px) en 200 ms; el ícono de "sincronizando" gira de forma continua | SPEC-03 |
| Promedio del alumno al recalcular | El número cambia con fundido corto; sin contadores animados | SPEC-03 |
| Cambio de pantalla dentro de la app | View Transitions del router de Angular: fundido con desplazamiento de 8 px en 220 ms | Todas |
| Hojas inferiores en celular (importar, notas, filtros) | Suben desde abajo con `--ease-drawer`; se cierran deslizando hacia abajo con cierre por velocidad | SPEC-02, SPEC-03 |
| Diálogos de confirmación | Entran con `scale(0.95)` y opacidad; fondo con fundido | Todas |
| Toasts (guardado, error, enviado) | Sonner o equivalente para Angular con las mismas reglas: entran y salen por el mismo lado, apilados | Todas |
| Generación de observaciones con IA | Lista de alumnos con estado por fila; cada observación aparece con fundido al llegar, escalonado corto | SPEC-04 |
| Aprobar observación | El botón cambia de "Aprobar" a "Aprobada" con transición de ancho por `clip-path` y un check | SPEC-04 |
| Publicar boletas | Barra de progreso real; al terminar, un único detalle de celebración discreto la primera vez que el docente publica | SPEC-05 |
| Portal del tutor: cambiar entre hijos | Pestañas con indicador que se desliza y contenido con fundido | SPEC-06 |
| Abrir boleta en el portal | La tarjeta del periodo se expande hacia la vista de detalle con View Transition compartida | SPEC-06 |
| Estados vacíos y de carga | Esqueletos sin brillo animado continuo; el contenido real entra con fundido | Todas |

Cualquier animación fuera de este catálogo se justifica en el PR con la tabla de frecuencia y el peso de criterios de arriba.

## Sensación nativa en celular

Reglas del skill `mobile-native`:
- Sin estado hover pegado después de tocar: los hover solo van dentro de `@media (hover: hover) and (pointer: fine)`.
- Sin destello gris al tocar (`-webkit-tap-highlight-color: transparent`) y con un estado `:active` propio.
- Alturas con `dvh` en lugar de `100vh`. Áreas seguras con `env(safe-area-inset-*)`.
- Campos con fuente de al menos 16 px para que iOS no haga zoom. Nunca se desactiva el zoom.
- `touch-action: manipulation` en controles para quitar el retraso del toque.
- `overscroll-behavior` contenido en hojas y listas con scroll propio.
- Se prueba en un teléfono real (Android de gama media y iPhone) antes de cerrar cada hito.

## Rendimiento del movimiento

- Ninguna animación baja de 60 fps en un Android de gama media. Se revisa con `animation-performance` cuando haya duda.
- No se animan variables CSS heredadas en contenedores con muchos hijos; se anima el `transform` del elemento.
- Lighthouse móvil en CI no puede bajar por una animación (RNF-010).
