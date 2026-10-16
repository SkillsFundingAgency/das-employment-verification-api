namespace SFA.DAS.EmploymentVerificationService.Messages.Learner.Events;

public class VerifyNotEmployedCompleted : EmploymentVerificationEventBase
{
    public bool NotEmployed { get; set; }
}
