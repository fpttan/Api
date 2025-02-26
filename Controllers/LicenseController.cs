using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace LicenseServer.Controllers
{
    [Route("api/license")]
    [ApiController]
    public class LicenseController : ControllerBase
    {
        private readonly LicenseDbContext _context;
        private const string API_KEY = "your-secret-api-key"; // API Key của Admin

        private static readonly byte[] AES_KEY = Encoding.UTF8.GetBytes("ThisIsASecretKey1234567890123456");
        private static readonly byte[] AES_IV = Encoding.UTF8.GetBytes("ThisIsAnIV123456");

        public LicenseController(LicenseDbContext context)
        {
            _context = context;
        }

        // ✅ 1. Kiểm tra License (Không cần API Key)
        [HttpPost("validate")]
        public IActionResult ValidateLicense([FromBody] License license)
        {
            var foundLicense = _context.Licenses.FirstOrDefault(l => l.Key == license.Key);
            if (foundLicense == null)
                return NotFound(new { Message = Encrypt("License không hợp lệ") });

            return Ok(new
            {
                Message = Encrypt("License hợp lệ"),
                ExpiryDateDaily = Encrypt(foundLicense.ExpiryDateDaily),
                ExpiryDate200v = Encrypt(foundLicense.ExpiryDate200v)
            });
        }

        // ✅ 2. Thêm License (Cần API Key + Kiểm tra trùng Key)
        [HttpPost("add")]
        public IActionResult AddLicense([FromBody] License license, [FromHeader(Name = "X-Api-Key")] string apiKey)
        {
            if (!IsValidApiKey(apiKey))
                return Unauthorized(new { Message = Encrypt("API Key không hợp lệ") });

            if (_context.Licenses.Any(l => l.Key == license.Key))
                return BadRequest(new { Message = Encrypt("License đã tồn tại") });

            _context.Licenses.Add(license);
            _context.SaveChanges();

            return Ok(new { Message = Encrypt("License được thêm thành công") });
        }

        // ✅ 3. Xóa License (Cần API Key + Kiểm tra Key tồn tại)
        [HttpDelete("delete/{key}")]
        public IActionResult DeleteLicense(string key, [FromHeader(Name = "X-Api-Key")] string apiKey)
        {
            if (!IsValidApiKey(apiKey))
                return Unauthorized(new { Message = Encrypt("API Key không hợp lệ") });

            var license = _context.Licenses.FirstOrDefault(l => l.Key == key);
            if (license == null)
                return NotFound(new { Message = Encrypt("License không tồn tại") });

            _context.Licenses.Remove(license);
            _context.SaveChanges();

            return Ok(new { Message = Encrypt("License đã bị xóa") });
        }

        // ✅ 4. Cập nhật License (Cần API Key + Kiểm tra Key tồn tại)
        [HttpPut("update")]
        public IActionResult UpdateLicense([FromBody] License license, [FromHeader(Name = "X-Api-Key")] string apiKey)
        {
            if (!IsValidApiKey(apiKey))
                return Unauthorized(new { Message = Encrypt("API Key không hợp lệ") });

            var existingLicense = _context.Licenses.FirstOrDefault(l => l.Key == license.Key);
            if (existingLicense == null)
                return NotFound(new { Message = Encrypt("License không tồn tại") });

            existingLicense.Name = license.Name;
            existingLicense.ExpiryDateDaily = license.ExpiryDateDaily;
            existingLicense.ExpiryDate200v = license.ExpiryDate200v;

            _context.SaveChanges();
            return Ok(new { Message = Encrypt("License đã được cập nhật") });
        }

        // ✅ 5. Lấy tất cả License (Cần API Key)
        [HttpGet("all")]
        public IActionResult GetAllLicenses([FromHeader(Name = "X-Api-Key")] string apiKey)
        {
            if (!IsValidApiKey(apiKey))
                return Unauthorized(new { Message = Encrypt("API Key không hợp lệ") });

            var licenses = _context.Licenses.ToList();
            return Ok(licenses);
        }

        // ✅ 6. Lấy License đã hết hạn
        [HttpGet("expired")]
        public IActionResult GetExpiredLicenses([FromHeader(Name = "X-Api-Key")] string apiKey)
        {
            if (!IsValidApiKey(apiKey))
                return Unauthorized(new { Message = Encrypt("API Key không hợp lệ") });

            var expiredLicenses = _context.Licenses
                .Where(l => DateTime.Parse(l.ExpiryDateDaily) < DateTime.Now)
                .ToList();
            return Ok(expiredLicenses);
        }

        // ✅ 7. Lấy License gần hết hạn (trong 7 ngày)
        [HttpGet("nearexpired")]
        public IActionResult GetNearExpiredLicenses([FromHeader(Name = "X-Api-Key")] string apiKey)
        {
            if (!IsValidApiKey(apiKey))
                return Unauthorized(new { Message = Encrypt("API Key không hợp lệ") });

            var nearExpiredLicenses = _context.Licenses
                .Where(l => DateTime.Parse(l.ExpiryDateDaily) > DateTime.Now &&
                            DateTime.Parse(l.ExpiryDateDaily) <= DateTime.Now.AddDays(7))
                .ToList();
            return Ok(nearExpiredLicenses);
        }

        // ✅ 8. Lấy License còn hạn
        [HttpGet("valid")]
        public IActionResult GetValidLicenses([FromHeader(Name = "X-Api-Key")] string apiKey)
        {
            if (!IsValidApiKey(apiKey))
                return Unauthorized(new { Message = Encrypt("API Key không hợp lệ") });

            var validLicenses = _context.Licenses
                .Where(l => DateTime.Parse(l.ExpiryDateDaily) > DateTime.Now)
                .ToList();
            return Ok(validLicenses);
        }

        // 🔒 Xác thực API Key
        private bool IsValidApiKey(string apiKey)
        {
            return !string.IsNullOrEmpty(apiKey) && apiKey == API_KEY;
        }

        // 🔐 Mã hóa AES
        private string Encrypt(string plainText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = AES_KEY;
                aes.IV = AES_IV;
                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    byte[] inputBytes = Encoding.UTF8.GetBytes(plainText);
                    byte[] encryptedBytes = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
                    return Convert.ToBase64String(encryptedBytes);
                }
            }
        }
    }
}
