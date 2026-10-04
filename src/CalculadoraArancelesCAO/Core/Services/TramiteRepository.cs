using CalculadoraArancelesCAO.Core.Models;

namespace CalculadoraArancelesCAO.Core.Services;

public sealed class TramiteRepository
{
    public const string AvisoVigencia =
        "Tarifas de referencia cargadas en la aplicacion. Deben ser confirmadas por la Asamblea " +
        "antes de su uso para el cobro de servicios institucionales.";

    public IReadOnlyList<Tramite> Todos { get; } =
    [
        new("PD-01", "Plano demostrativo de vivienda unifamiliar (A3)", CategoriaTramite.PlanosDemonstrativos,
            BaseTarifa.Fija, 350.00, "por lámina", "Reglamento 2026", "Anexo de planos demostrativos"),
        new("PD-02", "Plano demostrativo de vivienda multifamiliar (A3)", CategoriaTramite.PlanosDemonstrativos,
            BaseTarifa.Fija, 450.00, "por lámina", "Reglamento 2026", "Anexo de planos demostrativos"),
        new("PD-03", "Plano demostrativo comercial (A3)", CategoriaTramite.PlanosDemonstrativos,
            BaseTarifa.Fija, 400.00, "por lámina", "Reglamento 2026", "Anexo de planos demostrativos"),
        new("PD-04", "Plano demostrativo en formato digital (PDF)", CategoriaTramite.PlanosDemonstrativos,
            BaseTarifa.Fija, 120.00, "por archivo", "Reglamento 2026", "Anexo de planos demostrativos"),
        new("PD-05", "Juego completo de planos demostrativos", CategoriaTramite.PlanosDemonstrativos,
            BaseTarifa.Fija, 1500.00, "por juego", "Reglamento 2026", "Anexo de planos demostrativos"),

        new("DJ-01", "Declaración jurada de conformidad de obra", CategoriaTramite.DeclaracionesJuradas,
            BaseTarifa.Fija, 200.00, "por declaración", "Reglamento 2026", "Formulario institucional"),
        new("DJ-02", "Declaración jurada de inexistencia de obra", CategoriaTramite.DeclaracionesJuradas,
            BaseTarifa.Fija, 200.00, "por declaración", "Reglamento 2026", "Formulario institucional"),
        new("DJ-03", "Declaración jurada de responsable de obra", CategoriaTramite.DeclaracionesJuradas,
            BaseTarifa.Fija, 250.00, "por declaración", "Reglamento 2026", "Formulario institucional"),
        new("DJ-04", "Declaración jurada de conformidad de uso", CategoriaTramite.DeclaracionesJuradas,
            BaseTarifa.Fija, 250.00, "por declaración", "Reglamento 2026", "Formulario institucional"),

        new("TM-01", "Inspección municipal de obra (nivel 1)", CategoriaTramite.TramitesMunicipales,
            BaseTarifa.MultiplicadorCostoHora, 4.0, "por inspección", "Reglamento 2026", "Convenio municipal CAO"),
        new("TM-02", "Inspección municipal de obra (nivel 2)", CategoriaTramite.TramitesMunicipales,
            BaseTarifa.MultiplicadorCostoHora, 8.0, "por inspección", "Reglamento 2026", "Convenio municipal CAO"),
        new("TM-03", "Certificación de uso de suelo", CategoriaTramite.TramitesMunicipales,
            BaseTarifa.MultiplicadorCostoHora, 3.0, "por certificación", "Reglamento 2026", "Convenio municipal CAO"),
        new("TM-04", "Aprobación de planos en GAMO", CategoriaTramite.TramitesMunicipales,
            BaseTarifa.MultiplicadorCostoHora, 6.0, "por trámite", "Reglamento 2026", "Convenio municipal CAO"),
        new("TM-05", "Licencia de construcción (segundo tramo)", CategoriaTramite.TramitesMunicipales,
            BaseTarifa.MultiplicadorHonorarioBase, 0.25, "por trámite", "Reglamento 2026", "Convenio municipal CAO"),

        new("SC-01", "Timbre institucional de peritaje", CategoriaTramite.ServiciosCAO,
            BaseTarifa.Fija, 60.00, "por timbre", "Reglamento 2026", "Reglamento interno CAO"),
        new("SC-02", "Firma de médico architecto", CategoriaTramite.ServiciosCAO,
            BaseTarifa.Fija, 150.00, "por firma", "Reglamento 2026", "Reglamento interno CAO"),
        new("SC-03", "Copias de documentos de proyecto", CategoriaTramite.ServiciosCAO,
            BaseTarifa.Fija, 5.00, "por copia", "Reglamento 2026", "Reglamento interno CAO"),
        new("SC-04", "Aporte al Patrimonio Inmobiliario (5,5 %)", CategoriaTramite.ServiciosCAO,
            BaseTarifa.MultiplicadorHonorarioBase, 0.055, "del honorario", "Reglamento 2026", "Área 2A")
    ];

    public IEnumerable<Tramite> Buscar(string? texto, CategoriaTramite? categoria = null)
    {
        var consulta = Todos.AsEnumerable();

        if (categoria is not null)
            consulta = consulta.Where(t => t.Categoria == categoria);

        var termino = (texto ?? string.Empty).Trim();

        if (termino.Length > 0)
        {
            consulta = consulta.Where(t =>
                t.Nombre.Contains(termino, StringComparison.CurrentCultureIgnoreCase) ||
                t.Codigo.Contains(termino, StringComparison.CurrentCultureIgnoreCase) ||
                CategoriaTramiteCatalogo.Nombre(t.Categoria)
                    .Contains(termino, StringComparison.CurrentCultureIgnoreCase));
        }

        return consulta;
    }

    public Tramite? ObtenerPorCodigo(string codigo) =>
        Todos.FirstOrDefault(t =>
            string.Equals(t.Codigo, codigo, StringComparison.OrdinalIgnoreCase));

    public bool TodosConfirmados => Todos.All(t => t.ConfirmadoPorAsamblea);
}
