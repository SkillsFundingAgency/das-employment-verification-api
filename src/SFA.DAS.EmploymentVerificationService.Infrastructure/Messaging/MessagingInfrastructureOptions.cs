using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;

public sealed record MessagingRoute(string RuleName, string TopicName, string SubscriptionName, string QueueName, string SqlFilter);

public sealed record MessagingInfrastructureOptions(bool Enabled, IReadOnlyList<MessagingRoute> Routes)
{
    // Deliberately accept only a single property comparison. Arbitrary SQL can contain
    // tautologies (including OR 1=1), which must never create a catch-all subscription.
    private static readonly Regex PropertyComparison = new(
        @"^\s*(?:sys\.Label|(?:user\.)?(?:\[[^\[\]\r\n]+\]|[A-Za-z_][A-Za-z0-9_]*))\s*(=|LIKE)\s*'((?:[^']|'')+)'\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static MessagingInfrastructureOptions Read(IConfiguration configuration)
    {
        const string section = "MessagingInfrastructure";
        var enabled = ReadFlag(configuration, $"{section}:Enabled");
        if (!enabled)
            return new(false, Array.Empty<MessagingRoute>());

        if (!ReadFlag(configuration, $"{section}:FiltersReviewed"))
            throw new InvalidOperationException("MessagingInfrastructure:FiltersReviewed must be true before enabling provisioning for both routes.");

        var routes = new[] { "VerifyNotEmployed", "EmploymentCheckCompleted" }.Select(name =>
        {
            var prefix = $"{section}:{name}";
            var filter = Required(configuration, $"{prefix}:SqlFilter");
            var match = PropertyComparison.Match(filter);
            if (!match.Success || (match.Groups[1].Value.Equals("LIKE", StringComparison.OrdinalIgnoreCase)
                && !match.Groups[2].Value.Any(char.IsLetterOrDigit)))
                throw new InvalidOperationException($"{prefix}:SqlFilter must be a restrictive property = 'value' or property LIKE 'pattern' expression with a literal value. Compound SQL is not supported.");

            return new MessagingRoute(
                name == "EmploymentCheckCompleted" ? "EmploymentCheckCompletedEvent" : name,
                Required(configuration, $"{prefix}:TopicName"),
                Required(configuration, $"{prefix}:SubscriptionName"),
                Required(configuration, $"{name}QueueName"), filter);
        }).ToArray();

        if (routes[0].QueueName.Equals(routes[1].QueueName, StringComparison.OrdinalIgnoreCase)
            || (routes[0].TopicName.Equals(routes[1].TopicName, StringComparison.OrdinalIgnoreCase)
                && routes[0].SubscriptionName.Equals(routes[1].SubscriptionName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("The two incoming routes must use distinct queues and distinct topic/subscription pairs.");

        return new(true, routes);
    }

    private static bool ReadFlag(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (value is null) return false;
        if (bool.TryParse(value, out var result)) return result;
        throw new InvalidOperationException($"{key} must be true or false.");
    }

    private static string Required(IConfiguration configuration, string key) =>
        string.IsNullOrWhiteSpace(configuration[key])
            ? throw new InvalidOperationException($"{key} is required for messaging infrastructure provisioning.")
            : configuration[key]!.Trim();
}
