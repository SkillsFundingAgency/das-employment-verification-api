using System.Net.Http.Headers;
using System.Net.Http.Json;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Services;

namespace SFA.DAS.EmploymentVerificationService.Infrastructure.Clients;

public sealed class EmploymentCheckClient(
    HttpClient httpClient,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<EmploymentCheckClient> logger,
    TokenCredential credential) : IEmploymentCheckClient
{
    public async Task SendAsync(EmploymentCheckRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var body = new RegisterCheckRequest
        {
            CorrelationId = request.CorrelationId,
            Uln = request.ULN,
            ApprenticeshipAccountId = checked((int)request.EmployerAccountIdentifier),
            ApprenticeshipId = request.ApprenticeshipId,
            MinDate = request.StartDate,
            MaxDate = request.EndDate
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, configuration["EmploymentCheckApiUrl"])
        {
            Content = JsonContent.Create(body)
        };
        if (!environment.IsDevelopment())
        {
            var token = await credential.GetTokenAsync(
                new TokenRequestContext([configuration["EmploymentCheckApiScope"]!]), cancellationToken);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }

        logger.LogInformation("Registering Employment Check for {CorrelationId}", request.CorrelationId);
        using var response = await httpClient.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Do not log response content, which could include learner data. The trigger logs the exception.
            logger.LogWarning("Employment Check registration returned HTTP {StatusCode} for {CorrelationId}",
                (int)response.StatusCode, request.CorrelationId);
            response.EnsureSuccessStatusCode();
        }

        var acknowledgement = await response.Content.ReadFromJsonAsync<RegisterCheckResponse>(cancellationToken);
        if (acknowledgement is null)
            throw new HttpRequestException("Employment Check API returned a null registration acknowledgement.");

        if (!string.IsNullOrEmpty(acknowledgement.ErrorType) || !string.IsNullOrEmpty(acknowledgement.ErrorMessage))
        {
            logger.LogWarning("Employment Check API rejected registration for {CorrelationId}", request.CorrelationId);
            throw new HttpRequestException("Employment Check API returned an error in the registration acknowledgement.");
        }

        logger.LogInformation("Employment Check registration accepted for {CorrelationId}", request.CorrelationId);
        // Transport failures, timeouts, malformed responses and cancellation propagate to the caller.
    }

    private class RegisterCheckRequest
    {
        public Guid CorrelationId { get; set; }

        public string CheckType { get; set; } = "VerifyNotEmployed";

        public long Uln { get; set; }

        public int ApprenticeshipAccountId { get; set; }

        public long? ApprenticeshipId { get; set; }

        public DateTime MinDate { get; set; }

        public DateTime MaxDate { get; set; }
    }

    private sealed record RegisterCheckResponse(string? ErrorType, string? ErrorMessage);
}
