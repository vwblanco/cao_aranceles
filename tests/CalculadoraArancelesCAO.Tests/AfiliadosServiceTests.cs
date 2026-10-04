using CalculadoraArancelesCAO.UI.Services;
using OfficeOpenXml;

namespace CalculadoraArancelesCAO.Tests;

public sealed class AfiliadosServiceTests
{
    private sealed class Almacenamiento : Core.Services.ILocalStorageService
    {
        public Dictionary<string, string> Datos { get; } = new(StringComparer.Ordinal);

        public Task<string?> GetItemAsync(string clave) =>
            Task.FromResult(Datos.TryGetValue(clave, out var v) ? v : null);

        public Task<T?> GetItemAsync<T>(string clave) => Task.FromResult<T?>(default);

        public Task SetItemAsync(string clave, string valor)
        {
            Datos[clave] = valor;
            return Task.CompletedTask;
        }

        public Task RemoveItemAsync(string clave)
        {
            Datos.Remove(clave);
            return Task.CompletedTask;
        }
    }

    private static MemoryStream ExcelValido()
    {
        using var paquete = new ExcelPackage();
        var hoja = paquete.Workbook.Worksheets.Add("Afiliados");

        hoja.Cells[1, 1].Value = "Numero correlativo";
        hoja.Cells[1, 2].Value = "Registro nacional";
        hoja.Cells[1, 3].Value = "Apellidos y nombres";
        hoja.Cells[1, 4].Value = "Cedula identidad";

        hoja.Cells[2, 1].Value = 1;
        hoja.Cells[2, 2].Value = "12345";
        hoja.Cells[2, 3].Value = "BLANCO VICTOR";
        hoja.Cells[2, 4].Value = "1234567 LP";

        hoja.Cells[3, 1].Value = 2;
        hoja.Cells[3, 2].Value = "54321";
        hoja.Cells[3, 3].Value = "PEREZ JUAN CARLOS";
        hoja.Cells[3, 4].Value = "7654321 CB";

        var salida = new MemoryStream();
        paquete.SaveAs(salida);
        salida.Position = 0;
        return salida;
    }

    /// <summary>
    /// Reproduce los encabezados reales de "Lista APP.xlsx" del Colegio:
    /// N | Nº REG. | PATERNO | C.I.
    /// </summary>
    private static MemoryStream ExcelEstiloColegio()
    {
        using var paquete = new ExcelPackage();
        var hoja = paquete.Workbook.Worksheets.Add("Hoja2");

        hoja.Cells[1, 1].Value = "N";
        hoja.Cells[1, 2].Value = "Nº REG.";
        hoja.Cells[1, 3].Value = "PATERNO";
        hoja.Cells[1, 4].Value = "C.I.";

        hoja.Cells[2, 1].Value = 1;
        hoja.Cells[2, 2].Value = 10762;
        hoja.Cells[2, 3].Value = "Abasto Rojas Juan Carlos";
        hoja.Cells[2, 4].Value = 3106161;

        hoja.Cells[3, 1].Value = 2;
        hoja.Cells[3, 2].Value = 174;
        hoja.Cells[3, 3].Value = "Mendizabal Jara Luis";
        hoja.Cells[3, 4].Value = null;

        var salida = new MemoryStream();
        paquete.SaveAs(salida);
        salida.Position = 0;
        return salida;
    }

    [Fact]
    public async Task CargarDesdeExcel_ReconoceLasCuatroColumnasDelColegio()
    {
        var servicio = new AfiliadosService(new Almacenamiento());

        var resultado = await servicio.CargarDesdeExcelAsync(ExcelValido());

        Assert.True(resultado.Exitoso, resultado.Mensaje);
        Assert.Equal(2, resultado.Afiliados.Count);

        var primero = resultado.Afiliados[0];
        Assert.Equal("1", primero.Correlativo);
        Assert.Equal("12345", primero.NumeroRegistro);
        Assert.Equal("BLANCO VICTOR", primero.Nombre);
        Assert.Equal("1234567 LP", primero.CI);
        Assert.True(primero.Vigente);
    }

    [Fact]
    public async Task CargarDesdeExcel_PersisteLaListaEnAlmacenamiento()
    {
        var almacenamiento = new Almacenamiento();
        var servicio = new AfiliadosService(almacenamiento);

        await servicio.CargarDesdeExcelAsync(ExcelValido());

        Assert.Contains("cao.afiliados.lista", almacenamiento.Datos.Keys);
        Assert.Equal(2, (await servicio.ObtenerAfiliadosAsync()).Count);
    }

    [Fact]
    public async Task CargarDesdeExcel_SinColumnasRequeridas_InformaCualesFaltan()
    {
        using var paquete = new ExcelPackage();
        var hoja = paquete.Workbook.Worksheets.Add("X");
        hoja.Cells[1, 1].Value = "Columna sin sentido";
        hoja.Cells[2, 1].Value = "dato";

        var salida = new MemoryStream();
        paquete.SaveAs(salida);
        salida.Position = 0;

        var servicio = new AfiliadosService(new Almacenamiento());
        var resultado = await servicio.CargarDesdeExcelAsync(salida);

        Assert.False(resultado.Exitoso);
        Assert.Contains("Registro nacional", resultado.Mensaje);
        Assert.Contains("Apellidos y nombres", resultado.Mensaje);
        Assert.Contains("Cedula identidad", resultado.Mensaje);
    }

    [Fact]
    public async Task BuscarAfiliado_EncuentraPorRegistroYCi()
    {
        var servicio = new AfiliadosService(new Almacenamiento());
        await servicio.CargarDesdeExcelAsync(ExcelValido());

        var encontrado = await servicio.BuscarAfiliadoAsync("12345", "1234567 lp");

        Assert.NotNull(encontrado);
        Assert.Equal("BLANCO VICTOR", encontrado!.Nombre);
    }

    [Fact]
    public async Task BuscarAfiliado_SinListaDevuelveNull()
    {
        var servicio = new AfiliadosService(new Almacenamiento());

        Assert.Null(await servicio.BuscarAfiliadoAsync("12345", "1234567 LP"));
        Assert.False(await servicio.ValidarAccesoAsync("12345", "1234567 LP"));
    }

    [Fact]
    public async Task BuscarAfiliado_CiInexistenteDevuelveNull()
    {
        var servicio = new AfiliadosService(new Almacenamiento());
        await servicio.CargarDesdeExcelAsync(ExcelValido());

        Assert.Null(await servicio.BuscarAfiliadoAsync("12345", "9999999 XX"));
    }

    [Fact]
    public async Task LimpiarAsync_EliminaLaListaPersistida()
    {
        var almacenamiento = new Almacenamiento();
        var servicio = new AfiliadosService(almacenamiento);
        await servicio.CargarDesdeExcelAsync(ExcelValido());

        await servicio.LimpiarAsync();

        Assert.Empty(almacenamiento.Datos);
        Assert.Empty(await servicio.ObtenerAfiliadosAsync());
    }

    [Fact]
    public async Task EncabezadosRealesDelColegio_SonDetectados()
    {
        var servicio = new AfiliadosService(new Almacenamiento());

        var resultado = await servicio.CargarDesdeExcelAsync(ExcelEstiloColegio());

        Assert.True(resultado.Exitoso, resultado.Mensaje);
        Assert.Equal(2, resultado.Afiliados.Count);
        Assert.Equal("10762", resultado.Afiliados[0].NumeroRegistro);
        Assert.Equal("3106161", resultado.Afiliados[0].CI);
        Assert.Equal("1", resultado.Afiliados[0].Correlativo);
    }

    [Fact]
    public async Task AfiliadoSinCedulaEnElArchivo_SeCargaYCuentaEnElAviso()
    {
        var servicio = new AfiliadosService(new Almacenamiento());

        var resultado = await servicio.CargarDesdeExcelAsync(ExcelEstiloColegio());

        Assert.Equal(2, resultado.Afiliados.Count);
        Assert.Equal(1, resultado.SinCedula);
        Assert.False(resultado.Afiliados[1].TieneCedula);
        Assert.True(resultado.Afiliados[0].TieneCedula);
    }

    [Fact]
    public async Task ValidarAcceso_AfiliadoSinCedulaIngresaConRegistroYNombre()
    {
        var servicio = new AfiliadosService(new Almacenamiento());
        await servicio.CargarDesdeExcelAsync(ExcelEstiloColegio());

        var ok = await servicio.ValidarAccesoAsync("174", string.Empty, "Mendizabal Jara Luis");
        Assert.True(ok.Exitoso, ok.Mensaje);

        var nombreIncorrecto = await servicio.ValidarAccesoAsync("174", string.Empty, "Otra Persona");
        Assert.False(nombreIncorrecto.Exitoso);
        Assert.Contains("no coincide", nombreIncorrecto.Mensaje);
    }

    [Fact]
    public async Task ValidarAcceso_AfiliadoConCedula_RequiereCiSiFaltaEnLogin()
    {
        var servicio = new AfiliadosService(new Almacenamiento());
        await servicio.CargarDesdeExcelAsync(ExcelEstiloColegio());

        // Afiliado con CI en la lista: si no se proveyó CI en el login,
        // se permite el acceso (Exitoso=true) pero se indica que falta CI (RequiereCi=true).
        var sinCi = await servicio.ValidarAccesoAsync("10762", string.Empty, "Abasto Rojas Juan Carlos");
        Assert.True(sinCi.Exitoso, sinCi.Mensaje);
        Assert.True(sinCi.RequiereCi);
        Assert.Contains("cedula", sinCi.Mensaje);

        // CI equivocado → rechazo total
        var ciEquivocado = await servicio.ValidarAccesoAsync("10762", "9999999", "Abasto Rojas Juan Carlos");
        Assert.False(ciEquivocado.Exitoso);

        // CI correcto → acceso completo sin requerir nada más
        var correcto = await servicio.ValidarAccesoAsync("10762", "3106161", "Abasto Rojas Juan Carlos");
        Assert.True(correcto.Exitoso, correcto.Mensaje);
        Assert.False(correcto.RequiereCi);
    }

    [Fact]
    public async Task ValidarAcceso_RegistroInexistente_DaMensajeEspecifico()
    {
        var servicio = new AfiliadosService(new Almacenamiento());
        await servicio.CargarDesdeExcelAsync(ExcelEstiloColegio());

        var resultado = await servicio.ValidarAccesoAsync("999999", "1234567", "Quien Sea");

        Assert.False(resultado.Exitoso);
        Assert.Contains("no figura en la lista", resultado.Mensaje);
    }

    [Fact]
    public async Task ValidarAcceso_SinListaCargada_AvisarContactarAlColegio()
    {
        var servicio = new AfiliadosService(new Almacenamiento());

        var resultado = await servicio.ValidarAccesoAsync("12345", "1234567", "BLANCO VICTOR");

        Assert.False(resultado.Exitoso);
        Assert.Contains("No hay lista de afiliados", resultado.Mensaje);
    }

    [Theory]
    [InlineData("BLANCO VICTOR", "Blanco Victor")]
    [InlineData("BLANCO VICTOR", "VICTOR BLANCO")]
    [InlineData("BLANCO VICTOR", "blanco  victor")]
    public void NombresAdmitenVariacionesDeMayusculasYOrden(string registrado, string ingressado)
    {
        var servicio = new AfiliadosService(new Almacenamiento());
        var almacen = (Almacenamiento)typeof(AfiliadosService)
            .GetField("_localStorage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(servicio)!;

        almacen.Datos["cao.afiliados.lista"] =
            System.Text.Json.JsonSerializer.Serialize(new List<Afiliado>
            {
                new() { NumeroRegistro = "1", Nombre = registrado, CI = "1234567" }
            });

        var validacion = servicio.ValidarAccesoAsync("1", "1234567", ingressado).GetAwaiter().GetResult();

        Assert.True(validacion.Exitoso, validacion.Mensaje);
    }
}
