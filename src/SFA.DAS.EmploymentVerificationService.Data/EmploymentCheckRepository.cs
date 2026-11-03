using Microsoft.EntityFrameworkCore;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Persistence;
using RequestEntity = SFA.DAS.EmploymentVerificationService.Data.Entities.EmploymentCheckRequest;
using ResponseEntity = SFA.DAS.EmploymentVerificationService.Data.Entities.EmploymentCheckResponse;

namespace SFA.DAS.EmploymentVerificationService.Data;

public class EmploymentCheckRepository(EmploymentVerificationDbContext context) : IEmploymentCheckRepository
{
    public Task<bool> ExistsAsync(Guid correlationId, CancellationToken cancellationToken)
    {
        return context.EmploymentCheckRequests
            .AsNoTracking()
            .AnyAsync(x => x.CorrelationId == correlationId, cancellationToken);
    }

    public async Task SaveRequestAsync(EmploymentCheckRequest request, CancellationToken cancellationToken)
    {
        var entity = new RequestEntity
        {
            CorrelationId = request.CorrelationId,
            ULN = request.ULN,
            AccountId = request.AccountId,
            ApprenticeshipId = request.ApprenticeshipId,
            Reference = request.Reference,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NumberOfAttempts = request.NumberOfAttempts,
            Status = request.Status,
            CreatedOn = request.CreatedOn
        };

        context.EmploymentCheckRequests.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<EmploymentCheckRequest?> GetRequestAsync(Guid correlationId, CancellationToken cancellationToken)
    {
        return context.EmploymentCheckRequests
            .AsNoTracking()
            .Where(x => x.CorrelationId == correlationId)
            .Select(x => new EmploymentCheckRequest(
                x.CorrelationId,
                x.ULN,
                x.AccountId,
                x.ApprenticeshipId,
                x.Reference,
                x.StartDate,
                x.EndDate,
                x.NumberOfAttempts,
                x.Status,
                x.CreatedOn))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task RecordCompletionAsync(EmploymentCheckCompletion completion, CancellationToken cancellationToken)
    {
        var request = await context.EmploymentCheckRequests
            .SingleOrDefaultAsync(x => x.CorrelationId == completion.CorrelationId, cancellationToken)
            ?? throw new InvalidOperationException($"Employment check request '{completion.CorrelationId}' was not found.");

        var response = new ResponseEntity
        {
            CorrelationId = completion.CorrelationId,
            Employed = completion.Employed,
            CreatedOn = completion.CreatedOn
        };

        request.Status = completion.UpdatedStatus;
        context.EmploymentCheckResponses.Add(response);

        await context.SaveChangesAsync(cancellationToken);
    }
}
