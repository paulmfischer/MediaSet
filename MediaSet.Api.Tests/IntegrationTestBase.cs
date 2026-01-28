using NUnit.Framework;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace MediaSet.Api.Tests;

/// <summary>
/// Base class for integration tests using WebApplicationFactory.
/// Handles setup and cleanup of test resources like image storage directories.
/// </summary>
[TestFixture]
public abstract class IntegrationTestBase
{
    protected const string TestImageDirectory = "./test-images";
    
    [OneTimeSetUp]
    public virtual void OneTimeSetUp()
    {
        // Ensure test directory exists relative to the test project
        var testImagePath = Path.GetFullPath(TestImageDirectory);
        if (!Directory.Exists(testImagePath))
        {
            Directory.CreateDirectory(testImagePath);
        }
    }

    [OneTimeTearDown]
    public virtual void OneTimeTearDown()
    {
        // Clean up test directory after all tests in the fixture complete
        var testImagePath = Path.GetFullPath(TestImageDirectory);
        if (Directory.Exists(testImagePath))
        {
            try
            {
                Directory.Delete(testImagePath, recursive: true);
            }
            catch (Exception ex)
            {
                // Log but don't fail the test if cleanup fails
                TestContext.Out.WriteLine($"Warning: Failed to clean up test directory: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Configures WebApplicationFactory to use the test environment settings.
    /// This ensures appsettings.Testing.json is loaded and the content root is properly set.
    /// </summary>
    protected static WebApplicationFactory<Program> CreateWebApplicationFactory()
    {
        // Get the test project directory (where appsettings.Testing.json is located)
        var testProjectDir = Path.GetDirectoryName(typeof(IntegrationTestBase).Assembly.Location) 
            ?? throw new InvalidOperationException("Cannot determine test project directory");

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Set the content root to the test project directory so it finds appsettings.Testing.json
                builder.UseContentRoot(testProjectDir);
                builder.UseEnvironment("Testing");
                
                // Suppress all logging output during tests
                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.None);
                });
            });
    }
}
