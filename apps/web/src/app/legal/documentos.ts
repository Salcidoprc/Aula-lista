/**
 * Textos legales de Aulísima (RF-005, RF-006, RNF-006). Son la fuente única: la página /legal/:documento
 * los pinta tal cual. Al cambiar el fondo de un documento que el docente acepta, sube su `version` aquí
 * y en `Cuentas:VersionesLegales` de la API (deben ser iguales), para que se pida aceptarlo de nuevo (RN-003).
 */

/** Un párrafo, una lista con viñetas o una tabla. */
export type Bloque = string | { readonly lista: readonly string[] } | { readonly tabla: Tabla };

export interface Tabla {
  readonly encabezados: readonly string[];
  readonly filas: readonly (readonly string[])[];
}

export interface Seccion {
  readonly id: string;
  readonly titulo: string;
  readonly bloques: readonly Bloque[];
}

export interface DocumentoLegal {
  readonly titulo: string;
  /** Fecha de la versión vigente (AAAA-MM-DD); coincide con `Cuentas:VersionesLegales` cuando se acepta. */
  readonly version: string;
  readonly entrada: readonly string[];
  readonly secciones: readonly Seccion[];
}

export const RESPONSABLE = {
  nombre: 'Jesús Antonio Salcido Gaxiola',
  domicilio: 'Altaria 106, colonia Altaria, código postal 27086, Torreón, Coahuila, México',
  ciudad: 'Torreón, Coahuila',
  correoPrivacidad: 'privacidad@aulisima.com',
  correoSoporte: 'soporte@aulisima.com',
  sitio: 'https://aulisima.com',
} as const;

const R = RESPONSABLE;

const terminos: DocumentoLegal = {
  titulo: 'Términos y condiciones de uso',
  version: '2026-11-25',
  entrada: [
    `Estos términos regulan el uso de Aulísima, la aplicación web disponible en ${R.sitio}, que opera ${R.nombre}, persona física con domicilio en ${R.domicilio} (en adelante, “Aulísima” o “nosotros”).`,
    'Al crear una cuenta confirmas que leíste y aceptas estos términos y el Aviso de privacidad. Si no estás de acuerdo, no uses el servicio.',
  ],
  secciones: [
    {
      id: 'servicio',
      titulo: '1. Qué es Aulísima',
      bloques: [
        'Aulísima es una herramienta para docentes de educación básica. Permite registrar grupos y alumnos, capturar calificaciones, asistencia y participaciones, cerrar periodos con observaciones redactadas con ayuda de inteligencia artificial, generar boletas, exportar a Excel y enviar avisos a madres, padres y tutores, que los consultan en un portal sin instalar nada.',
        'Aulísima no es un sistema oficial de la Secretaría de Educación Pública ni de ninguna autoridad educativa. Las boletas y reportes que genera son documentos informativos del docente: no sustituyen la boleta oficial ni los registros que te pida tu escuela.',
        'El servicio está en etapa beta. Puede cambiar, mejorar o tener interrupciones. Te avisaremos de los cambios que afecten la forma en que lo usas.',
      ],
    },
    {
      id: 'usuarios',
      titulo: '2. Quién puede usarlo',
      bloques: [
        {
          lista: [
            'Personas mayores de 18 años, con capacidad legal para contratar, que ejercen la docencia o apoyan en ella.',
            'Cada cuenta es personal. No la compartas ni la uses en nombre de otra persona.',
            'Las madres, padres y tutores entran al portal con el enlace personal que les comparte el docente, sin crear cuenta. Para ellos aplica la sección 15.',
          ],
        },
      ],
    },
    {
      id: 'cuenta',
      titulo: '3. Tu cuenta',
      bloques: [
        {
          lista: [
            'Proporciona datos verdaderos y mantén tu correo actualizado. Debes verificar tu correo antes de registrar alumnos.',
            'Cuida tu contraseña o tu cuenta de Google. Eres responsable de lo que se haga con tu cuenta.',
            `Si notas un uso que no reconoces, cierra la sesión en todos tus dispositivos desde Ajustes y escríbenos a ${R.correoSoporte}.`,
            'Podemos suspender o cancelar una cuenta que incumpla estos términos. Salvo un riesgo de seguridad o una obligación legal, te avisaremos antes y te daremos oportunidad de exportar tus datos.',
          ],
        },
      ],
    },
    {
      id: 'datos-alumnos',
      titulo: '4. Los datos de tus alumnos y sus tutores',
      bloques: [
        'Tú decides qué alumnos y tutores registras, y lo haces solo con fines escolares. Antes de registrar al primer alumno aceptas la Declaración de autorización de datos, en la que confirmas que cuentas con la autorización de tu escuela o de los tutores.',
        'Sobre esos datos, Aulísima actúa como encargado: los trata por tu cuenta y solo para prestarte el servicio. No los vendemos, no los usamos para publicidad y no los usamos para entrenar modelos de inteligencia artificial.',
        'No registres CURP, fechas de nacimiento, domicilios, fotografías de alumnos ni datos de salud, religión, origen étnico, preferencias o cualquier otro dato sensible. Las notas y observaciones deben ser respetuosas, objetivas y sobre el desempeño escolar.',
        'Comparte cada enlace del portal solo con el tutor que corresponde. Si un enlace llega a otra persona, regéneralo o revócalo desde la ficha del alumno.',
      ],
    },
    {
      id: 'ia',
      titulo: '5. Observaciones e importación con inteligencia artificial',
      bloques: [
        'Las observaciones de cierre de periodo son borradores que redacta un modelo de inteligencia artificial de Anthropic a partir de las calificaciones, la asistencia, tus notas y el estilo que elijas. Al modelo solo le llega un seudónimo del alumno, nunca su nombre, ni el de su escuela, ni datos de contacto.',
        'Un borrador puede contener errores u omisiones. Debes leerlo y corregirlo. Nada que redacte la inteligencia artificial llega a un tutor sin que tú lo apruebes. Al aprobarlo, el texto se vuelve tuyo y eres responsable de su contenido.',
        'Si importas una lista desde una fotografía, la imagen se envía al mismo proveedor solo para leer los nombres, no se guarda y debes revisar los nombres antes de confirmarlos.',
        'Para cuidar el servicio, el número de solicitudes a la inteligencia artificial por hora es limitado.',
      ],
    },
    {
      id: 'planes',
      titulo: '6. Planes y precios',
      bloques: [
        {
          lista: [
            'Gratis: grupos, alumnos, importación, captura de calificaciones, rubros, equipos, notas, asistencia y participaciones, sin costo.',
            'Pro: se contrata por grupo e incluye las funciones que se describen en la pantalla Plan, como cierre con observaciones de inteligencia artificial, boletas, promedios, exportación a Excel y avisos a tutores.',
            'Prueba Pro: cada docente puede usarla una sola vez, en un grupo que elige al iniciar su primer cierre de periodo. Dura 14 días o hasta publicar las boletas de ese periodo, lo que ocurra después, con un máximo de 30 días. Al terminar, el grupo vuelve a Gratis sin perder datos.',
          ],
        },
        'Los precios se muestran en pesos mexicanos, con los impuestos incluidos, en la pantalla Plan antes de pagar. Si un precio cambia, te avisaremos con al menos 30 días de anticipación; mientras tu suscripción siga activa conservas el precio con el que contrataste.',
        'Los pagos los procesa Stripe. Aulísima nunca recibe ni guarda los datos completos de tu tarjeta.',
        `Si necesitas factura (CFDI), solicítala a ${R.correoSoporte} con tus datos fiscales dentro del mes en que hiciste el pago.`,
      ],
    },
    {
      id: 'tarjeta',
      titulo: '7. Pago con tarjeta: suscripción mensual',
      bloques: [
        {
          lista: [
            'Al pagar con tarjeta contratas una suscripción mensual que se renueva y se cobra automáticamente cada mes por los grupos que tengas en Pro, hasta que la canceles.',
            'Puedes agregar o quitar grupos en cualquier momento desde Plan. Antes de confirmar verás el nuevo total y cómo se refleja en tu siguiente cobro.',
            'Puedes cancelar en cualquier momento desde Plan, en “Tarjeta y facturas”. No se te volverá a cobrar y tus grupos siguen en Pro hasta el final del mes ya pagado.',
            'Si un cobro falla, tienes 7 días de gracia con un aviso diario. Después, tus grupos pasan a Gratis. No se borra ningún dato y lo ya publicado sigue visible para los tutores.',
          ],
        },
      ],
    },
    {
      id: 'oxxo',
      titulo: '8. Pago en OXXO: prepago',
      bloques: [
        {
          lista: [
            'En OXXO pagas por adelantado los meses que ofrezca el plan (por ejemplo, 1, 3 o 6). No hay cobros automáticos.',
            'La ficha de pago vence a los 3 días. Tus grupos pasan a Pro cuando Stripe confirma el pago, lo que puede tardar hasta un día hábil.',
            'Te avisaremos en la app y por correo 7 días y 1 día antes de que termine tu prepago.',
            'Si agregas un grupo durante un prepago, pagas ese grupo hasta la misma fecha de término. Quitar un grupo no genera reembolso.',
          ],
        },
      ],
    },
    {
      id: 'reembolsos',
      titulo: '9. Cancelación, revocación y reembolsos',
      bloques: [
        `Tienes derecho a revocar tu contratación dentro de los 5 días hábiles siguientes a tu primer pago de un plan, sin responsabilidad y sin dar explicaciones, conforme a la Ley Federal de Protección al Consumidor. Escríbenos a ${R.correoSoporte} desde el correo de tu cuenta.`,
        'Al revocar te devolvemos el total pagado a más tardar 10 días hábiles después de tu solicitud: en la misma tarjeta si pagaste con tarjeta, o por transferencia a una cuenta bancaria a tu nombre si pagaste en OXXO, porque OXXO no permite devoluciones automáticas. Tus grupos vuelven a Gratis sin perder datos.',
        'Fuera de ese plazo, la cancelación evita cobros futuros pero no genera reembolsos por periodos parciales. Siempre reembolsamos los cobros duplicados o hechos por error, y lo que proceda si el servicio no estuvo disponible por causas atribuibles a nosotros.',
      ],
    },
    {
      id: 'uso',
      titulo: '10. Uso aceptable',
      bloques: [
        'No está permitido:',
        {
          lista: [
            'Usar Aulísima para fines distintos a los escolares o para registrar datos de personas sin autorización.',
            'Publicar contenido ilegal, ofensivo, discriminatorio o que exponga a un menor en notas, observaciones o avisos.',
            'Intentar entrar a cuentas, grupos o datos ajenos, o probar la seguridad del servicio sin nuestro permiso por escrito.',
            'Copiar, descompilar o revender el servicio, o automatizar su uso de forma que lo sature.',
            'Hacerte pasar por otra persona o por una escuela o autoridad.',
          ],
        },
      ],
    },
    {
      id: 'propiedad',
      titulo: '11. Propiedad intelectual',
      bloques: [
        'El software, la marca Aulísima, el logotipo, el diseño y los textos de la aplicación nos pertenecen. Te damos un permiso personal, limitado, no exclusivo e intransferible para usarlos mientras tengas una cuenta.',
        'La información que registras sigue siendo tuya. Nos das permiso de guardarla y procesarla solo en la medida necesaria para prestarte el servicio, y la puedes exportar en cualquier momento.',
      ],
    },
    {
      id: 'disponibilidad',
      titulo: '12. Disponibilidad y respaldos',
      bloques: [
        'Hacemos lo razonable para que Aulísima esté disponible y segura: copias de respaldo cifradas cada día, aislamiento de los datos de cada docente y monitoreo. Aun así, no podemos garantizar un servicio sin interrupciones ni errores, y puede haber mantenimientos programados.',
        'La captura de calificaciones funciona sin conexión y se sincroniza al volver la señal. Te recomendamos exportar tus datos al cerrar cada periodo.',
      ],
    },
    {
      id: 'responsabilidad',
      titulo: '13. Responsabilidad',
      bloques: [
        'Las decisiones académicas, las calificaciones capturadas y los textos que apruebas o publicas son responsabilidad del docente. No respondemos por fallas de servicios de terceros fuera de nuestro control, como la conexión a internet, Google, Stripe, WhatsApp o el proveedor de correo, ni por caso fortuito o fuerza mayor.',
        'En la medida en que la ley lo permita, nuestra responsabilidad total frente a ti se limita al monto que nos pagaste en los 12 meses anteriores al hecho que la origine. Esta limitación no aplica en caso de dolo o negligencia grave, ni afecta los derechos que te otorga la Ley Federal de Protección al Consumidor.',
      ],
    },
    {
      id: 'terminacion',
      titulo: '14. Terminación y eliminación de la cuenta',
      bloques: [
        'Puedes eliminar tu cuenta cuando quieras desde Ajustes. Antes puedes exportar todos tus datos en Excel y PDF. Al pedir la eliminación, la cuenta se desactiva de inmediato, los enlaces de los tutores de tus grupos dejan de funcionar y tus datos se borran por completo a los 30 días. Las copias de respaldo cifradas se eliminan solas en los 30 días siguientes.',
        'Si tienes una suscripción con tarjeta, cancélala en Plan antes de eliminar tu cuenta para que no se generen nuevos cobros.',
      ],
    },
    {
      id: 'tutores',
      titulo: '15. Madres, padres y tutores',
      bloques: [
        {
          lista: [
            'El portal sirve para consultar la información escolar que publica el docente de tu hija o hijo: calificaciones, asistencia, observaciones, boletas y avisos. Es gratuito.',
            'Tu enlace es personal. No lo compartas: quien lo tenga puede ver la información. Si lo compartiste por error, pide al docente que lo cambie.',
            'La información la captura y publica el docente. Si algo no es correcto, acláralo con el docente o con la escuela.',
          ],
        },
      ],
    },
    {
      id: 'cambios',
      titulo: '16. Cambios a estos términos',
      bloques: [
        'Podemos actualizar estos términos. Publicaremos la nueva versión en esta página con su fecha y te avisaremos por correo o en la aplicación cuando el cambio sea importante. La próxima vez que entres te pediremos aceptarla; si no estás de acuerdo, puedes exportar tus datos y eliminar tu cuenta.',
      ],
    },
    {
      id: 'ley',
      titulo: '17. Ley aplicable y controversias',
      bloques: [
        `Estos términos se rigen por las leyes federales de los Estados Unidos Mexicanos. Antes de cualquier reclamación, escríbenos a ${R.correoSoporte} y buscaremos resolverlo. También puedes acudir a la Procuraduría Federal del Consumidor (Profeco).`,
        `Para la interpretación y el cumplimiento de estos términos, las partes se someten a los tribunales competentes de ${R.ciudad}, sin perjuicio del derecho que tengas como consumidor de acudir a los tribunales de tu domicilio.`,
      ],
    },
    {
      id: 'contacto',
      titulo: '18. Contacto',
      bloques: [
        {
          lista: [
            `Soporte, pagos y facturas: ${R.correoSoporte}`,
            `Privacidad y datos personales: ${R.correoPrivacidad}`,
            `Domicilio: ${R.domicilio}`,
          ],
        },
      ],
    },
  ],
};

const privacidad: DocumentoLegal = {
  titulo: 'Aviso de privacidad integral',
  version: '2026-11-25',
  entrada: [
    'Este aviso explica qué datos personales tratamos en Aulísima, para qué, con quién los compartimos y cómo puedes ejercer tus derechos, conforme a la Ley Federal de Protección de Datos Personales en Posesión de los Particulares publicada en el Diario Oficial de la Federación el 20 de marzo de 2025, y a la normativa que de ella derive.',
  ],
  secciones: [
    {
      id: 'responsable',
      titulo: '1. Responsable',
      bloques: [
        `${R.nombre}, persona física, con domicilio en ${R.domicilio}, es el responsable del tratamiento de los datos personales que se describen en este aviso. Para cualquier asunto de privacidad escríbenos a ${R.correoPrivacidad}.`,
      ],
    },
    {
      id: 'alcance',
      titulo: '2. A quién aplica y qué papel tenemos',
      bloques: [
        {
          lista: [
            'Docentes que crean una cuenta. Respecto de sus datos somos responsables.',
            'Alumnas y alumnos, y sus madres, padres o tutores, cuyos datos registra el docente. Respecto de esos datos tratamos la información por cuenta del docente, que decide qué registra y declara contar con la autorización de su escuela o de los tutores. Somos responsables solo de lo necesario para operar el portal de forma segura: identificar al tutor cuando varios docentes registran el mismo teléfono, proteger su enlace y entregarle las notificaciones que pidió.',
          ],
        },
        'Las alumnas y alumnos no usan Aulísima ni nos dan datos directamente.',
      ],
    },
    {
      id: 'datos',
      titulo: '3. Datos personales que tratamos',
      bloques: [
        {
          tabla: {
            encabezados: ['De quién', 'Datos'],
            filas: [
              [
                'Docentes',
                'Nombre, correo electrónico, contraseña (guardada solo como huella cifrada, nunca en texto), identificador de tu cuenta de Google si entras con ella, nivel educativo, nombre de la escuela, clave de centro de trabajo (opcional), estado de la república, zona horaria, estilo de observaciones, fechas de aceptación de documentos legales, historial de sesiones y datos de tu plan y pagos (identificadores de Stripe, montos, fechas y modalidad; no los datos de tu tarjeta).',
              ],
              [
                'Alumnas y alumnos',
                'Nombres, apellidos, número de lista, trato gramatical (él, ella o neutro), un seudónimo que genera el sistema y la información escolar que captura el docente: calificaciones, asistencia, participaciones, equipos, notas del docente, observaciones y boletas.',
              ],
              [
                'Madres, padres y tutores',
                'Nombre, teléfono celular, correo electrónico (opcional), parentesco y alumnos vinculados; la fecha de uso de tu enlace, tus sesiones del portal, los dispositivos en los que activaste notificaciones y si leíste los avisos.',
              ],
              [
                'Cualquier visitante',
                'Dirección IP y datos técnicos del navegador, que se usan de forma temporal para la seguridad y para detectar errores.',
              ],
            ],
          },
        },
        'No pedimos datos personales sensibles ni datos de menores que no sean escolares. Está prohibido registrar CURP, fecha de nacimiento, domicilio, fotografías de alumnos o datos de salud, religión u origen. Los datos de niñas, niños y adolescentes se tratan con el menor número de datos posible, con seudónimos ante terceros y atendiendo a su interés superior.',
      ],
    },
    {
      id: 'finalidades',
      titulo: '4. Para qué usamos tus datos',
      bloques: [
        'Finalidades necesarias para el servicio, que no requieren tu consentimiento por ser indispensables para la relación que tienes con nosotros:',
        {
          lista: [
            'Crear y administrar la cuenta del docente, verificar su correo e iniciar sesión, también con Google.',
            'Registrar grupos y alumnos, capturar y calcular calificaciones, asistencia y participaciones.',
            'Redactar borradores de observaciones con inteligencia artificial y generar boletas y exportaciones.',
            'Dar acceso al portal a los tutores y enviarles avisos y notificaciones por el navegador, por correo y, cuando esté disponible, por WhatsApp.',
            'Cobrar los planes de pago, emitir facturas y atender aclaraciones.',
            'Dar soporte y responder tus solicitudes.',
            'Proteger el servicio: limitar intentos de acceso, detectar enlaces reenviados, investigar incidentes y hacer copias de respaldo.',
            'Cumplir obligaciones legales y atender requerimientos de autoridades competentes.',
          ],
        },
        `Hoy no usamos datos personales para finalidades secundarias como publicidad o mercadotecnia. Si en el futuro quisiéramos enviar a los docentes noticias o promociones, te pediremos antes tu consentimiento y podrás negarte en cualquier momento escribiendo a ${R.correoPrivacidad}. Las estadísticas que usamos para mejorar Aulísima son conteos agregados que no identifican a nadie.`,
      ],
    },
    {
      id: 'ia',
      titulo: '5. Inteligencia artificial',
      bloques: [
        'Para redactar observaciones enviamos al modelo de inteligencia artificial de Anthropic solo el seudónimo del alumno, su trato gramatical, sus calificaciones, asistencia, las notas del docente y el estilo elegido. Nunca enviamos su nombre, el nombre de su escuela ni datos de contacto de nadie. Anthropic no usa la información que recibe por su servicio comercial para entrenar sus modelos.',
        'La única excepción es la importación de una lista desde una fotografía: la imagen contiene los nombres y se envía para leerlos. La imagen no se guarda en nuestros servidores ni en nuestros registros.',
        'No tomamos decisiones automatizadas sobre ninguna persona: el docente revisa, corrige y aprueba cada observación antes de que la vea un tutor.',
      ],
    },
    {
      id: 'terceros',
      titulo: '6. Con quién compartimos datos',
      bloques: [
        'Para prestar el servicio nos apoyamos en los siguientes proveedores, que tratan los datos solo por nuestra cuenta, bajo contrato y con medidas de seguridad. Estas comunicaciones no requieren tu consentimiento.',
        {
          tabla: {
            encabezados: ['Proveedor', 'Para qué', 'País'],
            filas: [
              [
                'Oracle Cloud',
                'Servidores y base de datos donde vive Aulísima',
                'México (Monterrey)',
              ],
              ['Microsoft Azure', 'Copias de respaldo cifradas', 'Estados Unidos'],
              [
                'Stripe',
                'Cobros con tarjeta y en OXXO (nombre, correo y montos)',
                'Estados Unidos y México',
              ],
              [
                'Brevo',
                'Envío de correos de la cuenta y avisos por correo',
                'Francia (Unión Europea)',
              ],
              [
                'Anthropic',
                'Redacción de observaciones y lectura de listas (sección 5)',
                'Estados Unidos',
              ],
              [
                'Google',
                'Inicio de sesión con Google, si lo eliges, y entrega de notificaciones en navegadores que usan su servicio',
                'Estados Unidos',
              ],
              [
                'Mozilla, Apple y Microsoft',
                'Entrega de notificaciones del navegador, según el navegador del tutor',
                'Estados Unidos',
              ],
              [
                'Meta (WhatsApp)',
                'Avisos por WhatsApp a tutores, cuando esté disponible: solo nombre del tutor, nombre corto del grupo y el enlace',
                'Estados Unidos',
              ],
              [
                'Grafana Labs',
                'Monitoreo técnico de errores y rendimiento, sin nombres ni datos de contacto',
                'Estados Unidos',
              ],
            ],
          },
        },
        'No vendemos ni rentamos datos personales. Solo transferimos datos a terceros cuando una autoridad competente lo requiera conforme a la ley. La información de un alumno la ven únicamente su docente y los tutores a los que el docente dio un enlace.',
      ],
    },
    {
      id: 'derechos',
      titulo: '7. Tus derechos (ARCO) y cómo ejercerlos',
      bloques: [
        `Tienes derecho a acceder a tus datos, rectificarlos, cancelarlos u oponerte a su tratamiento. Madres, padres y tutores pueden ejercerlos también en nombre de sus hijas e hijos menores de edad. Envía tu solicitud a ${R.correoPrivacidad} con:`,
        {
          lista: [
            'Tu nombre y el correo de tu cuenta o el teléfono con el que te registró el docente.',
            'Una identificación oficial y, si actúas en nombre de un menor o de otra persona, el documento que acredite la representación.',
            'Qué derecho quieres ejercer y sobre qué datos, y cualquier elemento que nos ayude a localizarlos (por ejemplo, el nombre del docente o del grupo).',
          ],
        },
        'Te responderemos en un plazo máximo de 20 días hábiles y, si procede, lo haremos efectivo dentro de los 15 días hábiles siguientes. El trámite es gratuito. Si tu solicitud se refiere a datos que registró un docente, se la comunicaremos para atenderla juntos.',
        'Los docentes pueden además corregir su perfil, exportar todos sus datos y eliminar su cuenta directamente desde Ajustes.',
      ],
    },
    {
      id: 'revocar',
      titulo: '8. Revocar tu consentimiento y limitar el uso',
      bloques: [
        `Puedes revocar tu consentimiento o pedir que limitemos el uso de tus datos escribiendo a ${R.correoPrivacidad}, siguiendo el mismo procedimiento de la sección 7. Los tutores pueden apagar las notificaciones del navegador desde el portal y pedir al docente que elimine su contacto. La revocación no tiene efectos retroactivos y puede impedir que sigamos prestándote el servicio.`,
      ],
    },
    {
      id: 'cookies',
      titulo: '9. Cookies y tecnologías similares',
      bloques: [
        'Usamos solo las tecnologías necesarias para que la aplicación funcione. No usamos cookies de publicidad ni de rastreo de terceros.',
        {
          lista: [
            'Cookie de sesión del docente (30 días, renovable) y cookie de sesión del tutor (90 días, renovable). No pueden leerse desde otros sitios.',
            'Almacenamiento del navegador para recordar el tema claro u oscuro y para guardar las calificaciones capturadas sin conexión hasta que se sincronizan.',
            'Un service worker que guarda la aplicación en el dispositivo para que abra rápido y funcione sin señal.',
            'La suscripción a notificaciones, solo si la activas.',
            'Registro técnico de errores del navegador, sin nombres ni datos de contacto.',
          ],
        },
      ],
    },
    {
      id: 'conservacion',
      titulo: '10. Cuánto tiempo conservamos los datos',
      bloques: [
        {
          tabla: {
            encabezados: ['Datos', 'Plazo'],
            filas: [
              [
                'Cuenta del docente y sus grupos',
                'Mientras la cuenta esté activa. Al pedir su eliminación, se borran a los 30 días.',
              ],
              ['Grupos archivados', 'Se borran 2 años después del fin de su ciclo escolar.'],
              [
                'Enlaces y sesiones de tutores',
                'El enlace vence 30 días después del fin del ciclo escolar; la sesión, a los 90 días sin uso.',
              ],
              ['Copias de respaldo cifradas', '30 días.'],
              ['Exportaciones de datos', '24 horas.'],
              ['Fotografías de listas', 'No se guardan.'],
              ['Registros técnicos', 'Hasta 30 días.'],
              [
                'Comprobantes de pago y facturas',
                'Lo que exijan las leyes fiscales, normalmente 5 años.',
              ],
            ],
          },
        },
      ],
    },
    {
      id: 'seguridad',
      titulo: '11. Seguridad',
      bloques: [
        'Protegemos los datos con conexiones cifradas (HTTPS), contraseñas guardadas como huellas cifradas, enlaces de tutores que solo guardamos como huella, aislamiento de los datos de cada docente en la base de datos, seudónimos ante terceros, copias de respaldo cifradas fuera del servidor y acceso restringido a la infraestructura. Si ocurriera una vulneración de seguridad que afecte de forma significativa tus derechos, te lo informaremos sin demora para que puedas tomar medidas.',
      ],
    },
    {
      id: 'cambios',
      titulo: '12. Cambios a este aviso',
      bloques: [
        `Cualquier cambio se publicará en ${R.sitio}/legal/privacidad con su fecha de actualización. A los docentes les pediremos aceptar la nueva versión al entrar a la aplicación; los tutores la encuentran siempre en el portal.`,
      ],
    },
    {
      id: 'autoridad',
      titulo: '13. Autoridad',
      bloques: [
        'Si consideras que tu derecho a la protección de datos personales fue vulnerado, puedes acudir ante la autoridad garante que establece la ley, actualmente la Secretaría Anticorrupción y Buen Gobierno.',
      ],
    },
  ],
};

const declaracionDatos: DocumentoLegal = {
  titulo: 'Declaración de autorización de datos',
  version: '2026-11-25',
  entrada: [
    'Antes de registrar a tu primer alumno te pedimos esta declaración. La aceptas una sola vez y guardamos la fecha y la versión.',
    'Como docente, declaro que:',
  ],
  secciones: [
    {
      id: 'declaraciones',
      titulo: 'Declaraciones del docente',
      bloques: [
        {
          lista: [
            'Soy docente, o personal autorizado, de los grupos que registro en Aulísima.',
            'Cuento con la autorización de mi escuela, o de las madres, padres o tutores, para registrar los nombres de mis alumnos, su información escolar y los datos de contacto de sus tutores, y para usarlos solo con fines escolares.',
            `Informaré a los tutores que uso Aulísima y dónde pueden consultar el aviso de privacidad: ${R.sitio}/legal/tutores.`,
            'Registraré solo los datos necesarios. No registraré CURP, fechas de nacimiento, domicilios, fotografías de alumnos ni datos sensibles.',
            'Mis notas y observaciones serán respetuosas, objetivas y sobre el desempeño escolar. Revisaré cada borrador de la inteligencia artificial antes de aprobarlo.',
            'Compartiré cada enlace del portal solo con el tutor que corresponde y lo revocaré si deja de corresponder.',
            `Atenderé, o canalizaré a ${R.correoPrivacidad}, las solicitudes de los tutores sobre sus datos o los de sus hijas e hijos.`,
          ],
        },
      ],
    },
    {
      id: 'aulisima',
      titulo: 'Lo que hace Aulísima con estos datos',
      bloques: [
        'Aulísima trata estos datos por mi cuenta y solo para prestarme el servicio, como se describe en el Aviso de privacidad. No los vende, no los usa para publicidad y no los usa para entrenar modelos de inteligencia artificial. Puedo exportarlos y eliminarlos cuando quiera.',
        'Entiendo que si registro datos sin autorización o contrarios a esta declaración, respondo frente a sus titulares, y que Aulísima puede suspender mi cuenta.',
      ],
    },
  ],
};

const tutores: DocumentoLegal = {
  titulo: 'Aviso de privacidad para madres, padres y tutores',
  version: '2026-11-25',
  entrada: [
    `El docente de tu hija o hijo usa Aulísima para registrar su información escolar y compartirla contigo. Aulísima la opera ${R.nombre}, con domicilio en ${R.domicilio}.`,
  ],
  secciones: [
    {
      id: 'datos',
      titulo: 'Qué datos tratamos',
      bloques: [
        {
          lista: [
            'Tuyos: nombre, teléfono celular, correo (si el docente lo registró) y parentesco.',
            'De tu hija o hijo: nombre, número de lista e información escolar que publica el docente, como calificaciones, asistencia, observaciones, boletas y avisos.',
          ],
        },
      ],
    },
    {
      id: 'finalidades',
      titulo: 'Para qué',
      bloques: [
        'Para mostrarte en el portal la información que publica el docente, enviarte sus avisos y las notificaciones que actives, y proteger tu acceso. No los usamos para publicidad ni los vendemos.',
        'Para redactar observaciones usamos inteligencia artificial que solo recibe un seudónimo, nunca el nombre de tu hija o hijo. El docente revisa y aprueba cada texto antes de que lo veas.',
      ],
    },
    {
      id: 'derechos',
      titulo: 'Tus derechos',
      bloques: [
        `Puedes acceder, corregir, cancelar u oponerte al uso de tus datos y de los de tu hija o hijo escribiendo a ${R.correoPrivacidad}, o pidiéndolo al docente. Puedes apagar las notificaciones desde el portal en cualquier momento.`,
        `Consulta el aviso de privacidad completo en ${R.sitio}/legal/privacidad.`,
      ],
    },
  ],
};

export const DOCUMENTOS_LEGALES = {
  terminos,
  privacidad,
  declaracion_datos: declaracionDatos,
  tutores,
} as const satisfies Record<string, DocumentoLegal>;

export type ClaveDocumento = keyof typeof DOCUMENTOS_LEGALES;
