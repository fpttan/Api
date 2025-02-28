using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

[Route("api/[controller]")]
[ApiController]
public class SecureController : ControllerBase
{
    private static readonly string Key = "ThisIsASecretKey123456"; // 32 bytes cho AES-256
    private static readonly string IV = "ThisIsAnIV123456"; // 16 bytes cho IV

    [HttpGet("encrypt")]
    public IActionResult GetEncryptedData()
    {
        var data = new
        {
            Name = "John Doe",
            Age = 30,
            Message = "Hello from Server"
        };

        string jsonData = JsonSerializer.Serialize(data);
        string encryptedData = EncryptData(jsonData);
        
        return Ok(new { Encrypted = encryptedData });
    }

    private static string EncryptData(string plainText)
    {
        using Aes aesAlg = Aes.Create();
        aesAlg.Key = Encoding.UTF8.GetBytes(Key);
        aesAlg.IV = Encoding.UTF8.GetBytes(IV);
        aesAlg.Mode = CipherMode.CBC;
        aesAlg.Padding = PaddingMode.PKCS7;

        byte[] encryptedBytes;
        using (ICryptoTransform encryptor = aesAlg.CreateEncryptor())
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        }

        return Convert.ToBase64String(encryptedBytes);
    }
}
