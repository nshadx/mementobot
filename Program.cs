using mementobot;
using mementobot.Middlewares;
using mementobot.Services;
using mementobot.Telegram;

var builder = Host.CreateApplicationBuilder(args);

builder.AddAppDbContext();
builder.AddTelegram();
builder.AddAppPipeline();

builder.RouteCommands();
builder.RouteCallbacks();
builder.RouteFiles();
builder.RouteStates();

using (var app = builder.Build())
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    await app.RunAsync();
}