using AulaLista.Application.Grupos;
using AulaLista.Infrastructure.Ia;

namespace AulaLista.Pruebas.Unit.Grupos;

/// <summary>SPEC-02, foto de lista (RF-015): qué se acepta como foto y cómo se lee la respuesta de la IA.</summary>
public sealed class LecturaDeFotoPruebas
{
    [Fact]
    public void RF015_SoloJpgYPng_PorSusPrimerosBytes()
    {
        Assert.Equal(ImagenLista.Jpeg, CrearImportacionCasoUso.TipoDeFoto([0xFF, 0xD8, 0xFF, 0xE1, 0x00]));
        Assert.Equal(ImagenLista.Png, CrearImportacionCasoUso.TipoDeFoto([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]));
        Assert.Null(CrearImportacionCasoUso.TipoDeFoto("GIF89a"u8));
        Assert.Null(CrearImportacionCasoUso.TipoDeFoto("%PDF-1.7"u8));
        Assert.Null(CrearImportacionCasoUso.TipoDeFoto([0xFF, 0xD8]));
    }

    [Fact]
    public void RF015_RespuestaDeLaIa_UnNombrePorRenglonSinElMarcadorNiRenglonesVacios()
    {
        var nombres = PromptLista.Nombres("Acosta Beltrán Ana Sofía\n\n  Bojórquez Félix Diego  \r\nSIN_LISTA\n" + new string('x', 200));

        Assert.Equal(["Acosta Beltrán Ana Sofía", "Bojórquez Félix Diego"], nombres);
        Assert.Empty(PromptLista.Nombres(PromptLista.SinLista));
    }

    [Fact]
    public void RF015_ElPrompt_PideSoloNombresYTieneVersion()
    {
        Assert.Equal("lista-v1", PromptLista.Version);
        Assert.Contains("SIN_LISTA", PromptLista.Sistema, StringComparison.Ordinal);
        Assert.Contains("Nada más", PromptLista.Sistema, StringComparison.Ordinal);
    }
}
