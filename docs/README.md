# Documentación de Aula Lista

Esta carpeta es la **fuente de verdad** del producto: qué se construye, cómo debe comportarse y con qué reglas. El código sigue a estos documentos, no al revés.

## Orden de lectura

1. [00-prd.md](00-prd.md): qué es el producto, para quién y por qué.
2. [01-requerimientos.md](01-requerimientos.md): requerimientos funcionales (RF) y no funcionales (RNF) con ID y prioridad.
3. [02-arquitectura.md](02-arquitectura.md): componentes, estructura del repo, modelo de datos, API, sincronización e integraciones.
4. [specs/](specs/): una spec por módulo con reglas de negocio (RN), API, pantallas y criterios de aceptación.
5. [reglas/](reglas/): reglas obligatorias de desarrollo.
6. [decisiones/](decisiones/): decisiones técnicas registradas (ADR).
7. [03-glosario.md](03-glosario.md), [04-preguntas-abiertas.md](04-preguntas-abiertas.md) y [05-plan-de-desarrollo.md](05-plan-de-desarrollo.md).
8. [operacion/](operacion/): cómo operar el servicio: [monitoreo con Grafana Cloud](operacion/grafana-cloud.md) y [registro de restauraciones](operacion/restauraciones.md).

## Specs

| Spec | Módulo | Requerimientos |
| --- | --- | --- |
| [SPEC-01](specs/SPEC-01-cuenta-docente.md) | Cuenta y acceso del docente | RF-001 a RF-008 |
| [SPEC-02](specs/SPEC-02-grupos-alumnos-tutores.md) | Grupos, alumnos, tutores, ciclo y unidades | RF-010 a RF-022 |
| [SPEC-03](specs/SPEC-03-evaluacion-captura.md) | Evaluación y captura | RF-030 a RF-039 |
| [SPEC-04](specs/SPEC-04-cierre-observaciones-ia.md) | Cierre de periodo y observaciones con IA | RF-040 a RF-046 |
| [SPEC-05](specs/SPEC-05-boletas.md) | Boletas | RF-050 a RF-055 |
| [SPEC-06](specs/SPEC-06-portal-tutores.md) | Portal de tutores | RF-060 a RF-065 |
| [SPEC-07](specs/SPEC-07-avisos-notificaciones.md) | Avisos y notificaciones | RF-070 a RF-073 |
| [SPEC-08](specs/SPEC-08-planes-pagos.md) | Planes y pagos | RF-080 a RF-087 |
| [SPEC-09](specs/SPEC-09-operacion-metricas.md) | Operación, métricas y panel de administración | RF-090 a RF-094 |
| [SPEC-10](specs/SPEC-10-asistencia.md) | Asistencia | RF-100 a RF-103 |

## Reglas

| Archivo | Tema |
| --- | --- |
| [00-generales.md](reglas/00-generales.md) | Fuente de verdad, idioma, definición de terminado, alcance |
| [01-backend-dotnet.md](reglas/01-backend-dotnet.md) | Capas, dominio, API, persistencia, pruebas en .NET 10 |
| [02-frontend-angular.md](reglas/02-frontend-angular.md) | Angular, PWA sin conexión, accesibilidad, rendimiento |
| [03-base-de-datos.md](reglas/03-base-de-datos.md) | PostgreSQL, Row Level Security, migraciones |
| [04-seguridad-privacidad.md](reglas/04-seguridad-privacidad.md) | Datos de menores, seudonimización, LFPDPPP |
| [05-ia.md](reglas/05-ia.md) | Uso de Claude, prompts, evaluación de calidad, costos |
| [06-git-calidad-despliegue.md](reglas/06-git-calidad-despliegue.md) | Ramas, PRs, CI, despliegue en Coolify |
| [07-diseno-y-movimiento.md](reglas/07-diseno-y-movimiento.md) | Diseño con Impeccable como base, mejorado con todos los skills de animación y diseño |

## Convención de IDs

| Prefijo | Qué es | Dónde vive |
| --- | --- | --- |
| RF-### | Requerimiento funcional | 01-requerimientos.md |
| RNF-### | Requerimiento no funcional | 01-requerimientos.md |
| RN-### | Regla de negocio | Spec del módulo |
| SPEC-## | Especificación de módulo | specs/ |
| ADR-#### | Decisión registrada | decisiones/ |
| PA-## | Pregunta abierta | 04-preguntas-abiertas.md |

Los IDs son permanentes; lo eliminado se marca **Retirado**.

## Cómo se cambia un documento

Por PR, igual que el código. El PR explica el cambio, cita los IDs afectados y actualiza todos los documentos que dependan de él. Un cambio de alcance o de precio también actualiza el PRD.
