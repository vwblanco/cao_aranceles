using CalculadoraArancelesCAO.UI.Services;

namespace CalculadoraArancelesCAO.Tests;

/// <summary>
/// Prueba de integracion contra el archivo real del Colegio.
/// Si el archivo no esta disponible, la prueba se omite automaticamente.
/// </summary>
public sealed class ListaRealColegioTests
{
    private const string RutaPorDefecto =
        @"D:\Arq. Victor Blanco\Documentos VW\CAO\Lista APP.xlsx";

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

    private static string? ResolverRuta()
    {
        var ruta = Environment.GetEnvironmentVariable("CAO_LISTA_AFILIADOS") ?? RutaPorDefecto;
        return File.Exists(ruta) ? ruta : null;
    }

    [Fact]
    public async Task ListaRealDelColegio_SeCargaCompleta()
    {
        var ruta = ResolverRuta();
        if (ruta is null)
        {
            return;
        }

        var servicio = new AfiliadosService(new Almacenamiento(), HttpDePrueba.SinIndice());
        await using var stream = File.OpenRead(ruta);

        var resultado = await servicio.CargarDesdeExcelAsync(stream);

        Assert.True(resultado.Exitoso, resultado.Mensaje);
        Assert.True(resultado.Afiliados.Count > 600,
            $"Se esperaban mas de 600 afiliados, se obtuvieron {resultado.Afiliados.Count}.");

        Assert.All(resultado.Afiliados, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.NumeroRegistro));
            Assert.False(string.IsNullOrWhiteSpace(a.Nombre));
        });

        // La lista real incluye afiliados sin CI: no deben descartarse.
        Assert.Contains(resultado.Afiliados, a => !a.TieneCedula);
        Assert.Contains(resultado.Afiliados, a => a.TieneCedula);

        // El total cargado debe coincidir con las filas de datos del archivo.
        Assert.True(resultado.TotalOmmitidas <= 5,
            $"Se omitieron {resultado.TotalOmmitidas} filas, se esperaban muy pocas.");
    }

    [Fact]
    public async Task ListaRealDelColegio_LosAfiliadosConCiRequierenElCiCorrecto()
    {
        var ruta = ResolverRuta();
        if (ruta is null)
        {
            return;
        }

        var servicio = new AfiliadosService(new Almacenamiento(), HttpDePrueba.SinIndice());
        await using var stream = File.OpenRead(ruta);
        await servicio.CargarDesdeExcelAsync(stream);

        var lista = await servicio.ObtenerAfiliadosAsync();
        var conCi = lista.First(a => a.TieneCedula);
        var sinCi = lista.First(a => !a.TieneCedula);

        var accesoConCi = await servicio.ValidarAccesoAsync(
            conCi.NumeroRegistro, conCi.CI, conCi.Nombre);
        Assert.True(accesoConCi.Exitoso, accesoConCi.Mensaje);

        var accesoSinCi = await servicio.ValidarAccesoAsync(
            sinCi.NumeroRegistro, string.Empty, sinCi.Nombre);
        Assert.True(accesoSinCi.Exitoso, accesoSinCi.Mensaje);
    }

    [Fact]
    public async Task ListaRealDelColegio_RechazaUnRegistroInventado()
    {
        var ruta = ResolverRuta();
        if (ruta is null)
        {
            return;
        }

        var servicio = new AfiliadosService(new Almacenamiento(), HttpDePrueba.SinIndice());
        await using var stream = File.OpenRead(ruta);
        await servicio.CargarDesdeExcelAsync(stream);

        var resultado = await servicio.ValidarAccesoAsync("99999999", "9999999", "Persona Inventada");

        Assert.False(resultado.Exitoso);
    }
}
