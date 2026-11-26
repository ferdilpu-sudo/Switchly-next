using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Switchly.Application.Entitlements;
using Switchly.Domain.Entitlements;

namespace Switchly.Infrastructure.Entitlements;

/// <summary>
/// Offline Ed25519 signature verifier for Switchly License API tokens (D16).
/// Holds only the read-only Ed25519 public key.
/// </summary>
public sealed class Ed25519LicenseTokenVerifier : ILicenseTokenVerifier
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly Ed25519PublicKeyParameters _publicKey;

    public Ed25519LicenseTokenVerifier(string publicKeyBase64)
    {
        if (string.IsNullOrWhiteSpace(publicKeyBase64))
        {
            throw new ArgumentException("Ed25519 public key Base64 must not be empty.", nameof(publicKeyBase64));
        }

        var keyBytes = Convert.FromBase64String(publicKeyBase64.Trim());
        if (keyBytes.Length != 32)
        {
            throw new ArgumentException("Ed25519 public key must be exactly 32 bytes.", nameof(publicKeyBase64));
        }

        _publicKey = new Ed25519PublicKeyParameters(keyBytes, 0);
    }

    public bool TryVerify(string signedToken, out SignedLicensePayload? payload)
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
            payload = JsonSerializer.Deserialize<SignedLicensePayload>(jsonBytes, JsonOptions);
            return payload is not null && payload.Version == 1;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryBase64UrlDecode(string input, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
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
