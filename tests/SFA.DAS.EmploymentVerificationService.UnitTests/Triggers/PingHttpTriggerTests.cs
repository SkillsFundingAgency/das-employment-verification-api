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
    public async Task Run_ReturnsHttp200WithPong()
    {
        using var body = new MemoryStream();
        var context = Mock.Of<FunctionContext>();
        var response = new Mock<HttpResponseData>(context);
        response.SetupProperty(x => x.StatusCode);
        response.SetupGet(x => x.Body).Returns(body);
        response.SetupGet(x => x.Headers).Returns(new HttpHeadersCollection());
        var request = new Mock<HttpRequestData>(context);
        request.Setup(x => x.CreateResponse()).Returns(response.Object);

        var result = await new PingHttpTrigger().Run(request.Object);

        result.Body.Position = 0;
        using var reader = new StreamReader(result.Body);
        var content = await reader.ReadToEndAsync();
        Assert.Multiple(() =>
        {
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(content, Is.EqualTo("pong"));
        });
    }
}
