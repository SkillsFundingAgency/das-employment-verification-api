namespace SFA.DAS.EmploymentVerificationService.Messages.Learner.Events;

public class VerifyNotEmployedFailed
{
    public Guid CorrelationId { get; set; }

    public long ULN { get; set; }

    public long EmployerAccountIdentifier { get; set; }

    public int FailureId { get; set; }
    public string FailureReason { get; set; } = string.Empty;
}
