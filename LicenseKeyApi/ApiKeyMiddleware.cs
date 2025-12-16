using dotenv.net;
public static class ApiKeyMiddleware
{
    public static readonly string AdminApiKey;
    public static readonly string AESKey;
    public static readonly string TG_BOT_TOKEN;
    public static readonly string BOT_WEBHOOK_URL;
    public static readonly string BOT_WEBHOOK_SECRET;
    public static readonly string ADMIN_TELEGRAM_IDS;

    // Static constructor để khởi tạo giá trị từ biến môi trường
    static ApiKeyMiddleware()
    {
        DotEnv.Load();
        AdminApiKey = Environment.GetEnvironmentVariable("ADMIN_API_KEY") ?? "default-admin-key";
        AESKey = Environment.GetEnvironmentVariable("AES_KEY") ?? "default-aes-key";
        TG_BOT_TOKEN = Environment.GetEnvironmentVariable("TG_BOT_TOKEN") ?? "";
        BOT_WEBHOOK_URL = Environment.GetEnvironmentVariable("BOT_WEBHOOK_URL") ?? "";
        BOT_WEBHOOK_SECRET = Environment.GetEnvironmentVariable("BOT_WEBHOOK_SECRET") ?? "default";
        ADMIN_TELEGRAM_IDS = Environment.GetEnvironmentVariable("ADMIN_TELEGRAM_IDS") ?? "";
    }
}
