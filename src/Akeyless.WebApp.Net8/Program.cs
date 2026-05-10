using Akeyless.WebApp.Net8;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<AkeylessMemorySecrets>();

var app = builder.Build();

var secrets = app.Services.GetRequiredService<AkeylessMemorySecrets>();
secrets.LoadFromAkeyless(app.Configuration);

app.Lifetime.ApplicationStopping.Register(secrets.Dispose);

app.MapGet("/health", () => Results.Ok(new { status = "ok", akeyless_secrets_loaded = secrets.LoadedCount }));

app.Run();
