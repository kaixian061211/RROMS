namespace RROMS.Web.Models.Payments;

/// <summary>Values stored in Payment.PaymentMethod. They must match the CHECK constraint on the table.</summary>
public static class PaymentMethod
{
    public const string Cash = "cash";
    public const string Card = "card";
    public const string OnlineBanking = "online_banking";
    public const string EWallet = "e-wallet";
    public const string PayAtCounter = "pay_at_counter";

    public static readonly string[] All = { Cash, Card, OnlineBanking, EWallet, PayAtCounter };

    /// <summary>Methods that are paid through Stripe.</summary>
    public static readonly string[] Online = { Card, OnlineBanking, EWallet };

    /// <summary>What a customer may choose while checking out.</summary>
    public static readonly string[] CheckoutChoices = { Card, OnlineBanking, EWallet, PayAtCounter };

    /// <summary>What staff may record when they collect money at the restaurant.</summary>
    public static readonly string[] CounterMethods = { Cash, Card, EWallet };

    public static bool IsOnline(string method) => Online.Contains(method);
}