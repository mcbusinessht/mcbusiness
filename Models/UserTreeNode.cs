using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MCBusiness.Models
{
    public class UserTreeNode
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public int Generation { get; set; }
        public List<UserTreeNode> Referrals { get; set; } = new List<UserTreeNode>();
        
    }
}


