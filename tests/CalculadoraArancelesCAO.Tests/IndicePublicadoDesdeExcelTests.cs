using CalculadoraArancelesCAO.Core.Services;
using CalculadoraArancelesCAO.UI.Services;

namespace CalculadoraArancelesCAO.Tests;

/// <summary>
/// Comprueba el escenario real de una instalacion nueva del APK: sin lista local en
/// el dispositivo, el afiliado debe poder ingresar usando unicamente el indice
/// publicado. Si este test falla, los colegiados que instalan la app nueva no pueden
/// entrar.
/// </summary>
public sealed class IndicePublicadoDesdeExcelTests
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

    private static string? ResolverExcel()
    {
        var ruta = Environment.GetEnvironmentVariable("CAO_LISTA_AFILIADOS") ?? RutaPorDefecto;
        return File.Exists(ruta) ? ruta : null;
    }

    private static string ResolverIndice()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            var candidatos = Path.Combine(
                directorio.FullName, "src", "CalculadoraArancelesCAO", "wwwroot", "data", "indice-acceso.json");

            if (File.Exists(candidatos))
            {
                return candidatos;
            }

            directorio = directorio.Parent;
        }

        throw new FileNotFoundException("No se encontro wwwroot/data/indice-acceso.json.");
    }

    [Fact]
    public async Task ElIndicePermiteAutenticarSinTenerLaListaLocal()
    {
        var excel = ResolverExcel();
        if (excel is null)
        {
            return;
        }

        // El indice tal como viaja en la app.
        var indice = IndiceAcceso.Deserializar(await File.ReadAllTextAsync(ResolverIndice()));
        Assert.NotNull(indice);
        Assert.True(indice!.Total > 600, $"El indice solo tiene {indice.Total} entradas.");

        // La lista real, solo para saber contra quien comparar.
        var servicio = new AfiliadosService(new Almacenamiento(), HttpDePrueba.SinIndice());
        await using (var stream = File.OpenRead(excel))
        {
            await servicio.CargarDesdeExcelAsync(stream);
        }

        var lista = await servicio.ObtenerAfiliadosAsync();

        // Todos los afiliados del Excel deben tener su entrada en el indice.
        var sinEntrada = lista
            .Where(a => !indice.Entradas.Any(e => e.R == IndiceAcceso.NormalizarRegistro(a.NumeroRegistro)))
            .ToList();

        Assert.True(sinEntrada.Count == 0,
            $"{sinEntrada.Count} afiliados del Excel no tienen entrada en el indice.");

        // Muestra de 40 afiliados: cada uno debe entrar con su credencial.
        var muestra = lista.OrderBy(a => a.NumeroRegistro).Take(40).ToList();

        foreach (var afiliado in muestra)
        {
            if (afiliado.TieneCedula)
            {
                Assert.True(IndiceAcceso.Coincide(indice, afiliado.NumeroRegistro, afiliado.CI, null),
                    $"El registro {afiliado.NumeroRegistro} no entra con su CI.");
            }
            else
            {
                Assert.True(IndiceAcceso.Coincide(indice, afiliado.NumeroRegistro, string.Empty, afiliado.Nombre),
                    $"El registro {afiliado.NumeroRegistro} (sin CI) no entra con su nombre.");
            }
        }

        // Y un registro inventado debe seguir siendo rechazado.
        Assert.False(IndiceAcceso.Coincide(indice, "99999999", "9999999", "Persona Inventada"));
    }

    [Fact]
    public void ElIndicePublicadoNoExponeDatosPersonales()
    {
        var indice = IndiceAcceso.Deserializar(File.ReadAllText(ResolverIndice()));

        Assert.NotNull(indice);

        // Cada entrada debe tener solo registro, marca de CI y hash.
        Assert.All(indice!.Entradas, e =>
        {
            Assert.Matches("^[0-9]+$", e.R);
            Assert.Matches("^[0-9a-f]{64}$", e.H);
        });

        // Ningun nombre de la muestra debe aparecer en claro en el archivo.
        var json = File.ReadAllText(ResolverIndice());
        var nombres = Environment.GetEnvironmentVariable("CAO_VERIFICAR_NOMBRES");

        if (!string.IsNullOrWhiteSpace(nombres))
        {
            foreach (var nombre in nombres.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                Assert.DoesNotContain(nombre.Trim(), json, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}