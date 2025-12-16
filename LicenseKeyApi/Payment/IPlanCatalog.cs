using Microsoft.Extensions.Options;

public class ProductCfg
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public long Monthly { get; set; }          // Giá 1 tháng (đơn vị VND)
    public bool Active { get; set; } = true;
    public int Sort { get; set; } = 0;
}

public class DurationCfg
{
    public string Code { get; set; } = default!;  // D1,W1,M1,M3,M6,M12...
    public string Name { get; set; } = default!;
    public int DeltaDays { get; set; }            // 1,7,30,90,180,365...
    public string Kind { get; set; } = "MONTH";   // DAY/WEEK/MONTH/M3/M6/M12
}

public class PlanCatalogCfg
{
    public string Currency { get; set; } = "VND";
    public double DayMarkup { get; set; } = 1.0;    // Hệ số so với tháng/30
    public double WeekMarkup { get; set; } = 1.0;   // Hệ số so với tháng/4
    public long Rounding { get; set; } = 1000;      // Bậc làm tròn (vd 1000)
    public Dictionary<string, double> LongTermDiscount { get; set; } = new()
    {
        ["M3"] = 0.05,
        ["M6"] = 0.1,
        ["M9"] = 0.15,
        ["M12"] = 0.20
    };
    public List<ProductCfg> Products { get; set; } = new();
    public List<DurationCfg> Durations { get; set; } = new();
}

public record PlanItem(string PlanCode, string Name, int DeltaDays, long Price);

public interface IPlanCatalog
{
    bool ChangeActive(string planCode, bool status);
    IEnumerable<ProductCfg> ListProduct();
    IEnumerable<PlanItem> ListForLicense(string licenseKey);
    PlanItem? Resolve(string planCode);
}

public sealed class PlanCatalog : IPlanCatalog
{
    private PlanCatalogCfg _cfg;
    private readonly IPricingPolicy _policy;

    public PlanCatalog(IOptions<PlanCatalogCfg> opt, IPricingPolicy policy)
    {
        _cfg = opt.Value;
        _policy = policy;
    }

    public IEnumerable<PlanItem> ListForLicense(string licenseKey)
    {
        foreach (var p in _cfg.Products.Where(x => x.Active).OrderBy(x => x.Sort))
        {
            foreach (var d in _cfg.Durations)
            {
                var raw = CalcRaw(p.Monthly, d.Kind);
                var price = _policy.Apply(p.Code, d.Kind, raw);

                yield return new PlanItem(
                    PlanCode: $"{p.Code}_{d.Code}",
                    Name: $"{p.Name} - {d.Name}",
                    DeltaDays: d.DeltaDays,
                    Price: price
                );
            }
        }
    }

    public IEnumerable<ProductCfg> ListProduct()
    {
        return _cfg.Products.OrderBy(x => x.Sort); 
    }
    public bool ChangeActive(string planCode, bool status)
    {
        try
        {
            _cfg.Products.FirstOrDefault(x => x.Code == planCode).Active = status;
            return true;
        }
        catch(Exception ex)
        {
            return false;
        }
       
    }

    public PlanItem? Resolve(string planCode) =>
        ListForLicense("").FirstOrDefault(x =>
            x.PlanCode.Equals(planCode, StringComparison.OrdinalIgnoreCase));

    private long CalcRaw(long monthly, string kind)
    {
        double v = kind switch
        {
            "DAY" => (monthly / 30.0) * _cfg.DayMarkup,
            "3DAY" => (monthly / 30.0) * _cfg.DayMarkup * 3,
            "WEEK" => (monthly / 4.0) * _cfg.WeekMarkup,
            "MONTH" => monthly,
            "M3" => monthly * 3 * (1 - GetDiscount("M3")),
            "M6" => monthly * 6 * (1 - GetDiscount("M6")),
            "M9" => monthly * 9 * (1 - GetDiscount("M9")),
            "M12" => monthly * 12 * (1 - GetDiscount("M12")),
            _ => monthly
        };

        var r = _cfg.Rounding <= 0 ? 1 : _cfg.Rounding;
        long rounded = (long)(Math.Ceiling(v / r) * r);
        return rounded;
    }

    private double GetDiscount(string key) =>
        _cfg.LongTermDiscount != null && _cfg.LongTermDiscount.TryGetValue(key, out var d)
            ? Math.Clamp(d, 0, 0.99)
            : 0.0;

    
}

