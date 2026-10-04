namespace CalculadoraArancelesCAO.Core.Models;

public enum EtapaProyecto
{
    EstudiosPreliminares = 0,
    Anteproyecto = 1,
    ProyectoArquitectonico = 2,
    Especificaciones = 3,
    PlanosDeDetalle = 4,
    ComputosYPresupuestos = 5
}

public sealed record EtapaProyectoInfo(
    EtapaProyecto Id,
    string Codigo,
    string Nombre,
    string Descripcion);

public static class EtapaProyectoCatalogo
{
    public static readonly IReadOnlyList<EtapaProyectoInfo> Todas = new[]
    {
        new EtapaProyectoInfo(EtapaProyecto.EstudiosPreliminares, "E1",
            "Estudios preliminares",
            "Levantamiento, análisis del entorno, condicionamiento y programación."),
        new EtapaProyectoInfo(EtapaProyecto.Anteproyecto, "E2",
            "Anteproyecto",
            "Propuesta de volumetría, implantación y esquema funcional."),
        new EtapaProyectoInfo(EtapaProyecto.ProyectoArquitectonico, "E3",
            "Proyecto arquitectonico",
            "Planos generales, cortes, dimensionados y presentación de layouts."),
        new EtapaProyectoInfo(EtapaProyecto.Especificaciones, "E4",
            "Especificaciones",
            "Especificaciones técnicas y partición de espacios."),
        new EtapaProyectoInfo(EtapaProyecto.PlanosDeDetalle, "E5",
            "Planos de detalle",
            "Detalle constructivo, carpintería, instalaciones y equipamiento."),
        new EtapaProyectoInfo(EtapaProyecto.ComputosYPresupuestos, "E6",
            "Cómputos y presupuestos",
            "Metrados, partidas, presupuesto base y cronograma.")
    };

    public static EtapaProyectoInfo Obtener(EtapaProyecto etapa) => Todas[(int)etapa];
}
