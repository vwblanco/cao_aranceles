using System.Text.Json;
using CalculadoraArancelesCAO.Core.Services;
using CalculadoraArancelesCAO.UI.Services;

namespace CalculadoraArancelesCAO.Tests;

/// <summary>
/// Regenera <c>src/CalculadoraArancelesCAO/wwwroot/data/indice-acceso.json</c> a
/// partir del Excel real del Colegio.
///
/// No se ejecuta en la pasada normal de pruebas: solo corre si se define
/// <c>CAO_GENERAR_INDICE=1</c>. Ese archivo se versiona en el repositorio y viaja
/// dentro de cada instalacion nueva, asi que hay que regenerarlo cada vez que el
/// Colegio incorpora o da de baja a un afiliado.
///
///   $env:CAO_GENERAR_INDICE=1; dotnet test --filter GenerarIndice
/// </summary>
public sealed class GenerarIndiceDesdeListaReal
{
    private const string RutaPorDefecto =
        @"D:\Arq. Victor Blanco\Documentos VW\CAO\Lista APP.xlsx";

    private sealed class Almacenamiento : ILocalStorageService
    {
        public Dictionary<string, string> Datos { get; } = new(StringComparer.Ordinal);

        public Task<string?> GetItemAsync(string clave)
            => Task.FromResult(Datos.TryGetValue(clave, out var v) ? v : null);

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

    [Fact]
    public async Task GenerarIndice()
    {
        if (Environment.GetEnvironmentVariable("CAO_GENERAR_INDICE") != "1")
        {
            return;
        }

        var excel = Environment.GetEnvironmentVariable("CAO_LISTA_AFILIADOS") ?? RutaPorDefecto;
        Assert.True(File.Exists(excel), $"No se encontro el Excel: {excel}");

        var servicio = new AfiliadosService(new Almacenamiento(), HttpDePrueba.SinIndice());
        await using (var stream = File.OpenRead(excel))
        {
            var carga = await servicio.CargarDesdeExcelAsync(stream);
            Assert.True(carga.Exitoso, carga.Mensaje);
        }

        var (exitoso, json, total) = await servicio.GenerarIndiceAsync();
        Assert.True(exitoso, "La lista quedo vacia: no se genero el indice.");

        var destino = Path.Combine(
            LocalizarRaizDelRepositorio(),
            "src", "CalculadoraArancelesCAO", "wwwroot", "data", "indice-acceso.json");

        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        await File.WriteAllTextAsync(destino, json);

        // Comprobaciones minimas sobre lo recien escrito.
        var releido = IndiceAcceso.Deserializar(await File.ReadAllTextAsync(destino));
        Assert.NotNull(releido);
        Assert.Equal(total, releido!.Total);
        Assert.Equal(IndiceAcceso.VersionFormato, releido.Version);
        Assert.DoesNotContain("PEREZ", await File.ReadAllTextAsync(destino), StringComparison.OrdinalIgnoreCase);

        var conCi = releido.Entradas.Count(e => e.C);
        Console.WriteLine(
            $"Indice generado: {total} afiliados ({conCi} con CI, {total - conCi} por nombre). " +
            $"Fecha: {releido.Generado}. Ruta: {destino}");

        Assert.False(string.IsNullOrWhiteSpace(JsonSerializer.Serialize(releido)));
    }

    private static string LocalizarRaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            if (directorio.GetFiles("*.slnx").Length > 0
                || directorio.GetFiles("*.sln").Length > 0)
            {
                return directorio.FullName;
            }

            directorio = directorio.Parent;
        }

        throw new InvalidOperationException(
            "No se encontro la raiz del repositorio (ningun .slnx o .sln por encima del binario).");
    }
}