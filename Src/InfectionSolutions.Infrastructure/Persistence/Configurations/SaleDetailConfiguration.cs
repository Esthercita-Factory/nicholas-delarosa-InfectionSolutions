using InfectionSolutions.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfectionSolutions.Infrastructure.Persistence.Configurations;

public class SaleDetailConfiguration : IEntityTypeConfiguration<SaleDetail>
{
    public void Configure(EntityTypeBuilder<SaleDetail> builder)
    {
        builder.ToTable("sale_details", table =>
        {
            table.HasCheckConstraint("ck_sale_details_quantity_positive", "quantity > 0");
        });

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(d => d.SaleId)
            .HasColumnName("sale_id")
            .IsRequired();

        builder.Property(d => d.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.Property(d => d.Quantity)
            .HasColumnName("quantity")
            .IsRequired();

        builder.Property(d => d.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(14, 2)
            .IsRequired();

        builder.Property(d => d.LineTotal)
            .HasColumnName("line_total")
            .HasPrecision(14, 2)
            .IsRequired();

        // Los detalles viven y mueren con su venta.
        builder.HasOne(d => d.Sale)
            .WithMany(s => s.Details)
            .HasForeignKey(d => d.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un producto vendido no se puede borrar: se desactiva.
        builder.HasOne(d => d.Product)
            .WithMany(p => p.SaleDetails)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.SaleId);
        builder.HasIndex(d => d.ProductId);
    }
}
