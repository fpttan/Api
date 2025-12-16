using System.Text.Json;

public class AppLogger : IAppLogger
{
    private readonly string _folder;
    private readonly JsonSerializerOptions _jsonOptions;

    public AppLogger(IWebHostEnvironment env)
    {
        _folder = Path.Combine(env.ContentRootPath, "Logs");
        Directory.CreateDirectory(_folder);

        _jsonOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };
    }

    private void Write(string level, string message, object data = null, Exception ex = null)
    {
        var log = new
        {
            Level = level,
            Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Message = message,
            Data = data,
            Exception = ex?.ToString()
        };

        string json = JsonSerializer.Serialize(log, _jsonOptions);

        string file = Path.Combine(_folder, $"{DateTime.Now:yyyy-MM-dd}.json");
        File.AppendAllText(file, json + Environment.NewLine);
    }

    public void Info(string message, object data = null)
        => Write("INFO", message, data);

    public void Warn(string message, object data = null)
        => Write("WARN", message, data);

    public void Error(string message, Exception ex = null, object data = null)
        => Write("ERROR", message, data, ex);
}

public interface IAppLogger
{
    void Info(string message, object data = null);
    void Warn(string message, object data = null);
    void Error(string message, Exception ex = null, object data = null);
}

