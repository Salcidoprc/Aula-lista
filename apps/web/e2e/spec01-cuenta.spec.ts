import { expect, test } from '@playwright/test';
import {
  CONTRASENA,
  completarPerfil,
  correoNuevo,
  enlaceDelCorreo,
  registrarYCompletarPerfil,
  sinFallasDeAccesibilidad,
} from './cuenta';

test.describe('SPEC-01: cuenta y acceso del docente', () => {
  test('RF-001: se registra, completa su perfil y verifica su correo', async ({ page, request }) => {
    const correo = correoNuevo();
    await page.goto('/cuenta/registro');
    await sinFallasDeAccesibilidad(page);

    await page.getByLabel('Correo').fill(correo);
    await page.getByLabel('Contraseña nueva').fill(CONTRASENA);
    await page.getByRole('checkbox').check();
    await page.getByRole('button', { name: 'Crear mi cuenta' }).click();

    await expect(page).toHaveURL(/\/bienvenida$/);
    await sinFallasDeAccesibilidad(page);
    await completarPerfil(page);
    await expect(page.getByText('Confirma tu correo.')).toBeVisible();
    await sinFallasDeAccesibilidad(page);

    // La sesión sobrevive a recargar: el token de acceso vive en memoria y se renueva con la cookie.
    await page.reload();
    await expect(page.getByRole('heading', { level: 1, name: 'Mis grupos' })).toBeVisible();

    await page.goto(await enlaceDelCorreo(request, correo, /Confirma/));
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Correo confirmado');
    await page.getByRole('link', { name: 'Ir a mis grupos' }).click();
    await expect(page.getByText('Confirma tu correo.')).toBeHidden();
  });

  test('RN-002: rechaza una contraseña corta antes de enviarla', async ({ page }) => {
    await page.goto('/cuenta/registro');
    await page.getByLabel('Correo').fill(correoNuevo());
    await page.getByLabel('Contraseña nueva').fill('corta');
    await page.getByRole('checkbox').check();
    await page.getByRole('button', { name: 'Crear mi cuenta' }).click();

    await expect(page.getByText('La contraseña necesita al menos 10 caracteres.')).toBeVisible();
    await expect(page).toHaveURL(/\/cuenta\/registro/);
  });

  test('una contraseña incorrecta no dice cuál dato falló', async ({ page }) => {
    await page.goto('/cuenta/entrar');
    await page.getByLabel('Correo').fill(correoNuevo());
    await page.getByLabel('Contraseña').fill('no-es-la-contrasena');
    await page.getByRole('button', { name: 'Entrar' }).click();

    await expect(page.getByRole('alert')).toHaveText('El correo o la contraseña no coinciden.');
  });

  test('sin sesión, una pantalla del docente lleva a entrar y luego regresa', async ({ page }) => {
    const correo = await registrarYCompletarPerfil(page);
    await page.context().clearCookies();
    await page.goto('/ajustes');

    await expect(page).toHaveURL(/\/cuenta\/entrar\?volver=%2Fajustes/);
    await page.getByLabel('Correo').fill(correo);
    await page.getByLabel('Contraseña').fill(CONTRASENA);
    await page.getByRole('button', { name: 'Entrar' }).click();

    await expect(page).toHaveURL(/\/ajustes$/);
  });

  test('RF-003: recupera su contraseña con el enlace del correo', async ({ page, request, browser }) => {
    const otra = await browser.newContext();
    const correo = await registrarYCompletarPerfil(await otra.newPage());
    await otra.close();

    await page.goto('/cuenta/recuperar');
    await sinFallasDeAccesibilidad(page);
    await page.getByLabel('Correo').fill(correo);
    await page.getByRole('button', { name: 'Enviar enlace' }).click();
    await expect(page.getByText(correo)).toBeVisible();

    await page.goto(await enlaceDelCorreo(request, correo, /Restablece/));
    await page.getByLabel('Contraseña nueva').fill('otra-frase-para-entrar-2026');
    await page.getByRole('button', { name: 'Guardar contraseña' }).click();
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Contraseña actualizada');

    await page.getByRole('link', { name: 'Entrar' }).click();
    await page.getByLabel('Correo').fill(correo);
    await page.getByLabel('Contraseña').fill('otra-frase-para-entrar-2026');
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Mis grupos' })).toBeVisible();
  });

  test('RF-002: sin credenciales de Google, el botón avisa y deja entrar con correo', async ({ page }) => {
    await page.goto('/cuenta/entrar');
    await page.getByRole('link', { name: 'Continuar con Google' }).click();

    await expect(page.getByRole('alert')).toHaveText('Entrar con Google todavía no está disponible. Usa tu correo.');
  });

  test('RF-007 y RF-008: exporta sus datos y elimina su cuenta', async ({ page }) => {
    const correo = await registrarYCompletarPerfil(page);
    await page.getByRole('link', { name: 'Ajustes' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Ajustes' })).toBeVisible();
    await sinFallasDeAccesibilidad(page);

    await page.getByRole('button', { name: 'Exportar mis datos' }).click();
    await expect(page).toHaveURL(/\/ajustes\/exportaciones\//);
    const excel = page.getByRole('button', { name: 'Descargar Excel' });
    await expect(excel).toBeVisible({ timeout: 30_000 });
    const descarga = page.waitForEvent('download');
    await excel.click();
    expect((await descarga).suggestedFilename()).toMatch(/^aula-lista-mis-datos-.*\.xlsx$/);

    await page.getByRole('link', { name: 'Volver a Ajustes' }).click();
    await page.getByRole('button', { name: 'Eliminar mi cuenta' }).click();
    const dialogo = page.getByRole('dialog', { name: '¿Eliminar tu cuenta?' });
    await expect(dialogo).toBeVisible();
    await sinFallasDeAccesibilidad(page);
    await dialogo.getByLabel('Escribe tu contraseña para confirmar').fill(CONTRASENA);
    await dialogo.getByRole('button', { name: 'Eliminar cuenta' }).click();

    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Tu cuenta quedó desactivada');
    await page.goto('/cuenta/entrar');
    await page.getByLabel('Correo').fill(correo);
    await page.getByLabel('Contraseña').fill(CONTRASENA);
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page.getByRole('alert')).toHaveText('El correo o la contraseña no coinciden.');
  });
});
