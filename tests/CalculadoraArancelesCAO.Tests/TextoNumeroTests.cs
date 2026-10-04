using CalculadoraArancelesCAO.Core.Models;

namespace CalculadoraArancelesCAO.Tests;

public class TextoNumeroTests
{
    [Theory]
    [InlineData(0, "Cero")]
    [InlineData(1, "Uno")]
    [InlineData(15, "Quince")]
    [InlineData(16, "Dieciséis")]
    [InlineData(21, "Veintiuno")]
    [InlineData(30, "Treinta")]
    [InlineData(100, "Cien")]
    [InlineData(101, "Ciento uno")]
    [InlineData(2233.79, "Dos mil doscientos treinta y tres con 79/100")]
    [InlineData(12967.15095, "Doce mil novecientos sesenta y siete con 15/100")]
    [InlineData(1000000, "Un millón")]
    [InlineData(2000000, "Dos millones")]
    [InlineData(1234567.89, "Un millón doscientos treinta y cuatro mil quinientos sesenta y siete con 89/100")]
    public void EnLetras_ConvierteMontosASuLiteralEnBolivianos(double monto, string esperado)
    {
        Assert.Equal(esperado, TextoNumero.EnLetras(monto));
    }

    [Fact]
    public void EnLetras_IgnoraElSignoNegativo()
    {
        Assert.Equal("Doscientos cincuenta", TextoNumero.EnLetras(-250));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void EnLetras_ValoresNoNumericos_RetornanVacio(double monto)
    {
        Assert.Equal(string.Empty, TextoNumero.EnLetras(monto));
    }

    [Fact]
    public void EnLetras_RedondeaADosDecimales()
    {
        Assert.Equal("Ciento veinticinco", TextoNumero.EnLetras(124.995));
        Assert.Equal("Ciento veinticinco con 01/100", TextoNumero.EnLetras(125.0051));
    }

    [Fact]
    public void FormatoBoliviano_MuestraBsConSeparadorDeMiles()
    {
        Assert.Equal("Bs 8.935,16", FormatoBoliviano.Moneda(8935.16));
        Assert.Equal("12.967,09", FormatoBoliviano.Numero(12967.0929));
        Assert.Equal("5%", FormatoBoliviano.Porcentaje(0.05));
        Assert.Equal("5,5%", FormatoBoliviano.Porcentaje(0.055));
    }

    [Fact]
    public void NumeroParseado_AceptaFormatoBolivianoYAngosto()
    {
        Assert.Equal(12967.09, FormatoBoliviano.NumeroParseado("12.967,09"), 2);
        Assert.Equal(180, FormatoBoliviano.NumeroParseado("180"), 2);
        Assert.Equal(0, FormatoBoliviano.NumeroParseado("abc"), 2);
        Assert.Equal(0, FormatoBoliviano.NumeroParseado(null), 2);
    }
}
