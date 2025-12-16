using Microsoft.EntityFrameworkCore;

public class ApiDbContext : DbContext
{
    public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options) { }

    // ✅ Ánh xạ bảng Licenses trong database
    public DbSet<License> Licenses { get; set; }
    public DbSet<LicenseExtension> LicenseExtension { get; set; }
    public DbSet<Payment> Payment { get; set; }
    public DbSet<PaymentIntent> PaymentIntent { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Licenses
        // Licenses
        modelBuilder.Entity<License>(e =>
        {
            e.ToTable("Licenses");
            e.HasKey(x => x.LicenseKey);
            // các DateTime? mặc định map TEXT ISO-8601
        });

        // PaymentIntent
        modelBuilder.Entity<PaymentIntent>(e =>
        {
            e.ToTable("PaymentIntent");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TransferNote, x.Status });
            e.HasIndex(x => x.LicenseKey);
        });

        // Payment
        modelBuilder.Entity<Payment>(e =>
        {
            e.ToTable("Payment");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.IntentId);
            e.HasIndex(x => x.ReferenceCode).IsUnique();
            e.HasIndex(x => new { x.Provider, x.ExternalId,x.ReferenceCode }).IsUnique(); // idempotent
        });

        // LicenseExtension
        modelBuilder.Entity<LicenseExtension>(e =>
        {
            e.ToTable("LicenseExtension");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.LicenseKey);
        });

    }
}
