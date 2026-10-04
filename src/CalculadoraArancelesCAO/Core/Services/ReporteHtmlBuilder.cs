using System.Net;
using System.Text;
using CalculadoraArancelesCAO.Core.Models;

namespace CalculadoraArancelesCAO.Core.Services;

/// <summary>Genera el HTML del reporte de cotización institucional listo para exportar a PDF.</summary>
public static class ReporteHtmlBuilder
{
    public const string NombreInstitucion = "Colegio de Arquitectos de Oruro";
    public const string SiglasInstitucion = "CAO";
    public const string TextoLegal =
        "El presente documento es un estimateo referencial emitido por la Calculadora de Aranceles del " +
        "Colegio de Arquitectos de Oruro. Su cumplimiento es obligatorio conforme al Reglamento de " +
        "Aranceles 2026 y a la Ley N. 1373 de Ejecución de Obras Públicas. El Colegio se reserva el " +
        "derecho de verificar los datos declarados por el solicitante y de ajustar el monto cuando " +
        "la Asamblea modifique la Variable Vida o los anexos normativos.";

    public static string Construir(ReporteCotizacion cotizacion)
    {
        var resultado = cotizacion.Resultado;
        var area = AreaArancelCatalogo.Obtener(resultado.Area);
        var html = new StringBuilder();

        html.Append("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\">");
        html.Append("<style>").Append(Css()).Append("</style></head><body>");

        html.Append("<div class=\"membrete\">");
        html.Append("<img src=\"CAO-192.png\" alt=\"CAO\" class=\"escudo\" />");
        html.Append("<div class=\"institucion\">");
        html.Append("<div class=\"nombre\">").Append(NombreInstitucion).Append("</div>");
        html.Append("<div class=\"sub\">Colegio Profesional - Departamento de Oruro</div>");
        html.Append("<div class=\"cargo\">COMPUTACIÓN DE ARANCELES</div>");
        html.Append("</div>");
        html.Append("<div class=\"folio\"><div class=\"folio-label\">Cotización N.°</div>");
        html.Append("<div class=\"folio-valor\">").Append(Escape(cotizacion.Numero)).Append("</div>");
        html.Append("<div class=\"folio-label\">Fecha</div>");
        html.Append("<div class=\"folio-valor\">")
            .Append(cotizacion.Fecha.ToString("dd/MM/yyyy", FormatoBoliviano.Culture))
            .Append("</div></div>");
        html.Append("</div>");

        html.Append("<h1>Reporte de cálculo de honorarios profesionales</h1>");

        html.Append("<table class=\"datos\"><tbody>");
        Fila(html, "Área del reglamento", $"Área {area.Codigo} - {area.Nombre}");
        Fila(html, "Concepto", resultado.Concepto);

        if (!string.IsNullOrWhiteSpace(resultado.Tipologia))
            Fila(html, "Tipología", resultado.Tipologia);

        if (resultado.SuperficieM2 is not null)
            Fila(html, "Superficie", $"{FormatoBoliviano.Decimal(resultado.SuperficieM2.Value, 2)} m²");

        if (!string.IsNullOrWhiteSpace(resultado.RangoDescripcion))
            Fila(html, "Rango de superficie", resultado.RangoDescripcion);

        var solicitanteTexto = Valor(cotizacion.Solicitante.Solicitante, "No consignado");
        var ciTexto = Valor(cotizacion.Solicitante.Ci, "No consignado");
        if (!string.IsNullOrWhiteSpace(ciTexto) && ciTexto != "No consignado")
            solicitanteTexto += $" (CI/NIT: {ciTexto})";
        Fila(html, "Solicitante", solicitanteTexto);

        var profesionalTexto = Valor(cotizacion.Profesional, "No consignado");
        var matProfTexto = Valor(cotizacion.MatriculaProfesional, "No consignada");
        if (!string.IsNullOrWhiteSpace(matProfTexto) && matProfTexto != "No consignada")
            profesionalTexto += $" (Mat. prof.: {matProfTexto})";
        Fila(html, "Profesional", profesionalTexto);

        Fila(html, "Dirección", Valor(cotizacion.Solicitante.Direccion, "No consignada"));
        Fila(html, "Municipio", Valor(cotizacion.Solicitante.Municipio, "Oruro"));
        html.Append("</tbody></table>");

        html.Append("<table class=\"desglose\"><thead><tr>");
        html.Append("<th>Área</th><th>Tipología / Concepto</th><th>Superficie m²</th>");
        html.Append("<th>h<sub>(k,r)</sub></th><th>Fa</th><th>Fj</th><th>Subtotal (Bs)</th>");
        html.Append("</tr></thead><tbody><tr>");

        html.Append("<td>").Append(Escape(area.Codigo)).Append("</td>");
        html.Append("<td>").Append(Escape(Valor(resultado.Tipologia, resultado.Concepto))).Append("</td>");
        html.Append("<td class=\"num\">")
            .Append(resultado.SuperficieM2 is null
                ? "-"
                : FormatoBoliviano.Decimal(resultado.SuperficieM2.Value, 2))
            .Append("</td>");
        html.Append("<td class=\"num\">").Append(Factor(resultado.FactorH)).Append("</td>");
        html.Append("<td class=\"num\">").Append(Factor(resultado.FactorAlcance)).Append("</td>");
        html.Append("<td class=\"num\">").Append(Factor(resultado.FactorComplejidad)).Append("</td>");
        html.Append("<td class=\"num\">").Append(FormatoBoliviano.Moneda(resultado.Total)).Append("</td>");
        html.Append("</tr></tbody></table>");

        html.Append("<table class=\"desglose\"><thead><tr><th>Concepto</th><th>Detalle</th>");
        html.Append("<th>Cantidad</th><th>Unidad</th><th>Factor</th><th>Subtotal (Bs)</th>");
        html.Append("</tr></thead><tbody>");

        foreach (var linea in resultado.Desglose)
        {
            html.Append("<tr>");
            html.Append("<td>").Append(Escape(linea.Concepto)).Append("</td>");
            html.Append("<td class=\"detalle\">").Append(Escape(linea.Detalle)).Append("</td>");
            html.Append("<td class=\"num\">").Append(FormatoBoliviano.Decimal(linea.Cantidad, 2)).Append("</td>");
            html.Append("<td class=\"centro\">").Append(Escape(linea.Unidad)).Append("</td>");
            html.Append("<td class=\"num\">").Append(Factor(linea.Factor)).Append("</td>");
            html.Append("<td class=\"num\">").Append(FormatoBoliviano.Moneda(linea.Subtotal)).Append("</td>");
            html.Append("</tr>");
        }

        html.Append("</tbody></table>");

        html.Append("<div class=\"formula\"><strong>Fórmula aplicada:</strong> ")
            .Append(Escape(resultado.FormulaResumen)).Append("</div>");

        if (resultado.LimiteMinimoAplicado)
        {
            html.Append("<div class=\"alerta\"><strong>Límite mínimo aplicado.</strong> ")
                .Append("El honorario calculado fue inferior al mínimo reglamentario de Hbase × Fa = ")
                .Append(FormatoBoliviano.Moneda(resultado.LimiteMinimo ?? 0))
                .Append("; se aplicó el valor mínimo.</div>");
        }

        html.Append("<table class=\"total\"><tbody>");
        html.Append("<tr><td>Honorario total</td><td class=\"num\">")
            .Append(FormatoBoliviano.Moneda(resultado.Total)).Append("</td></tr>");

        if (resultado.AportePatrimonioInmobiliario is not null)
        {
            html.Append("<tr><td>Aporte al Patrimonio Inmobiliario (5,5 %)</td><td class=\"num\">")
                .Append(FormatoBoliviano.Moneda(resultado.AportePatrimonioInmobiliario.Value))
                .Append("</td></tr>");
        }

        html.Append("<tr class=\"letras\"><td colspan=\"2\">Son ").Append(Escape(resultado.TotalEnLetras))
            .Append(".-</td></tr>");
        html.Append("</tbody></table>");

        html.Append("<table class=\"parametros\"><thead><tr><th>Parámetro</th><th>Valor</th></tr></thead><tbody>");

        foreach (var parametro in resultado.ParametrosUsados)
        {
            var partes = parametro.Split(" x ", StringSplitOptions.None);
            html.Append("<tr><td>").Append(Escape(partes[0])).Append("</td><td>")
                .Append(Escape(partes.Length > 1 ? partes[1] : string.Empty)).Append("</td></tr>");
        }

        html.Append("</tbody></table>");

        if (resultado.Advertencias.Count > 0)
        {
            html.Append("<div class=\"advertencias\"><strong>Advertencias:</strong><ul>");
            foreach (var advertencia in resultado.Advertencias)
                html.Append("<li>").Append(Escape(advertencia)).Append("</li>");
            html.Append("</ul></div>");
        }

        if (!string.IsNullOrWhiteSpace(cotizacion.Observaciones))
        {
            html.Append("<div class=\"advertencias\"><strong>Observaciones:</strong> ")
                .Append(Escape(cotizacion.Observaciones)).Append("</div>");
        }

        html.Append("<div class=\"firmas\"><div class=\"firma\">Firma del profesional</div>");
        html.Append("<div class=\"firma\">Firma del interesado</div></div>");

        html.Append("<div class=\"legal\">").Append(Escape(TextoLegal)).Append("</div>");
        html.Append("<div class=\"pie\">").Append(NombreInstitucion)
            .Append(" &middot; ").Append(cotizacion.Numero)
            .Append(" &middot; Generado por la Calculadora de Aranceles CAO</div>");

        html.Append("</body></html>");
        return html.ToString();
    }

    private static void Fila(StringBuilder html, string etiqueta, string valor) =>
        html.Append("<tr><th>").Append(Escape(etiqueta)).Append("</th><td>").Append(Escape(valor)).Append("</td></tr>");

    private static string Valor(string? valor, string porDefecto) =>
        string.IsNullOrWhiteSpace(valor) ? porDefecto : valor;

    private static string Factor(double? valor) =>
        valor is null ? "-" : FormatoBoliviano.Decimal(valor.Value, 2);

    private static string Escape(string? texto) =>
        WebUtility.HtmlEncode(texto ?? string.Empty);

    private static string Css() => """
        * { box-sizing: border-box; }
        body { font-family: "Segoe UI", Arial, sans-serif; font-size: 10.5pt; color: #1a1a1a; margin: 0; }
        .membrete { display: flex; align-items: center; border-bottom: 3px solid #14532d; padding-bottom: 10px; margin-bottom: 14px; }
        .escudo { width: 80px; height: 54px; object-fit: contain; margin-right: 12px; flex-shrink: 0; }
        .institucion { flex: 1; }
        .institucion .nombre { font-size: 14pt; font-weight: 700; color: #14532d; text-transform: uppercase; }
        .institucion .sub { font-size: 9pt; color: #444; }
        .institucion .cargo { font-size: 9pt; font-weight: 700; letter-spacing: 1px; margin-top: 3px; }
        .folio { text-align: right; font-size: 9pt; }
        .folio-label { color: #666; }
        .folio-valor { font-weight: 700; font-size: 10.5pt; color: #14532d; margin-bottom: 4px; }
        h1 { font-size: 12pt; text-align: center; margin: 0 0 12px; text-transform: uppercase; letter-spacing: .5px; }
        table { width: 100%; border-collapse: collapse; margin-bottom: 12px; }
        .datos th { width: 32%; text-align: left; background: #f1f5f9; border: 1px solid #cbd5e1; padding: 4px 6px; font-size: 9.5pt; }
        .datos td { border: 1px solid #cbd5e1; padding: 4px 6px; font-size: 9.5pt; }
        .desglose th { background: #14532d; color: #fff; border: 1px solid #14532d; padding: 5px 6px; font-size: 9pt; text-align: left; }
        .desglose td { border: 1px solid #cbd5e1; padding: 4px 6px; font-size: 9.5pt; vertical-align: top; }
        .desglose .num { text-align: right; white-space: nowrap; }
        .desglose .centro { text-align: center; }
        .desglose .detalle { color: #475569; font-size: 8.5pt; }
        .formula { background: #f8fafc; border-left: 3px solid #14532d; padding: 6px 8px; font-size: 8.5pt; margin-bottom: 10px; }
        .alerta { background: #fef3c7; border: 1px solid #f59e0b; padding: 6px 8px; font-size: 9pt; margin-bottom: 10px; }
        .total { margin: 0 0 12px auto; width: 62%; }
        .total td { border: 1px solid #cbd5e1; padding: 5px 6px; font-size: 10pt; }
        .total tr:first-child td { font-weight: 700; font-size: 12pt; background: #f1f5f9; }
        .total .num { text-align: right; }
        .total .letras { font-size: 9pt; font-style: italic; }
        .parametros th { background: #e2e8f0; border: 1px solid #cbd5e1; padding: 4px 6px; font-size: 9pt; text-align: left; }
        .parametros td { border: 1px solid #cbd5e1; padding: 4px 6px; font-size: 9pt; }
        .advertencias { border: 1px dashed #94a3b8; padding: 6px 8px; font-size: 8.5pt; margin-bottom: 10px; }
        .advertencias ul { margin: 4px 0 0; padding-left: 18px; }
        .firmas { display: flex; justify-content: space-between; margin-top: 40px; }
        .firma { width: 45%; text-align: center; border-top: 1px solid #1a1a1a; padding-top: 4px; font-size: 9pt; }
        .legal { margin-top: 22px; font-size: 8pt; color: #475569; border-top: 1px solid #cbd5e1; padding-top: 6px; text-align: justify; }
        .pie { margin-top: 8px; font-size: 7.5pt; color: #94a3b8; text-align: center; }

        @media print {
            @page { size: letter; margin: 12mm 10mm; }
            body { font-size: 9.5pt; color: #000; }
            .membrete { border-bottom: 2px solid #000; }
            .escudo { object-fit: contain; }
            .institucion .nombre { color: #000; }
            .institucion .cargo { color: #000; }
            .folio-valor { color: #000; }
            h1 { color: #000; }
            .datos th { background: #f0f0f0 !important; border-color: #999; color: #000; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
            .datos td { border-color: #999; color: #000; }
            .desglose th { background: #000 !important; color: #fff !important; border-color: #000 !important; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
            .desglose td { border-color: #999; color: #000; }
            .desglose .num { color: #000; }
            .desglose .detalle { color: #333; }
            .formula { background: #f5f5f5; border-left: 3px solid #000; color: #000; }
            .alerta { background: #fffde7; border-color: #000; color: #000; }
            .total { border-color: #999; }
            .total td { border-color: #999; color: #000; }
            .total tr:first-child td { background: #f0f0f0 !important; color: #000 !important; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
            .total .num { color: #000; }
            .total .letras { color: #000; }
            .parametros { display: none !important; }
            .advertencias { border-color: #999; color: #000; }
            .firma { border-top: 1px solid #000; color: #000; }
            .legal { border-top: 1px solid #999; color: #333; }
            .pie { color: #666; }
        }
        """;
}
