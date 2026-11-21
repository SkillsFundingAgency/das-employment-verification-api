namespace SFA.DAS.EmploymentVerificationService.Models
{
    public class EmploymentCheck
    {
        public long Id { get; set; }
        public Guid CheckCorrelationId { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? Reference { get; set; }
        public DateTime RequestTime { get; set; }
        public DateTime ResponseTime { get; set; }
        public string? FailureReason { get; set; }
        public EmploymentCheckStatus Status { get; set; }
    }
}
