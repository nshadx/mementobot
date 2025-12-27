using mementobot.Handlers;
using mementobot.Services;
using mementobot.Telegram;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAppDbContext(builder.Configuration);
builder.Services.AddTelegram(builder.Configuration);
builder.Services.AddAppPipeline();

builder.Services.RouteCommands();
builder.Services.RouteCallbacks();
builder.Services.RouteFiles();
builder.Services.RouteStates();

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