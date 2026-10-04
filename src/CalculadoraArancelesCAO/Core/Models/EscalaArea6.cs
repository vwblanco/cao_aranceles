namespace CalculadoraArancelesCAO.Core.Models;

public sealed record TramoEscalaArea6(
    string Tramo,
    int Desde,
    int? Hasta,
    double Porcentaje,
    string Descripcion)
{
    public bool Acepta(int cantidad) => cantidad >= Desde && (Hasta is null || cantidad <= Hasta.Value);

    public string RangoMostrado => Hasta is null
        ? $"De la {Desde}a unidad en adelante"
        : Desde == Hasta.Value
            ? $"{Desde}a unidad"
            : $"De la {Desde}a a la {Hasta}a unidad";

    public int CuantasAplica(int cantidad) => Math.Max(0, Math.Min(cantidad, Hasta ?? int.MaxValue) - Desde + 1);
}

public static class EscalaArea6Catalogo
{
    public static readonly IReadOnlyList<TramoEscalaArea6> Tramos = new[]
    {
        new TramoEscalaArea6("1a", 1, 1, 1.00, "Primera unidad al 100 %."),
        new TramoEscalaArea6("2a", 2, 2, 0.50, "Segunda unidad al 50 %."),
        new TramoEscalaArea6("3a", 3, 3, 0.40, "Tercera unidad al 40 %."),
        new TramoEscalaArea6("4a", 4, 4, 0.30, "Cuarta unidad al 30 %."),
        new TramoEscalaArea6("5a", 5, 5, 0.20, "Quinta unidad al 20 %."),
        new TramoEscalaArea6("6a a 10a", 6, 10, 0.10, "De la sexta a la décima unidad al 10 %."),
        new TramoEscalaArea6("11a a 50a", 11, 50, 0.05, "De la undécima a la quincuagésima unidad al 5 %."),
        new TramoEscalaArea6("51a en adelante", 51, null, 0.02, "De la quincuagésima primera unidad en adelante al 2 %.")
    };
}
