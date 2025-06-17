using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;



var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseMiddleware<TokenAuthenticationMiddleware>();

app.UseLoggingMiddleware();


// The following has been built, debugged, and optimized with the assistance of Co-Pilot 
var users = new List<User>();

app.MapPost("/users", (User user) =>
{
    if (string.IsNullOrWhiteSpace(user.UserName) || user.UserAge <= 0)
    {
        return Results.BadRequest("Invalid user data.");
    }

    user.Id = users.Count > 0 ? users.Max(u => u.Id) + 1 : 1;
    users.Add(user);
    return Results.Created($"/users/{user.Id}", user);
});

app.MapGet("/users", () => users);

app.MapGet("/users/{id}", (int id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    return user is not null ? Results.Ok(user) : Results.NotFound();
});

app.MapPut("/users/{id}", (int id, User inputUser) =>
{
    if (string.IsNullOrWhiteSpace(inputUser.UserName) || inputUser.UserAge <= 0)
    {
        return Results.BadRequest("Invalid user data.");
    }

    var user = users.FirstOrDefault(u => u.Id == id);
    if (user is null) return Results.NotFound();

    user.UserName = inputUser.UserName;
    user.UserAge = inputUser.UserAge;

    return Results.NoContent();
});

app.MapDelete("/users/{id}", (int id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    if (user is null) return Results.NotFound();

    users.Remove(user);
    return Results.NoContent();
});

// This is added for testing the global handler, while debugging with Co-Pilot it has been identified and handled with the preceeding function
app.MapGet("/exception-test/{num}", (int num) => 1/num);

// Handled version by Co-Pilot
// app.MapGet("/exception-test/{num}", (int num) =>
// {
//     if (num == 0)
//     {
//         throw new DivideByZeroException();
//     }
//     return Results.Ok(1 / num);
// });



app.Run();

public class User
{
    public int Id { get; set; }
    public string UserName { get; set; }
    public int UserAge { get; set; }
}



public class LoggingMiddleware
{
	 private readonly RequestDelegate _next;
	 private readonly ILogger<LoggingMiddleware> _logger;

	 public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
	 {
		 _next = next;
		 _logger = logger;
	 }

	 public async Task InvokeAsync(HttpContext context)
	 {
		 // Log the request details
		 _logger.LogInformation("HTTP Request: {Method} {Path}", context.Request.Method, context.Request.Path);

		 // Call the next middleware in the pipeline
		 await _next(context);

		 // Log the response details
		 _logger.LogInformation("HTTP Response: {StatusCode}", context.Response.StatusCode);
	 }
}



public static class LoggingMiddlewareExtensions
{
	 public static IApplicationBuilder UseLoggingMiddleware(this IApplicationBuilder builder)
	 {
		 return builder.UseMiddleware<LoggingMiddleware>();
	 }
}


public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var response = new { error = "Internal server error." };
        return context.Response.WriteAsJsonAsync(response);
    }
}



public class TokenAuthenticationMiddleware
{
	 private readonly RequestDelegate _next;

	 public TokenAuthenticationMiddleware(RequestDelegate next)
	 {
		 _next = next;
	 }

	 public async Task InvokeAsync(HttpContext context)
	 {
		 if (!context.Request.Headers.TryGetValue("Authorization", out var token))
		 {
				 context.Response.StatusCode = StatusCodes.Status401Unauthorized;
				 await context.Response.WriteAsync("Unauthorized");
				 return;
		 }

		 if (!ValidateToken(token))
		 {
				 context.Response.StatusCode = StatusCodes.Status401Unauthorized;
				 await context.Response.WriteAsync("Unauthorized");
				 return;
		 }

		 await _next(context);
	 }

	 private bool ValidateToken(string token)
	 {
        return token.ToString().Replace("Bearer ", "") == "TestToken";
	 }
}
