namespace MCBusiness.Services
{
    public static class MonCashPricing
    {
        public const decimal DepositFeeRate = 0.02m;

        public static decimal CalculateDepositGross(decimal netAmount)
        {
            return decimal.Round(
                netAmount * (1m + DepositFeeRate),
                2,
                MidpointRounding.AwayFromZero);
        }
    }
}
