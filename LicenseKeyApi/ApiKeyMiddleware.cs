using dotenv.net;
public static class ApiKeyMiddleware
{
    public static readonly string AdminApiKey;
    public static readonly string AESKey;

    // Static constructor để khởi tạo giá trị từ biến môi trường
    static ApiKeyMiddleware()
    {
        DotEnv.Load();
        AdminApiKey = Environment.GetEnvironmentVariable("ADMIN_API_KEY") ?? "default-admin-key";
        AESKey = Environment.GetEnvironmentVariable("AES_KEY") ?? "default-aes-key";
    }
}
