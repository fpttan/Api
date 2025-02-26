using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public DbSet<License> Licenses { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=licenses.db");
    }
}