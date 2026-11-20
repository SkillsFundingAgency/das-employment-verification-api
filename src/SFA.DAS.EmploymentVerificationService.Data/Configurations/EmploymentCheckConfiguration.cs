using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFA.DAS.EmploymentVerificationService.Models;

namespace SFA.DAS.EmploymentVerificationService.Data.Configurations;

public class EmploymentCheckConfiguration : IEntityTypeConfiguration<EmploymentCheck>
{
    public void Configure(EntityTypeBuilder<EmploymentCheck> builder)
    {
        builder.ToTable("EmploymentCheck", "EVS");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("bigint").UseIdentityColumn().IsRequired();
        builder.Property(x => x.CheckCorrelationId).IsRequired();
        builder.Property(x => x.StartDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.EndDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.Reference).HasColumnType("varchar(200)").IsRequired(false);
        builder.Property(x => x.RequestTime).HasColumnType("datetime2").HasPrecision(7).IsRequired();
        builder.Property(x => x.ResponseTime).HasColumnType("datetime2").HasPrecision(7).IsRequired(false);
        builder.Property(x => x.FailureReason).HasColumnType("varchar(200)").IsRequired(false);
        builder.Property(x => x.Status).HasColumnType("tinyint").IsRequired();
        builder.Property(x => x.CreationTime).HasColumnType("datetime2").HasPrecision(7).HasDefaultValueSql("GETDATE()").IsRequired();
    }
}
