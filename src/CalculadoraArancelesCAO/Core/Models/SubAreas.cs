namespace CalculadoraArancelesCAO.Core.Models;

public enum SubArea3
{
    Remodelacion = 0,
    Restauracion = 1,
    Patrimonio = 2
}

public sealed record SubArea3Info(SubArea3 Id, string Codigo, string Nombre, string Descripcion, double Fj);

public enum TipoAvaluo
{
    Avaluo = 0,
    Peritaje = 1,
    Dirimicion = 2
}

public sealed record TipoAvaluoInfo(TipoAvaluo Id, string Codigo, string Nombre, double TasaPorMil, string Formula);

public enum TipoInspeccion
{
    Simple = 0,
    Tecnica = 1,
    Integral = 2
}

public sealed record TipoInspeccionInfo(TipoInspeccion Id, string Codigo, string Nombre, double Multiplicador, string Descripcion);

public static class SubAreasCatalogo
{
    public static readonly IReadOnlyList<SubArea3Info> Area3 = new[]
    {
        new SubArea3Info(SubArea3.Remodelacion, "3A", "Remodelación",
            "Intervención sobre la ocupación existente, sin alterar la estructura de valor patrimonial.", 1.10),
        new SubArea3Info(SubArea3.Restauracion, "3B", "Restauración",
            "Recuperación de áreas intervenidas y retorno a condiciones de servicio.", 1.20),
        new SubArea3Info(SubArea3.Patrimonio, "3C", "Patrimonio",
            "Intervención sobre inmueble de valor patrimonial, con exigencias técnicas adicionales.", 1.25)
    };

    public static readonly IReadOnlyList<TipoAvaluoInfo> Area4A = new[]
    {
        new TipoAvaluoInfo(TipoAvaluo.Avaluo, "4A.1", "Avalúo", 3.0,
            "Valor del inmueble × 3 / 1000"),
        new TipoAvaluoInfo(TipoAvaluo.Peritaje, "4A.2", "Peritaje", 6.0,
            "Valor del inmueble × 6 / 1000"),
        new TipoAvaluoInfo(TipoAvaluo.Dirimicion, "4A.3", "Dirimición", 6.0,
            "Valor del inmueble × 6 / 1000")
    };

    public static readonly IReadOnlyList<TipoInspeccionInfo> Area4B = new[]
    {
        new TipoInspeccionInfo(TipoInspeccion.Simple, "4B.1", "Inspección simple", 0.50,
            "Verificación básica de conformidad documentaria."),
        new TipoInspeccionInfo(TipoInspeccion.Tecnica, "4B.2", "Auditoría técnica", 0.75,
            "Auditoría técnica sobre la consistencia de la obra."),
        new TipoInspeccionInfo(TipoInspeccion.Integral, "4B.3", "Auditoría integral", 1.50,
            "Auditoría integral con dictamen técnico de peritaje.")
    };

    public static SubArea3Info ObtenerArea3(SubArea3 sub) => Area3[(int)sub];

    public static TipoAvaluoInfo ObtenerAvaluo(TipoAvaluo tipo) => Area4A[(int)tipo];

    public static TipoInspeccionInfo ObtenerInspeccion(TipoInspeccion tipo) => Area4B[(int)tipo];
}
