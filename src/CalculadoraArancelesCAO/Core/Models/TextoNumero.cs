using System.Globalization;
using System.Text;

namespace CalculadoraArancelesCAO.Core.Models;

public static class TextoNumero
{
    private static readonly string[] Units =
    [
        "", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve",
        "diez", "once", "doce", "trece", "catorce", "quince", "dieciséis", "diecisiete",
        "dieciocho", "diecinueve", "veinte"
    ];

    private static readonly string[] Decenas =
    [
        "", "", "veinte", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta",
        "ochenta", "noventa"
    ];

    private static readonly string[] Centenas =
    [
        "", "ciento", "doscientos", "trescientos", "cuatrocientos", "quinientos",
        "seiscientos", "setecientos", "ochocientos", "novecientos"
    ];

    public static string EnLetras(double monto)
    {
        if (double.IsNaN(monto) || double.IsInfinity(monto))
            return string.Empty;

        var redondeado = Math.Round(Math.Abs(monto), 2, MidpointRounding.AwayFromZero);
        var entero = (long)Math.Floor(redondeado);
        var centavos = (long)Math.Round((redondeado - entero) * 100.0, MidpointRounding.AwayFromZero);

        if (centavos == 100)
        {
            entero += 1;
            centavos = 0;
        }

        var sb = new StringBuilder();
        sb.Append(Capitalizar(EnteroEnLetras(entero)));

        if (centavos > 0)
        {
            sb.Append(" con ");
            sb.Append(centavos.ToString("00", CultureInfo.InvariantCulture));
            sb.Append("/100");
        }

        return sb.ToString();
    }

    public static string EnteroEnLetras(long valor)
    {
        if (valor < 0)
            return "menos " + EnteroEnLetras(Math.Abs(valor));

        if (valor == 0)
            return "cero";

        if (valor >= 1_000_000_000)
            return $"{EnteroEnLetras(valor / 1_000_000_000)} mil millones{Residuo(valor % 1_000_000_000)}";

        if (valor >= 1_000_000)
        {
            var millones = valor / 1_000_000;
            var prefijo = millones == 1 ? "un millón" : $"{EnteroEnLetras(millones)} millones";
            return prefijo + Residuo(valor % 1_000_000);
        }

        if (valor >= 1000)
            return $"{ResiduoMil(valor / 1000)}{Residuo(valor % 1000)}";

        return HastaNovecientos(valor);
    }

    private static string ResiduoMil(long miles) =>
        miles == 1 ? "mil" : $"{HastaNovecientos(miles)} mil";

    private static string Residuo(long resto) =>
        resto == 0 ? string.Empty : $" {EnteroEnLetras(resto)}";

    private static string HastaNovecientos(long valor)
    {
        if (valor < 100)
            return HastaNoventaYNueve((int)valor);

        var centena = (int)(valor / 100);
        var resto = (int)(valor % 100);

        if (centena == 1 && resto == 0)
            return "cien";

        var partes = new StringBuilder(Centenas[centena]);
        if (resto > 0)
            partes.Append(' ').Append(HastaNoventaYNueve(resto));

        return partes.ToString();
    }

    private static string HastaNoventaYNueve(int valor)
    {
        if (valor < 21)
            return Units[valor];

        var decena = valor / 10;
        var unidad = valor % 10;

        if (decena == 2)
            return unidad == 0 ? "veinte" : $"veinti{Units[unidad]}";

        if (unidad == 0)
            return Decenas[decena];

        return $"{Decenas[decena]} y {Units[unidad]}";
    }

    private static string Capitalizar(string texto) =>
        string.IsNullOrEmpty(texto)
            ? texto
            : char.ToUpper(texto[0], CultureInfo.InvariantCulture) + texto[1..];
}
