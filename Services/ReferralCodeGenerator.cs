using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MCBusiness.Models;

namespace MCBusiness.Services
{
    public interface IReferralCodeGenerator
    {
        Task<string> GenerateUniqueReferralCodeAsync();
        bool IsValidReferralCode(string code);
        Task<bool> IsReferralCodeAvailableAsync(string code);
    }

    public class ReferralCodeGenerator : IReferralCodeGenerator
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ReferralCodeGenerator> _logger;

        // Caractères excluant ceux qui peuvent être confondus (0, O, I, 1, l, etc.)
        private const string AllowedChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const int CodeLength = 8;
        private const int MaxRetries = 10;

        public ReferralCodeGenerator(AppDbContext context, ILogger<ReferralCodeGenerator> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<string> GenerateUniqueReferralCodeAsync()
        {
            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var code = GenerateRandomCode();
                
                if (await IsReferralCodeAvailableAsync(code))
                {
                    _logger.LogInformation("Code de référence généré avec succès: {Code} (Tentative {Attempt})", code, attempt + 1);
                    return code;
                }
                
                _logger.LogWarning("Code de référence en conflit: {Code} (Tentative {Attempt})", code, attempt + 1);
            }

            // Fallback avec timestamp si échec après plusieurs tentatives
            var timestampCode = GenerateTimestampBasedCode();
            
            if (await IsReferralCodeAvailableAsync(timestampCode))
            {
                _logger.LogInformation("Code de référence basé sur timestamp généré: {Code}", timestampCode);
                return timestampCode;
            }

            throw new InvalidOperationException("Impossible de générer un code de référence unique après plusieurs tentatives");
        }

        public bool IsValidReferralCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;

            if (code.Length != CodeLength)
                return false;

            // Vérifier que tous les caractères sont autorisés
            return code.All(c => AllowedChars.Contains(c));
        }

        public async Task<bool> IsReferralCodeAvailableAsync(string code)
        {
            if (!IsValidReferralCode(code))
                return false;

            try
            {
                var exists = await _context.Users
                    .AnyAsync(u => u.ReferralCode == code);

                return !exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la vérification de disponibilité du code: {Code}", code);
                return false;
            }
        }

        private string GenerateRandomCode()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[CodeLength];
            var result = new StringBuilder(CodeLength);

            for (int i = 0; i < CodeLength; i++)
            {
                rng.GetBytes(bytes, i, 1);
                var index = bytes[i] % AllowedChars.Length;
                result.Append(AllowedChars[index]);
            }

            return result.ToString();
        }

        private string GenerateTimestampBasedCode()
        {
            // Utiliser les 6 derniers caractères du timestamp Unix + 2 caractères aléatoires
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var timestampStr = timestamp.ToString();
            
            // Prendre les 6 derniers chiffres et les convertir en base-32
            var lastSixDigits = long.Parse(timestampStr.Substring(Math.Max(0, timestampStr.Length - 6)));
            var base32Timestamp = ConvertToBase32(lastSixDigits, 6);
            
            // Ajouter 2 caractères aléatoires
            using var rng = RandomNumberGenerator.Create();
            var randomBytes = new byte[2];
            rng.GetBytes(randomBytes);
            
            var randomChars = string.Concat(randomBytes.Select(b => AllowedChars[b % AllowedChars.Length]));
            
            return base32Timestamp + randomChars;
        }

        private string ConvertToBase32(long value, int minLength)
        {
            if (value == 0) return new string(AllowedChars[0], minLength);

            var result = new StringBuilder();
            var baseValue = AllowedChars.Length;

            while (value > 0)
            {
                result.Insert(0, AllowedChars[(int)(value % baseValue)]);
                value /= baseValue;
            }

            // Padding avec le premier caractère si nécessaire
            while (result.Length < minLength)
            {
                result.Insert(0, AllowedChars[0]);
            }

            return result.ToString();
        }

        // Méthodes utilitaires pour la migration de codes existants
        public async Task<List<string>> FindDuplicateReferralCodesAsync()
        {
            var duplicates = await _context.Users
                .GroupBy(u => u.ReferralCode)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToListAsync();

            return duplicates;
        }

        public async Task<int> FixDuplicateReferralCodesAsync()
        {
            var duplicates = await FindDuplicateReferralCodesAsync();
            var fixedCount = 0;

            foreach (var duplicateCode in duplicates)
            {
                var usersWithDuplicateCode = await _context.Users
                    .Where(u => u.ReferralCode == duplicateCode)
                    .OrderBy(u => u.Id)
                    .ToListAsync();

                // Garder le premier utilisateur avec ce code, changer les autres
                for (int i = 1; i < usersWithDuplicateCode.Count; i++)
                {
                    var user = usersWithDuplicateCode[i];
                    var newCode = await GenerateUniqueReferralCodeAsync();
                    user.ReferralCode = newCode;
                    fixedCount++;

                    _logger.LogInformation("Code de référence dupliqué corrigé pour l'utilisateur {UserId}: {OldCode} -> {NewCode}", 
                        user.Id, duplicateCode, newCode);
                }
            }

            if (fixedCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            return fixedCount;
        }
    }
}
