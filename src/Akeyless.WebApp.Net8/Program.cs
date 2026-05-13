using Akeyless.WebApp.Net8;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddAkeylessResolvedSecrets();

var app = builder.Build();

app.MapGet("/health", (IConfiguration config) =>
    Results.Ok(new
    {
        status = "ok",
        logging_level = config["Logging:LogLevel:Default"],
    }));

app.Run();
