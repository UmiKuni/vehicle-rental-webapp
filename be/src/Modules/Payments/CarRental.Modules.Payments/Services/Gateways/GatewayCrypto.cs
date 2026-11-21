using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using CarRental.Modules.Payments.Contracts.Dtos;

namespace CarRental.Modules.Payments.Services.Gateways;

internal static class GatewayCrypto
{
    public static string ComputeHmacSha512Hex(string secret, string payload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA512.HashData(keyBytes, payloadBytes);
        return Convert.ToHexStringLower(hash);
    }

    public static string ComputeHmacSha256Hex(string secret, string payload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return Convert.ToHexStringLower(hash);
    }

    public static bool FixedTimeEquals(string? left, string? right)
    {
        if (left is null || right is null)
            return false;

        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    public static bool FixedTimeEqualsHex(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        return FixedTimeEquals(
            left.Trim().ToLowerInvariant(),
            right.Trim().ToLowerInvariant());
    }

    public static string BuildUnifiedWebhookCanonicalPayload(PaymentWebhookRequest request) =>
        FormattableString.Invariant(
            $"{request.TransactionId?.Trim()}|{request.OrderId?.Trim()}|{request.Amount:0.##}|{request.ResponseCode?.Trim()}");

    public static string SanitizeAscii(string? input, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(input) || maxLength <= 0)
            return string.Empty;

        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        var previousSpace = false;

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            var mapped = ch switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                >= ' ' and <= '~' => ch,
                _ => ' '
            };

            if (mapped == ' ')
            {
                if (!previousSpace && sb.Length > 0)
                {
                    sb.Append(' ');
                    previousSpace = true;
                }
            }
            else
            {
                sb.Append(mapped);
                previousSpace = false;
            }

            if (sb.Length >= maxLength)
                break;
        }

        return sb.ToString().Trim();
    }

    public static string NormalizeClientIp(string? rawIp, string fallback = "127.0.0.1")
    {
        var safeFallback = string.IsNullOrWhiteSpace(fallback) ? "127.0.0.1" : fallback.Trim();
        if (string.IsNullOrWhiteSpace(rawIp))
            return safeFallback;

        var candidate = rawIp.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(candidate) || !IPAddress.TryParse(candidate, out var ip))
            return safeFallback;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
            return ip.ToString();

        if (ip.IsIPv4MappedToIPv6)
            return ip.MapToIPv4().ToString();

        return safeFallback;
    }

    public static bool TryGetCaseInsensitiveValue(
        IReadOnlyDictionary<string, string>? dictionary,
        string key,
        out string value)
    {
        if (dictionary is not null)
        {
            if (dictionary.TryGetValue(key, out var exact) && !string.IsNullOrWhiteSpace(exact))
            {
                value = exact;
                return true;
            }

            foreach (var pair in dictionary)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(pair.Value))
                {
                    value = pair.Value;
                    return true;
                }
            }
        }

        value = string.Empty;
        return false;
    }
}
