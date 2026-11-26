using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Switchly.LicenseApi.Options;

namespace Switchly.LicenseApi.Services;

public interface IEmailSender
{
    Task SendOtpAsync(string toEmail, string otpCode, int expiryMinutes, CancellationToken ct);
}

public sealed class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
{
    public Task SendOtpAsync(string toEmail, string otpCode, int expiryMinutes, CancellationToken ct)
    {
        logger.LogInformation(
            "[DEV EMAIL] OTP untuk {Email}: {Code} (berlaku {Minutes} menit)",
            toEmail,
            otpCode,
            expiryMinutes);
        return Task.CompletedTask;
    }
}

public sealed class ResendEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> options,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendOtpAsync(string toEmail, string otpCode, int expiryMinutes, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ResendApiKey))
        {
            throw new InvalidOperationException("Email:ResendApiKey is required when Provider=Resend.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ResendApiKey);

        var body = new
        {
            from = _options.FromAddress,
            to = new[] { toEmail },
            subject = $"Kode Verifikasi Switchly: {otpCode}",
            text = $"Kode OTP Switchly kamu adalah: {otpCode}\n\nKode berlaku selama {expiryMinutes} menit. Jangan bagikan kode ini ke siapa pun.",
        };

        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Resend API error ({Status}): {Body}", (int)response.StatusCode, err);
            throw new InvalidOperationException("Gagal mengirim email OTP. Silakan coba lagi beberapa saat.");
        }
    }
}
