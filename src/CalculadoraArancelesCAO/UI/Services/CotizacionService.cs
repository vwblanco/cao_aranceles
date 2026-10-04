using System.Globalization;
using System.Text.Json;
using CalculadoraArancelesCAO.Core.Models;
using CalculadoraArancelesCAO.Core.Services;

namespace CalculadoraArancelesCAO.UI.Services;

public sealed class CotizacionService
{
    private const string ClaveContador = "cao.cotizacion.contador";
    private const string ClaveSolicitante = "cao.cotizacion.solicitante";

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        WriteIndented = false
    };

    private readonly ILocalStorageService _storage;
    private int _contador;

    public CotizacionService(ILocalStorageService storage) => _storage = storage;

    public DatosSolicitante Solicitante { get; private set; } = new();

    public int ContadorActual => _contador;

    public async Task CargarAsync()
    {
        _contador = await LeerContadorAsync();
        Solicitante = await LeerSolicitanteAsync();
    }

    public async Task GuardarSolicitanteAsync(DatosSolicitante datos)
    {
        Solicitante = datos;

        try
        {
            await _storage.SetItemAsync(ClaveSolicitante, JsonSerializer.Serialize(datos, OpcionesJson));
        }
        catch (Exception)
        {
        }
    }

    public async Task<ReporteCotizacion> GenerarAsync(
        ResultadoArancel resultado,
        ParametrosCAO parametros,
        string profesional = "",
        string matriculaProfesional = "",
        string observaciones = "")
    {
        _contador++;
        await _storage.SetItemAsync(ClaveContador, _contador.ToString(CultureInfo.InvariantCulture));

        return new ReporteCotizacion
        {
            Numero = GenerarNumero(_contador),
            Fecha = DateTimeOffset.Now,
            Resultado = resultado,
            Solicitante = Solicitante,
            Parametros = parametros,
            Profesional = profesional,
            MatriculaProfesional = matriculaProfesional,
            Observaciones = observaciones
        };
    }

    public static string GenerarNumero(int contador) =>
        $"CAO-{DateTime.Now:yyyy}-{contador:00000}";

    private async Task<int> LeerContadorAsync()
    {
        var texto = await _storage.GetItemAsync(ClaveContador);

        return int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) && valor >= 0
            ? valor
            : 0;
    }

    private async Task<DatosSolicitante> LeerSolicitanteAsync()
    {
        var texto = await _storage.GetItemAsync(ClaveSolicitante);

        if (string.IsNullOrWhiteSpace(texto))
            return new DatosSolicitante();

        try
        {
            return JsonSerializer.Deserialize<DatosSolicitante>(texto, OpcionesJson) ?? new DatosSolicitante();
        }
        catch (JsonException)
        {
            return new DatosSolicitante();
        }
    }
}
