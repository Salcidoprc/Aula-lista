import AxeBuilder from '@axe-core/playwright';
import { APIRequestContext, Page, expect } from '@playwright/test';

export const CONTRASENA = 'cuaderno-de-forma-italiana-7';

export function correoNuevo(): string {
  return `docente-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@prueba.mx`;
}

/** Último enlace con token que recibió una dirección en el buzón de desarrollo de la API. */
export async function enlaceDelCorreo(request: APIRequestContext, para: string, asunto: RegExp): Promise<string> {
  let enlace: string | undefined;
  await expect
    .poll(async () => {
      const respuesta = await request.get(`/api/v1/desarrollo/correos?para=${encodeURIComponent(para)}`);
      const correos = (await respuesta.json()) as { asunto: string; texto: string }[];
      const correo = correos.find((c) => asunto.test(c.asunto));
      enlace = correo?.texto.match(/https?:\/\/\S+/)?.[0];
      return enlace;
    })
    .toBeTruthy();
  // El enlace apunta a la URL web configurada; la prueba lo abre en el origen bajo prueba.
  const url = new URL(enlace!);
  return `${url.pathname}${url.search}`;
}

export async function sinFallasDeAccesibilidad(page: Page): Promise<void> {
  const resultado = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
  expect(resultado.violations).toEqual([]);
}

/** Crea la cuenta desde la pantalla de registro y completa la bienvenida. */
export async function registrarYCompletarPerfil(page: Page, correo = correoNuevo()): Promise<string> {
  await page.goto('/cuenta/registro');
  await page.getByLabel('Correo').fill(correo);
  await page.getByLabel('Contraseña nueva').fill(CONTRASENA);
  await page.getByRole('checkbox').check();
  await page.getByRole('button', { name: 'Crear mi cuenta' }).click();
  await expect(page).toHaveURL(/\/bienvenida$/);
  await completarPerfil(page);
  return correo;
}

export async function completarPerfil(page: Page): Promise<void> {
  await page.getByLabel('Tu nombre').fill('Rosa María Félix Gaxiola');
  await page.getByLabel('Primaria').check();
  await page.getByLabel('Nombre de la escuela').fill('Escuela Primaria Benito Juárez');
  await page.getByLabel('Estado', { exact: true }).selectOption('sinaloa');
  await page.getByRole('button', { name: 'Empezar' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Mis grupos' })).toBeVisible();
}
