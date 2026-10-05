using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace MCBusiness.Models
{
    public class Message : BaseEntity
    {
        [Required, ForeignKey("Conversation")]
        public int ConversationId { get; set; }
        [Required, ForeignKey("Sender")]
        public string SenderId { get; set; } // Qui a envoyé le message ?
        [Required, ForeignKey("Receiver")]
        public string ReceiverId { get; set; } // Qui l'a reçu ?
        [Required, MaxLength(2000)]
        public string Content { get; set; } // Contenu du message
        [Required]
        public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
        public bool IsRead { get; set; } = false; // Pour indiquer si le message est lu
        public bool IsDeletedBySender { get; set; } = false; // Pour indiquer si le message est supprimé par l'expéditeur
        public bool IsDeletedByReceiver { get; set; } = false; // Pour indiquer si le message est supprimé par le destinataire
        
        public Conversation Conversation { get; set; }
        public ApplicationUser Sender { get; set; }
        public ApplicationUser Receiver { get; set; }
        
    }
}
