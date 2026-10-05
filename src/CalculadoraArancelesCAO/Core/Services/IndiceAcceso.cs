using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CalculadoraArancelesCAO.Core.Services;

/// <summary>
/// Indice de acceso que viaja dentro de la aplicacion.
///
/// Diseno de privacidad: el indice NO contiene nombres ni cedulas en texto plano.
/// Por cada afiliado solo se guarda su numero de registro y un hash SHA-256 de
/// su credencial (cedula de identidad normalizada, o el nombre normalizado cuando
/// el Colegio todavia no registro la CI). Quien extraiga el archivo del APK obtiene
/// hashes, no datos personales, y no puede revertir el SHA-256.
///
/// El hash se calcula sobre registro + credencial, de modo que una cedula robada no
/// sirva con otro numero de registro.
/// </summary>
public static class IndiceAcceso
{
    /// <summary>Version del formato del indice. Cambiarla invalida indices antiguos.</summary>
    public const int VersionFormato = 2;

    /// <summary>
    /// Sal del hash. No es un secreto (viaja en el cliente) pero evita que alguien
    /// precalcule una tabla de hashes a partir de una lista de cedulas conocidas.
    /// </summary>
    public const string Sal = "CAO_ORURO_ACCESO_v1";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Normaliza un numero de registro a su forma canonica: solo digitos, sin ceros
    /// a la izquierda. El Excel suele traer "0077" y el afiliado escribe "77"; ambos
    /// deben aperturar la misma sesion y producir el mismo hash.
    /// </summary>
    public static string NormalizarRegistro(string? texto)
    {
        var base_ = NormalizarCredencial(texto);

        var digitos = new StringBuilder(base_.Length);
        foreach (var c in base_)
        {
            if (char.IsDigit(c))
            {
                digitos.Append(c);
            }
        }

        var canonico = digitos.ToString().TrimStart('0');
        return canonico.Length > 0 ? canonico : (digitos.Length > 0 ? "0" : string.Empty);
    }

    /// <summary>
    /// Normaliza una cedula o un nombre para el hash: solo letras y digitos en
    /// mayusculas, sin acentos ni signos. Quitar los diacriticos es indispensable:
    /// el Colegio escribe "MÉNDEZ" y el afiliado escribe "mendez" en el telefono.
    /// </summary>
    public static string NormalizarCredencial(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        // FormD separa cada letra acentuada en su letra base mas un acento combinante,
        // que despues se descarta.
        var sinAcentos = texto.ToUpperInvariant().Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(sinAcentos.Length);
        foreach (var c in sinAcentos)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Normaliza un nombre para el hash. Delega en
    /// <see cref="NormalizarCredencial"/> porque el criterio es el mismo.
    /// </summary>
    public static string NormalizarNombre(string? texto) => NormalizarCredencial(texto);

    /// <summary>
    /// Calcula la clave de acceso de un afiliado. Se usa la cedula cuando existe;
    /// si el Colegio no tiene CI registrada, se recurre al nombre.
    /// </summary>
    public static string CalcularClave(string numeroRegistro, string? cedula, string? nombre)
    {
        var registro = NormalizarRegistro(numeroRegistro);

        var credencial = NormalizarCredencial(cedula);
        if (credencial.Length == 0)
        {
            credencial = NormalizarNombre(nombre);
        }

        return CalcularClave(registro, credencial);
    }

    /// <summary>Calcula el hash de acceso a partir de registro y credencial ya normalizados.</summary>
    public static string CalcularClave(string registroNormalizado, string credencialNormalizada)
    {
        var material = $"{registroNormalizado}|{credencialNormalizada}|{Sal}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Construye el indice a partir de la lista completa de afiliados.
    /// Descarta las filas sin numero de registro.
    /// </summary>
    public static IndiceAccesoArchivo Construir(IEnumerable<EntradaAfiliado> afiliados)
    {
        var entradas = new List<EntradaIndice>();

        foreach (var afiliado in afiliados)
        {
            var registro = NormalizarRegistro(afiliado.NumeroRegistro);
            if (registro.Length == 0)
            {
                continue;
            }

            var cedula = NormalizarCredencial(afiliado.CI);
            var credencial = cedula.Length > 0 ? cedula : NormalizarNombre(afiliado.Nombre);

            if (credencial.Length == 0)
            {
                continue;
            }

            var entrada = new EntradaIndice
            {
                R = registro,
                // Marca si la credencial es una CI, para poder pedirla al usuario.
                C = cedula.Length > 0,
                H = CalcularClave(registro, credencial)
            };

            // Si el mismo registro aparece dos veces, gana la entrada que exige CI:
            // es la mas restrictiva y no deja pasar a un homonimo por nombre.
            var existente = entradas.FindIndex(e => e.R == registro);
            if (existente >= 0)
            {
                if (entradas[existente].C || !entrada.C)
                {
                    continue;
                }

                entradas[existente] = entrada;
                continue;
            }

            entradas.Add(entrada);
        }

        return new IndiceAccesoArchivo
        {
            Version = VersionFormato,
            Generado = DateTimeOffset.Now.ToString("yyyy-MM-dd"),
            Total = entradas.Count,
            Entradas = entradas
        };
    }

    /// <summary>
    /// Busca en el indice una credencial que coincida con el registro. Primero
    /// prueba con la cedula y luego con el nombre, para cubrir tanto a los
    /// afiliados con CI registrada como a los que todavia no la tienen.
    ///
    /// Cuando el Colegio ya tiene CI registrada (marca C), el nombre NO habilita el
    /// acceso: de lo contrario bastaria conocer el nombre de un colega para entrar
    /// sin su cedula.
    /// </summary>
    public static bool Coincide(IndiceAccesoArchivo indice, string numeroRegistro, string? cedula, string? nombre)
    {
        if (indice?.Entradas is null || indice.Entradas.Count == 0)
        {
            return false;
        }

        var registro = NormalizarRegistro(numeroRegistro);
        if (registro.Length == 0)
        {
            return false;
        }

        var entradas = indice.Entradas.Where(e => e.R == registro).ToList();
        if (entradas.Count == 0)
        {
            return false;
        }

        // Intento 1: cedula de identidad.
        var credencial = NormalizarCredencial(cedula);
        if (credencial.Length > 0)
        {
            var clave = CalcularClave(registro, credencial);
            if (entradas.Any(e => e.H == clave))
            {
                return true;
            }
        }

        // Intento 2: nombre normalizado, solo para entradas sin CI registrada.
        var nombreNorm = NormalizarNombre(nombre);
        if (nombreNorm.Length > 0)
        {
            var clave = CalcularClave(registro, nombreNorm);
            if (entradas.Any(e => !e.C && e.H == clave))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Indica si el indice exige cedula para el registro indicado.</summary>
    public static bool ExigeCedula(IndiceAccesoArchivo indice, string numeroRegistro)
    {
        var registro = NormalizarRegistro(numeroRegistro);
        if (registro.Length == 0 || indice?.Entradas is null)
        {
            return false;
        }

        return indice.Entradas.Any(e => e.R == registro && e.C);
    }

    /// <summary>
    /// Genera el JSON del indice en formato compacto, listo para publicarse.
    /// </summary>
    public static string Serializar(IndiceAccesoArchivo indice)
        => JsonSerializer.Serialize(indice, OpcionesJson);

    /// <summary>Interpreta el JSON del indice. Devuelve null si el contenido no es valido.</summary>
    public static IndiceAccesoArchivo? Deserializar(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var indice = JsonSerializer.Deserialize<IndiceAccesoArchivo>(json, OpcionesJson);
            if (indice?.Entradas is null || indice.Version != VersionFormato)
            {
                return null;
            }

            return indice;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Contenido del archivo indice-acceso.json.</summary>
public sealed class IndiceAccesoArchivo
{
    public int Version { get; set; }
    public string Generado { get; set; } = string.Empty;
    public int Total { get; set; }
    public List<EntradaIndice> Entradas { get; set; } = new();
}

/// <summary>
/// Entrada del indice. Las propiedades se abrevian para que el archivo pese lo
/// minimo posible: R = registro, C = exige cedula, H = hash de acceso.
/// </summary>
public sealed class EntradaIndice
{
    public string R { get; set; } = string.Empty;
    public bool C { get; set; }
    public string H { get; set; } = string.Empty;
}

/// <summary>Datos minimos necesarios para construir una entrada del indice.</summary>
public sealed class EntradaAfiliado
{
    public string NumeroRegistro { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string CI { get; set; } = string.Empty;
}