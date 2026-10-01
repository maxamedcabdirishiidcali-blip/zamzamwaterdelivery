using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZamzamWaterDelivery.Domain.Entities;

namespace ZamzamWaterDelivery.Infrastructure.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(u => u.FullName).HasMaxLength(100).IsRequired();
        b.HasIndex(u => u.IsActive);
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.Property(c => c.FullName).HasMaxLength(100).IsRequired();
        b.Property(c => c.PhoneNumber).HasMaxLength(30).IsRequired();
        b.Property(c => c.Email).HasMaxLength(150).IsRequired();

        b.HasIndex(c => c.UserId).IsUnique();
        b.HasIndex(c => c.PhoneNumber);
        b.HasIndex(c => c.Email);

        b.HasOne(c => c.User)
            .WithOne(u => u.Customer)
            .HasForeignKey<Customer>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> b)
    {
        b.Property(a => a.AddressLine).HasMaxLength(200).IsRequired();
        b.Property(a => a.City).HasMaxLength(60).IsRequired();
        b.Property(a => a.Location).HasMaxLength(100);

        b.HasOne(a => a.Customer)
            .WithMany(c => c.Addresses)
            .HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> b)
    {
        b.Property(d => d.FullName).HasMaxLength(100).IsRequired();
        b.Property(d => d.PhoneNumber).HasMaxLength(30).IsRequired();
        b.Property(d => d.Email).HasMaxLength(150).IsRequired();
        b.Property(d => d.VehicleType).HasMaxLength(50).IsRequired();
        b.Property(d => d.VehicleNumber).HasMaxLength(30).IsRequired();
        b.Property(d => d.LicenseNumber).HasMaxLength(50).IsRequired();
        b.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);

        b.HasIndex(d => d.UserId).IsUnique();
        b.HasIndex(d => d.LicenseNumber).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(d => d.Status);

        b.HasOne(d => d.User)
            .WithOne(u => u.Driver)
            .HasForeignKey<Driver>(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.Property(n => n.Title).HasMaxLength(150).IsRequired();
        b.Property(n => n.Message).HasMaxLength(1000).IsRequired();
        b.Property(n => n.Type).HasConversion<string>().HasMaxLength(30);
        b.Property(n => n.Channel).HasConversion<string>().HasMaxLength(20);

        b.HasIndex(n => new { n.UserId, n.IsRead });

        b.HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(n => n.Order)
            .WithMany()
            .HasForeignKey(n => n.OrderId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
