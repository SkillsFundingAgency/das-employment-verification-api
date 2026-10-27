using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SFA.DAS.EmploymentVerificationService.Data.Entities;

namespace SFA.DAS.EmploymentVerificationService.Data.Configurations;

public class EmploymentCheckResponseConfiguration : IEntityTypeConfiguration<EmploymentCheckResponse>
{
    public void Configure(EntityTypeBuilder<EmploymentCheckResponse> builder)
    {
        builder.ToTable("EmploymentCheckResponse", "EVS");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnType("bigint").UseIdentityColumn().IsRequired();
        builder.Property(x => x.CorrelationId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(x => x.Employed).HasColumnType("bit").IsRequired();
        builder.Property(x => x.CreatedOn).HasColumnType("datetime2").HasPrecision(7).IsRequired();
    }
}
