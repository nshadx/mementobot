using mementobot;
using mementobot.Services;
using mementobot.Telegram;
using Telegram.Bot;

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

var botClient = app.Services.GetRequiredService<ITelegramBotClient>();
await botClient.SetMyDescription(
    """
    Привет! Я помогаю тренировать память с помощью метода интервальных повторений — создавай собственные опросники или проходи чужие.

    Вызови /help, если что-то непонятно.
    """
);

app.Run();
