using System.Linq;
using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Features.Plex.Services;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Features.Plex.Services;

[TestFixture]
public class PlexConfigurationValidatorTests
{
    private const string Token = "super-secret-token-value";

    private static PlexConfiguration Valid(string? schedule = null, bool enabled = true) => new()
    {
        BaseUrl = "http://plex.local:32400",
        Token = Token,
        Entities = { ["Movies"] = new PlexEntityConfiguration { Enabled = enabled, Schedule = schedule ?? PlexConfiguration.DefaultSchedule } }
    };

    [Test]
    public void Validate_ValidMoviesEntity_EnablesIntegrationAndMovies()
    {
        var result = PlexConfigurationValidator.Validate(Valid());

        Assert.That(result.IntegrationEnabled, Is.True);
        Assert.That(result.EnabledEntityTypes, Is.EqualTo(["movies"]));
        Assert.That(result.Messages, Is.Empty);
    }

    [Test]
    public void Validate_DefaultConfiguration_UsesDocumentedDefaults()
    {
        var config = new PlexConfiguration();
        var entity = new PlexEntityConfiguration();

        Assert.That(config.Timeout, Is.EqualTo(30));
        Assert.That(config.AcceptInvalidCertificate, Is.False);
        Assert.That(entity.Schedule, Is.EqualTo("0 3 * * 0"));
        Assert.That(entity.LibrarySectionIds, Is.Empty);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not a url")]
    [TestCase("ftp://plex.local")]
    public void Validate_BadBaseUrl_DisablesWholeIntegration(string baseUrl)
    {
        var config = Valid();
        config.BaseUrl = baseUrl;

        var result = PlexConfigurationValidator.Validate(config);

        Assert.That(result.IntegrationEnabled, Is.False);
        Assert.That(result.EnabledEntityTypes, Is.Empty);
        Assert.That(result.Messages.Single(), Does.Contain("BaseUrl"));
    }

    [Test]
    public void Validate_MissingToken_DisablesWholeIntegration()
    {
        var config = Valid();
        config.Token = "";

        var result = PlexConfigurationValidator.Validate(config);

        Assert.That(result.IntegrationEnabled, Is.False);
        Assert.That(result.Messages.Single(), Does.Contain("Token"));
    }

    [Test]
    public void Validate_NonPositiveTimeout_DisablesWholeIntegration()
    {
        var config = Valid();
        config.Timeout = 0;

        var result = PlexConfigurationValidator.Validate(config);

        Assert.That(result.IntegrationEnabled, Is.False);
    }

    [Test]
    public void Validate_BadCron_DisablesOnlyThatEntityType()
    {
        var result = PlexConfigurationValidator.Validate(Valid(schedule: "not a cron"));

        Assert.That(result.IntegrationEnabled, Is.True);
        Assert.That(result.EnabledEntityTypes, Is.Empty);
        Assert.That(result.Messages.Single(), Does.Contain("movies").And.Contain("invalid schedule"));
    }

    [Test]
    public void Validate_UnsupportedEntityKey_DisablesOnlyThatKey()
    {
        var config = Valid();
        config.Entities["Books"] = new PlexEntityConfiguration { Enabled = true };

        var result = PlexConfigurationValidator.Validate(config);

        Assert.That(result.IntegrationEnabled, Is.True);
        Assert.That(result.EnabledEntityTypes, Is.EqualTo(["movies"]));
        Assert.That(result.Messages.Single(), Does.Contain("Books").And.Contain("not supported"));
    }

    [Test]
    public void Validate_EntityNotEnabled_IsNotEnabledAndNotAnError()
    {
        var result = PlexConfigurationValidator.Validate(Valid(enabled: false));

        Assert.That(result.IntegrationEnabled, Is.True);
        Assert.That(result.EnabledEntityTypes, Is.Empty);
        Assert.That(result.Messages, Is.Empty);
    }

    [Test]
    public void Validate_AnyFailure_MessagesNeverContainToken()
    {
        var config = Valid(schedule: "bogus");
        config.Entities["Books"] = new PlexEntityConfiguration { Enabled = true };
        var broken = Valid();
        broken.BaseUrl = "";

        var messages = PlexConfigurationValidator.Validate(config).Messages
            .Concat(PlexConfigurationValidator.Validate(broken).Messages);

        Assert.That(messages, Is.Not.Empty);
        Assert.That(messages, Has.None.Contain(Token));
    }
}
