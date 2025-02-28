using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

[Route("api/licenses")]
[ApiController]
public class LicenseController : ControllerBase
{
    private readonly LicenseDbContext _context;

    public LicenseController(LicenseDbContext context)
    {
        _context = context;
    }

    // ✅ Lấy danh sách tất cả License (Chỉ Admin có quyền)
    [HttpGet]
    public IActionResult GetAllLicenses([FromHeader(Name = "X-API-KEY")] string apiKey)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey)
            return Forbid();

        var licenses = _context.Licenses.ToList();
        return Ok(licenses);
    }

    [HttpGet("{key}")]
public IActionResult GetLicense(string key)
{
    var license = _context.Licenses.Find(key);
    if (license == null) return NotFound();

    // Serialize object JSON
    var jsonData = System.Text.Json.JsonSerializer.Serialize(license);

    // Mã hóa AES
    var encryptedLicense = EncryptData(jsonData, out string ivBase64);

    return Ok(new { data = encryptedLicense, iv = ivBase64 });
}

private string EncryptData(string plainText, out string ivBase64)
{
    using (Aes aes = Aes.Create())
    {
        aes.Key = Encoding.UTF8.GetBytes("12345678901234567890123456789012"); // 32 bytes key (AES-256)
        aes.GenerateIV(); // Sinh IV ngẫu nhiên
        ivBase64 = Convert.ToBase64String(aes.IV); // Lưu IV dưới dạng Base64
        
        using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
        {
            byte[] inputBuffer = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedData = encryptor.TransformFinalBlock(inputBuffer, 0, inputBuffer.Length);
            return Convert.ToBase64String(encryptedData);
        }
    }
}


    // ✅ Thêm License mới (Chỉ Admin có quyền)
    [HttpPost]
    public IActionResult AddLicense([FromHeader(Name = "X-API-KEY")] string apiKey, [FromBody] License license)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey)
            return Forbid();
        try
        {
            if (_context.Licenses.Any(l => l.LicenseKey == license.LicenseKey))
                return Conflict("License already exists");
            _context.Licenses.Add(license);
            _context.SaveChangesAsync();
            return Ok(new { message = "License added successfully!" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
    

    // ✅ Cập nhật License (Chỉ Admin có quyền)
    [HttpPut("{licenseKey}")]
    public IActionResult UpdateLicense([FromHeader(Name = "X-API-KEY")] string apiKey, string licenseKey, [FromBody] License updatedLicense)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey)
            return Forbid();

        var license = _context.Licenses.FirstOrDefault(l => l.LicenseKey == licenseKey);
        if (license == null) return NotFound();

        // Cập nhật thông tin License
        license.Name = updatedLicense.Name;
        license.TimeExpireDaily = updatedLicense.TimeExpireDaily;
        license.TimeExpire200v = updatedLicense.TimeExpire200v;

        _context.SaveChanges();
        return Ok(license);
    }

    // ✅ Xóa License (Chỉ Admin có quyền)
    [HttpDelete("{licenseKey}")]
    public IActionResult DeleteLicense([FromHeader(Name = "X-API-KEY")] string apiKey, string licenseKey)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey)
            return Forbid();

        var license = _context.Licenses.FirstOrDefault(l => l.LicenseKey == licenseKey);
        if (license == null) return NotFound();

        _context.Licenses.Remove(license);
        _context.SaveChanges();
        return Ok($"License {licenseKey} deleted successfully");
    }
}
