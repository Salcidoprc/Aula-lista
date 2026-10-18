import { APIRequestContext, Browser, Page, expect, test } from '@playwright/test';
import { NOMBRES } from './cierre';
import { sinFallasDeAccesibilidad } from './cuenta';
import { crearCicloYGrupo, docenteVerificado } from './grupos';

/** Un WhatsApp distinto en cada corrida: la base local se comparte entre pruebas (RN-015 vincula por número). */
const telefonoNuevo = () =>
  `66${Math.floor(Math.random() * 1e8)
    .toString()
    .padStart(8, '0')}`;

async function grupoConAlumnos(page: Page): Promise<string> {
  await crearCicloYGrupo(page);
  const grupo = new URL(page.url()).pathname.replace(/\/alumnos$/, '');
  await page.goto(grupo + '/importar?origen=texto');
  await page.getByLabel('Lista de alumnos').fill(NOMBRES.join('\n'));
  await page.getByRole('button', { name: 'Ver cómo queda' }).click();
  await page.getByRole('button', { name: `Importar ${NOMBRES.length} alumnos` }).click();
  await expect(page.getByText(`Se importaron ${NOMBRES.length} alumnos.`)).toBeVisible();
  return grupo;
}

async function agregarTutor(
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

/** El tutor en su propio navegador: sin la sesión del docente. */
async function navegadorDelTutor(browser: Browser): Promise<Page> {
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const { defaultBrowserType, ...opciones } = test.info().project.use;
  const contexto = await browser.newContext(opciones);
  return contexto.newPage();
}

/** El enlace del último correo de aviso de esa dirección, con su `?alumno=` y su `#token`. */
async function enlaceDelAviso(request: APIRequestContext, para: string): Promise<string> {
  let enlace: string | undefined;
  await expect
    .poll(async () => {
      const respuesta = await request.get(
        `/api/v1/desarrollo/correos?para=${encodeURIComponent(para)}`,
      );
      const correos = (await respuesta.json()) as { asunto: string; texto: string }[];
      enlace = correos
        .find((c) => c.asunto === 'Aviso del grupo 4° B')
        ?.texto.match(/https?:\/\/\S+/)?.[0];
      return enlace;
    })
    .toBeTruthy();
  const url = new URL(enlace!);
  return url.pathname + url.search + url.hash;
}

test.describe('SPEC-07: avisos a tutores', () => {
  test('RF-070 a RF-073, RF-063, RF-064, RN-072, RN-073 y RN-075: el docente avisa, comparte a quien no tiene canal y ve quién lo leyó; el tutor lo lee en el portal', async ({
    page,
    request,
    browser,
    context,
  }) => {
    test.setTimeout(240_000);
    // wa.me no se abre de verdad: la ventana nueva solo tiene que recibir la dirección.
    await context.route('https://wa.me/**', (ruta) => ruta.fulfill({ body: '' }));
    await docenteVerificado(page, request);
    const grupo = await grupoConAlumnos(page);
    const correoLuis = `tutor-${Date.now()}@prueba.mx`;
    await agregarTutor(page, grupo, /Cruz Ortiz/, 'Rosa Vega');
    await agregarTutor(page, grupo, /Flores Rojas/, 'Luis Paz', correoLuis);

    // La pestaña nueva del grupo, vacía al principio.
    await page.goto(grupo + '/avisos');
    await expect(page.getByRole('heading', { level: 1, name: 'Avisos' })).toBeVisible();
    await expect(page.getByText('Aún no envías avisos')).toBeVisible();
    await sinFallasDeAccesibilidad(page);

    // RF-070, RN-072, RN-073: antes de enviar se ve por dónde llega.
    await page.getByRole('link', { name: 'Nuevo aviso' }).locator('visible=true').click();
    await expect(page.getByRole('radio', { name: 'Todos los tutores (2)' })).toBeChecked();
    await page.getByLabel('Título').fill('Salida temprano el viernes');
    await page
      .getByLabel('Aviso', { exact: true })
      .fill('El viernes salimos a las 12:00 por junta de consejo.\nTraigan lunch.');
    const llegada = page.getByRole('region', { name: 'Cómo llegará' });
    await expect(llegada.getByRole('definition').nth(1)).toHaveText('1');
    await expect(llegada.getByRole('definition').nth(3)).toHaveText('1');
    await sinFallasDeAccesibilidad(page);
    // RF-071: urgente suma WhatsApp mientras quede saldo (RN-086).
    await page.getByRole('switch', { name: /Urgente/ }).check();
    await expect(llegada.getByRole('definition').nth(2)).toHaveText('2');
    await page.getByRole('switch', { name: /Urgente/ }).uncheck();
    await page.getByRole('button', { name: 'Enviar aviso' }).click();

    // RF-072: el detalle con quién lo leyó y a quién compartírselo.
    await expect(page.getByText('Aviso enviado a 2 tutores')).toBeVisible();
    await expect(
      page.getByRole('heading', { level: 1, name: 'Salida temprano el viernes' }),
    ).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Leído por 0 de 2' })).toBeVisible();
    const compartir = page.getByRole('region', { name: 'Compártelo tú' });
    const [ventana] = await Promise.all([
      context.waitForEvent('page'),
      compartir.getByRole('button', { name: 'Enviar por WhatsApp Rosa Vega' }).click(),
    ]);
    await ventana.waitForURL(/wa\.me/);
    const texto = new URL(ventana.url()).searchParams.get('text')!;
    expect(texto).toContain('"Salida temprano el viernes"');
    const enlaceRosa = new URL(texto.match(/https?:\/\/\S+/)![0]);
    await ventana.close();
    await expect(compartir.getByText(/Compartido el/)).toBeVisible();
    await sinFallasDeAccesibilidad(page);

    // RF-063, RN-075: Rosa abre su enlace y cae en la pestaña Avisos de su hijo; abrirla cuenta como leído.
    const rosa = await navegadorDelTutor(browser);
    await rosa.goto(enlaceRosa.pathname + enlaceRosa.search + enlaceRosa.hash);
    await expect(rosa).toHaveURL(/\/tutor\/alumnos\/[0-9a-f-]+\?ver=avisos$/);
    await expect(rosa.getByRole('tab', { name: /Avisos/ })).toHaveAttribute(
      'aria-selected',
      'true',
    );
    const panel = rosa.getByRole('tabpanel');
    await expect(panel.getByRole('heading', { name: 'Salida temprano el viernes' })).toBeVisible();
    await expect(panel.getByText('Traigan lunch.')).toBeVisible();
    await sinFallasDeAccesibilidad(rosa);

    // RF-073: el docente ve la lectura (la pantalla se refresca sola; aquí se recarga para no esperar).
    await expect
      .poll(async () => {
        await page.reload();
        const lectura = page.getByRole('heading', { name: /^Leído por/ });
        await lectura.waitFor();
        return (await lectura.textContent())?.trim();
      })
      .toBe('Leído por 1 de 2');
    await expect(page.getByText(/Leído el \d/)).toHaveCount(1);

    // RN-072: a Luis le llegó por correo; su enlace también abre el aviso.
    const luis = await navegadorDelTutor(browser);
    await luis.goto(await enlaceDelAviso(request, correoLuis));
    await expect(luis.getByRole('tabpanel').getByText('Salida temprano el viernes')).toBeVisible();

    // RF-064: Notificaciones del tutor, con push y correo.
    await rosa.getByRole('link', { name: 'Recibir avisos en este celular o por correo' }).click();
    await expect(rosa.getByRole('heading', { level: 1, name: 'Notificaciones' })).toBeVisible();
    await rosa.getByLabel('Tu correo').fill('rosa');
    await rosa.getByRole('button', { name: 'Guardar' }).click();
    await expect(rosa.getByText('Revisa el correo; parece incompleto.')).toBeVisible();
    await rosa.getByLabel('Tu correo').fill(`rosa-${Date.now()}@prueba.mx`);
    await rosa.getByRole('button', { name: 'Guardar' }).click();
    await expect(rosa.getByText('Correo guardado')).toBeVisible();
    await sinFallasDeAccesibilidad(rosa);

    // RN-076: borrar lo quita de la lista del docente y del portal.
    await page.getByRole('button', { name: 'Borrar aviso' }).click();
    await page
      .getByRole('dialog', { name: '¿Borrar este aviso?' })
      .getByRole('button', { name: 'Borrar', exact: true })
      .click();
    await expect(page.getByText('Aviso borrado')).toBeVisible();
    await expect(page.getByText('Aún no envías avisos')).toBeVisible();
    await luis.reload();
    await expect(luis.getByText('Aquí verás los avisos de la maestra o el maestro.')).toBeVisible();

    await rosa.context().close();
    await luis.context().close();
  });
});
