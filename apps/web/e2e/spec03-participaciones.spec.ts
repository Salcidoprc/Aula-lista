import { Page, expect, test } from '@playwright/test';
import { sinFallasDeAccesibilidad } from './cuenta';
import { crearCicloYGrupo, docenteVerificado } from './grupos';

const ALUMNOS = ['López Ruiz Ana', 'Muñoz Pérez Bruno', 'Ortega Díaz Carla', 'Vega Cruz Diego'];

async function grupoConAlumnos(page: Page): Promise<string> {
  await crearCicloYGrupo(page);
  const grupo = new URL(page.url()).pathname.replace(/\/alumnos$/, '');
  await page.goto(grupo + '/importar?origen=texto');
  await page.getByLabel('Lista de alumnos').fill(ALUMNOS.join('\n'));
  await page.getByRole('button', { name: 'Ver cómo queda' }).click();
  await page.getByRole('button', { name: `Importar ${ALUMNOS.length} alumnos` }).click();
  await expect(page.getByText(`Se importaron ${ALUMNOS.length} alumnos.`)).toBeVisible();
  return grupo;
}

const filas = (page: Page) => page.locator('button.fila');
const sincronizacion = (page: Page) => page.locator('app-indicador-sincronizacion .texto');

test.describe('SPEC-03: participaciones extra', () => {
  test('RF-110: se busca al alumno y un toque le suma una; RN-121: el valor del grupo se cambia de 0.01 a 1.00', async ({
    page,
    request,
  }) => {
    await docenteVerificado(page, request);
    const grupo = await grupoConAlumnos(page);

    await page.goto(grupo);
    await page.getByRole('link', { name: 'Participaciones' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Participaciones' })).toBeVisible();
    await expect(filas(page)).toHaveCount(4);
    await expect(page.getByText('Cada una vale 0.10').locator('visible=true')).toBeVisible();
    await sinFallasDeAccesibilidad(page);

    // Buscar sin acentos y con Enter suma al único que coincide.
    await page.getByLabel('Buscar alumno').fill('munoz');
    await expect(filas(page)).toHaveCount(1);
    await page.getByLabel('Buscar alumno').press('Enter');
    await expect(page.getByLabel('Buscar alumno')).toHaveValue('');
    await expect(filas(page)).toHaveCount(4);

    // Un toque por participación.
    await page.getByRole('button', { name: /^Sumar una participación a .*Carla$/ }).click();
    await page.getByRole('button', { name: /^Sumar una participación a .*Carla$/ }).click();
    await expect(page.locator('li', { hasText: 'Carla' })).toContainText('2 hoy');
    await expect(page.locator('li', { hasText: 'Carla' })).toContainText('2 en la unidad · +0.20');
    await expect(sincronizacion(page)).toHaveText('Guardado');

    // Quitar una (RF-111) y que lo guardado salga del servidor.
    await page.getByRole('button', { name: /^Quitar una participación a .*Carla$/ }).click();
    await expect(page.locator('li', { hasText: 'Carla' })).toContainText('1 hoy');
    await expect(sincronizacion(page)).toHaveText('Guardado');
    await page.reload();
    await expect(page.locator('li', { hasText: 'Carla' })).toContainText('1 hoy');
    await expect(page.locator('li', { hasText: 'Bruno' })).toContainText('1 hoy');

    // RN-121 y RN-122: cambiar el valor recalcula los puntos.
    await page.getByRole('button', { name: 'Cambiar' }).locator('visible=true').click();
    const dialogo = page.getByRole('dialog', { name: '¿Cuánto vale cada participación?' });
    await expect(dialogo).toBeVisible();
    // Se revisa ya abierto: a media entrada los colores todavía se mezclan.
    await expect(dialogo).toHaveCSS('opacity', '1');
    await sinFallasDeAccesibilidad(page);
    await page.getByRole('button', { name: '0.25', exact: true }).click();
    await page.getByRole('button', { name: 'Guardar' }).click();
    await expect(page.getByText('Ahora cada participación vale 0.25.')).toBeVisible();
    await expect(page.locator('li', { hasText: 'Carla' })).toContainText('1 en la unidad · +0.25');
  });

  test('RF-111: sin señal se suman y se quitan, y al volver la red llegan una sola vez', async ({
    page,
    context,
    request,
  }) => {
    await docenteVerificado(page, request);
    const grupo = await grupoConAlumnos(page);
    await page.goto(grupo + '/participaciones');
    await expect(filas(page)).toHaveCount(4);

    await context.setOffline(true);
    const ana = page.getByRole('button', { name: /^Sumar una participación a .*Ana$/ });
    for (let i = 0; i < 3; i++) {
      await ana.click();
    }
    await page.getByRole('button', { name: /^Quitar una participación a .*Ana$/ }).click();
    await expect(page.locator('li', { hasText: 'Ana' })).toContainText('2 hoy');
    await expect(sincronizacion(page)).toHaveText('Sin conexión: guardado aquí');

    await context.setOffline(false);
    await expect(sincronizacion(page)).toHaveText('Guardado');
    await page.reload();
    await expect(page.locator('li', { hasText: 'Ana' })).toContainText('2 hoy');
  });
});
