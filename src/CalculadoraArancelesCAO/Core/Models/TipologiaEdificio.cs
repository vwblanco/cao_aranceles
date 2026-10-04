namespace CalculadoraArancelesCAO.Core.Models;

public enum TipologiaEdificio
{
    ViviendaUnifamiliar = 0,
    ComercioOficinas = 1,
    MultifamiliarEquipamiento = 2,
    IndustriaEspeciales = 3
}

public sealed record TipologiaInfo(
    TipologiaEdificio Id,
    string Codigo,
    string Nombre,
    string Descripcion,
    double[] MatrizH)
{
    public double ObtenerFactorH(RangoSuperficie rango) => MatrizH[(int)rango];
}

public static class TipologiaCatalogo
{
    public static readonly IReadOnlyList<TipologiaInfo> Todas = new[]
    {
        new TipologiaInfo(
            TipologiaEdificio.ViviendaUnifamiliar,
            "VU",
            "Vivienda Unifamiliar",
            "Vivienda unifamiliar aislada, pareada o en serie.",
            [1.50, 1.29, 1.12, 1.00, 0.92]),
        new TipologiaInfo(
            TipologiaEdificio.ComercioOficinas,
            "CO",
            "Comercio / Oficinas",
            "Locales comerciales, oficinas, galerías y mixtos de uso comercial.",
            [1.67, 1.46, 1.29, 1.17, 1.08]),
        new TipologiaInfo(
            TipologiaEdificio.MultifamiliarEquipamiento,
            "ME",
            "Multifamiliar / Equipamiento",
            "Viviendas multifamiliares y edificios de equipamiento social.",
            [1.83, 1.62, 1.46, 1.29, 1.21]),
        new TipologiaInfo(
            TipologiaEdificio.IndustriaEspeciales,
            "IE",
            "Industria / Proyectos Especiales",
            "Naves industriales, equipamientos técnicos y proyectos especiales.",
            [2.00, 1.75, 1.58, 1.42, 1.29])
    };

    public static TipologiaInfo Obtener(TipologiaEdificio tipologia) =>
        Todas[(int)tipologia];
}
