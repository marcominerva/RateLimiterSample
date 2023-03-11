using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;

namespace RateLimiterSample.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public class SampleController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => NoContent();
}
