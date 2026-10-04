using CalculadoraArancelesCAO.Core.Models;
using CalculadoraArancelesCAO.Core.Services;
using Microsoft.JSInterop;

namespace CalculadoraArancelesCAO.UI.Services;

public sealed class PdfService
{
    private readonly IJSRuntime _js;

    public PdfService(IJSRuntime js) => _js = js;

    public string ConstruirHtml(ReporteCotizacion cotizacion) => ReporteHtmlBuilder.Construir(cotizacion);

    public async Task ExportarAsync(ReporteCotizacion cotizacion)
    {
        var html = ConstruirHtml(cotizacion);

        try
        {
            await _js.InvokeVoidAsync("caoPdf.generar", html, NombreArchivo(cotizacion));
        }
        catch (JSException)
        {
            await _js.InvokeVoidAsync("caoPdf.imprimir", html);
        }
    }

    public async Task ImprimirAsync(ReporteCotizacion cotizacion) =>
        await _js.InvokeVoidAsync("caoPdf.imprimir", ConstruirHtml(cotizacion));

    public async Task<bool> LibreriaDisponibleAsync()
    {
        try
        {
            return await _js.InvokeAsync<bool>("caoPdf.disponible");
        }
        catch (JSException)
        {
            return false;
        }
    }

    public static string NombreArchivo(ReporteCotizacion cotizacion)
    {
        var numero = string.IsNullOrWhiteSpace(cotizacion.Numero) ? "cotizacion" : cotizacion.Numero;
        var seguro = string.Join("-", numero.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

        return $"Cotizacion-CAO-{seguro}-{cotizacion.Fecha:yyyyMMdd}.pdf";
    }
}
