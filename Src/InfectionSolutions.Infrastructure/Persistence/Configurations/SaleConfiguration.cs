using InfectionSolutions.Domain.Entities;
using InfectionSolutions.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfectionSolutions.Infrastructure.Persistence.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("sales", table =>
        {
            table.HasCheckConstraint("ck_sales_total_matches", "total = subtotal + tax");
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(s => s.SaleNumber)
            .HasColumnName("sale_number")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(s => s.SaleDate)
            .HasColumnName("sale_date")
            .HasDefaultValueSql("now()")
            .IsRequired();

        // Se guarda como texto para que la base sea legible sin el enum.
        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.Subtotal)
            .HasColumnName("subtotal")
            .HasPrecision(14, 2)
            .IsRequired();

        builder.Property(s => s.Tax)
            .HasColumnName("tax")
            .HasPrecision(14, 2)
            .IsRequired();

        builder.Property(s => s.Total)
            .HasColumnName("total")
            .HasPrecision(14, 2)
            .IsRequired();

        builder.Property(s => s.Notes)
            .HasColumnName("notes")
            .HasMaxLength(500);

        builder.Property(s => s.CreatedByUserId)
            .HasColumnName("created_by_user_id");

        builder.Property(s => s.CancelledAt)
            .HasColumnName("cancelled_at");

        // Un cliente con ventas no se puede borrar: se desactiva.
        builder.HasOne(s => s.Customer)
            .WithMany(c => c.Sales)
            .HasForeignKey(s => s.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(s => s.SaleNumber).IsUnique();
        builder.HasIndex(s => s.CustomerId);
        builder.HasIndex(s => s.SaleDate);
        builder.HasIndex(s => s.Status);
    }
}
