using Microsoft.EntityFrameworkCore;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Persistence;
using SFA.DAS.EmploymentVerificationService.Data.Entities;
using SFA.DAS.EmploymentVerificationService.Models;

namespace SFA.DAS.EmploymentVerificationService.Data;

public class EmploymentCheckRepository(EmploymentVerificationDbContext context) : IEmploymentCheckRepository
{
    public Task<bool> VerificationExists(Guid externalCorrelationId, CancellationToken cancellationToken)
    {
        return context.LearnerEmploymentVerifications
            .AsNoTracking()
            .AnyAsync(x => x.Detail.ExternalCorrelationId == externalCorrelationId, cancellationToken);
    }

    public async Task SaveVerificationAsync(LearnerEmploymentVerification verification, CancellationToken cancellationToken)
    {
        context.LearnerEmploymentVerifications.Add(verification);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveRequestAsync(EmploymentCheckRequest request, CancellationToken cancellationToken)
    {
        var entity = new EmploymentVerificationRequest
        {
            CorrelationId = request.CorrelationId,
            ULN = request.ULN,
            AccountId = request.EmployerAccountIdentifier,
            ApprenticeshipId = request.ApprenticeshipId,
            Reference = request.Reference,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NumberOfAttempts = request.NumberOfAttempts,
            NumberOfFailures = 0,
            Status = request.Status,
            CreatedOn = request.CreatedOn
        };

        context.EmploymentVerificationRequests.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<LearnerEmploymentVerification?> GetVerificationForCheckAsync(Guid checkCorrelationId, CancellationToken cancellationToken)
    {
        return context.LearnerEmploymentVerifications
            .AsTracking()
            .Include(verification => verification.Detail)
            .Include(verification => verification.LearnerEmploymentChecks
                .Where(check => check.Detail.CheckCorrelationId == checkCorrelationId))
                .ThenInclude(check => check.Detail)
            .SingleOrDefaultAsync(verification => verification.LearnerEmploymentChecks
                .Any(check => check.Detail.CheckCorrelationId == checkCorrelationId), cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
