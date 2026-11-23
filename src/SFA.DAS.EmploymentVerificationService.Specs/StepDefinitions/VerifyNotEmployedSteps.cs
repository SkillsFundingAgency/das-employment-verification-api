using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Reqnroll;
using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Events;
using SFA.DAS.EmploymentVerificationService.Models;
using EmploymentCheckStatus = SFA.DAS.EmploymentVerificationService.Models.EmploymentCheckStatus;

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
        requestWasNew = !await scenario.DataContext.LearnerEmploymentVerifications
            .AnyAsync(verification => verification.Detail.ExternalCorrelationId == scenario.Command.CorrelationId);
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
        Assert.That(await scenario.DataContext.LearnerEmploymentVerifications
            .CountAsync(verification => verification.Detail.ExternalCorrelationId == scenario.Command.CorrelationId), Is.EqualTo(1));
    }

    [Given("the employment check result has been received from the Employment Check service")]
    public async Task ReceiveEmploymentCheckResult()
    {
        await ProcessCommand();
        using var request = ReadSubmittedRequest();
        var checkCorrelationId = request.RootElement.GetProperty("correlationId").GetGuid();
        completion = new EmploymentCheckCompletedEvent(checkCorrelationId, false, DateTime.UtcNow, "");
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
        var verification = await LoadVerificationAsync();
        Assert.That(completion!.CorrelationId, Is.EqualTo(verification.LearnerEmploymentChecks.Single().Detail.CheckCorrelationId));
        Assert.That(completion.CorrelationId, Is.Not.EqualTo(verification.Detail.ExternalCorrelationId));
    }

    [Then("the verification and its first check are stored as InProgress")]
    public async Task RequestIsInProgress()
    {
        var verification = await LoadVerificationAsync();
        var check = verification.LearnerEmploymentChecks.Single().Detail;
        Assert.Multiple(() =>
        {
            Assert.That(verification.UniqueLearnerNumber, Is.EqualTo(scenario.Command.ULN));
            Assert.That(verification.EmployerAccountId, Is.EqualTo(scenario.Command.EmployerAccountIdentifier));
            Assert.That(verification.ApprenticeshipId, Is.EqualTo(scenario.Command.ApprenticeshipId));
            Assert.That(verification.Detail.Reference, Is.EqualTo(scenario.Command.Reference));
            Assert.That(verification.Detail.StartDate, Is.EqualTo(DateOnly.FromDateTime(scenario.Command.StartDate)));
            Assert.That(verification.Detail.DurationInDays, Is.EqualTo(scenario.Command.DurationInDays));
            Assert.That(verification.Detail.RetryCount, Is.Zero);
            Assert.That(verification.Detail.Status, Is.EqualTo(EmploymentVerificationStatus.InProgress));
            Assert.That(check.StartDate, Is.EqualTo(verification.Detail.StartDate));
            Assert.That(check.EndDate, Is.EqualTo(verification.Detail.StartDate.AddDays(verification.Detail.DurationInDays)));
            Assert.That(check.Status, Is.EqualTo((byte)EmploymentCheckStatus.InProgress));
        });
    }

    private Task<LearnerEmploymentVerification> LoadVerificationAsync() =>
        scenario.DataContext.LearnerEmploymentVerifications.AsNoTracking()
            .Include(verification => verification.Detail)
            .Include(verification => verification.LearnerEmploymentChecks).ThenInclude(check => check.Detail)
            .SingleAsync(verification => verification.Detail.ExternalCorrelationId == scenario.Command.CorrelationId);

    [Then("EVS constructs the outgoing RegisterCheckRequest to the Employment Check service")]
    public async Task RegisterCheckRequestWasConstructed()
    {
        var request = scenario.Api.LogEntries.Single().RequestMessage!;
        Assert.That(request.Path, Is.EqualTo("/api/EmploymentCheck/RegisterCheck"));
        Assert.That(request.Method, Is.EqualTo("POST"));
        using var json = ReadSubmittedRequest();
        var body = json.RootElement;
        var verification = await LoadVerificationAsync();
        var check = verification.LearnerEmploymentChecks.Single().Detail;
        Assert.Multiple(() =>
        {
            Assert.That(body.EnumerateObject().Select(property => property.Name), Is.EquivalentTo(new[]
                { "correlationId", "checkType", "uln", "apprenticeshipAccountId", "apprenticeshipId", "minDate", "maxDate" }));
            Assert.That(body.GetProperty("correlationId").GetGuid(), Is.EqualTo(check.CheckCorrelationId)
                .And.Not.EqualTo(scenario.Command.CorrelationId));
            Assert.That(body.GetProperty("checkType").GetString(), Is.EqualTo("VerifyNotEmployed"));
            Assert.That(body.GetProperty("uln").GetInt64(), Is.EqualTo(scenario.Command.ULN));
            Assert.That(body.GetProperty("apprenticeshipAccountId").GetInt32(), Is.EqualTo(scenario.Command.EmployerAccountIdentifier));
            Assert.That(body.GetProperty("apprenticeshipId").GetInt64(), Is.EqualTo(scenario.Command.ApprenticeshipId));
            Assert.That(body.GetProperty("minDate").GetDateTime(), Is.EqualTo(scenario.Command.StartDate));
            Assert.That(body.GetProperty("maxDate").GetDateTime(), Is.EqualTo(scenario.Command.StartDate.AddDays(30)));
        });
    }

    private JsonDocument ReadSubmittedRequest() =>
        JsonDocument.Parse(scenario.Api.LogEntries.Single().RequestMessage!.Body!);

    [Then("the request is submitted")]
    public void RequestWasSubmitted()
    {
        Assert.That(processing, Is.Not.Null);
        Assert.That(processing!.IsCompletedSuccessfully, Is.True);
        Assert.That(scenario.Api.LogEntries.Single().ResponseMessage!.StatusCode, Is.EqualTo(200));
    }

    [Then("^the check is completed and the verification outcome is (Employed|NotEmployed)$")]
    public async Task ResponseWasSaved(EmploymentVerificationStatus status)
    {
        var verification = await LoadVerificationAsync();
        var check = verification.LearnerEmploymentChecks.Single().Detail;
        Assert.Multiple(() =>
        {
            Assert.That(check.Status, Is.EqualTo((byte)EmploymentCheckStatus.Completed));
            Assert.That(check.ResponseTime, Is.Not.Null);
            Assert.That(verification.Detail.Status, Is.EqualTo(status));
        });
    }

    [Then("^EVS publishes a VerifyNotEmployedCompleted message with NotEmployed=(True|False)$")]
    public void CompletionWasPublished(bool notEmployed)
    {
        var sent = scenario.PublishedMessages.Single();
        var message = JsonSerializer.Deserialize<VerifyNotEmployedCompleted>(sent.Body.ToString())!;
        Assert.Multiple(() =>
        {
            Assert.That(sent.Subject, Is.Null);
            Assert.That(sent.CorrelationId, Is.Null);
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
