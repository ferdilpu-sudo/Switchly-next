using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Switchly.EngineSpike;

/// <summary>
/// Minimal Kestrel HTTP server on an ephemeral loopback port, serving a single
/// trivial page. Used as the local synthetic test origin for the CEF isolation
/// and persistence probes (cookies/localStorage require a real http(s) origin;
/// file:// and data: origins are unsuitable).
/// </summary>
public sealed class TestOriginServer : IDisposable
{
    private readonly WebApplication _app;

    public string Origin { get; }

    private TestOriginServer(WebApplication app, string origin)
    {
        _app = app;
        Origin = origin;
    }

    public static TestOriginServer Start()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();
        app.MapGet("/", () => Results.Text(
            "<!doctype html><title>probe</title><body>ok</body>", "text/html"));

        app.StartAsync().GetAwaiter().GetResult();

        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses
                        ?? throw new InvalidOperationException("No server addresses bound.");
        var origin = addresses.First(a => a.StartsWith("http://127.0.0.1:", StringComparison.Ordinal));
        return new TestOriginServer(app, origin);
    }

    public void Dispose()
    {
        try { _app.StopAsync().GetAwaiter().GetResult(); }
        catch { /* best-effort */ }
    }
}
