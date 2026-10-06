using System.Security.Claims;
using System.Text.Json;
using CalculadoraArancelesCAO.Core.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace CalculadoraArancelesCAO.UI.Services;

/// <summary>
/// Control de acceso del personal del Colegio.
///
/// El calculo de aranceles es de acceso libre: el Reglamento 2026 y la Ley N.° 1373
/// son de dominio publico y cualquiera puede reproducir el resultado en una hoja de
/// calculo. La aplicacion no guarda ninguna lista de colegiados, por lo que aqui no
/// hay ni registros ni cedulas ni nombres de afiliados.
///
/// Este servicio existe unicamente para el Administrador y el personal Staff, que
/// modifican parametros economicos y gestionan credenciales.
/// </summary>
public sealed class AutenticacionService : AuthenticationStateProvider
{
    private const string ClaveCredenciales = "cao.sesion.credenciales";
    private const string ClaveSesion = "cao.sesion.activa";
    private const string ClavePinHash = "cao.sesion.pin";
    private const string ClaveUltimaActividad = "cao.sesion.actividad";

    /// <summary>
    /// Minutos de inactividad antes de que la aplicacion vuelva a pedir el PIN.
    /// Evita que prestar el celular con la sesion abierta sirva de algo.
    /// </summary>
    public const int MinutosInactividadParaBloquear = 5;

    /// <summary>
    /// Mensaje unico para cualquier rechazo. No distingue entre registro inexistente
    /// y clave incorrecta, para no permitir enumerar el personal.
    /// </summary>
    private const string MensajeAccesoDenegado = "Credenciales no validas. Verifique su registro y su clave.";

    // Credenciales fijas del super-admin.
    private const string AdminRegistro = "3107";
    private const string AdminClave = "#Teresita24#";
    private const string AdminNombre = "VICTOR WILFREDO BLANCO COCA";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    private readonly ILocalStorageService _localStorage;
    private readonly StaffService _staff;

    private AuthenticationState _estadoActual = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public AutenticacionService(ILocalStorageService localStorage, StaffService staff)
    {
        _localStorage = localStorage;
        _staff = staff;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(_estadoActual);

    /// <summary>
    /// Inicia sesion del Administrador o del personal Staff. No existe ninguna otra
    /// via de acceso: el resto de la aplicacion es publico.
    /// </summary>
    public async Task<ResultadoLogin> LoginAsync(string numeroRegistro, string clave, bool recordar)
    {
        var registro = (numeroRegistro ?? string.Empty).Trim();
        var credencial = (clave ?? string.Empty).Trim();

        if (registro.Length == 0 || credencial.Length == 0)
        {
            return new ResultadoLogin(false, MensajeAccesoDenegado, string.Empty);
        }

        if (EsAdmin(registro, credencial))
        {
            return await CrearSesionAsync(new SesionUsuario
            {
                Nombre = AdminNombre,
                NumeroRegistro = AdminRegistro,
                FechaLogin = DateTimeOffset.Now,
                Recordar = recordar,
                Rol = "Admin"
            }, recordar, "Acceso concedido (Administrador).");
        }

        var cred = await _staff.ValidarStaffAsync(registro, credencial);
        if (cred is null)
        {
            return new ResultadoLogin(false, MensajeAccesoDenegado, string.Empty);
        }

        return await CrearSesionAsync(new SesionUsuario
        {
            Nombre = cred.Nombre,
            NumeroRegistro = cred.NumeroRegistro,
            FechaLogin = DateTimeOffset.Now,
            Recordar = recordar,
            Rol = "Staff"
        }, recordar, $"Acceso concedido (Staff: {cred.Nombre}).");
    }

    /// <summary>Credenciales fijas del super-admin. La clave no se distingue por mayusculas.</summary>
    private static bool EsAdmin(string numeroRegistro, string clave)
    {
        return numeroRegistro == AdminRegistro
            && clave.Equals(AdminClave, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ResultadoLogin> CrearSesionAsync(
        SesionUsuario sesion, bool recordar, string mensaje)
    {
        await _localStorage.SetItemAsync(ClaveCredenciales, JsonSerializer.Serialize(sesion, OpcionesJson));

        if (recordar)
        {
            await _localStorage.SetItemAsync(ClaveSesion, "true");
        }
        else
        {
            await _localStorage.RemoveItemAsync(ClaveSesion);
        }

        await MarcarActividadAsync();
        var requierePin = await RequierePinAsync();

        _estadoActual = new AuthenticationState(CrearPrincipal(sesion));
        NotifyAuthenticationStateChanged(Task.FromResult(_estadoActual));

        return new ResultadoLogin(true, mensaje, sesion.Rol, requierePin);
    }

    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemAsync(ClaveCredenciales);
        await _localStorage.RemoveItemAsync(ClaveSesion);
        await _localStorage.RemoveItemAsync(ClaveUltimaActividad);

        _estadoActual = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        NotifyAuthenticationStateChanged(Task.FromResult(_estadoActual));
    }

    // ===== PIN de desbloqueo =====
    //
    // El PIN protege el dispositivo del personal: si el celular se presta con la
    // sesion abierta, al volver a abrir la aplicacion se pide el PIN.

    /// <summary>Registra la interaccion del usuario para reiniciar el reloj de inactividad.</summary>
    public async Task MarcarActividadAsync()
        => await _localStorage.SetItemAsync(ClaveUltimaActividad, DateTimeOffset.Now.ToString("o"));

    /// <summary>Indica si este dispositivo tiene un PIN configurado.</summary>
    public async Task<bool> RequierePinAsync()
    {
        var hash = await _localStorage.GetItemAsync(ClavePinHash);
        return !string.IsNullOrWhiteSpace(hash);
    }

    /// <summary>
    /// Indica si la aplicacion debe bloquearse por inactividad. Solo aplica si hay
    /// una sesion abierta y el usuario ya/configuro un PIN.
    /// </summary>
    public async Task<bool> DeboBloquearAsync()
    {
        var sesionActiva = await _localStorage.GetItemAsync(ClaveSesion);
        if (sesionActiva is not "true")
        {
            // El usuario cerro sesion: no hay nada que bloquear.
            return false;
        }

        var hash = await _localStorage.GetItemAsync(ClavePinHash);
        if (string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        var marca = await _localStorage.GetItemAsync(ClaveUltimaActividad);
        if (!DateTimeOffset.TryParse(marca, out var ultima))
        {
            return false;
        }

        return DateTimeOffset.Now - ultima > TimeSpan.FromMinutes(MinutosInactividadParaBloquear);
    }

    /// <summary>Registra un PIN de 4 digitos para este dispositivo.</summary>
    public async Task<(bool Exitoso, string Mensaje)> EstablecerPinAsync(string pin)
    {
        var pinLimpio = new string((pin ?? string.Empty).Where(char.IsDigit).ToArray());

        if (pinLimpio.Length != 4)
        {
            return (false, "El PIN debe tener exactamente 4 digitos.");
        }

        if (EsPinTrivial(pinLimpio))
        {
            return (false, "El PIN no puede ser 1234, 0000 ni un numero repetido.");
        }

        var sal = Guid.NewGuid().ToString("N");
        await _localStorage.SetItemAsync(ClavePinHash, HashClaveLocal(pinLimpio, sal) + ":" + sal);
        await MarcarActividadAsync();
        return (true, "PIN registrado.");
    }

    /// <summary>Verifica el PIN. Si falla, cierra la sesion y obliga a ingresar de nuevo.</summary>
    public async Task<(bool Exitoso, string Mensaje)> VerificarPinAsync(string pin)
    {
        var almacenado = await _localStorage.GetItemAsync(ClavePinHash);
        if (string.IsNullOrWhiteSpace(almacenado))
        {
            // Sin PIN configurado no hay nada que verificar.
            await MarcarActividadAsync();
            return (true, "Sesion desbloqueada.");
        }

        var partes = almacenado.Split(':', 2);
        if (partes.Length != 2)
        {
            return (false, "PIN incorrecto. Ingrese de nuevo.");
        }

        var pinLimpio = new string((pin ?? string.Empty).Where(char.IsDigit).ToArray());

        if (HashClaveLocal(pinLimpio, partes[1]) == partes[0])
        {
            await MarcarActividadAsync();
            return (true, "Sesion desbloqueada.");
        }

        await LogoutAsync();
        return (false, "PIN incorrecto. Por seguridad se cerro la sesion: ingrese de nuevo.");
    }

    private static bool EsPinTrivial(string pin)
        => pin is "1234" or "0000" or "1111" or "2222" or "3333" or "4444"
            or "5555" or "6666" or "7777" or "8888" or "9999"
        || pin.All(c => c == pin[0]);

    private static string HashClaveLocal(string valor, string sal)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(valor + "|" + sal);
        return Convert.ToBase64String(sha.ComputeHash(bytes));
    }

    public async Task<bool> RestaurarSesionAsync()
    {
        try
        {
            var sesionActiva = await _localStorage.GetItemAsync(ClaveSesion);
            if (sesionActiva is not "true")
            {
                return false;
            }

            var sesion = await ObtenerSesionAsync();
            if (sesion is null)
            {
                return false;
            }

            await MarcarActividadAsync();

            _estadoActual = new AuthenticationState(CrearPrincipal(sesion));
            NotifyAuthenticationStateChanged(Task.FromResult(_estadoActual));
            return true;
        }
        catch
        {
            await LogoutAsync();
            return false;
        }
    }

    public async Task<SesionUsuario?> ObtenerSesionAsync()
    {
        var json = await _localStorage.GetItemAsync(ClaveCredenciales);
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<SesionUsuario>(json, OpcionesJson);
    }

    private static ClaimsPrincipal CrearPrincipal(SesionUsuario sesion)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, sesion.Nombre),
            new("NumeroRegistro", sesion.NumeroRegistro),
            new("FechaLogin", sesion.FechaLogin.ToString("o")),
            new(ClaimTypes.Role, sesion.Rol)
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "CredencialesCAO"));
    }
}

public sealed class SesionUsuario
{
    public string Nombre { get; set; } = string.Empty;
    public string NumeroRegistro { get; set; } = string.Empty;
    public DateTimeOffset FechaLogin { get; set; } = DateTimeOffset.Now;
    public bool Recordar { get; set; } = true;
    public string Rol { get; set; } = "Staff";
}

/// <summary>
/// Resultado de un intento de ingreso del personal.
/// - RequierePin: hay que crear el PIN de desbloqueo de este dispositivo.
/// </summary>
public sealed record ResultadoLogin(
    bool Exitoso,
    string Mensaje,
    string Rol,
    bool RequierePin = false);