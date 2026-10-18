import { Page, expect, test } from '@playwright/test';
import { sinFallasDeAccesibilidad } from './cuenta';
import { crearCicloYGrupo, docenteVerificado } from './grupos';

const APELLIDOS = [
  'Álvarez',
  'Cruz',
  'Díaz',
  'Flores',
  'García',
  'Hernández',
  'Jiménez',
  'López',
  'Martínez',
  'Morales',
];
const NOMBRES = ['Ana', 'Mateo', 'Regina'];

/** Grupo "4° B" con `cantidad` alumnos pegados como lista; devuelve la ruta del grupo. */
async function grupoConAlumnos(page: Page, cantidad: number): Promise<string> {
  await crearCicloYGrupo(page);
  const grupo = new URL(page.url()).pathname.replace(/\/alumnos$/, '');
  const lista = Array.from(
    { length: cantidad },
    (_, i) => `${APELLIDOS[i % 10]} ${APELLIDOS[(i + 3) % 10]} ${NOMBRES[Math.floor(i / 10)]}`,
  );
  await page.goto(grupo + '/importar?origen=texto');
  await page.getByLabel('Lista de alumnos').fill(lista.join('\n'));
  await page.getByRole('button', { name: 'Ver cómo queda' }).click();
  await page.getByRole('button', { name: `Importar ${cantidad} alumnos` }).click();
  await expect(page.getByText(`Se importaron ${cantidad} alumnos.`)).toBeVisible();
  return grupo;
}

const filas = (page: Page) => page.locator('button.fila');
const conteo = (page: Page, estado: string) =>
  page.locator('.conteo:visible').locator(`[data-estado="${estado}"] dd`);

test.describe('SPEC-10: asistencia', () => {
  test('RF-100: con 30 alumnos, "Todos presentes" y dos toques dejan 28 presentes y 2 faltas; el historial da el porcentaje (RN-103)', async ({
    page,
    request,
  }) => {
    await docenteVerificado(page, request);
    const grupo = await grupoConAlumnos(page, 30);

    await page.goto(grupo);
    await page.getByRole('link', { name: 'Asistencia' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Pasar lista' })).toBeVisible();
    await expect(page.getByText('Todavía no pasas lista hoy.')).toBeVisible();
    await expect(filas(page)).toHaveCount(30);
    await sinFallasDeAccesibilidad(page);

    await page.getByRole('button', { name: 'Todos presentes' }).locator('visible=true').click();
    await expect(filas(page).first()).toContainText('Presente');
    await filas(page).nth(2).click();
    await filas(page).nth(6).click();
    await expect(filas(page).nth(2)).toContainText('Falta');
    await expect(conteo(page, 'presente')).toHaveText('28');
    await expect(conteo(page, 'falta')).toHaveText('2');

    // RN-101: un toque más recorre el ciclo; con teclado, P lo regresa a presente.
    await filas(page).nth(6).click();
    await expect(filas(page).nth(6)).toContainText('Retardo');
    await filas(page).nth(6).press('p');
    await expect(filas(page).nth(6)).toContainText('Presente');
    await filas(page).nth(6).press('f');
    await expect(page.locator('app-indicador-sincronizacion .texto')).toHaveText('Guardado');

    // Lo guardado sale del servidor al volver a abrir.
    await page.reload();
    await expect(conteo(page, 'falta')).toHaveText('2');
    await expect(filas(page).nth(6)).toContainText('Falta');
    await sinFallasDeAccesibilidad(page);

    await page
      .getByRole('link', { name: 'Historial de la unidad' })
      .locator('visible=true')
      .click();
    await expect(
      page.getByRole('heading', { level: 1, name: 'Historial de asistencia' }),
    ).toBeVisible();
    await expect(page.getByText('Días con lista: 1')).toBeVisible();
    const porcentajes = page.locator(':is(td.total, .porcentaje):visible');
    await expect(porcentajes).toHaveCount(30);
    await expect(porcentajes.nth(0)).toHaveText('100 %');
    await expect(porcentajes.nth(2)).toHaveText('0 %');
    await sinFallasDeAccesibilidad(page);
  });

  test('RF-103: sin conexión la lista se guarda en el dispositivo y llega al volver la red, sin duplicados', async ({
    page,
    request,
    context,
  }) => {
    await docenteVerificado(page, request);
    const grupo = await grupoConAlumnos(page, 12);
    await page.goto(grupo + '/asistencia');
    await expect(filas(page)).toHaveCount(12);

    await context.setOffline(true);
    // Sin toma, tocar a un alumno crea la lista con todos presentes y ese en falta (RN-101).
    await filas(page).nth(4).click();
    await expect(filas(page).nth(4)).toContainText('Falta');
    await expect(filas(page).nth(0)).toContainText('Presente');
    await filas(page).nth(7).click();
    await filas(page).nth(7).click();
    await expect(filas(page).nth(7)).toContainText('Retardo');
    await expect(page.locator('app-indicador-sincronizacion .texto')).toHaveText(
      'Sin conexión: guardado aquí',
    );

    await context.setOffline(false);
    await expect(page.locator('app-indicador-sincronizacion .texto')).toHaveText('Guardado');
    await page.reload();
    await expect(conteo(page, 'presente')).toHaveText('10');
    await expect(conteo(page, 'falta')).toHaveText('1');
    await expect(conteo(page, 'retardo')).toHaveText('1');
  });
});
