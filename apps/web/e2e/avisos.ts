import { Page, expect } from '@playwright/test';
import { NOMBRES } from './cierre';
import { crearCicloYGrupo } from './grupos';

/** Un WhatsApp distinto en cada corrida: la base local se comparte entre pruebas (RN-015 vincula por número). */
const telefonoNuevo = () =>
  `66${Math.floor(Math.random() * 1e8)
    .toString()
    .padStart(8, '0')}`;

/** Grupo "4° B" con los alumnos de NOMBRES; devuelve la ruta del grupo. */
export async function grupoConAlumnos(page: Page): Promise<string> {
  await crearCicloYGrupo(page);
  const grupo = new URL(page.url()).pathname.replace(/\/alumnos$/, '');
  await page.goto(grupo + '/importar?origen=texto');
  await page.getByLabel('Lista de alumnos').fill(NOMBRES.join('\n'));
  await page.getByRole('button', { name: 'Ver cómo queda' }).click();
  await page.getByRole('button', { name: `Importar ${NOMBRES.length} alumnos` }).click();
  await expect(page.getByText(`Se importaron ${NOMBRES.length} alumnos.`)).toBeVisible();
  return grupo;
}

/** Agrega un tutor con WhatsApp (y correo, si se da) al alumno desde su ficha. */
export async function agregarTutor(
  page: Page,
  grupo: string,
  alumno: RegExp,
  nombre: string,
  correo?: string,
): Promise<void> {
  await page.goto(grupo + '/alumnos');
  await page.getByRole('link', { name: alumno }).click();
  await page.getByRole('link', { name: 'Datos' }).click();
  await page.getByLabel('Nombre del tutor').fill(nombre);
  await page.getByLabel('WhatsApp').fill(telefonoNuevo());
  if (correo) {
    await page.getByLabel('Correo (opcional)').fill(correo);
  }
  await page.getByRole('button', { name: 'Agregar tutor' }).click();
  await expect(page.getByText(`${nombre} quedó como tutor.`)).toBeVisible();
}
