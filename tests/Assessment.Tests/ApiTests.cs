using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Application.DTOs;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Assessment.Tests;

public class Factory : WebApplicationFactory<Program>
{
    readonly SqliteConnection connection = new("Data Source=:memory:");

    public Factory()
    {
        connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(
            (_, c) =>
                c.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        { "Jwt:Key", "Test-only-signing-key-with-more-than-32-bytes" },
                        { "Database:Initialize", "false" },
                    }
                )
        );
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            s.RemoveAll<ApplicationDbContext>();
            s.AddScoped<ApplicationDbContext>(_ => new TestDb(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options
            ));
        });
    }

    public void Seed()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        foreach (var role in new[] { "Admin", "Reader" })
        {
            var u = new User { Username = role.ToLowerInvariant(), Role = role };
            u.PasswordHash = new PasswordHasher<User>().HashPassword(u, "TestPassword123!");
            db.Users.Add(u);
        }
        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            connection.Dispose();
    }
}

public class TestDb(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<RefreshToken>().Property(x => x.Version).ValueGeneratedNever();
    }
}

public class ApiTests
{
    async Task<TokenPair> Login(HttpClient c, string username = "admin")
    {
        var response = await c.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(username, "TestPassword123!")
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenPair>())!;
    }

    [Fact]
    public async Task AuthenticationAndRoleBoundaries()
    {
        using var f = new Factory();
        f.Seed();
        using var c = f.CreateClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await c.GetAsync("/api/v1/products")).StatusCode
        );
        var t = await Login(c, "reader");
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            t.AccessToken
        );
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/v1/products")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await c.PostAsJsonAsync("/api/v1/products", new ProductWrite("Denied"))).StatusCode
        );
    }

    [Fact]
    public async Task ProductAndItemLifecycle()
    {
        using var f = new Factory();
        f.Seed();
        using var c = f.CreateClient();
        var t = await Login(c);
        c.DefaultRequestHeaders.Authorization = new("Bearer", t.AccessToken);
        var created = await c.PostAsJsonAsync("/api/v1/products", new ProductWrite("Keyboard"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var p = (await created.Content.ReadFromJsonAsync<ProductDto>())!;
        var url = $"/api/v1/products/{p.Id}";
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(created.Headers.Location)).StatusCode);
        var itemResponse = await c.PostAsJsonAsync(url + "/items", new ItemWrite(3));
        Assert.Equal(HttpStatusCode.Created, itemResponse.StatusCode);
        var i = (await itemResponse.Content.ReadFromJsonAsync<ItemDto>())!;
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await c.PutAsJsonAsync(url + "/items/" + i.Id, new ItemWrite(4))).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (
                await c.PutAsJsonAsync("/api/v1/products/999/items/" + i.Id, new ItemWrite(4))
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await c.PutAsJsonAsync(url, new ProductWrite("Updated"))).StatusCode
        );
        var page = (await c.GetFromJsonAsync<Page<ProductDto>>("/api/v1/products?pageSize=1"))!;
        Assert.Single(page.Data);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(url)).StatusCode);
        using var scope = f.Services.CreateScope();
        Assert.Empty(
            await scope
                .ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Items.ToListAsync()
        );
    }

    [Fact]
    public async Task ValidationAndMalformedJson_ReturnProblemDetails()
    {
        using var f = new Factory();
        f.Seed();
        using var c = f.CreateClient();
        var t = await Login(c);
        c.DefaultRequestHeaders.Authorization = new("Bearer", t.AccessToken);
        foreach (
            var response in new[]
            {
                await c.PostAsJsonAsync("/api/v1/products", new ProductWrite("")),
                await c.GetAsync("/api/v1/products?pageSize=101"),
                await c.PostAsync(
                    "/api/v1/products",
                    new StringContent("{bad", System.Text.Encoding.UTF8, "application/json")
                ),
            }
        )
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType!.MediaType
            );
        }
    }

    [Fact]
    public async Task RefreshRotation_RejectsReplayAndRevokesFamily()
    {
        using var f = new Factory();
        f.Seed();
        using var c = f.CreateClient();
        var first = await Login(c);
        var response = await c.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest(first.RefreshToken)
        );
        response.EnsureSuccessStatusCode();
        var second = (await response.Content.ReadFromJsonAsync<TokenPair>())!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (
                await c.PostAsJsonAsync(
                    "/api/v1/auth/refresh",
                    new RefreshRequest(first.RefreshToken)
                )
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (
                await c.PostAsJsonAsync(
                    "/api/v1/auth/refresh",
                    new RefreshRequest(second.RefreshToken)
                )
            ).StatusCode
        );
    }

    [Fact]
    public async Task RevokeAndInvalidCredentials()
    {
        using var f = new Factory();
        f.Seed();
        using var c = f.CreateClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (
                await c.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "wrong"))
            ).StatusCode
        );
        var t = await Login(c);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (
                await c.PostAsJsonAsync("/api/v1/auth/revoke", new RefreshRequest(t.RefreshToken))
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (
                await c.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(t.RefreshToken))
            ).StatusCode
        );
    }
}
