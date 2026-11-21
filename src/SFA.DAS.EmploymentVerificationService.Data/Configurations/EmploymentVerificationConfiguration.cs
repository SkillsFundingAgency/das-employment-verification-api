using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFA.DAS.EmploymentVerificationService.Models;

namespace SFA.DAS.EmploymentVerificationService.Data.Configurations;

public class EmploymentVerificationConfiguration : IEntityTypeConfiguration<EmploymentVerification>
{
    public void Configure(EntityTypeBuilder<EmploymentVerification> builder)
    {
        builder.ToTable("EmploymentVerification", "EVS");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("bigint").UseIdentityColumn().IsRequired();
        builder.Property(x => x.Type).HasColumnType("tinyint").IsRequired();
        builder.Property(x => x.ExternalCorrelationId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(x => x.StartDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.DurationInDays).HasColumnType("int").IsRequired();
        builder.Property(x => x.Reference).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(x => x.Status).HasColumnType("tinyint").IsRequired();
        builder.Property(x => x.FailureReason).HasColumnType("nvarchar(200)").IsRequired(false);
        builder.Property(x => x.RetryCount).HasColumnType("tinyint").IsRequired();
        builder.Property(x => x.NextRetryTime).HasColumnType("datetime2").HasPrecision(7).IsRequired(false);
    }
}
