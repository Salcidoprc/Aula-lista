import {
  FilaVista,
  aConfirmar,
  estados,
  filasDesdeTabla,
  repetidos,
  separarNombreCompleto,
  validarFila,
} from './importacion';

function fila(datos: Partial<FilaVista>): FilaVista {
  return { renglon: 1, apellidos: 'Ruiz', nombres: 'Ana', tutores: [], excluida: false, ...datos };
}

describe('Importación de alumnos (RF-013, RF-014)', () => {
  it('separa apellidos y nombres según el orden elegido', () => {
    expect(separarNombreCompleto('García López Ana Sofía', 'apellidos')).toEqual({
      apellidos: 'García López',
      nombres: 'Ana Sofía',
    });
    expect(separarNombreCompleto('Ana Sofía García López', 'nombres')).toEqual({
      apellidos: 'García López',
      nombres: 'Ana Sofía',
    });
    expect(separarNombreCompleto('Ruiz Ana', 'apellidos')).toEqual({
      apellidos: 'Ruiz',
      nombres: 'Ana',
    });
  });

  it('respeta la coma y las partículas de los apellidos', () => {
    expect(separarNombreCompleto('De la Cruz Pérez, José', 'nombres')).toEqual({
      apellidos: 'De la Cruz Pérez',
      nombres: 'José',
    });
    expect(separarNombreCompleto('de la Cruz del Río María José', 'apellidos')).toEqual({
      apellidos: 'de la Cruz del Río',
      nombres: 'María José',
    });
  });

  it('aplica el mapeo propuesto por la API, con dos tutores', () => {
    const [f] = filasDesdeTabla(
      [
        [
          '3',
          'García',
          'López',
          'Ana',
          'María López',
          '667 123 4567',
          '',
          'Mamá',
          'Juan',
          '6671112233',
        ],
      ],
      [
        'ignorar',
        'apellido_paterno',
        'apellido_materno',
        'nombres',
        'tutor_nombre_1',
        'tutor_telefono_1',
        'tutor_correo_1',
        'tutor_parentesco_1',
        'tutor_nombre_2',
        'tutor_telefono_2',
      ],
      'apellidos',
    );
    expect(f.apellidos).toBe('García López');
    expect(f.nombres).toBe('Ana');
    expect(f.tutores).toHaveLength(2);
    expect(f.tutores[0]).toEqual({
      nombre: 'María López',
      telefono: '667 123 4567',
      correo: '',
      parentesco: 'Mamá',
    });
    expect(validarFila(f)).toBeNull();
  });

  it('marca nombre vacío, teléfono inválido y tutor incompleto como la API', () => {
    expect(validarFila(fila({ nombres: ' ' }))).toBe('nombre_vacio');
    const tutor = { nombre: 'María', telefono: '123', correo: '', parentesco: '' };
    expect(validarFila(fila({ tutores: [tutor] }))).toBe('telefono_invalido');
    expect(validarFila(fila({ tutores: [{ ...tutor, nombre: '', telefono: '6671234567' }] }))).toBe(
      'tutor_incompleto',
    );
    expect(
      validarFila(fila({ tutores: [{ ...tutor, telefono: '6671234567', correo: 'no' }] })),
    ).toBe('correo_invalido');
  });

  it('señala repetidos sin acentos ni mayúsculas, pero los deja importar', () => {
    const filas = [
      fila({ renglon: 1, apellidos: 'Pérez', nombres: 'José' }),
      fila({ renglon: 2, apellidos: 'perez', nombres: 'jose' }),
      fila({ renglon: 3 }),
    ];
    expect([...repetidos(filas)]).toEqual([1, 2]);
    expect(estados(filas, 50)).toEqual(['lista', 'lista', 'lista']);
  });

  it('con 48 alumnos en el grupo solo caben 2 más (RN-011) y las filas con error no ocupan lugar', () => {
    const filas = [fila({ nombres: '' }), fila({}), fila({}), fila({}), fila({ excluida: true })];
    expect(estados(filas, 2)).toEqual(['error', 'lista', 'lista', 'no_cabe', 'excluida']);
    expect(aConfirmar(filas, 2)).toHaveLength(2);
  });
});
