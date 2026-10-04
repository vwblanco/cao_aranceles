namespace CalculadoraArancelesCAO.Core.Models;

public sealed class DatosSolicitante
{
    public string Solicitante { get; set; } = string.Empty;

    public string Ci { get; set; } = string.Empty;

    public string Matricula { get; set; } = string.Empty;

    public string Direccion { get; set; } = string.Empty;

    public string Municipio { get; set; } = "Oruro";

    public string Telefono { get; set; } = string.Empty;

    public string Correo { get; set; } = string.Empty;

    public bool Completos =>
        !string.IsNullOrWhiteSpace(Solicitante) && !string.IsNullOrWhiteSpace(Ci);
}

public sealed class ReporteCotizacion
{
    public string Numero { get; set; } = string.Empty;

    public DateTimeOffset Fecha { get; set; } = DateTimeOffset.Now;

    public required ResultadoArancel Resultado { get; set; }

    public DatosSolicitante Solicitante { get; set; } = new();

    public required ParametrosCAO Parametros { get; set; }

    public string Profesional { get; set; } = string.Empty;

    public string MatriculaProfesional { get; set; } = string.Empty;

    public string Observaciones { get; set; } = string.Empty;
}
