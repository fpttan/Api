using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/plans/")]
public class PlansController : ControllerBase
{
    private readonly ApiDbContext _context;

    private readonly IPlanCatalog _catalog;
    public PlansController(IPlanCatalog catalog, ApiDbContext context)
    {
        _catalog = catalog;
        _context = context;
    }
    private const long MinDefault = 10_000; // 10k VND
    private const long MinCombo = 15_000; // 15k VND cho Combo (C_*)

    [HttpGet("{licenseKey}")]
    public IActionResult List([FromRoute] string licenseKey,[FromHeader(Name = "X-App-Version")] string? version,[FromHeader(Name = "X-App-Sha")] string? sha)
    {
        if (!IsValidLicense(licenseKey, out _))
            return NotFound("License không tồn tại.");

        bool isNewClient = IsVersionGreaterThan(version, "3.1.4");
        if(isNewClient)
        {
            var plans = _catalog.ListForLicense(licenseKey)
                 .Where(p => !_blockedPlans.Contains(p.PlanCode))
                 .OrderBy(p => p.PlanCode.Split('_')[0])   // S, M, V, C
                 .ThenBy(p => p.DeltaDays)
                 .ToList();
            return Ok(plans);
        }
        else
        {
            var plans = _catalog.ListForLicense(licenseKey)
                 .Where(p => !_blockedPlansoldversion.Contains(p.PlanCode))
                 .OrderBy(p => p.PlanCode.Split('_')[0])   // S, M, V, C
                 .ThenBy(p => p.DeltaDays)
                 .ToList();
            return Ok(plans);
        }
     
    }

    private static readonly HashSet<string> _blockedPlans =new(StringComparer.OrdinalIgnoreCase)
    {
        "S_1D","S_3M","S_6M","S_9M","S_12M",
        "T_7D","T_1M","T_3M","T_6M","T_9M","T_12M",
        "N_7D","N_1M","N_3M","N_6M","N_9M","N_12M"
    };
    private static readonly HashSet<string> _blockedPlansoldversion = new(StringComparer.OrdinalIgnoreCase)
    {
        "S_1D","S_3D","S_3M","S_6M","S_9M","S_12M",
        "M_3D",
        "V_3D",
        "C_3D",
        "T_3D","T_7D","T_1M","T_3M","T_6M","T_9M","T_12M",
        "N_3D","N_7D","N_1M","N_3M","N_6M","N_9M","N_12M"
    };
    private static bool IsVersionGreaterThan(string? clientVersion, string targetVersion)
    {
        if (string.IsNullOrWhiteSpace(clientVersion))
            return false;

        if (!Version.TryParse(clientVersion, out var vClient))
            return false;

        if (!Version.TryParse(targetVersion, out var vTarget))
            return false;

        return vClient > vTarget;
    }
    [HttpGet("listplan")]
    public IActionResult actionResult([FromHeader(Name = "X-API-KEY")] string apiKey)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey)
            return Forbid();
        var product = _catalog.ListProduct()
            .OrderBy(p => p.Sort)
            .ToList();
        return Ok(product);
    }
    [HttpPost("changesplans/{plancodes}/{status}")]
    public IActionResult actionResult2([FromHeader(Name = "X-API-KEY")] string apiKey, [FromRoute] string plancodes, [FromRoute] bool status)
    {
        if (apiKey != ApiKeyMiddleware.AdminApiKey)
            return Forbid();
        return Ok(_catalog.ChangeActive(plancodes, status));
    }
    private bool IsValidLicense(string? key, out License? license)
    {
        license = null;
        if (string.IsNullOrWhiteSpace(key)) return false;
        license = _context.Licenses.Find(key);
        return license != null;
    }


}
