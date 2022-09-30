using Microsoft.AspNetCore.SignalR;
using MovieCastIdentifier.Models;

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
    public async Task SendImdbData(string user, List<Member> members)
    {
        await Clients.All.ReceiveImdbData(user, members);
    }
}