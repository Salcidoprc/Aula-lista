import { APIRequestContext, Page, expect } from '@playwright/test';
import { correoNuevo, enlaceDelCorreo, registrarYCompletarPerfil } from './cuenta';

/** Docente con el correo confirmado (RN-001), en "Mis grupos". */
export async function docenteVerificado(page: Page, request: APIRequestContext): Promise<string> {
  const correo = await registrarYCompletarPerfil(page, correoNuevo());
  await page.goto(await enlaceDelCorreo(request, correo, /Confirma/));
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Correo confirmado');
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1, name: 'Mis grupos' })).toBeVisible();
  return correo;
}

/** Crea el ciclo propuesto (trimestres de agosto a julio) y el grupo "4° B"; termina en sus alumnos. */
export async function crearCicloYGrupo(page: Page, nombre = 'B'): Promise<void> {
  await page.goto('/grupos/nuevo');
  await expect(page).toHaveURL(/\/ciclos\/nuevo/);
  await page.getByLabel('Empieza el').fill('2026-08-25');
  await page.getByLabel('Termina el').fill('2027-07-16');
  await page.getByRole('button', { name: 'Guardar ciclo' }).click();
  await expect(page).toHaveURL(/\/grupos\/nuevo/);
  await page.getByText('4°', { exact: true }).click();
  await page.getByLabel('Grupo', { exact: true }).fill(nombre);
  await page.getByRole('button', { name: 'Crear grupo' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Alumnos' })).toBeVisible();
}
