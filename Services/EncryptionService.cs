using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LicenseAPI.Services
{
    public class EncryptionService
    {
        private static readonly byte[] AES_KEY = Encoding.UTF8.GetBytes("ThisIsASecretKey1234567890123456"); // 32 bytes
        private static readonly byte[] AES_IV = Encoding.UTF8.GetBytes("ThisIsAnIV123456"); // 16 bytes

        public static string Encrypt(string plainText)
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

        public static string Decrypt(string encryptedText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = AES_KEY;
                aes.IV = AES_IV;
                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                {
                    byte[] cipherBytes = Convert.FromBase64String(encryptedText);
                    byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                    return Encoding.UTF8.GetString(plainBytes);
                }
            }
        }
    }
}
