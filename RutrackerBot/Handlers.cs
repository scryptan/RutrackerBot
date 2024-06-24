using System.Text;
using BencodeNET.Torrents;
using ByteSizeLib;
using RuTracker.Client.Model.SearchTopics.Request;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace RutrackerBot;

public class Handlers
{
    private const int MaxTopicsCount = 20;

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

        var isCommand = messageText.StartsWith('/');
        if (isCommand)
        {
            await ProcessCommands(botClient, messageText, chatId, cancellationToken);
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
        foreach (var topic in resp.Topics.Take(MaxTopicsCount))
        {
            sb.AppendLine($"{topic.Title} | {topic.Author?.Name} | {topic.TopicStatus}");
            sb.AppendLine(
                $"Сиды: {topic.SeedsCount} | Личи: {topic.LeechesCount} | Скачивания: {topic.DownloadsCount}");
            var size = ByteSize.FromBytes(topic.SizeInBytes);

            string sizeText;
            if (size.GibiBytes >= 1)
            {
                sizeText = $"{size.GibiBytes:F}GB";
            }
            else if (size.MebiBytes >= 1)
            {
                sizeText = $"{size.MebiBytes:F}MB";
            }
            else
            {
                sizeText = $"{size.KibiBytes:F}KB";
            }

            sb.AppendLine($"Размер: {sizeText}");
            sb.AppendLine($"/show@{topic.Id}");
            sb.AppendLine($"/download@{topic.Id}\n");

            if (sb.Length > 3000)
            {
                await SendFoundMessage();
                sb.Clear();
            }
        }

        if (sb.Length > 0)
            await SendFoundMessage();

        async Task SendFoundMessage()
        {
            await SendTextMessage(botClient,
                $"*Найдено {resp.Found} топиков, отображается первые {MaxTopicsCount} по сидам*\n\n{EscapeString(sb.ToString())}",
                chatId, cancellationToken);
        }
    }

    private static async Task ProcessCommands(ITelegramBotClient botClient, string messageText, long chatId,
        CancellationToken cancellationToken)
    {
        if (messageText.StartsWith("/download@"))
        {
            if (TryGetCommandTopic(messageText, out var topicId))
            {
                await ProcessDownloadCommand(botClient, chatId, topicId, cancellationToken);
            }
            else
            {
                await SendTextMessage(botClient, "Не получилось найти такой топик", chatId, cancellationToken);
            }

            return;
        }

        if (messageText.StartsWith("/show@"))
        {
            if (TryGetCommandTopic(messageText, out var topicId))
            {
                await ProcessShowCommand(botClient, chatId, topicId, cancellationToken);
            }
            else
            {
                await SendTextMessage(botClient, "Не получилось найти такой топик", chatId, cancellationToken);
            }

            return;
        }

        if (messageText.StartsWith("/start"))
        {
            await SendTextMessage(botClient, "Напиши мне что хочешь найти", chatId, cancellationToken);
            return;
        }


        await SendTextMessage(botClient, "Какая-то неизвестная команда", chatId, cancellationToken);
    }

    private static async Task SendTextMessage(ITelegramBotClient botClient, string messageText, long chatId,
        CancellationToken cancellationToken)
    {
        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: messageText,
            parseMode: ParseMode.MarkdownV2,
            cancellationToken: cancellationToken);
    }

    private static bool TryGetCommandTopic(string messageText, out int topicId)
    {
        var topicStr = messageText.Split('@', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Last().Trim();
        return int.TryParse(topicStr, out topicId);
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
        var content = await RutrackerSession.Instance.Client
            .GetTopicTorrentFile(topicId, cancellationToken);

        var parser = new TorrentParser(TorrentParserMode.Strict);
        var torrent = parser.Parse(new MemoryStream(content));

        await botClient.SendDocumentAsync(
            chatId: chatId,
            InputFile.FromStream(new MemoryStream(content), $"{torrent.DisplayName}_[rutracker {topicId}].torrent"),
            cancellationToken: cancellationToken);
    }

    private static async Task ProcessShowCommand(ITelegramBotClient botClient, long chatId, int topicId,
        CancellationToken cancellationToken)
    {
        var content = await RutrackerSession.Instance.Client
            .GetTopicTorrentFile(topicId, cancellationToken);

        var parser = new TorrentParser(TorrentParserMode.Strict);
        var torrent = parser.Parse(new MemoryStream(content));

        var sb = new StringBuilder($"{torrent.DisplayName}\n");


        if (torrent.Files != null)
        {
            foreach (var filePath in torrent.Files
                         .Select(ToDescription)
                         .Distinct()
                         .OrderBy(x => x.FilePath.Length)
                         .Take(30)
                     )
            {
                sb.AppendLine($"- {filePath}");
                if (sb.Length > 3000)
                {
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: EscapeString(sb.ToString()),
                        parseMode: ParseMode.MarkdownV2,
                        cancellationToken: cancellationToken);
                    sb.Clear();
                }
            }
        }

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: $"*Показаны первые 30 файлов*\n{EscapeString(sb.ToString())}",
            parseMode: ParseMode.MarkdownV2,
            cancellationToken: cancellationToken);

        TorrentFileDescription ToDescription(MultiFileInfo fileInfo)
        {
            var path = fileInfo.FullPath.Split('/',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (path.Length > 1)
                return new TorrentFileDescription(path[0], FileType.Folder);

            return new TorrentFileDescription(fileInfo.FullPath, FileType.File);
        }
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