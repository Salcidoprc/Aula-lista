using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace AulaLista.Pruebas.Integracion;

/// <summary>Criterios de aceptación de SPEC-03 (rubros, criterios, actividades, captura y cola sin conexión).</summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class Spec03EvaluacionPruebas(BaseDeDatosFixture baseDeDatos) : IDisposable
{
    private readonly FabricaApi _fabrica = new(baseDeDatos);

    public void Dispose() => _fabrica.Dispose();

    [Fact]
    public async Task SPEC03_ExposicionConCriterios40_30_30_Con9_8_10_Da9()
    {
        var g = await PrepararAsync(1);
        var rubros = await GuardarRubrosAsync(g, Exposicion("numerica", 0m));
        var rubro = rubros[0];
        var actividad = await CrearActividadAsync(g, rubro.Id, "Exposición del sistema solar");

        var resultado = await GuardarAsync(g, Celda(actividad, g.Alumnos[0], criterios: Criterios(rubro, "9", "8", "10")));

        Assert.Empty(resultado.GetProperty("rechazadas").EnumerateArray());
        var matriz = await MatrizAsync(g);
        Assert.Equal(9.00m, Valor(Celdas(matriz).Single(), "valor"));
        Assert.Equal(9.00m, PromedioRubro(matriz, g.Alumnos[0], rubro.Id));
    }

    [Fact]
    public async Task RN111_LosCriteriosSeLeenEnElOrdenEnQueSeGuardaron()
    {
        var g = await PrepararAsync(0);
        await GuardarRubrosAsync(g, Exposicion("numerica", 0m));

        var leidos = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{g.PeriodoId}/rubros");
        var nombres = leidos.GetProperty("rubros")[0].GetProperty("criterios").EnumerateArray()
            .Select(c => c.GetProperty("nombre").GetString()).ToList();

        Assert.Equal(["Información", "Presentación", "Dominio del tema"], nombres);
    }

    [Fact]
    public async Task SPEC03_CriteriosQueSuman90_SeRechazanConPesosCriteriosNoSuman100()
    {
        var g = await PrepararAsync(0);
        var exposicion = new
        {
            nombre = "Exposición",
            tipoRubroId = g.Tipo("Exposición"),
            peso = 100m,
            forma = "numerica",
            califMinima = 5m,
            criterios = new[]
            {
                new { nombre = "Información", peso = 40m, forma = "numerica", califMinima = 5m },
                new { nombre = "Presentación", peso = 50m, forma = "numerica", califMinima = 5m },
            },
        };

        var respuesta = await g.Cliente.PutAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/rubros", new { rubros = new[] { exposicion } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal("pesos_criterios_no_suman_100", await CodigoAsync(respuesta));
    }

    [Fact]
    public async Task SPEC03_Actividades30ConOcho_Nueve_Diez_Siete_Seis_PromedioOchoYAporta240()
    {
        var g = await PrepararAsync(1);
        var rubros = await GuardarRubrosAsync(g, Simple("Actividades", 30m), Simple("Exámenes", 70m));
        var actividades = new List<Guid>();
        foreach (var valor in new[] { "8", "9", "10", "7", "6" })
        {
            var actividad = await CrearActividadAsync(g, rubros[0].Id, $"Actividad {valor}");
            actividades.Add(actividad);
            await GuardarAsync(g, Celda(actividad, g.Alumnos[0], valor));
        }

        var examen = await CrearActividadAsync(g, rubros[1].Id, "Examen");
        await GuardarAsync(g, Celda(examen, g.Alumnos[0], "10"));
        var matriz = await MatrizAsync(g);

        Assert.Equal(8.00m, PromedioRubro(matriz, g.Alumnos[0], rubros[0].Id));
        Assert.Equal(9.40m, Resultado(matriz, g.Alumnos[0]).GetProperty("periodo").GetDecimal()); // 8 × 30 / 100 + 10 × 70 / 100
        Assert.Equal(100m, matriz.GetProperty("sumaPesos").GetDecimal());
    }

    [Fact]
    public async Task SPEC03_NoEntregoConMinima5_CuentaComo5()
    {
        var g = await PrepararAsync(1);
        var rubro = (await GuardarRubrosAsync(g, Simple("Tareas", 100m, "numerica")))[0];
        var a1 = await CrearActividadAsync(g, rubro.Id, "Tarea 1");
        var a2 = await CrearActividadAsync(g, rubro.Id, "Tarea 2");

        await GuardarAsync(g, Celda(a1, g.Alumnos[0], "9"), Celda(a2, g.Alumnos[0], marca: "no_entrego"));

        Assert.Equal(7.00m, PromedioRubro(await MatrizAsync(g), g.Alumnos[0], rubro.Id));
    }

    [Fact]
    public async Task SPEC03_CumplimientoConMinima5_CuatroCumplioYUnoNo_Da9()
    {
        var g = await PrepararAsync(1);
        var rubro = (await GuardarRubrosAsync(g, Simple("Tareas", 100m, "cumplimiento")))[0];
        var celdas = new List<object>();
        for (var i = 0; i < 5; i++)
        {
            celdas.Add(Celda(await CrearActividadAsync(g, rubro.Id, $"Tarea {i + 1}"), g.Alumnos[0], marca: i < 4 ? "cumplio" : "no_cumplio"));
        }

        await GuardarAsync(g, [.. celdas]);

        Assert.Equal(9.00m, PromedioRubro(await MatrizAsync(g), g.Alumnos[0], rubro.Id));
    }

    [Theory]
    [InlineData("3", "calificacion_menor_a_minima")]
    [InlineData("11", "calificacion_fuera_de_rango")]
    [InlineData("-1", "calificacion_fuera_de_rango")]
    [InlineData("8.555", "calificacion_invalida")]
    public async Task SPEC03_ValorInvalido_SeRechazaPorCeldaSinDetenerElLote(string valor, string codigo)
    {
        var g = await PrepararAsync(2);
        var rubro = (await GuardarRubrosAsync(g, Simple("Exámenes", 100m)))[0];
        var actividad = await CrearActividadAsync(g, rubro.Id, "Examen");

        var resultado = await GuardarAsync(g, Celda(actividad, g.Alumnos[0], valor), Celda(actividad, g.Alumnos[1], "9"));

        var rechazada = Assert.Single(resultado.GetProperty("rechazadas").EnumerateArray());
        Assert.Equal(codigo, rechazada.GetProperty("codigo").GetString());
        Assert.Equal(g.Alumnos[1], Assert.Single(resultado.GetProperty("aplicadas").EnumerateArray()).GetProperty("alumnoId").GetGuid());
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM calificacion WHERE actividad_id = '{actividad}'"));
    }

    [Fact]
    public async Task SPEC03_CriterioPorCumplimientoConMinima0_NoCumplioAporta0()
    {
        var g = await PrepararAsync(1);
        var rubro = (await GuardarRubrosAsync(g, Exposicion("cumplimiento", 0m)))[0];
        var actividad = await CrearActividadAsync(g, rubro.Id, "Exposición");

        await GuardarAsync(g, Celda(actividad, g.Alumnos[0], criterios:
        [
            new { criterioId = rubro.Criterios[0], valor = (decimal?)9m, marca = "ninguna" },
            new { criterioId = rubro.Criterios[1], valor = (decimal?)8m, marca = "ninguna" },
            new { criterioId = rubro.Criterios[2], valor = (decimal?)null, marca = "no_cumplio" },
        ]));

        Assert.Equal(6.00m, Valor(Celdas(await MatrizAsync(g)).Single(), "valor"));
    }

    [Fact]
    public async Task SPEC03_SinConexion30Calificaciones_LleganSinDuplicadosAunqueSeReenvien()
    {
        var g = await PrepararAsync(30);
        var rubro = (await GuardarRubrosAsync(g, Simple("Exámenes", 100m)))[0];
        var actividad = await CrearActividadAsync(g, rubro.Id, "Examen");
        var cambios = g.Alumnos.Select((alumno, i) => new
        {
            id = Guid.CreateVersion7(),
            actividadId = actividad,
            alumnoId = alumno,
            valor = (decimal?)(5 + (i % 6)),
            marca = "ninguna",
            version = 0,
        }).ToList();

        var primera = await SincronizarAsync(g, cambios);
        var segunda = await SincronizarAsync(g, cambios); // la app se cerró antes de recibir la respuesta

        Assert.Equal(30, primera.GetProperty("aplicadas").GetArrayLength());
        Assert.Equal(30, segunda.GetProperty("aplicadas").GetArrayLength());
        Assert.All(segunda.GetProperty("aplicadas").EnumerateArray(), c => Assert.Equal(1, c.GetProperty("version").GetInt32()));
        Assert.Equal(30, await ContarAsync($"SELECT count(*) FROM calificacion WHERE actividad_id = '{actividad}'"));
        Assert.Equal(30, await ContarAsync($"SELECT count(*) FROM cambio_sincronizado WHERE docente_id = '{g.DocenteId}'"));
    }

    [Fact]
    public async Task RF037_MismaIdempotencyKey_NoAplicaDosVeces()
    {
        var g = await PrepararAsync(1);
        var rubro = (await GuardarRubrosAsync(g, Simple("Exámenes", 100m)))[0];
        var actividad = await CrearActividadAsync(g, rubro.Id, "Examen");
        var clave = Guid.CreateVersion7();

        await GuardarConClaveAsync(g, clave, Celda(actividad, g.Alumnos[0], "8"));
        var repetida = await GuardarConClaveAsync(g, clave, Celda(actividad, g.Alumnos[0], "8"));

        var celda = Assert.Single(repetida.GetProperty("aplicadas").EnumerateArray());
        Assert.Equal(1, celda.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task SPEC03_DosDispositivosEnLaMismaCelda_GanaLaUltimaQueLlega()
    {
        var g = await PrepararAsync(1);
        var rubro = (await GuardarRubrosAsync(g, Simple("Exámenes", 100m)))[0];
        var actividad = await CrearActividadAsync(g, rubro.Id, "Examen");

        await GuardarAsync(g, Celda(actividad, g.Alumnos[0], "7", version: 0));
        var ultima = await GuardarAsync(g, Celda(actividad, g.Alumnos[0], "9", version: 0));

        var celda = Assert.Single(ultima.GetProperty("aplicadas").EnumerateArray());
        Assert.Equal(9m, celda.GetProperty("valor").GetDecimal());
        Assert.Equal(2, celda.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task SPEC03_BorrarActividadConCalificaciones_PideConfirmacionYRecalcula()
    {
        var g = await PrepararAsync(1);
        var rubro = (await GuardarRubrosAsync(g, Simple("Exámenes", 100m)))[0];
        var a1 = await CrearActividadAsync(g, rubro.Id, "Examen 1");
        var a2 = await CrearActividadAsync(g, rubro.Id, "Examen 2");
        await GuardarAsync(g, Celda(a1, g.Alumnos[0], "6"), Celda(a2, g.Alumnos[0], "10"));

        var sinConfirmar = await g.Cliente.DeleteAsync($"/api/v1/actividades/{a1}");
        var confirmado = await g.Cliente.DeleteAsync($"/api/v1/actividades/{a1}?confirmar=true");

        Assert.Equal(HttpStatusCode.Conflict, sinConfirmar.StatusCode);
        Assert.Equal("borrado_requiere_confirmacion", await CodigoAsync(sinConfirmar));
        Assert.Equal(HttpStatusCode.NoContent, confirmado.StatusCode);
        Assert.Equal(10.00m, PromedioRubro(await MatrizAsync(g), g.Alumnos[0], rubro.Id));
    }

    [Fact]
    public async Task RN113_QuitarRubroOCriterioConCalificaciones_PideConfirmacion()
    {
        var g = await PrepararAsync(1);
        var rubros = await GuardarRubrosAsync(g, Exposicion("numerica", 0m));
        var actividad = await CrearActividadAsync(g, rubros[0].Id, "Exposición");
        await GuardarAsync(g, Celda(actividad, g.Alumnos[0], criterios: Criterios(rubros[0], "9", "8", "10")));
        var sinUnCriterio = ConIds(rubros[0], Exposicion("numerica", 0m), quitarUltimo: true)(g);

        var sinConfirmar = await g.Cliente.PutAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/rubros", new { rubros = new[] { sinUnCriterio } });
        var confirmado = await g.Cliente.PutAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/rubros", new { rubros = new[] { sinUnCriterio }, confirmarBorrado = true });

        Assert.Equal("borrado_requiere_confirmacion", await CodigoAsync(sinConfirmar));
        Assert.Equal(HttpStatusCode.OK, confirmado.StatusCode);
        Assert.Equal(8.60m, Valor(Celdas(await MatrizAsync(g)).Single(), "valor")); // 9 × 60 % + 8 × 40 % sin Dominio del tema

        var sinRubros = await g.Cliente.PutAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/rubros", new { rubros = Array.Empty<object>() });
        Assert.Equal("borrado_requiere_confirmacion", await CodigoAsync(sinRubros));
    }

    [Fact]
    public async Task RN113_CambiarPesosDeCriterios_RecalculaLasActividades()
    {
        var g = await PrepararAsync(1);
        var rubro = (await GuardarRubrosAsync(g, Exposicion("numerica", 0m)))[0];
        var actividad = await CrearActividadAsync(g, rubro.Id, "Exposición");
        await GuardarAsync(g, Celda(actividad, g.Alumnos[0], criterios: Criterios(rubro, "10", "5", "5")));
        var nuevo = ConIds(rubro, Exposicion("numerica", 0m, pesos: [50m, 25m, 25m]))(g);

        (await g.Cliente.PutAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/rubros", new { rubros = new[] { nuevo } })).EnsureSuccessStatusCode();

        Assert.Equal(7.50m, Valor(Celdas(await MatrizAsync(g)).Single(), "valor"));
    }

    [Fact]
    public async Task RN120_FormaConCalificaciones_NoCambiaYMinimaMayorAvisaCuantas()
    {
        var g = await PrepararAsync(2);
        var rubro = (await GuardarRubrosAsync(g, Simple("Exámenes", 100m)))[0];
        var actividad = await CrearActividadAsync(g, rubro.Id, "Examen");
        await GuardarAsync(g, Celda(actividad, g.Alumnos[0], "6"), Celda(actividad, g.Alumnos[1], "6.5"));

        var forma = await g.Cliente.PutAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/rubros",
            new { rubros = new[] { new { id = rubro.Id, nombre = "Exámenes", tipoRubroId = g.Tipo("Exámenes"), peso = 100m, forma = "cumplimiento", califMinima = 5m } } });
        var minima = await g.Cliente.PutAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/rubros",
            new { rubros = new[] { new { id = rubro.Id, nombre = "Exámenes", tipoRubroId = g.Tipo("Exámenes"), peso = 100m, forma = "numerica", califMinima = 7m } } });

        Assert.Equal(HttpStatusCode.Conflict, forma.StatusCode);
        Assert.Equal("forma_con_calificaciones", await CodigoAsync(forma));
        var problema = await minima.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("calificaciones_menores_a_minima", problema.GetProperty("codigo").GetString());
        Assert.Contains("2 calificaciones", problema.GetProperty("title").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RN037_RN038_EnCierreNoSeEditanRubrosYCerradoEsDeSoloLectura()
    {
        var g = await PrepararAsync(1);
        var rubro = (await GuardarRubrosAsync(g, Simple("Exámenes", 100m)))[0];
        var actividad = await CrearActividadAsync(g, rubro.Id, "Examen");

        await EjecutarSqlAsync($"UPDATE periodo SET estado = 'en_cierre' WHERE id = '{g.PeriodoId}'");
        var rubrosEnCierre = await g.Cliente.PutAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/rubros", new { rubros = Array.Empty<object>() });
        var capturaEnCierre = await GuardarAsync(g, Celda(actividad, g.Alumnos[0], "9"));

        await EjecutarSqlAsync($"UPDATE periodo SET estado = 'cerrado' WHERE id = '{g.PeriodoId}'");
        var capturaCerrado = await GuardarAsync(g, Celda(actividad, g.Alumnos[0], "8"));
        var actividadCerrado = await g.Cliente.PostAsJsonAsync($"/api/v1/rubros/{rubro.Id}/actividades", new { nombre = "Otro" });

        Assert.Equal("periodo_en_cierre", await CodigoAsync(rubrosEnCierre));
        Assert.Single(capturaEnCierre.GetProperty("aplicadas").EnumerateArray());
        var rechazada = Assert.Single(capturaCerrado.GetProperty("rechazadas").EnumerateArray());
        Assert.Equal("periodo_cerrado", rechazada.GetProperty("codigo").GetString());
        Assert.Equal(9m, rechazada.GetProperty("vigente").GetProperty("valor").GetDecimal());
        Assert.Equal("periodo_cerrado", await CodigoAsync(actividadCerrado));
        Assert.True((await MatrizAsync(g)).GetProperty("soloLectura").GetBoolean());
    }

    [Fact]
    public async Task RF031_CopiarRubros_TraeSusCriteriosYSoloAUnPeriodoVacio()
    {
        var g = await PrepararAsync(0);
        await GuardarRubrosAsync(g, Exposicion("numerica", 5m));
        var periodos = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{g.GrupoId}/periodos");
        var segundo = periodos[1].GetProperty("id").GetGuid();

        var copia = await g.Cliente.PostAsJsonAsync($"/api/v1/periodos/{segundo}/rubros/copiar", new { desdePeriodoId = g.PeriodoId });
        var otraVez = await g.Cliente.PostAsJsonAsync($"/api/v1/periodos/{segundo}/rubros/copiar", new { desdePeriodoId = g.PeriodoId });

        Assert.Equal(HttpStatusCode.OK, copia.StatusCode);
        var rubro = Assert.Single((await copia.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rubros").EnumerateArray());
        Assert.Equal(3, rubro.GetProperty("criterios").GetArrayLength());
        Assert.Equal("periodo_con_rubros", await CodigoAsync(otraVez));
    }

    [Fact]
    public async Task RN039_ActividadConFechaFueraDeLaUnidad_SeRechaza()
    {
        var g = await PrepararAsync(0);
        var rubro = (await GuardarRubrosAsync(g, Simple("Exámenes", 100m)))[0];

        var fuera = await g.Cliente.PostAsJsonAsync($"/api/v1/rubros/{rubro.Id}/actividades", new { nombre = "Examen", fecha = "2027-07-01" });
        var dentro = await g.Cliente.PostAsJsonAsync($"/api/v1/rubros/{rubro.Id}/actividades", new { nombre = "Examen", fecha = "2026-09-01" });

        Assert.Equal("fecha_fuera_de_unidad", await CodigoAsync(fuera));
        Assert.Equal(HttpStatusCode.Created, dentro.StatusCode);
    }

    [Fact]
    public async Task RN027_UnidadConRubros_NoSeBorra()
    {
        var g = await PrepararAsync(0);
        await GuardarRubrosAsync(g, Simple("Exámenes", 100m));
        var ciclo = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/ciclos/{g.CicloId}");
        var restantes = ciclo.GetProperty("unidades").EnumerateArray().Skip(1).ToList();

        var respuesta = await g.Cliente.PutAsJsonAsync($"/api/v1/ciclos/{g.CicloId}/unidades", new { unidades = restantes });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("unidad_con_datos", await CodigoAsync(respuesta));
    }

    [Fact]
    public async Task SPEC02_GrupoConCalificaciones_NoSeBorra()
    {
        var g = await PrepararAsync(1);
        var rubro = (await GuardarRubrosAsync(g, Simple("Exámenes", 100m)))[0];
        await GuardarAsync(g, Celda(await CrearActividadAsync(g, rubro.Id, "Examen"), g.Alumnos[0], "9"));

        var respuesta = await g.Cliente.DeleteAsync($"/api/v1/grupos/{g.GrupoId}");

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("grupo_con_calificaciones", await CodigoAsync(respuesta));
    }

    [Fact]
    public async Task SPEC03_AlumnoDadoDeBaja_SeVeEnLaMatrizPeroNoSeCaptura()
    {
        var g = await PrepararAsync(2);
        var rubro = (await GuardarRubrosAsync(g, Simple("Exámenes", 100m)))[0];
        var actividad = await CrearActividadAsync(g, rubro.Id, "Examen");
        await GuardarAsync(g, Celda(actividad, g.Alumnos[0], "9"));
        (await g.Cliente.DeleteAsync($"/api/v1/alumnos/{g.Alumnos[0]}")).EnsureSuccessStatusCode();
        (await g.Cliente.DeleteAsync($"/api/v1/alumnos/{g.Alumnos[1]}")).EnsureSuccessStatusCode();

        var resultado = await GuardarAsync(g, Celda(actividad, g.Alumnos[0], "10"));
        var matriz = await MatrizAsync(g);

        Assert.Equal("alumno_dado_de_baja", resultado.GetProperty("rechazadas")[0].GetProperty("codigo").GetString());
        var alumno = Assert.Single(matriz.GetProperty("alumnos").EnumerateArray()); // la baja sin calificaciones no aparece
        Assert.True(alumno.GetProperty("dadoDeBaja").GetBoolean());
    }

    [Fact]
    public async Task RNF001_EvaluacionDeOtroDocente_Responde404YNoSeCaptura()
    {
        var g = await PrepararAsync(1);
        var rubro = (await GuardarRubrosAsync(g, Simple("Exámenes", 100m)))[0];
        var actividad = await CrearActividadAsync(g, rubro.Id, "Examen");
        var (intruso, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();

        var respuestas = new[]
        {
            await intruso.GetAsync($"/api/v1/periodos/{g.PeriodoId}/rubros"),
            await intruso.PutAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/rubros", new { rubros = Array.Empty<object>(), confirmarBorrado = true }),
            await intruso.PostAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/rubros/copiar", new { desdePeriodoId = g.PeriodoId }),
            await intruso.GetAsync($"/api/v1/periodos/{g.PeriodoId}/calificaciones"),
            await intruso.GetAsync($"/api/v1/rubros/{rubro.Id}/actividades"),
            await intruso.PostAsJsonAsync($"/api/v1/rubros/{rubro.Id}/actividades", new { nombre = "Intrusa" }),
            await intruso.PutAsJsonAsync($"/api/v1/actividades/{actividad}", new { nombre = "Intrusa" }),
            await intruso.DeleteAsync($"/api/v1/actividades/{actividad}?confirmar=true"),
        };
        var captura = await (await intruso.PutAsJsonAsync("/api/v1/calificaciones", new { celdas = new[] { Celda(actividad, g.Alumnos[0], "10") } }))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        Assert.Equal("celda_inexistente", captura.GetProperty("rechazadas")[0].GetProperty("codigo").GetString());
        Assert.Equal(0, await ContarAsync($"SELECT count(*) FROM calificacion WHERE actividad_id = '{actividad}'"));
    }

    [Fact]
    public async Task ADR0012_TiposDeRubro_LosLeeLaAppPeroNoLosEscribe()
    {
        var g = await PrepararAsync(0);
        var tipos = await g.Cliente.GetFromJsonAsync<JsonElement>("/api/v1/tipos-rubro");

        // Los sembrados van primero; después pueden venir los que agregó un administrador (SPEC-09).
        Assert.Equal(["Tareas", "Actividades", "Prácticas", "Participaciones", "Exámenes", "Proyecto", "Exposición", "Asistencia"],
            tipos.EnumerateArray().Take(8).Select(t => t.GetProperty("nombre").GetString()));
        await using var conexion = new NpgsqlConnection(baseDeDatos.ConexionRuntime);
        await conexion.OpenAsync();
        // SPEC-09: sin app.admin_id el rol de la app no cambia el catálogo, y nadie lo borra (RN-094).
        await using var cambio = new NpgsqlCommand("UPDATE tipo_rubro SET nombre = 'X'", conexion);
        Assert.Equal(0, await cambio.ExecuteNonQueryAsync());
        await using var alta = new NpgsqlCommand(
            "INSERT INTO tipo_rubro (id, nombre, descripcion, icono, comportamiento, forma_sugerida, nombres_sugeridos, orden, activo, created_at, updated_at) "
            + "VALUES (gen_random_uuid(), 'X', '', 'x', 'actividades', 'numerica', ARRAY['X'], 99, true, now(), now())",
            conexion);
        var errorAlta = await Assert.ThrowsAsync<PostgresException>(() => alta.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, errorAlta.SqlState);
        await using var borrado = new NpgsqlCommand("DELETE FROM tipo_rubro", conexion);
        var error = await Assert.ThrowsAsync<PostgresException>(() => borrado.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task SPEC03_EquipoDe4Con9_AjusteA7_YRecalificarCon8_TresTienen8YElAjustadoConserva7()
    {
        var g = await PrepararAsync(5);
        var rubro = (await GuardarRubrosAsync(g, Simple("Proyecto", 100m)))[0];
        var equipos = await GuardarEquiposAsync(g, ("Los Halcones", g.Alumnos.Take(4).ToArray()));
        var actividad = await CrearActividadAsync(g, rubro.Id, "Maqueta", "por_equipo");

        await GuardarAsync(g, CeldaEquipo(actividad, equipos[0], "9"));
        var primera = await MatrizAsync(g);
        Assert.All(g.Alumnos.Take(4), a => Assert.Equal(9m, ValorDe(primera, actividad, a)));
        Assert.Null(ValorDe(primera, actividad, g.Alumnos[4])); // sin equipo: se califica solo

        await GuardarAsync(g, Celda(actividad, g.Alumnos[1], "7"));
        await GuardarAsync(g, CeldaEquipo(actividad, equipos[0], "8"));

        var matriz = await MatrizAsync(g);
        Assert.Equal([8m, 7m, 8m, 8m], g.Alumnos.Take(4).Select(a => ValorDe(matriz, actividad, a)));
        Assert.Equal(["equipo", "ajustada", "equipo", "equipo"], g.Alumnos.Take(4).Select(a => CeldaDe(matriz, actividad, a).GetProperty("origen").GetString()));
        Assert.Equal(7m, PromedioRubro(matriz, g.Alumnos[1], rubro.Id)); // RN-117: cuenta su propia calificación
    }

    [Fact]
    public async Task RN116_ReorganizarEquiposDespuesDeCalificar_NoCambiaCalificacionesNiComposicion()
    {
        var g = await PrepararAsync(4);
        var rubro = (await GuardarRubrosAsync(g, Simple("Proyecto", 100m)))[0];
        var equipos = await GuardarEquiposAsync(g, ("Rojo", [g.Alumnos[0], g.Alumnos[1]]), ("Azul", [g.Alumnos[2], g.Alumnos[3]]));
        var actividad = await CrearActividadAsync(g, rubro.Id, "Maqueta", "por_equipo");
        await GuardarAsync(g, CeldaEquipo(actividad, equipos[0], "9"), CeldaEquipo(actividad, equipos[1], "6"));

        // El alumno 1 pasa de Rojo a Azul y se agrega un equipo nuevo vacío.
        var respuesta = await g.Cliente.PutAsJsonAsync($"/api/v1/grupos/{g.GrupoId}/equipos", new
        {
            equipos = new object[]
            {
                new { id = equipos[0], nombre = "Rojo", alumnoIds = new[] { g.Alumnos[0] } },
                new { id = equipos[1], nombre = "Azul", alumnoIds = new[] { g.Alumnos[2], g.Alumnos[3], g.Alumnos[1] } },
                new { nombre = "Verde", alumnoIds = Array.Empty<Guid>() },
            },
        });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var matriz = await MatrizAsync(g);
        Assert.Equal([9m, 9m, 6m, 6m], g.Alumnos.Select(a => ValorDe(matriz, actividad, a)));
        var rojo = matriz.GetProperty("composiciones").EnumerateArray().Single(c => c.GetProperty("equipoId").GetGuid() == equipos[0]);
        Assert.Equal(new[] { g.Alumnos[0], g.Alumnos[1] }.Order(), rojo.GetProperty("alumnoIds").EnumerateArray().Select(a => a.GetGuid()).Order());

        // Recalificar a Rojo en esta actividad sigue la composición guardada: el alumno 1 también cambia.
        await GuardarAsync(g, CeldaEquipo(actividad, equipos[0], "10"));
        matriz = await MatrizAsync(g);
        Assert.Equal([10m, 10m, 6m, 6m], g.Alumnos.Select(a => ValorDe(matriz, actividad, a)));
        Assert.Equal(3, matriz.GetProperty("equipos").GetArrayLength());
    }

    [Fact]
    public async Task RN114_UnAlumnoEnDosEquiposONombreRepetido_SeRechazaConEquiposInvalidos()
    {
        var g = await PrepararAsync(2);
        var enDos = await g.Cliente.PutAsJsonAsync($"/api/v1/grupos/{g.GrupoId}/equipos", new
        {
            equipos = new object[]
            {
                new { nombre = "Uno", alumnoIds = new[] { g.Alumnos[0] } },
                new { nombre = "Dos", alumnoIds = new[] { g.Alumnos[0], g.Alumnos[1] } },
            },
        });
        var repetido = await g.Cliente.PutAsJsonAsync($"/api/v1/grupos/{g.GrupoId}/equipos", new
        {
            equipos = new object[] { new { nombre = "Uno", alumnoIds = Array.Empty<Guid>() }, new { nombre = "uno ", alumnoIds = Array.Empty<Guid>() } },
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, enDos.StatusCode);
        Assert.Equal("equipos_invalidos", await CodigoAsync(enDos));
        Assert.Equal("equipos_invalidos", await CodigoAsync(repetido));
        Assert.Equal(0, await ContarAsync($"SELECT count(*) FROM equipo WHERE grupo_id = '{g.GrupoId}'"));
    }

    [Fact]
    public async Task RF037_EquipoSinConexion_LlegaUnaVezAunqueSeReenvie()
    {
        var g = await PrepararAsync(3);
        var rubro = (await GuardarRubrosAsync(g, Simple("Proyecto", 100m)))[0];
        var equipos = await GuardarEquiposAsync(g, ("Todos", g.Alumnos.ToArray()));
        var actividad = await CrearActividadAsync(g, rubro.Id, "Maqueta", "por_equipo");
        var cambio = new
        {
            id = Guid.NewGuid(),
            actividadId = actividad,
            alumnoId = Guid.Empty,
            equipoId = equipos[0],
            valor = 8.5m,
            marca = "ninguna",
            version = 0,
        };

        var primera = await SincronizarAsync(g, new[] { cambio });
        var segunda = await SincronizarAsync(g, new[] { cambio });

        Assert.Equal(3, primera.GetProperty("aplicadas").GetArrayLength());
        Assert.Equal(3, segunda.GetProperty("aplicadas").GetArrayLength());
        Assert.All(segunda.GetProperty("aplicadas").EnumerateArray(), c => Assert.Equal(1, c.GetProperty("version").GetInt32()));
        Assert.Equal(3, await ContarAsync($"SELECT count(*) FROM actividad_equipo WHERE actividad_id = '{actividad}'"));
    }

    [Fact]
    public async Task SPEC03_EquipoEnActividadIndividual_SeRechazaConCeldaInexistente()
    {
        var g = await PrepararAsync(2);
        var rubro = (await GuardarRubrosAsync(g, Simple("Proyecto", 100m)))[0];
        var equipos = await GuardarEquiposAsync(g, ("Todos", g.Alumnos.ToArray()));
        var actividad = await CrearActividadAsync(g, rubro.Id, "Individual");

        var resultado = await GuardarAsync(g, CeldaEquipo(actividad, equipos[0], "9"));

        Assert.Equal("celda_inexistente", resultado.GetProperty("rechazadas")[0].GetProperty("codigo").GetString());
        Assert.Equal(equipos[0], resultado.GetProperty("rechazadas")[0].GetProperty("equipoId").GetGuid());
    }

    [Fact]
    public async Task SPEC03_Notas_SeGuardanUnaVezPorIdYSeListanConSuEtiqueta()
    {
        var g = await PrepararAsync(1);
        var id = Guid.NewGuid();
        var nota = new { id, periodoId = g.PeriodoId, texto = "Explicó el ciclo del agua a sus compañeros.", etiqueta = "fortaleza", fecha = "2026-09-02" };

        var primera = await g.Cliente.PostAsJsonAsync($"/api/v1/alumnos/{g.Alumnos[0]}/notas", nota);
        var repetida = await g.Cliente.PostAsJsonAsync($"/api/v1/alumnos/{g.Alumnos[0]}/notas", nota);
        var larga = await g.Cliente.PostAsJsonAsync($"/api/v1/alumnos/{g.Alumnos[0]}/notas",
            new { id = Guid.NewGuid(), periodoId = g.PeriodoId, texto = new string('a', 1001) });
        var etiqueta = await g.Cliente.PostAsJsonAsync($"/api/v1/alumnos/{g.Alumnos[0]}/notas",
            new { id = Guid.NewGuid(), periodoId = g.PeriodoId, texto = "Hola", etiqueta = "secreta" });
        var lista = await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/alumnos/{g.Alumnos[0]}/notas");

        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
        Assert.Equal(HttpStatusCode.Created, repetida.StatusCode);
        Assert.Equal("nota_invalida", await CodigoAsync(larga));
        Assert.Equal("nota_invalida", await CodigoAsync(etiqueta));
        var guardada = Assert.Single(lista.EnumerateArray());
        Assert.Equal(id, guardada.GetProperty("id").GetGuid());
        Assert.Equal("fortaleza", guardada.GetProperty("etiqueta").GetString());
        Assert.Equal("2026-09-02", guardada.GetProperty("fecha").GetString());
    }

    [Fact]
    public async Task RNF001_EquiposYNotasDeOtroDocente_Responden404()
    {
        var g = await PrepararAsync(1);
        var equipos = await GuardarEquiposAsync(g, ("Uno", [g.Alumnos[0]]));
        var nota = new { id = Guid.NewGuid(), periodoId = g.PeriodoId, texto = "Privada" };
        Assert.Equal(HttpStatusCode.Created, (await g.Cliente.PostAsJsonAsync($"/api/v1/alumnos/{g.Alumnos[0]}/notas", nota)).StatusCode);
        var (intruso, _, _) = await _fabrica.RegistrarDocenteVerificadoAsync();

        var respuestas = new[]
        {
            await intruso.GetAsync($"/api/v1/grupos/{g.GrupoId}/equipos"),
            await intruso.PutAsJsonAsync($"/api/v1/grupos/{g.GrupoId}/equipos", new { equipos = Array.Empty<object>() }),
            await intruso.GetAsync($"/api/v1/alumnos/{g.Alumnos[0]}/notas"),
            await intruso.PostAsJsonAsync($"/api/v1/alumnos/{g.Alumnos[0]}/notas", new { id = Guid.NewGuid(), periodoId = g.PeriodoId, texto = "x" }),
            await intruso.PostAsJsonAsync($"/api/v1/alumnos/{g.Alumnos[0]}/notas", nota), // reenviar el id de otro no la revela
        };

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        Assert.Equal(1, await ContarAsync($"SELECT count(*) FROM equipo WHERE id = '{equipos[0]}'"));
    }

    private sealed record Grupo(HttpClient Cliente, Guid DocenteId, Guid CicloId, Guid GrupoId, Guid PeriodoId, IReadOnlyList<Guid> Alumnos, JsonElement Tipos)
    {
        public Guid Tipo(string nombre) =>
            Tipos.EnumerateArray().Single(t => t.GetProperty("nombre").GetString() == nombre).GetProperty("id").GetGuid();
    }

    private sealed record RubroCreado(Guid Id, IReadOnlyList<Guid> Criterios);

    private async Task<Grupo> PrepararAsync(int alumnos)
    {
        var (cliente, docenteId, _) = await _fabrica.RegistrarDocenteVerificadoAsync();
        var ciclo = await cliente.PostAsJsonAsync("/api/v1/ciclos", new { nombre = "2026-2027", inicio = "2026-08-25", fin = "2027-07-16", tipoUnidad = "trimestre" });
        ciclo.EnsureSuccessStatusCode();
        var cicloId = (await ciclo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var grupo = await cliente.PostAsJsonAsync("/api/v1/grupos", new { nombre = "A", nivel = "secundaria", grado = 2, materia = "Ciencias", cicloId });
        grupo.EnsureSuccessStatusCode();
        var grupoId = (await grupo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var periodo = (await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/grupos/{grupoId}/periodos"))[0].GetProperty("id").GetGuid();
        var ids = new List<Guid>();
        for (var i = 1; i <= alumnos; i++)
        {
            var alta = await cliente.PostAsJsonAsync($"/api/v1/grupos/{grupoId}/alumnos", new { nombres = $"Alumno {i}", apellidos = $"Prueba {i:00}" });
            alta.EnsureSuccessStatusCode();
            ids.Add((await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        }

        var tipos = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/tipos-rubro");
        return new Grupo(cliente, docenteId, cicloId, grupoId, periodo, ids, tipos);
    }

    private static Func<Grupo, object> Exposicion(string formaUltimo, decimal minimaUltimo, decimal[]? pesos = null) => g => new
    {
        nombre = "Exposición",
        tipoRubroId = g.Tipo("Exposición"),
        peso = 100m,
        forma = "numerica",
        califMinima = 5m,
        criterios = new object[]
        {
            new { nombre = "Información", peso = pesos?[0] ?? 40m, forma = "numerica", califMinima = 5m },
            new { nombre = "Presentación", peso = pesos?[1] ?? 30m, forma = "numerica", califMinima = 5m },
            new { nombre = "Dominio del tema", peso = pesos?[2] ?? 30m, forma = formaUltimo, califMinima = minimaUltimo },
        },
    };

    private static Func<Grupo, object> Simple(string tipo, decimal peso, string? forma = null) => g => new
    {
        nombre = tipo,
        tipoRubroId = g.Tipo(tipo),
        peso,
        forma = forma ?? "numerica",
        califMinima = 5m,
    };

    /// <summary>Repite un rubro guardado con sus ids, para editarlo en lugar de crear otro.</summary>
    private static Func<Grupo, object> ConIds(RubroCreado rubro, Func<Grupo, object> plantilla, bool quitarUltimo = false) => g =>
    {
        var json = JsonSerializer.SerializeToElement(plantilla(g), JsonSerializerOptions.Web);
        var criterios = json.GetProperty("criterios").EnumerateArray()
            .Select((c, i) => new
            {
                id = rubro.Criterios[i],
                nombre = c.GetProperty("nombre").GetString(),
                peso = c.GetProperty("peso").GetDecimal(),
                forma = c.GetProperty("forma").GetString(),
                califMinima = c.GetProperty("califMinima").GetDecimal(),
            })
            .Take(quitarUltimo ? rubro.Criterios.Count - 1 : rubro.Criterios.Count)
            .ToList();
        if (quitarUltimo)
        {
            criterios = criterios.Select((c, i) => c with { peso = i == 0 ? 60m : 40m }).ToList();
        }

        return new
        {
            id = rubro.Id,
            nombre = json.GetProperty("nombre").GetString(),
            tipoRubroId = json.GetProperty("tipoRubroId").GetGuid(),
            peso = json.GetProperty("peso").GetDecimal(),
            forma = json.GetProperty("forma").GetString(),
            califMinima = json.GetProperty("califMinima").GetDecimal(),
            criterios,
        };
    };

    private static async Task<List<RubroCreado>> GuardarRubrosAsync(Grupo g, params Func<Grupo, object>[] rubros)
    {
        var respuesta = await g.Cliente.PutAsJsonAsync($"/api/v1/periodos/{g.PeriodoId}/rubros", new { rubros = rubros.Select(r => r(g)).ToList() });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rubros").EnumerateArray()
            .Select(r => new RubroCreado(
                r.GetProperty("id").GetGuid(),
                r.GetProperty("criterios").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ToList()))
            .ToList();
    }

    private static async Task<Guid> CrearActividadAsync(Grupo g, Guid rubro, string nombre, string modo = "individual")
    {
        var respuesta = await g.Cliente.PostAsJsonAsync($"/api/v1/rubros/{rubro}/actividades", new { nombre, modo });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static object Celda(Guid actividad, Guid alumno, string? valor = null, string marca = "ninguna", object[]? criterios = null, int version = 0) => new
    {
        actividadId = actividad,
        alumnoId = alumno,
        valor = valor is null ? (decimal?)null : decimal.Parse(valor, CultureInfo.InvariantCulture),
        marca,
        criterios,
        version,
    };

    private static object CeldaEquipo(Guid actividad, Guid equipo, string valor) => new
    {
        actividadId = actividad,
        alumnoId = Guid.Empty,
        equipoId = equipo,
        valor = decimal.Parse(valor, CultureInfo.InvariantCulture),
        marca = "ninguna",
        version = 0,
    };

    private static async Task<List<Guid>> GuardarEquiposAsync(Grupo g, params (string Nombre, Guid[] Alumnos)[] equipos)
    {
        var respuesta = await g.Cliente.PutAsJsonAsync($"/api/v1/grupos/{g.GrupoId}/equipos",
            new { equipos = equipos.Select(e => new { nombre = e.Nombre, alumnoIds = e.Alumnos }) });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
    }

    private static JsonElement CeldaDe(JsonElement matriz, Guid actividad, Guid alumno) =>
        Celdas(matriz).Single(c => c.GetProperty("actividadId").GetGuid() == actividad && c.GetProperty("alumnoId").GetGuid() == alumno);

    private static decimal? ValorDe(JsonElement matriz, Guid actividad, Guid alumno) =>
        Celdas(matriz).Where(c => c.GetProperty("actividadId").GetGuid() == actividad && c.GetProperty("alumnoId").GetGuid() == alumno)
            .Select(c => Valor(c, "valor")).FirstOrDefault();

    private static object[] Criterios(RubroCreado rubro, params string[] valores) =>
        valores.Select((v, i) => (object)new { criterioId = rubro.Criterios[i], valor = decimal.Parse(v, CultureInfo.InvariantCulture), marca = "ninguna" }).ToArray();

    private static async Task<JsonElement> GuardarAsync(Grupo g, params object[] celdas)
    {
        var respuesta = await g.Cliente.PutAsJsonAsync("/api/v1/calificaciones", new { celdas });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> GuardarConClaveAsync(Grupo g, Guid clave, params object[] celdas)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Put, "/api/v1/calificaciones") { Content = JsonContent.Create(new { celdas }) };
        solicitud.Headers.Add("Idempotency-Key", clave.ToString());
        var respuesta = await g.Cliente.SendAsync(solicitud);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> SincronizarAsync(Grupo g, object cambios)
    {
        var respuesta = await g.Cliente.PostAsJsonAsync("/api/v1/sincronizacion", new { cambios });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> MatrizAsync(Grupo g) =>
        await g.Cliente.GetFromJsonAsync<JsonElement>($"/api/v1/periodos/{g.PeriodoId}/calificaciones");

    private static JsonElement.ArrayEnumerator Celdas(JsonElement matriz) => matriz.GetProperty("celdas").EnumerateArray();

    private static decimal? Valor(JsonElement celda, string propiedad) =>
        celda.GetProperty(propiedad).ValueKind == JsonValueKind.Null ? null : celda.GetProperty(propiedad).GetDecimal();

    private static JsonElement Resultado(JsonElement matriz, Guid alumno) =>
        matriz.GetProperty("resultados").EnumerateArray().Single(r => r.GetProperty("alumnoId").GetGuid() == alumno);

    private static decimal? PromedioRubro(JsonElement matriz, Guid alumno, Guid rubro) =>
        Valor(Resultado(matriz, alumno).GetProperty("rubros").EnumerateArray().Single(r => r.GetProperty("rubroId").GetGuid() == rubro), "promedio");

    private async Task EjecutarSqlAsync(string sql)
    {
        await using var conexion = new NpgsqlConnection(baseDeDatos.ConexionAdmin);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    private async Task<long> ContarAsync(string sql)
    {
        await using var conexion = new NpgsqlConnection(baseDeDatos.ConexionAdmin);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        return (long)(await comando.ExecuteScalarAsync())!;
    }

    private static async Task<string?> CodigoAsync(HttpResponseMessage respuesta)
    {
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.TryGetProperty("codigo", out var codigo) ? codigo.GetString() : null;
    }
}
