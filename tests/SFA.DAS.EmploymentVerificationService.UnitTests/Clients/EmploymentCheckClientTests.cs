using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Language.Flow;
using Moq.Protected;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Clients;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Clients;

[TestFixture]
public class EmploymentCheckClientTests
{
    private Mock<HttpMessageHandler> handler = null!;
    private ISetup<HttpMessageHandler, Task<HttpResponseMessage>> send = null!;
    private Mock<TokenCredential> credential = null!;
    private Mock<IHostEnvironment> environment = null!;
    private IConfiguration configuration = null!;
    private HttpClient http = null!;
    private EmploymentCheckClient client = null!;
    private EmploymentCheckRequest request = null!;

    [SetUp]
    public void SetUp()
    {
        handler = new Mock<HttpMessageHandler>();
        send = handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        send.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        http = new HttpClient(handler.Object);
        credential = new Mock<TokenCredential>(MockBehavior.Strict);
        environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns(Environments.Development);
        configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EmploymentCheckApiUrl"] = "https://localhost:5221/api/EmploymentCheck/RegisterCheck",
            ["EmploymentCheckApiScope"] = "api://test-api/.default"
        }).Build();
        client = new EmploymentCheckClient(http, configuration, environment.Object,
            NullLogger<EmploymentCheckClient>.Instance, credential.Object);
        request = new EmploymentCheckRequest(Guid.NewGuid(), 1234567890, 123, 456, "not-sent",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc), 1, 1, DateTime.UtcNow);
    }

    [TearDown]
    public void TearDown() => http.Dispose();

    [TestCase(null, 123L)]
    [TestCase(456L, (long)int.MaxValue)]
    public async Task SendAsync_PostsMappedRequestWithoutAuthenticationInDevelopment(long? apprenticeshipId, long accountId)
    {
        request = request with { EmployerAccountIdentifier = accountId, ApprenticeshipId = apprenticeshipId };
        HttpRequestMessage outgoing = null!;
        string bodyText = null!;
        send.Returns(async (HttpRequestMessage message, CancellationToken token) =>
        {
            outgoing = message;
            bodyText = await message.Content!.ReadAsStringAsync(token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await client.SendAsync(request, CancellationToken.None);

        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        credential.VerifyNoOtherCalls();
        using var json = JsonDocument.Parse(bodyText);
        var body = json.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(outgoing.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(outgoing.RequestUri!.AbsoluteUri, Is.EqualTo(configuration["EmploymentCheckApiUrl"]));
            Assert.That(outgoing.Headers.Authorization, Is.Null);
            Assert.That(outgoing.Content!.Headers.ContentType!.MediaType, Is.EqualTo("application/json"));
            Assert.That(body.EnumerateObject().Select(x => x.Name), Is.EquivalentTo(new[]
                { "correlationId", "checkType", "uln", "apprenticeshipAccountId", "apprenticeshipId", "minDate", "maxDate" }));
            Assert.That(body.GetProperty("correlationId").GetGuid(), Is.EqualTo(request.CorrelationId));
            Assert.That(body.GetProperty("checkType").GetString(), Is.EqualTo("VerifyNotEmployed"));
            Assert.That(body.GetProperty("uln").GetInt64(), Is.EqualTo(request.ULN));
            Assert.That(body.GetProperty("apprenticeshipAccountId").GetInt32(), Is.EqualTo(accountId));
            Assert.That(body.GetProperty("apprenticeshipId").Deserialize<long?>(), Is.EqualTo(apprenticeshipId));
            Assert.That(body.GetProperty("minDate").GetDateTime(), Is.EqualTo(request.StartDate));
            Assert.That(body.GetProperty("maxDate").GetDateTime(), Is.EqualTo(request.EndDate));
        });
    }

    [TestCase((long)int.MaxValue + 1)]
    [TestCase((long)int.MinValue - 1)]
    public void SendAsync_RejectsAccountOverflowBeforeSending(long accountId)
    {
        request = request with { EmployerAccountIdentifier = accountId };

        Assert.ThrowsAsync<OverflowException>(() => client.SendAsync(request, CancellationToken.None));

        handler.Protected().Verify("SendAsync", Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [TestCase(200, "{\"errorType\":null,\"errorMessage\":null}")]
    [TestCase(200, "not-json")]
    [TestCase(204, "")]
    public void SendAsync_AcceptsSuccessfulStatusWithoutParsingBody(int status, string body)
    {
        send.ReturnsAsync(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) });

        Assert.DoesNotThrowAsync(() => client.SendAsync(request, CancellationToken.None));
    }

    [TestCase(400)]
    [TestCase(500)]
    [TestCase(302)]
    public void SendAsync_PropagatesNonSuccessStatusWithoutRetry(int status)
    {
        send.ReturnsAsync(new HttpResponseMessage((HttpStatusCode)status));

        var exception = Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(request, CancellationToken.None));

        Assert.That(exception!.StatusCode, Is.EqualTo((HttpStatusCode)status));
        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Test]
    public void SendAsync_PropagatesTransportFailure()
    {
        var failure = new HttpRequestException("Connection failed");
        send.ThrowsAsync(failure);

        var thrown = Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(request, CancellationToken.None));

        Assert.That(thrown, Is.SameAs(failure));
    }

    [Test]
    public void SendAsync_PreCancelledRequestDoesNotSend()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() => client.SendAsync(request, cancellation.Token));

        handler.Protected().Verify("SendAsync", Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        credential.VerifyNoOtherCalls();
    }

    [Test]
    public void SendAsync_CancelsInFlightRequest()
    {
        using var cancellation = new CancellationTokenSource();
        send.Returns(async (HttpRequestMessage _, CancellationToken token) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        Assert.CatchAsync<OperationCanceledException>(() => client.SendAsync(request, cancellation.Token));
    }

    [Test]
    public void SendAsync_PropagatesTimeout()
    {
        send.Returns(async (HttpRequestMessage _, CancellationToken token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        http.Timeout = TimeSpan.FromMilliseconds(50);

        var exception = Assert.CatchAsync<OperationCanceledException>(() => client.SendAsync(request, CancellationToken.None));

        Assert.That(exception!.InnerException, Is.TypeOf<TimeoutException>());
    }

    [TestCase("Production")]
    [TestCase("Staging")]
    [TestCase("")]
    public async Task SendAsync_AcquiresTokenPerRequestOutsideDevelopment(string environmentName)
    {
        environment.SetupGet(x => x.EnvironmentName).Returns(environmentName);
        using var cancellation = new CancellationTokenSource();
        credential.Setup(x => x.GetTokenAsync(It.Is<TokenRequestContext>(context =>
                context.Scopes.SequenceEqual(new[] { configuration["EmploymentCheckApiScope"]! })), cancellation.Token))
            .ReturnsAsync(new AccessToken("fake-token", DateTimeOffset.UtcNow.AddMinutes(5)));
        var headers = new List<AuthenticationHeaderValue?>();
        send.Callback<HttpRequestMessage, CancellationToken>((message, _) => headers.Add(message.Headers.Authorization))
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK));

        await client.SendAsync(request, cancellation.Token);
        await client.SendAsync(request, cancellation.Token);

        Assert.That(headers.Select(header => header?.ToString()), Is.EqualTo(new[] { "Bearer fake-token", "Bearer fake-token" }));
        credential.Verify(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), cancellation.Token), Times.Exactly(2));
    }

    [Test]
    public void SendAsync_TokenAcquisitionCancellationPreventsHttpRequest()
    {
        environment.SetupGet(x => x.EnvironmentName).Returns(Environments.Production);
        using var cancellation = new CancellationTokenSource();
        credential.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), cancellation.Token))
            .Returns(() =>
            {
                cancellation.Cancel();
                return ValueTask.FromCanceled<AccessToken>(cancellation.Token);
            });

        Assert.CatchAsync<OperationCanceledException>(() => client.SendAsync(request, cancellation.Token));

        handler.Protected().Verify("SendAsync", Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Test]
    public void SendAsync_AuthenticationFailurePreventsHttpRequest()
    {
        environment.SetupGet(x => x.EnvironmentName).Returns(Environments.Production);
        var failure = new AuthenticationFailedException("Token acquisition failed");
        credential.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var thrown = Assert.ThrowsAsync<AuthenticationFailedException>(() => client.SendAsync(request, CancellationToken.None));

        Assert.That(thrown, Is.SameAs(failure));
        handler.Protected().Verify("SendAsync", Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Test]
    public async Task SendAsync_UsesConfiguredEndpoint()
    {
        configuration["EmploymentCheckApiUrl"] = "https://example.invalid/custom/register?version=1";
        Uri? endpoint = null;
        send.Callback<HttpRequestMessage, CancellationToken>((message, _) => endpoint = message.RequestUri)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        await client.SendAsync(request, CancellationToken.None);

        Assert.That(endpoint!.AbsoluteUri, Is.EqualTo(configuration["EmploymentCheckApiUrl"]));
    }
}
