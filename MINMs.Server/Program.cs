using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Minio;
using MINMs.Server.Database;
using MINMs.Server.Options;
using MINMs.Server.Services;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IDbConnectionFactory, MySqlConnectionFactory>();
builder.Services.AddScoped<IUserSearchService, UserSearchService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddSingleton<JwtTokenService>();

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.Configure<JwtOptions>(jwtSection);

var jwt = jwtSection.Get<JwtOptions>()
    ?? throw new InvalidOperationException($"Configuration section '{JwtOptions.SectionName}' is missing.");
var jwtKeyBytes = Encoding.UTF8.GetBytes(jwt.Key);
if (jwtKeyBytes.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 UTF-8 bytes (256 bits) for HS256.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes),
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        };
        
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var sessionService = context.HttpContext.RequestServices.GetRequiredService<IJwtSessionService>();
                var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

                if (string.IsNullOrWhiteSpace(jti))
                {
                    context.Fail("JWT has no jti claim.");
                    return;
                }

                var isActive = await sessionService.IsActiveAsync(jti, context.HttpContext.RequestAborted).ConfigureAwait(false);
                if (!isActive)
                    context.Fail("JWT session is revoked or expired.");
            }
        };
    });

var minioSection = builder.Configuration.GetSection(MinioOptions.SectionName);
builder.Services.Configure<MinioOptions>(minioSection);

builder.Services.AddSingleton<IMinioClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<MinioOptions>>().Value;

    var client = new MinioClient()
        .WithEndpoint(options.Endpoint)
        .WithCredentials(options.AccessKey, options.SecretKey)
        .WithSSL(options.Secure)
        .WithRegion(string.IsNullOrWhiteSpace(options.Region) ? "us-east-1" : options.Region)
        .Build();

    return client;
});

builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<MinioOptions>>().Value.BucketName);
builder.Services.AddScoped<IMinioStorageService, MinioStorageService>();

var redisSection = builder.Configuration.GetSection(RedisOptions.SectionName);
builder.Services.Configure<RedisOptions>(redisSection);
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var options = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
    return ConnectionMultiplexer.Connect(options.Endpoint);
});

builder.Services.AddScoped<IJwtSessionService, RedisJwtSessionService>();

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseDefaultFiles();
app.MapStaticAssets();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();
