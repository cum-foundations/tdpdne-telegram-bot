namespace TDPDNE.Telegram.Bot.Services;

using Abstract;
using Exceptions;
using global::Telegram.Bot;
using global::Telegram.Bot.Exceptions;
using global::Telegram.Bot.Polling;
using global::Telegram.Bot.Types;
using global::Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Logging;
using System;
using TDPDNE.Telegram.Bot.Configs;

public class UpdateHandler : IUpdateHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<UpdateHandler> _logger;

    private static readonly BotConfiguration BotConfiguration;
    private static readonly ITDPDNEWrapper Wrapper;

    public UpdateHandler(ITelegramBotClient botClient, ILogger<UpdateHandler> logger)
    {
        _botClient = botClient;
        _logger = logger;
    }

    static UpdateHandler()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", true)
            .AddJsonFile("appsettings.Development.json", true)
            .Build();

        BotConfiguration = configuration.GetRequiredSection(BotConfiguration.Configuration).Get<BotConfiguration>() ??
                           throw new ArgumentNullException(BotConfiguration.Configuration);
        OverrideBotConfigurationFromEnvironmentVariables(BotConfiguration);

        Wrapper = new TDPDNEWrapper(configuration);
    }

    public async Task HandleUpdateAsync(ITelegramBotClient _, Update update, CancellationToken cancellationToken)
    {
        var handler = update switch
        {
            { Message: { } message } => BotOnMessageReceived(message, cancellationToken),
            { EditedMessage: { } message } => BotOnMessageReceived(message, cancellationToken),
            _ => UnknownUpdateHandlerAsync(update, cancellationToken)
        };

        await handler;
    }

    private async Task BotOnMessageReceived(Message message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Receive message type: {MessageType}", message.Type);
        if (message.Text is not { } messageText)
            return;

        var action = messageText.Split(' ')[0] switch
        {
            "/generate" => UploadPicture(_botClient, message, cancellationToken),
            "/support" => SendSupport(_botClient, message, cancellationToken),
            "/donations" => SendDonations(_botClient, message, cancellationToken),
            _ => Usage(_botClient, message, cancellationToken)
        };
        var sentMessage = await action;
        _logger.LogInformation("The message was sent with id: {SentMessageId}", sentMessage.MessageId);
        _logger.LogInformation("The message was sent by user: " +
                               $"id - {sentMessage.Chat.Id}, " +
                               $"username - {sentMessage.Chat.Username}, " +
                               $"first name - {sentMessage.Chat.FirstName}, " +
                               $"last name - {sentMessage.Chat.LastName}");

        static async Task<Message> UploadPicture(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            await botClient.SendChatActionAsync(
                message.Chat.Id,
                ChatAction.UploadPhoto,
                cancellationToken: cancellationToken);

            try
            {
                var content = Wrapper.GetPicture(cancellationToken);

                return await botClient.SendPhotoAsync(
                    chatId: message.Chat.Id,
                    photo: InputFile.FromStream(content),
                    cancellationToken: cancellationToken);
            }
            catch (ServiceUnavailableException)
            {
                return await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Service is temporarily unavailable",
                    cancellationToken: cancellationToken);
            }
        }

        static async Task<Message> SendSupport(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            string text = (@"*Support*:\n" +
                          $"{BotConfiguration.Support}")
                .Replace(@"\n", Environment.NewLine);

            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: text,
                parseMode: ParseMode.MarkdownV2,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> SendDonations(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            string text = (@"*Donations*:\n" +
                          $"{BotConfiguration.Donations}")
                .Replace(@"\n", Environment.NewLine);

            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: text,
                parseMode: ParseMode.MarkdownV2,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> Usage(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            const string usage = "*Usage*:\n" +
                                 "/generate \\- _generate a new dickpic_\n" +
                                 "/support \\- _support contacts_\n" +
                                 "/donations \\- _links to donations_";

            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: usage,
                parseMode: ParseMode.MarkdownV2,
                cancellationToken: cancellationToken);
        }
    }

    private Task UnknownUpdateHandlerAsync(Update update, CancellationToken _)
    {
        _logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
        return Task.CompletedTask;
    }

    public async Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogInformation("HandleError: {ErrorMessage}", errorMessage);

        // Cooldown in case of network connection error
        if (exception is RequestException)
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    }

    private static void OverrideBotConfigurationFromEnvironmentVariables(BotConfiguration botConfiguration)
    {
        botConfiguration.BotToken = Environment.GetEnvironmentVariable("BotConfiguration__BotToken") ?? botConfiguration.BotToken;
        botConfiguration.Support = Environment.GetEnvironmentVariable("BotConfiguration__Support") ?? botConfiguration.Support;
        botConfiguration.Donations = Environment.GetEnvironmentVariable("BotConfiguration__Donations") ?? botConfiguration.Donations;
    }
}
