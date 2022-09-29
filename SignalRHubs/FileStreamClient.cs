namespace MovieCastIdentifier.SignalRHubs;

public interface FileStreamClient
{
    Task ReceiveMessage(string user, string message);
    Task CallImdb(string castMembers);
}