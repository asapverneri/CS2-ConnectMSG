using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using CounterStrikeSharp.API.Modules.Utils;

namespace ConnectMSG;

[MinimumApiVersion(247)]
public class ConnectMSGConfig : BasePluginConfig
{
    [JsonPropertyName("PlayerWelcomeMessage")] public bool PlayerWelcomeMessage { get; set; } = true;

    [JsonPropertyName("ConnectMessages")]
    public List<ConnectMessage> ConnectMessages { get; set; } = new()
    {
        new ConnectMessage
        {
            Permission = "#css/vipplus",
            Message = "{GOLD}VIP+ {PURPLE}{NAME} {GREEN}connected [{COUNTRY}]"
        },
        new ConnectMessage
        {
            Permission = "#css/vip",
            Message = "{GOLD}✦ VIP {PURPLE}{NAME} {GREEN}connected [{COUNTRY}]"
        },
        new ConnectMessage
        {
            Permission = "",
            Message = "{PURPLE}{NAME} {GREEN}connected [{COUNTRY}]"
        }
    };
    [JsonPropertyName("PlayerDisconnectMessage")] public string PlayerDisconnectMessage { get; set; } = "{PURPLE}{NAME} {RED}disconnected [{COUNTRY}]";
    [JsonPropertyName("Timer")] public float Timer { get; set; } = 5.0f;
    [JsonPropertyName("LogMessagesToDiscord")] public bool LogMessagesToDiscord { get; set; } = true;
    [JsonPropertyName("DiscordWebhook")] public string DiscordWebhook { get; set; } = "";

}
public class ConnectMessage
{
    [JsonPropertyName("Permission")]
    public string Permission { get; set; } = "";

    [JsonPropertyName("Message")]
    public string Message { get; set; } = "";
}
public class ConnectMSG : BasePlugin, IPluginConfig<ConnectMSGConfig>
{
    public override string ModuleName => "ConnectMSG";
    public override string ModuleDescription => "Simple connect/disconnect messages";
    public override string ModuleAuthor => "verneri";
    public override string ModuleVersion => "1.1.0";

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
        if (@event == null) return HookResult.Continue;
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;
        var steamid = player.SteamID;
        var Name = player.PlayerName;
        string country = GetCountry(player.IpAddress?.Split(":")[0] ?? "Unknown");
        string playerip = player.IpAddress?.Split(":")[0] ?? "Unknown";

        if (LoopConnections.ContainsKey(steamid))
        {
            LoopConnections.Remove(steamid);
        }

        Console.WriteLine($"[{ModuleName}] {Name} has connected!");

        foreach (var permissionMessage in Config.ConnectMessages)
        {
            if (string.IsNullOrEmpty(permissionMessage.Permission) ||
                (permissionMessage.Permission.StartsWith("#")
                    ? AdminManager.PlayerInGroup(player, permissionMessage.Permission)
                    : AdminManager.PlayerHasPermissions(player, permissionMessage.Permission)))
            {
                string message = permissionMessage.Message
                    .Replace("{NAME}", Name)
                    .Replace("{COUNTRY}", country);

                message = ParseChatColors(message);

                Server.PrintToChatAll(" " + message);
                break;
            }
        }

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


        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerDisconnectPre(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event == null) return HookResult.Continue;
        info.DontBroadcast = true;


        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event == null) return HookResult.Continue;
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;
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
                   return HookResult.Continue;
                }

            }
        info.DontBroadcast = true;

        Console.WriteLine($"[{ModuleName}] {Name} has disconnected!");

        string message = Config.PlayerDisconnectMessage.Replace("{NAME}", Name).Replace("{COUNTRY}", country);

        Server.PrintToChatAll(" " + ParseChatColors(message));

        if (Config.LogMessagesToDiscord) {
            _ = SendWebhookMessageAsEmbedDisconnected(player.PlayerName, player.SteamID, playerip, country);
        }


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
    private string ParseChatColors(string message)
    {
        return message
            .Replace("{DEFAULT}", ChatColors.Default.ToString())
            .Replace("{WHITE}", ChatColors.White.ToString())
            .Replace("{DARKRED}", ChatColors.DarkRed.ToString())
            .Replace("{GREEN}", ChatColors.Green.ToString())
            .Replace("{LIGHTYELLOW}", ChatColors.LightYellow.ToString())
            .Replace("{LIGHTBLUE}", ChatColors.LightBlue.ToString())
            .Replace("{OLIVE}", ChatColors.Olive.ToString())
            .Replace("{LIME}", ChatColors.Lime.ToString())
            .Replace("{RED}", ChatColors.Red.ToString())
            .Replace("{LIGHTPURPLE}", ChatColors.LightPurple.ToString())
            .Replace("{PURPLE}", ChatColors.Purple.ToString())
            .Replace("{GREY}", ChatColors.Grey.ToString())
            .Replace("{YELLOW}", ChatColors.Yellow.ToString())
            .Replace("{GOLD}", ChatColors.Gold.ToString())
            .Replace("{SILVER}", ChatColors.Silver.ToString())
            .Replace("{BLUE}", ChatColors.Blue.ToString())
            .Replace("{DARKBLUE}", ChatColors.DarkBlue.ToString());
    }

}