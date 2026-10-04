namespace CalculadoraArancelesCAO.Core.Models;

public sealed record LineaDesglose(
    string Concepto,
    string Detalle,
    double Cantidad,
    string Unidad,
    double? Factor,
    double Subtotal)
{
    public static LineaDesglose Simple(string concepto, string detalle, double subtotal) =>
        new(concepto, detalle, 1, "Bs", null, subtotal);
}

public sealed record LineaEscala(
    string Tramo,
    int Cantidad,
    double Porcentaje,
    double Subtotal)
{
    public string PorcentajeMostrado => FormatoBoliviano.Decimal(Porcentaje, 2);
}

public sealed class ResultadoEscala
{
    public required double ValorUnidadBase { get; init; }

    public required int Cantidad { get; init; }

    public required IReadOnlyList<LineaEscala> Tramos { get; init; }

    public double FactorAcumulado => Tramos.Sum(t => t.Cantidad * t.Porcentaje);

    public double Total => ValorUnidadBase * FactorAcumulado;

    public string TotalEnLetras => TextoNumero.EnLetras(Total);

    public ResultadoArancel ToResultadoArancel()
    {
        var desglose = new List<LineaDesglose>
        {
            new("Valor de la unidad base", "Honorario de la unidad de referencia", ValorUnidadBase, "Bs", null, ValorUnidadBase)
        };

        desglose.AddRange(Tramos.Select(t => new LineaDesglose(
            $"Unidades {t.Tramo}",
            $"{t.Cantidad} x {FormatoBoliviano.Decimal(t.Porcentaje, 2)} ({FormatoBoliviano.Porcentaje(t.Porcentaje)})",
            t.Cantidad,
            "und",
            t.Porcentaje,
            t.Subtotal)));

        desglose.Add(LineaDesglose.Simple(
            "Honorario total",
            $"{Cantidad} unidad(es) x escala decreciente (factor acumulado {FormatoBoliviano.Decimal(FactorAcumulado, 2)})",
            Total));

        return new ResultadoArancel
        {
            Area = AreaArancel.Area6,
            Concepto = "Plantas tipo y proyectos repetidos",
            FactorAlcance = FactorAcumulado,
            Total = Total,
            Desglose = desglose,
            Advertencias =
            [
                "La escala decreciente se aplica sobre el valor de la unidad base; " +
                "la unidad base debe ser calculada previamente por el Area 1 o declarada por el solicitante."
            ],
            FormulaResumen =
                $"Escala decreciente: 1a 100 %, 2a 50 %, 3a 40 %, 4a 30 %, 5a 20 %, 6a-10a 10 %, " +
                $"11a-50a 5 %, >50a 2 % | Factor acumulado = {FormatoBoliviano.Decimal(FactorAcumulado, 2)} | " +
                $"Total = {FormatoBoliviano.Moneda(ValorUnidadBase)} x {FormatoBoliviano.Decimal(FactorAcumulado, 2)} = " +
                $"{FormatoBoliviano.Moneda(Total)}"
        };
    }
}
