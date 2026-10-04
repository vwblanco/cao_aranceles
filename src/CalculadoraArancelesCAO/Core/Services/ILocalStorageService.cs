using CalculadoraArancelesCAO.Core.Models;

namespace CalculadoraArancelesCAO.Core.Services;

/// <summary>
/// Interfaz para acceso al almacenamiento local del navegador (localStorage).
/// </summary>
public interface ILocalStorageService
{
    Task<string?> GetItemAsync(string clave);
    Task<T?> GetItemAsync<T>(string clave);
    Task SetItemAsync(string clave, string valor);
    Task RemoveItemAsync(string clave);
}