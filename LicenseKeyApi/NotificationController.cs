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
            string rawresult = _notificationService.GetNotificationVN();
            return Ok(EncryptData(rawresult,out string ivBase64));
    }

    // ✅ Lấy thông báo Brazil (đã mã hóa)
    [HttpGet("bra")]
    public IActionResult GetNotificationBRA()
    {
            string rawresult = _notificationService.GetNotificationBRA();
            return Ok(EncryptData(rawresult, out string ivBase64));
    }

    // ✅ Lấy link tải xuống VN (đã mã hóa)
    [HttpGet("linkdown/vn")]
    public IActionResult GetLinkdownVN()
    {
            string rawresult = _notificationService.GetLinkdownVN();
            return Ok(EncryptData(rawresult, out string ivBase64));
    }

    // ✅ Lấy link tải xuống Brazil (đã mã hóa)
    [HttpGet("linkdown/bra")]
    public IActionResult GetLinkdownBRA()
    {
            string rawresult = _notificationService.GetLinkdownBRA();
            return Ok(EncryptData(rawresult, out string ivBase64));
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
    private static object EncryptData(string plainText, out string ivBase64)
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

public class NotificationService
{
    public string NotificationVN { get; private set; } = "Thông báo mặc định VN";
    public string NotificationBRA { get; private set; } = "Mensagem padrão do Brasil";
    public string LinkdownVN { get; private set; } = "https://example.com/vn";
    public string LinkdownBRA { get; private set; } = "https://example.com/bra";

    // ✅ GET methods
    public string GetNotificationVN() => NotificationVN;
    public string GetNotificationBRA() => NotificationBRA;
    public string GetLinkdownVN() => LinkdownVN;
    public string GetLinkdownBRA() => LinkdownBRA;

    // ✅ UPDATE methods
    public void UpdateNotificationVN(string newMessage) => NotificationVN = newMessage;
    public void UpdateNotificationBRA(string newMessage) => NotificationBRA = newMessage;
    public void UpdateLinkdownVN(string newLink) => LinkdownVN = newLink;
    public void UpdateLinkdownBRA(string newLink) => LinkdownBRA = newLink;
}
