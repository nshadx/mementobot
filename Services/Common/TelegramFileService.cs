using Telegram.Bot;

namespace mementobot.Services.Common;

public class TelegramFileService(ITelegramBotClient client)
{
    public async Task Download(string fileId, Stream dest)
    {
        var file = await client.GetFile(fileId);
        await client.DownloadFile(file, dest);
        dest.Position = 0;
    }
}
