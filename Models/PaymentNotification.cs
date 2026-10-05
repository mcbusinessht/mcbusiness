using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MCBusiness.Models
{
    public class PaymentNotification
{
    public string TransactionId { get; set; }
    public string OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; }
    public string Reference { get; set; }
    public string Timestamp { get; set; }
}
}
