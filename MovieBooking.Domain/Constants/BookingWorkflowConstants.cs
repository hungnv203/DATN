namespace MovieBooking.Domain.Constants;

public static class BookingChannels
{
    public const string CustomerOnline = "CustomerOnline";
    public const string PointOfSale = "PointOfSale";
}

public static class BookingStatuses
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
}

public static class PaymentStatuses
{
    public const string Pending = "Pending";
    public const string Success = "Success";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
}

public static class PaymentMethods
{
    public const string VnPay = "VNPAY";
    public const string Cash = "Cash";
}

public static class VnPayStatuses
{
    public const string Success = "00";

    public static readonly IReadOnlySet<string> ConfirmedFailureResponseCodes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "07", "09", "10", "11", "12", "13", "24", "51", "65", "75", "79", "99"
        };

    public static readonly IReadOnlySet<string> ConfirmedFailureTransactionStatuses =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "01", "02", "04", "05", "06", "07", "09"
        };
}

public static class TicketStatuses
{
    public const string Held = "Held";
    public const string Booked = "Booked";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
}

public static class LoyaltyEffectTypes
{
    public const string Redeem = "Redeem";
    public const string RedeemReturn = "RedeemReturn";
    public const string Earn = "Earn";
}

public static class PaymentOperationTypes
{
    public const string PosConfirmation = "PosConfirmation";
    public const string PosCancellation = "PosCancellation";
    public const string ProviderNotification = "ProviderNotification";
}

public static class PaymentOperationResults
{
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string ReviewRequired = "ReviewRequired";
}
