using CalculadoraArancelesCAO.Core.Services;
using Microsoft.JSInterop;
using System.Text.Json;

namespace CalculadoraArancelesCAO.UI.Services;

public sealed class LocalStorageService : ILocalStorageService
{
    private readonly IJSRuntime _js;

    public LocalStorageService(IJSRuntime js) => _js = js;

    public async Task<string?> GetItemAsync(string clave)
    {
        try
        {
            return await _js.InvokeAsync<string?>("localStorage.getItem", clave);
        }
        catch (JSException)
        {
            return null;
        }
    }

    public async Task<T?> GetItemAsync<T>(string clave)
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", clave);
            if (string.IsNullOrWhiteSpace(json))
                return default;
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JSException)
        {
            return default;
        }
    }

    public async Task SetItemAsync(string clave, string valor)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", clave, valor);
        }
        catch (JSException)
        {
        }
    }

    public async Task RemoveItemAsync(string clave)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", clave);
        }
        catch (JSException)
        {
        }
    }
}
