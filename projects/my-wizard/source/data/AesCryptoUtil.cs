using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class AesCryptoUtil
{
    static readonly string Password = "<REDACTED_CRYPTO_PASSWORD>";

    public static byte[] EncryptStringToBytes(string plainText)
    {
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        return EncryptBytes(plainBytes, Password);
    }

    public static string DecryptBytesToString(byte[] encryptedBytes)
    {
        byte[] plain = DecryptBytes(encryptedBytes, Password);
        return Encoding.UTF8.GetString(plain);
    }

    public static byte[] EncryptBytes(byte[] plain, string password, int iterations = 120000)
    {
        byte[] salt = RandomBytes(16);
        byte[] iv = RandomBytes(16);

        using var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        byte[] key = kdf.GetBytes(32);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var ms = new MemoryStream();
        ms.Write(Encoding.ASCII.GetBytes("AES1"), 0, 4);
        ms.Write(salt, 0, salt.Length);
        ms.Write(iv, 0, iv.Length);

        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            cs.Write(plain, 0, plain.Length);
            cs.FlushFinalBlock();
        }

        return ms.ToArray();
    }

    public static byte[] DecryptBytes(byte[] encrypted, string password, int iterations = 120000)
    {
        using var ms = new MemoryStream(encrypted);

        byte[] magic = new byte[4];
        if (ms.Read(magic, 0, 4) != 4) throw new Exception("Invalid data");
        if (Encoding.ASCII.GetString(magic) != "AES1") throw new Exception("Invalid header");

        byte[] salt = new byte[16];
        byte[] iv = new byte[16];
        if (ms.Read(salt, 0, 16) != 16) throw new Exception("Invalid data");
        if (ms.Read(iv, 0, 16) != 16) throw new Exception("Invalid data");

        using var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        byte[] key = kdf.GetBytes(32);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var outMs = new MemoryStream();
        using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
        {
            cs.CopyTo(outMs);
        }
        return outMs.ToArray();
    }

    static byte[] RandomBytes(int len)
    {
        byte[] b = new byte[len];
        RandomNumberGenerator.Fill(b);
        return b;
    }
}
