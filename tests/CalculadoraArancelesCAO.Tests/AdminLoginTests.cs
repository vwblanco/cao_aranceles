using CalculadoraArancelesCAO.UI.Services;
using Xunit;

namespace CalculadoraArancelesCAO.Tests;

public sealed class AdminLoginTests
{
    [Fact]
    public void EsAdmin_CredencialesCorrectas_RetornaTrue()
    {
        // Arrange
        var tipo = typeof(AutenticacionService);
        var metodo = tipo.GetMethod("EsAdmin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var resultado = (bool)metodo.Invoke(null, new object[] { "VICTOR WILFREDO BLANCO COCA", "3107", "#Teresita24#" });

        // Assert
        Assert.True(resultado);
    }

    [Fact]
    public void EsAdmin_CredencialesConMayusculasDistintas_RetornaTrue()
    {
        var tipo = typeof(AutenticacionService);
        var metodo = tipo.GetMethod("EsAdmin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Case-insensitive para clave
        var resultado = (bool)metodo.Invoke(null, new object[] { "victor wilfredo blanco coca", "3107", "#TERESITA24#" });

        Assert.True(resultado);
    }

    [Fact]
    public void EsAdmin_ClaveIncorrecta_RetornaFalse()
    {
        var tipo = typeof(AutenticacionService);
        var metodo = tipo.GetMethod("EsAdmin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var resultado = (bool)metodo.Invoke(null, new object[] { "VICTOR WILFREDO BLANCO COCA", "3107", "claveincorrecta" });

        Assert.False(resultado);
    }

    [Fact]
    public void EsAdmin_RegistroIncorrecto_RetornaFalse()
    {
        var tipo = typeof(AutenticacionService);
        var metodo = tipo.GetMethod("EsAdmin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var resultado = (bool)metodo.Invoke(null, new object[] { "VICTOR WILFREDO BLANCO COCA", "9999", "#Teresita24#" });

        Assert.False(resultado);
    }
}