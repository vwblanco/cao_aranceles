using System.Globalization;

namespace CalculadoraArancelesCAO.Core.Models;

public static class FormatoBoliviano
{
    public static readonly CultureInfo Culture = new("es-BO");

    public static string Moneda(double valor) =>
        string.Create(Culture, $"Bs {valor:N2}");

    public static string Decimal(double valor, int decimales) =>
        valor.ToString("N" + decimales.ToString(CultureInfo.InvariantCulture), Culture);

    public static string Numero(double valor) =>
        valor.ToString("N2", Culture);

    public static string Factor(double valor) =>
        valor.ToString("0.0000", Culture);

    public static string Porcentaje(double valor) =>
        valor.ToString("0.##%", Culture);

    /// <summary>
    /// Interpreta un número escrito por el usuario. Acepta indistintamente el separador
    /// decimal boliviano (coma) y el angosto (punto), e ignora los separadores de miles.
    /// </summary>
    public static double NumeroParseado(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return 0;

        var limpio = new string(texto.Where(c => char.IsDigit(c) || c is '.' or ',' or '-').ToArray());

        if (limpio.Length == 0)
            return 0;

        var ultimoPunto = limpio.LastIndexOf('.');
        var ultimaComa = limpio.LastIndexOf(',');
        var decimalPositivo = Math.Max(ultimoPunto, ultimaComa);

        if (decimalPositivo < 0)
        {
            limpio = limpio.Replace(".", string.Empty);
        }
        else
        {
            var parteEntera = limpio[..decimalPositivo].Replace(".", string.Empty).Replace(",", string.Empty);
            var parteDecimal = limpio[(decimalPositivo + 1)..].Replace(".", string.Empty).Replace(",", string.Empty);
            limpio = parteDecimal.Length > 0 ? $"{parteEntera}.{parteDecimal}" : parteEntera;
        }

        return double.TryParse(limpio, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : 0;
    }
}
