using System.Security.Cryptography;
using System.Text;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>Compares local-service secrets without content-dependent timing.</summary>
internal static class SecretText
{
    public static bool FixedEquals(
        string? supplied,
        string expected)
    {
        if (supplied is null)
        {
            return false;
        }

        byte[] left = Encoding.UTF8.GetBytes(supplied);
        byte[] right = Encoding.UTF8.GetBytes(expected);
        try
        {
            return left.Length == right.Length
                && CryptographicOperations.FixedTimeEquals(
                    left,
                    right);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(left);
            CryptographicOperations.ZeroMemory(right);
        }
    }
}
