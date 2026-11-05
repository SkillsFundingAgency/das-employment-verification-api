using System.Net;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Clients;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Extensions;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Clients;

[TestFixture]
public class EmploymentCheckClientTests
{
    [TestCase(null, 123L)]
    [TestCase(456L, (long)int.MaxValue)]
    public async Task SendAsync_PostsMappedRequestAndAcceptsAcknowledgement(long? apprenticeshipId, long accountId)
    {
        var request = Request() with { EmployerAccountIdentifier = accountId, ApprenticeshipId = apprenticeshipId };
        var calls = 0;
        using var handler = new StubHandler(async (message, token) =>
        {
            calls++;
            Assert.That(message.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(message.RequestUri!.AbsoluteUri, Is.EqualTo("https://localhost:5221/api/EmploymentCheck/RegisterCheck"));
            Assert.That(message.Headers.Authorization, Is.Null);
            Assert.That(message.Content!.Headers.ContentType!.MediaType, Is.EqualTo("application/json"));
            using var json = JsonDocument.Parse(await message.Content.ReadAsStringAsync(token));
            var body = json.RootElement;
            Assert.That(body.EnumerateObject().Select(x => x.Name), Is.EquivalentTo(new[]
            { "correlationId", "checkType", "uln", "apprenticeshipAccountId", "apprenticeshipId", "minDate", "maxDate" }));
            Assert.That(body.GetProperty("correlationId").GetGuid(), Is.EqualTo(request.CorrelationId));
            Assert.That(body.GetProperty("checkType").GetString(), Is.EqualTo("VerifyNotEmployed"));
            Assert.That(body.GetProperty("uln").GetInt64(), Is.EqualTo(request.ULN));
            Assert.That(body.GetProperty("apprenticeshipAccountId").GetInt32(), Is.EqualTo(accountId));
            Assert.That(body.GetProperty("apprenticeshipId").Deserialize<long?>(), Is.EqualTo(apprenticeshipId));
            Assert.That(body.GetProperty("minDate").GetDateTime(), Is.EqualTo(request.StartDate));
            Assert.That(body.GetProperty("maxDate").GetDateTime(), Is.EqualTo(request.EndDate));
            return Response();
        });
        using var http = Http(handler);
        await Client(http).SendAsync(request, CancellationToken.None);
        Assert.That(calls, Is.EqualTo(1));
    }

    [TestCase((long)int.MaxValue + 1)]
    [TestCase((long)int.MinValue - 1)]
    public void SendAsync_RejectsAccountOverflowBeforeSending(long accountId)
    {
        using var handler = new StubHandler((_, _) => throw new AssertionException("Must not send"));
        using var http = Http(handler);
        Assert.ThrowsAsync<OverflowException>(() => Client(http).SendAsync(Request() with { EmployerAccountIdentifier = accountId }, CancellationToken.None));
    }

    [TestCase("{}")]
    [TestCase("{\"ErrorType\":null,\"ErrorMessage\":null}")]
    [TestCase("{\"errorType\":\"\",\"errorMessage\":\"\"}")]
    public async Task SendAsync_AcceptsSuccessfulAcknowledgements(string json)
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(Response(json)));
        using var http = Http(handler);
        await Client(http).SendAsync(Request(), CancellationToken.None);
    }

    [TestCase("{\"errorType\":\"Bad_Data\"}")]
    [TestCase("{\"ErrorMessage\":\"Rejected\"}")]
    [TestCase("null")]
    public void SendAsync_RejectsApiLevelErrorsOrNullAcknowledgement(string json)
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(Response(json)));
        using var http = Http(handler);
        Assert.ThrowsAsync<HttpRequestException>(() => Client(http).SendAsync(Request(), CancellationToken.None));
    }

    [TestCase(400)]
    [TestCase(401)]
    [TestCase(403)]
    [TestCase(500)]
    [TestCase(302)]
    public void SendAsync_PropagatesNonSuccessStatusWithoutRetry(int status)
    {
        var calls = 0;
        using var handler = new StubHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status));
        });
        using var http = Http(handler);
        var exception = Assert.ThrowsAsync<HttpRequestException>(() => Client(http).SendAsync(Request(), CancellationToken.None));
        Assert.That(exception!.StatusCode, Is.EqualTo((HttpStatusCode)status));
        Assert.That(calls, Is.EqualTo(1));
    }

    [TestCase("")]
    [TestCase("not-json")]
    public void SendAsync_RejectsMalformedAcknowledgement(string json)
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(Response(json)));
        using var http = Http(handler);
        Assert.ThrowsAsync<JsonException>(() => Client(http).SendAsync(Request(), CancellationToken.None));
    }

    [Test]
    public void SendAsync_PropagatesTransportFailure()
    {
        var failure = new HttpRequestException("Connection failed");
        using var handler = new StubHandler((_, _) => throw failure);
        using var http = Http(handler);
        Assert.That(Assert.ThrowsAsync<HttpRequestException>(() => Client(http).SendAsync(Request(), CancellationToken.None)), Is.SameAs(failure));
    }

    [Test]
    public void SendAsync_PreCancelledRequestDoesNotSend()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var handler = new StubHandler((_, _) => throw new AssertionException("Must not send"));
        using var http = Http(handler);
        Assert.ThrowsAsync<OperationCanceledException>(() => Client(http).SendAsync(Request(), cancellation.Token));
    }

    [Test]
    public void SendAsync_CancelsInFlightRequest()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new StubHandler(async (_, token) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return Response();
        });
        using var http = Http(handler);
        Assert.CatchAsync<OperationCanceledException>(() => Client(http).SendAsync(Request(), cancellation.Token));
    }

    [Test]
    public void SendAsync_TimeoutPropagates()
    {
        using var handler = new StubHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Response();
        });
        using var http = Http(handler);
        http.Timeout = TimeSpan.FromMilliseconds(50);
        var exception = Assert.CatchAsync<OperationCanceledException>(() => Client(http).SendAsync(Request(), CancellationToken.None));
        Assert.That(exception!.InnerException, Is.TypeOf<TimeoutException>());
    }

    [TestCase("Production")]
    [TestCase("Staging")]
    [TestCase("")]
    public async Task Authentication_AcquiresTokenPerRequestWithScopeAndCancellation(string environment)
    {
        using var cancellation = new CancellationTokenSource();
        var configuration = Config(Settings());
        var credential = new Mock<TokenCredential>(MockBehavior.Strict);
        credential.Setup(x => x.GetTokenAsync(It.Is<TokenRequestContext>(c => c.Scopes.SequenceEqual(new[] { configuration["EmploymentCheckApiScope"]! })), cancellation.Token))
            .ReturnsAsync(new AccessToken("fake-token", DateTimeOffset.UtcNow.AddMinutes(5)));
        using var handler = new StubHandler((message, _) =>
        {
            Assert.That(message.Headers.Authorization!.Scheme, Is.EqualTo("Bearer"));
            Assert.That(message.Headers.Authorization.Parameter, Is.EqualTo("fake-token"));
            return Task.FromResult(Response());
        });
        using var http = Http(handler);
        var client = Client(http, environment, credential.Object);
        await client.SendAsync(Request(), cancellation.Token);
        await client.SendAsync(Request(), cancellation.Token);
        credential.Verify(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), cancellation.Token), Times.Exactly(2));
    }

    [Test]
    public void Authentication_TokenAcquisitionCancellationPreventsHttpRequest()
    {
        using var cancellation = new CancellationTokenSource();
        var credential = new Mock<TokenCredential>();
        credential.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), cancellation.Token))
            .Returns(() =>
            {
                cancellation.Cancel();
                return ValueTask.FromCanceled<AccessToken>(cancellation.Token);
            });
        using var handler = new StubHandler((_, _) => throw new AssertionException("Must not send"));
        using var http = Http(handler);
        Assert.CatchAsync<OperationCanceledException>(() => Client(http, Environments.Production, credential.Object).SendAsync(Request(), cancellation.Token));
    }

    [Test]
    public void Authentication_FailurePreventsHttpRequest()
    {
        var failure = new AuthenticationFailedException("Token acquisition failed");
        var credential = new Mock<TokenCredential>();
        credential.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        using var handler = new StubHandler((_, _) => throw new AssertionException("Must not send"));
        using var http = Http(handler);

        Assert.That(Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            Client(http, Environments.Production, credential.Object).SendAsync(Request(), CancellationToken.None)),
            Is.SameAs(failure));
    }

    [Test]
    public void Registration_UsesSingletonDefaultAzureCredential()
    {
        var settings = Settings();
        settings["EmploymentCheckApiUrl"] = "https://example.invalid";
        settings["AZURE_FUNCTIONS_ENVIRONMENT"] = "Production";
        settings["ServiceBusConnection"] = "not-used";
        settings["VerifyNotEmployedCompletedTopicName"] = "outgoing";
        using var provider = new ServiceCollection().AddSingleton(HostEnvironment(Environments.Production)).AddInfrastructure(Config(settings)).BuildServiceProvider();

        var credential = provider.GetRequiredService<TokenCredential>();
        Assert.That(credential, Is.TypeOf<DefaultAzureCredential>());
        Assert.That(credential, Is.SameAs(provider.GetRequiredService<TokenCredential>()));
        Assert.That(provider.GetRequiredService<IEmploymentCheckClient>(), Is.TypeOf<EmploymentCheckClient>());
    }

    [Test]
    public async Task Registration_ResolvesClientUsingInjectedCredentialAndConfiguration()
    {
        var settings = Settings();
        settings["EmploymentCheckApiUrl"] = "https://example.invalid/custom/register?version=1";
        settings["ServiceBusConnection"] = "not-used";
        settings["VerifyNotEmployedCompletedTopicName"] = "outgoing";
        var credential = new Mock<TokenCredential>();
        credential.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessToken("di-token", DateTimeOffset.UtcNow.AddMinutes(5)));
        var services = new ServiceCollection().AddSingleton(HostEnvironment(Environments.Production)).AddSingleton(credential.Object).AddInfrastructure(Config(settings));
        services.AddHttpClient<IEmploymentCheckClient, EmploymentCheckClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new StubHandler((message, _) =>
            {
                Assert.That(message.RequestUri!.AbsoluteUri, Is.EqualTo(settings["EmploymentCheckApiUrl"]));
                Assert.That(message.Headers.Authorization!.Parameter, Is.EqualTo("di-token"));
                return Task.FromResult(Response());
            }));
        await using var provider = services.BuildServiceProvider();
        var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IEmploymentCheckClient));
        Assert.That(http.Timeout, Is.EqualTo(TimeSpan.FromSeconds(100)));
        await provider.GetRequiredService<IEmploymentCheckClient>().SendAsync(Request(), CancellationToken.None);
    }

    private static EmploymentCheckRequest Request() => new(Guid.NewGuid(), 1234567890, 123, 456,
        "not-sent", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc), 1, 1, DateTime.UtcNow);
    private static HttpResponseMessage Response(string json = "{\"errorType\":null,\"errorMessage\":null}") =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
    private static HttpClient Http(HttpMessageHandler handler) => new(handler);
    private static EmploymentCheckClient Client(HttpClient http, string environment = "Development", TokenCredential? credential = null) =>
        new(http, Config(Settings()), HostEnvironment(environment), NullLogger<EmploymentCheckClient>.Instance,
            credential ?? new Mock<TokenCredential>(MockBehavior.Strict).Object);
    private static IHostEnvironment HostEnvironment(string environment) =>
        Mock.Of<IHostEnvironment>(host => host.EnvironmentName == environment);
    private static IConfiguration Config(Dictionary<string, string?> settings) => new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    private static Dictionary<string, string?> Settings() => new()
    {
        ["EmploymentCheckApiUrl"] = "https://localhost:5221/api/EmploymentCheck/RegisterCheck",
        ["EmploymentCheckApiScope"] = "api://test-api/.default"
    };
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
