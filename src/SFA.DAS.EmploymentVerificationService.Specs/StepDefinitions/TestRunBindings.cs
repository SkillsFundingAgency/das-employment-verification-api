using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Reqnroll;

namespace SFA.DAS.EmploymentVerificationService.Specs.StepDefinitions;

[Binding]
public sealed class TestRunBindings(TestSession scenario)
{
    public static IConfiguration Config { get; private set; } = null!;

    [BeforeTestRun]
    public static void LoadConfiguration()
    {
        Config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appSettings.json")
            .AddJsonFile("appSettings.Development.json", optional: true)
            .Build();
    }

    [BeforeScenario]
    public void SetUp() => scenario.Start(Config);

    [AfterScenario]
    public async Task TearDown()
    {
        try
        {
            if (scenario.DataContext is not null)
            {
                var database = scenario.DataContext;
                var correlationId = scenario.Command.CorrelationId;
                await using var transaction = await database.Database.BeginTransactionAsync();
                var verifications = database.LearnerEmploymentVerifications
                    .Where(verification => verification.Detail.ExternalCorrelationId == correlationId);
                var links = verifications.SelectMany(verification => verification.LearnerEmploymentChecks);
                var checkIds = await links.Select(link => link.EmploymentCheckId).ToListAsync();
                await database.LearnerEmploymentChecks.Where(link => checkIds.Contains(link.EmploymentCheckId)).ExecuteDeleteAsync();
                await database.Set<SFA.DAS.EmploymentVerificationService.Models.EmploymentCheck>()
                    .Where(check => checkIds.Contains(check.Id)).ExecuteDeleteAsync();
                await verifications.ExecuteDeleteAsync();
                await database.Set<SFA.DAS.EmploymentVerificationService.Models.EmploymentVerification>()
                    .Where(verification => verification.ExternalCorrelationId == correlationId).ExecuteDeleteAsync();
                await transaction.CommitAsync();
            }
        }
        finally
        {
            scenario.Stop();
        }
    }
}
