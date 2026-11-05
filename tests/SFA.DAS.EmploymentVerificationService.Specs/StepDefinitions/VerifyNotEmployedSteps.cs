using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Reqnroll;
using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Events;

namespace SFA.DAS.EmploymentVerificationService.Specs.StepDefinitions;

[Binding]
public sealed class VerifyNotEmployedSteps(TestSession scenario)
{
    private bool requestWasNew;
    private EmploymentCheckCompletedEvent? completion;
    private Task? processing;

    [Given("a VerifyNotEmployed check has been published by a consuming service")]
    public async Task PublishedRequest()
    {
        requestWasNew = !await scenario.DataContext.EmploymentCheckRequests.AnyAsync(request => request.CorrelationId == scenario.Command.CorrelationId);
        Assert.That(requestWasNew, Is.True);
    }

    [When("EVS receives the request")]
    public async Task ReceiveRequest()
    {
        processing = ProcessCommand();
        await processing;
    }

    [When("the same request has not been received previously")]
    public async Task RequestWasNotReceivedPreviously()
    {
        // This condition is captured before the preceding When invokes the service.
        Assert.That(requestWasNew, Is.True);
        Assert.That(await scenario.DataContext.EmploymentCheckRequests.CountAsync(request => request.CorrelationId == scenario.Command.CorrelationId), Is.EqualTo(1));
    }

    [Given("the employment check result has been received from the Employment Check service")]
    public async Task ReceiveEmploymentCheckResult()
    {
        await ProcessCommand();
        await InitialAttemptCount();
        completion = new EmploymentCheckCompletedEvent(scenario.Command.CorrelationId, false, DateTime.UtcNow, "");
    }

    [Given(@"^the employment check request has completed successfully \(no errors or exceptions\)$")]
    public void SuccessfulEmploymentCheck()
    {
        Assert.That(completion, Is.Not.Null);
        Assert.That(completion!.ErrorType, Is.Null.Or.Empty);
        Assert.That(completion.EmploymentResult.HasValue, Is.True);
    }

    [Given(@"^the result returns an employed = 'false' result \(success response\)$")]
    public void LearnerIsNotEmployed() => SetEmploymentResult(false);

    [Given(@"^the result returns employed = 'true' result \(failure response\)$")]
    public void LearnerIsEmployed() => SetEmploymentResult(true);

    private void SetEmploymentResult(bool employed)
    {
        Assert.That(completion, Is.Not.Null);
        completion = new EmploymentCheckCompletedEvent(completion!.CorrelationId, employed, completion.CheckDate, completion.ErrorType);
    }

    private Task ProcessCommand() =>
        scenario.VerifyNotEmployedService.ProcessAsync(scenario.Command, CancellationToken.None);

    [When("the result is processed")]
    public async Task ProcessResult()
    {
        Assert.That(completion, Is.Not.Null);
        processing = scenario.EmploymentCheckCompletionService
            .ProcessAsync(new EmploymentCheckResult(completion!.CorrelationId, completion.EmploymentResult,
                completion.CheckDate, completion.ErrorType), CancellationToken.None);
        await processing;
    }

    [When("the result is matched to the verify request")]
    public async Task ResultMatchesRequest()
    {
        Assert.That(completion, Is.Not.Null);
        var request = await scenario.DataContext.EmploymentCheckRequests.AsNoTracking()
            .SingleAsync(request => request.CorrelationId == scenario.Command.CorrelationId);
        Assert.That(completion!.CorrelationId, Is.EqualTo(request.CorrelationId));
    }

    [Then("the request details are stored in the EVS EmploymentVerificationRequest table with Request Status 'In Progress' and NumberOfAttempts '1'")]
    public async Task RequestIsInProgress()
    {
        await AssertRequestStatus(EmploymentCheckStatus.InProgress);
        await InitialAttemptCount();
    }

    [Then("status value stored in the EVS EmploymentVerificationRequest table is updated to 'Completed'")]
    public Task RequestIsCompleted() => AssertRequestStatus(EmploymentCheckStatus.Completed);

    private async Task AssertRequestStatus(EmploymentCheckStatus status)
    {
        var request = await scenario.DataContext.EmploymentCheckRequests.AsNoTracking().SingleAsync(request => request.CorrelationId == scenario.Command.CorrelationId);
        Assert.Multiple(() =>
        {
            Assert.That(request.CorrelationId, Is.EqualTo(scenario.Command.CorrelationId));
            Assert.That(request.ULN, Is.EqualTo(scenario.Command.ULN));
            Assert.That(request.AccountId, Is.EqualTo(scenario.Command.EmployerAccountIdentifier));
            Assert.That(request.ApprenticeshipId, Is.EqualTo(scenario.Command.ApprenticeshipId));
            Assert.That(request.Reference, Is.EqualTo(scenario.Command.Reference));
            Assert.That(request.StartDate, Is.EqualTo(scenario.Command.StartDate));
            Assert.That(request.EndDate, Is.EqualTo(scenario.Command.StartDate.AddDays(30)));
            Assert.That(request.Status, Is.EqualTo((byte)status));
        });
    }

    [When("this is the first attempt for this verify request")]
    public async Task InitialAttemptCount()
    {
        var request = await scenario.DataContext.EmploymentCheckRequests.AsNoTracking().SingleAsync(request => request.CorrelationId == scenario.Command.CorrelationId);
        Assert.That(request.NumberOfAttempts, Is.EqualTo(1));
    }

    [Then("EVS constructs the outgoing RegisterCheckRequest to the Employment Check service")]
    public void RegisterCheckRequestWasConstructed()
    {
        var request = scenario.Api.LogEntries.Single().RequestMessage!;
        Assert.That(request.Path, Is.EqualTo("/api/EmploymentCheck/RegisterCheck"));
        Assert.That(request.Method, Is.EqualTo("POST"));
        using var json = JsonDocument.Parse(request.Body!);
        var body = json.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(body.EnumerateObject().Select(property => property.Name), Is.EquivalentTo(new[]
                { "correlationId", "checkType", "uln", "apprenticeshipAccountId", "apprenticeshipId", "minDate", "maxDate" }));
            Assert.That(body.GetProperty("correlationId").GetGuid(), Is.EqualTo(scenario.Command.CorrelationId));
            Assert.That(body.GetProperty("checkType").GetString(), Is.EqualTo("VerifyNotEmployed"));
            Assert.That(body.GetProperty("uln").GetInt64(), Is.EqualTo(scenario.Command.ULN));
            Assert.That(body.GetProperty("apprenticeshipAccountId").GetInt32(), Is.EqualTo(scenario.Command.EmployerAccountIdentifier));
            Assert.That(body.GetProperty("apprenticeshipId").GetInt64(), Is.EqualTo(scenario.Command.ApprenticeshipId));
            Assert.That(body.GetProperty("minDate").GetDateTime(), Is.EqualTo(scenario.Command.StartDate));
            Assert.That(body.GetProperty("maxDate").GetDateTime(), Is.EqualTo(scenario.Command.StartDate.AddDays(30)));
        });
    }

    [Then("the request is submitted")]
    public void RequestWasSubmitted()
    {
        Assert.That(processing, Is.Not.Null);
        Assert.That(processing!.IsCompletedSuccessfully, Is.True);
        Assert.That(scenario.Api.LogEntries.Single().ResponseMessage!.StatusCode, Is.EqualTo(200));
    }

    [Then("^the result is stored in the EmploymentVerificationResponse table with Employed=(True|False)$")]
    public async Task ResponseWasSaved(bool employed)
    {
        var response = await scenario.DataContext.EmploymentCheckResponses.AsNoTracking().SingleAsync(response => response.CorrelationId == scenario.Command.CorrelationId);
        Assert.Multiple(() =>
        {
            Assert.That(response.CorrelationId, Is.EqualTo(scenario.Command.CorrelationId));
            Assert.That(response.Employed, Is.EqualTo(employed));
        });
    }

    [Then("^EVS publishes a VerifyNotEmployedCompleted message with NotEmployed=(True|False)$")]
    public void CompletionWasPublished(bool notEmployed)
    {
        var sent = scenario.PublishedMessages.Single();
        var message = JsonSerializer.Deserialize<VerifyNotEmployedCompleted>(sent.Body.ToString())!;
        Assert.Multiple(() =>
        {
            Assert.That(sent.Subject, Is.EqualTo(nameof(VerifyNotEmployedCompleted)));
            Assert.That(sent.CorrelationId, Is.EqualTo(scenario.Command.CorrelationId.ToString()));
            Assert.That(message.CorrelationId, Is.EqualTo(scenario.Command.CorrelationId));
            Assert.That(message.ULN, Is.EqualTo(scenario.Command.ULN));
            Assert.That(message.EmployerAccountIdentifier, Is.EqualTo(scenario.Command.EmployerAccountIdentifier));
            Assert.That(message.ApprenticeshipId, Is.EqualTo(scenario.Command.ApprenticeshipId));
            Assert.That(message.NotEmployed, Is.EqualTo(notEmployed));
        });
    }

    [Then("processing ends")]
    public void ProcessingHasEnded()
    {
        Assert.That(processing, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(processing!.IsCompletedSuccessfully, Is.True);
            Assert.That(scenario.PublishedMessages, Has.Count.EqualTo(1));
            Assert.That(scenario.Api.LogEntries.Count(), Is.EqualTo(1));
        });
    }
}
