# ADR-0010: Diseño con Impeccable mejorado con los skills de animación y diseño

**Estado:** Aceptada · **Fecha:** 11/10/2026 (ampliada el mismo día a todos los skills de animación disponibles, a petición del dueño del producto)

## Contexto

El dueño del producto quiere que las pantallas tengan un diseño cuidado y transiciones con movimiento, sin 3D. La interfaz se construye en buena parte con agentes de IA, que tienden a producir diseños genéricos y animaciones mal elegidas (por ejemplo `ease-in` en entradas o `transition: all`). El docente usa la app en el celular y repite la captura cientos de veces, así que una animación mal puesta lo hace más lento.

## Decisión

- El diseño visual de cada pantalla se hace y se revisa con el skill **Impeccable** (`pbakaus/impeccable`). `PRODUCT.md` y `DESIGN.md` en la raíz del repo son su fuente de contexto.
- Sobre esa base se combinan todos los skills de animación y diseño disponibles:
  - Los de Emil Kowalski (`emilkowalski/skills`).
  - Design Motion Principles (`kylezantos/design-motion-principles`), con los criterios de Emil Kowalski, Jakub Krehel y Jhey Tompkins.
  - Los de diseño de Claude: design-system, design-critique, design-handoff, accessibility-review y ux-copy.
- Cuando se contradicen, el orden de prioridad está en la regla 07: accesibilidad y rendimiento, contención en acciones frecuentes, Impeccable en lo visual, curvas de Emil y Apple, y pulido.
- Las reglas concretas (curvas, duraciones, qué no se anima, catálogo de transiciones) se fijan en `reglas/07-diseno-y-movimiento.md`, para que no dependan de la versión del skill.
- Angular CDK con componentes propios en lugar de usar Angular Material como capa visual principal.

## Alternativas

- Angular Material con su tema por defecto: rápido, pero con aspecto de plantilla.
- Animaciones libres a criterio de cada tarea: inconsistentes y riesgosas en pantallas de uso intensivo.

## Consecuencias

- H0 incluye instalar los skills, crear `PRODUCT.md`, `DESIGN.md` y los tokens antes de la primera pantalla.
- Cada PR con interfaz anota los comandos de revisión que corrió (definición de terminado).
- Hacer componentes propios cuesta más al inicio; se compensa con CDK para foco, overlays y accesibilidad.

## Actualización del 11/10/2026: estilo visual "Libros de texto"

El 11/10/2026 el dueño del producto eligió el estilo **"Libros de texto"** entre tres propuestas (líneas y estaciones, libros de texto y estándar de apps). Reemplaza el mundo de señalética de transporte ("líneas y estaciones") con el que se cerró H0.

- Cada grupo lleva el color de su grado (1° a 6° de primaria y 1° a 3° de secundaria), como las portadas de los libros de texto gratuitos. La cabecera es una **portada** en ese color con el número del grado muy grande y recortado; los grupos se muestran como **tapas de libro** con lomo.
- Letra **Lexend** (autoalojada con Fontsource). Como Lexend no trae cifras tabulares, las cifras de datos usan los dígitos tabulares de Figtree con la familia "Aula Cifras", limitada por `unicode-range` a dígitos, punto y coma.
- Acción principal en azul, controles con esquinas de 10 a 12 px y fondo de papel cálido.
- Riesgos aceptados: puede sentirse infantil o confundirse con algo oficial de la SEP (no se usan logotipos, escudos ni textos de la SEP), y dos grupos del mismo grado comparten color (se distinguen por la letra del grupo y la materia).
- Las reglas de movimiento de la regla 07 no cambian. `DESIGN.md`, `apps/web/src/styles/tokens.css` y la regla 07 se actualizaron con el estilo.
