using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Extensions;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Messaging;

[TestFixture]
public class SetupMessagingInfrastructureTests
{
    private const string CompletionFilter = "[NServiceBus.EnclosedMessageTypes] LIKE '%SFA.DAS.EmploymentCheck.Types.EmploymentCheckCompletedEvent%'";

    [Test]
    public void DisabledByDefault_DoesNotRegisterProvisionerOrAdminClient()
    {
        var settings = new Dictionary<string, string?>
        {
            ["ServiceBusConnection"] = "not-used",
            ["VerifyNotEmployedCompletedTopicName"] = "outgoing",
            ["EmploymentCheckApiUrl"] = "https://localhost:5221/api/EmploymentCheck/RegisterCheck"
        };
        var services = new ServiceCollection().AddInfrastructure(Config(settings));
        Assert.That(services.Any(x => x.ServiceType == typeof(ServiceBusAdministrationClient)), Is.False);
        Assert.That(services.Any(x => x.ServiceType == typeof(IHostedService)), Is.False);
    }

    [Test]
    public void EnabledAndReviewed_RegistersProvisioner()
    {
        var settings = Settings();
        settings["ServiceBusConnection"] = "not-used";
        settings["VerifyNotEmployedCompletedTopicName"] = "outgoing";
        var services = new ServiceCollection().AddInfrastructure(Config(settings));
        Assert.That(services.Single(x => x.ServiceType == typeof(IHostedService)).ImplementationType,
            Is.EqualTo(typeof(SetupMessagingInfrastructure)));
    }

    [TestCase(null)]
    [TestCase("false")]
    public void EnabledWithoutReview_IsRejected(string? reviewed)
    {
        var settings = Settings();
        settings["ServiceBusFiltersReviewed"] = reviewed;
        Assert.Throws<InvalidOperationException>(() => MessagingInfrastructureOptions.Read(Config(settings)));
    }

    [TestCase("")]
    [TestCase("1=1")]
    [TestCase("TRUE")]
    [TestCase("sys.Label LIKE '%' ")]
    [TestCase("sys.Label LIKE '___%'")]
    [TestCase("sys.Label = 'expected' OR 1=1")]
    [TestCase("'x' = 'x'")]
    public void UnrestrictedOrUnsupportedCommandFilters_AreRejected(string filter)
    {
        var settings = Settings();
        settings["VerifyNotEmployedFilter"] = filter;
        Assert.Throws<InvalidOperationException>(() => MessagingInfrastructureOptions.Read(Config(settings)));
    }

    [Test]
    public void MissingSecondRoute_IsRejectedBeforeAnyProvisioning()
    {
        var settings = Settings();
        settings.Remove("EmploymentCheckCompletedFilter");
        Assert.Throws<InvalidOperationException>(() => MessagingInfrastructureOptions.Read(Config(settings)));
    }

    [Test]
    public void FlatKeys_MapBothRoutesIncludingIndependentTopics()
    {
        var settings = Settings();
        settings["VerifyNotEmployedTopic"] = "command-topic";
        settings["EmploymentCheckCompletedTopic"] = "completion-topic";

        var options = MessagingInfrastructureOptions.Read(Config(settings));

        Assert.That(options.Enabled, Is.True);
        Assert.That(options.Routes, Is.EqualTo(new[]
        {
            new MessagingRoute("VerifyNotEmployed", "command-topic", "command-sub", "commands", "sys.Label = 'test-command'"),
            new MessagingRoute("EmploymentCheckCompletedEvent", "completion-topic", "completion-sub", "completions", CompletionFilter)
        }));
    }

    [TestCase("false")]
    [TestCase("true")]
    public void DisabledSetup_AllowsUnverifiedEmptyCommandFilterWithoutRegisteringAdministration(string reviewed)
    {
        var settings = Settings();
        settings["ServiceBusSetupEnabled"] = "false";
        settings["ServiceBusFiltersReviewed"] = reviewed;
        settings["VerifyNotEmployedFilter"] = "";
        settings["ServiceBusConnection"] = "not-used";
        settings["VerifyNotEmployedCompletedTopicName"] = "bundle-1";

        var options = MessagingInfrastructureOptions.Read(Config(settings));
        var services = new ServiceCollection().AddInfrastructure(Config(settings));

        Assert.That(options.Enabled, Is.False);
        Assert.That(options.Routes, Is.Empty);
        Assert.That(services.Any(x => x.ServiceType == typeof(ServiceBusAdministrationClient)), Is.False);
        Assert.That(services.Any(x => x.ServiceType == typeof(IHostedService)), Is.False);
    }

    [Test]
    public async Task CreatesBothRoutesWithAtomicFilters_AndIsIdempotent()
    {
        var admin = new InMemoryAdministrationClient();
        var provisioner = Create(admin);
        await provisioner.StartAsync(CancellationToken.None);
        await provisioner.StartAsync(CancellationToken.None);

        Assert.That(admin.Queues, Is.EquivalentTo(new[] { "commands", "completions" }));
        Assert.That(admin.CreatedSubscriptions, Has.Count.EqualTo(2));
        Assert.That(admin.Rules.Values.All(rules => rules.Count == 1), Is.True);
        Assert.That(((SqlRuleFilter)admin.Rules["completion-sub"].Single().Filter).SqlExpression, Is.EqualTo(CompletionFilter));
        Assert.That(admin.Subscriptions["command-sub"].ForwardTo, Is.EqualTo("commands"));
        Assert.That(admin.Subscriptions["completion-sub"].ForwardTo, Is.EqualTo("completions"));
    }

    [Test]
    public async Task RepairsOwnedFilterAndForwarding_RemovesOnlyEvsDefaults()
    {
        var admin = new InMemoryAdministrationClient();
        await Create(admin).StartAsync(CancellationToken.None);
        var rules = admin.Rules["command-sub"];
        rules[0].Filter = new TrueRuleFilter();
        rules[0].Action = new SqlRuleAction("SET unwanted = 'value'");
        rules.Add(ServiceBusModelFactory.RuleProperties("$Default", new TrueRuleFilter(), null));
        admin.Subscriptions["command-sub"].ForwardTo = "wrong-queue";
        admin.Rules["another-service"] = new() { ServiceBusModelFactory.RuleProperties("$Default", new TrueRuleFilter(), null) };

        await Create(admin).StartAsync(CancellationToken.None);

        Assert.That(rules, Has.Count.EqualTo(1));
        Assert.That(rules[0].Filter, Is.TypeOf<SqlRuleFilter>());
        Assert.That(rules[0].Action, Is.Null);
        Assert.That(admin.Subscriptions["command-sub"].ForwardTo, Is.EqualTo("commands"));
        Assert.That(admin.Rules["another-service"].Single().Name, Is.EqualTo("$Default"));
    }

    [Test]
    public async Task UnexpectedRules_FailWithoutDeletingThem()
    {
        var admin = new InMemoryAdministrationClient();
        await Create(admin).StartAsync(CancellationToken.None);
        admin.Rules["command-sub"].Add(ServiceBusModelFactory.RuleProperties("unowned", new TrueRuleFilter(), null));
        Assert.ThrowsAsync<InvalidOperationException>(() => Create(admin).StartAsync(CancellationToken.None));
        Assert.That(admin.Rules["command-sub"].Any(x => x.Name == "unowned"), Is.True);
    }

    [Test]
    public async Task MissingRule_IsCreatedBeforeDefaultIsDeleted()
    {
        var admin = new InMemoryAdministrationClient();
        await Create(admin).StartAsync(CancellationToken.None);
        admin.Rules["command-sub"] = new() { ServiceBusModelFactory.RuleProperties("$Default", new TrueRuleFilter(), null) };
        await Create(admin).StartAsync(CancellationToken.None);
        Assert.That(admin.Rules["command-sub"].Single().Name, Is.EqualTo("VerifyNotEmployed"));
    }

    [Test]
    public async Task ConcurrentCreatesAndDefaultDeletion_AreSafe()
    {
        var admin = new InMemoryAdministrationClient { SimulateCreateRaces = true, SimulateDeleteRace = true };
        await Create(admin).StartAsync(CancellationToken.None);
        admin.Rules["command-sub"] = new() { ServiceBusModelFactory.RuleProperties("$Default", new TrueRuleFilter(), null) };
        await Create(admin).StartAsync(CancellationToken.None);
        Assert.That(admin.Rules["command-sub"].Single().Name, Is.EqualTo("VerifyNotEmployed"));
    }

    [Test]
    public void CancellationAndPermissionErrors_Propagate()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var admin = new InMemoryAdministrationClient();
        Assert.ThrowsAsync<OperationCanceledException>(() => Create(admin).StartAsync(cancellation.Token));
        Assert.That(admin.Queues, Is.Empty);

        var failure = new RequestFailedException(403, "Manage permission required");
        var mock = new Mock<ServiceBusAdministrationClient>();
        mock.Setup(x => x.TopicExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        Assert.That(Assert.ThrowsAsync<RequestFailedException>(() => Create(mock.Object).StartAsync(CancellationToken.None)), Is.SameAs(failure));
    }

    [Test]
    public void MissingTopic_FailsWithoutCreatingQueues()
    {
        var mock = new Mock<ServiceBusAdministrationClient>(MockBehavior.Strict);
        mock.Setup(x => x.TopicExistsAsync("bundle-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));
        Assert.ThrowsAsync<InvalidOperationException>(() => Create(mock.Object).StartAsync(CancellationToken.None));
        mock.Verify(x => x.CreateQueueAsync(It.IsAny<CreateQueueOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void ConflictWithoutCreatedQueue_IsNotTreatedAsSuccess()
    {
        var mock = new Mock<ServiceBusAdministrationClient>(MockBehavior.Strict);
        mock.Setup(x => x.TopicExistsAsync("bundle-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
        mock.Setup(x => x.QueueExistsAsync("commands", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));
        mock.Setup(x => x.CreateQueueAsync(It.IsAny<CreateQueueOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(409, "Conflict unrelated to a completed create"));
        Assert.ThrowsAsync<InvalidOperationException>(() => Create(mock.Object).StartAsync(CancellationToken.None));
    }

    [Test]
    public void CancellationDuringAdministration_PropagatesWithoutContinuing()
    {
        using var cancellation = new CancellationTokenSource();
        var mock = new Mock<ServiceBusAdministrationClient>(MockBehavior.Strict);
        mock.Setup(x => x.TopicExistsAsync("bundle-1", cancellation.Token))
            .Returns(() =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<Response<bool>>(cancellation.Token);
            });
        Assert.ThrowsAsync<TaskCanceledException>(() => Create(mock.Object).StartAsync(cancellation.Token));
        mock.Verify(x => x.CreateQueueAsync(It.IsAny<CreateQueueOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task AllCallsReceiveStartupCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        var admin = new InMemoryAdministrationClient();
        await Create(admin).StartAsync(cancellation.Token);
        Assert.That(admin.Tokens, Is.Not.Empty);
        Assert.That(admin.Tokens.All(token => token == cancellation.Token), Is.True);
    }

    private static SetupMessagingInfrastructure Create(ServiceBusAdministrationClient admin) => new(admin,
        MessagingInfrastructureOptions.Read(Config(Settings())), NullLogger<SetupMessagingInfrastructure>.Instance);

    private static IConfiguration Config(Dictionary<string, string?> settings) => new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    private static Dictionary<string, string?> Settings() => new()
    {
        ["ServiceBusSetupEnabled"] = "true",
        ["ServiceBusFiltersReviewed"] = "true",
        ["VerifyNotEmployedQueueName"] = "commands",
        ["EmploymentCheckCompletedQueueName"] = "completions",
        ["VerifyNotEmployedTopic"] = "bundle-1",
        ["VerifyNotEmployedSubscription"] = "command-sub",
        // Synthetic test metadata only; this is not a claim about the real command publisher.
        ["VerifyNotEmployedFilter"] = "sys.Label = 'test-command'",
        ["EmploymentCheckCompletedTopic"] = "bundle-1",
        ["EmploymentCheckCompletedSubscription"] = "completion-sub",
        ["EmploymentCheckCompletedFilter"] = CompletionFilter,
        ["EmploymentCheckApiUrl"] = "https://localhost:5221/api/EmploymentCheck/RegisterCheck"
    };

    private sealed class InMemoryAdministrationClient : ServiceBusAdministrationClient
    {
        public HashSet<string> Queues { get; } = new();
        public Dictionary<string, SubscriptionProperties> Subscriptions { get; } = new();
        public Dictionary<string, List<RuleProperties>> Rules { get; } = new();
        public List<CreateSubscriptionOptions> CreatedSubscriptions { get; } = new();
        public List<CancellationToken> Tokens { get; } = new();
        public bool SimulateCreateRaces { get; init; }
        public bool SimulateDeleteRace { get; init; }
        private static Response<T> Result<T>(T value) => Response.FromValue(value, Mock.Of<Response>());
        private void Track(CancellationToken token) { Tokens.Add(token); token.ThrowIfCancellationRequested(); }
        private void Race()
        {
            if (SimulateCreateRaces) throw new ServiceBusException("Concurrent creation", ServiceBusFailureReason.MessagingEntityAlreadyExists);
        }

        public override Task<Response<bool>> TopicExistsAsync(string topicName, CancellationToken cancellationToken = default)
        { Track(cancellationToken); return Task.FromResult(Result(true)); }
        public override Task<Response<bool>> QueueExistsAsync(string name, CancellationToken cancellationToken = default)
        { Track(cancellationToken); return Task.FromResult(Result(Queues.Contains(name))); }
        public override Task<Response<QueueProperties>> CreateQueueAsync(CreateQueueOptions options, CancellationToken cancellationToken = default)
        { Track(cancellationToken); Queues.Add(options.Name); Race(); return Task.FromResult(Result<QueueProperties>(null!)); }
        public override Task<Response<bool>> SubscriptionExistsAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        { Track(cancellationToken); return Task.FromResult(Result(Subscriptions.ContainsKey(subscriptionName))); }
        public override Task<Response<SubscriptionProperties>> CreateSubscriptionAsync(CreateSubscriptionOptions options, CreateRuleOptions rule, CancellationToken cancellationToken = default)
        {
            Track(cancellationToken);
            Assert.That(rule.Filter, Is.TypeOf<SqlRuleFilter>());
            CreatedSubscriptions.Add(options);
            var subscription = ServiceBusModelFactory.SubscriptionProperties(options.TopicName, options.SubscriptionName,
                TimeSpan.FromMinutes(5), false, TimeSpan.FromDays(7), TimeSpan.MaxValue, true, 50, true, EntityStatus.Active, options.ForwardTo, null, "");
            Subscriptions.Add(options.SubscriptionName, subscription);
            Rules.Add(options.SubscriptionName, new() { ServiceBusModelFactory.RuleProperties(rule.Name, rule.Filter, null) });
            Race();
            return Task.FromResult(Result(subscription));
        }
        public override AsyncPageable<RuleProperties> GetRulesAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        { Track(cancellationToken); return AsyncPageable<RuleProperties>.FromPages(new[] { Page<RuleProperties>.FromValues(Rules[subscriptionName].ToArray(), null, Mock.Of<Response>()) }); }
        public override Task<Response<RuleProperties>> GetRuleAsync(string topicName, string subscriptionName, string ruleName, CancellationToken cancellationToken = default)
        { Track(cancellationToken); return Task.FromResult(Result(Rules[subscriptionName].Single(x => x.Name == ruleName))); }
        public override Task<Response<RuleProperties>> CreateRuleAsync(string topicName, string subscriptionName, CreateRuleOptions options, CancellationToken cancellationToken = default)
        {
            Track(cancellationToken);
            var rule = ServiceBusModelFactory.RuleProperties(options.Name, options.Filter, null);
            Rules[subscriptionName].Add(rule); Race(); return Task.FromResult(Result(rule));
        }
        public override Task<Response<RuleProperties>> UpdateRuleAsync(string topicName, string subscriptionName, RuleProperties rule, CancellationToken cancellationToken = default)
        { Track(cancellationToken); return Task.FromResult(Result(rule)); }
        public override Task<Response> DeleteRuleAsync(string topicName, string subscriptionName, string ruleName, CancellationToken cancellationToken = default)
        {
            Track(cancellationToken);
            Assert.That(Rules[subscriptionName].Any(x => x.Name != ruleName && x.Filter is SqlRuleFilter), Is.True);
            Rules[subscriptionName].RemoveAll(x => x.Name == ruleName);
            if (SimulateDeleteRace) throw new RequestFailedException(404, "Concurrent deletion");
            return Task.FromResult(Mock.Of<Response>());
        }
        public override Task<Response<SubscriptionProperties>> GetSubscriptionAsync(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        { Track(cancellationToken); return Task.FromResult(Result(Subscriptions[subscriptionName])); }
        public override Task<Response<SubscriptionProperties>> UpdateSubscriptionAsync(SubscriptionProperties subscription, CancellationToken cancellationToken = default)
        { Track(cancellationToken); return Task.FromResult(Result(subscription)); }
    }
}
