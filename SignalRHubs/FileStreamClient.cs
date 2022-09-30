namespace MovieCastIdentifier.SignalRHubs;

public interface FileStreamClient
{
    Task ReceiveMessage(string user, string message);
    Task FetchImdbApi(string user, string message);
}