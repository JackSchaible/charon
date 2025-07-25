namespace API.Models;

public class SteamPlayer
{
    public string steamid { get; set; } = string.Empty;
    public int communityvisibilitystate { get; set; }
    public int profilestate { get; set; }
    public string personaname { get; set; } = string.Empty;
    public long lastlogoff { get; set; }
    public int commentpermission { get; set; }
    public string profileurl { get; set; } = string.Empty;
    public string avatar { get; set; } = string.Empty;
    public string avatarmedium { get; set; } = string.Empty;
    public string avatarfull { get; set; } = string.Empty;
    public int personastate { get; set; }
    public string? realname { get; set; }
    public string primaryclanid { get; set; } = string.Empty;
    public long timecreated { get; set; }
    public int personastateflags { get; set; }
    public string? gameextrainfo { get; set; }
    public string? gameid { get; set; }
    public string? loccountrycode { get; set; }
    public string? locstatecode { get; set; }
    public int? loccityid { get; set; }
}
