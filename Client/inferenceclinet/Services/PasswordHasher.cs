using System.Security.Cryptography;
using System.Text;

namespace inferenceclinet.Services;

// salt 없이 SHA-256 해시. 결과는 64자리 16진 소문자 문자열 -> Login.HashPassword(VARCHAR(64))와 길이 일치.
public static class PasswordHasher
{
    public static string Hash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
