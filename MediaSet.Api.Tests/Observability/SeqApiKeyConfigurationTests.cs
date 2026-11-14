using System.Collections.Generic;
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

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void GetSeqApiKey_KeyMissingOrBlank_ReturnsNull(string? value)
    {
        Assert.That(LoggingExtensions.GetSeqApiKey(Configure(value)), Is.Null);
    }
}
