using System.Text;
using System.Threading.RateLimiting;
using API;
using API.Middleware;
using Application.Interfaces;
using Application.Services;
using Application.Validators;
using Asp.Versioning;
using Domain.Entities;
using FluentValidation;
using Infrastructure.Data;
using Infrastructure.Data.Repositories;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog(
    (ctx, log) =>
        log.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext().WriteTo.Console()
);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder
    .Services.AddApiVersioning(o =>
    {
        o.DefaultApiVersion = new ApiVersion(1, 0);
        o.ReportApiVersions = true;
        o.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(o =>
    {
        o.GroupNameFormat = "'v'VVV";
        o.SubstituteApiVersionInUrl = true;
    });
builder.Services.AddDbContext<ApplicationDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Database"))
);
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddValidatorsFromAssemblyContaining<ProductValidator>();
builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            ),
            ClockSkew = TimeSpan.FromSeconds(30),
        }
    );
builder.Services.AddAuthorization();
builder.Services.AddResponseCompression();
builder.Services.AddCors(o =>
    o.AddDefaultPolicy(p =>
        p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod()
    )
);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy(
        "auth",
        ctx =>
            RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }
            )
    );
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "API.xml"));
    o.OperationFilter<SwaggerSecurityFilter>();
    o.SwaggerDoc("v1", new() { Title = "CRN Products API", Version = "v1" });
    o.AddSecurityDefinition(
        "Bearer",
        new()
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
        }
    );

});
var app = builder.Build();
var key =
    builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Set Jwt:Key using user secrets or an environment variable."
    );
if (Encoding.UTF8.GetByteCount(key) < 32)
    throw new InvalidOperationException("Jwt:Key must contain at least 32 bytes.");

if (builder.Configuration.GetValue<bool>("Database:Initialize"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    foreach (var role in new[] { "Admin", "Reader" })
    {
        var name = role.ToLowerInvariant();
        var password =
            builder.Configuration[$"Seed:{role}Password"]
            ?? throw new InvalidOperationException(
                $"Set Seed:{role}Password to initialize demo users."
            );
        if (password.Length < 12)
            throw new InvalidOperationException("Seed passwords must be at least 12 characters.");
        if (!await db.Users.AnyAsync(x => x.Username == name))
        {
            var user = new User { Username = name, Role = role };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
            db.Users.Add(user);
        }
    }
    await db.SaveChangesAsync();
}
app.UseSerilogRequestLogging();
app.UseMiddleware<ErrorMiddleware>();
app.Use(
    async (ctx, next) =>
    {
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["X-Frame-Options"] = "DENY";
        ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
        ctx.Response.Headers["Cache-Control"] = "no-store";
        await next();
    }
);
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePages(async c =>
{
    await Results
        .Problem(
            statusCode: c.HttpContext.Response.StatusCode,
            extensions: new Dictionary<string, object?>
            {
                { "traceId", c.HttpContext.TraceIdentifier },
            }
        )
        .ExecuteAsync(c.HttpContext);
});
app.UseResponseCompression();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.Run();

public partial class Program;
