using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFA.DAS.EmploymentVerificationService.Models;

namespace SFA.DAS.EmploymentVerificationService.Data.Configurations;

public class LearnerEmploymentVerificationConfiguration : IEntityTypeConfiguration<LearnerEmploymentVerification>
{
    public void Configure(EntityTypeBuilder<LearnerEmploymentVerification> builder)
    {
        builder.ToTable("LearnerEmploymentVerification", "EVS");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("bigint").UseIdentityColumn().IsRequired();
        builder.Property(x => x.EmploymentVerificationId).HasColumnType("bigint").IsRequired();
        builder.Property(x => x.UniqueLearnerNumber).HasColumnName("ULN").HasColumnType("bigint").IsRequired();
        builder.Property(x => x.EmployerAccountId).HasColumnType("bigint").IsRequired();
        builder.HasOne(x => x.Detail)
            .WithOne()
            .HasForeignKey<EmploymentVerification>(x => x.Id);
        builder.HasMany(x => x.LearnerEmploymentChecks)
            .WithOne()
            .HasForeignKey(x => x.LearnerEmploymentVerificationId);
    }

}
