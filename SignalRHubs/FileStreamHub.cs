using Microsoft.AspNetCore.SignalR;

namespace MovieCastIdentifier.SignalRHubs;

public class FileStreamHub : Hub
{
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}