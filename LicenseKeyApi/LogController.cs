using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

[ApiController]
[Route("api/")]
public class LogController : ControllerBase
{
    private readonly string logFolder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Logs"));

    // Hash license để không lộ dữ liệu nhạy cảm
    private string HashLicense(string license)
    {
        if (string.IsNullOrEmpty(license)) return "";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(license));
        return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 16); // 16 ký tự đầu
    }

    [HttpGet("logs")]
    public IActionResult GetLogs(
     [FromHeader(Name = "X-API-KEY")] string apiKey,
     [FromQuery] string startDate,
     [FromQuery] string endDate,
     [FromQuery] int lines = 500,
     [FromQuery] string message = null,
     [FromQuery] string level = null,
     [FromQuery] string ip = null,
     [FromQuery] string user = null)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey)
            return Forbid();

        if (!DateTime.TryParse(startDate, out var start))
            start = DateTime.UtcNow;

        if (!DateTime.TryParse(endDate, out var end))
            end = DateTime.UtcNow;

        var allLines = new List<string>();

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var logFile = Path.Combine(logFolder, $"{date:yyyy-MM-dd}.json");
            if (System.IO.File.Exists(logFile))
            {
                allLines.AddRange(System.IO.File.ReadLines(logFile, Encoding.UTF8));
            }
        }

        var logObjects = new List<object>();

        foreach (var line in allLines)
        {
            try
            {
                var log = JsonSerializer.Deserialize<Dictionary<string, object>>(line);
                if (log == null) continue;

                string logLevel = log.TryGetValue("Level", out var lv) ? lv?.ToString() : null;
                object dataObj = log.TryGetValue("Data", out var d) ? d : null;

                string logIp = GetStringFromData(dataObj, "ip");
                string logUser = GetStringFromData(dataObj, "name");
                string logLicense = GetStringFromData(dataObj, "license");

                // ✅ Mask license
                if (!string.IsNullOrEmpty(logLicense))
                {
                    var dict = new Dictionary<string, object>
                    {
                        ["name"] = logUser,
                        ["ip"] = logIp,
                        ["license"] = logLicense,
                        ["version"] = GetStringFromData(dataObj, "version"),
                        ["sha"] = GetStringFromData(dataObj, "sha"),

                    };
                    log["Data"] = dict;
                }

                // ✅ FILTER MESSAGE
                if (!string.IsNullOrEmpty(message))
                {
                    if (log.TryGetValue("Message", out var msgObj))
                    {
                        var msgStr = msgObj?.ToString() ?? "";
                        if (!msgStr.Contains(message, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                    else
                    {
                        continue;
                    }
                }

                // ✅ FILTER LEVEL
                if (!string.IsNullOrEmpty(level))
                {
                    if (string.IsNullOrEmpty(logLevel) ||
                        !string.Equals(logLevel, level, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                // ✅ FILTER IP
                if (!string.IsNullOrEmpty(ip))
                {
                    if (string.IsNullOrEmpty(logIp) ||
                        !string.Equals(logIp, ip, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                // ✅ FILTER USER (hỗ trợ tiếng Việt có dấu & không dấu)
                if (!string.IsNullOrEmpty(user))
                {
                    if (string.IsNullOrEmpty(logUser))
                        continue;

                    var u1 = RemoveDiacritics(user).ToLowerInvariant();
                    var u2 = RemoveDiacritics(logUser).ToLowerInvariant();

                    if (!u2.Contains(u1))
                        continue;
                }

                logObjects.Add(log);
            }
            catch
            {
                // Bỏ dòng lỗi
            }
        }

        var result = logObjects.TakeLast(lines);
        return Ok(result);
    }

    string GetStringFromData(object data, string key)
    {
        if (data == null) return null;

        if (data is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            if (je.TryGetProperty(key, out var prop))
                return prop.ToString();
            return null;
        }

        if (data is Dictionary<string, object> dict)
        {
            if (dict.TryGetValue(key, out var v))
                return v?.ToString();
            return null;
        }

        return null;
    }

    string RemoveDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
            if (Char.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }



}
