using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFA.DAS.EmploymentVerificationService.Data.Entities;

namespace SFA.DAS.EmploymentVerificationService.Data.Configurations;

public class EmploymentCheckRequestConfiguration : IEntityTypeConfiguration<EmploymentCheckRequest>
{
    public void Configure(EntityTypeBuilder<EmploymentCheckRequest> builder)
    {
        builder.ToTable("EmploymentVerificationRequest", "EVS");
        builder.HasKey(x => x.Id).HasName("PK_EmploymentCheckRequest");

        builder.Property(x => x.Id).HasColumnType("bigint").UseIdentityColumn().IsRequired();
        builder.Property(x => x.CorrelationId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(x => x.ULN).HasColumnType("bigint").IsRequired();
        builder.Property(x => x.AccountId).HasColumnType("bigint").IsRequired();
        builder.Property(x => x.ApprenticeshipId).HasColumnType("bigint").IsRequired(false);
        builder.Property(x => x.Reference).HasColumnType("nvarchar(255)").HasMaxLength(255).IsRequired(false);
        builder.Property(x => x.StartDate).HasColumnType("datetime2").HasPrecision(7).IsRequired();
        builder.Property(x => x.EndDate).HasColumnType("datetime2").HasPrecision(7).IsRequired();
        builder.Property(x => x.NumberOfAttempts).HasColumnType("int").IsRequired();
        builder.Property(x => x.Status).HasColumnType("tinyint").IsRequired();
        builder.Property(x => x.CreatedOn).HasColumnType("datetime2").HasPrecision(7).IsRequired();
    }
}
