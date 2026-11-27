using Microsoft.EntityFrameworkCore;
using SFA.DAS.EmploymentVerificationService.Models;

namespace SFA.DAS.EmploymentVerificationService.Data;

public class EmploymentVerificationDbContext(DbContextOptions<EmploymentVerificationDbContext> options)
    : DbContext(options)
{
    public DbSet<LearnerEmploymentVerification> LearnerEmploymentVerifications => Set<LearnerEmploymentVerification>();
    public DbSet<LearnerEmploymentCheck> LearnerEmploymentChecks => Set<LearnerEmploymentCheck>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EmploymentVerificationDbContext).Assembly);
    }
}
