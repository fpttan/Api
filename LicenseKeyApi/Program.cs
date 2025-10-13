using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ✅ Cấu hình giới hạn request theo IP
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        string ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ipAddress,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,  // Tối đa 10 request
                Window = TimeSpan.FromSeconds(10), // Trong 10 giây
                QueueLimit = 5 // Không xếp hàng nếu quá giới hạn
            }
        );
    });
});

var dbPath = "licenses.db";

// ✅ Đăng ký DbContext sử dụng SQLite
builder.Services.AddDbContext<LicenseDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// ✅ Đăng ký NotificationService
builder.Services.AddSingleton<NotificationService>();

builder.Services.AddControllers();

var app = builder.Build();
Console.WriteLine(ApiKeyMiddleware.AdminApiKey);
Console.WriteLine(ApiKeyMiddleware.AESKey);
Console.WriteLine(FileController.DataRoot);
bool havedb = false;
// ✅ Kiểm tra & tạo database nếu chưa có
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
    if (!File.Exists(dbPath))
    {
        havedb = false;
        Console.WriteLine("⚡ Database không tồn tại. Đang tạo mới...");
        dbContext.Database.EnsureCreated();
        Console.WriteLine("✅ Database và bảng đã được tạo!");
    }
    else
    {
        havedb = true;
        Console.WriteLine("✅ Database đã tồn tại.");
    
    }
}
if(havedb)
{
      using (var connection_db = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=licenses.db"))
       {
            connection_db.Open();
           using (var command = connection_db.CreateCommand())
           {
               Console.WriteLine("✅ Kiểm tra xem cột đã tồn tại chưa");
               using (var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=licenses.db"))
                   // Kiểm tra xem cột đã tồn tại chưa
                   command.CommandText = "PRAGMA table_info(Licenses);";
               bool hasColumn = false;

               using (var reader = command.ExecuteReader())
               {
                   while (reader.Read())
                   {
                       if (reader.GetString(1) == "TimeExpireTool")
                       {
                           hasColumn = true;
                           break;
                       }
                   }
               }

               // Nếu chưa có thì thêm cột
               if (!hasColumn)
               {
                   Console.WriteLine("✅ Chưa tồn tại, đang thêm cột TimeExpireTool");
                   command.CommandText = "ALTER TABLE Licenses ADD COLUMN TimeExpireTool TEXT;";
                   command.ExecuteNonQuery();
               }
               else
               {
                   Console.WriteLine("✅ Cột TimeExpireTool đã tồn tại, không cần thêm.");
               }
        
    }
       }
}
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// ✅ Bật middleware chống DDoS
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.Run("http://0.0.0.0:5009");



