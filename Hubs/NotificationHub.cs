using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace MCBusiness.Hubs
{
    public class NotificationHub : Hub
    {
        public async Task SubscribeToNotifications()
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            }
        }

        public override async Task OnConnectedAsync()
        {
            await SubscribeToNotifications();
            await base.OnConnectedAsync();
        }
    }
}
