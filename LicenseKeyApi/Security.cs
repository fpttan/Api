using System.Text;
using System.Security.Cryptography;
public static class Security
{
    public static string EncryptData(string plainText, out string ivBase64)
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
                return Convert.ToBase64String(encryptedData);
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

