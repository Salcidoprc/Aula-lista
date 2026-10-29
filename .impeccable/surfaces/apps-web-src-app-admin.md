---
version: 1
slug: "apps-web-src-app-admin"
primary_target: "apps/web/src/app/admin/admin.ts"
related_targets: ["apps/web/src/app/admin"]
---

# Superficie: panel de administración (`/admin`, SPEC-09)

Modo: Operate. Brief de `/impeccable shape` (18/10/2026). Supuestos marcados con (S): el dueño del producto no respondió
ronda de preguntas; la spec es precisa y se sigue tal cual.

## Trabajo y audiencia

El dueño del producto (y quien él nombre) entra una o dos veces por semana desde su computadora, a veces desde el
celular, para saber si el piloto va bien, si algún grupo cuesta más de lo que paga y si algo se rompe. No es un docente
capturando: lee, compara y decide. Solo una tarea escribe: el catálogo de tipos de rubro.

## Resultado y prueba

- Métricas: en un vistazo, cuáles de las 6 metas del piloto se cumplen esta semana y cómo vienen las anteriores.
- Costos: qué grupos rebasan el umbral (8 MXN al mes, 2 MXN de IA) y el total de costo contra ingreso del mes.
- Tipos de rubro: crear "Laboratorio" con `actividades` y verlo disponible al instante; desactivar sin borrar.
- Prueba propia del producto: el panel se lee como una **boleta del piloto**. Cada métrica es una fila, cada semana una
  columna, y la celda se marca aprobada o no contra su meta, igual que la boleta que el docente entrega.

## Dirección elegida

Dentro del mundo "Libros de texto" (DESIGN.md, sin cambiarlo). Tesis: la libreta de control de la escuela. Papel,
tinta pizarra y cifras tabulares; color solo para el estado (éxito, aviso, peligro) y las tapas de grado donde aparece
un grupo. Momento focal: la boleta de métricas con la semana actual destacada y su meta al lado.

Secuencia: Métricas (inicio del panel) → Costos → Docentes → Errores → Tipos de rubro → Auditoría.

## Alcance y límites

- Pantallas de producción para las 6 secciones, con estados de carga, vacío y error. Sin gráficos de librería, sin
  exportar (S), sin filtros avanzados: la lista de docentes busca por correo (S).
- RN-090: nunca alumnos, tutores, notas ni observaciones; docentes con correo y nombre.
- No hay enlace desde la app del docente: se entra escribiendo `/admin` (S). Quien no es admin ve la página de
  "no existe" (la API ya responde 404).
- Anti-metas: panel SaaS genérico con tarjetas KPI y gráficas de dona; colores de grado como decoración.

## Estados y rangos

Piloto: 10 a 50 docentes, 0 a 200 grupos, 12 semanas de métricas, 0 a 100 errores, 6 a 20 tipos de rubro, auditoría
paginada de 50 en 50. Métricas sin datos (división entre cero) se muestran como "sin datos", nunca 0 %.

## Interacción y disposición

- Escritorio: navegación de secciones en columna a la izquierda, contenido a la derecha con tablas densas y cifras
  alineadas a la derecha. Celular: las secciones en una fila desplazable arriba; las tablas pasan a filas apiladas
  (Costos, Docentes, Errores, Auditoría) y la boleta de métricas se desplaza horizontalmente con la columna de la
  métrica fija.
- Tipos de rubro: lista con orden, nombre, comportamiento, rubros que lo usan y estado; editar en una hoja (el patrón
  `hoja` de la app) con el formulario completo. Comportamiento: selector con la lista de `/admin/comportamientos`;
  bloqueado si hay rubros que lo usan. Desactivar es un interruptor con su consecuencia escrita.
- Auditoría: cada registro muestra quién, cuándo, la acción y los campos con antes → después.
- Movimiento: solo el de la hoja ya existente y el de los mensajes; las tablas no se animan.

## Restricciones

Angular con signals y Transloco (es-MX), cliente generado de la API, tokens de `tokens.css`, componentes compartidos
(`hoja`, `tapa`, `ocupado`, `iconos`). WCAG 2.1 AA, tablas con encabezados reales, objetivos táctiles de 44 px.

## Resumen con gráficas (`/impeccable shape`, 29/10/2026)

El dueño del producto pidió que el Resumen (RF-094) sea "gráficas y así", rediseñado con Impeccable. Supuestos (S): no
hubo ronda de preguntas; se toma la lectura más probable y se dice en el PR.

- **Trabajo:** el dueño abre el panel una o dos veces por semana y quiere ver de un golpe si el uso y el dinero suben o
  bajan. La forma de la curva importa más que la cifra exacta; la cifra exacta sigue a un toque (tooltip) y en la tabla.
- **Dirección:** el mismo mundo "Libros de texto", en su versión libreta de control. Se cambia la regla anterior "sin
  gráficas ni tarjetas de KPI" solo para el Resumen: arriba una franja de cuatro cifras sobre papel (sin tarjetas, con
  divisorias), y debajo gráficas de una sola tinta, `accion`, sobre la cuadrícula de la libreta (líneas de 1 px en
  `divisoria`). Nada de anillos, donas, degradados ni colores de grado.
- **Forma por dato:** activos por semana = línea con lavado al 10 %; registros, docentes que pagan y valor entregado =
  columnas; activación = barras horizontales; retención = mapa de calor por cohorte (una tinta, cinco pasos); MRR contra
  costo = dos barras en la misma escala; frecuencia y avisos leídos = medidor.
- **Semana en curso:** honesta, no destacada: la columna va en un tinte más claro y la línea en tramo punteado, con
  "en curso" al pasar el cursor. Cero es cero; sin datos es una raya.
- **Interacción:** cada gráfica es enfocable; el cursor o las flechas eligen la semana y un tooltip dice la cifra.
  Nada depende del tooltip: la tabla "Ver los datos por semana" lo tiene todo. Sin animación (tampoco al cargar).
- **Disposición:** celular apilado a lo ancho; escritorio en dos columnas, con Uso y Valor entregado a lo ancho.
- **Límites:** sin librerías de gráficas (SVG propio), RN-090 igual, presupuesto del bundle intacto.
