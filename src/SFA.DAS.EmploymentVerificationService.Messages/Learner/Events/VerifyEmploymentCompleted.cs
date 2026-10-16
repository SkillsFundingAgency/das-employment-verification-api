namespace SFA.DAS.EmploymentVerificationService.Messages.Learner.Events;

public class VerifyEmploymentCompleted : EmploymentVerificationEventBase
{
    public bool Employed { get; set; }
}
