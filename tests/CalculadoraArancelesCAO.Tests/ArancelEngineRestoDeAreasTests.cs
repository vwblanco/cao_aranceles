using CalculadoraArancelesCAO.Core.Models;
using CalculadoraArancelesCAO.Core.Services;

namespace CalculadoraArancelesCAO.Tests;

public class ArancelEngineRestoDeAreasTests
{
    private static readonly ParametrosCAO P = ParametrosCAO.PorDefecto();
    private readonly ArancelEngine _engine = new();

    [Theory]
    [InlineData(50, 500, 25000, 1375, 26375, false)]
    [InlineData(2, 100, 200, 122.858, 2356.648, true)]
    [InlineData(0.50, 100, 50, 122.858, 2356.648, true)]
    public void Area2A_AplicaMaximoMasAportePatrimonioInmobiliario(
        double precioBsM2, double m2, double esperadoHonorario, double esperadoAporte,
        double esperadoTotal, bool limiteAplicado)
    {
        var resultado = _engine.CalcularArea2A(P, precioBsM2, m2);

        Assert.Equal(esperadoHonorario, resultado.HonorarioCalculado!.Value, 3);
        Assert.Equal(2233.79, resultado.LimiteMinimo!.Value, 2);
        Assert.Equal(esperadoAporte, resultado.AportePatrimonioInmobiliario!.Value, 3);
        Assert.Equal(esperadoTotal, resultado.Total, 3);
        Assert.Equal(limiteAplicado, resultado.LimiteMinimoAplicado);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Area2A_PrecioInvalido_EsRechazado(double precio)
    {
        Assert.Throws<ArgumentException>(() => _engine.CalcularArea2A(P, precio, 500));
    }

    [Fact]
    public void Area2A_SuperficieInvalida_EsRechazada()
    {
        Assert.Throws<ArgumentException>(() => _engine.CalcularArea2A(P, 50, 0));
    }

    [Theory]
    [InlineData(TipoAvaluo.Avaluo, 500000, 1500)]
    [InlineData(TipoAvaluo.Peritaje, 500000, 3000)]
    [InlineData(TipoAvaluo.Dirimicion, 500000, 3000)]
    [InlineData(TipoAvaluo.Avaluo, 125000, 375)]
    [InlineData(TipoAvaluo.Peritaje, 1_000_000, 6000)]
    public void Area4A_AplicaTresOMilesIPorMilSobreElValor(TipoAvaluo tipo, double valor, double esperado)
    {
        var resultado = _engine.CalcularAvaluo(P, tipo, valor);

        Assert.Equal(esperado, resultado.Total, 4);
        Assert.False(resultado.LimiteMinimoAplicado);
        Assert.NotEmpty(resultado.Advertencias);
    }

    [Theory]
    [InlineData(TipoAvaluo.Avaluo, 3.0)]
    [InlineData(TipoAvaluo.Peritaje, 6.0)]
    [InlineData(TipoAvaluo.Dirimicion, 6.0)]
    public void Area4A_TasasPorMil_SonLasDelReglamento(TipoAvaluo tipo, double tasaEsperada)
    {
        var info = _engine.Matrices.ObtenerTipoAvaluo(tipo);

        Assert.Equal(tasaEsperada, info.TasaPorMil, 4);
    }

    [Fact]
    public void Area4A_ValorInvalido_EsRechazado()
    {
        Assert.Throws<ArgumentException>(() => _engine.CalcularAvaluo(P, TipoAvaluo.Avaluo, 0));
    }

    [Theory]
    [InlineData(TipoInspeccion.Simple, 0.50, 1116.895)]
    [InlineData(TipoInspeccion.Tecnica, 0.75, 1675.3425)]
    [InlineData(TipoInspeccion.Integral, 1.50, 3350.685)]
    public void Area4B_AplicaMultiplicadorSobreElHonorarioBase(
        TipoInspeccion tipo, double multiplicador, double esperado)
    {
        var resultado = _engine.CalcularInspeccion(P, tipo);

        Assert.Equal(multiplicador, resultado.FactorComplejidad!.Value, 4);
        Assert.Equal(esperado, resultado.Total, 4);
    }

    [Fact]
    public void Area4B_CantidadMultiplicaElHonorarioUnitario()
    {
        var resultado = _engine.CalcularInspeccion(P, TipoInspeccion.Simple, 4);

        Assert.Equal(2233.79 * 0.50, resultado.Total / 4, 6);
    }

    [Fact]
    public void Area4B_CantidadInvalida_EsRechazada()
    {
        Assert.Throws<ArgumentException>(() => _engine.CalcularInspeccion(P, TipoInspeccion.Simple, 0));
    }

    [Theory]
    [InlineData(SubArea3.Remodelacion, 1.10)]
    [InlineData(SubArea3.Restauracion, 1.20)]
    [InlineData(SubArea3.Patrimonio, 1.25)]
    public void Area3_AplicaElFactorDeComplejidadDeLaSubArea(SubArea3 subArea, double fjEsperado)
    {
        var etapas = EtapaProyectoCatalogo.Todas.Select(e => e.Id);

        var resultado = _engine.CalcularArea3(
            P, subArea, TipologiaEdificio.ViviendaUnifamiliar, 180, PerfilAlcance.Habitual, etapas);

        Assert.Equal(fjEsperado, resultado.FactorComplejidad!.Value, 4);
        Assert.Equal(8935.16 / 160.0 * 180 * 1.29 * fjEsperado, resultado.Total, 6);
    }

    [Theory]
    [InlineData(1, 1.00)]
    [InlineData(2, 1.50)]
    [InlineData(3, 1.90)]
    [InlineData(4, 2.20)]
    [InlineData(5, 2.40)]
    [InlineData(6, 2.50)]
    [InlineData(10, 2.90)]
    [InlineData(11, 2.95)]
    [InlineData(50, 4.90)]
    [InlineData(51, 4.92)]
    [InlineData(60, 5.10)]
    [InlineData(100, 5.90)]
    public void Area6_AplicaLaEscalaDecrecienteDelReglamento(int cantidad, double factorEsperado)
    {
        var resultado = _engine.CalcularArea6(1000, cantidad);

        Assert.Equal(factorEsperado, resultado.FactorAcumulado, 6);
        Assert.Equal(1000 * factorEsperado, resultado.Total, 4);
    }

    [Fact]
    public void Area6_DescomponeLaCantidadEnTramos()
    {
        var resultado = _engine.CalcularArea6(1000, 12);

        Assert.Equal(
        [
            ("1a", 1, 1.00),
            ("2a", 1, 0.50),
            ("3a", 1, 0.40),
            ("4a", 1, 0.30),
            ("5a", 1, 0.20),
            ("6a a 10a", 5, 0.10),
            ("11a a 50a", 2, 0.05)
        ], resultado.Tramos.Select(t => (t.Tramo, t.Cantidad, t.Porcentaje)).ToArray());
    }

    [Fact]
    public void Area6_DesdeProyecto_UsaElHonorarioDeLaUnidadBase()
    {
        var etapas = EtapaProyectoCatalogo.Todas.Select(e => e.Id);

        var resultado = _engine.CalcularArea6DesdeProyecto(
            P, TipologiaEdificio.ViviendaUnifamiliar, 180, PerfilAlcance.Habitual, etapas, 10);

        Assert.Equal(12967.15095, resultado.ValorUnidadBase, 5);
        Assert.Equal(12967.15095 * 2.90, resultado.Total, 4);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(100, 0)]
    public void Area6_EntradasInvalidas_SonRechazadas(double valorBase, int cantidad)
    {
        Assert.Throws<ArgumentException>(() => _engine.CalcularArea6(valorBase, cantidad));
    }

    [Fact]
    public void EscalaArea6_ConvertibleEnResultadoDeArancelParaElReporte()
    {
        var resultado = _engine.CalcularArea6(1000, 3).ToResultadoArancel();

        Assert.Equal(AreaArancel.Area6, resultado.Area);
        Assert.Equal(1900, resultado.Total, 4);
        Assert.NotEmpty(resultado.Desglose);
        Assert.NotEmpty(resultado.TotalEnLetras);
    }
}
