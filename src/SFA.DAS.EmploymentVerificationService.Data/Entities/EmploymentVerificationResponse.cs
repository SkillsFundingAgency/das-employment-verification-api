namespace SFA.DAS.EmploymentVerificationService.Data.Entities;

public class EmploymentVerificationResponse
{
    public long Id { get; set; }
    public Guid CorrelationId { get; set; }
    public bool Employed { get; set; }
    public DateTime CreatedOn { get; set; }
}
