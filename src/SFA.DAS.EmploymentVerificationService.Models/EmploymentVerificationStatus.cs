namespace SFA.DAS.EmploymentVerificationService.Models
{
    public enum EmploymentVerificationStatus: byte
    {
        Pending = 1,
        InProgress,
        AwaitingConfirmation,
        ConfirmationInProgress,
        AwaitingRetry,
        RetryInProgress,
        Employed,
        NotEmployed,
        Failed,
    }
}
