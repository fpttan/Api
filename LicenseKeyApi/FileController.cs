using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;

[Route("api/files")]
[ApiController]
public class FileController : ControllerBase
{
    private readonly ApiDbContext _context;

    // Thư mục chứa các file dữ liệu trên server – nên đưa vào appsettings về sau
    public static readonly string DataRoot  = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "GameData"));
    // Thư mục chứa các file dữ liệu trên server – nên đưa vào appsettings về sau
    public static readonly string ImageDataRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Images"));

    // Chỉ cho phép các loại file này
    private static readonly HashSet<string> AllowedExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".json"
    };

    public FileController(ApiDbContext context)
    {
        _context = context;
    }

    // ============ CLIENT (cần LicenseKey hợp lệ) ============

    // ✅ Liệt kê file cho client (tối giản thông tin)
    [HttpGet("list")]
    public IActionResult ListForClient()
    {
        //if (!IsValidLicense(key, out _)) return NotFound("License không tồn tại.");

        var files = Directory.EnumerateFiles(DataRoot, "*", SearchOption.TopDirectoryOnly)
            .Where(p => AllowedExt.Contains(Path.GetExtension(p)))
            .Select(p => Path.GetFileName(p))
            .OrderBy(n => n)
            .ToList();

        return Ok(new { files });
    }

    // ✅ Trả về NỘI DUNG file TEXT/JSON (đã mã hóa AES + trả IV)
    [HttpGet("get")]
    public IActionResult GetFileContent([FromQuery] string key, [FromQuery] string name)
    {
        if (!IsValidLicense(key, out _)) return NotFound("License không tồn tại.");

        var license = _context.Licenses.Find(key);
        if (license == null) return NotFound();

        var now = DateTime.UtcNow;
        bool expiredDaily = license.TimeExpireDaily.HasValue && license.TimeExpireDaily.Value <= now;
        bool expired200v = license.TimeExpire200v.HasValue && license.TimeExpire200v.Value <= now;
        if (expiredDaily && expired200v)
            return StatusCode(StatusCodes.Status403Forbidden, "License đã hết hạn sử dụng");

        if (!TryResolveSafePath(name, out var fullPath, out var err)) return BadRequest(err);
        if (!System.IO.File.Exists(fullPath)) return NotFound("File không tồn tại.");

        // Đọc nội dung text (UTF-8)
        string content = System.IO.File.ReadAllText(fullPath, Encoding.UTF8);

        // Mã hóa AES tương tự LicenseController
        string cipher = EncryptData(content, out string ivBase64);

        return Ok(new { data = cipher, data2 = ivBase64 });
    }

    [HttpGet("valuetele")]
    public IActionResult GetDataTele([FromQuery] string key, [FromServices] IAppLogger logger)
    {
        if (string.IsNullOrEmpty(key))
        {
            logger.Warn("GetDataTele called without LicenseKey");
            return BadRequest("Vui lòng cung cấp LicenseKey.");
        }
        var license = _context.Licenses.Find(key);
        if (license == null) return NotFound();
        var now = DateTime.UtcNow;
        bool expiredDaily = license.TimeExpireDaily.HasValue && license.TimeExpireDaily.Value <= now;
        bool expired200v = license.TimeExpire200v.HasValue && license.TimeExpire200v.Value <= now;
        if (expiredDaily && expired200v)
            return StatusCode(StatusCodes.Status403Forbidden, "License đã hết hạn sử dụng");

        var Data = 12884901889;
        // Mã hóa AES tương tự LicenseController
        string cipher = EncryptData(Data.ToString(), out string ivBase64);
        
        return Ok(new { data = cipher, data2 = ivBase64 });

    }
  
    [HttpGet("image")]
    public IActionResult GetImage([FromQuery] string key, [FromQuery] string path, [FromServices] IAppLogger logger)
    {
        if (!IsValidLicense(key, out _)) 
        {
            logger.Warn("GetImage called with invalid LicenseKey: " + key);
            return BadRequest("LicenseKey không hợp lệ.");
        } 

        var license = _context.Licenses.Find(key);
        if (license == null) return NotFound();

        var now = DateTime.UtcNow;
        bool expiredDaily = license.TimeExpireDaily.HasValue && license.TimeExpireDaily.Value <= now;
        bool expired200v = license.TimeExpire200v.HasValue && license.TimeExpire200v.Value <= now;
        if (expiredDaily && expired200v)
            return StatusCode(StatusCodes.Status403Forbidden, "License đã hết hạn sử dụng");

        if (string.IsNullOrWhiteSpace(path))
            return BadRequest(new { error = "Path is required" });

        // Chuẩn hóa path
        path = path.Replace("\\", "/");

        string fullPath = Path.Combine(ImageDataRoot, path);

        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { error = "Image not found", file = path });

        byte[] fileBytes = System.IO.File.ReadAllBytes(fullPath);
        return File(fileBytes, "image/png");
    }
    // ============ QUẢN TRỊ (cần X-API-KEY) ============

    // ✅ Liệt kê chi tiết cho admin
    [HttpGet("admin/list")]
    public IActionResult ListForAdmin([FromHeader(Name = "X-API-KEY")] string apiKey)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey) return Forbid();

        var files = Directory.EnumerateFiles(DataRoot, "*", SearchOption.TopDirectoryOnly)
            .Where(p => AllowedExt.Contains(Path.GetExtension(p)))
            .Select(p => new
            {
                Name = Path.GetFileName(p),
                Type = Path.GetExtension(p),
                Size = new FileInfo(p).Length,
                DateModified = System.IO.File.GetLastWriteTime(p)
            })
            .OrderBy(f => f.Name)
            .ToList();

        return Ok(files);
    }

    // ✅ Tải file nhị phân (admin)
    [HttpGet("admin/download")]
    public IActionResult Download([FromHeader(Name = "X-API-KEY")] string apiKey, [FromQuery] string name)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey) return Forbid();
        if (!TryResolveSafePath(name, out var fullPath, out var err)) return BadRequest(err);
        if (!System.IO.File.Exists(fullPath)) return NotFound();

        var bytes = System.IO.File.ReadAllBytes(fullPath);
        return File(bytes, "application/octet-stream", Path.GetFileName(fullPath));
        
    }

    // ============ Helpers ============

    private bool IsValidLicense(string? key, out License? license)
    {
        license = null;
        if (string.IsNullOrWhiteSpace(key)) return false;
        license = _context.Licenses.Find(key);
        return license != null;
    }

    private static bool TryResolveSafePath(string? name, out string fullPath, out string error)
    {
        fullPath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Thiếu tham số 'name'.";
            return false;
        }

        // Cấm đường dẫn chứa thư mục hoặc traversal
        if (name.Contains("..") || name.Contains(Path.DirectorySeparatorChar) || name.Contains(Path.AltDirectorySeparatorChar))
        {
            error = "Tên file không hợp lệ.";
            return false;
        }

        // Kiểm tra phần mở rộng
        var ext = Path.GetExtension(name);
        if (!AllowedExt.Contains(ext))
        {
            error = $"Phần mở rộng không được phép: {ext}";
            return false;
        }

        fullPath = Path.Combine(DataRoot, name);
        // Bảo đảm vẫn ở trong DataRoot
        var normalized = Path.GetFullPath(fullPath);
        if (!normalized.StartsWith(Path.GetFullPath(DataRoot), StringComparison.OrdinalIgnoreCase))
        {
            error = "Đường dẫn không hợp lệ.";
            return false;
        }
        return true;
    }

    private static string EncryptData(string plainText, out string ivBase64)
    {
        using var aes = Aes.Create();
        aes.Key = ConvertHexStringToByteArray(ApiKeyMiddleware.AESKey);
        aes.GenerateIV();
        ivBase64 = Convert.ToBase64String(aes.IV);

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        byte[] inputBuffer = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptedData = encryptor.TransformFinalBlock(inputBuffer, 0, inputBuffer.Length);
        return Convert.ToBase64String(encryptedData);
    }

    private static byte[] ConvertHexStringToByteArray(string hex)
    {
        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }
}