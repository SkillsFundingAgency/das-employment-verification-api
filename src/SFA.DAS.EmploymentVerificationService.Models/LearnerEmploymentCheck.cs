namespace SFA.DAS.EmploymentVerificationService.Models
{
    public class LearnerEmploymentCheck 
    {
        public long Id { get; set; }
        public long LearnerEmploymentVerificationId { get; set; }
        public long EmploymentCheckId { get; set; }
        public EmploymentCheck Detail { get; set; } = null!;
        public long UniqueLearnerNumber { get; set; }
        public long EmployerAccountId { get; set; }
        public DateTime CreationTime { get; set; }

    }
}
