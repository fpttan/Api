using Microsoft.EntityFrameworkCore;

public class LicenseDbContext : DbContext
{
    public LicenseDbContext(DbContextOptions<LicenseDbContext> options) : base(options) { }

    // ✅ Ánh xạ bảng Licenses trong database
    public DbSet<License> Licenses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<License>().ToTable("Licenses");
    }
}
