using Microsoft.AspNetCore.SignalR;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Collections.Concurrent;

namespace MCBusiness.Hubs
{
    public class ChatHub : Hub
    {
        private readonly AppDbContext _context;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentDictionary<string, byte>> _userConnections = new();

        public ChatHub(AppDbContext context)
        {
            _context = context;
        }

        private static void AddConnection(string userId, string connectionId)
        {
            var set = _userConnections.GetOrAdd(userId, _ => new System.Collections.Concurrent.ConcurrentDictionary<string, byte>());
            set.TryAdd(connectionId, 0);
        }

        private static void RemoveConnection(string userId, string connectionId)
        {
            if (_userConnections.TryGetValue(userId, out var set))
            {
                set.TryRemove(connectionId, out _);
                if (set.IsEmpty)
                    _userConnections.TryRemove(userId, out _);
            }
        }

        private static bool IsOnline(string userId)
            => _userConnections.TryGetValue(userId, out var set) && !set.IsEmpty;

        public async Task JoinConversation(int conversationId)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return;

            await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");

            // Envoyer présence + last seen de l'autre participant au client qui rejoint
            var conv = await _context.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conversationId && (c.User1Id == userId || c.User2Id == userId));

            if (conv != null)
            {
                var otherUserId = conv.User1Id == userId ? conv.User2Id : conv.User1Id;
                var otherUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == otherUserId);

                await Clients.Caller.SendAsync("PresenceSnapshot", new
                {
                    userId = otherUserId,
                    isOnline = IsOnline(otherUserId),
                    lastSeenAtUtc = otherUser?.LastSeenAtUtc?.ToString("o")
                });

                // Notifier le groupe que l'utilisateur est en ligne (utile si l'autre est déjà dans la conversation)
                await Clients.Group($"conversation-{conversationId}")
                    .SendAsync("UserOnline", new { userId });
            }
        }

        public async Task SendTyping(int conversationId, bool isTyping)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return;

            await Clients.Group($"conversation-{conversationId}")
                .SendAsync(isTyping ? "UserTyping" : "UserStoppedTyping", new { userId });
        }

        public async Task SendMessage(int conversationId, string content)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(content)) return;

            var message = new Message
            {
                ConversationId = conversationId,
                SenderId = userId,
                Content = content,
                SentAt = DateTimeOffset.UtcNow
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            await Clients.Group($"conversation-{conversationId}")
                .SendAsync("ReceiveMessage", new {
                    id = message.Id,
                    senderId = userId,
                    content,
                    sentAtUtc = message.SentAt.ToString("o"),
                    isRead = message.IsRead
                });
        }

        public async Task MarkAsRead(int messageId)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return;

            var message = await _context.Messages.FirstOrDefaultAsync(m => m.Id == messageId);
            if (message == null) return;

            // Seul le receiver peut marquer comme lu
            if (message.ReceiverId != userId) return;

            if (!message.IsRead)
            {
                message.IsRead = true;
                await _context.SaveChangesAsync();

                // Notifier l'expéditeur (double check) + le groupe de conversation pour MAJ UI
                await Clients.User(message.SenderId).SendAsync("MessageRead", new { messageId });
                await Clients.Group($"conversation-{message.ConversationId}")
                    .SendAsync("MessageRead", new { messageId });
            }
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                AddConnection(userId, Context.ConnectionId);
                // On considère "en ligne" => last seen null/ignoré, mais on ne met pas à jour ici pour éviter du bruit
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                RemoveConnection(userId, Context.ConnectionId);

                // Si plus aucune connexion, enregistrer last seen et notifier les conversations
                if (!IsOnline(userId))
                {
                    var nowUtc = DateTimeOffset.UtcNow;
                    var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                    if (user != null)
                    {
                        user.LastSeenAtUtc = nowUtc;
                        await _context.SaveChangesAsync();
                    }

                    var convIds = await _context.Conversations
                        .AsNoTracking()
                        .Where(c => c.User1Id == userId || c.User2Id == userId)
                        .Select(c => c.Id)
                        .ToListAsync();

                    foreach (var convId in convIds)
                    {
                        await Clients.Group($"conversation-{convId}")
                            .SendAsync("UserLastSeenUpdated", new { userId, lastSeenAtUtc = nowUtc.ToString("o") });

                        await Clients.Group($"conversation-{convId}")
                            .SendAsync("UserOffline", new { userId });
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
