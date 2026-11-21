using System;
using System.Collections.Generic;
using System.Text;

namespace SFA.DAS.EmploymentVerificationService.Models
{
    public class EmploymentVerification
    {
        public long Id { get; set; }
        public EmploymentVerificationType Type { get; set; }
        public Guid ExternalCorrelationId { get; set; }
        public DateOnly StartDate { get; set; }
        public int DurationInDays { get; set; }
        public string Reference { get; set; }
        public EmploymentVerificationStatus Status { get; set; }
        public string FailureReason { get; set; }
        public byte RetryCount { get; set; }
        public DateTime? NextRetryTime { get; set; }
    }
}
