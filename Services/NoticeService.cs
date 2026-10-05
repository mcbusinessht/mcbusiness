using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MCBusiness.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MCBusiness.Services
{
    public class NoticeService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NoticeService(
            AppDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<string> GetAnnonceAsync()
        {
            List<Notice> messages = await _context.Notices
                .Where(n => n.Type == NoticeType.TopBar && n.EndDate > DateTimeOffset.UtcNow)
                .OrderBy(a => a.StartDate)
                .ToListAsync();
            string message = "";
            foreach (var msg in messages)
            {
                message =  msg.Content.Replace("\n", "<br/>") + ". " + message;
            }
            return message;
        }

        public async Task<int> GetMessageCountAsync(string userId){
            return await _context.Messages
                .Where(m=>m.ReceiverId == userId && m.IsRead == false)
                .CountAsync();
        }

        public async Task<int> GetNotificationCountAsync(string userId){
            return await _context.Notifications
            .Where(m=> m.UserId == userId  && m.IsRead == false)
            .CountAsync();
        }
        public async Task<DateTimeOffset?> GetLastVisitAsync(string userId)
        {
            var lastLogin = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.LastLoginDate)
                .FirstOrDefaultAsync();

            return lastLogin?.ToLocalTime();
        }




        public async Task<int> GetCartCountAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return 0;

            var cart = await _context.Carts
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
                return 0;

            return await _context.CartItems
                .Where(ci => ci.CartId == cart.Id)
                .CountAsync();
        }
        public async Task<int> GetNewCommandeAsync(string userId){
            var companyId = await _context.Companies.Where(c=>c.SellerId == userId).Select(c=> c.Id).FirstOrDefaultAsync();

            return await _context.OrderProducts
                .Include(o => o.Order)
                .Where(o => o.CompanyId == companyId && o.Order.Status == OrderStatus.Paid) 
                .Select(o => o.OrderId)
                .Distinct()
                .CountAsync();  
        }
    }
}
