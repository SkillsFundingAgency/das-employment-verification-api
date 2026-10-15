namespace SFA.DAS.EmploymentVerificationService.Messages.Learner.Events;

public class VerifyEmploymentCompleted
{
    public Guid CorrelationId { get; set; }

    public long ULN { get; set; }

    public long EmployerAccountIdentifier { get; set; }

    public bool Employed { get; set; }
}
