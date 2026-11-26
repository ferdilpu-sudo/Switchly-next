using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Switchly.LicenseApi.Options;

namespace Switchly.LicenseApi.Crypto;

public sealed record LicenseTokenPayload(
    [property: JsonPropertyName("v")] int Version,
    [property: JsonPropertyName("sub")] string AccountId,
    [property: JsonPropertyName("tier")] string Tier,
    [property: JsonPropertyName("max_profiles")] int? MaxProfiles,
    [property: JsonPropertyName("device_hash")] string DeviceHash,
    [property: JsonPropertyName("issued_at")] DateTimeOffset IssuedAtUtc,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAtUtc,
    [property: JsonPropertyName("updates_until")] DateTimeOffset? UpdatesUntilUtc,
    [property: JsonPropertyName("refresh_after")] DateTimeOffset RefreshAfterUtc,
    [property: JsonPropertyName("offline_grace_until")] DateTimeOffset OfflineGraceUntilUtc);

public interface IEd25519TokenService
{
    string PublicKeyBase64 { get; }
    string Sign(LicenseTokenPayload payload);
    bool TryVerify(string signedToken, out LicenseTokenPayload? payload);
}

public sealed class Ed25519TokenService : IEd25519TokenService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly Ed25519PrivateKeyParameters _privateKey;
    private readonly Ed25519PublicKeyParameters _publicKey;

    public Ed25519TokenService(IOptions<LicenseApiOptions> options)
        : this(options.Value.Ed25519PrivateKeyBase64)
    {
    }

    public Ed25519TokenService(string privateKeyBase64)
    {
        if (string.IsNullOrWhiteSpace(privateKeyBase64))
        {
            throw new InvalidOperationException(
                "LicenseApi:Ed25519PrivateKeyBase64 must be configured with a 32-byte Base64 Ed25519 seed.");
        }

        var seed = Convert.FromBase64String(privateKeyBase64.Trim());
        if (seed.Length != 32)
        {
            throw new InvalidOperationException("Ed25519 private key seed must be exactly 32 bytes.");
        }

        _privateKey = new Ed25519PrivateKeyParameters(seed, 0);
        _publicKey = _privateKey.GeneratePublicKey();
        PublicKeyBase64 = Convert.ToBase64String(_publicKey.GetEncoded());
    }

    public string PublicKeyBase64 { get; }

    public static (string PrivateKeyBase64, string PublicKeyBase64) GenerateKeyPair()
    {
        var seed = RandomNumberGenerator.GetBytes(32);
        var priv = new Ed25519PrivateKeyParameters(seed, 0);
        var pub = priv.GeneratePublicKey();
        return (Convert.ToBase64String(seed), Convert.ToBase64String(pub.GetEncoded()));
    }

    public string Sign(LicenseTokenPayload payload)
    {
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var payloadPart = Base64UrlEncode(jsonBytes);
        var payloadPartBytes = Encoding.ASCII.GetBytes(payloadPart);

        var signer = new Ed25519Signer();
        signer.Init(true, _privateKey);
        signer.BlockUpdate(payloadPartBytes, 0, payloadPartBytes.Length);
        var signature = signer.GenerateSignature();

        return $"{payloadPart}.{Base64UrlEncode(signature)}";
    }

    public bool TryVerify(string signedToken, out LicenseTokenPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(signedToken))
        {
            return false;
        }

        var dotIndex = signedToken.IndexOf('.');
        if (dotIndex <= 0 || dotIndex != signedToken.LastIndexOf('.'))
        {
            return false;
        }

        var payloadPart = signedToken[..dotIndex];
        var sigPart = signedToken[(dotIndex + 1)..];
        if (!TryBase64UrlDecode(sigPart, out var signature) || signature.Length != 64)
        {
            return false;
        }

        var payloadPartBytes = Encoding.ASCII.GetBytes(payloadPart);
        var verifier = new Ed25519Signer();
        verifier.Init(false, _publicKey);
        verifier.BlockUpdate(payloadPartBytes, 0, payloadPartBytes.Length);
        if (!verifier.VerifySignature(signature))
        {
            return false;
        }

        if (!TryBase64UrlDecode(payloadPart, out var jsonBytes))
        {
            return false;
        }

        try
        {
            payload = JsonSerializer.Deserialize<LicenseTokenPayload>(jsonBytes, JsonOptions);
            return payload is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryBase64UrlDecode(string input, out byte[] bytes)
    {
        bytes = [];
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
            case 1: return false;
        }

        try
        {
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
