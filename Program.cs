using LicenseAPI.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<LicenseDbContext>(options =>
    options.UseSqlite("Data Source=licenses.db"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
    db.Database.Migrate();
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.Run("http://0.0.0.0:5009");
