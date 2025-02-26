using Microsoft.EntityFrameworkCore;

namespace LicenseServer.Models
{
    public class LicenseDbContext : DbContext
    {
        public DbSet<License> Licenses { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite("Data Source=licenses.db");
    }
}
