using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Identity;

/// <summary>Issues short-lived JWTs and rotates hashed, single-use refresh tokens.</summary>
public class AuthService(
    ApplicationDbContext db,
    IConfiguration config,
    IValidator<LoginRequest> validator
) : IAuthService
{
    static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    TokenPair Issue(User user, Guid family)
    {
        var expires = DateTime.UtcNow.AddMinutes(15);
        var key = config["Jwt:Key"]!;
        var jwt = new JwtSecurityToken(
            config["Jwt:Issuer"],
            config["Jwt:Audience"],
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ],
            expires: expires,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256
            )
        );
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        db.RefreshTokens.Add(
            new RefreshToken
            {
                UserId = user.Id,
                Hash = Hash(raw),
                Family = family,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
            }
        );
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), raw, expires);
    }

    public async Task<TokenPair> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var user = await db
            .Users.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Username == request.Username, ct);
        if (
            user is null
            || new PasswordHasher<User>().VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password
            ) == PasswordVerificationResult.Failed
        )
            throw new AuthenticationException();
        var pair = Issue(user, Guid.NewGuid());
        await db.SaveChangesAsync(ct);
        return pair;
    }

    public async Task<TokenPair> RefreshAsync(string token, CancellationToken ct)
    {
        var t = await db
            .RefreshTokens.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Hash == Hash(token), ct);
        if (t is null)
            throw new AuthenticationException();
        if (t.RevokedAt is not null)
        {
            await RevokeFamily(t.Family, ct);
            throw new AuthenticationException();
        }
        if (t.ExpiresAt <= DateTime.UtcNow)
            throw new AuthenticationException();
        t.RevokedAt = DateTime.UtcNow;
        var pair = Issue(t.User, t.Family);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            await RevokeFamily(t.Family, ct);
            throw new AuthenticationException();
        }
        return pair;
    }

    async Task RevokeFamily(Guid family, CancellationToken ct)
    {
        var tokens = await db
            .RefreshTokens.Where(x => x.Family == family && x.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in tokens)
            t.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAsync(string token, CancellationToken ct)
    {
        var t = await db
            .RefreshTokens.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Hash == Hash(token), ct);
        if (t is not null)
            await RevokeFamily(t.Family, ct);
    }
}
