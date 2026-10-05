using System.Text.Json;
using System.Text.RegularExpressions;
using CalculadoraArancelesCAO.Core.Services;
using OfficeOpenXml;

namespace CalculadoraArancelesCAO.UI.Services;

public sealed class AfiliadosService
{
    private const string ClaveLista = "cao.afiliados.lista";
    private const string ClaveIndice = "cao.acceso.indice";
    private const long TamanoMaximo = 10L * 1024 * 1024;

    /// <summary>Ruta del indice que viaja dentro de la aplicacion.</summary>
    public const string RutaIndice = "data/indice-acceso.json";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    private readonly ILocalStorageService _localStorage;
    private readonly HttpClient _http;

    private IndiceAccesoArchivo? _indice;
    private bool _indiceCargado;

    public AfiliadosService(ILocalStorageService localStorage, HttpClient http)
    {
        _localStorage = localStorage;
        _http = http;
    }

    public async Task<ResultadoCarga> CargarDesdeExcelAsync(Stream archivoExcel)
    {
        var resultado = new ResultadoCarga();

        try
        {
            using var package = new ExcelPackage(archivoExcel);
            var hoja = package.Workbook.Worksheets.FirstOrDefault();

            if (hoja?.Dimension is null)
            {
                return Fallar("El archivo Excel esta vacio.");
            }

            var filaInicio = hoja.Dimension.Start.Row;
            var filaFin = hoja.Dimension.End.Row;
            var colInicio = hoja.Dimension.Start.Column;
            var colFin = hoja.Dimension.End.Column;

            var encabezados = new Dictionary<string, int>();
            for (var col = colInicio; col <= colFin; col++)
            {
                var texto = Normalizar(hoja.Cells[filaInicio, col].Text);
                if (texto.Length > 0 && !encabezados.ContainsKey(texto))
                {
                    encabezados[texto] = col;
                }
            }

            var colCorrelativo = BuscarColumna(encabezados,
                "numero correlativo", "numero", "correlativo", "n correlativo", "n de correlativo", "n");

            var colRegistro = BuscarColumna(encabezados,
                "registro nacional", "registro", "n de registro", "numero de registro",
                "n registro", "nro registro", "matricula", "n reg", "reg");

            var colNombre = BuscarColumna(encabezados,
                "apellidos y nombres", "apellidos y nombre",
                "nombres y apellidos", "nombre completo", "nombre", "apellidos",
                "paterno", "apellido paterno", "materno", "nombres", "colegiado");

            var colCi = BuscarColumna(encabezados,
                "cedula identidad", "cedula de identidad", "ci", "carnet",
                "documento", "identidad", "cedula");

            var colVigente = BuscarColumna(encabezados,
                "vigente", "activo", "estado", "habilitado", "situacion");

            // "N" y "N" son demasiado ambiguos: si el correlativo no se identifico
            // por nombre, se toma la primera columna libre de la izquierda.
            if (colCorrelativo == -1)
            {
                var ocupados = new[] { colRegistro, colNombre, colCi, colVigente };
                for (var col = colInicio; col < colFin; col++)
                {
                    if (!ocupados.Contains(col))
                    {
                        colCorrelativo = col;
                        break;
                    }
                }
            }

            if (colRegistro == -1 || colNombre == -1 || colCi == -1)
            {
                var faltantes = new List<string>();
                if (colRegistro == -1) faltantes.Add("Registro nacional");
                if (colNombre == -1) faltantes.Add("Apellidos y nombres");
                if (colCi == -1) faltantes.Add("Cedula identidad");

                return Fallar("Faltan columnas requeridas: " + string.Join(", ", faltantes) + ".");
            }

            var afiliados = new List<Afiliado>();
            var omitidas = 0;
            var sinCi = 0;

            for (var fila = filaInicio + 1; fila <= filaFin; fila++)
            {
                var registro = LimpiarNumero(hoja.Cells[fila, colRegistro].Text);
                var nombre = hoja.Cells[fila, colNombre].Text?.Trim() ?? string.Empty;
                var ci = NormalizarCi(hoja.Cells[fila, colCi].Text);

                // El CI no es obligatorio: en el archivo real del Colegio 235 de
                // 886 afiliados no tienen CI registrado. Solo se descartan las
                // filas sin numero de registro o sin nombre, que no permiten
                // identificar a nadie.
                if (registro.Length == 0 || nombre.Length == 0)
                {
                    omitidas++;
                    continue;
                }

                if (ci.Length == 0)
                {
                    sinCi++;
                }

                var vigente = true;
                if (colVigente != -1)
                {
                    var estado = Normalizar(hoja.Cells[fila, colVigente].Text);
                    vigente = !new[] { "no", "0", "inactivo", "baja", "anulado" }.Contains(estado);
                }

                afiliados.Add(new Afiliado
                {
                    Correlativo = colCorrelativo == -1
                        ? (afiliados.Count + 1).ToString()
                        : hoja.Cells[fila, colCorrelativo].Text?.Trim() ?? string.Empty,
                    NumeroRegistro = registro,
                    Nombre = nombre,
                    CI = ci,
                    Vigente = vigente
                });
            }

            if (afiliados.Count == 0)
            {
                return Fallar("No se encontraron filas validas. Revise que los datos Comiencen en la fila 2.");
            }

            await GuardarAsync(afiliados);

            resultado.Exitoso = true;
            resultado.Afiliados = afiliados;
            resultado.TotalOmmitidas = omitidas;
            resultado.SinCedula = sinCi;

            var detalle = omitidas > 0
                ? $", {omitidas} filas sin numero de registro fueron omitidas"
                : string.Empty;

            var aviso = sinCi > 0
                ? $" Hay {sinCi} afiliados sin cedula de identidad registrada; podran ingresar solo con su numero de registro y nombre."
                : string.Empty;

            resultado.Mensaje =
                $"Se cargaron {afiliados.Count} afiliados correctamente{detalle}.{aviso}";
        }
        catch (Exception ex)
        {
            resultado.Exitoso = false;
            resultado.Mensaje = $"Error al procesar el archivo: {ex.Message}";
        }

        return resultado;
    }

    public async Task<List<Afiliado>> ObtenerAfiliadosAsync()
    {
        var json = await _localStorage.GetItemAsync(ClaveLista);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<Afiliado>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<Afiliado>>(json, OpcionesJson) ?? new List<Afiliado>();
        }
        catch
        {
            return new List<Afiliado>();
        }
    }

    public async Task GuardarAsync(List<Afiliado> afiliados)
    {
        await _localStorage.SetItemAsync(ClaveLista, JsonSerializer.Serialize(afiliados, OpcionesJson));
    }

    /// <summary>
    /// Guarda la lista de afiliados manejando duplicados por número de registro.
    /// Devuelve tupla: (exitoso, mensaje, totalGuardados, duplicadosReemplazados, duplicadosMantenidos).
    /// </summary>
    public async Task<(bool Exitoso, string Mensaje, int TotalGuardados, int DuplicadosReemplazados, int DuplicadosMantenidos)>
        GuardarConManejoDuplicadosAsync(List<Afiliado> nuevosAfiliados, bool reemplazarDuplicados = true)
    {
        var existentes = await ObtenerAfiliadosAsync();
        var dictExistentes = existentes.ToDictionary(a => a.NumeroRegistro, a => a);

        int duplicadosReemplazados = 0;
        int duplicadosMantenidos = 0;

        foreach (var nuevo in nuevosAfiliados)
        {
            if (dictExistentes.TryGetValue(nuevo.NumeroRegistro, out var existente))
            {
                if (reemplazarDuplicados)
                {
                    dictExistentes[nuevo.NumeroRegistro] = nuevo;
                    duplicadosReemplazados++;
                }
                else
                {
                    duplicadosMantenidos++;
                }
            }
            else
            {
                dictExistentes[nuevo.NumeroRegistro] = nuevo;
            }
        }

        var listaFinal = dictExistentes.Values.ToList();
        await GuardarAsync(listaFinal);

        var mensaje = reemplazarDuplicados
            ? $"Se guardaron {listaFinal.Count} afiliados ({duplicadosReemplazados} duplicados reemplazados)."
            : $"Se guardaron {listaFinal.Count} afiliados ({duplicadosMantenidos} duplicados mantenidos sin cambios).";

        return (true, mensaje, listaFinal.Count, duplicadosReemplazados, duplicadosMantenidos);
    }

    public Task LimpiarAsync() => _localStorage.RemoveItemAsync(ClaveLista);

    // ===== Indice de acceso que viaja con la aplicacion =====
    //
    // La lista completa solo existe en los dispositivos donde el administrador
    // cargo el Excel. Para que un afiliado recien instalado pueda ingresar, el
    // indice (registro + hash de la credencial, sin nombres ni CI en texto plano)
    // se distribuye dentro de la app y se valida en el dispositivo.

    /// <summary>
    /// Devuelve el indice de acceso. Se reintenta la descarga en cada arranque para
    /// recoger afiliados nuevos, y si no hay conexion se usa la copia cacheada.
    ///
    /// La peticion se envia con no-cache porque los archivos estaticos de Vercel se
    /// sirven con cache largo: sin esto, un afiliado recien incorporado quedaria
    /// sin acceso indefinidamente en los telefonos que ya tenian la app instalada.
    /// </summary>
    public async Task<IndiceAccesoArchivo?> ObtenerIndiceAsync()
    {
        if (_indiceCargado)
        {
            return _indice;
        }

        _indiceCargado = true;

        var enCache = await _localStorage.GetItemAsync(ClaveIndice);
        _indice = IndiceAcceso.Deserializar(enCache);

        try
        {
            using var peticion = new HttpRequestMessage(HttpMethod.Get, RutaIndice);
            peticion.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true
            };

            using var respuesta = await _http.SendAsync(peticion);
            if (respuesta.IsSuccessStatusCode)
            {
                var json = await respuesta.Content.ReadAsStringAsync();
                var descargado = IndiceAcceso.Deserializar(json);

                // Solo se reescribe la cache si el contenido cambio de verdad.
                if (descargado is not null && json != enCache)
                {
                    await _localStorage.SetItemAsync(ClaveIndice, json);
                    _indice = descargado;
                }
            }
        }
        catch (HttpRequestException)
        {
            // Sin conexion: se sigue con la copia cacheada.
        }
        catch (TaskCanceledException)
        {
            // Sin conexion o timeout: se sigue con la copia cacheada.
        }

        return _indice;
    }

    /// <summary>Valida el acceso contra el indice de acceso embebido.</summary>
    public async Task<bool> ValidarConIndiceAsync(string numeroRegistro, string ci, string nombre)
    {
        var indice = await ObtenerIndiceAsync();
        if (indice is null)
        {
            return false;
        }

        return IndiceAcceso.Coincide(indice, numeroRegistro, ci, nombre);
    }

    /// <summary>Indica si el indice exige cedula de identidad para ese registro.</summary>
    public async Task<bool> IndiceExigeCedulaAsync(string numeroRegistro)
    {
        var indice = await ObtenerIndiceAsync();
        return indice is not null && IndiceAcceso.ExigeCedula(indice, numeroRegistro);
    }

    /// <summary>Cantidad de afiliados incluidos en el indice; 0 si no hay indice.</summary>
    public async Task<int> ObtenerTotalIndiceAsync()
    {
        var indice = await ObtenerIndiceAsync();
        return indice?.Total ?? 0;
    }

    /// <summary>
    /// Genera el contenido de data/indice-acceso.json a partir de la lista que el
    /// administrador tiene cargada. Ese archivo se versiona en el repositorio para
    /// que viaje con cada nueva instalacion.
    /// </summary>
    public async Task<(bool Exitoso, string Json, int Total)> GenerarIndiceAsync()
    {
        var lista = await ObtenerAfiliadosAsync();
        if (lista.Count == 0)
        {
            return (false, string.Empty, 0);
        }

        var entradas = lista.Select(a => new EntradaAfiliado
        {
            NumeroRegistro = a.NumeroRegistro,
            Nombre = a.Nombre,
            CI = a.CI
        });

        var indice = IndiceAcceso.Construir(entradas);
        return (true, IndiceAcceso.Serializar(indice), indice.Total);
    }

    /// <summary>Descarta el indice cacheado para forzar una recarga.</summary>
    public async Task InvalidarIndiceAsync()
    {
        _indice = null;
        _indiceCargado = false;
        await _localStorage.RemoveItemAsync(ClaveIndice);
    }

    // ===== Staff credentials (configurables por Admin) =====
    private const string ClaveStaff = "cao.staff.credenciales";

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

    public async Task<Afiliado?> BuscarAfiliadoAsync(string numeroRegistro, string ci)
    {
        var registro = LimpiarNumero(numeroRegistro);
        var ciNormalizado = NormalizarCi(ci);

        if (registro.Length == 0)
        {
            return null;
        }

        var afiliados = await ObtenerAfiliadosAsync();

        var porRegistro = afiliados.FirstOrDefault(a =>
            a.Vigente && LimpiarNumero(a.NumeroRegistro) == registro);

        if (porRegistro is null)
        {
            return null;
        }

        // Si el Colegio tiene CI registrado, este es obligatorio y debe coincidir.
        // Si no hay CI en el archivo, el registro por si solo es la credencial.
        if (porRegistro.CI.Length > 0 &&
            !porRegistro.CI.Equals(ciNormalizado, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return porRegistro;
    }

    public async Task<bool> ValidarAccesoAsync(string numeroRegistro, string ci)
    {
        var registro = LimpiarNumero(numeroRegistro);
        var ciNormalizado = NormalizarCi(ci);

        if (registro.Length == 0)
        {
            return false;
        }

        var lista = await ObtenerAfiliadosAsync();
        if (lista.Count == 0)
        {
            return false;
        }

        var afiliado = lista.FirstOrDefault(a =>
            a.Vigente && LimpiarNumero(a.NumeroRegistro) == registro);

        if (afiliado is null)
        {
            return false;
        }

        // Si el afiliado tiene CI en la lista, debe coincidir (o estar vacío en login).
        // Si no tiene CI en la lista, el acceso por registro es suficiente.
        if (afiliado.CI.Length > 0 && ciNormalizado.Length > 0 &&
            !afiliado.CI.Equals(ciNormalizado, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Valida el acceso indicando el motivo exacto del rechazo.
    /// - Si el afiliado TIENE CI en la lista: se exige que coincida (si se proveyó)
    ///   o se indica que falta CI (RequiereCi=true).
    /// - Si el afiliado NO TIENE CI en la lista: se permite ingreso con
    ///   registro + nombre y se pide CI para enrollamiento (RequiereCi=true).
    /// </summary>
    public async Task<ResultadoValidacion> ValidarAccesoAsync(
        string numeroRegistro, string ci, string nombre)
    {
        var registro = LimpiarNumero(numeroRegistro);
        var ciNormalizado = NormalizarCi(ci);

        if (registro.Length == 0)
        {
            return new ResultadoValidacion(false, "El numero de registro es obligatorio.", null, false);
        }

        var lista = await ObtenerAfiliadosAsync();
        if (lista.Count == 0)
        {
            return new ResultadoValidacion(
                false,
                "No hay lista de afiliados cargada. Contacte al administrador del Colegio.",
                null, false);
        }

        var normalizado = (nombre ?? string.Empty).Trim();
        if (normalizado.Length == 0)
        {
            return new ResultadoValidacion(false, "El nombre es obligatorio.", null, false);
        }

        var afiliado = lista.FirstOrDefault(a =>
            a.Vigente && LimpiarNumero(a.NumeroRegistro) == registro);

        if (afiliado is null)
        {
            return new ResultadoValidacion(
                false,
                "El numero de registro no figura en la lista de afiliados del Colegio.",
                null, false);
        }

        // Si el afiliado TIENE CI en la lista:
        // - Si se proveyó CI en el login, debe coincidir exactamente.
        // - Si NO se proveyó CI, se rechaza indicando que falta (RequiereCi=true).
        if (afiliado.CI.Length > 0)
        {
            if (ciNormalizado.Length > 0 &&
                !afiliado.CI.Equals(ciNormalizado, StringComparison.OrdinalIgnoreCase))
            {
                return new ResultadoValidacion(
                    false,
                    "La cedula de identidad no corresponde al numero de registro ingresado.",
                    null, false);
            }

            // Tiene CI en lista pero no proveyó ninguno en el login → pedirlo
            if (ciNormalizado.Length == 0)
            {
                return new ResultadoValidacion(
                    true,
                    "Su registro tiene CI asociado. Ingrese su cedula de identidad para acceder.",
                    afiliado, true);
            }
        }

        if (!CoincidenNombres(afiliado.Nombre, normalizado))
        {
            return new ResultadoValidacion(
                false,
                $"El nombre no coincide con el registrado. Figura como: {afiliado.Nombre}.",
                null, false);
        }

        // Éxito. Si no tiene CI en la lista, pedimos que lo ingrese (enrollamiento).
        var requiereCi = afiliado.CI.Length == 0;

        return new ResultadoValidacion(true, "Acceso concedido.", afiliado, requiereCi);
    }

    /// <summary>
    /// Actualiza el CI de un afiliado en la lista persistida (enrollamiento).
    /// </summary>
    public async Task<bool> ActualizarCedulaAsync(string numeroRegistro, string nuevaCi)
    {
        var registro = LimpiarNumero(numeroRegistro);
        var ciNormalizada = NormalizarCi(nuevaCi);

        if (registro.Length == 0 || ciNormalizada.Length == 0)
        {
            return false;
        }

        var lista = await ObtenerAfiliadosAsync();
        var idx = lista.FindIndex(a => a.Vigente && LimpiarNumero(a.NumeroRegistro) == registro);

        if (idx < 0)
        {
            return false;
        }

        // Solo actualiza si no tenía CI o si coincide con la existente (idempotente)
        if (lista[idx].CI.Length > 0 &&
            !lista[idx].CI.Equals(ciNormalizada, StringComparison.OrdinalIgnoreCase))
        {
            return false; // CI distinto al ya registrado → no sobrescribir silenciosamente
        }

        lista[idx].CI = ciNormalizada;
        await GuardarAsync(lista);
        return true;
    }

    private static bool CoincidenNombres(string registrado, string ingressado)
    {
        static string Simplificar(string texto) =>
            string.Concat(texto.ToUpperInvariant().Where(char.IsLetterOrDigit));

        var a = Simplificar(registrado);
        var b = Simplificar(ingressado);

        if (a.Length == 0 || b.Length == 0)
        {
            return false;
        }

        if (a == b)
        {
            return true;
        }

        // Tolerancia al orden invertido: "BLANCO VICTOR" vs "VICTOR BLANCO"
        return a.OrderBy(c => c).SequenceEqual(b.OrderBy(c => c));
    }

    private static ResultadoCarga Fallar(string mensaje) => new()
    {
        Exitoso = false,
        Mensaje = mensaje
    };

    private static int BuscarColumna(Dictionary<string, int> encabezados, params string[] candidatos)
    {
        foreach (var candidato in candidatos)
        {
            if (encabezados.TryGetValue(candidato, out var indice))
            {
                return indice;
            }
        }

        return -1;
    }

    private static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var minusculas = texto.Trim().ToLowerInvariant();

        // Los indicadores ordinales son "letras" para .NET, asi que se eliminan
        // antes de quitar los caracteres no alfanumericos: "Nº REG." -> "n reg".
        // (No se convierten en "n", porque "Nº REG." ya trae la letra N y
        // duplicarla produciria "nn reg".)
        minusculas = minusculas
            .Replace("º", string.Empty)
            .Replace("°", string.Empty)
            .Replace("ª", "a")
            .Replace("ᵒ", "o");

        minusculas = Regex.Replace(minusculas, @"[\s_\-]+", " ");
        minusculas = Regex.Replace(minusculas, @"[^\p{L}\p{N} ]", string.Empty);

        return Regex.Replace(minusculas, @"\s+", " ").Trim();
    }

    private static string LimpiarNumero(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var digitos = Regex.Replace(texto, @"\D", string.Empty);
        return digitos.TrimStart('0') is { Length: > 0 } limpio ? limpio : digitos;
    }

    /// <summary>
    /// Normaliza una cédula de identidad para comparaciones (mayúsculas, sin puntos/guiones, espacios unificados).
    /// </summary>
    public static string NormalizarCi(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var partes = texto.ToUpperInvariant()
            .Replace(".", string.Empty)
            .Replace("-", string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return string.Join(" ", partes);
    }
}

public class Afiliado
{
    public string Correlativo { get; set; } = string.Empty;
    public string NumeroRegistro { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string CI { get; set; } = string.Empty;
    public bool Vigente { get; set; } = true;

    /// <summary>Indica si el Colegio tiene CI registrado para este afiliado.</summary>
    public bool TieneCedula => CI.Length > 0;
}

public class ResultadoCarga
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public List<Afiliado> Afiliados { get; set; } = new();
    public int TotalOmmitidas { get; set; }
    public int SinCedula { get; set; }
}

public sealed record ResultadoValidacion(bool Exitoso, string Mensaje, Afiliado? Afiliado, bool RequiereCi = false);

public sealed class StaffCredencial
{
    public string NumeroRegistro { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string ClaveHash { get; set; } = string.Empty;
    public DateTimeOffset FechaCreacion { get; set; } = DateTimeOffset.Now;
}
