using CalculadoraArancelesCAO.Core.Services;

namespace CalculadoraArancelesCAO.Tests;

public sealed class IndiceAccesoTests
{
    private static List<EntradaAfiliado> ListaEjemplo() =>
    [
        new() { NumeroRegistro = "3107", Nombre = "BLANCO COCA VICTOR WILFREDO", CI = "1234567 LP" },
        new() { NumeroRegistro = "1042", Nombre = "PEREZ ANA MARIA", CI = "8765432" },
        // Sin CI registrada en el Colegio: el indice usa el nombre.
        new() { NumeroRegistro = "77", Nombre = "RAMIREZ CARLOS", CI = "" },
        // Sin CI y conacentos/espacios: debe normalizar igual.
        new() { NumeroRegistro = "88", Nombre = "MÉNDEZ  SOFÍA  PÉREZ", CI = "" }
    ];

    [Fact]
    public void Construir_GeneraUnaEntradaPorAfiliadoValido()
    {
        var indice = IndiceAcceso.Construir(ListaEjemplo());

        Assert.Equal(IndiceAcceso.VersionFormato, indice.Version);
        Assert.Equal(4, indice.Total);
        Assert.Equal(4, indice.Entradas.Count);
    }

    [Fact]
    public void Construir_DescartaFilasSinNumeroDeRegistro()
    {
        var lista = ListaEjemplo();
        lista.Add(new EntradaAfiliado { NumeroRegistro = "", Nombre = "SIN REGISTRO", CI = "1111111" });

        var indice = IndiceAcceso.Construir(lista);

        Assert.Equal(4, indice.Entradas.Count);
        Assert.DoesNotContain(indice.Entradas, e => e.R.Length == 0);
    }

    [Fact]
    public void Construir_NoIncluyeNombresNiCedulasEnTextoPlano()
    {
        var indice = IndiceAcceso.Construir(ListaEjemplo());
        var json = IndiceAcceso.Serializar(indice);

        Assert.DoesNotContain("BLANCO", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WILFREDO", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PEREZ", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1234567", json);
        Assert.DoesNotContain("8765432", json);
    }

    [Fact]
    public void Coincide_AceptaCedulaDelAfiliado()
    {
        var indice = IndiceAcceso.Construir(ListaEjemplo());

        Assert.True(IndiceAcceso.Coincide(indice, "3107", "1234567 LP", null));
        Assert.True(IndiceAcceso.Coincide(indice, "1042", "8765432", null));
    }

    [Fact]
    public void Coincide_AceptaLaMismaCedulaEscritaDeOtraForma()
    {
        var indice = IndiceAcceso.Construir(ListaEjemplo());

        Assert.True(IndiceAcceso.Coincide(indice, "3107", "1.234.567-LP", null));
        Assert.True(IndiceAcceso.Coincide(indice, "3107", "1234567lp", null));
        Assert.True(IndiceAcceso.Coincide(indice, "3107", " 1234567 L.P ", null));
    }

    [Fact]
    public void Coincide_AceptaNumeroRegistroConCerosAdelante()
    {
        var indice = IndiceAcceso.Construir(ListaEjemplo());

        Assert.True(IndiceAcceso.Coincide(indice, "077", "", "RAMIREZ CARLOS"));
        Assert.True(IndiceAcceso.Coincide(indice, "0077", "", "RAMIREZ CARLOS"));
    }

    [Fact]
    public void Coincide_AceptaAfiliadoSinCedulaUsandoElNombre()
    {
        var indice = IndiceAcceso.Construir(ListaEjemplo());

        Assert.True(IndiceAcceso.Coincide(indice, "77", "", "RAMIREZ CARLOS"));
        // El nombre se normaliza: acentos, espacios y mayusculas no importan.
        Assert.True(IndiceAcceso.Coincide(indice, "88", "", "mendez sofia perez"));
        Assert.True(IndiceAcceso.Coincide(indice, "88", "", "MÉNDEZ SOFÍA PÉREZ"));
    }

    [Fact]
    public void Coincide_RechazaCedulaAjenaAlRegistro()
    {
        var indice = IndiceAcceso.Construir(ListaEjemplo());

        // La CI del 3107 no sirve para el registro 1042.
        Assert.False(IndiceAcceso.Coincide(indice, "1042", "1234567 LP", null));
    }

    [Fact]
    public void Coincide_RechazaRegistroInexistente()
    {
        var indice = IndiceAcceso.Construir(ListaEjemplo());

        Assert.False(IndiceAcceso.Coincide(indice, "9999", "1234567 LP", null));
    }

    [Fact]
    public void Coincide_RechazaCredencialVacia()
    {
        var indice = IndiceAcceso.Construir(ListaEjemplo());

        Assert.False(IndiceAcceso.Coincide(indice, "", "1234567 LP", null));
        Assert.False(IndiceAcceso.Coincide(indice, "3107", "", null));
    }

    [Fact]
    public void Coincide_RechazaIndiceVacioONulo()
    {
        var vacio = new IndiceAccesoArchivo();

        Assert.False(IndiceAcceso.Coincide(vacio, "3107", "1234567 LP", null));
        Assert.False(IndiceAcceso.Coincide(null!, "3107", "1234567 LP", null));
    }

    [Fact]
    public void ExigeCedula_DistingueAfiliadosConYSinCedula()
    {
        var indice = IndiceAcceso.Construir(ListaEjemplo());

        Assert.True(IndiceAcceso.ExigeCedula(indice, "3107"));
        Assert.False(IndiceAcceso.ExigeCedula(indice, "77"));
    }

    [Fact]
    public void Serializar_Y_Deserializar_ConservanLaCoincidencia()
    {
        var original = IndiceAcceso.Construir(ListaEjemplo());
        var copia = IndiceAcceso.Deserializar(IndiceAcceso.Serializar(original));

        Assert.NotNull(copia);
        Assert.Equal(original.Total, copia!.Total);
        Assert.True(IndiceAcceso.Coincide(copia, "3107", "1234567 LP", null));
        Assert.True(IndiceAcceso.Coincide(copia, "88", "", "MÉNDEZ SOFÍA PÉREZ"));
    }

    [Fact]
    public void Deserializar_RechazaJsonInvalidoODeVersionDistinta()
    {
        Assert.Null(IndiceAcceso.Deserializar("no es json"));
        Assert.Null(IndiceAcceso.Deserializar(null));
        Assert.Null(IndiceAcceso.Deserializar(""));
        Assert.Null(IndiceAcceso.Deserializar(
            """{"version":99,"total":0,"entradas":[]}"""));
    }

    [Fact]
    public void CalcularClave_EsEstableYDependeDelRegistro()
    {
        var a = IndiceAcceso.CalcularClave("3107", "1234567LP");
        var b = IndiceAcceso.CalcularClave("3107", "1234567LP");
        var otra = IndiceAcceso.CalcularClave("1042", "1234567LP");

        Assert.Equal(a, b);
        Assert.NotEqual(a, otra);
        Assert.Equal(64, a.Length); // SHA-256 en hex
    }

    /// <summary>
    /// Si el Colegio ya tiene CI registrada, conocer el nombre del colega no debe
    /// alcanzar para entrar sin la cedula.
    /// </summary>
    [Fact]
    public void Coincide_NoAceptaElNombreCuandoElIndiceExigeCedula()
    {
        var indice = IndiceAcceso.Construir(ListaEjemplo());

        Assert.True(IndiceAcceso.ExigeCedula(indice, "3107"));

        // Nombre correcto, sin cedula: debe rechazar.
        Assert.False(IndiceAcceso.Coincide(indice, "3107", "", "BLANCO COCA VICTOR WILFREDO"));

        // Cedula equivocada mas nombre correcto: tambien debe rechazar.
        Assert.False(IndiceAcceso.Coincide(indice, "3107", "9999999", "BLANCO COCA VICTOR WILFREDO"));

        // Con la cedula correcta si entra.
        Assert.True(IndiceAcceso.Coincide(indice, "3107", "1234567 LP", null));
    }

    [Fact]
    public void Coincide_AceptaElNombreCuandoLaEntradaNoExigeCedula()
    {
        var indice = IndiceAcceso.Construir(ListaEjemplo());

        Assert.False(IndiceAcceso.ExigeCedula(indice, "77"));
        Assert.True(IndiceAcceso.Coincide(indice, "77", "", "RAMIREZ CARLOS"));
    }

    [Fact]
    public void Construir_NoRepiteRegistroYGanaLaEntradaMasRestrictiva()
    {
        var lista = new List<EntradaAfiliado>
        {
            new() { NumeroRegistro = "500", Nombre = "PEREZ ANA MARIA", CI = "" },
            new() { NumeroRegistro = "0500", Nombre = "OTRO APELLIDO", CI = "1111111" }
        };

        var indice = IndiceAcceso.Construir(lista);

        // "500" y "0500" son el mismo registro: una sola entrada.
        Assert.Single(indice.Entradas);

        // Gana la que exige cedula.
        Assert.True(IndiceAcceso.ExigeCedula(indice, "500"));
        Assert.True(IndiceAcceso.Coincide(indice, "500", "1111111", null));
        Assert.False(IndiceAcceso.Coincide(indice, "500", "", "OTRO APELLIDO"));
    }

    [Fact]
    public void NormalizarRegistro_QuitaCerosAdelanteYNoDejaVacio()
    {
        Assert.Equal("77", IndiceAcceso.NormalizarRegistro("077"));
        Assert.Equal("77", IndiceAcceso.NormalizarRegistro("0077"));
        Assert.Equal("77", IndiceAcceso.NormalizarRegistro(" 77 "));
        Assert.Equal("0", IndiceAcceso.NormalizarRegistro("000"));
        Assert.Equal(string.Empty, IndiceAcceso.NormalizarRegistro(null));
        Assert.Equal(string.Empty, IndiceAcceso.NormalizarRegistro("   "));
    }

    [Fact]
    public void NormalizarCredencial_NoQuitaCerosAdelanteNiAcentos()
    {
        // La cedula 0123 es distinta de la 123: aqui no se recortan ceros.
        Assert.Equal("0123", IndiceAcceso.NormalizarCredencial("0123"));

        // Los acentos se eliminan para que "mendez" entre con "MENDEZ".
        Assert.Equal("MENDEZ", IndiceAcceso.NormalizarCredencial("MÉNDEZ"));
        Assert.Equal("MENDEZSOFIAPEREZ", IndiceAcceso.NormalizarNombre("mendez sofia perez"));
    }
}