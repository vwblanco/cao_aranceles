using System.Security.Claims;
using System.Text.Json;
using CalculadoraArancelesCAO.Core.Services;
using CalculadoraArancelesCAO.UI.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace CalculadoraArancelesCAO.UI.Services;

public sealed class AutenticacionService : AuthenticationStateProvider
{
    private const string ClaveCredenciales = "cao.colega.credenciales";
    private const string ClaveSesion = "cao.sesion.activa";

    // Credenciales fijas del super-admin (Victor)
    private const string AdminRegistro = "3107";
    private const string AdminClave = "#Teresita24#";
    private const string AdminNombre = "VICTOR WILFREDO BLANCO COCA";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    private readonly ILocalStorageService _localStorage;
    private readonly AfiliadosService _afiliados;

    private AuthenticationState _estadoActual = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public AutenticacionService(ILocalStorageService localStorage, AfiliadosService afiliados)
    {
        _localStorage = localStorage;
        _afiliados = afiliados;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(_estadoActual);

    public Task<List<Afiliado>> ObtenerAfiliadosAsync() => _afiliados.ObtenerAfiliadosAsync();

    /// <summary>
    /// Intenta iniciar sesión. Devuelve si fue exitoso, el mensaje, y si se requiere
    /// ingresar la cédula de identidad (enrollamiento progresivo).
    /// </summary>
    public async Task<ResultadoLogin> LoginAsync(string nombre, string numeroRegistro, string ci, bool recordar)
    {
        // 1. Verificar super-admin (credenciales fijas, no dependen de lista)
        if (EsAdmin(nombre, numeroRegistro, ci))
        {
            return await CrearSesionAdminAsync(recordar);
        }

        // 2. Verificar staff (credenciales configurables por admin)
        var staff = await _afiliados.ValidarStaffAsync(numeroRegistro, ci);
        if (staff is not null)
        {
            return await CrearSesionStaffAsync(staff, recordar);
        }

        // 3. Validar contra lista de afiliados (colegiados normales)
        var validacion = await _afiliados.ValidarAccesoAsync(numeroRegistro, ci, nombre);

        if (!validacion.Exitoso || validacion.Afiliado is null)
        {
            return new ResultadoLogin(false, validacion.Mensaje, false, "Colegiado");
        }

        var afiliado = validacion.Afiliado;

        // Si se requiere CI (falta en lista o falta en login aunque esté en lista),
        // NO crear credenciales aún; devolver RequiereCi para que UI lo pida.
        if (validacion.RequiereCi)
        {
            return new ResultadoLogin(true, validacion.Mensaje, true, "Colegiado");
        }

        // Si proveyó CI, validar/actualizar en la lista
        if (ci.Trim().Length > 0)
        {
            var actualizado = await _afiliados.ActualizarCedulaAsync(numeroRegistro, ci);
            if (!actualizado)
            {
                return new ResultadoLogin(false,
                    "La cedula de identidad no coincide con la registrada para este numero de registro.", false, "Colegiado");
            }
        }

        var rol = await DeterminarRolAsync(afiliado.NumeroRegistro);
        var credenciales = new ColegaCredenciales
        {
            Nombre = afiliado.Nombre,
            NumeroRegistro = afiliado.NumeroRegistro,
            CI = afiliado.CI.Length > 0 ? afiliado.CI : ci.Trim().ToUpperInvariant(),
            Recordar = recordar,
            FechaLogin = DateTimeOffset.Now,
            Rol = rol
        };

        await _localStorage.SetItemAsync(ClaveCredenciales, JsonSerializer.Serialize(credenciales, OpcionesJson));

        if (recordar)
        {
            await _localStorage.SetItemAsync(ClaveSesion, "true");
        }
        else
        {
            await _localStorage.RemoveItemAsync(ClaveSesion);
        }

        _estadoActual = new AuthenticationState(CrearPrincipal(credenciales));
        NotifyAuthenticationStateChanged(Task.FromResult(_estadoActual));

        return new ResultadoLogin(true, "Acceso concedido.", false, rol);
    }

    private static bool EsAdmin(string nombre, string numeroRegistro, string ci)
    {
        var n = (nombre ?? string.Empty).Trim();
        var reg = (numeroRegistro ?? string.Empty).Trim();
        var c = (ci ?? string.Empty).Trim();

        if (reg != AdminRegistro || !c.Equals(AdminClave, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Aceptar tanto el nombre hardcoded como el formato Excel (apellidos primero)
        return n.Equals(AdminNombre, StringComparison.OrdinalIgnoreCase) ||
               n.Equals("BLANCO COCA VICTOR WILFREDO", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ResultadoLogin> CrearSesionAdminAsync(bool recordar)
    {
        var credenciales = new ColegaCredenciales
        {
            Nombre = AdminNombre,
            NumeroRegistro = AdminRegistro,
            CI = AdminClave, // se guarda como identificador interno
            Recordar = recordar,
            FechaLogin = DateTimeOffset.Now,
            Rol = "Admin"
        };

        await _localStorage.SetItemAsync(ClaveCredenciales, JsonSerializer.Serialize(credenciales, OpcionesJson));

        if (recordar)
        {
            await _localStorage.SetItemAsync(ClaveSesion, "true");
        }
        else
        {
            await _localStorage.RemoveItemAsync(ClaveSesion);
        }

        _estadoActual = new AuthenticationState(CrearPrincipal(credenciales));
        NotifyAuthenticationStateChanged(Task.FromResult(_estadoActual));

        return new ResultadoLogin(true, "Acceso concedido (Administrador).", false, "Admin");
    }

    private async Task<string> DeterminarRolAsync(string numeroRegistro)
    {
        if (numeroRegistro == AdminRegistro)
            return "Admin";

        var staff = await _afiliados.ObtenerStaffAsync();
        if (staff.Any(s => s.NumeroRegistro == numeroRegistro))
            return "Staff";

        return "Colegiado";
    }

    private async Task<ResultadoLogin> CrearSesionStaffAsync(StaffCredencial staff, bool recordar)
    {
        var credenciales = new ColegaCredenciales
        {
            Nombre = staff.Nombre,
            NumeroRegistro = staff.NumeroRegistro,
            CI = staff.ClaveHash, // hash interno
            Recordar = recordar,
            FechaLogin = DateTimeOffset.Now,
            Rol = "Staff"
        };

        await _localStorage.SetItemAsync(ClaveCredenciales, JsonSerializer.Serialize(credenciales, OpcionesJson));

        if (recordar)
        {
            await _localStorage.SetItemAsync(ClaveSesion, "true");
        }
        else
        {
            await _localStorage.RemoveItemAsync(ClaveSesion);
        }

        _estadoActual = new AuthenticationState(CrearPrincipal(credenciales));
        NotifyAuthenticationStateChanged(Task.FromResult(_estadoActual));

        return new ResultadoLogin(true, $"Acceso concedido (Staff: {staff.Nombre}).", false, "Staff");
    }

    /// <summary>
    /// Completa el CI tras un login exitoso que lo requería.
    /// </summary>
    public async Task<bool> CompletarCedulaAsync(string cedula)
    {
        var credenciales = await ObtenerCredencialesAsync();
        if (credenciales is null)
        {
            return false;
        }

        var ciNormalizada = AfiliadosService.NormalizarCi(cedula);
        if (ciNormalizada.Length == 0)
        {
            return false;
        }

        var actualizado = await _afiliados.ActualizarCedulaAsync(credenciales.NumeroRegistro, ciNormalizada);
        if (!actualizado)
        {
            return false;
        }

        // Refrescar credenciales con el nuevo CI
        credenciales.CI = ciNormalizada;
        await _localStorage.SetItemAsync(ClaveCredenciales, JsonSerializer.Serialize(credenciales, OpcionesJson));

        _estadoActual = new AuthenticationState(CrearPrincipal(credenciales));
        NotifyAuthenticationStateChanged(Task.FromResult(_estadoActual));

        return true;
    }

    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemAsync(ClaveCredenciales);
        await _localStorage.RemoveItemAsync(ClaveSesion);

        _estadoActual = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        NotifyAuthenticationStateChanged(Task.FromResult(_estadoActual));
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

            var credenciales = await ObtenerCredencialesAsync();
            if (credenciales is null)
            {
                return false;
            }

            // Admin: restaurar sin validar contra lista
            if (credenciales.Rol == "Admin")
            {
                _estadoActual = new AuthenticationState(CrearPrincipal(credenciales));
                NotifyAuthenticationStateChanged(Task.FromResult(_estadoActual));
                return true;
            }

            // Staff: restaurar sin validar contra lista (credenciales propias)
            if (credenciales.Rol == "Staff")
            {
                _estadoActual = new AuthenticationState(CrearPrincipal(credenciales));
                NotifyAuthenticationStateChanged(Task.FromResult(_estadoActual));
                return true;
            }

            var validacion = await _afiliados.ValidarAccesoAsync(
                credenciales.NumeroRegistro, credenciales.CI, credenciales.Nombre);

            if (!validacion.Exitoso)
            {
                await LogoutAsync();
                return false;
            }

            _estadoActual = new AuthenticationState(CrearPrincipal(credenciales));
            NotifyAuthenticationStateChanged(Task.FromResult(_estadoActual));
            return true;
        }
        catch
        {
            await LogoutAsync();
            return false;
        }
    }

    public async Task<ColegaCredenciales?> ObtenerCredencialesAsync()
    {
        var json = await _localStorage.GetItemAsync(ClaveCredenciales);
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<ColegaCredenciales>(json, OpcionesJson);
    }

    private static ClaimsPrincipal CrearPrincipal(ColegaCredenciales credenciales)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, credenciales.Nombre),
            new("NumeroRegistro", credenciales.NumeroRegistro),
            new(ClaimTypes.Sid, credenciales.CI),
            new("FechaLogin", credenciales.FechaLogin.ToString("o")),
            new(ClaimTypes.Role, credenciales.Rol)
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "CredencialesCAO"));
    }
}

public sealed class ColegaCredenciales
{
    public string Nombre { get; set; } = string.Empty;
    public string NumeroRegistro { get; set; } = string.Empty;
    public string CI { get; set; } = string.Empty;
    public DateTimeOffset FechaLogin { get; set; } = DateTimeOffset.Now;
    public bool Recordar { get; set; } = true;
    public string Rol { get; set; } = "Colegiado";
}

public sealed record ResultadoLogin(bool Exitoso, string Mensaje, bool RequiereCi, string Rol);