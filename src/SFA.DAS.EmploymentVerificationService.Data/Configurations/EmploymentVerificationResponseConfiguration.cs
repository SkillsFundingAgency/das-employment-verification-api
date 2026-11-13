using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFA.DAS.EmploymentVerificationService.Data.Entities;

namespace SFA.DAS.EmploymentVerificationService.Data.Configurations;

public class EmploymentVerificationResponseConfiguration : IEntityTypeConfiguration<EmploymentVerificationResponse>
{
    public void Configure(EntityTypeBuilder<EmploymentVerificationResponse> builder)
    {
        builder.ToTable("EmploymentVerificationResponse", "EVS");
        builder.HasKey(x => x.Id).HasName("PK_EmploymentCheckResponse");

        builder.Property(x => x.Id).HasColumnType("bigint").UseIdentityColumn().IsRequired();
        builder.Property(x => x.CorrelationId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(x => x.Employed).HasColumnType("bit").IsRequired();
        builder.Property(x => x.CreatedOn).HasColumnType("datetime2").HasPrecision(7).IsRequired();
    }
}
