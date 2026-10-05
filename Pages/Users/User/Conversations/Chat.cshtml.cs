using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using MCBusiness.Hubs;
using MCBusiness.Services;
using MCBusiness.Interfaces;
#nullable enable


namespace MCBusiness.Pages.Users.User.Conversations
{
    public class ChatModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IEmailSender _emailSender;
        public ChatModel(AppDbContext context, IHubContext<ChatHub> hubContext, IEmailSender emailSender)
        {
            _context = context;
            _hubContext = hubContext;           
            _emailSender = emailSender;
        }

        public List<Conversation> Conversations { get; set; } = new();
        public Conversation? CurrentConversation { get; set; }
        public List<Company> Company { get; set;} = new();

        public async Task OnGetAsync(int? conversationId, string? code)
        {
            Company = _context.Companies.ToList();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if(!string.IsNullOrEmpty(code)){
                var user2 = await _context.Users
                    .FirstOrDefaultAsync(u => u.ReferralCode == code);

                if (user2 != null)
                {
                    var verifyConExist = await _context.Conversations
                        .FirstOrDefaultAsync(c =>
                            (c.User1Id == userId && c.User2Id == user2.Id) ||
                            (c.User1Id == user2.Id && c.User2Id == userId));

                    if (verifyConExist != null)
                    {
                        conversationId = verifyConExist.Id;
                    }
                    else if (user2.Id == userId)
                    {
                        // Code de parrainage invalide ou auto-parrainage
                    }
                    else
                    {
                        // Créer une nouvelle conversation si elle n'existe pas
                        Conversation conv = new Conversation{
                            User1Id = userId,
                            User2Id = user2.Id
                        };
                        _context.Conversations.Add(conv);
                        await _context.SaveChangesAsync();
                        conversationId = conv.Id;
                    }
                }
                else
                {
                    // Code de parrainage invalide
                }
            }
            
            Conversations = await _context.Conversations
                .Include(c => c.User1)
                .Include(c => c.User2)
                .Include(c => c.Messages)
                .Where(c => c.User1Id == userId || c.User2Id == userId)
                .ToListAsync();

            if (conversationId.HasValue)
            {
                CurrentConversation = await _context.Conversations
                    .Include(c => c.User1)
                    .Include(c => c.User2)
                    .Include(c => c.Messages)
                    .FirstOrDefaultAsync(c => c.Id == conversationId);

                if (CurrentConversation == null)
                    return;

                // Marquer les messages comme lus
                var unreadMessages = CurrentConversation.Messages
                    .Where(m => m.ReceiverId == userId && !m.IsRead)
                    .ToList();

                foreach (var msg in unreadMessages)
                {
                    msg.IsRead = true;
                }

                await _context.SaveChangesAsync();

                // Notifier l'expéditeur des messages lus (double check)
                foreach (var msg in unreadMessages)
                {
                    await _hubContext.Clients.User(msg.SenderId)
                        .SendAsync("MessageRead", new { messageId = msg.Id });
                }
            }
        }

        public async Task<IActionResult> OnPostStartConversation(string username)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            username = username?.Trim();
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest("Nom d'utilisateur invalide");

            // Recherche par UserName (insensible à la casse)
            var normalized = username.ToUpperInvariant();
            var targetUser = await _context.Users
                .FirstOrDefaultAsync(u => u.NormalizedUserName == normalized);

            if (targetUser == null || targetUser.Id == userId)
            {
                return BadRequest("Utilisateur introuvable");
            }

            var existingConversation = await _context.Conversations
                .FirstOrDefaultAsync(c =>
                    (c.User1Id == userId && c.User2Id == targetUser.Id) ||
                    (c.User1Id == targetUser.Id && c.User2Id == userId));

            if (existingConversation == null)
            {
                var newConversation = new Conversation
                {
                    User1Id = userId,
                    User2Id = targetUser.Id
                };
                _context.Conversations.Add(newConversation);
                await _context.SaveChangesAsync();
                return RedirectToPage("./Chat", new { conversationId = newConversation.Id });
            }

            return RedirectToPage("./Chat", new { conversationId = existingConversation.Id });
        }

        public async Task<IActionResult> OnGetMessagesAsync(int conversationId, int? beforeMessageId, int take = 30)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return new JsonResult(Array.Empty<object>()) { StatusCode = 401 };

            // Vérifier l'accès à la conversation
            var conv = await _context.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conversationId && (c.User1Id == userId || c.User2Id == userId));
            if (conv == null) return new JsonResult(Array.Empty<object>()) { StatusCode = 404 };

            var query = _context.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.SentAt);

            if (beforeMessageId.HasValue)
            {
                var before = await _context.Messages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == beforeMessageId.Value);
                if (before != null)
                {
                    query = query.Where(m => m.SentAt < before.SentAt).OrderByDescending(m => m.SentAt);
                }
            }

            take = Math.Clamp(take, 10, 100);
            var messages = await query.Take(take).ToListAsync();
            messages.Reverse(); // ancien -> récent

            var result = messages.Select(m => new {
                id = m.Id,
                senderId = m.SenderId,
                content = m.Content,
                sentAtUtc = m.SentAt.ToString("o"),
                isRead = m.IsRead
            });

            return new JsonResult(result);
        }

        public async Task<IActionResult> OnPostSendMessage(int conversationId, string content)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var conversation = await _context.Conversations
                .Include(c => c.User1)
                .Include(c => c.User2)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null) return NotFound();

            var receiverId = conversation.User1Id == userId ? 
                conversation.User2Id : 
                conversation.User1Id;

            var message = new Message
            {
                ConversationId = conversationId,
                SenderId = userId,
                ReceiverId = receiverId,
                Content = content,
                SentAt = DateTimeOffset.UtcNow
            };

            try 
            {
                _context.Messages.Add(message);
                await _context.SaveChangesAsync();
                
                await _hubContext.Clients.Group($"conversation-{conversationId}")
                    .SendAsync("ReceiveMessage", new {
                        id = message.Id,
                        senderId = userId,
                        content,
                        sentAtUtc = message.SentAt.ToString("o"),
                        isRead = message.IsRead
                    });

                var sender =  await _context.Users.FindAsync(userId);   
                var receiver = await _context.Users.FindAsync(receiverId); 
                await _emailSender.SendEmailAsync(
                    receiver.Email,
                    "Nouveau message",
                    $"Vous avez reçu un nouveau message de {sender.UserName}."
                );


                return new OkResult();
            }
            catch (Exception ex)
            {
                // Logger l'erreur
                return StatusCode(500, $"{ex}--:Erreur interne");
            }
        }
    }
}
