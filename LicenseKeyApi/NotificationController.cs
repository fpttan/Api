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

    // ✅ Lấy DomainVN (đã mã hóa)
    [HttpGet("vn_domain")]
    public IActionResult GetVietNamDomain()
    {
        return Ok(EncryptResponse(_notificationService.GetVietNamDomain()));
    }
    [HttpGet("bra_domain")]
    public IActionResult GetBrazilDomain()
    {
        return Ok(EncryptResponse(_notificationService.GetBrazilDomain()));
    }
    [HttpGet("neo_domain")]
    public IActionResult GetNeoDomain()
    {
        return Ok(EncryptResponse(_notificationService.GetNeoDomain()));
    }

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
    // ✅ Lấy ghi chú cập nhật (đã mã hóa)
    [HttpGet("release_notes")]
    public IActionResult GetReleaseUpdateNotes()    
    {
        return Ok(EncryptResponse(_notificationService.GetReleaseUpdateNotes()));
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
    // ✅ API cập nhật domain VN (chỉ admin)
    [HttpPost("domain_vn")]
    public IActionResult UpdateDomainVN([FromHeader(Name = "X-API-KEY")] string apiKey, [FromBody] string domain)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey) return Forbid();
        _notificationService.UpdateDomainVN(domain);
        return Ok("Domain VN đã cập nhật.");
    }
    // ✅ API cập nhật domain Brazil (chỉ admin)
    [HttpPost("domain_bra")]
    public IActionResult UpdateDomainBrazil([FromHeader(Name = "X-API-KEY")] string apiKey, [FromBody] string domain)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey) return Forbid();
        _notificationService.UpdateDomainBrazil(domain);
        return Ok("Domain Brazil đã cập nhật.");
    }
    // ✅ API cập nhật domain Neo (chỉ admin)
    [HttpPost("domain_neo")]
    public IActionResult UpdateDomainNeo([FromHeader(Name = "X-API-KEY")] string apiKey, [FromBody] string domain)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey) return Forbid();
        _notificationService.UpdateDomainNeo(domain);       
        return Ok("Domain Neo đã cập nhật.");
    }
    // ✅ API cập nhật ghi chú cập nhật (chỉ admin)
    [HttpPost("release_notes")]
    public IActionResult UpdateReleaseUpdateNotes([FromHeader(Name = "X-API-KEY")] string apiKey, [FromBody] string notes)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey) return Forbid();
        _notificationService.UpdateReleaseUpdateNotes(notes);
        return Ok("Ghi chú cập nhật đã được cập nhật.");
    }
    // ✅ Hàm mã hóa AES-256
    private object EncryptResponse(string plainText)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = ConvertHexStringToByteArray(ApiKeyMiddleware.AESKey);
            aes.GenerateIV(); // Sinh IV ngẫu nhiên
            string ivBase64 = Convert.ToBase64String(aes.IV); // Lưu IV dưới dạng Base64

            using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
            {
                byte[] inputBuffer = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedData = encryptor.TransformFinalBlock(inputBuffer, 0, inputBuffer.Length);
                string encryptedBase64 = Convert.ToBase64String(encryptedData);
                return new { data = encryptedBase64, data2 = ivBase64 };
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
}


