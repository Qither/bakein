using Bakein.Api.Api;
using Bakein.Api.Infrastructure;
using Bakein.Api.Security;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0)
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            return;
        }

        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Default")
        ?? configuration["POSTGRES_CONNECTION_STRING"]
        ?? throw new InvalidOperationException("Missing Postgres connection string. Set ConnectionStrings:Default or POSTGRES_CONNECTION_STRING.");

    return new NpgsqlDataSourceBuilder(connectionString).Build();
});
builder.Services.AddSingleton<DatabaseInitializer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Frontend");
app.UseSessionAuthentication();

if (app.Configuration.GetValue("Database:Initialize", true))
{
    await app.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "bakein.api",
    utc = DateTimeOffset.UtcNow,
}));

var api = app.MapGroup("/api");
api.MapAuthEndpoints();
api.MapCatalogEndpoints();
api.MapUserEndpoints();

app.Run();

public partial class Program;
