using MovieCastIdentifier.Models;

namespace MovieCastIdentifier.SignalRHubs;

public interface FileStreamClient
{
    Task ReceiveMessage(string user, string message);
    Task FetchImdbApi(string user, string message);
    Task ReceiveImdbData(string user, List<Member> members);
}