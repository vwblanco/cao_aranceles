namespace CalculadoraArancelesCAO.Core.Models;

/// <summary>
/// Parámetros económicos institucionales del Reglamento de Aranceles 2026 del
/// Colegio de Arquitectos de Oruro (CAO).
/// </summary>
public sealed class ParametrosCAO
{
    /// <summary>Valor de la Variable Vida (V) vigente segun el Reglamento 2026, en bolivianos.</summary>
    public const double VariableVidaPorDefecto = 8935.16;

    /// <summary>Carga horaria mensual normalizada (V / CH) usada para derivar el costo hora.</summary>
    public const double HorasMensualesPorDefecto = 160.0;

    /// <summary>Divisor del honorario base semanal (V / 4).</summary>
    public const double DivisorHonorarioBase = 4.0;

    /// <summary>
    /// Variable Vida (V) en bolivianos. Es configurable en memoria y se persiste en
    /// local storage para cuando la Asamblea actualice el Salario Minimo Nacional.
    /// </summary>
    public double VariableVida { get; set; } = VariableVidaPorDefecto;

    /// <summary>Carga horaria mensual (160 horas) sobre la que se calcula el costo hora.</summary>
    public double HorasMensuales { get; set; } = HorasMensualesPorDefecto;

    /// <summary>Costo hora (CH) = V / 160. Se conserva precision completa para el calculo.</summary>
    public double CostoHora => VariableVida / HorasMensuales;

    /// <summary>Honorario base semanal (Hbase) = V / 4.</summary>
    public double HonorarioBaseSemanal => VariableVida / DivisorHonorarioBase;

    /// <summary>Costohora redondeado a dos decimales, solo para presentacion.</summary>
    public double CostoHoraMostrado => Redondear(CostoHora);

    /// <summary>Honorario base semanal redondeado a dos decimales, solo para presentacion.</summary>
    public double HonorarioBaseMostrado => Redondear(HonorarioBaseSemanal);

    public static ParametrosCAO PorDefecto() => new();

    public ParametrosCAO Copia() => new()
    {
        VariableVida = VariableVida,
        HorasMensuales = HorasMensuales
    };

    public void RestaurarValoresOficiales()
    {
        VariableVida = VariableVidaPorDefecto;
        HorasMensuales = HorasMensualesPorDefecto;
    }

    public bool UsaValoresOficiales =>
        Math.Abs(VariableVida - VariableVidaPorDefecto) < 0.005 &&
        Math.Abs(HorasMensuales - HorasMensualesPorDefecto) < 0.005;

    /// <summary>Validaciones de consistencia de los parametros ingresados por la asamblea.</summary>
    public IReadOnlyList<string> Validar()
    {
        var errores = new List<string>();

        if (double.IsNaN(VariableVida) || double.IsInfinity(VariableVida) || VariableVida <= 0)
            errores.Add("La Variable Vida (V) debe ser un valor mayor que cero.");

        if (double.IsNaN(HorasMensuales) || double.IsInfinity(HorasMensuales) || HorasMensuales <= 0)
            errores.Add("La carga horaria mensual debe ser un valor mayor que cero.");

        if (HorasMensuales < 40)
            errores.Add("La carga horaria mensual no puede ser inferior a 40 horas.");

        return errores;
    }

    public static double Redondear(double valor) =>
        (double)Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
