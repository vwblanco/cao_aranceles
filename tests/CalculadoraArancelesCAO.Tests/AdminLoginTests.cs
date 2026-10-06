using CalculadoraArancelesCAO.UI.Services;
using Xunit;

namespace CalculadoraArancelesCAO.Tests;

public sealed class AdminLoginTests
{
    private static bool EsAdmin(string numeroRegistro, string clave)
    {
        var metodo = typeof(AutenticacionService).GetMethod(
            "EsAdmin",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("No se encontro AutenticacionService.EsAdmin.");

        return (bool)metodo.Invoke(null, new object[] { numeroRegistro, clave})!;
    }

    [Fact]
    public void EsAdmin_CredencialesCorrectas_RetornaTrue()
        => Assert.True(EsAdmin("3107", "#Teresita24#"));

    [Fact]
    public void EsAdmin_ClaveConMinusculas_RetornaTrue()
        => Assert.True(EsAdmin("3107", "#teresita24#"));

    [Fact]
    public void EsAdmin_ClaveIncorrecta_RetornaFalse()
        => Assert.False(EsAdmin("3107", "claveincorrecta"));

    [Fact]
    public void EsAdmin_RegistroIncorrecto_RetornaFalse()
        => Assert.False(EsAdmin("9999", "#Teresita24#"));
}