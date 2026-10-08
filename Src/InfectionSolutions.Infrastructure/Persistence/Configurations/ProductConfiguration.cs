using InfectionSolutions.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfectionSolutions.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", table =>
        {
            table.HasCheckConstraint("ck_products_price_positive", "price > 0");
            table.HasCheckConstraint("ck_products_stock_non_negative", "stock >= 0");
        });

        builder.HasKey(p => p.Id);

        // La base genera el id; EF lo omite en el INSERT y lo lee de vuelta.
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(p => p.Sku)
            .HasColumnName("sku")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(p => p.Category)
            .HasColumnName("category")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(p => p.Unit)
            .HasColumnName("unit")
            .HasMaxLength(30)
            .HasDefaultValue("Unidad")
            .IsRequired();

        // numeric(14,2): con plata nunca punto flotante.
        builder.Property(p => p.Price)
            .HasColumnName("price")
            .HasPrecision(14, 2)
            .IsRequired();

        builder.Property(p => p.Stock)
            .HasColumnName("stock")
            .IsRequired();

        // Sin HasDefaultValue(true): EF omitiria el false en el INSERT y la
        // base lo guardaria como activo.
        builder.Property(p => p.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(p => p.Sku).IsUnique();
        builder.HasIndex(p => p.Name);
        builder.HasIndex(p => p.Category);
    }
}
