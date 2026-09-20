using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Services;

public interface IRecoveryEmailSender
{
    Task SendAsync(string email, string link);
}

public sealed class SmtpRecoveryEmailSender(IConfiguration configuration) : IRecoveryEmailSender
{
    public async Task SendAsync(string email, string link)
    {
        var host = configuration["Smtp:Host"] ?? throw new InvalidOperationException("SMTP is not configured.");
        using var client = new SmtpClient(host, configuration.GetValue("Smtp:Port", 587)) {
            EnableSsl = true, Credentials = new NetworkCredential(configuration["Smtp:Username"], configuration["Smtp:Password"]) };
        using var message = new MailMessage(configuration["Smtp:From"] ?? throw new InvalidOperationException("SMTP sender is not configured."), email,
            "Reset your FoodSupply password", $"Use this single-use link within 30 minutes to reset your password:\n{link}\n\nIf you did not request this, ignore this email.");
        await client.SendMailAsync(message);
    }
}

public sealed class PasswordRecoveryService(ApplicationDbContext db, IRecoveryEmailSender sender,
    IConfiguration configuration, ILogger<PasswordRecoveryService> logger)
{
    public async Task RequestAsync(string email)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email && u.IsActive && !u.IsArchived);
        if (user == null) return;
        var origin = configuration["Application:PublicUrl"];
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != "https")
        { logger.LogError("Password recovery requires Application:PublicUrl with an HTTPS origin."); return; }
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        user.ResetTokenHash = Hash(token);
        user.ResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(30);
        await db.SaveChangesAsync();
        var link = new Uri(uri, $"/Account/ResetPassword?userId={user.Id}&token={token}").AbsoluteUri;
        try { await sender.SendAsync(user.Email, link); }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            // Never expose reset tokens or account existence in responses or logs.
            logger.LogError("Recovery email delivery failed ({ErrorType}). Check SMTP configuration.", ex.GetType().Name);
            user.ResetTokenHash = null;
            user.ResetTokenExpiresAt = null;
            await db.SaveChangesAsync();
        }
    }

    public async Task<bool> ResetAsync(int userId, string token, string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 12) return false;
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.IsActive && !u.IsArchived);
        if (user == null || !Valid(user, token)) return false;
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        user.ResetTokenHash = null;
        user.ResetTokenExpiresAt = null;
        user.SecurityStamp = Guid.NewGuid().ToString();
        await db.SaveChangesAsync();
        return true;
    }

    public static bool Valid(User user, string token) => !string.IsNullOrWhiteSpace(token) && token.Length == 64 &&
        user.ResetTokenExpiresAt > DateTime.UtcNow && user.ResetTokenHash != null &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(user.ResetTokenHash), Encoding.UTF8.GetBytes(Hash(token)));
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
