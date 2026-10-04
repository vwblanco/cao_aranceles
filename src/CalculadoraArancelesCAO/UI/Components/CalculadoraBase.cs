using CalculadoraArancelesCAO.Core.Models;
using CalculadoraArancelesCAO.Core.Services;
using Microsoft.AspNetCore.Components;

namespace CalculadoraArancelesCAO.UI.Components;

/// <summary>
/// Base compartida por las pantallas de cálculo: centraliza el acceso a los servicios,
/// el recálculo reactivo ante cambios de la Variable Vida y el manejo de errores de entrada.
/// </summary>
public abstract class CalculadoraBase : ComponentBase, IDisposable
{
    [Inject] protected ArancelEngine Motor { get; set; } = default!;

    [Inject] protected MatrixRepository Matrices { get; set; } = default!;

    [Inject] protected ParametrosService Parametros { get; set; } = default!;

    private IDisposable? _suscripcion;

    protected ResultadoArancel? Resultado { get; set; }

    protected string? Error { get; private set; }

    protected ParametrosCAO P => Parametros.Actual;

    protected override void OnInitialized() =>
        _suscripcion = Parametros.Suscribir(() => InvokeAsync(() =>
        {
            Recalcular();
            StateHasChanged();
        }));

    protected abstract void Calcular();

    protected void Recalcular()
    {
        try
        {
            Error = null;
            Calcular();
        }
        catch (Exception ex)
        {
            Resultado = null;
            Error = ex.Message;
        }
    }

    protected static double Leer(string? texto) => FormatoBoliviano.NumeroParseado(texto);

    public void Dispose()
    {
        _suscripcion?.Dispose();
        GC.SuppressFinalize(this);
    }
}
