# Preguntas abiertas

Cada pregunta indica qué bloquea y qué se asume mientras no se resuelva. Al resolverla, se anota la respuesta, la fecha y el documento que se actualizó.

## Abiertas

Ninguna por ahora.

## Resueltas

| ID | Pregunta | Respuesta | Fecha | Documentos actualizados |
| --- | --- | --- | --- | --- |
| PA-01 | ¿El docente de escuela pública puede entregar una boleta propia o la SEP exige su formato oficial? | La boleta de la app es un reporte del docente, no oficial (RN-055). El formato oficial se llena con la exportación a Excel (RF-055). | 11/10/2026 | Sin cambios; se confirma lo escrito |
| PA-02 | ¿Cuántas horas dedica hoy un docente al cierre de periodo? | Se mide con 10 entrevistas que hace el dueño del producto, con la guía y el registro en Google Sheets "Aula Lista: entrevistas a docentes". La meta del piloto se ajusta con el resultado. | 11/10/2026 | 00-prd.md |
| PA-03 | ¿Qué nivel se ataca primero: primaria o secundaria? | Ambos desde el MVP. Se pueden crear varios grupos a la vez (RF-020). La prueba Pro cubre 1 grupo elegido por el docente. | 11/10/2026 | 00-prd.md, 01-requerimientos.md, SPEC-02, SPEC-04, SPEC-08, ADR-0006, glosario |
| PA-04 | ¿Nombre definitivo del producto? | Aula Lista. Espacio de nombres `AulaLista` en código. | 11/10/2026 | 00-prd.md |
| PA-05 | ¿Cómo se redondea la calificación final? | Hasta 2 decimales en captura y cálculo. Cada grupo elige los decimales de la calificación final (0, 1 o 2; por defecto 2) y el sentido del redondeo (arriba, abajo o al más cercano; por defecto al más cercano). | 11/10/2026 | SPEC-03 (RN-030, RN-035), RF-035, 02-arquitectura.md, glosario |
| PA-06 | ¿Evaluar por campos formativos de la NEM o por rubros libres? | Rubros libres definidos por el docente. | 11/10/2026 | Sin cambios; se confirma lo escrito |
| PA-07 | ¿Tarifa de WhatsApp de utilidad en México? | 0.0085 USD por mensaje (unos 0.16 MXN), vigente desde el 1/10/2026; ya se cobra también dentro de la ventana de 24 h. 25% de descuento arriba de 1 millón de mensajes al mes. Los primeros 1,000 mensajes de servicio al mes son gratis. | 11/10/2026 | SPEC-07, ADR-0005, 00-prd.md |
| PA-08 | ¿Stripe permite OXXO para suscripción? | No para cobros recurrentes. Tarjeta como suscripción mensual y OXXO como prepago de 3 o 6 meses. Falta confirmar con Stripe que el giro de la cuenta (MCC) permite OXXO. | 11/10/2026 | SPEC-08 (RN-084, RN-089), RF-084, ADR-0009, 02-arquitectura.md |
| PA-09 | ¿Qué proveedor de correo se usa? | Brevo (ya se tiene cuenta). | 11/10/2026 | ADR-0008, 02-arquitectura.md |
| PA-10 | ¿Dónde vive la producción? | El servidor de Oracle es de prueba gratuito: ahí se hacen las pruebas. Después se migra a Azure o similar. Imágenes multiarquitectura. | 11/10/2026 | ADR-0007, ADR-0002, 02-arquitectura.md, 00-prd.md |
| PA-11 | ¿Quién revisa el aviso de privacidad y los términos? | Un abogado externo antes de abrir el piloto. | 11/10/2026 | 05-plan-de-desarrollo.md |
