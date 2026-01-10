using mementobot;
using mementobot.Services;
using mementobot.Telegram;

var builder = Host.CreateApplicationBuilder(args);

builder.AddTelegram();
builder.AddAppPipeline();
builder.AddServices();
builder.AddDb();

builder.RouteCommands();
builder.RouteCallbacks();
builder.RouteStates();

using (var app = builder.Build())
{
    var dbService = app.Services.GetRequiredService<DbService>();
    dbService.Migrate();
    
    app.Run();
}