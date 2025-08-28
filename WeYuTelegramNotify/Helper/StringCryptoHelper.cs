using System;
using System.Text;

namespace WeYuTelegramNotify.Helper;

public static class StringCryptoHelper
{
    // 固定一個簡單 Key 做 XOR（這樣比純 Base64 多一道）
    private const byte XorKey = 0x5A; // 任意一個 byte 值，例如 0x5A

    /// <summary>
    /// 明文 → 混淆後字串 (Base64 + XOR)
    /// </summary>
    public static string SimpleEncrypt(this string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        var bytes = Encoding.UTF8.GetBytes(plaintext);

        // 每個 byte 做 XOR
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] ^= XorKey;

        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// 混淆字串 (Base64 + XOR) → 明文
    /// </summary>
    public static string SimpleDecrypt(this string cipher)
    {
        if (string.IsNullOrEmpty(cipher)) return string.Empty;
        var bytes = Convert.FromBase64String(cipher);

        for (int i = 0; i < bytes.Length; i++)
            bytes[i] ^= XorKey;

        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// 驗證是否等於預期字串
    /// </summary>
    public static bool MatchesSecret(this string cipher, string expectedPlain)
    {
        try
        {
            var plain = cipher.SimpleDecrypt().Trim();
            return plain == expectedPlain;
        }
        catch
        {
            return false;
        }
    }
}