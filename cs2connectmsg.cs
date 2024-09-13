using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Entities;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CS2connectmsg;

[MinimumApiVersion(247)]
public class CS2connectmsgConfig : BasePluginConfig
{
    [JsonPropertyName("PlayerWelcomeMessage")] public bool PlayerWelcomeMessage { get; set; } = true;
    [JsonPropertyName("Timer")] public float Timer { get; set; } = 5.0f;

}
public class CS2connectmsg : BasePlugin, IPluginConfig<CS2connectmsgConfig>
{
    public override string ModuleName => "ConnectMSG";
    public override string ModuleDescription => "Simple connect/disconnect messages";
    public override string ModuleAuthor => "verneri";
    public override string ModuleVersion => "1.3";

    public static Dictionary<ulong, bool> LoopConnections = new Dictionary<ulong, bool>();

    public CS2connectmsgConfig Config { get; set; } = new();

    public void OnConfigParsed(CS2connectmsgConfig config)
    {
        Config = config;
    }
    public override void Load(bool hotReload)
    {
        Console.WriteLine($"loaded successfully! (Version {ModuleVersion})");
    }

    [GameEventHandler]
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        if (@event == null) return HookResult.Continue;
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;
        var steamid = player.SteamID;
        var Name = player.PlayerName;
        string country = GetCountry(player.IpAddress?.Split(":")[0] ?? "Unknown");

        if (LoopConnections.ContainsKey(steamid))
        {
            LoopConnections.Remove(steamid);
        }

        Console.WriteLine($"[{ModuleName}] {Name} has connected!");
        Server.PrintToChatAll($"{Localizer["playerconnect", Name, country]}");

        if (Config.PlayerWelcomeMessage)
        {
            AddTimer(Config.Timer, () =>
            {
                player.PrintToChat($"{Localizer["playerwelcomemsg", Name]}");
                player.PrintToChat($"{Localizer["playerwelcomemsgnextline"]}");
            });
        }
        

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnectPre(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event == null) return HookResult.Continue;
        info.DontBroadcast = true;

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event == null) return HookResult.Continue;
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;
        var reason = @event.Reason;
        var Name = player.PlayerName;
        string country = GetCountry(player.IpAddress?.Split(":")[0] ?? "Unknown");

        info.DontBroadcast = true;

        
            if(reason == 54 || reason == 55 || reason == 57)
            {
                if (!LoopConnections.ContainsKey(player.SteamID))
                {
                    LoopConnections.Add(player.SteamID, true);
                }
                if (LoopConnections.ContainsKey(player.SteamID))
                {
                    return HookResult.Continue;
                }
            }

        Console.WriteLine($"[{ModuleName}] {Name} has disconnected!");
        Server.PrintToChatAll($"{Localizer["playerdisconnect", Name, country]}");

        

        return HookResult.Continue;
    }


    public string GetCountry(string ipAddress)
    {
        try
        {
            using var reader = new DatabaseReader(Path.Combine(ModuleDirectory, "GeoLite2-Country.mmdb"));
            var response = reader.Country(ipAddress);
            return response?.Country?.IsoCode ?? "Unknown";
        }
        catch (AddressNotFoundException)
        {
            return "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

}