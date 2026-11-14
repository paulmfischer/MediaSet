using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Serilog;
using MediaSet.Api.Shared.Extensions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Observability;

[TestFixture]
public class SeqApiKeyConfigurationTests
{
    private static IConfiguration Configure(string? apiKey)
    {
        var values = new Dictionary<string, string?>();
        if (apiKey is not null)
        {
            values["ExternalLogging:SeqApiKey"] = apiKey;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Test]
    public void GetSeqApiKey_KeyConfigured_ReturnsKey()
    {
        Assert.That(LoggingExtensions.GetSeqApiKey(Configure("TEST_KEY")), Is.EqualTo("TEST_KEY"));
    }

    [Test]
    public void GetSeqApiKey_KeyHasSurroundingWhitespace_ReturnsTrimmedKey()
    {
        Assert.That(LoggingExtensions.GetSeqApiKey(Configure("  TEST_KEY \n")), Is.EqualTo("TEST_KEY"));
    }

    [Test]
    public void GetSeqApiKey_KeyHasControlCharacters_ReturnsNull()
    {
        Assert.That(LoggingExtensions.GetSeqApiKey(Configure("TEST\nKEY")), Is.Null);
    }

    [TestCase("TEST_KEY", "TEST_KEY")]
    [TestCase(null, null)]
    public async Task WriteToConfiguredSeq_KeyConfigured_SendsApiKeyHeader(string? key, string? expected)
    {
        using var listener = new HttpListener();
        int port;
        using (var probe = new TcpListener(IPAddress.Loopback, 0))
        {
            probe.Start();
            port = ((IPEndPoint)probe.LocalEndpoint).Port;
        }

        var url = $"http://127.0.0.1:{port}/";
        listener.Prefixes.Add(url);
        listener.Start();
        var received = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            var header = ctx.Request.Headers["X-Seq-ApiKey"];
            ctx.Response.StatusCode = 201;
            ctx.Response.Close();
            return header;
        });

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ExternalLogging:SeqUrl"] = url,
            ["ExternalLogging:SeqApiKey"] = key
        }).Build();

        var logger = new LoggerConfiguration().WriteTo.WriteToConfiguredSeq(config).CreateLogger();
        logger.Information("hello");
        logger.Dispose();

        var winner = await Task.WhenAny(received, Task.Delay(TimeSpan.FromSeconds(15)));
        Assert.That(winner, Is.SameAs(received), "Seq sink never posted the event");
        Assert.That(await received, Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void GetSeqApiKey_KeyMissingOrBlank_ReturnsNull(string? value)
    {
        Assert.That(LoggingExtensions.GetSeqApiKey(Configure(value)), Is.Null);
    }
}
