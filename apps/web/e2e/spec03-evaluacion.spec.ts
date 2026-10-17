import { Page, expect, test } from '@playwright/test';
import { sinFallasDeAccesibilidad } from './cuenta';
import { crearCicloYGrupo, docenteVerificado } from './grupos';

const NOMBRES = [
  'Álvarez Soto José Luis',
  'Cruz Ortiz Leonardo',
  'Díaz Vega Camila',
  'Flores Rojas Regina',
  'García López Ana Sofía',
  'Hernández Ruiz Mateo',
  'Jiménez Ríos Sebastián',
  'López Pérez Diego',
  'Martínez Soto Valeria',
  'Morales Reyes Renata',
  'Ramírez Castro Santiago',
  'Rodríguez Vega Ximena',
  'Sánchez Mora Emiliano',
  'Torres Luna Natalia',
  'Vargas Mendoza Regina',
];

/** Grupo "4° B" con alumnos pegados como lista; termina en Calificaciones. */
async function grupoConAlumnos(page: Page, cantidad: number): Promise<string> {
  await crearCicloYGrupo(page);
  const grupo = new URL(page.url()).pathname.replace(/\/alumnos$/, '');
  await page.goto(grupo + '/importar?origen=texto');
  await page.getByLabel('Lista de alumnos').fill(NOMBRES.slice(0, cantidad).join('\n'));
  await page.getByRole('button', { name: 'Ver cómo queda' }).click();
  await page.getByRole('button', { name: `Importar ${cantidad} alumnos` }).click();
  await expect(page.getByText(`Se importaron ${cantidad} alumnos.`)).toBeVisible();
  await page.goto(grupo);
  await expect(page.getByRole('heading', { level: 1, name: 'Calificaciones' })).toBeVisible();
  return grupo;
}

/** Rubros del periodo desde el editor: [nombre visible del tipo, peso, forma] y, opcional, criterios. */
async function crearRubros(
  page: Page,
  rubros: {
    tipo: string;
    peso: string;
    forma?: 'Con número' | 'Cumplió ✓ / ✗';
    criterios?: [string, string][];
  }[],
): Promise<void> {
  await page.getByRole('link', { name: 'Crear rubros' }).click();
  await expect(page.getByRole('heading', { name: 'Esta unidad no tiene rubros' })).toBeVisible();
  for (const [i, r] of rubros.entries()) {
    await page.getByRole('button', { name: 'Agregar rubro' }).first().click();
    const bloque = page.locator('section.bloque').nth(i);
    await bloque.getByLabel('Tipo').selectOption({ label: r.tipo });
    await bloque.getByLabel('Peso', { exact: true }).fill(r.peso);
    if (r.forma) {
      await bloque.getByRole('radio', { name: r.forma }).check();
    }
    if (r.criterios) {
      await bloque.getByLabel('Calificar por criterios').check();
      for (const [j, [nombre, peso]] of r.criterios.entries()) {
        if (j >= 2) {
          await bloque.getByRole('button', { name: 'Agregar criterio' }).click();
        }
        await bloque.getByRole('textbox', { name: `Criterio ${j + 1}`, exact: true }).fill(nombre);
        await bloque.locator('.criterio').nth(j).getByLabel('Peso', { exact: true }).fill(peso);
      }
    }
  }
}

async function nuevaActividad(page: Page, rubro: string): Promise<void> {
  await page.getByRole('button', { name: 'Nueva actividad' }).first().click();
  await page.getByLabel('Rubro').selectOption({ label: rubro });
  await page.getByRole('button', { name: 'Crear y capturar' }).click();
  await expect(page.getByRole('group', { name: 'Teclado rápido' })).toBeVisible();
}

const teclado = (page: Page) => page.getByRole('group', { name: 'Teclado rápido' });

test.describe('SPEC-03: evaluación y captura', () => {
  test('rubros con criterios: 40/30/30 con 9, 8 y 10 da 9.00; criterios que suman 90 no se guardan', async ({
    page,
    request,
  }) => {
    await docenteVerificado(page, request);
    const grupo = await grupoConAlumnos(page, 3);
    await expect(page.getByText('Define cómo vas a calificar')).toBeVisible();
    await sinFallasDeAccesibilidad(page);

    await crearRubros(page, [
      {
        tipo: 'Exposición',
        peso: '100',
        criterios: [
          ['Información', '40'],
          ['Presentación', '50'],
        ],
      },
    ]);
    await page.getByRole('button', { name: 'Guardar rubros' }).click();
    await expect(
      page.getByText('Los pesos de los criterios deben sumar 100.').first(),
    ).toBeVisible();
    await sinFallasDeAccesibilidad(page);

    const bloque = page.locator('section.bloque').first();
    await bloque.locator('.criterio').nth(1).getByLabel('Peso', { exact: true }).fill('30');
    await bloque.getByRole('button', { name: 'Agregar criterio' }).click();
    await bloque.getByRole('textbox', { name: 'Criterio 3', exact: true }).fill('Dominio del tema');
    await bloque.locator('.criterio').nth(2).getByLabel('Peso', { exact: true }).fill('30');
    await expect(bloque.getByText('Suman 100 de 100')).toBeVisible();
    await page.getByRole('button', { name: 'Guardar rubros' }).click();

    await expect(page).toHaveURL(new RegExp(`${grupo}\\?unidad=`));
    await expect(page.getByText('Pesos: 100 de 100').locator('visible=true').first()).toBeVisible();
    await nuevaActividad(page, 'Exposición');

    const fila = page.locator('li.fila').first();
    await fila.getByLabel('Información').click();
    for (const n of ['9', '8', '10']) {
      await teclado(page).getByRole('button', { name: n, exact: true }).click();
    }
    await expect(fila.locator('.valor-resultado')).toHaveText('9.00');
    await sinFallasDeAccesibilidad(page);
  });

  test('captura rápida: NE vale la mínima, 3 con mínima 5 se rechaza y cumplimiento 4 ✓ y 1 ✗ da 9.00', async ({
    page,
    request,
  }, info) => {
    test.skip(info.project.name !== 'escritorio', 'La tabla es de escritorio');
    await docenteVerificado(page, request);
    const grupo = await grupoConAlumnos(page, 3);
    await crearRubros(page, [
      { tipo: 'Tareas', peso: '50', forma: 'Cumplió ✓ / ✗' },
      { tipo: 'Actividades', peso: '50', forma: 'Con número' },
    ]);
    await page.getByRole('button', { name: 'Guardar rubros' }).click();
    await expect(page).toHaveURL(new RegExp(`${grupo}\\?unidad=`));

    // Cumplimiento: cinco tareas, el primer alumno con 4 ✓ y 1 ✗. Se navega dentro de la app, como el docente.
    for (let i = 0; i < 5; i++) {
      if (i > 0) {
        await page.getByRole('link', { name: 'Calificaciones' }).first().click();
      }
      await nuevaActividad(page, 'Tareas');
      const marca = i < 4 ? 'Cumplió' : 'No cumplió';
      await page
        .locator('li.fila')
        .first()
        .getByRole('button', { name: marca, exact: true })
        .click();
    }

    // Numérica: 3 se rechaza (RN-118); el primero NE y luego 8 y 9.
    await page.getByRole('link', { name: 'Calificaciones' }).first().click();
    await nuevaActividad(page, 'Actividades');
    const celda = page.getByLabel(/^Calificación de 1\./);
    await celda.fill('3');
    await celda.press('Enter');
    await expect(
      page.getByText('La calificación no puede ser menor que la mínima del rubro.'),
    ).toBeVisible();
    await page.locator('li.fila').first().getByRole('button').first().click();
    await teclado(page).getByRole('button', { name: 'No entregó' }).click();
    await teclado(page).getByRole('button', { name: '8', exact: true }).click();
    await teclado(page).getByRole('button', { name: '9', exact: true }).click();
    await expect(page.getByText('3 de 3 capturadas')).toBeVisible();
    await expect(page.getByRole('status').filter({ hasText: 'Guardado' })).toBeVisible();

    await page.goto(grupo);
    await page.getByRole('link', { name: 'Ver tabla' }).click();
    const primera = page.locator('tbody tr').first();
    const promedios = primera.locator('td.promedio');
    await expect(promedios.nth(0)).toHaveText('9.00'); // (10 × 4 + 5) / 5
    await expect(promedios.nth(1)).toHaveText('5.00'); // NE vale la mínima
    await expect(primera.locator('td.total').first()).toHaveText('7.00');
    await sinFallasDeAccesibilidad(page);
  });

  test('sin conexión: 30 calificaciones se guardan en el dispositivo y llegan al reabrir, sin duplicados', async ({
    page,
    request,
    context,
  }) => {
    await docenteVerificado(page, request);
    const grupo = await grupoConAlumnos(page, 15);
    await crearRubros(page, [{ tipo: 'Actividades', peso: '100', forma: 'Con número' }]);
    await page.getByRole('button', { name: 'Guardar rubros' }).click();
    await expect(page).toHaveURL(new RegExp(`${grupo}\\?unidad=`));

    const actividades: string[] = [];
    for (let i = 0; i < 2; i++) {
      await page.goto(grupo);
      await nuevaActividad(page, 'Actividades');
      actividades.push(page.url());
    }

    // Sin red: se captura la actividad abierta y se pasa a la otra desde la lista, que sale de la copia local.
    await context.setOffline(true);
    for (const nombre of ['Actividades 2', 'Actividades 1']) {
      if (nombre === 'Actividades 1') {
        await page.getByRole('link', { name: 'Calificaciones' }).first().click();
        await page.getByRole('link', { name: /Actividades 1/ }).click();
      }
      await expect(page.getByRole('heading', { level: 1, name: nombre })).toBeVisible();
      for (let k = 0; k < 15; k++) {
        await teclado(page)
          .getByRole('button', { name: String(10 - (k % 5)), exact: true })
          .click();
      }
      await expect(page.getByText('15 de 15 capturadas')).toBeVisible();
    }
    await expect(page.getByText('Sin conexión: guardado aquí')).toBeVisible();
    await page.close();

    await context.setOffline(false);
    const otra = await context.newPage();
    await otra.goto(actividades[1]);
    await expect(otra.getByText('Guardado', { exact: true })).toBeVisible();
    await otra.reload();
    await expect(otra.getByText('15 de 15 capturadas')).toBeVisible();
    await otra.goto(actividades[0]);
    await expect(otra.getByText('15 de 15 capturadas')).toBeVisible();
  });
});
