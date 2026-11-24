using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Switchly.Application.Security;
using Switchly.Domain.Security;

namespace Switchly.Infrastructure.Security;

/// <summary>
/// Chunked streaming AES-256-GCM archive cryptography with PBKDF2-HMAC-SHA256 KDF (ADR-006, D3).
/// Binds SHA-256(header) + chunkIndex + isFinal into every chunk's AAD to prevent OOM,
/// chunk reordering, tampering, and truncation attacks (TEST-012, TEST-013).
/// </summary>
public static class ChunkedAesGcmArchiveCrypto
{
    private const int SaltLength = 32;
    private const int NoncePrefixLength = 8;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int KeyLength = 32;
    private const int FixedHeaderLength = 8 + 4 + 4 + 4 + SaltLength + NoncePrefixLength + 4; // 64 bytes

    public static void EncryptStream(
        Stream plaintextInput,
        Stream archiveOutput,
        BackupManifest manifest,
        string passphrase,
        int? kdfIterationsOverride = null,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plaintextInput);
        ArgumentNullException.ThrowIfNull(archiveOutput);
        ArgumentNullException.ThrowIfNull(manifest);

        if (string.IsNullOrEmpty(passphrase) || passphrase.Length < BackupLimits.MinPassphraseLength)
        {
            throw new ArgumentException($"Passphrase minimal {BackupLimits.MinPassphraseLength} karakter.", nameof(passphrase));
        }

        var iterations = kdfIterationsOverride ?? BackupLimits.Pbkdf2Iterations;
        var chunkSize = BackupLimits.ChunkSizeBytes;
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var noncePrefix = RandomNumberGenerator.GetBytes(NoncePrefixLength);
        var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
        if (manifestBytes.Length > BackupLimits.MaxManifestBytes)
        {
            throw new InvalidOperationException("Backup manifest exceeds maximum allowed size.");
        }

        var headerBytes = BuildHeaderBytes(
            manifest.ArchiveVersion,
            iterations,
            chunkSize,
            salt,
            noncePrefix,
            manifestBytes);
        var headerHash = SHA256.HashData(headerBytes);

        archiveOutput.Write(headerBytes);

        var key = DeriveKey(passphrase, salt, iterations);
        try
        {
            using var aes = new AesGcm(key, TagLength);
            var readBuffer = new byte[chunkSize];
            var nextBuffer = new byte[chunkSize];
            var cipherBuffer = new byte[chunkSize];
            Span<byte> nonce = stackalloc byte[NonceLength];
            Span<byte> tag = stackalloc byte[TagLength];
            Span<byte> aad = stackalloc byte[32 + 4 + 1];
            Span<byte> frameHeader = stackalloc byte[4 + 1];

            headerHash.CopyTo(aad[..32]);
            noncePrefix.CopyTo(nonce[..NoncePrefixLength]);

            uint chunkIndex = 0;
            var currentLen = ReadFullyUpTo(plaintextInput, readBuffer);
            long totalProcessed = 0;
            var totalExpected = Math.Max(1L, plaintextInput.CanSeek ? plaintextInput.Length : manifest.EstimatedPlaintextBytes);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var nextLen = currentLen > 0 ? ReadFullyUpTo(plaintextInput, nextBuffer) : 0;
                var isFinal = nextLen == 0 ? (byte)1 : (byte)0;

                BinaryPrimitives.WriteUInt32BigEndian(nonce[NoncePrefixLength..], chunkIndex);
                BinaryPrimitives.WriteUInt32BigEndian(aad.Slice(32, 4), chunkIndex);
                aad[36] = isFinal;

                aes.Encrypt(
                    nonce,
                    readBuffer.AsSpan(0, currentLen),
                    cipherBuffer.AsSpan(0, currentLen),
                    tag,
                    aad);

                BinaryPrimitives.WriteUInt32LittleEndian(frameHeader[..4], (uint)currentLen);
                frameHeader[4] = isFinal;

                archiveOutput.Write(frameHeader);
                archiveOutput.Write(tag);
                if (currentLen > 0)
                {
                    archiveOutput.Write(cipherBuffer.AsSpan(0, currentLen));
                }

                totalProcessed += currentLen;
                progress?.Report(new BackupProgress("Mengenkripsi arsip...", Math.Min(0.95, (double)totalProcessed / totalExpected)));

                if (isFinal == 1)
                {
                    break;
                }

                (readBuffer, nextBuffer) = (nextBuffer, readBuffer);
                currentLen = nextLen;
                chunkIndex = checked(chunkIndex + 1);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public static BackupManifest InspectHeader(Stream archiveInput)
    {
        var parsed = ReadAndValidateHeader(archiveInput);
        return parsed.Manifest;
    }

    public static BackupManifest DecryptStream(
        Stream archiveInput,
        Stream plaintextOutput,
        string passphrase,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archiveInput);
        ArgumentNullException.ThrowIfNull(plaintextOutput);

        if (string.IsNullOrEmpty(passphrase))
        {
            throw new CryptographicException("Passphrase diperlukan untuk mendekripsi arsip.");
        }

        var header = ReadAndValidateHeader(archiveInput);
        var key = DeriveKey(passphrase, header.Salt, header.KdfIterations);

        try
        {
            using var aes = new AesGcm(key, TagLength);
            var cipherBuffer = new byte[header.ChunkSize];
            var plainBuffer = new byte[header.ChunkSize];
            Span<byte> nonce = stackalloc byte[NonceLength];
            Span<byte> tag = stackalloc byte[TagLength];
            Span<byte> aad = stackalloc byte[32 + 4 + 1];
            Span<byte> frameHeader = stackalloc byte[4 + 1];

            header.HeaderHash.CopyTo(aad[..32]);
            header.NoncePrefix.CopyTo(nonce[..NoncePrefixLength]);

            uint chunkIndex = 0;
            var sawFinalChunk = false;
            long totalDecrypted = 0;
            var totalExpected = Math.Max(1L, header.Manifest.EstimatedPlaintextBytes);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var headerRead = ReadExactOrEof(archiveInput, frameHeader);
                if (headerRead == 0)
                {
                    break;
                }

                if (headerRead < frameHeader.Length)
                {
                    throw new CryptographicException("Arsip backup terpotong atau rusak (header chunk tidak lengkap).");
                }

                if (sawFinalChunk)
                {
                    throw new CryptographicException("Arsip backup mengandung data tambahan tak dikenal setelah chunk akhir.");
                }

                var chunkLen = BinaryPrimitives.ReadUInt32LittleEndian(frameHeader[..4]);
                var isFinal = frameHeader[4];
                if (chunkLen > header.ChunkSize || (isFinal != 0 && isFinal != 1))
                {
                    throw new CryptographicException("Frame arsip backup tidak valid.");
                }

                if (ReadExactOrEof(archiveInput, tag) != TagLength)
                {
                    throw new CryptographicException("Arsip backup terpotong (tag autentikasi tidak lengkap).");
                }

                if (chunkLen > 0 && ReadExactOrEof(archiveInput, cipherBuffer.AsSpan(0, (int)chunkLen)) != (int)chunkLen)
                {
                    throw new CryptographicException("Arsip backup terpotong (payload chunk tidak lengkap).");
                }

                BinaryPrimitives.WriteUInt32BigEndian(nonce[NoncePrefixLength..], chunkIndex);
                BinaryPrimitives.WriteUInt32BigEndian(aad.Slice(32, 4), chunkIndex);
                aad[36] = isFinal;

                aes.Decrypt(
                    nonce,
                    cipherBuffer.AsSpan(0, (int)chunkLen),
                    tag,
                    plainBuffer.AsSpan(0, (int)chunkLen),
                    aad);

                if (chunkLen > 0)
                {
                    plaintextOutput.Write(plainBuffer.AsSpan(0, (int)chunkLen));
                }

                totalDecrypted += chunkLen;
                progress?.Report(new BackupProgress("Mendekripsi & memverifikasi arsip...", Math.Min(0.8, (double)totalDecrypted / totalExpected)));

                if (isFinal == 1)
                {
                    sawFinalChunk = true;
                }

                chunkIndex = checked(chunkIndex + 1);
            }

            if (!sawFinalChunk)
            {
                throw new CryptographicException("Arsip backup terpotong sebelum chunk akhir (truncation detected).");
            }

            return header.Manifest;
        }
        catch (AuthenticationTagMismatchException)
        {
            throw new CryptographicException("Passphrase salah atau arsip backup telah rusak/dimodifikasi.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] BuildHeaderBytes(
        int archiveVersion,
        int kdfIterations,
        int chunkSize,
        byte[] salt,
        byte[] noncePrefix,
        byte[] manifestBytes)
    {
        var header = new byte[FixedHeaderLength + manifestBytes.Length];
        BackupLimits.ArchiveMagic.CopyTo(header.AsSpan(0, 8));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8, 4), (uint)archiveVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12, 4), (uint)kdfIterations);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16, 4), (uint)chunkSize);
        salt.CopyTo(header.AsSpan(20, SaltLength));
        noncePrefix.CopyTo(header.AsSpan(52, NoncePrefixLength));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(60, 4), (uint)manifestBytes.Length);
        manifestBytes.CopyTo(header.AsSpan(64));
        return header;
    }

    private static ParsedHeader ReadAndValidateHeader(Stream stream)
    {
        var fixedBytes = new byte[FixedHeaderLength];
        if (ReadExactOrEof(stream, fixedBytes) != FixedHeaderLength)
        {
            throw new CryptographicException("File bukan arsip Switchly Next yang valid (header terlalu pendek).");
        }

        if (!fixedBytes.AsSpan(0, 8).SequenceEqual(BackupLimits.ArchiveMagic))
        {
            throw new CryptographicException("Format file arsip tidak dikenali (magic header tidak cocok).");
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(fixedBytes.AsSpan(8, 4));
        if (version == 0 || version > BackupLimits.CurrentArchiveVersion)
        {
            throw new NotSupportedException($"Versi arsip backup ({version}) tidak didukung oleh versi aplikasi ini.");
        }

        var kdfIterations = (int)BinaryPrimitives.ReadUInt32LittleEndian(fixedBytes.AsSpan(12, 4));
        var chunkSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(fixedBytes.AsSpan(16, 4));
        if (kdfIterations < 10_000 || kdfIterations > 5_000_000)
        {
            throw new CryptographicException("Parameter KDF arsip backup tidak valid.");
        }

        if (chunkSize < 1024 || chunkSize > 4 * 1024 * 1024)
        {
            throw new CryptographicException("Ukuran chunk arsip backup tidak valid.");
        }

        var salt = fixedBytes.AsSpan(20, SaltLength).ToArray();
        var noncePrefix = fixedBytes.AsSpan(52, NoncePrefixLength).ToArray();
        var manifestLen = (int)BinaryPrimitives.ReadUInt32LittleEndian(fixedBytes.AsSpan(60, 4));
        if (manifestLen <= 0 || manifestLen > BackupLimits.MaxManifestBytes)
        {
            throw new CryptographicException("Ukuran manifest arsip backup tidak valid.");
        }

        var manifestBytes = new byte[manifestLen];
        if (ReadExactOrEof(stream, manifestBytes) != manifestLen)
        {
            throw new CryptographicException("Arsip backup terpotong (manifest tidak lengkap).");
        }

        var fullHeader = new byte[FixedHeaderLength + manifestLen];
        fixedBytes.CopyTo(fullHeader, 0);
        manifestBytes.CopyTo(fullHeader, FixedHeaderLength);
        var headerHash = SHA256.HashData(fullHeader);

        BackupManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<BackupManifest>(manifestBytes)
                ?? throw new CryptographicException("Manifest arsip backup kosong.");
        }
        catch (JsonException)
        {
            throw new CryptographicException("Manifest arsip backup rusak.");
        }

        return new ParsedHeader(manifest, kdfIterations, chunkSize, salt, noncePrefix, headerHash);
    }

    private static byte[] DeriveKey(string passphrase, byte[] salt, int iterations)
    {
        var passBytes = Encoding.UTF8.GetBytes(passphrase);
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(passBytes, salt, iterations, HashAlgorithmName.SHA256, KeyLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passBytes);
        }
    }

    private static int ReadFullyUpTo(Stream stream, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private static int ReadExactOrEof(Stream stream, Span<byte> destination)
    {
        var total = 0;
        while (total < destination.Length)
        {
            var read = stream.Read(destination[total..]);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private sealed record ParsedHeader(
        BackupManifest Manifest,
        int KdfIterations,
        int ChunkSize,
        byte[] Salt,
        byte[] NoncePrefix,
        byte[] HeaderHash);
}
