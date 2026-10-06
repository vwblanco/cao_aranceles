using CalculadoraArancelesCAO.Core.Models;

namespace CalculadoraArancelesCAO.Core.Services;

public sealed class MatrixRepository
{
    public IReadOnlyList<TipologiaInfo> Tipologias => TipologiaCatalogo.Todas;

    public IReadOnlyList<RangoSuperficieInfo> Rangos => RangoSuperficieCatalogo.Todos;

    public IReadOnlyList<PerfilAlcanceInfo> Perfiles => PerfilAlcanceCatalogo.Todos;

    public IReadOnlyList<EtapaProyectoInfo> Etapas => EtapaProyectoCatalogo.Todas;

    public IReadOnlyList<AreaArancelInfo> Areas => AreaArancelCatalogo.Todas;

    public IReadOnlyList<ItemNavegacion> ItemsNavegacion => new[]
    {
        new ItemNavegacion("1", "Servicios de arquitectura", "area1", "area"),
        new ItemNavegacion("2A", "Lotes y fraccionamiento", "area2a", "area"),
        new ItemNavegacion("3", "Remodelación y patrimonio", "area3", "area"),
        new ItemNavegacion("4A", "Avalúos y peritajes", "area4a", "area"),
        new ItemNavegacion("4B", "Inspecciones y auditorías", "area4b", "area"),
        new ItemNavegacion("6", "Plantas tipo", "area6", "area"),
        new ItemNavegacion("ST", "Staff", "staff-admin", "admin"),
        new ItemNavegacion("RG", "Reglamento 2026", "reglamento", "reglamento"),
        new ItemNavegacion("TR", "Trámites", "tramites", "tramites"),
        new ItemNavegacion("AN", "Anexos", "matrices", "anexos")
    };

    public IReadOnlyList<SubArea3Info> SubAreasArea3 => SubAreasCatalogo.Area3;

    public IReadOnlyList<TipoAvaluoInfo> TiposAvaluo => SubAreasCatalogo.Area4A;

    public IReadOnlyList<TipoInspeccionInfo> TiposInspeccion => SubAreasCatalogo.Area4B;

    public IReadOnlyList<TramoEscalaArea6> EscalaArea6 => EscalaArea6Catalogo.Tramos;

    public TipologiaInfo ObtenerTipologia(TipologiaEdificio tipologia) =>
        TipologiaCatalogo.Obtener(tipologia);

    public RangoSuperficieInfo ObtenerRango(double m2) => RangoSuperficieCatalogo.Resolver(m2);

    public RangoSuperficieInfo ObtenerRango(RangoSuperficie rango) =>
        RangoSuperficieCatalogo.Obtener(rango);

    public PerfilAlcanceInfo ObtenerPerfil(PerfilAlcance perfil) =>
        PerfilAlcanceCatalogo.Obtener(perfil);

    public EtapaProyectoInfo ObtenerEtapa(EtapaProyecto etapa) =>
        EtapaProyectoCatalogo.Obtener(etapa);

    public AreaArancelInfo ObtenerArea(AreaArancel area) => AreaArancelCatalogo.Obtener(area);

    public SubArea3Info ObtenerSubArea3(SubArea3 sub) => SubAreasCatalogo.ObtenerArea3(sub);

    public TipoAvaluoInfo ObtenerTipoAvaluo(TipoAvaluo tipo) => SubAreasCatalogo.ObtenerAvaluo(tipo);

    public TipoInspeccionInfo ObtenerTipoInspeccion(TipoInspeccion tipo) =>
        SubAreasCatalogo.ObtenerInspeccion(tipo);

    /// <summary>
    /// Devuelve la matriz completa h(k,r) del Anexo A para la tipologia indicada,
    /// en el orden de los cinco rangos de superficie del reglamento.
    /// </summary>
    public IReadOnlyList<RangoFactorH> ObtenerMatrizAnexoA(TipologiaEdificio tipologia)
    {
        var info = ObtenerTipologia(tipologia);
        return Rangos
            .Select((rango, indice) => new RangoFactorH(rango, info.MatrizH[indice]))
            .ToList();
    }

    /// <summary>Factor h(k,r) para una tipologia y una superficie dadas.</summary>
    public (double Factor, RangoSuperficieInfo Rango) ObtenerFactorH(TipologiaEdificio tipologia, double m2)
    {
        var rango = ObtenerRango(m2);
        return (ObtenerTipologia(tipologia).ObtenerFactorH(rango.Id), rango);
    }

    /// <summary>Factor de alcance Fa acumulado por las etapas efectivamente contratadas.</summary>
    public double CalcularFactorAlcance(PerfilAlcance perfil, IEnumerable<EtapaProyecto> etapas)
    {
        var info = ObtenerPerfil(perfil);
        return etapas.Distinct().Sum(info.ObtenerPeso);
    }

    public IReadOnlyList<LineaAlcance> ObtenerDesgloseAlcance(PerfilAlcance perfil, IEnumerable<EtapaProyecto> etapas)
    {
        var info = ObtenerPerfil(perfil);
        var contratadas = etapas.Distinct().ToHashSet();

        return Etapas
            .Select(etapa => new LineaAlcance(
                etapa,
                info.ObtenerPeso(etapa.Id),
                contratadas.Contains(etapa.Id)))
            .ToList();
    }

    public double FactorAlcanceMaximoTeorico(PerfilAlcance perfil) =>
        ObtenerPerfil(perfil).PesosPorEtapa.Values.Sum();
}

public sealed record RangoFactorH(RangoSuperficieInfo Rango, double Factor);

public sealed record LineaAlcance(EtapaProyectoInfo Etapa, double Peso, bool Contratada);

public sealed record ItemNavegacion(string Codigo, string Nombre, string Ruta, string Tipo);
