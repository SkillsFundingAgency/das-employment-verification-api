namespace SFA.DAS.EmploymentVerificationService.Messages.Learner.Events;

public class VerifyNotEmployedFailed : EmploymentVerificationEventBase
{
    public int FailureId { get; set; }
    public string FailureReason { get; set; } = string.Empty;
}
