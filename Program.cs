using mementobot;
using mementobot.Services;
using mementobot.Telegram;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTelegram(builder.Configuration.GetSection(nameof(TelegramConfiguration)).Bind);
builder.Services.AddServices();
builder.Services.AddDb(builder.Configuration.GetConnectionString("Db")!);

builder.Services.RouteCommands();
builder.Services.RouteStateMachines();
builder.Services.ConfigureAppPipeline();

using var app = builder.Build();

var dbService = app.Services.GetRequiredService<DbService>();
dbService.Migrate();

app.Run();
