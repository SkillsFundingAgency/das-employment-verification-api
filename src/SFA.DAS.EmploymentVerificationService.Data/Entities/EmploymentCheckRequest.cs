namespace SFA.DAS.EmploymentVerificationService.Data.Entities;

public class EmploymentCheckRequest
{
    public long Id { get; set; }
    public Guid CorrelationId { get; set; }
    public long ULN { get; set; }
    public long AccountId { get; set; }
    public long? ApprenticeshipId { get; set; }
    public string? Reference { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int NumberOfAttempts { get; set; }
    public byte Status { get; set; }
    public DateTime CreatedOn { get; set; }
}
