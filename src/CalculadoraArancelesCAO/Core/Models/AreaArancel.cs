namespace CalculadoraArancelesCAO.Core.Models;

public enum AreaArancel
{
    Area1 = 1,
    Area2A = 2,
    Area3 = 3,
    Area4A = 4,
    Area4B = 5,
    Area6 = 6
}

public sealed record AreaArancelInfo(
    AreaArancel Id,
    string Codigo,
    string Nombre,
    string Descripcion,
    string Ruta)
{
    public bool MuestraMatrizAlcance => Id is AreaArancel.Area1 or AreaArancel.Area3;
    public bool MuestraSuperficie => Id is AreaArancel.Area1 or AreaArancel.Area2A or AreaArancel.Area3;
}

public static class AreaArancelCatalogo
{
    public static readonly IReadOnlyList<AreaArancelInfo> Todas = new[]
    {
        new AreaArancelInfo(AreaArancel.Area1, "1",
            "Servicios de arquitectura",
            "Proyecto arquitectónico según tipología, superficie y alcance contratado.",
            "area1"),
        new AreaArancelInfo(AreaArancel.Area2A, "2A",
            "Lotes y fraccionamiento",
            "Honorario por valorización del lote más aporte al Patrimonio Inmobiliario.",
            "area2a"),
        new AreaArancelInfo(AreaArancel.Area3, "3",
            "Remodelación, restauración y patrimonio",
            "Intervenciones sobre construido existente con factor de complejidad.",
            "area3"),
        new AreaArancelInfo(AreaArancel.Area4A, "4A",
            "Avalúos, peritajes y dirimición",
            "Honorarios proporcionales al valor comercial del inmueble.",
            "area4a"),
        new AreaArancelInfo(AreaArancel.Area4B, "4B",
            "Inspecciones y auditorías",
            "Honorarios por multiplicador sobre el honorario base semanal.",
            "area4b"),
        new AreaArancelInfo(AreaArancel.Area6, "6",
            "Plantas tipo y proyectos repetidos",
            "Escala decreciente sobre el valor de la unidad base.",
            "area6")
    };

    public static AreaArancelInfo Obtener(AreaArancel area) => Todas.First(a => a.Id == area);
}
