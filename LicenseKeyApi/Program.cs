using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ✅ Cấu hình giới hạn request theo IP
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var ip = httpContext.GetClientIp();

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromSeconds(10),
                QueueLimit = 50
            }
        );
    });
});

var dbPath = "licenses.db";

// ✅ Đăng ký DbContext sử dụng SQLite
builder.Services.AddDbContext<ApiDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// ---- PlanCatalog (đọc từ appsettings.json) ----
// Bind từ "PlanCatalog" trong appsettings
builder.Services.Configure<PlanCatalogCfg>(builder.Configuration.GetSection("PlanCatalog"));
// Đăng ký service dùng options
builder.Services.AddSingleton<IPricingPolicy, DefaultPricingPolicy>();
builder.Services.AddSingleton<IPlanCatalog, PlanCatalog>();
builder.Services.AddSingleton<IAppLogger, AppLogger>();

builder.Services.AddHostedService<IntentExpirationService>();


// ✅ Đăng ký NotificationService
builder.Services.AddSingleton<NotificationService>();

builder.Services.AddControllers();

var app = builder.Build();
bool havedb = false;
// ✅ Kiểm tra & tạo database nếu chưa có
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
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
    using (var connection_db = new SqliteConnection("Data Source=licenses.db"))
    {
        connection_db.Open();

        using var command = connection_db.CreateCommand();

        // 1️⃣ Lấy danh sách cột
        command.CommandText = "PRAGMA table_info(Licenses);";

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                columns.Add(reader.GetString(1)); // column name
            }
        }

        // 2️⃣ TypeTool
        if (!columns.Contains("TypeTool"))
        {
            Console.WriteLine("✅ Thêm cột TypeTool");
            command.CommandText =
                "ALTER TABLE Licenses ADD COLUMN TypeTool TEXT;";
            command.ExecuteNonQuery();
        }
        else
        {
            Console.WriteLine("✅ Cột TypeTool đã tồn tại");
        }

        // 3️⃣ TimeExpireAoMaThap
        if (!columns.Contains("TimeExpireAoMaThap"))
        {
            Console.WriteLine("✅ Thêm cột TimeExpireAoMaThap");
            command.CommandText =
                "ALTER TABLE Licenses ADD COLUMN TimeExpireAoMaThap TEXT;";
            command.ExecuteNonQuery();
        }
        else
        {
            Console.WriteLine("✅ Cột TimeExpireAoMaThap đã tồn tại");
        }
        // 3️⃣ TimeExpireAoMaThap
        if (!columns.Contains("TimeExpireNoel"))
        {
            Console.WriteLine("✅ Thêm cột TimeExpireNoel");
            command.CommandText =
                "ALTER TABLE Licenses ADD COLUMN TimeExpireNoel TEXT;";
            command.ExecuteNonQuery();
        }
        else
        {
            Console.WriteLine("✅ Cột TimeExpireNoel đã tồn tại");
        }
    }

}
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// ✅ Bật middleware chống DDoS

app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.Run("http://0.0.0.0:5009");



