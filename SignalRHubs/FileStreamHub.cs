using Microsoft.AspNetCore.SignalR;

namespace MovieCastIdentifier.SignalRHubs;

public class FileStreamHub : Hub<FileStreamClient>
{
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.ReceiveMessage(user, message);
    }
    public async Task FetchImdbApi(string user, string message)
    {
        await Clients.All.FetchImdbApi(user, message);
    }
}