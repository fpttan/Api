using Microsoft.EntityFrameworkCore;
using LicenseAPI.Models;

namespace LicenseAPI.Data
{
    public class LicenseDbContext : DbContext
    {
        public LicenseDbContext(DbContextOptions<LicenseDbContext> options) : base(options) { }

        public DbSet<License> Licenses { get; set; }
    }
}
