using InvoiceBuilder.Domain.Businesses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceBuilder.Infrastructure.Persistence.Configurations;

public sealed class BusinessSettingsConfiguration : IEntityTypeConfiguration<BusinessSettings>
{
    public void Configure(EntityTypeBuilder<BusinessSettings> builder)
    {
        builder.ToTable("BusinessSettings");
        builder.Property(s => s.BankDetails).HasMaxLength(BusinessSettings.MaxBankDetailsLength);
        builder.Property(s => s.EmailFromName).HasMaxLength(200);
        builder.Property(s => s.EmailReplyTo).HasMaxLength(320);
        builder.Property(s => s.WhatsAppPhoneNumberId).HasMaxLength(50);
        builder.Property(s => s.WhatsAppTokenEncrypted).HasMaxLength(2000);
        builder.HasIndex(s => s.BusinessId).IsUnique();
    }
}
