using CalculadoraArancelesCAO.Core.Models;
using CalculadoraArancelesCAO.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CalculadoraArancelesCAO.Tests;

public sealed class AlmacenamientoFalso : ILocalStorageService
{
    public Dictionary<string, string> Datos { get; } = new(StringComparer.Ordinal);

    public Task<string?> GetItemAsync(string clave) =>
        Task.FromResult(Datos.TryGetValue(clave, out var valor) ? valor : null);

    public Task<T?> GetItemAsync<T>(string clave)
    {
        if (!Datos.TryGetValue(clave, out var valor) || valor is null)
        {
            return Task.FromResult<T?>(default);
        }

        try
        {
            return Task.FromResult<T?>(
                (T?)System.Text.Json.JsonSerializer.Deserialize(
                    System.Text.Json.JsonSerializer.Serialize(valor),
                    typeof(T)));
        }
        catch
        {
            return Task.FromResult<T?>(default);
        }
    }

    public Task SetItemAsync(string clave, string valor)
    {
        Datos[clave] = valor;
        return Task.CompletedTask;
    }

    public Task RemoveItemAsync(string clave)
    {
        Datos.Remove(clave);
        return Task.CompletedTask;
    }
}

public class ParametrosServiceTests
{
    private static ParametrosService Crear(AlmacenamientoFalso almacenamiento) =>
        new(almacenamiento, NullLogger<ParametrosService>.Instance);

    [Fact]
    public async Task SinValoresPersistidos_UsaLosValoresOficiales()
    {
        var servicio = Crear(new AlmacenamientoFalso());

        await servicio.CargarAsync();

        Assert.True(servicio.Actual.UsaValoresOficiales);
        Assert.Equal(ParametrosCAO.VariableVidaPorDefecto, servicio.Actual.VariableVida);
    }

    [Fact]
    public async Task Actualizar_PersisteLaVariableVidaYLaRecarga()
    {
        var almacenamiento = new AlmacenamientoFalso();
        var servicio = Crear(almacenamiento);

        await servicio.ActualizarAsync(9500.00, 160);
        Assert.False(servicio.Actual.UsaValoresOficiales);
        Assert.Equal(59.375, servicio.Actual.CostoHora, 3);
        Assert.Equal(2375.00, servicio.Actual.HonorarioBaseSemanal, 2);

        var recarga = Crear(almacenamiento);
        await recarga.CargarAsync();

        Assert.Equal(9500.00, recarga.Actual.VariableVida);
        Assert.Equal(2375.00, recarga.Actual.HonorarioBaseSemanal, 2);
    }

    [Fact]
    public async Task ValoresPersistidosInvalidos_RegresanALosOficiales()
    {
        var almacenamiento = new AlmacenamientoFalso();
        almacenamiento.Datos["cao.parametros.variableVida"] = "-500";
        almacenamiento.Datos["cao.parametros.horasMensuales"] = "abc";

        var servicio = Crear(almacenamiento);
        await servicio.CargarAsync();

        Assert.True(servicio.Actual.UsaValoresOficiales);
    }

    [Fact]
    public async Task Actualizar_ConValoresInvalidos_EsRechazado()
    {
        var servicio = Crear(new AlmacenamientoFalso());
        await servicio.CargarAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => servicio.ActualizarAsync(0, 160));
        Assert.True(servicio.Actual.UsaValoresOficiales);
    }

    [Fact]
    public async Task RestaurarValoresOficiales_LimpiaElAlmacenamiento()
    {
        var almacenamiento = new AlmacenamientoFalso();
        var servicio = Crear(almacenamiento);

        await servicio.ActualizarAsync(9500, 160);
        await servicio.RestaurarValoresOficialesAsync();

        Assert.True(servicio.Actual.UsaValoresOficiales);
        Assert.Empty(almacenamiento.Datos);
    }

    [Fact]
    public async Task Suscribir_NotificaLosCambios()
    {
        var servicio = Crear(new AlmacenamientoFalso());
        await servicio.CargarAsync();

        var notificaciones = 0;
        using var suscripcion = servicio.Suscribir(() => notificaciones++);

        await servicio.ActualizarAsync(9500, 160);

        Assert.Equal(1, notificaciones);
    }

    [Fact]
    public async Task Suscribirse_DespuesDeNotificar_NoRecibeLlamadas()
    {
        var servicio = Crear(new AlmacenamientoFalso());
        await servicio.CargarAsync();

        var notificaciones = 0;
        var suscripcion = servicio.Suscribir(() => notificaciones++);
        suscripcion.Dispose();

        await servicio.ActualizarAsync(9500, 160);

        Assert.Equal(0, notificaciones);
    }
}
