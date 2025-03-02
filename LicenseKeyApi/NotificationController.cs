using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

[Route("api/notifications")]
[ApiController]
public class NotificationController : ControllerBase
{
    private readonly NotificationService _notificationService;

    public NotificationController(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    // ✅ Lấy thông báo VN (đã mã hóa)
[HttpGet("vn")]
public IActionResult GetNotificationVN()
{
    return Ok(EncryptResponse(_notificationService.GetNotificationVN()));
}

// ✅ Lấy thông báo Brazil (đã mã hóa)
[HttpGet("bra")]
public IActionResult GetNotificationBRA()
{
    return Ok(EncryptResponse(_notificationService.GetNotificationBRA()));
}

// ✅ Lấy link tải xuống VN (đã mã hóa)
[HttpGet("linkdown/vn")]
public IActionResult GetLinkdownVN()
{
    return Ok(EncryptResponse(_notificationService.GetLinkdownVN()));
}

// ✅ Lấy link tải xuống Brazil (đã mã hóa)
[HttpGet("linkdown/bra")]
public IActionResult GetLinkdownBRA()
{
    return Ok(EncryptResponse(_notificationService.GetLinkdownBRA()));
}


    // ✅ API cập nhật thông báo tiếng Việt (chỉ admin)
    [HttpPost("vn")]
    public IActionResult UpdateNotificationVN([FromHeader(Name = "X-API-KEY")] string apiKey, [FromBody] string message)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey) return Forbid();
        _notificationService.UpdateNotificationVN(message);
        return Ok("Thông báo VN đã cập nhật.");
    }

    // ✅ API cập nhật thông báo tiếng Brazil (chỉ admin)
    [HttpPost("bra")]
    public IActionResult UpdateNotificationBRA([FromHeader(Name = "X-API-KEY")] string apiKey, [FromBody] string message)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey) return Forbid();
        _notificationService.UpdateNotificationBRA(message);
        return Ok("Notificação do Brasil atualizada.");
    }

    // ✅ API cập nhật link tải xuống VN (chỉ admin)
    [HttpPost("linkdown_vn")]
    public IActionResult UpdateLinkdownVN([FromHeader(Name = "X-API-KEY")] string apiKey, [FromBody] string link)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey) return Forbid();
        _notificationService.UpdateLinkdownVN(link);
        return Ok("Link tải xuống VN đã cập nhật.");
    }

    // ✅ API cập nhật link tải xuống Brazil (chỉ admin)
    [HttpPost("linkdown_bra")]
    public IActionResult UpdateLinkdownBRA([FromHeader(Name = "X-API-KEY")] string apiKey, [FromBody] string link)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey) return Forbid();
        _notificationService.UpdateLinkdownBRA(link);
        return Ok("Link de download do Brasil atualizado.");
    }

    // ✅ Hàm mã hóa AES-256
    private object EncryptResponse(string plainText)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes("12345678901234567890123456789012"); // 32 bytes key (AES-256)
            aes.GenerateIV(); // Sinh IV ngẫu nhiên
            string ivBase64 = Convert.ToBase64String(aes.IV); // Lưu IV dưới dạng Base64

            using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
            {
                byte[] inputBuffer = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedData = encryptor.TransformFinalBlock(inputBuffer, 0, inputBuffer.Length);
                string encryptedBase64 = Convert.ToBase64String(encryptedData);
                return new { data = encryptedBase64, iv = ivBase64 };
            }
        }
    }
}


