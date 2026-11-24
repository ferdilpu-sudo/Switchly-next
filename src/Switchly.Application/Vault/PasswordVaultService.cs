using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Switchly.Application.Security;
using Switchly.Domain.Vault;

namespace Switchly.Application.Vault;

/// <summary>
/// Encrypted per-profile password vault (ENT-009, ADR-005, D6, TASK-028).
/// Uses a DPAPI-wrapped 256-bit vault key and single-payload AES-256-GCM with unique 96-bit CSPRNG nonces
/// and AAD binding (CryptoVersion, CredentialId, ProfileId, Origin).
/// Never logs or includes raw CSV rows or passwords in exceptions or diagnostics (TEST-010, TEST-022).
/// </summary>
public sealed class PasswordVaultService : IPasswordVaultService
{
    private const int KeyLength = 32;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int MaxCsvRows = 10_000;
    private const long MaxCsvBytes = 20L * 1024 * 1024;
    private const int MaxFieldLength = 2048;

    private static readonly byte[] VaultKeyEntropy = "SwitchlyNext.VaultMasterKey.v1"u8.ToArray();

    private readonly IVaultCredentialRepository _repository;
    private readonly ILocalDataProtector _protector;
    private readonly string _vaultKeyFilePath;
    private readonly TimeProvider _timeProvider;
    private readonly object _keyLock = new();

    public PasswordVaultService(
        IVaultCredentialRepository repository,
        ILocalDataProtector protector,
        string dataDirectory,
        TimeProvider? timeProvider = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            throw new ArgumentException("Data directory is required.", nameof(dataDirectory));
        }

        Directory.CreateDirectory(dataDirectory);
        _vaultKeyFilePath = Path.Combine(dataDirectory, "vault-key.dat");
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<VaultCredentialSummary> UpsertCredentialAsync(
        Guid profileId,
        string urlOrOrigin,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("Profile ID is required.", nameof(profileId));
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username is required.", nameof(username));
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password is required.", nameof(password));
        }

        var origin = VaultOriginNormalizer.NormalizeOrigin(urlOrOrigin);
        var cleanUser = username.Trim();
        var now = _timeProvider.GetUtcNow();

        var existingForOrigin = await _repository.ListByProfileAndOriginAsync(profileId, origin, cancellationToken);
        var key = LoadOrCreateVaultKey();
        try
        {
            VaultCredential? matchingExisting = null;
            foreach (var candidate in existingForOrigin)
            {
                var decrypted = DecryptPayload(candidate, key);
                if (string.Equals(decrypted.Username, cleanUser, StringComparison.OrdinalIgnoreCase))
                {
                    matchingExisting = candidate;
                    break;
                }
            }

            if (matchingExisting is not null)
            {
                // Always rotate to a fresh unique 96-bit CSPRNG nonce on update.
                EncryptIntoEntity(matchingExisting, cleanUser, password, key);
                matchingExisting.UpdatedAtUtc = now;
                await _repository.UpdateAsync(matchingExisting, cancellationToken);
                return new VaultCredentialSummary(matchingExisting.CredentialId, profileId, origin, cleanUser, now);
            }

            var entity = new VaultCredential
            {
                CredentialId = Guid.NewGuid(),
                ProfileId = profileId,
                Origin = origin,
                CryptoVersion = VaultCredential.CurrentCryptoVersion,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            EncryptIntoEntity(entity, cleanUser, password, key);
            await _repository.AddAsync(entity, cancellationToken);
            return new VaultCredentialSummary(entity.CredentialId, profileId, origin, cleanUser, now);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public async Task<IReadOnlyList<VaultCredentialSummary>> ListSummariesAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        var records = await _repository.ListByProfileAsync(profileId, cancellationToken);
        if (records.Count == 0)
        {
            return Array.Empty<VaultCredentialSummary>();
        }

        var key = LoadExistingVaultKeyOrFail();
        try
        {
            var list = new List<VaultCredentialSummary>(records.Count);
            foreach (var record in records)
            {
                var payload = DecryptPayload(record, key);
                list.Add(new VaultCredentialSummary(
                    record.CredentialId,
                    record.ProfileId,
                    record.Origin,
                    payload.Username,
                    record.UpdatedAtUtc));
            }

            return list;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public async Task<IReadOnlyList<VaultCredentialMatch>> FindMatchesForUrlAsync(
        Guid profileId,
        string currentPageUrl,
        CancellationToken cancellationToken = default)
    {
        if (!VaultOriginNormalizer.TryNormalizeOrigin(currentPageUrl, out var origin))
        {
            return Array.Empty<VaultCredentialMatch>();
        }

        var records = await _repository.ListByProfileAndOriginAsync(profileId, origin, cancellationToken);
        if (records.Count == 0)
        {
            return Array.Empty<VaultCredentialMatch>();
        }

        var key = LoadExistingVaultKeyOrFail();
        try
        {
            var matches = new List<VaultCredentialMatch>(records.Count);
            foreach (var record in records)
            {
                var payload = DecryptPayload(record, key);
                matches.Add(new VaultCredentialMatch(record.CredentialId, record.Origin, payload.Username, payload.Password));
            }

            return matches;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public async Task DeleteAsync(Guid profileId, Guid credentialId, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(credentialId, cancellationToken);
        if (existing is null || existing.ProfileId != profileId)
        {
            return;
        }

        await _repository.DeleteAsync(existing, cancellationToken);
    }

    public async Task<CsvPasswordImportResult> ImportChromeEdgeCsvAsync(
        Guid profileId,
        string csvFilePath,
        CancellationToken cancellationToken = default)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("Profile ID is required.", nameof(profileId));
        }

        if (string.IsNullOrWhiteSpace(csvFilePath) || !File.Exists(csvFilePath))
        {
            throw new FileNotFoundException("File CSV password tidak ditemukan.");
        }

        var info = new FileInfo(csvFilePath);
        if (info.Length > MaxCsvBytes)
        {
            throw new InvalidOperationException("Ukuran file CSV melebihi batas aman impor (20 MB).");
        }

        var warnings = new List<string>();
        var imported = 0;
        var updated = 0;
        var skipped = 0;
        var malformed = 0;

        string content;
        using (var stream = new FileStream(csvFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            content = await reader.ReadToEndAsync(cancellationToken);
        }

        var rows = ParseRfc4180Csv(content, out var hadUnclosedQuote);
        if (hadUnclosedQuote)
        {
            warnings.Add("Terdeteksi tanda kutip yang tidak tertutup pada file CSV; baris akhir yang tidak valid dilewati.");
        }

        if (rows.Count == 0)
        {
            throw new InvalidDataException("File CSV kosong atau tidak memiliki header kolom yang valid.");
        }

        var header = rows[0];
        var urlIdx = FindHeaderIndex(header, "url", "origin", "website");
        var userIdx = FindHeaderIndex(header, "username", "login", "email");
        var passIdx = FindHeaderIndex(header, "password");

        if (urlIdx < 0 || userIdx < 0 || passIdx < 0)
        {
            throw new InvalidDataException(
                "Header CSV tidak dikenali. Gunakan ekspor resmi Chrome/Edge yang memiliki kolom 'url', 'username', dan 'password'.");
        }

        var existingRecords = await _repository.ListByProfileAsync(profileId, cancellationToken);
        var key = LoadOrCreateVaultKey();
        try
        {
            // Build in-memory lookup of existing (Origin, Username) -> entity so updates reuse entity ID cleanly.
            var existingMap = new Dictionary<(string Origin, string Username), VaultCredential>();
            foreach (var rec in existingRecords)
            {
                var dec = DecryptPayload(rec, key);
                existingMap[(rec.Origin, dec.Username.ToLowerInvariant())] = rec;
            }

            var maxRowIndex = Math.Min(rows.Count, MaxCsvRows + 1);
            if (rows.Count - 1 > MaxCsvRows)
            {
                warnings.Add($"Batas maksimum {MaxCsvRows} baris CSV tercapai; sisa baris dilewati.");
            }

            var now = _timeProvider.GetUtcNow();

            for (var r = 1; r < maxRowIndex; r++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fields = rows[r];
                if (fields.Count == 1 && string.IsNullOrWhiteSpace(fields[0]))
                {
                    continue;
                }

                if (fields.Count <= Math.Max(urlIdx, Math.Max(userIdx, passIdx)))
                {
                    malformed++;
                    continue;
                }

                var rawUrl = fields[urlIdx].Trim();
                var rawUser = fields[userIdx].Trim();
                var rawPass = fields[passIdx];

                if (rawUrl.Length > MaxFieldLength || rawUser.Length > MaxFieldLength || rawPass.Length > MaxFieldLength)
                {
                    malformed++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(rawUser) || string.IsNullOrEmpty(rawPass))
                {
                    skipped++;
                    continue;
                }

                if (!VaultOriginNormalizer.TryNormalizeOrigin(rawUrl, out var origin))
                {
                    malformed++;
                    continue;
                }

                var mapKey = (origin, rawUser.ToLowerInvariant());
                if (existingMap.TryGetValue(mapKey, out var existing))
                {
                    EncryptIntoEntity(existing, rawUser, rawPass, key);
                    existing.UpdatedAtUtc = now;
                    await _repository.UpdateAsync(existing, cancellationToken);
                    updated++;
                }
                else
                {
                    var entity = new VaultCredential
                    {
                        CredentialId = Guid.NewGuid(),
                        ProfileId = profileId,
                        Origin = origin,
                        CryptoVersion = VaultCredential.CurrentCryptoVersion,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now,
                    };
                    EncryptIntoEntity(entity, rawUser, rawPass, key);
                    await _repository.AddAsync(entity, cancellationToken);
                    existingMap[mapKey] = entity;
                    imported++;
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        if (malformed > 0)
        {
            // Redacted warning — NEVER include raw CSV row values or passwords (TEST-010, TEST-022).
            warnings.Add($"{malformed} baris CSV tidak valid atau menggunakan URL non-HTTP/HTTPS sehingga dilewati.");
        }

        return new CsvPasswordImportResult(imported, updated, skipped, malformed, warnings);
    }

    private byte[] LoadOrCreateVaultKey()
    {
        lock (_keyLock)
        {
            if (File.Exists(_vaultKeyFilePath))
            {
                var protectedBytes = File.ReadAllBytes(_vaultKeyFilePath);
                var key = _protector.Unprotect(protectedBytes, VaultKeyEntropy);
                if (key.Length != KeyLength)
                {
                    CryptographicOperations.ZeroMemory(key);
                    throw new CryptographicException("Vault master key length is invalid.");
                }

                return key;
            }

            var newKey = RandomNumberGenerator.GetBytes(KeyLength);
            var wrapped = _protector.Protect(newKey, VaultKeyEntropy);
            var tempPath = _vaultKeyFilePath + ".tmp";
            File.WriteAllBytes(tempPath, wrapped);
            File.Move(tempPath, _vaultKeyFilePath, overwrite: true);
            return newKey;
        }
    }

    private byte[] LoadExistingVaultKeyOrFail()
    {
        lock (_keyLock)
        {
            if (!File.Exists(_vaultKeyFilePath))
            {
                throw new CryptographicException("Vault master key is missing (fail-closed).");
            }

            var protectedBytes = File.ReadAllBytes(_vaultKeyFilePath);
            var key = _protector.Unprotect(protectedBytes, VaultKeyEntropy);
            if (key.Length != KeyLength)
            {
                CryptographicOperations.ZeroMemory(key);
                throw new CryptographicException("Vault master key length is invalid.");
            }

            return key;
        }
    }

    private static void EncryptIntoEntity(VaultCredential entity, string username, string password, byte[] key)
    {
        var payloadJson = JsonSerializer.Serialize(new SecretPayload(username, password));
        var plaintext = Encoding.UTF8.GetBytes(payloadJson);
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagLength];
        var aad = BuildAad(entity.CryptoVersion, entity.CredentialId, entity.ProfileId, entity.Origin);

        try
        {
            using var aes = new AesGcm(key, TagLength);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);

            entity.Nonce = nonce;
            entity.SecretPayloadCiphertext = ciphertext;
            entity.AuthenticationTag = tag;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static SecretPayload DecryptPayload(VaultCredential entity, byte[] key)
    {
        if (entity.Nonce.Length != NonceLength || entity.AuthenticationTag.Length != TagLength)
        {
            throw new CryptographicException("Encrypted credential metadata is malformed.");
        }

        var plaintext = new byte[entity.SecretPayloadCiphertext.Length];
        var aad = BuildAad(entity.CryptoVersion, entity.CredentialId, entity.ProfileId, entity.Origin);

        try
        {
            using var aes = new AesGcm(key, TagLength);
            aes.Decrypt(entity.Nonce, entity.SecretPayloadCiphertext, entity.AuthenticationTag, plaintext, aad);

            return JsonSerializer.Deserialize<SecretPayload>(plaintext)
                ?? throw new CryptographicException("Decrypted credential payload is empty.");
        }
        catch (AuthenticationTagMismatchException)
        {
            throw new CryptographicException("Credential authentication tag mismatch (tampered or wrong context).");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] BuildAad(int cryptoVersion, Guid credentialId, Guid profileId, string origin)
        => Encoding.UTF8.GetBytes($"{cryptoVersion}|{credentialId:D}|{profileId:D}|{origin}");

    private static int FindHeaderIndex(IReadOnlyList<string> header, params string[] acceptedNames)
    {
        for (var i = 0; i < header.Count; i++)
        {
            var normalized = header[i].Trim().Trim('\uFEFF').ToLowerInvariant();
            if (acceptedNames.Contains(normalized, StringComparer.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static List<List<string>> ParseRfc4180Csv(string text, out bool hadUnclosedQuote)
    {
        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            else
            {
                if (c == '"' && field.Length == 0)
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    currentRow.Add(field.ToString());
                    field.Clear();
                }
                else if (c == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    currentRow.Add(field.ToString());
                    field.Clear();
                    rows.Add(currentRow);
                    currentRow = new List<string>();
                }
                else if (c == '\n')
                {
                    currentRow.Add(field.ToString());
                    field.Clear();
                    rows.Add(currentRow);
                    currentRow = new List<string>();
                }
                else
                {
                    field.Append(c);
                }
            }
        }

        hadUnclosedQuote = inQuotes;
        if (!inQuotes && (field.Length > 0 || currentRow.Count > 0))
        {
            currentRow.Add(field.ToString());
            rows.Add(currentRow);
        }

        return rows;
    }

    private sealed record SecretPayload(string Username, string Password);
}
