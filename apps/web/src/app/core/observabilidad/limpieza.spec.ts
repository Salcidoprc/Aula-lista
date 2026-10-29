import { limpiarEnvio } from './limpieza';

describe('limpiarEnvio (SPEC-09, RN-019)', () => {
  it('quita el token del tutor del fragmento en la URL de la página', () => {
    const envio = limpiarEnvio({
      meta: { page: { url: 'https://aulalista.mx/tutor#9f8e7d6c5b4a-token-secreto' } },
      payload: { type: 'Error', value: 'falló' },
    });

    expect(envio.meta.page.url).toBe('https://aulalista.mx/tutor');
    expect(JSON.stringify(envio)).not.toContain('token-secreto');
  });

  it('quita la consulta de rutas y URLs dentro de textos, pilas y listas', () => {
    const envio = limpiarEnvio({
      payload: {
        value: 'Http failure response for /cuenta/restablecer?token=abc123: 400 Bad Request',
        stacktrace: {
          frames: [{ filename: 'https://aulalista.mx/main-X1.js?v=2#L10', lineno: 1 }],
        },
      },
    });

    expect(envio.payload.value).toContain('Http failure response for /cuenta/restablecer');
    expect(envio.payload.value).not.toContain('abc123');
    expect(envio.payload.stacktrace.frames[0]).toEqual({
      filename: 'https://aulalista.mx/main-X1.js',
      lineno: 1,
    });
  });

  it('conserva las rutas con IDs y los valores que no son texto', () => {
    const envio = limpiarEnvio({
      url: '/grupos/0193a1b2-c3d4/periodos/5/cierre',
      n: 3,
      activo: true,
      nada: null,
    });

    expect(envio).toEqual({
      url: '/grupos/0193a1b2-c3d4/periodos/5/cierre',
      n: 3,
      activo: true,
      nada: null,
    });
  });

  it('recorta los textos muy largos', () => {
    expect(limpiarEnvio('x'.repeat(5000))).toHaveLength(2000);
  });
});
