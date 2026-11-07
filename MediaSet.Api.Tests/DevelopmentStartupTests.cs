using System.Collections.Generic;
using MediaSet.Api.Infrastructure.Lookup.Clients.Igdb;
using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Infrastructure.Lookup.Strategies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace MediaSet.Api.Tests;

[TestFixture]
public class DevelopmentStartupTests
{
    [Test]
    public void Startup_WithoutIgdbCredentials_DoesNotRegisterUnavailableGameLookup()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            // Program.cs reads configuration while building, before ConfigureAppConfiguration
            // overrides apply, so personal user-secrets (loaded in Development) would win.
            // UseSetting writes to the host configuration, which the app builder reads first
            // and which outranks user-secrets, so the result is machine independent.
            var settings = new Dictionary<string, string?>
            {
                ["IgdbConfiguration:ClientId"] = string.Empty,
                ["IgdbConfiguration:ClientSecret"] = string.Empty,
                ["MediaSetDatabaseSettings:ConnectionString"] = "mongodb://127.0.0.1:27017",
                ["ImageConfiguration:StoragePath"] = TestContext.CurrentContext.WorkDirectory,
                ["ExternalLogging:Enabled"] = "false",
                ["BackgroundImageLookupConfiguration:Enabled"] = "false"
            };
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

        // Accessing Services builds the Development host and validates its dependency graph.
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetService<IIgdbClient>(), Is.Null);
        Assert.That(scope.ServiceProvider.GetServices<ILookupStrategy<GameResponse>>(), Is.Empty);
    }
}
