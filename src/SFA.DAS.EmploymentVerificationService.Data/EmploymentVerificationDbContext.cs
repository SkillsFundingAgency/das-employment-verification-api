using Microsoft.EntityFrameworkCore;
using SFA.DAS.EmploymentVerificationService.Data.Entities;
using SFA.DAS.EmploymentVerificationService.Models;

namespace SFA.DAS.EmploymentVerificationService.Data;

public class EmploymentVerificationDbContext(DbContextOptions<EmploymentVerificationDbContext> options)
    : DbContext(options)
{
    public DbSet<EmploymentVerificationRequest> EmploymentVerificationRequests => Set<EmploymentVerificationRequest>();
    public DbSet<EmploymentVerificationResponse> EmploymentVerificationResponses => Set<EmploymentVerificationResponse>();

    public DbSet<LearnerEmploymentVerification> LearnerEmploymentVerifications => Set<LearnerEmploymentVerification>();
    public DbSet<LearnerEmploymentCheck> LearnerEmploymentChecks => Set<LearnerEmploymentCheck>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EmploymentVerificationDbContext).Assembly);
    }
}
