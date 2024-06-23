using System.Runtime.InteropServices;
using RutrackerBot;
using Telegram.Bot;
using Telegram.Bot.Polling;

// ReSharper disable once ReturnValueOfPureMethodIsNotUsed
RutrackerSession.Instance.ToString();

var bot = new TelegramBotClient(Environment.GetEnvironmentVariable("BOT_TOKEN") ??
                                throw new ArgumentException("BOT_TOKEN not in env"));

using CancellationTokenSource cts = new();

ReceiverOptions receiverOptions = new()
{
    AllowedUpdates = [] // receive all update types except ChatMember related updates
};

bot.StartReceiving(
    updateHandler: Handlers.HandleUpdateAsync,
    pollingErrorHandler: Handlers.HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

var me = await bot.GetMeAsync();
var reg = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ =>
{
    // ReSharper disable once AccessToDisposedClosure
    cts.Cancel();
});

Console.WriteLine($"Start listening for @{me.Username}");
Console.ReadLine();

// Send cancellation request to stop bot
cts.Cancel();