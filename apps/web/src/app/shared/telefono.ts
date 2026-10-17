/**
 * Misma regla que la API (RN-014, `Telefono.Normalizar`): 10 dígitos son de México (+52); también se
 * aceptan 52, 521, 044 y 045 al frente. Otros países llevan + y de 8 a 15 dígitos. Devuelve E.164 o null.
 */
export function normalizarTelefono(texto: string | null | undefined): string | null {
  const limpio = (texto ?? '').trim();
  if (!limpio || /[^\d\s\-().+]/.test(limpio) || limpio.lastIndexOf('+') > 0) {
    return null;
  }
  const digitos = limpio.replace(/\D/g, '');
  if (!limpio.startsWith('+')) {
    if (digitos.length === 10) {
      return `+52${digitos}`;
    }
    if (digitos.length === 12 && digitos.startsWith('52')) {
      return `+${digitos}`;
    }
    if (digitos.length === 13 && /^(521|044|045)/.test(digitos)) {
      return `+52${digitos.slice(3)}`;
    }
    return null;
  }
  if (digitos.startsWith('52')) {
    if (digitos.length === 12) {
      return `+${digitos}`;
    }
    return digitos.length === 13 && digitos[2] === '1' ? `+52${digitos.slice(3)}` : null;
  }
  return digitos.length >= 8 && digitos.length <= 15 && digitos[0] !== '0' ? `+${digitos}` : null;
}

/** "+526671234567" se muestra "667 123 4567"; otros países, tal cual. */
export function mostrarTelefono(e164: string): string {
  const mexico = /^\+52(\d{3})(\d{3})(\d{4})$/.exec(e164);
  return mexico ? `${mexico[1]} ${mexico[2]} ${mexico[3]}` : e164;
}
