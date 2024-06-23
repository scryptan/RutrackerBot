using RuTracker.Client;

namespace RutrackerBot;

public class RutrackerSession : IDisposable
{
    public static RutrackerSession Instance
    {
        get
        {
            _instance ??= new RutrackerSession();
            return _instance;
        }
    }

    public readonly RuTrackerClient Client;

    private static RutrackerSession? _instance;

    private RutrackerSession()
    {
        Client = new RuTrackerClient();
        Client.Login(
            Environment.GetEnvironmentVariable("RUTRACKER_LOGIN") ??
            throw new ArgumentException("RUTRACKER_LOGIN not in env"),
            Environment.GetEnvironmentVariable("RUTRACKER_PSWD") ??
            throw new ArgumentException("RUTRACKER_PSWD not in env")).Wait();
    }

    public void Dispose()
    {
        Client.Dispose();
    }
}