using Microsoft.AspNetCore.SignalR;

namespace MovieCastIdentifier.SignalRHubs;

public class FileStreamHub : Hub<FileStreamClient>
{
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.ReceiveMessage(user, message);
    }
    public async Task CallImdb(string castMembers)
    {
        await Clients.All.CallImdb(castMembers);
    }
}