using System.Globalization;
using CalculadoraArancelesCAO;
using CalculadoraArancelesCAO.Core.Services;
using CalculadoraArancelesCAO.UI.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using OfficeOpenXml;

var cultura = CultureInfo.CreateSpecificCulture("es-BO");
CultureInfo.DefaultThreadCurrentCulture = cultura;
CultureInfo.DefaultThreadCurrentUICulture = cultura;

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

builder.Services.AddSingleton<MatrixRepository>();
builder.Services.AddSingleton<TramiteRepository>();
builder.Services.AddSingleton<ArancelEngine>();

builder.Services.AddScoped<ILocalStorageService, LocalStorageService>();
builder.Services.AddScoped<ParametrosService>();
builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<CotizacionService>();
builder.Services.AddScoped<AfiliadosService>();

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AutenticacionService>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<AutenticacionService>());

await builder.Build().RunAsync();
