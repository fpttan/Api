using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using LicenseServer.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddDbContext<LicenseDbContext>();

var app = builder.Build();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
    db.Database.EnsureCreated();
}
app.Run("http://0.0.0.0:5009");
