# Reglas del frontend (Angular)

## Base

- Versión estable más reciente de Angular al iniciar el proyecto, fijada en `package.json`. Actualizaciones mayores en un PR propio.
- Componentes standalone, `ChangeDetectionStrategy.OnPush`, signals para el estado local y servicios con signals para el estado compartido. Sin NgRx en el MVP.
- Control flow nativo (`@if`, `@for`) y `inject()` en lugar de inyección por constructor.
- TypeScript en modo `strict`. Prohibido `any` salvo con comentario que explique por qué.
- Componentes de interfaz: Angular CDK para comportamiento y componentes propios con los tokens de `DESIGN.md`. Angular Material solo para piezas complejas (por ejemplo, selector de fecha) con el tema del sistema. No se agregan otras librerías de componentes sin ADR.
- Diseño visual, transiciones y movimiento: obligatorio seguir `07-diseno-y-movimiento.md` (Impeccable como base, mejorado con los skills de animación y diseño). Animaciones con transiciones CSS y `animate.enter` / `animate.leave`; no se usa `@angular/animations`. Cambios de ruta con View Transitions (`withViewTransitions()`).

## Estructura

```
src/app/
  core/       autenticación, interceptores HTTP, sincronización sin conexión, manejo de errores
  shared/     componentes, pipes y utilidades sin lógica de negocio
  docente/    una carpeta por módulo: grupos, evaluacion, cierre, boletas, avisos, cuenta, planes
  tutor/      portal del tutor (cargado de forma diferida, separado del bundle del docente)
  api/        cliente generado desde OpenAPI (no se edita a mano)
```

- Rutas con carga diferida por módulo. El portal del tutor y la app del docente son bundles separados.
- La lógica de cálculo de promedios vive en `core/calculo/` y se prueba con el mismo archivo de vectores que el backend (RN-036).

## Sin conexión (PWA)

- `@angular/service-worker` para cachear la app.
- IndexedDB a través de Dexie, encapsulado en `core/sincronizacion`. Ningún componente toca IndexedDB directamente.
- Toda mutación de captura pasa por la cola de salida (`ColaSincronizacion`) con UUID de idempotencia; nunca una llamada HTTP directa desde un componente de captura.
- El indicador de estado de sincronización es un componente único en la barra superior.

## UX y accesibilidad

- Diseño primero para celular (360 px de ancho), luego escritorio.
- Objetivos táctiles de al menos 44 px; campos numéricos con `inputmode="decimal"`.
- WCAG 2.1 AA: etiquetas en todos los campos, contraste suficiente, navegación por teclado. axe corre en las pruebas end-to-end.
- Ninguna acción destructiva sin confirmación; ninguna pantalla sin estado vacío, de carga y de error.
- Todos los textos en archivos de traducción `es-MX` (Transloco); ningún texto visible escrito en las plantillas.

## Rendimiento

- Presupuesto en `angular.json`: bundle inicial máximo 250 KB comprimido (RNF-011); el build falla si se excede.
- Lighthouse móvil en CI sobre la pantalla de captura y el portal del tutor (RNF-010).
- Listas largas con `@for` y `track` por ID; virtual scroll si pasan de 100 elementos.

## Seguridad

- El token de acceso vive en memoria; el de actualización en cookie `HttpOnly` que el navegador maneja. Nada sensible en `localStorage`.
- Sin `innerHTML` con contenido del usuario. Las observaciones se muestran como texto.
- Content Security Policy estricta configurada en nginx.

## Pruebas

- Unitarias con el runner por defecto del proyecto Angular para servicios, cálculo y componentes con lógica.
- End-to-end con Playwright para los flujos de los criterios de aceptación: registro, importación, captura sin conexión, cierre, publicación y portal del tutor.
- Las pruebas end-to-end usan la API real contra una base de pruebas, no mocks.
