using Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Middleware;

public class ErrorMiddleware(RequestDelegate next, ILogger<ErrorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception ex)
        {
            var code = ex switch
            {
                ValidationException => 400,
                NotFoundException => 404,
                AuthenticationException => 401,
                DbUpdateException => 409,
                _ => 500,
            };
            if (code == 500)
                logger.LogError(ex, "Unhandled error {TraceId}", context.TraceIdentifier);
            var problem = new ProblemDetails
            {
                Status = code,
                Title =
                    code == 500 ? "An unexpected error occurred."
                    : code == 409 ? "Database conflict."
                    : ex.Message,
                Instance = context.Request.Path,
            };
            problem.Extensions["traceId"] = context.TraceIdentifier;
            if (ex is ValidationException v)
                problem.Extensions["errors"] = v
                    .Errors.GroupBy(x => x.PropertyName)
                    .ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).ToArray());
            context.Response.StatusCode = code;
            await context.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: context.RequestAborted
            );
        }
    }
}
