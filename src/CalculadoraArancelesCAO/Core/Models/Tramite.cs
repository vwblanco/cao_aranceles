namespace CalculadoraArancelesCAO.Core.Models;

public enum CategoriaTramite
{
    PlanosDemonstrativos = 0,
    DeclaracionesJuradas = 1,
    TramitesMunicipales = 2,
    ServiciosCAO = 3
}

public enum BaseTarifa
{
    Fija = 0,
    MultiplicadorHonorarioBase = 1,
    MultiplicadorCostoHora = 2
}

public sealed record Tramite(
    string Codigo,
    string Nombre,
    CategoriaTramite Categoria,
    BaseTarifa Base,
    double Valor,
    string Unidad,
    string Vigencia,
    string Fuente,
    bool ConfirmadoPorAsamblea = false)
{
    public bool EsParametrica => Base != BaseTarifa.Fija;

    public string BaseDescripcion => Base switch
    {
        BaseTarifa.MultiplicadorHonorarioBase => "Multiplicador del honorario base semanal",
        BaseTarifa.MultiplicadorCostoHora => "Multiplicador del costo hora",
        _ => "Tarifa fija"
    };

    public double Resolver(ParametrosCAO parametros) => Base switch
    {
        BaseTarifa.MultiplicadorHonorarioBase => parametros.HonorarioBaseSemanal * Valor,
        BaseTarifa.MultiplicadorCostoHora => parametros.CostoHora * Valor,
        _ => Valor
    };

    public string MostrarValorBase() => Base switch
    {
        BaseTarifa.MultiplicadorHonorarioBase => $"Bs {FormatoBoliviano.Numero(Valor)} x Hbase",
        BaseTarifa.MultiplicadorCostoHora => $"Bs {FormatoBoliviano.Numero(Valor)} x CH",
        _ => $"Bs {FormatoBoliviano.Numero(Valor)}"
    };
}

public static class CategoriaTramiteCatalogo
{
    public static readonly IReadOnlyList<(CategoriaTramite Id, string Codigo, string Nombre)> Todas = new[]
    {
        (CategoriaTramite.PlanosDemonstrativos, "PD", "Planos demostrativos"),
        (CategoriaTramite.DeclaracionesJuradas, "DJ", "Declaraciones juradas"),
        (CategoriaTramite.TramitesMunicipales, "TM", "Trámites municipales"),
        (CategoriaTramite.ServiciosCAO, "SC", "Servicios del CAO")
    };

    public static string Nombre(CategoriaTramite categoria) =>
        Todas.First(c => c.Id == categoria).Nombre;
}
