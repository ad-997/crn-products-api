using Application.DTOs;
using Application.Interfaces;
using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers;

[
    ApiController,
    ApiVersion("1.0"),
    Route("api/v{version:apiVersion}/auth"),
    EnableRateLimiting("auth")
]
public class AuthController(IAuthService service, IValidator<RefreshRequest> validator)
    : ControllerBase
{
    /// <summary>Verifies credentials and returns an access/refresh token pair.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<TokenPair>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await service.LoginAsync(request, ct));

    /// <summary>Rotates a single-use refresh token and issues a new token pair.</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<TokenPair>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return Ok(await service.RefreshAsync(request.RefreshToken, ct));
    }

    /// <summary>Revokes the refresh token family; existing JWTs expire normally.</summary>
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(RefreshRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        await service.RevokeAsync(request.RefreshToken, ct);
        return NoContent();
    }
}
