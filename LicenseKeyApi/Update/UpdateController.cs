using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;

[ApiController]
[Route("api/update/")]
public class UpdateController  : ControllerBase
{
    private readonly IConfiguration _cfg;
    [HttpGet("check")]
    public IActionResult Check([FromQuery] string version)
    {
        var sourceFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "sources");

        if (!Directory.Exists(sourceFolder))
            return BadRequest("Source folder not found");

        var files = Directory.GetFiles(sourceFolder, "VPT_*.rar");

        if (files.Length == 0)
            return Ok(new { latestVersion = version, isRequired = false });

        var versions = new List<(Version version, string filePath)>();

        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var parts = name.Split('_');
            if (parts.Length < 2) continue;

            if (Version.TryParse(parts[1], out Version parsed))
            {
                versions.Add((parsed, file));
            }
        }

        if (versions.Count == 0)
            return Ok(new { latestVersion = version, isRequired = false });

        var latest = versions.OrderByDescending(x => x.version).First();
        var latestVersion = latest.version.ToString();
        var latestFile = latest.filePath;

        // Nếu version client >= version mới nhất → không có update
        if (!string.IsNullOrEmpty(version) &&
            Version.TryParse(version, out Version clientVersion) &&
            clientVersion >= latest.version)
        {
            return Ok(new
            {
                latestVersion = version,
                isRequired = false
            });
        }

        // SHA256
        string sha256;
        using (var stream = System.IO.File.OpenRead(latestFile))
        using (var sha = SHA256.Create())
            sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");

        var baseUrl = "http://nhattan.online/";
        var downloadUrl = $"{baseUrl}/sources/{Path.GetFileName(latestFile)}";

        return Ok(new
        {
            latestVersion,
            downloadUrl,
            sha256,
            // ⭐ Luôn false — update tùy user
            isRequired = true,
            releaseNotes = NotificationService.releaseUpdateNotes
        });
    }
    
    [HttpGet("check_brazil")]
    public IActionResult CheckBrazil([FromQuery] string version)
    {
        var sourceFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "sources");

        if (!Directory.Exists(sourceFolder))
            return BadRequest("Source folder not found");

        var files = Directory.GetFiles(sourceFolder, "MCBot_*.rar");

        if (files.Length == 0)
            return Ok(new { latestVersion = version, isRequired = false });

        var versions = new List<(Version version, string filePath)>();

        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var parts = name.Split('_');
            if (parts.Length < 2) continue;

            if (Version.TryParse(parts[1], out Version parsed))
            {
                versions.Add((parsed, file));
            }
        }

        if (versions.Count == 0)
            return Ok(new { latestVersion = version, isRequired = false });

        var latest = versions.OrderByDescending(x => x.version).First();
        var latestVersion = latest.version.ToString();
        var latestFile = latest.filePath;

        // Nếu version client >= version mới nhất → không có update
        if (!string.IsNullOrEmpty(version) &&
            Version.TryParse(version, out Version clientVersion) &&
            clientVersion >= latest.version)
        {
            return Ok(new
            {
                latestVersion = version,
                isRequired = false
            });
        }

        // SHA256
        string sha256;
        using (var stream = System.IO.File.OpenRead(latestFile))
        using (var sha = SHA256.Create())
            sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");

        var baseUrl = "http://nhattan.online/";
        var downloadUrl = $"{baseUrl}/sources/{Path.GetFileName(latestFile)}";

        return Ok(new
        {
            latestVersion,
            downloadUrl,
            sha256,
            // ⭐ Luôn false — update tùy user
            isRequired = true,
            releaseNotes = NotificationService.releaseUpdateNotes
        });
    }
}