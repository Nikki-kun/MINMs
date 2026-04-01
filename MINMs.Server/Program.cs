using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Minio;
using MINMs.Server.Database;
using MINMs.Server.Options;
using MINMs.Server.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IDbConnectionFactory, MySqlConnectionFactory>();
builder.Services.AddScoped<UserSearchService>();
builder.Services.AddScoped<AuthService>();
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
    });

var minioConfig = builder.Configuration.GetSection("MinIO");
var endpoint = minioConfig["Endpoint"];
var accessKey = minioConfig["AccessKey"];
var secretKey = minioConfig["SecretKey"];
var secure = bool.Parse(minioConfig["Secure"] ?? "false");
var bucketName = minioConfig["BucketName"];

builder.Services.AddSingleton<IMinioClient>(sp =>
{


    var client = new MinioClient()
        .WithEndpoint(endpoint)
        .WithCredentials(accessKey, secretKey)
        .WithSSL(secure)
        .Build();

    return client;
});

builder.Services.AddSingleton<string>(bucketName ?? "minms");

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
