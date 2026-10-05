using System.Security.Claims;
using System.Text.Json;
using CalculadoraArancelesCAO.Core.Services;
using CalculadoraArancelesCAO.UI.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace CalculadoraArancelesCAO.UI.Services;

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
    /// Mensaje unico para cualquier rechazo de acceso. No distingue entre registro
    /// inexistente y credencial incorrecta: si los mensajes fueran distintos,
    /// alguien podria enumerar la lista de colegiados probando valores.
    /// </summary>
    private const string MensajeAccesoDenegado = "Credenciales no validas. Verifique su numero de registro y su cedula de identidad.";

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
    /// Cantidad de afiliados que valida el indice embebido. Sirve para saber si el
    /// dispositivo puede dar acceso aunque no tenga la lista completa del Excel.
    /// </summary>
    public Task<int> ObtenerTotalIndiceAsync() => _afiliados.ObtenerTotalIndiceAsync();

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

        // 3. Validar contra la lista local del dispositivo. Solo existe en equipos
        //    donde el administrador cargo el Excel, y es la unica via que conoce el
        //    nombre del afiliado.
        var validacion = await _afiliados.ValidarAccesoAsync(numeroRegistro, ci, nombre);
        Afiliado? afiliado = validacion.Exitoso ? validacion.Afiliado : null;

        // 4. Si no hay lista local (instalacion nueva del APK), se valida contra el
        //    indice de acceso que viaja dentro de la aplicacion. El indice solo
        //    guarda el hash de la credencial, asi que aqui no se conoce el nombre:
        //    se identifica al afiliado por su numero de registro.
        if (afiliado is null)
        {
            var resultadoIndice = await ValidarPorIndiceAsync(numeroRegistro, ci, nombre);
            if (!resultadoIndice.Acceso)
            {
                // Si habia lista local se explica el motivo real; si no, es el
                // mensaje generico para no revelar si el registro existe.
                return new ResultadoLogin(
                    false,
                    validacion.Mensaje.Length > 0 && TieneListaLocal(validacion.Mensaje)
                        ? validacion.Mensaje
                        : MensajeAccesoDenegado,
                    false,
                    "Colegiado");
            }

            return await CrearSesionColegiadoIndiceAsync(
                resultadoIndice.Registro,
                resultadoIndice.UsaCedula ? ci : string.Empty,
                recordar);
        }

        var requiereCi = validacion.RequiereCi;

        // Si se requiere CI (falta en lista o falta en login aunque esté en lista),
        // NO crear credenciales aún; devolver RequiereCi para que UI lo pida.
        if (requiereCi)
        {
            return new ResultadoLogin(
                true, validacion.Mensaje, true, "Colegiado",
                afiliado.Nombre, afiliado.NumeroRegistro);
        }

        // Si proveyó CI, validar/actualizar en la lista
        if (ci.Trim().Length > 0)
        {
            var actualizado = await _afiliados.ActualizarCedulaAsync(numeroRegistro, ci);
            if (!actualizado)
            {
                return new ResultadoLogin(false, MensajeAccesoDenegado, false, "Colegiado");
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

        await MarcarActividadAsync();
        var requierePin = await RequierePinAsync(rol);

        _estadoActual = new AuthenticationState(CrearPrincipal(credenciales));
        NotifyAuthenticationStateChanged(Task.FromResult(_estadoActual));

        return new ResultadoLogin(
            true, "Acceso concedido.", false, rol,
            afiliado.Nombre, afiliado.NumeroRegistro, requierePin);
    }

    /// <summary>
    /// Indica si el mensaje proviene de una lista local realmente cargada.
    /// Los mensajes de "no figura" o "no coincide" tambien provienen de la lista
    /// local, pero se reemplazan por el generico para no enumerar registros.
    /// </summary>
    private static bool TieneListaLocal(string mensaje)
        => mensaje.Contains("No hay lista de afiliados", StringComparison.OrdinalIgnoreCase)
        || mensaje.Contains("es obligatorio", StringComparison.OrdinalIgnoreCase);

    private readonly record struct ResultadoIndice(bool Acceso, bool UsaCedula, string Registro);

    /// <summary>
    /// Valida contra el indice embebido. Exige la cedula cuando el Colegio ya la
    /// tiene registrada; en caso contrario acepta el nombre.
    /// </summary>
    private async Task<ResultadoIndice> ValidarPorIndiceAsync(
        string numeroRegistro, string ci, string nombre)
    {
        var registro = IndiceAcceso.NormalizarCredencial(numeroRegistro);
        if (registro.Length == 0)
        {
            return new ResultadoIndice(false, false, string.Empty);
        }

        var total = await _afiliados.ObtenerTotalIndiceAsync();
        if (total == 0)
        {
            return new ResultadoIndice(false, false, string.Empty);
        }

        var exigeCedula = await _afiliados.IndiceExigeCedulaAsync(registro);

        if (exigeCedula)
        {
            // Sin cedula se rechaza en vez de preguntar, para no filtrar que el
            // registro existe. La UI pide la CI y vuelve a llamar a LoginAsync.
            if (IndiceAcceso.NormalizarCredencial(ci).Length == 0)
            {
                return new ResultadoIndice(false, true, registro);
            }
        }
        else if (IndiceAcceso.NormalizarNombre(nombre).Length == 0)
        {
            return new ResultadoIndice(false, false, registro);
        }

        var acceso = await _afiliados.ValidarConIndiceAsync(numeroRegistro, ci, nombre);
        return new ResultadoIndice(acceso, exigeCedula, registro);
    }

    /// <summary>
    /// Crea la sesion de quien ingreso validado contra el indice. Sin lista local
    /// no se conoce el nombre, asi que la identidad visible es el numero de registro.
    /// </summary>
    private async Task<ResultadoLogin> CrearSesionColegiadoIndiceAsync(
        string registro, string ci, bool recordar)
    {
        var rol = await DeterminarRolAsync(registro);
        var credenciales = new ColegaCredenciales
        {
            Nombre = $"Colegiado N° {registro}",
            NumeroRegistro = registro,
            CI = ci,
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

        await MarcarActividadAsync();
        var requierePin = await RequierePinAsync(rol);

        var principalIndice = CrearPrincipal(credenciales);
        _estadoActual = new AuthenticationState(principalIndice);
        NotifyAuthenticationStateChanged(Task.FromResult(_estadoActual));

        return new ResultadoLogin(
            true, "Acceso concedido.", false, rol,
            $"Colegiado N° {registro}", registro, requierePin);
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

        var principalCedula = CrearPrincipal(credenciales);
        _estadoActual = new AuthenticationState(principalCedula);
        NotifyAuthenticationStateChanged(Task.FromResult(_estadoActual));

        return true;
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
    // El PIN protege el dispositivo: si el celular se presta con la sesion abierta,
    // al volver a abrir la aplicacion se pide el PIN. No sustituye a la verificacion
    // de CI + registro, solo agrega friccion al prestamo.

    /// <summary>Registra la interaccion del usuario para reiniciar el reloj de inactividad.</summary>
    public async Task MarcarActividadAsync()
        => await _localStorage.SetItemAsync(ClaveUltimaActividad, DateTimeOffset.Now.ToString("o"));

    /// <summary>Indica si el usuario debe desbloquear con su PIN.</summary>
    public async Task<bool> RequierePinAsync(string? rol)
    {
        if (rol is not ("Colegiado" or "Admin" or "Staff"))
        {
            return false;
        }

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

        await _localStorage.RemoveItemAsync(ClaveSesion);
        _estadoActual = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        NotifyAuthenticationStateChanged(Task.FromResult(_estadoActual));

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

            var credenciales = await ObtenerCredencialesAsync();
            if (credenciales is null)
            {
                return false;
            }

            await MarcarActividadAsync();

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

            // Colegiado: revalidar contra la lista local si existe. En instalaciones
            // nuevas la lista no esta presente y la validacion real ya se hizo en el
            // ingreso contra el indice, asi que se acepta la sesion guardada.
            var validacion = await _afiliados.ValidarAccesoAsync(
                credenciales.NumeroRegistro, credenciales.CI, credenciales.Nombre);

            if (!validacion.Exitoso)
            {
                var tieneLista = (await _afiliados.ObtenerAfiliadosAsync()).Count > 0;

                if (tieneLista && !await _afiliados.ValidarConIndiceAsync(
                        credenciales.NumeroRegistro, credenciales.CI, credenciales.Nombre))
                {
                    await LogoutAsync();
                    return false;
                }
            }

            var principalColegiado = CrearPrincipal(credenciales);
            _estadoActual = new AuthenticationState(principalColegiado);
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

/// <summary>
/// Resultado de un intento de ingreso.
/// - RequiereCi: hay que pedir la cedula antes de poder crear la sesion.
/// - NombreConfirmacion / RegistroConfirmacion: datos que se muestran al afiliado
///   para que confirme su identidad antes de entrar.
/// - RequierePin: hay que crear el PIN de desbloqueo del dispositivo.
/// </summary>
public sealed record ResultadoLogin(
    bool Exitoso,
    string Mensaje,
    bool RequiereCi,
    string Rol,
    string? NombreConfirmacion = null,
    string? RegistroConfirmacion = null,
    bool RequierePin = false);