using CalculadoraArancelesCAO.Core.Models;
using CalculadoraArancelesCAO.Core.Services;

namespace CalculadoraArancelesCAO.Tests;

public class ArancelEngineArea1Tests
{
    private static readonly ParametrosCAO P = ParametrosCAO.PorDefecto();
    private readonly ArancelEngine _engine = new();

    private static EtapaProyecto[] TodasLasEtapas =>
        [.. EtapaProyectoCatalogo.Todas.Select(e => e.Id)];

    [Fact]
    public void EjemploDeVerificacion_DocenaViviendaUnifamiliarDe180m2()
    {
        var resultado = _engine.CalcularArea1(
            P,
            TipologiaEdificio.ViviendaUnifamiliar,
            180,
            PerfilAlcance.Habitual,
            TodasLasEtapas);

        Assert.Equal(1.29, resultado.FactorH!.Value, 3);
        Assert.Equal(1.00, resultado.FactorAlcance!.Value, 3);
        Assert.Equal(1.00, resultado.FactorComplejidad!.Value, 3);
        Assert.Equal(55.84475, resultado.CostoHora!.Value, 5);
        Assert.Equal(2233.79, resultado.HonorarioBaseSemanal!.Value, 2);

        var esperado = 8935.16 / 160.0 * 180 * 1.29 * 1.0 * 1.0;
        Assert.Equal(esperado, resultado.HonorarioCalculado!.Value, 6);
        Assert.Equal(12967.15095, resultado.HonorarioCalculado!.Value, 5);
        Assert.Equal(2233.79, resultado.LimiteMinimo!.Value, 2);

        Assert.False(resultado.LimiteMinimoAplicado);
        Assert.Equal(12967.15095, resultado.Total, 5);
        Assert.Equal("Doce mil novecientos sesenta y siete con 15/100", resultado.TotalEnLetras);
    }

    [Theory]
    [InlineData(180, 12967.15095)]
    [InlineData(50, 4188.35625)]
    [InlineData(500, 31273.06)]
    public void HonorarioCalculado_AplicaFormulaDelReglamento(double m2, double esperado)
    {
        var resultado = _engine.CalcularArea1(
            P, TipologiaEdificio.ViviendaUnifamiliar, m2, PerfilAlcance.Habitual, TodasLasEtapas);

        Assert.Equal(esperado, resultado.HonorarioCalculado!.Value, 4);
    }

    [Fact]
    public void SuperficieReducida_ActivaElLimiteMinimo_HbasePorFa()
    {
        var resultado = _engine.CalcularArea1(
            P, TipologiaEdificio.ViviendaUnifamiliar, 20, PerfilAlcance.Habitual, TodasLasEtapas);

        Assert.Equal(1675.3425, resultado.HonorarioCalculado!.Value, 4);
        Assert.Equal(2233.79, resultado.LimiteMinimo!.Value, 2);
        Assert.True(resultado.LimiteMinimoAplicado);
        Assert.Equal(2233.79, resultado.Total, 2);
    }

    [Fact]
    public void LimiteMinimo_TambienEsProporcionalAlFactorDeAlcance()
    {
        var etapas = new[] { EtapaProyecto.EstudiosPreliminares, EtapaProyecto.Anteproyecto };
        var fa = _engine.Matrices.CalcularFactorAlcance(PerfilAlcance.Habitual, etapas);

        var resultado = _engine.CalcularArea1(
            P, TipologiaEdificio.ViviendaUnifamiliar, 20, PerfilAlcance.Habitual, etapas);

        Assert.Equal(0.40, fa, 2);
        Assert.Equal(893.516, resultado.LimiteMinimo!.Value, 3);
        Assert.Equal(2233.79 * 0.40, resultado.Total, 3);
        Assert.True(resultado.LimiteMinimoAplicado);
    }

    [Fact]
    public void SeleccionParcialDeEtapas_ReduceElFactorDeAlcance()
    {
        var resultado = _engine.CalcularArea1(
            P, TipologiaEdificio.ViviendaUnifamiliar, 180, PerfilAlcance.Habitual,
            [EtapaProyecto.EstudiosPreliminares]);

        Assert.Equal(0.18, resultado.FactorAlcance!.Value, 3);
        Assert.Equal(8935.16 / 160.0 * 180 * 1.29 * 0.18, resultado.HonorarioCalculado!.Value, 6);
    }

    [Fact]
    public void FactorAlcanceManual_ReemplazaLaSeleccionDeEtapas()
    {
        var resultado = _engine.CalcularArea1(
            P, TipologiaEdificio.ViviendaUnifamiliar, 180, PerfilAlcance.Habitual,
            TodasLasEtapas, 0.75);

        Assert.Equal(0.75, resultado.FactorAlcance!.Value, 3);
        Assert.Equal(8935.16 / 160.0 * 180 * 1.29 * 0.75, resultado.HonorarioCalculado!.Value, 6);
    }

    [Fact]
    public void FactorAlcanceSuperiorAlPerfil_GeneraAdvertencia()
    {
        var resultado = _engine.CalcularArea1(
            P, TipologiaEdificio.ViviendaUnifamiliar, 180, PerfilAlcance.Simple,
            TodasLasEtapas, 1.25);

        Assert.NotEmpty(resultado.Advertencias);
    }

    [Fact]
    public void SinEtapasContratadas_EsRechazado()
    {
        var excepcion = Assert.Throws<ArgumentException>(() =>
            _engine.CalcularArea1(P, TipologiaEdificio.ViviendaUnifamiliar, 180, PerfilAlcance.Habitual, []));

        Assert.Contains("etapa", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    [InlineData(double.NaN)]
    public void SuperficieInvalida_EsRechazada(double m2)
    {
        Assert.Throws<ArgumentException>(() =>
            _engine.CalcularArea1(P, TipologiaEdificio.ViviendaUnifamiliar, m2, PerfilAlcance.Habitual, TodasLasEtapas));
    }

    [Fact]
    public void ParametrosInvalidos_SonRechazados()
    {
        var parametros = new ParametrosCAO { VariableVida = -1 };

        Assert.Throws<ArgumentException>(() =>
            _engine.CalcularArea1(parametros, TipologiaEdificio.ViviendaUnifamiliar, 180,
                PerfilAlcance.Habitual, TodasLasEtapas));
    }

    [Fact]
    public void Desglose_ExponeLaCadenaDeCalculoConSubtotalesAcumulados()
    {
        var resultado = _engine.CalcularArea1(
            P, TipologiaEdificio.ViviendaUnifamiliar, 180, PerfilAlcance.Habitual, TodasLasEtapas);

        Assert.Equal(5, resultado.Desglose.Count);
        Assert.Equal(8935.16 / 160.0 * 180 * 1.29, resultado.Desglose[2].Subtotal, 6);
        Assert.Equal(resultado.Total, resultado.Desglose[^1].Subtotal, 6);
    }

    [Fact]
    public void VariableVidaActualizada_EscalaTodoElResultado()
    {
        var parametros = new ParametrosCAO { VariableVida = 10000 };

        var oficial = _engine.CalcularArea1(P, TipologiaEdificio.ViviendaUnifamiliar, 180,
            PerfilAlcance.Habitual, TodasLasEtapas);
        var actualizado = _engine.CalcularArea1(parametros, TipologiaEdificio.ViviendaUnifamiliar, 180,
            PerfilAlcance.Habitual, TodasLasEtapas);

        Assert.Equal(oficial.Total * (10000 / 8935.16), actualizado.Total, 6);
    }
}
