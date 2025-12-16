using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;

using System.Text;

[Route("api/licenses")]
[ApiController]
public class LicenseController : ControllerBase
{
    private readonly ApiDbContext _context;
    public LicenseController(ApiDbContext context)
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
    public IActionResult GetLicense(string key, 
                        [FromHeader(Name = "X-App-Version")] string? version,
                        [FromHeader(Name = "X-App-Sha")] string? sha,
                        [FromServices] IAppLogger logger)
    {
        string clientIp = HttpContext.GetClientIp();

        var data = new Dictionary<string, string?>
        {
            { "key", key },
            { "version", version },
            { "sha", sha },
            { "ip", clientIp }
        };

        var license = _context.Licenses
            .FirstOrDefault(x => x.LicenseKey == key);
        if (license == null)
        {
            logger.Warn("GetLicense called with invalid LicenseKey", data);
            return NotFound();
        }
        object dataresult;
        bool isNewClient = IsVersionGreaterThan(version, "3.1.4");
        if (isNewClient)
        {
            dataresult = new
            {
                LicenseKey = license.LicenseKey,
                Name = license.Name,
                TimeExpireDaily = license.TimeExpireDaily,
                TimeExpire200v = license.TimeExpire200v,
                TimeExpireAoMaThap = license.TimeExpireAoMaThap,
                TimeExpireNoel = license.TimeExpireNoel,
                Multiversion = license.Multiversion ?? false
            };
        }
        else
        {
            dataresult = new
            {
                LicenseKey = license.LicenseKey,
                Name = license.Name,
                TimeExpireDaily = license.TimeExpireDaily,
                TimeExpire200v = license.TimeExpire200v,
                Multiversion = license.Multiversion ?? false
            };
        }
        //// Tạo anonymous object chỉ chứa các trường cần thiết
        //var licenseInfo = new
        //{
        //    LicenseKey = license.LicenseKey,
        //    Name = license.Name,
        //    TimeExpireDaily = license.TimeExpireDaily,
        //    TimeExpire200v = license.TimeExpire200v,
        //    Multiversion = license.Multiversion ?? false
        //};

        logger.Info($"User accessed", new {name = license.Name, license = license.LicenseKey, ip = clientIp , version = version, sha = sha});
        // Serialize object JSON
        var jsonData = System.Text.Json.JsonSerializer.Serialize(dataresult);

        // Mã hóa AES
        var encryptedLicense = Security.EncryptData(jsonData, out string ivBase64);

        return Ok(new { data = encryptedLicense, data2 = ivBase64 });
    }
    private static bool IsVersionGreaterThan(string? clientVersion, string targetVersion)
    {
        if (string.IsNullOrWhiteSpace(clientVersion))
            return false;

        if (!Version.TryParse(clientVersion, out var vClient))
            return false;

        if (!Version.TryParse(targetVersion, out var vTarget))
            return false;

        return vClient > vTarget;
    }
    [HttpGet("tool")]
    public IActionResult GetLicenseTool([FromQuery] string key, [FromServices] IAppLogger logger)
    {
        if (string.IsNullOrEmpty(key))
        {
            logger.Warn("GetLicenseTool called without LicenseKey");
            return BadRequest("Vui lòng cung cấp LicenseKey.");
        }

        var license = _context.Licenses.Find(key);
        if (license == null) return NotFound();
        // Tạo anonymous object chỉ chứa các trường cần thiết
        var licenseInfo = new
        {
            LicenseKey = license.LicenseKey,
            Name = license.Name,
            TimeExpireTool = license.TimeExpireTool,
            TypeTool = license.TypeTool
        };
        // Serialize object JSON
        var jsonData = System.Text.Json.JsonSerializer.Serialize(licenseInfo);

        // Mã hóa AES
        var encryptedLicense = Security.EncryptData(jsonData, out string ivBase64);

        return Ok(new { data = encryptedLicense, data2 = ivBase64 });
    }

  
    // ✅ Thêm License mới (Chỉ Admin có quyền)
    [HttpPost]
    public IActionResult AddLicense([FromHeader(Name = "X-API-KEY")] string apiKey,
                                    [FromHeader(Name = "X-App-Version")] string? version,
                                    [FromHeader(Name = "X-App-Sha")] string? sha,
                                    [FromBody] License license, [FromServices] IAppLogger logger)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey) 
        {
            logger.Warn("Unauthorized attempt to add license");
            return Forbid();
        }
        try
        {
            if (string.IsNullOrEmpty(license.LicenseKey))
            {
                logger.Warn("Attempted to add license with invalid LicenseKey");
                return BadRequest(new { error = "dữ liệu LicenseKey không hợp lệ" });
            }

            if (_context.Licenses.Any(l => l.LicenseKey == license.LicenseKey))
            { 
                logger.Warn($"Attempted to add duplicate license: {license.LicenseKey}");
                return Conflict("License already exists");
            }

            _context.Licenses.Add(license);
            _context.SaveChangesAsync();
            logger.Info($"License {license.LicenseKey} added successfully by admin");
            return Ok(new { message = "License added successfully!" });
        }
        catch (Exception ex)
        {
            logger.Error("Error adding license", ex);
            return StatusCode(500, new { error = ex.Message });
        }
    }
    // Thêm License mới nhưng không kiểm tra quyền (Dành cho public)
    [HttpPost("addpublic")]
    public async Task<IActionResult> AddLicensePublic([FromBody] License content, [FromServices] IAppLogger logger)
    {
        logger.Info("AddLicensePublic called", content);
        try
        {
            if (content == null)
            {
                logger.Warn("AddLicensePublic called with null content");
                return BadRequest(new { error = "Yêu cầu không hợp lệ" });
            }

            content.LicenseKey = content.LicenseKey?.Trim();
            content.Name = content.Name?.Trim();

            if (string.IsNullOrEmpty(content.LicenseKey))
            {
                logger.Warn("AddLicensePublic dữ liệu LicenseKey không hợp lệ");
                return BadRequest(new { error = "dữ liệu LicenseKey không hợp lệ" });
            }

            if (string.IsNullOrEmpty(content.Name))
            {
                logger.Warn("AddLicensePublic dữ liệu Name không hợp lệ");
                return BadRequest(new { error = "dữ liệu Name không hợp lệ" });

            }
            if (content.Name.ToLower().Contains("admin"))
            {
                logger.Warn("AddLicensePublic Tên 'admin' không được phép sử dụng\"");
                return BadRequest(new { error = "Tên 'admin' không được phép sử dụng" });
            }

            if (content.LicenseKey.Length < 5)
            {
                logger.Warn("AddLicensePublic LicenseKey quá ngắn, tối thiểu 5 ký tự");
                return BadRequest(new { error = "LicenseKey quá ngắn, tối thiểu 5 ký tự" });
            }

            if (content.LicenseKey.Length > 40)
            {

                logger.Warn("AddLicensePublic LicenseKey quá dài, tối đa 40 ký tự");
                return BadRequest(new { error = "LicenseKey quá dài, tối đa 40 ký tự" });

            }

            if (_context.Licenses.Any(l => l.LicenseKey == content.LicenseKey))
            {
                logger.Warn($"AddLicensePublic LicenseKey {content.LicenseKey} đã tồn tại");
                return Conflict(new { error = "License already exists" });
            }

            if (_context.Licenses.Any(l => l.Name == content.Name))
            {

                logger.Warn($"AddLicensePublic Tên {content.Name} đã tồn tại");
                return BadRequest(new { error = "Tên đã tồn tại, vui lòng chọn tên khác" });

            }

            var license = new License
            {
                LicenseKey = content.LicenseKey,
                Name = content.Name,
                TimeExpire200v = null,
                TimeExpireDaily = null,
                TimeExpireTool = null,
                Multiversion = false,
                TypeTool = null
            };

            _context.Licenses.Add(license);
            await _context.SaveChangesAsync();

            logger.Info($"AddLicensePublic License {license.LicenseKey} added successfully");
            return Ok(new { message = "License added successfully!" });
        }
        catch (Exception ex)
        {
            logger.Error("AddLicensePublic Error adding license", ex);
            return StatusCode(500, new { error = ex.Message });
        }
    }


    // ✅ Cập nhật License (Chỉ Admin có quyền)
    [HttpPut("updatelicense")]
    public IActionResult UpdateLicense([FromHeader(Name = "X-API-KEY")] string apiKey,  [FromBody] License updatedLicense, [FromServices] IAppLogger logger)
    {
        logger.Info("UpdateLicense called", updatedLicense);

        if (apiKey != ApiKeyMiddleware.AdminApiKey)
        {
            logger.Warn("Unauthorized attempt to update license");
            return Forbid();
        }

        if(string.IsNullOrEmpty(updatedLicense.LicenseKey))
        {
            logger.Warn("Attempted to update license with invalid LicenseKey");
            return BadRequest(new { error = "dữ liệu LicenseKey không hợp lệ" });
        }

        var license = _context.Licenses.FirstOrDefault(l => l.LicenseKey == updatedLicense.LicenseKey);
        if (license == null) return NotFound();

        license.Name = updatedLicense.Name;
        license.TimeExpireDaily = updatedLicense.TimeExpireDaily;
        license.TimeExpire200v = updatedLicense.TimeExpire200v;
        license.TimeExpireTool = updatedLicense.TimeExpireTool;
        license.Multiversion = updatedLicense.Multiversion;
        license.TypeTool = updatedLicense.TypeTool;

        logger.Info($"License {license.Name} updated successfully by admin", license); 
        _context.SaveChanges();
        return Ok(license);
        }
    // ✅ Xóa License (Chỉ Admin có quyền)
    [HttpDelete("{licenseKey}")]
    public IActionResult DeleteLicense([FromHeader(Name = "X-API-KEY")] string apiKey, string licenseKey,[FromServices] IAppLogger logger)
    {
        logger.Info($"DeleteLicense called for {licenseKey}");
        if (apiKey != ApiKeyMiddleware.AdminApiKey)
        {
            logger.Warn("Unauthorized attempt to delete license");
            return Forbid();
        }

        var license = _context.Licenses.FirstOrDefault(l => l.LicenseKey == licenseKey);
        if (license == null) 
        {
            logger.Warn($"Attempted to delete non-existent license: {licenseKey}");
            return NotFound();
        }

        logger.Info($"License {license.LicenseKey} deleted successfully by admin");
        _context.Licenses.Remove(license);
        _context.SaveChanges();
        return Ok($"License {licenseKey} deleted successfully");
    }



}

public static class HttpContextExtensions
{
    public static string GetClientIp(this HttpContext context)
    {
        // 1️⃣ Kiểm tra header X-Forwarded-For (proxy / load balancer)
        string ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(ip))
        {
            // X-Forwarded-For có thể là danh sách IP, lấy IP đầu tiên
            ip = ip.Split(',')[0].Trim();
        }

        // 2️⃣ Nếu không có X-Forwarded-For, lấy RemoteIpAddress
        if (string.IsNullOrEmpty(ip))
        {
            ip = context.Connection.RemoteIpAddress?.ToString();
        }

        // 3️⃣ Localhost IPv6 (::1) hoặc IPv4 (127.0.0.1)
        if (ip == "::1") ip = "127.0.0.1";

        // 4️⃣ Nếu vẫn null, đặt "unknown"
        return ip ?? "unknown";
    }
}
