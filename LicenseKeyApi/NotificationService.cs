public class NotificationService
{
    public string NotificationVN { get; private set; } = "";
    public string NotificationBRA { get; private set; } = "";
    public string LinkdownVN { get; private set; } = "http://nhattan.online/VPT.rar";
    public string LinkdownBRA { get; private set; } = "http://nhattan.online/MCBot.rar";
    public string VietNamDomain { get; private set; } = "https://s3-vuaphapthuat.goplay.vn/s/s";    
    public string BrazilDomain { get; private set; } = "https://magicx.xcloudgame.com/s/s";
    public string NeoDomain { get; private set; } = "https://main.vpt100.pages.dev/s/s";
    public static string releaseUpdateNotes { get; private set; } = "";

    // ✅ GET methods
    public string GetNotificationVN() => NotificationVN;
    public string GetNotificationBRA() => NotificationBRA;
    public string GetLinkdownVN() => LinkdownVN;
    public string GetLinkdownBRA() => LinkdownBRA;
    public string GetVietNamDomain() => VietNamDomain;
    public string GetBrazilDomain() => BrazilDomain;
    public string GetNeoDomain() => NeoDomain;
    public string GetReleaseUpdateNotes() => releaseUpdateNotes;
    // ✅ UPDATE methods
    public void UpdateNotificationVN(string newMessage) => NotificationVN = newMessage;
    public void UpdateNotificationBRA(string newMessage) => NotificationBRA = newMessage;
    public void UpdateLinkdownVN(string newLink) => LinkdownVN = newLink;
    public void UpdateLinkdownBRA(string newLink) => LinkdownBRA = newLink;
    public void UpdateDomainVN(string newDomain) => VietNamDomain = newDomain;
    public void UpdateDomainBrazil(string newDomain) => BrazilDomain = newDomain;
    public void UpdateDomainNeo(string newDomain) => NeoDomain = newDomain;
    public void UpdateReleaseUpdateNotes(string newNotes) => releaseUpdateNotes = newNotes;
}

