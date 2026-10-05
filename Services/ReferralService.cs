using System.Collections.Generic;
using System.Linq;
using MCBusiness.Models;

namespace MCBusiness.Services
{
    public class ReferralService
    {
        private readonly AppDbContext _context;

        public ReferralService(AppDbContext context)
        {
            _context = context;
        }

        public UserTreeNode BuildUserTree(string userId, int generation = 0, HashSet<string> visitedUsers = null)
        {
            // SÉCURITÉ: Prévention des cycles infinis
            visitedUsers ??= new HashSet<string>();
            
            if (visitedUsers.Contains(userId))
            {
                // Cycle détecté - arrêter la récursion
                return new UserTreeNode
                {
                    UserId = userId,
                    UserName = "[CYCLE DÉTECTÉ]",
                    Generation = generation,
                    Referrals = new List<UserTreeNode>()
                };
            }
            
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return null;

            // Limiter la profondeur pour éviter les arbres trop profonds
            if (generation > 10)
            {
                return new UserTreeNode
                {
                    UserId = user.Id,
                    UserName = user.UserName + " [PROFONDEUR LIMITÉE]",
                    Generation = generation,
                    Referrals = new List<UserTreeNode>()
                };
            }

            visitedUsers.Add(userId);

            var referrals = _context.Referrals
                .Where(r => r.ReferrerId == userId)
                .ToList();

            var userNode = new UserTreeNode
            {
                UserId = user.Id,
                UserName = user.UserName,
                Generation = generation,
                Referrals = referrals
                    .Select(r => BuildUserTree(r.ReferredId, generation + 1, new HashSet<string>(visitedUsers)))
                    .Where(node => node != null)
                    .ToList()
            };

            visitedUsers.Remove(userId); // Nettoyage pour autres branches

            return userNode;
        }

        public async Task<bool> ValidateReferralTreeIntegrityAsync()
        {
            // Détecter les cycles dans la base de données
            var allReferrals = _context.Referrals.ToList();
            var cycles = DetectCycles(allReferrals);
            
            if (cycles.Any())
            {
                // Logger les cycles détectés
                foreach (var cycle in cycles)
                {
                    var cycleInfo = string.Join(" -> ", cycle) + " -> " + cycle.First();
                    Console.WriteLine($"CYCLE DÉTECTÉ: {cycleInfo}");
                }
                return false;
            }
            
            return true;
        }

        private List<List<string>> DetectCycles(List<Referral> referrals)
        {
            var cycles = new List<List<string>>();
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();
            var path = new List<string>();

            var graph = referrals.GroupBy(r => r.ReferrerId)
                                .ToDictionary(g => g.Key, g => g.Select(r => r.ReferredId).ToList());

            foreach (var userId in graph.Keys)
            {
                if (!visited.Contains(userId))
                {
                    DetectCyclesHelper(userId, graph, visited, recursionStack, path, cycles);
                }
            }

            return cycles;
        }

        private bool DetectCyclesHelper(string userId, Dictionary<string, List<string>> graph,
            HashSet<string> visited, HashSet<string> recursionStack, List<string> path, List<List<string>> cycles)
        {
            visited.Add(userId);
            recursionStack.Add(userId);
            path.Add(userId);

            if (graph.ContainsKey(userId))
            {
                foreach (var neighbor in graph[userId])
                {
                    if (!visited.Contains(neighbor))
                    {
                        if (DetectCyclesHelper(neighbor, graph, visited, recursionStack, path, cycles))
                        {
                            return true;
                        }
                    }
                    else if (recursionStack.Contains(neighbor))
                    {
                        // Cycle trouvé
                        var cycleStart = path.IndexOf(neighbor);
                        var cycle = path.Skip(cycleStart).ToList();
                        cycles.Add(cycle);
                        return true;
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
            recursionStack.Remove(userId);
            return false;
        }
    }
}
