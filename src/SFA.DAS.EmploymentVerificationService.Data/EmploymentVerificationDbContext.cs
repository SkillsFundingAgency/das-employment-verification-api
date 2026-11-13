using Microsoft.EntityFrameworkCore;
using SFA.DAS.EmploymentVerificationService.Data.Entities;

namespace SFA.DAS.EmploymentVerificationService.Data;

public class EmploymentVerificationDbContext(DbContextOptions<EmploymentVerificationDbContext> options)
    : DbContext(options)
{
    public DbSet<EmploymentVerificationRequest> EmploymentVerificationRequests => Set<EmploymentVerificationRequest>();
    public DbSet<EmploymentVerificationResponse> EmploymentVerificationResponses => Set<EmploymentVerificationResponse>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EmploymentVerificationDbContext).Assembly);
    }
}
