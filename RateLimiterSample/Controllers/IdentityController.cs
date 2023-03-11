using System.Net.Mime;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SimpleAuthentication.JwtBearer;

namespace RateLimiterSample.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public class AuthController : ControllerBase
{
    private readonly IJwtBearerService jwtBearerService;

    public AuthController(IJwtBearerService jwtBearerService)
    {
        this.jwtBearerService = jwtBearerService;
    }

    [DisableRateLimiting]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesDefaultResponseType]
    public ActionResult<LoginResponse> Login(LoginRequest loginRequest, DateTime? expiration = null)
    {
        // Check for login rights...

        // Add custom claims (optional).
        var claims = new List<Claim>
        {
            new(ClaimTypes.GivenName, "Marco"),
            new(ClaimTypes.Surname, "Minerva")
        };

        var token = jwtBearerService.CreateToken(loginRequest.UserName, claims, absoluteExpiration: expiration);
        return new LoginResponse(token);
    }
}

public record class LoginRequest(string UserName, string Password);

public record class LoginResponse(string Token);
