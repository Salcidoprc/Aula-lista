# Product

<!-- impeccable:product-schema 1 -->

> Escrito en H0 a partir de `docs/` (PRD, requerimientos, specs y ADR confirmados por el dueño del producto el 11/10/2026). Si algo cambia, se cambia primero en `docs/` y después aquí.

## Platform

web

## Users

- **Docente de primaria o secundaria de escuela pública en México** (cliente inicial, lo adopta y lo paga por su cuenta). Usa la app sobre todo en el celular, a veces en el salón entre clases y a veces de noche en casa. Su trabajo: capturar calificaciones y notas durante el periodo, cerrar el periodo con observaciones por alumno, publicar boletas y avisar a los tutores.
- **Tutor (madre, padre o responsable)**: abre un enlace personal desde WhatsApp, push o correo, sin instalar nada ni crear cuenta. Quiere ver avance, boletas y avisos de todos sus hijos, aunque sean de docentes distintos. Suele tener un Android de gama baja o media y datos limitados.
- **Administrador interno**: opera y mide el piloto desde un panel interno.
- Fases posteriores, fuera del MVP: director (F2) y escuela privada (F3). El alumno no tiene cuenta.

## Product Purpose

Ahorrarle al docente las noches de cierre de periodo. Captura rápida en el celular durante el periodo, borrador de observación por alumno redactado por IA que el docente aprueba, boleta PDF generada y publicada, y tutores informados por un enlace. Éxito del piloto: 30 docentes activos, 60% cierra un periodo con la app, un grupo de 30 alumnos se cierra en menos de 1 hora, 70% de observaciones aceptadas sin cambios mayores, 50% de tutores abre el enlace, 20% de docentes paga después de la prueba.

## Positioning

Se le vende al docente, no a la dirección: lo adopta solo, sin esperar a la escuela. El tutor no instala nada: un enlace basta y ve a todos sus hijos con una sola identidad (su teléfono). La IA solo redacta borradores a partir de las notas del propio docente; nada llega a un tutor sin su aprobación.

## Operating Context

- Captura en el salón con una mano, a menudo sin conexión o con señal intermitente; debe funcionar sin conexión al menos 7 días.
- La captura se repite cientos de veces por periodo: velocidad y cero fricción antes que adorno.
- El cierre de periodo es el momento de mayor carga: revisar y aprobar decenas de observaciones seguidas.
- Las boletas son un reporte del docente, no el formato oficial de la SEP; se exportan también a Excel.
- Avisos frecuentes por push y correo; WhatsApp del sistema solo para boletas y avisos urgentes (costo por mensaje).

## Capabilities and Constraints

- MVP: cuenta del docente (correo o Google), grupos y alumnos con importación, rubros libres y captura de calificaciones con hasta 2 decimales, pase de lista sin conexión (la asistencia puede valer un porcentaje de la unidad), notas por alumno, cierre con observaciones de IA, boletas PDF, portal de tutores, avisos, planes de pago (Gratis, Prueba Pro, Pro por grupo).
- Primaria y secundaria desde el MVP. Varios grupos por docente; máximo 50 alumnos por grupo.
- Fuera del MVP: libros digitales, chat, exámenes en línea, cuentas de alumno, planeación con IA, asistencia visible para tutores, panel del director, cobros de colegiaturas, app nativa, boleta oficial SEP, facturación.
- Datos de menores: mínimos, seudonimizados ante la IA, nunca en logs.
- Español de México, fechas dd/mm/aaaa, zona America/Mexico_City por defecto.
- Rendimiento: carga en menos de 4 s en Android de gama baja con 4G; bundle inicial máximo 250 KB comprimido.
- Stack ya decidido: .NET 10, Angular PWA, PostgreSQL (ADR-0001).

## Brand Commitments

- Nombre definitivo: **Aula Lista**.
- Tono: cálido y claro, de herramienta escolar confiable. No es una app de tecnología ni de juegos.
- Debe sentirse como una app nativa y cuidada, no como una plantilla.
- Estilo visual elegido por el dueño del producto el 11/10/2026: **"Libros de texto"** (cada grupo con el color de su grado, como las portadas de los libros de texto gratuitos). Familiar para el docente, pero sin verse infantil ni parecer un documento oficial de la SEP.
- Movimiento: solo transiciones 2D, rápidas y con propósito; nada de 3D; nada animado en acciones repetidas como capturar calificaciones (ADR-0010, `docs/reglas/07-diseno-y-movimiento.md`).
- Restricciones visuales ya fijadas en la regla 07: fuente principal distinta de Inter, Roboto, Arial o la del sistema, con cifras tabulares y español completo; colores en OKLCH; neutros entintados, nunca negro ni blanco puros; sin degradados morado a azul ni neón sobre fondo oscuro; no todo en tarjetas y nunca tarjetas dentro de tarjetas; texto alineado a la izquierda.

## Evidence on Hand

- No hay logotipo, fotografías, testimonios ni clientes todavía. No se inventan.
- Las 10 entrevistas a docentes (PA-02) están en curso; hasta tenerlas, el tiempo de cierre actual es una hipótesis.
- Precios del piloto (79 MXN el primer grupo, 29 MXN cada adicional) son hipótesis documentadas en el PRD.

## Product Principles

1. Lo que se repite cientos de veces va primero: la captura es instantánea, sin animación y usable con una mano y sin conexión.
2. El docente decide: la IA propone, el docente aprueba; nada sale hacia un tutor sin su visto bueno.
3. Cero instalación para el tutor: un enlace, todos sus hijos, sin contraseñas.
4. Cuidar a los menores: el mínimo de datos y ninguno hacia la IA o los logs.
5. Barato de operar y de pagar: el costo por grupo se mide y se mantiene bajo.

## Accessibility & Inclusion

WCAG 2.1 AA, objetivos táctiles de al menos 44 px, respeto a `prefers-reduced-motion`, campos de al menos 16 px en celular, funcionamiento en Android de gama baja con datos limitados.
