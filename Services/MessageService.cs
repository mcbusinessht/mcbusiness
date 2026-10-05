using System;
using Twilio;
using Twilio.Rest.Verify.V2.Service;

namespace MCBusiness.Services
{
    public class MessageService
    {
        private const string VerifyServiceSid = "VA62b418eac8e3c2e5b0985e147947838f"; // SID du service Verify

        public MessageService(string accountSid, string authToken)
        {
            if (string.IsNullOrEmpty(accountSid) || string.IsNullOrEmpty(authToken))
            {
                throw new ArgumentException("Twilio Account SID and Auth Token cannot be null or empty.");
            }

            TwilioClient.Init(accountSid, authToken);
        }

        // Méthode pour envoyer un code de vérification
        public string SendVerificationCode(string phoneNumber)
        {
            var verification = VerificationResource.Create(
                to: phoneNumber,
                channel: "sms",
                pathServiceSid: VerifyServiceSid
            );

            return verification.Status; // Renvoie le statut (e.g., "pending")
        }

        // Méthode pour vérifier un code
        public bool VerifyCode(string phoneNumber, string code)
        {
            var verificationCheck = VerificationCheckResource.Create(
                to: phoneNumber,
                code: code,
                pathServiceSid: VerifyServiceSid
            );

            return verificationCheck.Status == "approved";
        }
    }
}
