namespace CalculadoraArancelesCAO.Core.Models;

public enum PerfilAlcance
{
    AltaComplejidad = 0,
    Habitual = 1,
    Simple = 2
}

public sealed record PerfilAlcanceInfo(
    PerfilAlcance Id,
    string Codigo,
    string Nombre,
    string Descripcion,
    IReadOnlyDictionary<EtapaProyecto, double> PesosPorEtapa,
    double FaMaximo)
{
    public double ObtenerPeso(EtapaProyecto etapa) =>
        PesosPorEtapa.TryGetValue(etapa, out var peso) ? peso : 0.0;
}

public static class PerfilAlcanceCatalogo
{
    public static readonly IReadOnlyList<PerfilAlcanceInfo> Todos = new[]
    {
        new PerfilAlcanceInfo(
            PerfilAlcance.AltaComplejidad,
            "A",
            "Perfil A - Alta complejidad",
            "Intervenciones especializadas, instalaciones complejas o normativa exigente. " +
            "El esfuerzo se concentra en proyecto, especificaciones y detalle.",
            new Dictionary<EtapaProyecto, double>
            {
                [EtapaProyecto.EstudiosPreliminares] = 0.15,
                [EtapaProyecto.Anteproyecto] = 0.20,
                [EtapaProyecto.ProyectoArquitectonico] = 0.30,
                [EtapaProyecto.Especificaciones] = 0.12,
                [EtapaProyecto.PlanosDeDetalle] = 0.15,
                [EtapaProyecto.ComputosYPresupuestos] = 0.08
            },
            1.00),
        new PerfilAlcanceInfo(
            PerfilAlcance.Habitual,
            "B",
            "Perfil B - Habitual",
            "Encargo residencial y comercial estándar. Distribución de esfuerzo equilibrada.",
            new Dictionary<EtapaProyecto, double>
            {
                [EtapaProyecto.EstudiosPreliminares] = 0.18,
                [EtapaProyecto.Anteproyecto] = 0.22,
                [EtapaProyecto.ProyectoArquitectonico] = 0.28,
                [EtapaProyecto.Especificaciones] = 0.12,
                [EtapaProyecto.PlanosDeDetalle] = 0.12,
                [EtapaProyecto.ComputosYPresupuestos] = 0.08
            },
            1.00),
        new PerfilAlcanceInfo(
            PerfilAlcance.Simple,
            "C",
            "Perfil C - Simple",
            "Ampliaciones, incrementos y encargos de alcance reducido con énfasis en proyecto.",
            new Dictionary<EtapaProyecto, double>
            {
                [EtapaProyecto.EstudiosPreliminares] = 0.20,
                [EtapaProyecto.Anteproyecto] = 0.30,
                [EtapaProyecto.ProyectoArquitectonico] = 0.35,
                [EtapaProyecto.Especificaciones] = 0.05,
                [EtapaProyecto.PlanosDeDetalle] = 0.05,
                [EtapaProyecto.ComputosYPresupuestos] = 0.05
            },
            1.00)
    };

    public static PerfilAlcanceInfo Obtener(PerfilAlcance perfil) => Todos[(int)perfil];
}
