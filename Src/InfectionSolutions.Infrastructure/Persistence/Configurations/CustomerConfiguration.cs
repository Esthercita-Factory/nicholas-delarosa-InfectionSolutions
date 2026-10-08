using InfectionSolutions.Domain.Entities;
using InfectionSolutions.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfectionSolutions.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers", table =>
        {
            table.HasCheckConstraint("ck_customers_age_range", "age BETWEEN 18 AND 120");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        // Cedula, NIT o pasaporte: texto y no numero, porque puede traer
        // ceros a la izquierda o letras.
        builder.Property(c => c.Document)
            .HasColumnName("document")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(c => c.Age)
            .HasColumnName("age")
            .IsRequired();

        builder.Property(c => c.Email)
            .HasColumnName("email")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(c => c.Phone)
            .HasColumnName("phone")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Address)
            .HasColumnName("address")
            .HasMaxLength(150);

        // Sin HasDefaultValue(true): EF omitiria el false en el INSERT y la
        // base lo guardaria como activo.
        builder.Property(c => c.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(c => c.UserId)
            .HasColumnName("user_id");

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at");

        // Si se borra el usuario del portal, el cliente y su historial quedan.
        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<Customer>(c => c.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => c.Document).IsUnique();
        builder.HasIndex(c => c.Email).IsUnique();
        builder.HasIndex(c => c.UserId).IsUnique();
        builder.HasIndex(c => c.FullName);
    }
}
