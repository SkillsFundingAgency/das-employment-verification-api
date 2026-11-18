namespace SFA.DAS.EmploymentVerificationService.Models
{
    public class LearnerEmploymentVerification 
    {
        public long Id { get; set; }
        public long EmploymentVerificationId { get; set; }
        public EmploymentVerification Detail { get; set; }
        public long UniqueLearnerNumber { get; set; }
        public long EmployerAccountId { get; set; }
        public List<LearnerEmploymentCheck> LearnerEmploymentChecks { get; set; } = new List<LearnerEmploymentCheck>();
    }
}
