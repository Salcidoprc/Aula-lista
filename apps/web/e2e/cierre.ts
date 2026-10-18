import { Page, expect } from '@playwright/test';
import { crearCicloYGrupo } from './grupos';

export const NOMBRES = [
  'Cruz Ortiz Leonardo',
  'Flores Rojas Regina',
  'García López Ana Sofía',
  'Morales Reyes Renata',
];

export const teclado = (page: Page) => page.getByRole('group', { name: 'Teclado rápido' });

/**
 * Grupo "4° B" con 4 alumnos, un rubro de 100 % y una actividad calificada: listo para iniciar el cierre
 * (RN-040). Termina en Calificaciones y devuelve la ruta del grupo.
 */
export async function grupoCalificado(page: Page): Promise<string> {
  await crearCicloYGrupo(page);
  const grupo = new URL(page.url()).pathname.replace(/\/alumnos$/, '');
  await page.goto(grupo + '/importar?origen=texto');
  await page.getByLabel('Lista de alumnos').fill(NOMBRES.join('\n'));
  await page.getByRole('button', { name: 'Ver cómo queda' }).click();
  await page.getByRole('button', { name: `Importar ${NOMBRES.length} alumnos` }).click();
  await expect(page.getByText(`Se importaron ${NOMBRES.length} alumnos.`)).toBeVisible();
  await page.goto(grupo);
  await page.getByRole('link', { name: 'Crear rubros' }).click();
  await page.getByRole('button', { name: 'Agregar rubro' }).first().click();
  const bloque = page.locator('section.bloque').first();
  await bloque.getByLabel('Tipo').selectOption({ label: 'Actividades' });
  await bloque.getByLabel('Peso', { exact: true }).fill('100');
  await bloque.getByRole('radio', { name: 'Con número' }).check();
  await page.getByRole('button', { name: 'Guardar rubros' }).click();
  await expect(page).toHaveURL(new RegExp(`${grupo}\\?unidad=`));
  await page.getByRole('button', { name: 'Nueva actividad' }).first().click();
  await page.getByLabel('Rubro').selectOption({ label: 'Actividades' });
  await page.getByRole('button', { name: 'Crear y capturar' }).click();
  await expect(teclado(page)).toBeVisible();
  for (const n of ['9', '8', '10', '7']) {
    await teclado(page).getByRole('button', { name: n, exact: true }).click();
  }
  await expect(
    page.locator('app-indicador-sincronizacion').getByText('Guardado', { exact: true }),
  ).toBeVisible();
  await page.goto(grupo);
  await expect(page.getByRole('heading', { level: 1, name: 'Calificaciones' })).toBeVisible();
  return grupo;
}

/** Inicia el cierre desde Calificaciones y genera todas las observaciones; termina en la lista del cierre. */
export async function cerrarYGenerar(page: Page): Promise<void> {
  await page.getByRole('link', { name: 'Cerrar la unidad' }).locator('visible=true').click();
  await expect(page.getByRole('heading', { name: 'Antes de iniciar' })).toBeVisible();
  await page.getByRole('button', { name: 'Iniciar cierre' }).click();
  await page
    .getByRole('button', { name: `Generar observaciones (${NOMBRES.length})` })
    .locator('visible=true')
    .click();
  await expect(page.locator('.fila[data-estado="borrador"]')).toHaveCount(NOMBRES.length, {
    timeout: 60_000,
  });
}

/** Aprueba todas las observaciones desde la primera con "Aprobar y siguiente" y vuelve a la lista del cierre. */
export async function aprobarTodas(page: Page): Promise<void> {
  await page.locator('.fila').first().click();
  for (let restantes = NOMBRES.length; restantes > 0; restantes--) {
    await page
      .getByRole('button', { name: /^Aprobar( y siguiente)?$/ })
      .locator('visible=true')
      .click();
  }
  await expect(page.getByText('Listo: todas las observaciones están aprobadas.')).toBeVisible();
  await page.getByRole('link', { name: 'Lista del cierre' }).click();
  await expect(
    page.getByText(`${NOMBRES.length} de ${NOMBRES.length} aprobadas`).locator('visible=true'),
  ).toBeVisible();
}
