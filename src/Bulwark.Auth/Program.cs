using dotenv.net;
using FluentEmail.MailKitSmtp;
using System;
using System.IO;
using Bulwark.Auth;
using Bulwark.Auth.Core;
using Bulwark.Auth.Core.PasswordPolicy;
using Bulwark.Auth.Core.Social;
using Bulwark.Auth.Core.Social.Validators;
using Bulwark.Auth.Repositories;
using Bulwark.Auth.Repositories.Util;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
//trigger build: 2 
var applicationBuilder = WebApplication.CreateBuilder(args);
DotEnv.Load(options: new DotEnvOptions(overwriteExistingVars: false));
//AppConfig must be initialized after DotEnv.Load for environment variables to be available
var appConfig = new AppConfig();

applicationBuilder.Logging.ClearProviders();
applicationBuilder.Logging.AddConsole();
applicationBuilder.Services.AddControllers();

applicationBuilder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
});

applicationBuilder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Bulwark.Auth", Version = "v1" });
});

applicationBuilder.Services
    .AddFluentEmail(appConfig.EmailFromAddress)
    .AddRazorRenderer(Directory.GetCurrentDirectory())
    .AddMailKitSender(new SmtpClientOptions
    {
        Server = appConfig.EmailSmtpHost,
        Port = appConfig.EmailSmtpPort,
        UseSsl = appConfig.EmailSmtpSecure,
        User = appConfig.EmailSmtpUser,
        Password = appConfig.EmailSmtpPass,
        RequiresAuthentication = false
    });
var mongoClient = new MongoClient(appConfig.DbConnection);

applicationBuilder.Services.AddSingleton<IMongoClient>(
    mongoClient);

var dbName="BulwarkAuth";
if(!string.IsNullOrEmpty(appConfig.DbNameSeed))
{
    dbName = $"{dbName}-{appConfig.DbNameSeed}";
}

var passwordPolicyService = new PasswordPolicyService();
var passwordLength = new PasswordLength(8, 512);
passwordPolicyService.Add(passwordLength);
var passwordLowerCase = new PasswordLowerCase();
passwordPolicyService.Add(passwordLowerCase);
var passwordUpperCase = new PasswordUpperCase();
passwordPolicyService.Add(passwordUpperCase);
var passwordSymbol = new PasswordSymbol();
passwordPolicyService.Add(passwordSymbol);
var passwordNumber = new PasswordNumber();
passwordPolicyService.Add(passwordNumber);

applicationBuilder.Services.AddSingleton(passwordPolicyService);
applicationBuilder.Services.AddSingleton(mongoClient.GetDatabase(dbName));
applicationBuilder.Services.AddTransient<ITokenRepository, MongoDbAuthToken>();
applicationBuilder.Services.AddTransient<ISigningKeyRepository, MongoDbSigningKey>();
applicationBuilder.Services.AddTransient<IEncrypt, BulwarkBCrypt>();
applicationBuilder.Services.AddSingleton<ISigningKeyService, SigningKeyService>();
applicationBuilder.Services.AddTransient<IAccountRepository, MongoDbAccount>();
applicationBuilder.Services.AddTransient<IAccountService, AccountService>();
applicationBuilder.Services.AddTransient<IAuthenticationService, AuthenticationService>();
applicationBuilder.Services.AddTransient<IMagicCodeRepository, MongoDbMagicCode>();
applicationBuilder.Services.AddTransient<IMagicCodeService, MagicCodeService>();
applicationBuilder.Services.AddTransient<IMagicCodeRepository, MongoDbMagicCode>();
applicationBuilder.Services.AddTransient<IAuthorizationRepository, MongoDbAuthorization>();
//social startup
var socialValidators = new ValidatorStrategies();

if (!string.IsNullOrEmpty(appConfig.GoogleClientId))
{
    var googleValidator = new GoogleValidator(appConfig.GoogleClientId);
    socialValidators.Add(googleValidator);
}

if (!string.IsNullOrEmpty(appConfig.MicrosoftClientId) && 
    !string.IsNullOrEmpty(appConfig.MicrosoftTenantId))
{
    var microSoftValidator = new MicrosoftValidator(appConfig.MicrosoftClientId, appConfig.MicrosoftTenantId);
    socialValidators.Add(microSoftValidator);
}

if (!string.IsNullOrEmpty(appConfig.GithubAppName))
{
    var gitHubValidator = new GithubValidator(appConfig.GithubAppName);
    socialValidators.Add(gitHubValidator);
}

applicationBuilder.Services.AddSingleton<IValidatorStrategies>(socialValidators);
applicationBuilder.Services.AddTransient<ISocialService, SocialService>();
//end of social startup
//end of Inject

//config
var webApplication = applicationBuilder.Build();

if (webApplication.Environment.IsDevelopment())
{
    webApplication.UseExceptionHandler("/error-development");
    webApplication.UseSwagger();
    webApplication.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json",
        "Bulwark.Auth v1"));
}
else
{
    webApplication.UseExceptionHandler("/error");
}

webApplication.UseRouting();
webApplication.MapControllers();
webApplication.Run();
//end of config
