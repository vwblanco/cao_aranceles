using CalculadoraArancelesCAO.Core.Models;

namespace CalculadoraArancelesCAO.Tests;

public class ParametrosCAOTests
{
    [Fact]
    public void ValoresOficiales_ProducenCostoHoraYHonorarioBaseEsperados()
    {
        var parametros = ParametrosCAO.PorDefecto();

        Assert.Equal(8935.16, parametros.VariableVida);
        Assert.Equal(160.0, parametros.HorasMensuales);
        Assert.Equal(55.84, parametros.CostoHora, 2);
        Assert.Equal(2233.79, parametros.HonorarioBaseSemanal, 2);
        Assert.Equal(55.84, parametros.CostoHoraMostrado, 2);
        Assert.Equal(2233.79, parametros.HonorarioBaseMostrado, 2);
    }

    [Fact]
    public void ActualizacionDeVariableVida_RecalculaCostoHoraYHonorarioBase()
    {
        var parametros = ParametrosCAO.PorDefecto();
        parametros.VariableVida = 10000.00;

        Assert.Equal(62.50, parametros.CostoHora, 2);
        Assert.Equal(2500.00, parametros.HonorarioBaseSemanal, 2);
        Assert.False(parametros.UsaValoresOficiales);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void VariableVidaInvalida_ESReportada(double valor)
    {
        var parametros = new ParametrosCAO { VariableVida = valor };

        Assert.NotEmpty(parametros.Validar());
    }

    [Fact]
    public void CargaHorariaInferiorAlMinimo_ESReportada()
    {
        var parametros = new ParametrosCAO { HorasMensuales = 20 };

        Assert.NotEmpty(parametros.Validar());
    }

    [Fact]
    public void RestaurarValoresOficiales_RegresaAlReglamento2026()
    {
        var parametros = new ParametrosCAO { VariableVida = 12000, HorasMensuales = 180 };
        parametros.RestaurarValoresOficiales();

        Assert.True(parametros.UsaValoresOficiales);
        Assert.Equal(ParametrosCAO.VariableVidaPorDefecto, parametros.VariableVida);
    }
}
