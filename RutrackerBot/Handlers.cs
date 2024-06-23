using System.Text;
using BencodeNET.Torrents;
using RuTracker.Client.Model.SearchTopics.Request;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace RutrackerBot;

public class Handlers
{
    public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        // Only process Message updates: https://core.telegram.org/bots/api#message
        if (update.Message is not { } message)
            return;
        // Only process text messages
        if (message.Text is not { } messageText)
            return;

        var chatId = message.Chat.Id;

        Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");

        if (messageText.StartsWith("/download@"))
        {
            var topicStr = messageText.Replace("/download@", String.Empty).Trim();
            if (int.TryParse(topicStr, out var topicId))
            {
                await ProcessDownloadCommand(botClient, chatId, topicId, cancellationToken);
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"Не получилось найти топик: {topicStr}",
                    cancellationToken: cancellationToken);
            }

            return;
        }

        if (messageText.StartsWith("/start"))
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"Напиши мне что хочешь найти",
                cancellationToken: cancellationToken);
            
            return;
        }

        var client = RutrackerSession.Instance.Client;

        var req = new SearchTopicsRequest(
            Title: messageText,
            SortBy: SearchTopicsSortBy.Seeds,
            SortDirection: SearchTopicsSortDirection.Descending
        );

        var resp = await client.SearchTopics(req, cancellationToken);
        var sb = new StringBuilder();
        foreach (var topic in resp.Topics.Take(15))
        {
            sb.AppendLine($"{topic.Title} | {topic.Author?.Name} | {topic.TopicStatus}");
            sb.AppendLine(
                $"Сиды: {topic.SeedsCount} | Личи: {topic.LeechesCount} | Скачивания: {topic.DownloadsCount}");
            sb.AppendLine($"/download@{topic.Id}\n");
        }

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: $"*Найдено {resp.Found} топиков*\n\n{EscapeString(sb.ToString())}",
            parseMode: ParseMode.MarkdownV2,
            cancellationToken: cancellationToken);
    }

    private static string EscapeString(string input)
    {
        foreach (var s in _charactersToEscape)
            input = input.Replace($"{s}", $"\\{s}");

        return input;
    }

    private static string[] _charactersToEscape =
    [
        "_", "*", "[", "]", "(", ")", "~", "`", ">", "#", "+", "-", "=", "|", "{", "}", ".", "!"
    ];

    private static async Task ProcessDownloadCommand(ITelegramBotClient botClient, long chatId, int topicId,
        CancellationToken cancellationToken)
    {
        var client = RutrackerSession.Instance.Client;
        var content = await client
            .GetTopicTorrentFile(topicId, cancellationToken);

        var parser = new TorrentParser(TorrentParserMode.Strict);
        var torrent = parser.Parse(new MemoryStream(content));

        await botClient.SendDocumentAsync(
            chatId: chatId,
            InputFile.FromStream(new MemoryStream(content), $"{torrent.DisplayName}_[rutracker {topicId}].torrent"),
            cancellationToken: cancellationToken);
    }

    public static Task HandlePollingErrorAsync(ITelegramBotClient thisBotClient, Exception exception,
        CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(errorMessage);
        return Task.CompletedTask;
    }
}