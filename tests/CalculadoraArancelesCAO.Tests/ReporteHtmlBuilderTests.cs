using CalculadoraArancelesCAO.Core.Models;
using CalculadoraArancelesCAO.Core.Services;

namespace CalculadoraArancelesCAO.Tests;

public class ReporteHtmlBuilderTests
{
    private readonly ArancelEngine _motor = new();
    private readonly MatrixRepository _matrices = new();

    private ReporteCotizacion Cotizacion(TipoAvaluo tipo = TipoAvaluo.Avaluo)
    {
        var resultado = tipo == TipoAvaluo.Avaluo
            ? _motor.CalcularAvaluo(ParametrosCAO.PorDefecto(), tipo, 500000)
            : _motor.CalcularArea1(
                ParametrosCAO.PorDefecto(),
                TipologiaEdificio.ViviendaUnifamiliar,
                180,
                PerfilAlcance.Habitual,
                _matrices.Etapas.Select(e => e.Id));

        return new ReporteCotizacion
        {
            Numero = "CAO-2026-00001",
            Fecha = new DateTimeOffset(2026, 3, 14, 10, 30, 0, TimeSpan.Zero),
            Resultado = resultado,
            Parametros = ParametrosCAO.PorDefecto(),
            Solicitante = new DatosSolicitante
            {
                Solicitante = "Juan Pérez <script>alert(1)</script>",
                Ci = "1234567 LP",
                Matricula = "A-1234",
                Direccion = "Av. Ballivian 123",
                Municipio = "Oruro"
            }
        };
    }

    [Fact]
    public void Construir_IncluyeElMembreteInstitucional()
    {
        var html = ReporteHtmlBuilder.Construir(Cotizacion());

        Assert.Contains(ReporteHtmlBuilder.NombreInstitucion, html);
        Assert.Contains("COMPUTACIÓN DE ARANCELES", html);
        Assert.Contains("Cotización N.°", html);
        Assert.Contains("CAO-2026-00001", html);
        Assert.Contains("14/03/2026", html);
    }

    [Fact]
    public void Construir_IncluyeLaTablaDeDesgloseYElLiteral()
    {
        var cotizacion = Cotizacion(TipoAvaluo.Peritaje);
        var html = ReporteHtmlBuilder.Construir(cotizacion);

        Assert.Contains("h<sub>(k,r)</sub>", html);
        Assert.Contains("Fa", html);
        Assert.Contains("Subtotal (Bs)", html);
        Assert.Contains(cotizacion.Resultado.TotalEnLetras, html);
        Assert.Contains("Bs 12.967,15", html);
        Assert.Contains("Vivienda Unifamiliar", html);
    }

    [Fact]
    public void Construir_IncluyeElAvisoLegalDelReglamentoYLey1373()
    {
        var html = ReporteHtmlBuilder.Construir(Cotizacion());

        Assert.Contains("Reglamento de Aranceles 2026", html);
        Assert.Contains("Ley N. 1373", html);
    }

    [Fact]
    public void Construir_ResaltaElLimiteMinimoCuandoAplica()
    {
        var resultado = _motor.CalcularArea1(
            ParametrosCAO.PorDefecto(),
            TipologiaEdificio.ViviendaUnifamiliar,
            20,
            PerfilAlcance.Habitual,
            _matrices.Etapas.Select(e => e.Id));

        var cotizacion = Cotizacion();
        cotizacion.Resultado = resultado;

        var html = ReporteHtmlBuilder.Construir(cotizacion);

        Assert.Contains("Límite mínimo aplicado", html);
        Assert.Contains("Bs 2.233,79", html);
    }

    [Fact]
    public void Construir_EscapaElContenidoProporcionadoPorElUsuario()
    {
        var html = ReporteHtmlBuilder.Construir(Cotizacion());

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void Construir_GeneraDocumentoHtmlCompleto()
    {
        var html = ReporteHtmlBuilder.Construir(Cotizacion());

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.EndsWith("</html>", html);
        Assert.Contains("Solicitante", html);
        Assert.Contains("Son ", html);
    }

    [Fact]
    public void Construir_ListaLosParametrosUsados()
    {
        var html = ReporteHtmlBuilder.Construir(Cotizacion());

        Assert.Contains("Variable Vida (V)", html);
        Assert.Contains("Honorario base semanal", html);
    }
}
