using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Entities;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace ConnectMSG;

[MinimumApiVersion(247)]
public class ConnectMSGConfig : BasePluginConfig
{
    [JsonPropertyName("PlayerWelcomeMessage")] public bool PlayerWelcomeMessage { get; set; } = true;
    [JsonPropertyName("Timer")] public float Timer { get; set; } = 5.0f;
    [JsonPropertyName("LogMessagesToDiscord")] public bool LogMessagesToDiscord { get; set; } = true;
    [JsonPropertyName("DiscordWebhook")] public string DiscordWebhook { get; set; } = "";

}
public class ConnectMSG : BasePlugin, IPluginConfig<ConnectMSGConfig>
{
    public override string ModuleName => "ConnectMSG";
    public override string ModuleDescription => "Simple connect/disconnect messages";
    public override string ModuleAuthor => "verneri";
    public override string ModuleVersion => "1.6";

    public static Dictionary<ulong, bool> LoopConnections = new Dictionary<ulong, bool>();

    public ConnectMSGConfig Config { get; set; } = new();

    public void OnConfigParsed(ConnectMSGConfig config)
    {
        Config = config;
    }
    public override void Load(bool hotReload)
    {
        Logger.LogInformation($"loaded successfully! (Version {ModuleVersion})");
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        if (@event == null) return HookResult.Handled;
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Handled;
        var steamid = player.SteamID;
        var Name = player.PlayerName;
        string country = GetCountry(player.IpAddress?.Split(":")[0] ?? "Unknown");
        string playerip = player.IpAddress?.Split(":")[0] ?? "Unknown";

        if (LoopConnections.ContainsKey(steamid))
        {
            LoopConnections.Remove(steamid);
        }

        Console.WriteLine($"[{ModuleName}] {Name} has connected!");
        Server.PrintToChatAll($"{Localizer["playerconnect", Name, country]}");

        if (Config.LogMessagesToDiscord)
        {
            _ = SendWebhookMessageAsEmbedConnected(player.PlayerName, player.SteamID, playerip, country);
        }

        if (Config.PlayerWelcomeMessage)
        {
            AddTimer(Config.Timer, () =>
            {
                player.PrintToChat($"{Localizer["playerwelcomemsg", Name]}");
                player.PrintToChat($"{Localizer["playerwelcomemsgnextline"]}");
            });
        }


        return HookResult.Handled;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerDisconnectPre(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event == null) return HookResult.Handled;
        info.DontBroadcast = true;


        return HookResult.Handled;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event == null) return HookResult.Handled;
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Handled;
        var reason = @event.Reason;
        var Name = player.PlayerName;
        string country = GetCountry(player.IpAddress?.Split(":")[0] ?? "Unknown");
        string playerip = player.IpAddress?.Split(":")[0] ?? "Unknown";


        if (reason == 54 || reason == 55 || reason == 57)
            {
                if (!LoopConnections.ContainsKey(player.SteamID))
                {
                   LoopConnections.Add(player.SteamID, true);
                }
                if (LoopConnections.ContainsKey(player.SteamID))
                {
                   return HookResult.Handled;
                }

            }
        info.DontBroadcast = true;

        Console.WriteLine($"[{ModuleName}] {Name} has disconnected!");
        Server.PrintToChatAll($"{Localizer["playerdisconnect", Name, country]}");

        if (Config.LogMessagesToDiscord) {
            _ = SendWebhookMessageAsEmbedDisconnected(player.PlayerName, player.SteamID, playerip, country);
        }


        return HookResult.Handled;
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

    public async Task SendWebhookMessageAsEmbedConnected(string playerName, ulong steamID, string playerip, string country)
    {
        using (var httpClient = new HttpClient())
        {
            var embed = new
            {
                title = $"{Localizer["Discord.ConnectTitle", playerName]}",
                url = $"https://steamcommunity.com/profiles/{steamID}",
                description = $"{Localizer["Discord.ConnectDescription", country, steamID, playerip]}",
                color = 65280,
                footer = new
                {
                    text = $"{Localizer["Discord.Footer"]}"
                }
            };

            var payload = new
            {
                embeds = new[] { embed }
            };

            var jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(Config.DiscordWebhook, content);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogInformation($"Failed to send message to Discord! code: {response.StatusCode}");
            }
        }
    }

    public async Task SendWebhookMessageAsEmbedDisconnected(string playerName, ulong steamID, string playerip, string country)
    {
        using (var httpClient = new HttpClient())
        {
            var embed = new
            {
                title = $"{Localizer["Discord.DisconnectTitle", playerName]}",
                url = $"https://steamcommunity.com/profiles/{steamID}",
                description = $"{Localizer["Discord.DisconnectDescription", country, steamID, playerip]}",
                color = 16711680,
                footer = new
                {
                    text = $"{Localizer["Discord.Footer"]}"
                }
            };

            var payload = new
            {
                embeds = new[] { embed }
            };

            var jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(Config.DiscordWebhook, content);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogInformation($"Failed to send message to Discord! code: {response.StatusCode}");
            }
        }
    }

}