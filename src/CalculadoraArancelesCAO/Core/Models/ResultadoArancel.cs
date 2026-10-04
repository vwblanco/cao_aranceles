namespace CalculadoraArancelesCAO.Core.Models;

public sealed class ResultadoArancel
{
    public required AreaArancel Area { get; init; }

    public required string Concepto { get; init; }

    public string? Tipologia { get; init; }

    public double? SuperficieM2 { get; init; }

    public string? RangoDescripcion { get; init; }

    public double? FactorH { get; init; }

    public double? FactorAlcance { get; init; }

    public double? FactorComplejidad { get; init; }

    public double? CostoHora { get; init; }

    public double? HonorarioBaseSemanal { get; init; }

    public double? HonorarioCalculado { get; init; }

    public double? LimiteMinimo { get; init; }

    public double Total { get; init; }

    public double? AportePatrimonioInmobiliario { get; init; }

    public bool LimiteMinimoAplicado { get; init; }

    public IReadOnlyList<LineaDesglose> Desglose { get; init; } = [];

    public IReadOnlyList<string> Advertencias { get; init; } = [];

    public IReadOnlyList<string> ParametrosUsados { get; init; } = [];

    public string FormulaResumen { get; init; } = string.Empty;

    public string TotalEnLetras => TextoNumero.EnLetras(Total);
}
