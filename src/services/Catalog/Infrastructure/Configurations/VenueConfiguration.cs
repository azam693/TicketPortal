using Catalog.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Infrastructure.Configurations;

public class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        builder.ToTable("venues");
        
        builder.HasKey(venue => venue.Id);
        builder.Property(venue => venue.Id).ValueGeneratedNever();
        
        builder.Property(venue => venue.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(venue => venue.City)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(venue => venue.Address)
            .IsRequired()
            .HasMaxLength(100);

        builder.OwnsMany(venue => venue.Sections, section =>
        {
            section.Property(s => s.Id).ValueGeneratedNever();
            section.Property(s => s.Name).IsRequired().HasMaxLength(100);
            section.Property(s => s.Category).HasConversion<string>().HasMaxLength(20);
        });

        builder.Navigation(venue => venue.Sections)
            .HasField("_sections")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
