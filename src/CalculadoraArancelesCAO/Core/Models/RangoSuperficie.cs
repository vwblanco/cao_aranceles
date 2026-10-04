namespace CalculadoraArancelesCAO.Core.Models;

public enum RangoSuperficie
{
    Hasta100 = 0,
    De101a250 = 1,
    De251a600 = 2,
    De601a1500 = 3,
    Mas1500 = 4
}

public sealed record RangoSuperficieInfo(
    RangoSuperficie Id,
    string Codigo,
    string Etiqueta,
    double? MinimoExcluido,
    double? MaximoIncluido)
{
    public bool Corresponde(double m2) => m2 switch
    {
        <= 100 => Id == RangoSuperficie.Hasta100,
        <= 250 => Id == RangoSuperficie.De101a250,
        <= 600 => Id == RangoSuperficie.De251a600,
        <= 1500 => Id == RangoSuperficie.De601a1500,
        _ => Id == RangoSuperficie.Mas1500
    };
}

public static class RangoSuperficieCatalogo
{
    public static readonly IReadOnlyList<RangoSuperficieInfo> Todos = new[]
    {
        new RangoSuperficieInfo(RangoSuperficie.Hasta100, "R1", "Hasta 100 m²", null, 100),
        new RangoSuperficieInfo(RangoSuperficie.De101a250, "R2", "101 a 250 m²", 100, 250),
        new RangoSuperficieInfo(RangoSuperficie.De251a600, "R3", "251 a 600 m²", 250, 600),
        new RangoSuperficieInfo(RangoSuperficie.De601a1500, "R4", "601 a 1.500 m²", 600, 1500),
        new RangoSuperficieInfo(RangoSuperficie.Mas1500, "R5", "Más de 1.500 m²", 1500, null)
    };

    public static RangoSuperficieInfo Resolver(double m2) =>
        Todos.FirstOrDefault(r => r.Corresponde(m2))
        ?? throw new ArgumentOutOfRangeException(nameof(m2), m2, "La superficie debe ser mayor que cero.");

    public static RangoSuperficieInfo Obtener(RangoSuperficie rango) => Todos[(int)rango];
}
