namespace TDPDNE.Telegram.Bot.Abstract;

public interface ITDPDNEWrapper
{
    Stream GetPicture(CancellationToken stoppingToken);
}
