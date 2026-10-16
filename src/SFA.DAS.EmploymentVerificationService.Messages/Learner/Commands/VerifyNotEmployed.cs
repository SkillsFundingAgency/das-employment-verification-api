namespace SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

public class VerifyNotEmployed
{
    public Guid CorrelationId { get; set; }
    public long ULN { get; set; }
    public long EmployerAccountIdentifier { get; set; }
    public DateTime StartDate { get; set; }
    public int? DurationInDays { get; set; }
    public string? Reference { get; set; }
}
