using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using RateLimiterSample.DataAccessLayer;
using RateLimiterSample.Models;
using SimpleAuthentication;
using SimpleAuthentication.ApiKey;
using SimpleAuthentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddSqlServer<ApplicationDbContext>(builder.Configuration.GetConnectionString("SqlConnection"));

builder.Services.AddTransient<IApiKeyValidator, ApiKeyAuthenticator>();
builder.Services.AddTransient<IClaimsTransformation, SubscriptionClaimTransformer>();
builder.Services.AddSimpleAuthentication(builder.Configuration);

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Executing Rate Limiting logic...");

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var permitLimit = Convert.ToInt32(context.User.FindFirstValue("PermitLimit"));
            var window = Convert.ToInt32(context.User.FindFirstValue("Window"));

            return RateLimitPartition.GetFixedWindowLimiter(context.User.Identity?.Name ?? "Default", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(window),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
        }

        return RateLimitPartition.GetTokenBucketLimiter("Shared", _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 100,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            TokensPerPeriod = 10
        });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = (context, token) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var window))
        {
            var response = context.HttpContext.Response;
            response.Headers.RetryAfter = window.TotalSeconds.ToString();
        }

        return ValueTask.CompletedTask;
    };
});

builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSimpleAuthentication(builder.Configuration);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapPost("/api/login", async (IJwtBearerService jwtBearerService, LoginRequest request, DateTime? expiration = null) =>
{
    // Check for login rights...

    // Add custom claims (optional).
    var claims = new List<Claim>
    {
        new(ClaimTypes.GivenName, "Marco"),
        new(ClaimTypes.Surname, "Minerva")
    };

    var token = await jwtBearerService.CreateTokenAsync(request.UserName, claims, absoluteExpiration: expiration);
    return TypedResults.Ok(new LoginResponse(token));
})
.DisableRateLimiting()
.WithOpenApi();

app.MapGet("/api/ping", () =>
{
    return TypedResults.NoContent();
})
.WithOpenApi();

app.Run();

public class ApiKeyAuthenticator(ApplicationDbContext dbContext) : IApiKeyValidator
{
    public async Task<ApiKeyValidationResult> ValidateAsync(string apiKey)
    {
        var user = await dbContext.Accounts.Include(a => a.Subscription)
            .FirstOrDefaultAsync(a => a.ApiKey == apiKey);

        if (user is null)
        {
            return ApiKeyValidationResult.Fail("Invalid user");
        }

        return ApiKeyValidationResult.Success(user.UserName);
    }
}

public class SubscriptionClaimTransformer(ApplicationDbContext dbContext) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var userName = principal.FindFirstValue(ClaimTypes.Name);
        var user = await dbContext.Accounts.Include(a => a.Subscription)
            .FirstOrDefaultAsync(a => a.UserName == userName);

        var identity = principal.Identity as ClaimsIdentity;
        identity!.AddClaim(new Claim("PermitLimit", (user?.Subscription?.PermitLimit ?? 1).ToString()));
        identity.AddClaim(new Claim("Window", (user?.Subscription?.WindowLimitMinutes ?? 1).ToString()));

        return principal;
    }
}
