import { Browser, Page, expect, test } from '@playwright/test';
import { NOMBRES, aprobarTodas, cerrarYGenerar, grupoCalificado } from './cierre';
import { sinFallasDeAccesibilidad } from './cuenta';
import { docenteVerificado } from './grupos';

const visible = (page: Page, nombre: string | RegExp) =>
  page.getByRole('button', { name: nombre }).locator('visible=true');

/** Un WhatsApp distinto en cada corrida: la base local se comparte entre pruebas (RN-015 vincula por número). */
function telefonoNuevo(): { captura: string; mostrado: string } {
  const digitos = `66${Math.floor(Math.random() * 1e8)
    .toString()
    .padStart(8, '0')}`;
  return {
    captura: digitos,
    mostrado: `${digitos.slice(0, 3)} ${digitos.slice(3, 6)} ${digitos.slice(6)}`,
  };
}

async function agregarTutor(page: Page, grupo: string, alumno: RegExp, telefono: string) {
  await page.goto(grupo + '/alumnos');
  await page.getByRole('link', { name: alumno }).click();
  await page.getByRole('link', { name: 'Datos' }).click();
  await page.getByLabel('Nombre del tutor').fill('Rosa Vega');
  await page.getByLabel('WhatsApp').fill(telefono);
  await page.getByRole('button', { name: 'Agregar tutor' }).click();
  await expect(page.getByText('Rosa Vega quedó como tutor.')).toBeVisible();
}

/** Abre la hoja "Enviar enlace" del tutor y devuelve la ruta del enlace (`/tutor#token`) que lleva el WhatsApp. */
async function enlaceDelTutor(page: Page): Promise<string> {
  await page.getByRole('button', { name: 'Enviar enlace Rosa Vega' }).click();
  const hoja = page.getByRole('dialog', { name: 'Enlace de Rosa Vega' });
  await expect(hoja).toBeVisible();
  const whatsapp = await hoja
    .getByRole('link', { name: 'Enviar por WhatsApp' })
    .getAttribute('href');
  const texto = new URL(whatsapp!).searchParams.get('text')!;
  const url = new URL(texto.match(/https?:\/\/\S+/)![0]);
  return url.pathname + url.hash;
}

/** El tutor en su propio navegador: sin la sesión del docente. */
async function navegadorDelTutor(browser: Browser): Promise<Page> {
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const { defaultBrowserType, ...opciones } = test.info().project.use;
  const contexto = await browser.newContext(opciones);
  return contexto.newPage();
}

test.describe('SPEC-06: portal de tutores', () => {
  test('RF-060, RF-061, RF-062 y RF-065: el tutor abre su enlace, ve a sus dos hijos, su boleta y vuelve sin el enlace; regenerar cierra su sesión', async ({
    page,
    request,
    browser,
  }) => {
    test.setTimeout(240_000);
    await docenteVerificado(page, request);
    const grupo = await grupoCalificado(page);
    const telefono = telefonoNuevo();
    // Dos hermanos en el grupo con el mismo tutor (RN-015 lo vincula a ambos por su número).
    await agregarTutor(page, grupo, /Cruz Ortiz/, telefono.captura);
    await agregarTutor(page, grupo, /Flores Rojas/, telefono.captura);

    await page.goto(grupo);
    await cerrarYGenerar(page);
    await aprobarTodas(page);
    await page
      .getByRole('link', { name: `Ir a boletas (${NOMBRES.length} aprobadas)` })
      .locator('visible=true')
      .click();
    await visible(page, `Generar boletas (${NOMBRES.length})`).click();
    await expect(page.locator('.fila[data-estado="lista"]')).toHaveCount(NOMBRES.length, {
      timeout: 60_000,
    });
    await visible(page, `Publicar boletas (${NOMBRES.length})`).click();
    await page
      .getByRole('dialog', { name: `Vas a publicar ${NOMBRES.length} boletas` })
      .getByRole('button', { name: 'Publicar', exact: true })
      .click();
    await expect(page.getByText(`${NOMBRES.length} boletas publicadas`)).toBeVisible();

    // RN-063: desde la ficha, el docente manda el enlace por WhatsApp con el texto ya escrito.
    await page.goto(grupo + '/alumnos');
    await page.getByRole('link', { name: /Cruz Ortiz/ }).click();
    await page.getByRole('link', { name: 'Datos' }).click();
    await expect(page.getByText('Aún no abre su enlace')).toBeVisible();
    const enlace = await enlaceDelTutor(page);
    expect(enlace).toMatch(/^\/tutor#[A-Za-z0-9_-]{40,}$/);
    await sinFallasDeAccesibilidad(page);
    await page.getByRole('button', { name: 'Cerrar' }).locator('visible=true').click();

    // RF-060 y criterio de 4 s: Mis hijos pinta a los dos hijos con su boleta más reciente.
    const tutor = await navegadorDelTutor(browser);
    const inicio = Date.now();
    await tutor.goto(enlace);
    await expect(tutor.getByRole('heading', { level: 1, name: 'Mis hijos' })).toBeVisible();
    await expect(tutor.locator('a.hijo')).toHaveCount(2);
    expect(Date.now() - inicio).toBeLessThan(4_000);
    // El token sale de la barra de direcciones en cuanto se canjea.
    expect(new URL(tutor.url()).hash).toBe('');
    await expect(tutor.getByText('Hola, Rosa')).toBeVisible();
    await sinFallasDeAccesibilidad(tutor);

    // RF-061 y RF-062: el detalle con sus boletas por periodo; los chips cambian de hijo.
    await tutor.locator('a.hijo').filter({ hasText: 'Cruz Ortiz' }).click();
    await expect(tutor.getByRole('heading', { level: 1 })).toContainText('Cruz Ortiz');
    await expect(tutor.getByRole('tab', { name: 'Boletas' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
    await sinFallasDeAccesibilidad(tutor);
    await tutor.getByRole('tab', { name: 'Calificaciones' }).click();
    await expect(tutor.getByRole('tabpanel')).toContainText('Actividades');
    await tutor.getByRole('tab', { name: 'Boletas' }).click();
    await tutor.locator('.periodos a').first().click();
    await expect(tutor.getByRole('heading', { name: 'Observación' })).toBeVisible();
    await expect(tutor.getByText('No es un documento oficial.')).toBeVisible();
    await sinFallasDeAccesibilidad(tutor);

    // RF-062: el PDF es el mismo de la boleta publicada.
    const [pdf] = await Promise.all([
      tutor.waitForEvent('download'),
      tutor.getByRole('link', { name: 'Descargar PDF' }).click(),
    ]);
    expect(pdf.suggestedFilename()).toMatch(/\.pdf$/);

    await tutor.getByRole('link', { name: /^Boletas de/ }).click();
    await tutor
      .getByRole('navigation', { name: 'Cambiar de hija o hijo' })
      .getByRole('link')
      .nth(1)
      .click();
    await expect(tutor.getByRole('heading', { level: 1 })).toContainText('Flores Rojas');

    // RN-061: vuelve a entrar desde el mismo navegador sin el enlace.
    await tutor.goto('/tutor');
    await expect(tutor.locator('a.hijo')).toHaveCount(2);

    // El docente ve cuándo lo abrió; regenerar revoca el enlace y la sesión (RN-063).
    await page.reload();
    await expect(page.getByText(/Abrió su enlace el \d+ de/)).toBeVisible();
    await page.getByRole('button', { name: 'Enviar enlace Rosa Vega' }).click();
    const hoja = page.getByRole('dialog', { name: 'Enlace de Rosa Vega' });
    await hoja.getByRole('button', { name: 'Regenerar enlace' }).click();
    const confirmar = page.getByRole('dialog', { name: '¿Regenerar el enlace de Rosa Vega?' });
    await expect(confirmar.getByText('deja de funcionar', { exact: false })).toBeVisible();
    await confirmar.getByRole('button', { name: 'Regenerar', exact: true }).click();
    await expect(
      page.getByText('Listo. El enlace anterior de Rosa Vega ya no funciona.'),
    ).toBeVisible();

    await tutor.goto('/tutor');
    await expect(tutor).toHaveURL(/\/tutor\/enlace-invalido$/);
    await tutor.goto(enlace);
    await expect(
      tutor.getByRole('heading', { level: 1, name: 'Este enlace ya no funciona' }),
    ).toBeVisible();
    await sinFallasDeAccesibilidad(tutor);
    await tutor.context().close();
  });

  test('RN-062: un enlace alterado lleva a "Este enlace ya no funciona" sin decir si existió', async ({
    page,
  }) => {
    await page.goto('/tutor#' + 'x'.repeat(43));
    await expect(
      page.getByRole('heading', { level: 1, name: 'Este enlace ya no funciona' }),
    ).toBeVisible();
    await expect(page.getByText('Pide a la maestra o al maestro un enlace nuevo.')).toBeVisible();
    await sinFallasDeAccesibilidad(page);
  });
});
