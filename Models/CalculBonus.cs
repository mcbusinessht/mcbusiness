using System;
using System.Linq;
using System.Collections.Generic;
using MCBusiness.Models;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Interfaces;
using MCBusiness.Services;

public class CalculBonus
{
    public int IdPlan { get; set; }
    public string IdUser { get; set; }
    private readonly AppDbContext _context;
    public decimal Gain { get; private set; }
    public Wallet wallet{get; set;}
    public Transaction transaction {get; set;}
    private readonly  IEmailSender _emailSender;
    private readonly INotificationService _notificationService;

    public CalculBonus(int idPlan, string idUser, AppDbContext context, IEmailSender emailSender, INotificationService notificationService)
    {
        IdPlan = idPlan;
        IdUser = idUser;
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _emailSender= emailSender;
        _notificationService = notificationService;
    }

    public async Task<decimal> CalculGainReferrerAsync()
    {
        if (DateTimeOffset.UtcNow >= DateTimeOffset.MinValue)
        {
            throw new NotSupportedException(
                "CalculBonus est desactive: les bonus et commissions doivent passer par AmbassadorCommissionService et IPlatformTransactionAuditService.");
        }

        var planUser =await _context.AmbassadorPlans.FirstOrDefaultAsync(p => p.Id == IdPlan);
        if (planUser == null)
        {
            Console.WriteLine("Erreur : Plan introuvable.");
            return 0;
        }

        var currentUser =await _context.Users.FirstOrDefaultAsync(u => u.Id == IdUser);
        if (currentUser == null)
        {
            Console.WriteLine("Erreur : Utilisateur introuvable.");
            return 0;
        }

        var referrals =await _context.Referrals.Where(r => r.ReferredId == currentUser.Id).ToListAsync();
        if (!referrals.Any())
        {
            Console.WriteLine("Erreur : Aucune référence trouvée pour cet utilisateur.");
            return 0;
        }

        // Enregistrement du montant de refund dans le portefeuille de transaction de l'utilisateur
        var walletTransactionRefund = await _context.WalletTransactions.FirstOrDefaultAsync(wt => wt.UserId == currentUser.Id && wt.Type == WalletTransactionType.Refund);
        if(walletTransactionRefund != null)
        {
            walletTransactionRefund.Balance += planUser.Price * 0.25m;
            _context.WalletTransactions.Update(walletTransactionRefund);
        }
        else
        {
            var newWalletTransaction = new WalletTransaction
            {
                UserId = currentUser.Id,
                Balance = planUser.Price * 0.25m,
                Type = WalletTransactionType.Refund,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            _context.WalletTransactions.Add(newWalletTransaction);
        }


        var referredSubscriptionCount =await _context.AmbassadorSubscriptions
            .Where(s => s.UserId == currentUser.Id).CountAsync();
        if(referredSubscriptionCount>1){
            return 0;  
        }

        var referredSubscription = await _context.AmbassadorSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == currentUser.Id && s.Status == SubscriptionStatus.Active);


        if (referredSubscription == null)
        {
            Console.WriteLine("Erreur : Aucune souscription trouvée pour cet utilisateur.");
            return 0;
        }

        foreach (var referral in referrals)
        {
    
            var subscriptionReferrer =await _context.AmbassadorSubscriptions
                .FirstOrDefaultAsync(s => s.Status == SubscriptionStatus.Active && s.UserId == referral.ReferrerId);

            if (subscriptionReferrer == null) continue;

            var subscriptionPlanReferrer =await _context.AmbassadorPlans.FirstOrDefaultAsync(p => p.Id == subscriptionReferrer.PlanId);
            if (subscriptionPlanReferrer == null) continue;

            // SIMPLIFIÉ: Commission unique, plus de multi-génération
            decimal bonusAmount = (subscriptionPlanReferrer.AmbassadorCommissionPercentage * planUser.Price) / 100;

            if (bonusAmount == 0) continue;

            wallet =await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == referral.ReferrerId);
            if (wallet == null)
            {
                Console.WriteLine($"Erreur : Portefeuille introuvable pour l'utilisateur {referral.ReferrerId}.");
                continue;
            }

            wallet.Balance += bonusAmount;
            // SUPPRIMÉ: referral.BonusAmount - maintenant géré par PointTransaction
            var ReferredUser=await _context.Users.FirstOrDefaultAsync(u=>u.Id==referral.ReferredId);
            transaction = new Transaction
            {
                UserId = referral.ReferrerId,
                Amount = bonusAmount,
                Type = TransactionType.Bonus,
                Description = $"Bonus reçu sur filleul '{ReferredUser.UserName}' de votre {referral.Generation}e generation.",
                Status = TransactionStatus.Completed,
                OrderId = await GenerateUniqueOrderIdAsync(),
            };
            _context.Transactions.Add(transaction);
            _context.Wallets.Update(wallet);

            //Mise à jour du Wallet (balance du système)
            var platformWallet = await _context.Wallets.Include(w => w.User)
                .FirstOrDefaultAsync(w => w.User.ReferralCode == "00000000");

            if (platformWallet == null)
                throw new Exception("Le portefeuille principal (plateforme) est introuvable.");

            if (platformWallet.Balance < bonusAmount)
                throw new InvalidOperationException("Solde plateforme insuffisant pour payer ce bonus.");

            var platformTransaction = new PlatformTransaction
            {
                Type = PlatformTransactionType.Expense,
                Category = PlatformTransactionCategory.CommissionReferrer,
                AmbassadorSubscriptionId = referredSubscription.Id, 
                Amount = -bonusAmount,
                Description = $"Commission du parrain sur cet abonnement",
                WalletId = platformWallet.Id,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            
            await _context.PlatformTransactions.AddAsync(platformTransaction);
            platformWallet.Balance -= bonusAmount;
            _context.Wallets.Update(platformWallet); 



            //Mis a jour du wallet Transaction Refund en reduisant le bonus de la balance
            var walletTransactionRefund2 = await _context.WalletTransactions.FirstOrDefaultAsync(wt => wt.UserId == referral.ReferrerId && wt.Type == WalletTransactionType.Refund);
            if(walletTransactionRefund2 != null)
            {
                walletTransactionRefund2.Balance -= bonusAmount;
                _context.WalletTransactions.Update(walletTransactionRefund2); 
            }
            _context.Entry(referral).State = EntityState.Modified;
            try{
                var notification = new Notification
                {
                    UserId = referral.ReferrerId,
                    Title = "Bonus",
                    Message = $"Vous avez reçu un bonus de {bonusAmount} sur la génération {referral.Generation} de votre filleul '{ReferredUser.UserName}'.",
                };
                await _notificationService.NotifyAsync(notification);
            }catch(Exception ex){
                Console.WriteLine($"Erreur lors de l'envoi de la notification: {ex}.");
            }

            // Enregistrement des gains dans le portefeuille de l'utilisateur
            var walletTransactionBonus = await _context.WalletTransactions.FirstOrDefaultAsync(wt => wt.UserId == referral.ReferrerId && wt.Type == WalletTransactionType.Bonus);
            if(walletTransactionBonus != null)
            {
                walletTransactionBonus.Balance += bonusAmount;
                _context.WalletTransactions.Update(walletTransactionBonus);
            }
            else
            {
                var newWalletTransaction = new WalletTransaction
                {
                    UserId = referral.ReferrerId,
                    Balance = bonusAmount,
                    Type = WalletTransactionType.Bonus,
                    UpdatedAt = DateTimeOffset.UtcNow,
                };
                _context.WalletTransactions.Add(newWalletTransaction);
            }
            
            Gain += bonusAmount;
        }
        
        try
        {
            await _context.SaveChangesAsync();
            Console.WriteLine("Gains enregistrés avec succès.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de l'enregistrement : {ex.Message}");
        }

        return Gain;
    }
    private async Task<string> GenerateUniqueOrderIdAsync()
        {
            string orderId;
            do
            {
                orderId = new Random().NextInt64(100000000000, 999999999999).ToString();
            } while (await _context.Transactions.AnyAsync(t => t.OrderId == orderId));

            return orderId;
        }

}

