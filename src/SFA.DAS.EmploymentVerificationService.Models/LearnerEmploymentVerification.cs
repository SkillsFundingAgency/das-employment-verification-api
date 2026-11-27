namespace SFA.DAS.EmploymentVerificationService.Models
{
    public class LearnerEmploymentVerification 
    {
        public long EmploymentVerificationId { get; set; }
        public EmploymentVerification Detail { get; set; } = null!;
        public long UniqueLearnerNumber { get; set; }
        public long ApprenticeshipId { get; set; }
        public long EmployerAccountId { get; set; }
        public DateTime CreationTime { get; set; }
        public List<LearnerEmploymentCheck> LearnerEmploymentChecks { get; set; } = new List<LearnerEmploymentCheck>();
    }
}
