using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Functions.Triggers;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Triggers;

[TestFixture]
public class PingHttpTriggerTests
{
    [Test]
    public async Task Run_ReturnsHttp200Ok()
    {
        using var body = new MemoryStream();
        var request = CreateRequest(body);

        var response = await new PingHttpTrigger().Run(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Run_ReturnsExactlyPong()
    {
        using var body = new MemoryStream();
        var request = CreateRequest(body);

        var response = await new PingHttpTrigger().Run(request);

        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body);
        Assert.That(await reader.ReadToEndAsync(), Is.EqualTo("pong"));
    }

    private static HttpRequestData CreateRequest(Stream body)
    {
        var context = Mock.Of<FunctionContext>();
        var response = new Mock<HttpResponseData>(context);
        response.SetupProperty(x => x.StatusCode);
        response.SetupGet(x => x.Body).Returns(body);
        response.SetupGet(x => x.Headers).Returns(new HttpHeadersCollection());

        var request = new Mock<HttpRequestData>(context);
        request.Setup(x => x.CreateResponse()).Returns(response.Object);
        return request.Object;
    }
}
