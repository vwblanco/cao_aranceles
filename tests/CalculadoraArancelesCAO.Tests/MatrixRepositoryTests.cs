using CalculadoraArancelesCAO.Core.Models;
using CalculadoraArancelesCAO.Core.Services;

namespace CalculadoraArancelesCAO.Tests;

public class MatrixRepositoryTests
{
    private readonly MatrixRepository _repo = new();

    [Theory]
    [InlineData(RangoSuperficie.Hasta100, 1)]
    [InlineData(RangoSuperficie.Hasta100, 100)]
    [InlineData(RangoSuperficie.De101a250, 100.01)]
    [InlineData(RangoSuperficie.De101a250, 101)]
    [InlineData(RangoSuperficie.De101a250, 250)]
    [InlineData(RangoSuperficie.De251a600, 250.01)]
    [InlineData(RangoSuperficie.De251a600, 600)]
    [InlineData(RangoSuperficie.De601a1500, 600.01)]
    [InlineData(RangoSuperficie.De601a1500, 1500)]
    [InlineData(RangoSuperficie.Mas1500, 1500.01)]
    [InlineData(RangoSuperficie.Mas1500, 12000)]
    public void LimitesDeRangoDeSuperficie_SonExclusivosEnElMinimoEInclusivosEnElMaximo(
        RangoSuperficie rangoEsperado, double m2)
    {
        var info = _repo.ObtenerRango(m2);

        Assert.Equal(rangoEsperado, info.Id);
    }

    [Fact]
    public void MatrizAnexoA_ViviendaUnifamiliar_CoincideConElReglamento()
    {
        var esperado = new[] { 1.50, 1.29, 1.12, 1.00, 0.92 };

        var matriz = _repo.ObtenerMatrizAnexoA(TipologiaEdificio.ViviendaUnifamiliar);

        Assert.Equal(esperado, matriz.Select(m => m.Factor).ToArray());
    }

    [Fact]
    public void MatrizAnexoA_ComercioOficinas_CoincideConElReglamento()
    {
        var esperado = new[] { 1.67, 1.46, 1.29, 1.17, 1.08 };

        var matriz = _repo.ObtenerMatrizAnexoA(TipologiaEdificio.ComercioOficinas);

        Assert.Equal(esperado, matriz.Select(m => m.Factor).ToArray());
    }

    [Fact]
    public void MatrizAnexoA_MultifamiliarEquipamiento_CoincideConElReglamento()
    {
        var esperado = new[] { 1.83, 1.62, 1.46, 1.29, 1.21 };

        var matriz = _repo.ObtenerMatrizAnexoA(TipologiaEdificio.MultifamiliarEquipamiento);

        Assert.Equal(esperado, matriz.Select(m => m.Factor).ToArray());
    }

    [Fact]
    public void MatrizAnexoA_IndustriaEspeciales_CoincideConElReglamento()
    {
        var esperado = new[] { 2.00, 1.75, 1.58, 1.42, 1.29 };

        var matriz = _repo.ObtenerMatrizAnexoA(TipologiaEdificio.IndustriaEspeciales);

        Assert.Equal(esperado, matriz.Select(m => m.Factor).ToArray());
    }

    [Fact]
    public void MatrizAnexoA_CubreLosCincoRangosParaCadaTipologia()
    {
        foreach (var tipologia in TipologiaCatalogo.Todas)
            Assert.Equal(5, _repo.ObtenerMatrizAnexoA(tipologia.Id).Count);
    }

    [Fact]
    public void ObtenerFactorH_UsaElRangoDeLaSuperficieIndicada()
    {
        var (factor, rango) = _repo.ObtenerFactorH(TipologiaEdificio.ViviendaUnifamiliar, 180);

        Assert.Equal(RangoSuperficie.De101a250, rango.Id);
        Assert.Equal(1.29, factor, 4);
    }

    [Theory]
    [InlineData(PerfilAlcance.AltaComplejidad)]
    [InlineData(PerfilAlcance.Habitual)]
    [InlineData(PerfilAlcance.Simple)]
    public void FactorAlcance_ConTodasLasEtapasEsUnoParaCadaPerfil(PerfilAlcance perfil)
    {
        var etapas = EtapaProyectoCatalogo.Todas.Select(e => e.Id);

        var fa = _repo.CalcularFactorAlcance(perfil, etapas);

        Assert.Equal(1.00, fa, 6);
        Assert.Equal(1.00, _repo.FactorAlcanceMaximoTeorico(perfil), 6);
    }

    [Fact]
    public void FactorAlcance_EsAcumulativoPorEtapaContratada()
    {
        var perfil = PerfilAlcance.AltaComplejidad;
        var info = _repo.ObtenerPerfil(perfil);

        var fa = _repo.CalcularFactorAlcance(perfil,
            [EtapaProyecto.Anteproyecto, EtapaProyecto.PlanosDeDetalle]);

        Assert.Equal(info.ObtenerPeso(EtapaProyecto.Anteproyecto) +
                     info.ObtenerPeso(EtapaProyecto.PlanosDeDetalle), fa, 6);
    }

    [Fact]
    public void FactorAlcance_NoCuentaEtapasRepetidas()
    {
        var etapas = new[]
        {
            EtapaProyecto.Anteproyecto, EtapaProyecto.Anteproyecto, EtapaProyecto.Anteproyecto
        };

        var fa = _repo.CalcularFactorAlcance(PerfilAlcance.Habitual, etapas);

        Assert.Equal(0.22, fa, 6);
    }

    [Fact]
    public void DesgloseDeAlcance_MarcaLasEtapasContratadas()
    {
        var desglose = _repo.ObtenerDesgloseAlcance(
            PerfilAlcance.Simple, [EtapaProyecto.Anteproyecto]);

        Assert.Equal(6, desglose.Count);
        Assert.Single(desglose, l => l.Contratada);
        Assert.All(desglose, l => Assert.True(l.Peso > 0));
    }

    [Fact]
    public void Catálogos_deAreasYCatalogos_EstanCompletos()
    {
        Assert.Equal(6, _repo.Areas.Count);
        Assert.Equal(4, _repo.Tipologias.Count);
        Assert.Equal(5, _repo.Rangos.Count);
        Assert.Equal(3, _repo.Perfiles.Count);
        Assert.Equal(6, _repo.Etapas.Count);
        Assert.Equal(3, _repo.SubAreasArea3.Count);
        Assert.Equal(3, _repo.TiposAvaluo.Count);
        Assert.Equal(3, _repo.TiposInspeccion.Count);
        Assert.Equal(8, _repo.EscalaArea6.Count);
    }
}
