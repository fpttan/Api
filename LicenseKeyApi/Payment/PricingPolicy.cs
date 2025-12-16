public interface IPricingPolicy
{
    long Apply(string productCode, string kind, long rawPrice);
}

public sealed class DefaultPricingPolicy : IPricingPolicy
{
    private const long MinDefault = 10_000;
    private const long MinComboDay = 15_000;
    private const long MinThapDay = 15000;
    public long Apply(string productCode, string kind, long rawPrice)
    {
        long price = rawPrice;

        // Min riêng cho Combo (C_*) loại DAY
        if (string.Equals(productCode, "C", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(kind, "DAY", StringComparison.OrdinalIgnoreCase) &&
            price < MinComboDay)
        {
            price = MinComboDay;
        }
        //Min riêng cho Thap (T_*) loại DAY
        else if (string.Equals(productCode, "T", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(kind, "DAY", StringComparison.OrdinalIgnoreCase) &&
            price < MinThapDay)
        {
            price = MinThapDay;
        }
        // Min chung
        if (price < MinDefault) price = MinDefault;

        return price;
    }
}
