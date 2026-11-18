using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFA.DAS.EmploymentVerificationService.Models;

namespace SFA.DAS.EmploymentVerificationService.Data.Configurations;

public class LearnerEmploymentCheckConfiguration : IEntityTypeConfiguration<LearnerEmploymentCheck>
{
    public void Configure(EntityTypeBuilder<LearnerEmploymentCheck> builder)
    {
        builder.ToTable("LearnerEmploymentCheck", "EVS");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("bigint").UseIdentityColumn().IsRequired();
        builder.Property(x => x.EmploymentCheckId).HasColumnType("bigint").IsRequired();
        builder.Property(x => x.LearnerEmploymentVerificationId).HasColumnType("bigint").IsRequired();
        builder.Property(x => x.UniqueLearnerNumber).HasColumnName("ULN").HasColumnType("bigint").IsRequired();
        builder.Property(x => x.EmployerAccountId).HasColumnType("bigint").IsRequired();
        builder.HasOne(x => x.Detail)
            .WithOne()
            .HasForeignKey<LearnerEmploymentCheck>(x => x.EmploymentCheckId)
            .IsRequired();
    }
}
