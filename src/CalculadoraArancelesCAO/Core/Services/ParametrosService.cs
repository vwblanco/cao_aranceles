using CalculadoraArancelesCAO.Core.Models;
using Microsoft.Extensions.Logging;

namespace CalculadoraArancelesCAO.Core.Services;

/// <summary>
/// Mantiene los parametros economicos en memoria y los persiste en el almacenamiento
/// local del navegador para sobrevivir a la actualizacion de la Variable Vida.
/// </summary>
public sealed class ParametrosService
{
    private const string ClaveVariableVida = "cao.parametros.variableVida";
    private const string ClaveHorasMensuales = "cao.parametros.horasMensuales";

    private readonly ILocalStorageService _storage;
    private readonly ILogger<ParametrosService> _logger;

    public ParametrosService(ILocalStorageService storage, ILogger<ParametrosService> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public ParametrosCAO Actual { get; private set; } = ParametrosCAO.PorDefecto();

    public event Action? Cambiado;

    public async Task CargarAsync()
    {
        try
        {
            var variableVida = await LeerAsync(ClaveVariableVida, ParametrosCAO.VariableVidaPorDefecto);
            var horas = await LeerAsync(ClaveHorasMensuales, ParametrosCAO.HorasMensualesPorDefecto);

            var parametros = new ParametrosCAO { VariableVida = variableVida, HorasMensuales = horas };

            if (parametros.Validar().Count > 0)
            {
                _logger.LogWarning("Parametros persistidos invalidos. Se restauran los valores oficiales.");
                parametros.RestaurarValoresOficiales();
            }

            Actual = parametros;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No fue posible leer los parametros persistidos.");
            Actual = ParametrosCAO.PorDefecto();
        }

        Notificar();
    }

    public async Task ActualizarAsync(double variableVida, double horasMensuales)
    {
        var candidato = new ParametrosCAO { VariableVida = variableVida, HorasMensuales = horasMensuales };
        var errores = candidato.Validar();

        if (errores.Count > 0)
            throw new ArgumentException(string.Join(" ", errores));

        Actual = candidato;

        try
        {
            await _storage.SetItemAsync(ClaveVariableVida, Formatear(variableVida));
            await _storage.SetItemAsync(ClaveHorasMensuales, Formatear(horasMensuales));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No fue posible persistir los parametros. El cambio queda solo en memoria.");
        }

        Notificar();
    }

    public async Task RestaurarValoresOficialesAsync()
    {
        Actual.RestaurarValoresOficiales();

        try
        {
            await _storage.RemoveItemAsync(ClaveVariableVida);
            await _storage.RemoveItemAsync(ClaveHorasMensuales);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No fue posible limpiar los parametros persistidos.");
        }

        Notificar();
    }

    public IDisposable Suscribir(Action handler)
    {
        Cambiado += handler;
        return new Suscripcion(() => Cambiado -= handler);
    }

    private async Task<double> LeerAsync(string clave, double porDefecto)
    {
        var texto = await _storage.GetItemAsync(clave);

        return double.TryParse(texto, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var valor) && valor > 0
            ? valor
            : porDefecto;
    }

    private static string Formatear(double valor) =>
        valor.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    private void Notificar() => Cambiado?.Invoke();

private sealed class Suscripcion(Action disposing) : IDisposable
{
    public void Dispose() => disposing();
}
}
