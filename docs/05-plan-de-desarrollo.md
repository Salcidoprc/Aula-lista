# Plan de desarrollo del MVP

El MVP se construye en 7 hitos. Cada hito termina con algo que se puede usar en staging. El orden sigue el flujo del docente: no se construye el cierre antes de poder capturar.

| Hito | Contenido | Specs | Listo cuando |
| --- | --- | --- | --- |
| H0. Cimientos | Repo, solución .NET y proyecto Angular, skills de diseño y animación instalados (regla 07), `/impeccable init` (`PRODUCT.md`), sistema de diseño y tokens de color, tipografía y movimiento (`DESIGN.md`, `tokens.css`), docker-compose local, CI, Coolify con staging, PostgreSQL con roles y RLS base, OpenAPI y cliente generado, logs y health check | Arquitectura, reglas | Un "hola mundo" autenticado pasa CI y se despliega solo a staging |
| H1. Cuenta | Registro, verificación, Google, sesión con renovación, perfil, aceptación legal | SPEC-01 | Criterios de SPEC-01 en verde |
| H2. Grupos y alumnos | Ciclo escolar del docente con sus unidades (convierte el catálogo global de H0 en ciclo por docente), grupos con un periodo por unidad, alumnos, tutores, importación por Excel y texto (foto queda para H6) | SPEC-02 | Un docente arma un grupo de 35 alumnos con tutores en menos de 10 minutos |
| H3. Captura y asistencia | Rubros de actividades y de asistencia, actividades, captura en tres vistas, pase de lista, notas, cálculo compartido, sincronización sin conexión | SPEC-03, SPEC-10 | Prueba end-to-end de captura y pase de lista sin conexión en verde |
| H4. Cierre y boletas | Cierre, observaciones con IA, evaluación de calidad, boletas PDF, publicación, exportación a Excel | SPEC-04, SPEC-05 | Un grupo de 30 alumnos se cierra en menos de 1 hora en prueba con un docente real |
| H5. Tutores y avisos | Portal de tutores, enlaces, push, correo, WhatsApp con plantillas aprobadas, avisos | SPEC-06, SPEC-07 | Un tutor con dos hijos de docentes distintos ve ambos y recibe la boleta |
| H6. Planes y operación | Prueba Pro, suscripción por grupo con tarjeta y prepago OXXO en Stripe, límites de WhatsApp, eventos, panel interno, respaldos, importación por foto | SPEC-08, SPEC-09, RF-015 | Un docente paga 2 grupos y el panel muestra su costo y su ingreso |

## Antes de abrir el piloto

- [ ] Revisión de términos y aviso de privacidad por un abogado externo (PA-11).
- [ ] Negocio y número verificados en Meta; plantillas aprobadas.
- [ ] Cuenta de Stripe en México activa con precios configurados y OXXO aprobado para el giro de la cuenta (ADR-0009).
- [ ] Dominio de Aula Lista con SPF, DKIM y DMARC en Brevo (ADR-0008).
- [ ] Prueba de restauración de respaldo registrada.
- [ ] 10 entrevistas a docentes registradas en el Sheet (PA-02).

## Cómo empezar una tarea

1. Leer la spec del módulo y sus criterios de aceptación.
2. Revisar reglas aplicables en `docs/reglas/`.
3. Crear rama con el ID de la spec.
4. Escribir primero las pruebas de los criterios de aceptación.
5. Si hay interfaz: `/impeccable shape` antes de construir y el flujo de revisión de `reglas/07-diseno-y-movimiento.md` antes del PR.
6. Implementar, actualizar documentación si algo cambió y abrir PR citando IDs.
