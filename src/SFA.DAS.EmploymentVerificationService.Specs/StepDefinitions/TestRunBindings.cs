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
                await database.EmploymentVerificationResponses.Where(response => response.CorrelationId == correlationId).ExecuteDeleteAsync();
                await database.EmploymentVerificationRequests.Where(request => request.CorrelationId == correlationId).ExecuteDeleteAsync();
                await transaction.CommitAsync();
            }
        }
        finally
        {
            scenario.Stop();
        }
    }
}
