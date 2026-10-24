---
version: 1
slug: "apps-web-src-app-docente-importar"
primary_target: "apps/web/src/app/docente/importar/importar.ts"
related_targets: ["apps/web/src/app/docente/importar"]
---

# Superficie: importar alumnos, pestaña Foto (SPEC-02, RF-015)

Modo: Operate. Brief de `/impeccable shape` (24/10/2026) para agregar la tercera pestaña a una pantalla que ya existe
(Excel, Pegar lista). Es un refinamiento: conserva el mundo "Libros de texto", la vista previa común y su copia.
Supuestos marcados con (S).

## Trabajo y audiencia

La docente arranca el ciclo con la lista impresa de la dirección o escrita a mano en su libreta. Está en el salón o en
casa, con el celular en una mano y la hoja en la mesa. Quiere sus 35 alumnos en la app sin teclear. Lo hace una vez por
grupo, así que se permite un paso de espera (la lectura tarda unos segundos), pero no un paso de configuración.

## Resultado y prueba

- De la hoja a la vista previa en dos toques: "Tomar o elegir foto" y la cámara del sistema.
- La vista previa es la misma de "Pegar lista": orden apellidos/nombres, corregir, quitar, confirmar.
- La docente sabe que la IA pudo equivocarse: una línea en la vista previa le pide revisar cada nombre (RN-042, "la
  IA propone, el docente decide").
- Si la foto no se lee o no hay conexión, el camino sigue: un botón la lleva a "Pegar lista" sin perder nada.

## Dirección

Igual que las otras pestañas: nota corta, una acción principal, aviso de error debajo. La única pieza nueva es la
miniatura de la foto mientras se lee, con el estado "Leyendo los nombres…" a su lado (aria-live). Sin animación nueva:
la lectura no es una acción repetida, pero tampoco necesita movimiento; basta el estado ocupado del botón (S).

Copia:
- Nota: "Toma una foto de la lista impresa o escrita a mano, de frente y con buena luz. Solo leemos los nombres y la
  foto no se guarda."
- Acción: "Tomar o elegir foto". En escritorio el sistema abre el selector de archivos.
- Sin conexión: "Para leer la foto necesitas conexión. Mientras, puedes pegar la lista." + "Pegar la lista".
- Falla: "No pudimos leer nombres en esta foto. Prueba con otra más de cerca o pega la lista." + "Pegar la lista".
- Vista previa: "Revisa cada nombre: la lectura de la foto puede equivocarse, sobre todo con letra a mano."

## Alcance y límites

- JPG o PNG. La web reduce la foto a 1568 px por el lado largo y la vuelve a codificar como JPEG antes de subirla:
  menos datos para el celular, menos tokens y sin metadatos (ubicación) (S). La API rechaza lo que pase de 5 MB.
- Solo nombres de alumnos; no se leen tutores ni teléfonos de la foto (SPEC-02 dice "solo para extraer nombres").
- Pestañas: Excel · Pegar lista · Foto, con flechas, Inicio y Fin como hoy.
- En celular la pestaña abre con `?origen=foto` desde "Importar" igual que las otras.

## Estados

Inicial · leyendo (miniatura + estado) · sin conexión · foto inválida (tipo o tamaño) · ilegible · límite por hora ·
vista previa con aviso de revisión.
