using CalculadoraArancelesCAO.Core.Models;

namespace CalculadoraArancelesCAO.Core.Services;

/// <summary>
/// Motor de cálculo de aranceles del Reglamento 2026 del Colegio de Arquitectos de Oruro.
/// Implementa las Areas 1, 2A, 3, 4A, 4B y 6 con sus límites mínimos.
/// </summary>
public sealed class ArancelEngine
{
    /// <summary>Aporte al Patrimonio Inmobiliario aplicado en el Area 2A.</summary>
    public const double AportePatrimonioInmobiliario = 0.055;

    private const double Tolerancia = 0.0000001;

    private readonly MatrixRepository _matrices;

    public ArancelEngine() : this(new MatrixRepository())
    {
    }

    public ArancelEngine(MatrixRepository matrices) => _matrices = matrices;

    public MatrixRepository Matrices => _matrices;

    /// <summary>Area 1: Servicios de arquitectura.</summary>
    public ResultadoArancel CalcularArea1(
        ParametrosCAO parametros,
        TipologiaEdificio tipologia,
        double superficieM2,
        PerfilAlcance perfil,
        IEnumerable<EtapaProyecto> etapas,
        double? factorAlcanceManual = null) =>
        CalcularProyecto(
            parametros,
            AreaArancel.Area1,
            "Servicios de arquitectura",
            tipologia,
            superficieM2,
            perfil,
            etapas,
            factorAlcanceManual,
            1.0,
            "Sin complejidad adicional");

    /// <summary>Area 3: Remodelacion (3A), Restauracion (3B) y Patrimonio (3C).</summary>
    public ResultadoArancel CalcularArea3(
        ParametrosCAO parametros,
        SubArea3 subArea,
        TipologiaEdificio tipologia,
        double superficieM2,
        PerfilAlcance perfil,
        IEnumerable<EtapaProyecto> etapas,
        double? factorAlcanceManual = null)
    {
        var info = _matrices.ObtenerSubArea3(subArea);

        return CalcularProyecto(
            parametros,
            AreaArancel.Area3,
            $"{info.Nombre} ({info.Codigo})",
            tipologia,
            superficieM2,
            perfil,
            etapas,
            factorAlcanceManual,
            info.Fj,
            info.Nombre);
    }

    /// <summary>Area 2A: Lotes y fraccionamiento, con aporte al Patrimonio Inmobiliario.</summary>
    public ResultadoArancel CalcularArea2A(ParametrosCAO parametros, double precioBsM2, double superficieM2)
    {
        ValidarParametros(parametros);

        ValidarNumeroPositivo(precioBsM2, nameof(precioBsM2));
        ValidarNumeroPositivo(superficieM2, nameof(superficieM2));

        var honorarioBase = parametros.HonorarioBaseSemanal;
        var calculado = precioBsM2 * superficieM2;
        var limiteMinimo = honorarioBase;
        var final = Math.Max(calculado, limiteMinimo);
        var limiteAplicado = limiteMinimo > calculado + Tolerancia;
        var aporte = final * AportePatrimonioInmobiliario;

        var advertencias = new List<string>();

        if (precioBsM2 < 1)
            advertencias.Add("El precio por metro cuadrado es referencial y podria no reflejar el valor comercial del lote.");

        var desglose = new List<LineaDesglose>
        {
            new("Precio comercial", "Valor unitario declarado por el solicitante", precioBsM2, "Bs/m²", null, precioBsM2),
            new("Superficie", "Superficie total del lote", superficieM2, "m²", precioBsM2, calculado),
            new("Honorario calculado", "Precio Bs/m² x M²", 1, "Bs", null, calculado),
            new("Limite minimo (Hbase)", "V / 4", 1, "Bs", null, limiteMinimo)
        };

        if (limiteAplicado)
            desglose.Add(new LineaDesglose("Ajuste por limite minimo", "Hfinal = Hbase", 1, "Bs", null, limiteMinimo - calculado));

        desglose.Add(LineaDesglose.Simple("Honorario final (Hfinal)", "max(Precio x M2, Hbase)", final));
        desglose.Add(LineaDesglose.Simple(
            "Aporte al Patrimonio Inmobiliario", "5,5 % del honorario final", aporte));

        return new ResultadoArancel
        {
            Area = AreaArancel.Area2A,
            Concepto = "Lotes y fraccionamiento",
            SuperficieM2 = superficieM2,
            FactorAlcance = 1.0,
            CostoHora = parametros.CostoHora,
            HonorarioBaseSemanal = honorarioBase,
            HonorarioCalculado = calculado,
            LimiteMinimo = limiteMinimo,
            Total = final + aporte,
            AportePatrimonioInmobiliario = aporte,
            LimiteMinimoAplicado = limiteAplicado,
            Desglose = desglose,
            Advertencias = advertencias,
            ParametrosUsados = DescribirParametros(parametros),
            FormulaResumen =
                $"Hcalc = {FormatoBoliviano.Numero(precioBsM2)} Bs/m2 x {FormatoBoliviano.Decimal(superficieM2, 2)} m2 = " +
                $"{FormatoBoliviano.Moneda(calculado)} | Hmin = {FormatoBoliviano.Moneda(limiteMinimo)} | " +
                $"Hfinal = {FormatoBoliviano.Moneda(final)} | Aporte PI = Hfinal x 0,055 = {FormatoBoliviano.Moneda(aporte)}"
        };
    }

    /// <summary>Area 4A: Avaluos (3/1000), peritajes y dirimiciones (6/1000).</summary>
    public ResultadoArancel CalcularAvaluo(ParametrosCAO parametros, TipoAvaluo tipo, double valorInmueble)
    {
        ValidarParametros(parametros);

        ValidarNumeroPositivo(valorInmueble, nameof(valorInmueble));

        var info = _matrices.ObtenerTipoAvaluo(tipo);
        var tasa = info.TasaPorMil / 1000.0;
        var honorario = valorInmueble * tasa;

        var advertencias = new List<string>();
        advertencias.Add("El valor comercial del inmueble es declarado por el solicitante y no fue verificado por el CAO.");

        var desglose = new List<LineaDesglose>
        {
            new("Valor comercial del inmueble", "Valor declarado", valorInmueble, "Bs", null, valorInmueble),
            new("Tasa por mil", $"{FormatoBoliviano.Decimal(info.TasaPorMil, 0)} por mil", 1, "factor", tasa, honorario),
            LineaDesglose.Simple("Honorario", info.Formula, honorario)
        };

        return new ResultadoArancel
        {
            Area = AreaArancel.Area4A,
            Concepto = info.Nombre,
            CostoHora = parametros.CostoHora,
            HonorarioBaseSemanal = parametros.HonorarioBaseSemanal,
            Total = honorario,
            Desglose = desglose,
            Advertencias = advertencias,
            ParametrosUsados = DescribirParametros(parametros),
            FormulaResumen =
                $"{info.Nombre} = {FormatoBoliviano.Moneda(valorInmueble)} x {FormatoBoliviano.Decimal(info.TasaPorMil, 0)}/1000 = " +
                $"{FormatoBoliviano.Moneda(honorario)}"
        };
    }

    /// <summary>Area 4B: Inspecciones y auditorias por multiplicador del honorario base.</summary>
    public ResultadoArancel CalcularInspeccion(ParametrosCAO parametros, TipoInspeccion tipo, int cantidad = 1)
    {
        ValidarParametros(parametros);

        if (cantidad < 1)
            throw new ArgumentException("La cantidad debe ser al menos uno.", nameof(cantidad));

        var info = _matrices.ObtenerTipoInspeccion(tipo);
        var honorarioBase = parametros.HonorarioBaseSemanal;
        var unitario = honorarioBase * info.Multiplicador;
        var total = unitario * cantidad;

        var desglose = new List<LineaDesglose>
        {
            new("Honorario base semanal", "V / 4", honorarioBase, "Bs", null, honorarioBase),
            new("Multiplicador", info.Nombre, info.Multiplicador, "factor", null, unitario),
            LineaDesglose.Simple("Honorario total", $"{cantidad} x {FormatoBoliviano.Moneda(unitario)}", total)
        };

        return new ResultadoArancel
        {
            Area = AreaArancel.Area4B,
            Concepto = info.Nombre,
            FactorComplejidad = info.Multiplicador,
            CostoHora = parametros.CostoHora,
            HonorarioBaseSemanal = honorarioBase,
            HonorarioCalculado = unitario,
            Total = total,
            Desglose = desglose,
            ParametrosUsados = DescribirParametros(parametros),
            FormulaResumen =
                $"Honorario = Hbase {FormatoBoliviano.Moneda(honorarioBase)} x {FormatoBoliviano.Decimal(info.Multiplicador, 2)} " +
                $"= {FormatoBoliviano.Moneda(unitario)} | Total ({cantidad}) = {FormatoBoliviano.Moneda(total)}"
        };
    }

    /// <summary>Area 6: escala decreciente sobre el valor de la unidad base.</summary>
    public ResultadoEscala CalcularArea6(double valorUnidadBase, int cantidad)
    {
        ValidarNumeroPositivo(valorUnidadBase, nameof(valorUnidadBase));

        if (cantidad < 1)
            throw new ArgumentException("La cantidad de unidades debe ser al menos una.", nameof(cantidad));

        var tramos = EscalaArea6Catalogo.Tramos
            .Select(tramo => new
            {
                Tramo = tramo,
                Aplicadas = tramo.CuantasAplica(cantidad)
            })
            .Where(x => x.Aplicadas > 0)
            .Select(x => new LineaEscala(
                x.Tramo.Tramo,
                x.Aplicadas,
                x.Tramo.Porcentaje,
                valorUnidadBase * x.Aplicadas * x.Tramo.Porcentaje))
            .ToList();

        return new ResultadoEscala
        {
            ValorUnidadBase = valorUnidadBase,
            Cantidad = cantidad,
            Tramos = tramos
        };
    }

    /// <summary>
    /// Area 6 sobre el valor de la unidad base derivado de un proyecto de arquitectura.
    /// </summary>
    public ResultadoEscala CalcularArea6DesdeProyecto(
        ParametrosCAO parametros,
        TipologiaEdificio tipologia,
        double superficieM2,
        PerfilAlcance perfil,
        IEnumerable<EtapaProyecto> etapas,
        int cantidad,
        double? factorAlcanceManual = null)
    {
        var unidad = CalcularArea1(parametros, tipologia, superficieM2, perfil, etapas, factorAlcanceManual);
        return CalcularArea6(unidad.Total, cantidad);
    }

    private ResultadoArancel CalcularProyecto(
        ParametrosCAO parametros,
        AreaArancel area,
        string concepto,
        TipologiaEdificio tipologia,
        double superficieM2,
        PerfilAlcance perfil,
        IEnumerable<EtapaProyecto> etapas,
        double? factorAlcanceManual,
        double factorComplejidad,
        string etiquetaComplejidad)
    {
        ValidarParametros(parametros);

        ValidarNumeroPositivo(superficieM2, nameof(superficieM2));
        ValidarNumeroPositivo(factorComplejidad, nameof(factorComplejidad));

        if (factorAlcanceManual is not null)
            ValidarNumeroPositivo(factorAlcanceManual.Value, nameof(factorAlcanceManual));

        var contratadas = etapas.Distinct().ToList();

        if (factorAlcanceManual is null && contratadas.Count == 0)
            throw new ArgumentException("Debe seleccionar al menos una etapa contratada para determinar el factor de alcance (Fa).", nameof(etapas));

        var infoTipologia = _matrices.ObtenerTipologia(tipologia);
        var (factorH, rango) = _matrices.ObtenerFactorH(tipologia, superficieM2);
        var factorAlcance = factorAlcanceManual ?? _matrices.CalcularFactorAlcance(perfil, contratadas);

        if (factorAlcance <= 0)
            throw new ArgumentException("El factor de alcance (Fa) debe ser mayor que cero.", nameof(etapas));
        var advertencias = new List<string>();

        if (factorAlcance > _matrices.ObtenerPerfil(perfil).FaMaximo + Tolerancia)
            advertencias.Add(
                $"El factor de alcance Fa = {FormatoBoliviano.Decimal(factorAlcance, 2)} supera el maximo del perfil " +
                $"({FormatoBoliviano.Decimal(_matrices.ObtenerPerfil(perfil).FaMaximo, 2)}). Verifique la seleccion de etapas.");

        if (superficieM2 > 1500)
            advertencias.Add("La superficie supera 1.500 m²; el factor h(k,r) aplicado es el de la última fila de la matriz.");

        var costoHora = parametros.CostoHora;
        var honorarioBase = parametros.HonorarioBaseSemanal;
        var calculado = costoHora * superficieM2 * factorH * factorAlcance * factorComplejidad;
        var limiteMinimo = honorarioBase * factorAlcance;
        var final = Math.Max(calculado, limiteMinimo);
        var limiteAplicado = limiteMinimo > calculado + Tolerancia;

        var desglose = new List<LineaDesglose>
        {
            new("Costo hora (CH)", $"V / {FormatoBoliviano.Decimal(parametros.HorasMensuales, 0)} h",
                costoHora, "Bs/h", null, costoHora),
            new("Superficie (M2)", $"{infoTipologia.Nombre} - rango {rango.Etiqueta}",
                superficieM2, "m²", factorH, costoHora * superficieM2 * factorH),
            new("Factor de alcance (Fa)", DescribirEtapas(perfil, contratadas),
                1, "factor", factorAlcance, costoHora * superficieM2 * factorH * factorAlcance),
            new("Factor de complejidad (Fj)", etiquetaComplejidad,
                1, "factor", factorComplejidad, calculado)
        };

        if (limiteAplicado)
        {
            desglose.Add(new LineaDesglose(
                "Ajuste por limite minimo", "Hmin = Hbase x Fa", 1, "Bs", null, limiteMinimo - calculado));
        }

        desglose.Add(LineaDesglose.Simple("Honorario final (Hfinal)", "max(Hcalc, Hmin)", final));

        return new ResultadoArancel
        {
            Area = area,
            Concepto = concepto,
            Tipologia = infoTipologia.Nombre,
            SuperficieM2 = superficieM2,
            RangoDescripcion = rango.Etiqueta,
            FactorH = factorH,
            FactorAlcance = factorAlcance,
            FactorComplejidad = factorComplejidad,
            CostoHora = costoHora,
            HonorarioBaseSemanal = honorarioBase,
            HonorarioCalculado = calculado,
            LimiteMinimo = limiteMinimo,
            Total = final,
            LimiteMinimoAplicado = limiteAplicado,
            Desglose = desglose,
            Advertencias = advertencias,
            ParametrosUsados = DescribirParametros(parametros),
            FormulaResumen =
                $"Hcalc = {FormatoBoliviano.Numero(costoHora)} x {FormatoBoliviano.Decimal(superficieM2, 2)} x " +
                $"{FormatoBoliviano.Decimal(factorH, 2)} x {FormatoBoliviano.Decimal(factorAlcance, 2)} x " +
                $"{FormatoBoliviano.Decimal(factorComplejidad, 2)} = {FormatoBoliviano.Moneda(calculado)} | " +
                $"Hmin = {FormatoBoliviano.Moneda(honorarioBase)} x {FormatoBoliviano.Decimal(factorAlcance, 2)} = " +
                $"{FormatoBoliviano.Moneda(limiteMinimo)} | Hfinal = {FormatoBoliviano.Moneda(final)}"
        };
    }

    private static void ValidarParametros(ParametrosCAO parametros)
    {
        ArgumentNullException.ThrowIfNull(parametros);

        var errores = parametros.Validar();

        if (errores.Count > 0)
            throw new ArgumentException(string.Join(" ", errores), nameof(parametros));
    }

    private static void ValidarNumeroPositivo(double valor, string nombre)
    {
        if (double.IsNaN(valor) || double.IsInfinity(valor) || valor <= 0)
            throw new ArgumentException($"{nombre} debe ser un numero mayor que cero.", nombre);
    }

    private static IReadOnlyList<string> DescribirParametros(ParametrosCAO parametros) =>
    [
        $"Variable Vida (V): {FormatoBoliviano.Moneda(parametros.VariableVida)}",
        $"Costo hora (CH) = V / {FormatoBoliviano.Decimal(parametros.HorasMensuales, 0)} = {FormatoBoliviano.Moneda(parametros.CostoHora)}",
        $"Honorario base semanal (Hbase) = V / 4 = {FormatoBoliviano.Moneda(parametros.HonorarioBaseSemanal)}"
    ];

    private static string DescribirEtapas(PerfilAlcance perfil, IReadOnlyList<EtapaProyecto> contratadas) =>
        contratadas.Count == 0
            ? $"Perfil {PerfilAlcanceCatalogo.Obtener(perfil).Nombre}"
            : $"Perfil {PerfilAlcanceCatalogo.Obtener(perfil).Nombre}: " +
              string.Join(", ", contratadas.Select(e => EtapaProyectoCatalogo.Obtener(e).Nombre));
}
