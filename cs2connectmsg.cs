using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;

namespace CS2connectmsg;

[MinimumApiVersion(247)]
public class CS2connectmsg : BasePlugin
{
    public override string ModuleName => "CS2-ConnectMSG";
    public override string ModuleDescription => "Simple connect/disconnect messages";
    public override string ModuleAuthor => "verneri";
    public override string ModuleVersion => "1.0.1";

    public override void Load(bool hotReload)
    {
        Console.WriteLine($"[{ModuleName}] loaded successfully!");
    }

    [GameEventHandler]
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        if (@event == null) return HookResult.Continue;
        var player = @event.Userid;
        var Name = player.PlayerName;

        if (player.IsValid)
        {
            Console.WriteLine($"[{ModuleName}] {Name} has connected!");
            Server.PrintToChatAll($"{Localizer["playerconnect", Name]}");

        }

        return HookResult.Continue;
    }
    [GameEventHandler]
    private HookResult OnPlayerDisconnectPre(EventPlayerDisconnect @event, GameEventInfo info)
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
        var reason = @event.Reason;
        var Name = player.PlayerName;

        info.DontBroadcast = true;

        if (player.IsValid)
        {
            Console.WriteLine($"[{ModuleName}] {Name} has disconnected!");
            Server.PrintToChatAll($"{Localizer["playerdisconnect", Name]}");

        }

        return HookResult.Continue;
    }

}