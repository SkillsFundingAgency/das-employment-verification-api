namespace SFA.DAS.EmploymentVerificationService.Models
{
    public enum  EmploymentCheckStats: byte
    {
        InProgress = 1,
        Completed = 2,
        Unknown = 3 //TODO: Find the actual value for this, as it is not in the spec
    }
}
