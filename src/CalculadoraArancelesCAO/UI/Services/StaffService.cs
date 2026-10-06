using System.Text.Json;
using CalculadoraArancelesCAO.Core.Services;

namespace CalculadoraArancelesCAO.UI.Services;

/// <summary>
/// Credenciales del personal del Colegio, configurables por el Administrador.
///
/// La aplicacion no almacena ninguna lista de colegiados: el calculo de aranceles
/// es de acceso libre porque el Reglamento 2026 y la Ley N.° 1373 son de dominio
/// publico y el resultado se puede reproducir en una hoja de calculo. Esta clase
/// existe unicamente para administrar al personal que manipula parametros
/// economicos y datos de gestion.
/// </summary>
public sealed class StaffService
{
    private const string ClaveStaff = "cao.staff.credenciales";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    private readonly ILocalStorageService _localStorage;

    public StaffService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task<List<StaffCredencial>> ObtenerStaffAsync()
    {
        var json = await _localStorage.GetItemAsync(ClaveStaff);
        if (string.IsNullOrWhiteSpace(json))
            return new List<StaffCredencial>();

        try
        {
            return JsonSerializer.Deserialize<List<StaffCredencial>>(json, OpcionesJson) ?? new List<StaffCredencial>();
        }
        catch
        {
            return new List<StaffCredencial>();
        }
    }

    public async Task<bool> GuardarStaffAsync(List<StaffCredencial> staff)
    {
        await _localStorage.SetItemAsync(ClaveStaff, JsonSerializer.Serialize(staff, OpcionesJson));
        return true;
    }

    public async Task<bool> AgregarStaffAsync(string numeroRegistro, string nombre, string clave)
    {
        var staff = await ObtenerStaffAsync();
        if (staff.Any(s => s.NumeroRegistro == numeroRegistro))
            return false; // ya existe

        staff.Add(new StaffCredencial
        {
            NumeroRegistro = numeroRegistro.Trim(),
            Nombre = nombre.Trim(),
            ClaveHash = HashClave(clave),
            FechaCreacion = DateTimeOffset.Now
        });
        return await GuardarStaffAsync(staff);
    }

    public async Task<bool> EliminarStaffAsync(string numeroRegistro)
    {
        var staff = await ObtenerStaffAsync();
        var eliminado = staff.RemoveAll(s => s.NumeroRegistro == numeroRegistro) > 0;
        if (eliminado)
            await GuardarStaffAsync(staff);
        return eliminado;
    }

    public async Task<StaffCredencial?> ValidarStaffAsync(string numeroRegistro, string clave)
    {
        var staff = await ObtenerStaffAsync();
        var cred = staff.FirstOrDefault(s => s.NumeroRegistro == numeroRegistro);
        if (cred is null) return null;
        return VerificarClave(clave, cred.ClaveHash) ? cred : null;
    }

    private static string HashClave(string clave)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(clave + "CAO_SALT_2026");
        return Convert.ToBase64String(sha.ComputeHash(bytes));
    }

    private static bool VerificarClave(string clave, string hash)
    {
        return HashClave(clave) == hash;
    }
}

public sealed class StaffCredencial
{
    public string NumeroRegistro { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string ClaveHash { get; set; } = string.Empty;
    public DateTimeOffset FechaCreacion { get; set; } = DateTimeOffset.Now;
}