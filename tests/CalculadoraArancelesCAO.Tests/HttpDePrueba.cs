using System.Net;

namespace CalculadoraArancelesCAO.Tests;

/// <summary>
/// Crea un <see cref="HttpClient"/> sin red para las pruebas. Por defecto responde
/// 404, de modo que el indice de acceso no se cargue y cada prueba controle
/// explicitamente el contenido que necesita.
/// </summary>
internal static class HttpDePrueba
{
    public static HttpClient SinIndice()
        => new(new ManejadorVacio()) { BaseAddress = new Uri("https://localhost/") };

    /// <summary>Responde con el JSON dado al solicitar el indice de acceso.</summary>
    public static HttpClient ConIndice(string json)
        => new(new ManejadorIndice(json)) { BaseAddress = new Uri("https://localhost/") };

    private sealed class ManejadorVacio : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancelacion)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(string.Empty)
            });
    }

    private sealed class ManejadorIndice : HttpMessageHandler
    {
        private readonly string _json;

        public ManejadorIndice(string json) => _json = json;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancelacion)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, System.Text.Encoding.UTF8, "application/json")
            });
    }
}