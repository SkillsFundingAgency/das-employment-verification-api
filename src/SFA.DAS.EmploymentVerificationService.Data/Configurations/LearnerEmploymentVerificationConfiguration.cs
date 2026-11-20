using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFA.DAS.EmploymentVerificationService.Models;

namespace SFA.DAS.EmploymentVerificationService.Data.Configurations;

public class LearnerEmploymentVerificationConfiguration : IEntityTypeConfiguration<LearnerEmploymentVerification>
{
    public void Configure(EntityTypeBuilder<LearnerEmploymentVerification> builder)
    {
        builder.ToTable("LearnerEmploymentVerification", "EVS");
        builder.HasKey(x => x.EmploymentVerificationId);
        builder.Property(x => x.EmploymentVerificationId).HasColumnType("bigint").ValueGeneratedNever().IsRequired();
        builder.Property(x => x.UniqueLearnerNumber).HasColumnName("ULN").HasColumnType("bigint").IsRequired();
        builder.Property(x => x.ApprenticeshipId).HasColumnType("bigint").IsRequired();
        builder.Property(x => x.EmployerAccountId).HasColumnName("EmployeraccountId").HasColumnType("bigint").IsRequired();
        builder.Property(x => x.CreationTime).HasColumnType("datetime2").HasPrecision(7).HasDefaultValueSql("GETDATE()").IsRequired();
        builder.HasOne(x => x.Detail)
            .WithOne()
            .HasForeignKey<LearnerEmploymentVerification>(x => x.EmploymentVerificationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasMany(x => x.LearnerEmploymentChecks)
            .WithOne()
            .HasForeignKey(x => x.LearnerEmploymentVerificationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.NoAction);
    }

}
