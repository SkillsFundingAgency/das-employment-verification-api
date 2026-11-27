namespace SFA.DAS.EmploymentVerificationService.Models
{
    public enum  EmploymentCheckStatus: byte
    {
        InProgress = 1,
        Employed,
        NotEmployed,
        Failed
    }
}
