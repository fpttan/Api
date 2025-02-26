using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>();
var app = builder.Build();

// Khóa AES để mã hóa dữ liệu
var AES_KEY = Encoding.UTF8.GetBytes("ThisIsASecretKey1234567890123456"); // 32 bytes
var AES_IV = Encoding.UTF8.GetBytes("ThisIsAnIV123456"); // 16 bytes
const string ADMIN_API_KEY = "MY_SECRET_ADMIN_KEY"; // API Key của admin

// ✅ Validate License (trả về dữ liệu mã hóa)
app.MapPost("/api/license/validate", async (AppDbContext db, License input) =>
{
    var license = await db.Licenses.FirstOrDefaultAsync(l => l.Key == input.Key);
    string message = license != null ? "License Found" : "Invalid License";
    string expiryDaily = license?.ExpiryDateDaily.ToString("yyyy-MM-dd") ?? "N/A";
    string expiry200v = license?.ExpiryDate200v.ToString("yyyy-MM-dd") ?? "N/A";

    return Results.Ok(new 
    { 
        Message = EncryptData(message), 
        ExpiryDateDaily = EncryptData(expiryDaily), 
        ExpiryDate200v = EncryptData(expiry200v) 
    });
});

// ✅ Thêm License Key (Chỉ Admin)
app.MapPost("/api/license/add", async (HttpContext context, AppDbContext db, License newLicense) =>
{
    if (!IsAdmin(context)) return Results.Forbid();

    if (await db.Licenses.AnyAsync(l => l.Key == newLicense.Key))
        return Results.BadRequest(new { Message = EncryptData("License Key already exists") });

    db.Licenses.Add(newLicense);
    await db.SaveChangesAsync();
    return Results.Ok(new { Message = EncryptData("License Key added successfully") });
});

// ✅ Xóa License Key (Chỉ Admin)
app.MapDelete("/api/license/delete/{key}", async (HttpContext context, AppDbContext db, string key) =>
{
    if (!IsAdmin(context)) return Results.Forbid();

    var license = await db.Licenses.FindAsync(key);
    if (license == null)
        return Results.NotFound(new { Message = EncryptData("License Key not found") });

    db.Licenses.Remove(license);
    await db.SaveChangesAsync();
    return Results.Ok(new { Message = EncryptData("License Key deleted successfully") });
});

// ✅ Sửa License Key (Chỉ Admin)
app.MapPut("/api/license/update", async (HttpContext context, AppDbContext db, License updatedLicense) =>
{
    if (!IsAdmin(context)) return Results.Forbid();

    var license = await db.Licenses.FindAsync(updatedLicense.Key);
    if (license == null)
        return Results.NotFound(new { Message = EncryptData("License Key not found") });

    license.Name = updatedLicense.Name;
    license.ExpiryDateDaily = updatedLicense.ExpiryDateDaily;
    license.ExpiryDate200v = updatedLicense.ExpiryDate200v;
    await db.SaveChangesAsync();

    return Results.Ok(new { Message = EncryptData("License Key updated successfully") });
});

// ✅ Lấy danh sách tất cả License Key (Chỉ Admin)
app.MapGet("/api/license/all", async (HttpContext context, AppDbContext db) =>
{
    if (!IsAdmin(context)) return Results.Forbid();

    var encryptedData = await db.Licenses
        .Select(l => new
        {
            Name = EncryptData(l.Name),
            Key = EncryptData(l.Key),
            ExpiryDateDaily = EncryptData(l.ExpiryDateDaily.ToString("yyyy-MM-dd")),
            ExpiryDate200v = EncryptData(l.ExpiryDate200v.ToString("yyyy-MM-dd"))
        })
        .ToListAsync();

    return Results.Ok(encryptedData);
});

// ✅ Trả về những key đã hết hạn (Chỉ Admin)
app.MapGet("/api/license/expired", async (HttpContext context, AppDbContext db) =>
{
    if (!IsAdmin(context)) return Results.Forbid();

    var expired = await db.Licenses
        .Where(l => l.ExpiryDateDaily < DateTime.UtcNow || l.ExpiryDate200v < DateTime.UtcNow)
        .Select(l => new
        {
            Name = EncryptData(l.Name),
            Key = EncryptData(l.Key),
            ExpiryDateDaily = EncryptData(l.ExpiryDateDaily.ToString("yyyy-MM-dd")),
            ExpiryDate200v = EncryptData(l.ExpiryDate200v.ToString("yyyy-MM-dd"))
        })
        .ToListAsync();

    return Results.Ok(expired);
});

// ✅ Trả về những key còn hạn (Chỉ Admin)
app.MapGet("/api/license/valid", async (HttpContext context, AppDbContext db) =>
{
    if (!IsAdmin(context)) return Results.Forbid();

    var valid = await db.Licenses
        .Where(l => l.ExpiryDateDaily > DateTime.UtcNow || l.ExpiryDate200v > DateTime.UtcNow)
        .Select(l => new
        {
            Name = EncryptData(l.Name),
            Key = EncryptData(l.Key),
            ExpiryDateDaily = EncryptData(l.ExpiryDateDaily.ToString("yyyy-MM-dd")),
            ExpiryDate200v = EncryptData(l.ExpiryDate200v.ToString("yyyy-MM-dd"))
        })
        .ToListAsync();

    return Results.Ok(valid);
});

// ✅ Hàm kiểm tra Admin
bool IsAdmin(HttpContext context)
{
    return context.Request.Headers["api_key"] == ADMIN_API_KEY;
}

// ✅ Hàm mã hóa AES
string EncryptData(string plainText)
{
    using Aes aes = Aes.Create();
    aes.Key = AES_KEY;
    aes.IV = AES_IV;

    using ICryptoTransform encryptor = aes.CreateEncryptor();
    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
    byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
    return Convert.ToBase64String(cipherBytes);
}

// Chạy ứng dụng
app.Run("http://0.0.0.0:5009");
