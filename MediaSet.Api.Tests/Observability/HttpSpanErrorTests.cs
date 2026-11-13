using System.Net;
using System.Net.Http;
using MediaSet.Api.Shared.Extensions;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Observability;

[TestFixture]
public class HttpSpanErrorTests
{
    [Test]
    public void IsErrorResponse_WithNotFound_IsNotAnError()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound);

        Assert.That(LoggingExtensions.IsErrorResponse(response), Is.False);
    }

    [TestCase(HttpStatusCode.OK, false)]
    [TestCase(HttpStatusCode.BadRequest, true)]
    [TestCase(HttpStatusCode.Unauthorized, true)]
    [TestCase(HttpStatusCode.TooManyRequests, true)]
    [TestCase(HttpStatusCode.InternalServerError, true)]
    [TestCase(HttpStatusCode.ServiceUnavailable, true)]
    public void IsErrorResponse_WithOtherStatus_KeepsDefaultBehavior(HttpStatusCode status, bool expected)
    {
        using var response = new HttpResponseMessage(status);

        Assert.That(LoggingExtensions.IsErrorResponse(response), Is.EqualTo(expected));
    }
}
