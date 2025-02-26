using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LicenseAPI.Data;
using LicenseAPI.Models;
using LicenseAPI.Services;
using System.Linq;
using System.Threading.Tasks;

namespace LicenseAPI.Controllers
{
    [Route("api/license")]
    [ApiController]
    public class LicenseController : ControllerBase
    {
        private readonly LicenseDbContext _context;

        public LicenseController(LicenseDbContext context)
        {
            _context = context;
        }

        [HttpPost("validate")]
        public async Task<IActionResult> ValidateLicense([FromBody] License request)
        {
            var license = await _context.Licenses.FirstOrDefaultAsync(l => l.Key == request.Key);
            if (license == null) return NotFound(new { Message = EncryptionService.Encrypt("License Not Found") });

            return Ok(new
            {
                Message = EncryptionService.Encrypt("License Found"),
                ExpiryDateDaily = EncryptionService.Encrypt(license.ExpiryDateDaily),
                ExpiryDate200v = EncryptionService.Encrypt(license.ExpiryDate200v)
            });
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddLicense([FromBody] License license)
        {
            _context.Licenses.Add(license);
            await _context.SaveChangesAsync();
            return Ok(new { Message = EncryptionService.Encrypt("License Added Successfully") });
        }

        [HttpDelete("delete/{key}")]
        public async Task<IActionResult> DeleteLicense(string key)
        {
            var license = await _context.Licenses.FirstOrDefaultAsync(l => l.Key == key);
            if (license == null) return NotFound(new { Message = EncryptionService.Encrypt("License Not Found") });

            _context.Licenses.Remove(license);
            await _context.SaveChangesAsync();
            return Ok(new { Message = EncryptionService.Encrypt("License Deleted Successfully") });
        }
    }
}
