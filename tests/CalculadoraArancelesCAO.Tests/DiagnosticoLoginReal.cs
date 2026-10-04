using CalculadoraArancelesCAO.UI.Services;
using System.IO;

namespace CalculadoraArancelesCAO.Tests;

public sealed class DiagnosticoLoginReal
{
    private const string Ruta = @"D:\Arq. Victor Blanco\Documentos VW\CAO\Lista APP.xlsx";

    private sealed class Almacenamiento : Core.Services.ILocalStorageService
    {
        public Dictionary<string, string> Datos { get; } = new(StringComparer.Ordinal);

        public Task<string?> GetItemAsync(string clave) => Task.FromResult(Datos.TryGetValue(clave, out var v) ? v : null);
        public Task<T?> GetItemAsync<T>(string clave) => Task.FromResult<T?>(default);
        public Task SetItemAsync(string clave, string valor) { Datos[clave] = valor; return Task.CompletedTask; }
        public Task RemoveItemAsync(string clave) { Datos.Remove(clave); return Task.CompletedTask; }
    }

    [Fact]
    public void Diagnostico_Login_Victor()
    {
        if (!File.Exists(Ruta)) return;

        var servicio = new AfiliadosService(new Almacenamiento());
        using var stream = File.OpenRead(Ruta);
        var resultado = servicio.CargarDesdeExcelAsync(stream).GetAwaiter().GetResult();

        Console.WriteLine($"Carga: {resultado.Exitoso} - {resultado.Mensaje}");
        Console.WriteLine($"Total afiliados: {resultado.Afiliados.Count}");
        Console.WriteLine($"Con CI: {resultado.Afiliados.Count(a => a.TieneCedula)}");
        Console.WriteLine($"Sin CI: {resultado.Afiliados.Count(a => !a.TieneCedula)}");
        Console.WriteLine($"Omitidas: {resultado.TotalOmmitidas}");

        // Buscar a Victor
        var victor = resultado.Afiliados.FirstOrDefault(a => a.NumeroRegistro == "3107");
        if (victor != null)
        {
            Console.WriteLine($"\nVictor encontrado:");
            Console.WriteLine($"  Registro: {victor.NumeroRegistro}");
            Console.WriteLine($"  Nombre: '{victor.Nombre}'");
            Console.WriteLine($"  CI: '{victor.CI}'");
            Console.WriteLine($"  TieneCedula: {victor.TieneCedula}");

            // Probar validación
            var servicio2 = new AfiliadosService(new DiagnosticoLoginReal.Almacenamiento());
            // Re-cargar en el mismo servicio
            using var stream2 = File.OpenRead(Ruta);
            servicio2.CargarDesdeExcelAsync(stream2).GetAwaiter().GetResult();

            // Test 1: Admin login (hardcoded)
            Console.WriteLine("\n--- Test Admin Login (hardcoded) ---");
            var tipo = typeof(AutenticacionService);
            var metodo = typeof(AutenticacionService).GetMethod("EsAdmin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var adminResult = (bool)metodo.Invoke(null, new object[] { "VICTOR WILFREDO BLANCO COCA", "3107", "#Teresita24#" });
            Console.WriteLine($"Admin check (VICTOR WILFREDO BLANCO COCA): {adminResult}");

            var adminResult2 = (bool)metodo.Invoke(null, new object[] { "BLANCO COCA VICTOR WILFREDO", "3107", "#Teresita24#" });
            Console.WriteLine($"Admin check (BLANCO COCA VICTOR WILFREDO): {adminResult2}");

            // Test 2: ValidarAccesoAsync with CI
            Console.WriteLine("\n--- Test ValidarAccesoAsync con CI ---");
            var servicio3 = new AfiliadosService(new Almacenamiento());
            using var stream3 = File.OpenRead(Ruta);
            servicio3.CargarDesdeExcelAsync(stream3).GetAwaiter().GetResult();

            if (victor.TieneCedula)
            {
                var val1 = servicio3.ValidarAccesoAsync(victor.NumeroRegistro, victor.CI, victor.Nombre).GetAwaiter().GetResult();
                Console.WriteLine($"Validar con CI correcto: Exitoso={val1.Exitoso}, RequiereCi={val1.RequiereCi}, Msg={val1.Mensaje}");

                var val2 = servicio3.ValidarAccesoAsync(victor.NumeroRegistro, "", victor.Nombre).GetAwaiter().GetResult();
                Console.WriteLine($"Validar SIN CI: Exitoso={val2.Exitoso}, RequiereCi={val2.RequiereCi}, Msg={val2.Mensaje}");

                var val3 = servicio3.ValidarAccesoAsync(victor.NumeroRegistro, "9999999", victor.Nombre).GetAwaiter().GetResult();
                Console.WriteLine($"Validar CI incorrecto: Exitoso={val3.Exitoso}, Msg={val3.Mensaje}");
            }
        }
        else
        {
            Console.WriteLine("Victor NO encontrado en la lista");
        }

        // Mostrar algunos afiliados con CI
        Console.WriteLine("\n--- Primeros 5 afiliados CON CI ---");
        foreach (var a in resultado.Afiliados.Where(a => a.TieneCedula).Take(5))
        {
            Console.WriteLine($"  Reg={a.NumeroRegistro}, Nombre={a.Nombre}, CI={a.CI}");
        }

        // Mostrar algunos afiliados SIN CI
        Console.WriteLine("\n--- Primeros 5 afiliados SIN CI ---");
        foreach (var a in resultado.Afiliados.Where(a => !a.TieneCedula).Take(5))
        {
            Console.WriteLine($"  Reg={a.NumeroRegistro}, Nombre={a.Nombre}, CI='{a.CI}'");
        }
    }
}