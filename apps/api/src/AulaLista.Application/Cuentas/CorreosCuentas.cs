using System.Net;
using AulaLista.Application.Comun;

namespace AulaLista.Application.Cuentas;

/// <summary>Plantillas de los correos de cuenta. Los enlaces llevan el token; el correo no se registra en logs.</summary>
public static class CorreosCuentas
{
    public static CorreoSaliente Verificacion(string para, string urlWeb, string token)
    {
        var enlace = $"{urlWeb.TrimEnd('/')}/cuenta/verificar?token={Uri.EscapeDataString(token)}";
        return Armar(
            para,
            "Confirma tu correo en Aula Lista",
            "Confirma tu correo para empezar a registrar a tus alumnos.",
            "Confirmar correo",
            enlace,
            "El enlace vale 24 horas. Si no creaste una cuenta, ignora este correo.");
    }

    public static CorreoSaliente Recuperacion(string para, string urlWeb, string token)
    {
        var enlace = $"{urlWeb.TrimEnd('/')}/cuenta/restablecer?token={Uri.EscapeDataString(token)}";
        return Armar(
            para,
            "Restablece tu contraseña de Aula Lista",
            "Recibimos una solicitud para restablecer tu contraseña.",
            "Elegir contraseña nueva",
            enlace,
            "El enlace vale 1 hora. Si no lo pediste, ignora este correo; tu contraseña no cambia.");
    }

    public static CorreoSaliente ExportacionLista(string para, string urlWeb, Guid exportacionId)
    {
        var enlace = $"{urlWeb.TrimEnd('/')}/ajustes/exportaciones/{exportacionId}";
        return Armar(
            para,
            "Tus datos de Aula Lista están listos",
            "Ya puedes descargar la exportación de tus datos en Excel y PDF.",
            "Descargar mis datos",
            enlace,
            "Los archivos están disponibles 24 horas y necesitas iniciar sesión para descargarlos.");
    }

    private static CorreoSaliente Armar(string para, string asunto, string mensaje, string boton, string enlace, string nota)
    {
        var e = WebUtility.HtmlEncode(enlace);
        var html = $"""
            <!doctype html>
            <html lang="es-MX"><body style="font-family:system-ui,sans-serif;color:#1f2433;max-width:480px;margin:0 auto;padding:24px">
            <p style="font-size:18px;font-weight:700">Aula Lista</p>
            <p>{WebUtility.HtmlEncode(mensaje)}</p>
            <p><a href="{e}" style="display:inline-block;background:#3b4fb8;color:#fff;padding:12px 20px;border-radius:12px;text-decoration:none;font-weight:700">{WebUtility.HtmlEncode(boton)}</a></p>
            <p style="color:#5b6275;font-size:14px">{WebUtility.HtmlEncode(nota)}</p>
            <p style="color:#5b6275;font-size:12px">Si el botón no funciona, copia este enlace: {e}</p>
            </body></html>
            """;
        var texto = $"{mensaje}\n\n{boton}: {enlace}\n\n{nota}";
        return new CorreoSaliente(para, asunto, html, texto);
    }
}
