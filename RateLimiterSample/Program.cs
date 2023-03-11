using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using RateLimiterSample.DataAccessLayer;
using SimpleAuthentication;
using SimpleAuthentication.ApiKey;

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
        var logger = context.RequestServices.GetService<ILogger<Program>>();
        logger.LogInformation("Executing Rate Limiting logic...");

        if (context.User.Identity.IsAuthenticated)
        {
            var permitLimit = Convert.ToInt32(context.User.FindFirstValue("PermitLimit"));
            var window = Convert.ToInt32(context.User.FindFirstValue("Window"));

            return RateLimitPartition.GetFixedWindowLimiter(context.User.Identity.Name, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(window)
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

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSimpleAuthentication(builder.Configuration);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    _ = app.UseSwagger();
    _ = app.UseSwaggerUI();
}

app.UseRateLimiter();

app.MapControllers();

app.Run();

public class ApiKeyAuthenticator : IApiKeyValidator
{
    private readonly ApplicationDbContext dbContext;

    public ApiKeyAuthenticator(ApplicationDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

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

public class SubscriptionClaimTransformer : IClaimsTransformation
{
    private readonly ApplicationDbContext dbContext;

    public SubscriptionClaimTransformer(ApplicationDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var userName = principal.FindFirstValue(ClaimTypes.Name);
        var user = await dbContext.Accounts.Include(a => a.Subscription)
            .FirstOrDefaultAsync(a => a.UserName == userName);

        var identity = principal.Identity as ClaimsIdentity;
        identity.AddClaim(new Claim("PermitLimit", user.Subscription.PermitLimit.ToString()));
        identity.AddClaim(new Claim("Window", user.Subscription.WindowLimitMinutes.ToString()));

        return principal;
    }
}
