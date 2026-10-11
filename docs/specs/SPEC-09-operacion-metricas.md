# SPEC-09: Operación y métricas

**Estado:** Aprobada para desarrollo · **Requerimientos:** RF-090, RF-091, RNF-005, RNF-009, RNF-015, RNF-016, RNF-021, RNF-022

## Objetivo

Poder medir las métricas del piloto, vigilar costos y operar el servicio sin entrar a la base de datos.

## Eventos de producto (RF-090)

Se guardan en `evento_producto` sin datos personales (solo IDs internos).

| Evento | Propiedades | Métrica que alimenta |
| --- | --- | --- |
| `docente_registrado` | origen (correo, google) | Registro |
| `grupo_creado` | nivel, alumnos | Activación |
| `calificacion_capturada` | vista (actividad, tabla, alumno), sin_conexion | Docentes activos |
| `cierre_iniciado` | grupo_id, alumnos | Cierre |
| `observacion_aprobada` | distancia_edicion (0 a 1), regeneraciones | Aceptación de la IA |
| `boletas_publicadas` | grupo_id, boletas, minutos_desde_inicio_cierre | Tiempo de cierre |
| `enlace_tutor_abierto` | primera_vez | Apertura de tutores |
| `suscripcion_activada` | grupos_pro | Conversión |

## Panel interno (RF-091)

Ruta separada, solo para usuarios con rol `admin`, de solo lectura:

- Métricas del piloto de `00-prd.md` sección 4, por semana.
- Lista de docentes con grupos, plan, último acceso y consumo del mes.
- Costo estimado del mes por grupo: tokens de IA × precio y mensajes de WhatsApp × tarifa, contra ingreso.
- Errores recientes de jobs (IA, WhatsApp, correo, pagos).

El panel no muestra nombres de alumnos ni teléfonos.

## Observabilidad

- Logs estructurados con Serilog en JSON; nunca se registran nombres, teléfonos, correos, textos de notas u observaciones, ni tokens.
- Trazas y métricas con OpenTelemetry; se exportan a un servicio gestionado (por definir) o a Grafana en el mismo servidor.
- Monitoreo externo de disponibilidad cada minuto sobre `/health` y alerta por correo y Telegram si falla 3 veces seguidas.
- Alertas: tasa de errores 5xx mayor a 2% en 5 minutos, job fallido tras sus reintentos, respaldo fallido, costo de un grupo mayor a 8 MXN en el mes.

## Respaldos y retención

- `pg_dump` diario cifrado (age o GPG) subido a almacenamiento de objetos fuera del servidor (Oracle Object Storage u otro proveedor), retención 30 días.
- Prueba de restauración el primer lunes de cada mes en staging, registrada en `docs/operacion/restauraciones.md`.
- Job diario de retención (RNF-009): elimina grupos archivados con más de 2 ciclos y cuentas con eliminación solicitada hace más de 30 días.

## Criterios de aceptación

- Dado el panel interno, entonces muestra las 6 métricas del piloto calculadas desde los eventos.
- Dado cualquier log de la API, entonces no contiene nombres ni teléfonos (prueba automática con datos marcados).
- Dado un respaldo del día anterior, cuando se restaura en staging, entonces la app arranca y los conteos coinciden.
