using System;
using Bulwark.Auth.Common.Exceptions;

namespace Bulwark.Auth;

public class AppConfig
{
    public string DbConnection { get; }
    public string DbNameSeed { get; }
    public string GoogleClientId { get;  }
    public string MicrosoftClientId { get; }
    public string MicrosoftTenantId { get; }
    public string GithubAppName { get; }
    public string Domain { get;  }
    public string WebsiteName { get; }
    public string EmailTemplateDir { get; }
    public bool EnableSmtp { get;  }
    public string EmailSmtpHost { get; }
    public int EmailSmtpPort { get; }
    public string EmailSmtpUser { get; }
    public string EmailSmtpPass { get; }
    public bool EmailSmtpSecure { get; }
    public bool EmailAuth { get; }
    public string VerificationUrl { get;  }
    public string ForgotPasswordUrl { get;  }
    public string EmailFromAddress { get; }
    public int MagicCodeExpireInMinutes { get; }
    public int AccessTokenExpireInSeconds { get; }
    public int RefreshTokenExpireInSeconds { get; }
    public AppConfig()
    {
        DbConnection = Environment.GetEnvironmentVariable("DB_CONNECTION") ?? "mongodb://localhost:27017";
        DbNameSeed = Environment.GetEnvironmentVariable("DB_NAME_SEED") ?? string.Empty;
        GoogleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? string.Empty;
        MicrosoftClientId = Environment.GetEnvironmentVariable("MICROSOFT_CLIENT_ID") ?? string.Empty;
        MicrosoftTenantId = Environment.GetEnvironmentVariable("MICROSOFT_TENANT_ID") ?? string.Empty;
        GithubAppName = Environment.GetEnvironmentVariable("GITHUB_APP_NAME") ?? string.Empty;
        Domain = Environment.GetEnvironmentVariable("DOMAIN") ??
                 throw new BulwarkAuthConfigException("DOMAIN environment variable is required.");
        WebsiteName = Environment.GetEnvironmentVariable("WEBSITE_NAME") ??
                      throw new BulwarkAuthConfigException("WEBSITE_NAME environment variable is required.");
        EmailTemplateDir = Environment.GetEnvironmentVariable("EMAIL_TEMPLATE_DIR") ??
                           "Templates/Email/";
        EmailFromAddress = Environment.GetEnvironmentVariable("EMAIL_FROM_ADDRESS") ??
                           throw new BulwarkAuthConfigException(
            "EMAIL_FROM_ADDRESS environment variable is required.");
        EmailFromAddress = EmailFromAddress.Trim();
        EnableSmtp = Environment.GetEnvironmentVariable("ENABLE_SMTP")?.ToLower() == "true";

        if (EnableSmtp)
        {
            EmailSmtpHost = Environment.GetEnvironmentVariable("EMAIL_SMTP_HOST") ??
                            throw new BulwarkAuthConfigException(
                                "EMAIL_SMTP_HOST environment variable is required when SMTP is enabled.");
            EmailSmtpPort = int.TryParse(Environment.GetEnvironmentVariable("EMAIL_SMTP_PORT"), out var port)
                ? port
                : 25;
            EmailSmtpUser = Environment.GetEnvironmentVariable("EMAIL_SMTP_USER") ?? String.Empty;
            EmailSmtpPass = Environment.GetEnvironmentVariable("EMAIL_SMTP_PASS") ?? String.Empty;
            EmailSmtpSecure = Environment.GetEnvironmentVariable("EMAIL_SMTP_SECURE")?.ToLower() == "true";
            EmailAuth = Environment.GetEnvironmentVariable("EMAIL_SMTP_AUTH")?.ToLower() == "true";
        }

        VerificationUrl = Environment.GetEnvironmentVariable("VERIFICATION_URL") ??
                         throw new BulwarkAuthConfigException("VERIFICATION_URL environment variable is required.");
        ForgotPasswordUrl = Environment.GetEnvironmentVariable("FORGOT_PASSWORD_URL") ??
                            throw new BulwarkAuthConfigException("FORGOT_PASSWORD_URL environment variable is required.");
        EmailFromAddress = Environment.GetEnvironmentVariable("EMAIL_FROM_ADDRESS") ??
                           throw new BulwarkAuthConfigException(
            "EMAIL_FROM_ADDRESS environment variable is required.");
        MagicCodeExpireInMinutes = int.TryParse(Environment.GetEnvironmentVariable("MAGIC_CODE_EXPIRE_IN_MINUTES"),
            out var expireInMinutes) ? expireInMinutes : 10;
        AccessTokenExpireInSeconds = int.TryParse(Environment.GetEnvironmentVariable("ACCESS_TOKEN_EXPIRE_IN_SECONDS"),
            out var accessTokenExpireInSeconds) ? accessTokenExpireInSeconds : 3600;
        RefreshTokenExpireInSeconds = int.TryParse(Environment.GetEnvironmentVariable("REFRESH_TOKEN_EXPIRE_IN_SECONDS"),
            out var  refreshTokenExpireInSeconds) ? refreshTokenExpireInSeconds : 36000;
    }
}