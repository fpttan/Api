using Microsoft.AspNetCore.Mvc;
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
        var license = _context.Licenses.FirstOrDefault(key);
        if (license == null) return NotFound();

        // Serialize object JSON
        var jsonData = System.Text.Json.JsonSerializer.Serialize(license);

        // Mã hóa AES
        var encryptedLicense = EncryptData(jsonData, out string ivBase64);

        return Ok(new { data = encryptedLicense, data2 = ivBase64 });
    }

    private static string EncryptData(string plainText, out string ivBase64)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = ConvertHexStringToByteArray(ApiKeyMiddleware.AESKey);
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
    // Chuyển chuỗi HEX sang mảng byte
    private static byte[] ConvertHexStringToByteArray(string hex)
    {
        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    // ✅ Thêm License mới (Chỉ Admin có quyền)
    [HttpPost]
    public IActionResult AddLicense([FromHeader(Name = "X-API-KEY")] string apiKey, [FromBody] License license)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey)
            return Forbid();
        try
        {
            if(string.IsNullOrEmpty(license.LicenseKey))
                 return BadRequest(new { error = "dữ liệu LicenseKey không hợp lệ" });

            if (_context.Licenses.Any(l => l.LicenseKey == license.LicenseKey))
                return Conflict("License already exists");
            
            // Kiểm tra định dạng ngày tháng (dd/MM/yyyy)
            if (!IsValidDateFormat(license.TimeExpireDaily) || !IsValidDateFormat(license.TimeExpire200v))
                return BadRequest(new { error = "Ngày tháng không hợp lệ! Định dạng đúng: dd/MM/yyyy" });
            if(!IsValidBoolFormat(license.Multiversion))
                return BadRequest(new { error = "dữ liệu Multiversion không hợp lệ" });
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
    [HttpPut("updatelicense")]
    public IActionResult UpdateLicense([FromHeader(Name = "X-API-KEY")] string apiKey,  [FromBody] License updatedLicense)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey)
            return Forbid();

        if(string.IsNullOrEmpty(updatedLicense.LicenseKey))
                 return BadRequest(new { error = "dữ liệu LicenseKey không hợp lệ" });
                 
        var license = _context.Licenses.FirstOrDefault(l => l.LicenseKey == updatedLicense.LicenseKey);
        if (license == null) return NotFound();

        // Kiểm tra định dạng ngày tháng (dd/MM/yyyy)
        if (!IsValidDateFormat(updatedLicense.TimeExpireDaily) || !IsValidDateFormat(updatedLicense.TimeExpire200v))
            return BadRequest(new { error = "Ngày tháng không hợp lệ! Định dạng đúng: dd/MM/yyyy" });
        if (!IsValidBoolFormat(updatedLicense.Multiversion))
            return BadRequest(new { error = "dữ liệu Multiversion không hợp lệ" });
        // Cập nhật thông tin License
        license.Name = updatedLicense.Name;
        license.TimeExpireDaily = updatedLicense.TimeExpireDaily;
        license.TimeExpire200v = updatedLicense.TimeExpire200v;
        license.Multiversion = updatedLicense.Multiversion;

        _context.SaveChanges();
        return Ok(license);
        }

    // Hàm kiểm tra định dạng ngày tháng
    private bool IsValidDateFormat(string date)
    {
        if (string.IsNullOrWhiteSpace(date))
            return true;

        string[] formats = { "d/M/yyyy", "dd/MM/yyyy" };
        var result = DateTime.TryParseExact(date, formats, null, System.Globalization.DateTimeStyles.None, out DateTime hihi);
        return result;
    }
    private bool IsValidBoolFormat(string multi)
    {
        return !string.IsNullOrWhiteSpace(multi) && bool.TryParse(multi, out _);
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
