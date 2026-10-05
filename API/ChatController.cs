using Microsoft.AspNetCore.Mvc;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly AppDbContext _context;

    public ChatController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var conversations = await _context.Conversations
            .Include(c => c.User1)
            .Include(c => c.User2)
            .Include(c => c.Messages)
            .Where(c => c.User1Id == userId || c.User2Id == userId)
            .Select(c => new {
                c.Id,
                OtherUser = c.User1Id == userId ? c.User2 : c.User1,
                LastMessage = c.Messages.OrderByDescending(m => m.SentAt).FirstOrDefault(),
                UnreadCount = c.Messages.Count(m => m.ReceiverId == userId && !m.IsRead)
            })
            .ToListAsync();

        return Ok(conversations);
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartConversation([FromBody] string otherUserId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(otherUserId) || otherUserId == userId)
        {
            return BadRequest("Invalid conversation participant");
        }

        var otherUserExists = await _context.Users.AnyAsync(u => u.Id == otherUserId);
        if (!otherUserExists)
        {
            return NotFound("User not found");
        }
        
        var existingConversation = await _context.Conversations
            .FirstOrDefaultAsync(c => 
                (c.User1Id == userId && c.User2Id == otherUserId) ||
                (c.User1Id == otherUserId && c.User2Id == userId));

        if (existingConversation != null)
            return Ok(existingConversation.Id);

        var conversation = new Conversation
        {
            User1Id = userId,
            User2Id = otherUserId
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        return Ok(conversation.Id);
    }

    [HttpGet("messages/{conversationId}")]
    public async Task<IActionResult> GetMessages(int conversationId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var canAccessConversation = await _context.Conversations
            .AnyAsync(c => c.Id == conversationId && (c.User1Id == userId || c.User2Id == userId));

        if (!canAccessConversation)
        {
            return NotFound();
        }

        var messages = await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        return Ok(messages);
    }
}
