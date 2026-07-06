using System;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Lumina.Excel.Sheets;

namespace UniversalisMarketBoard.Services;

public sealed class LifestreamTravelService
{
    private const string LifestreamPluginName = "Lifestream";
    private const int LimsaGatewayId = 8;

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICallGateSubscriber<bool> isBusy;
    private readonly ICallGateSubscriber<string, bool> canVisitSameDc;
    private readonly ICallGateSubscriber<string, bool> canVisitCrossDc;
    private readonly ICallGateSubscriber<string, bool, string?, bool, int?, bool?, bool?, object> tpAndChangeWorld;

    public LifestreamTravelService(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        isBusy = pluginInterface.GetIpcSubscriber<bool>("Lifestream.IsBusy");
        canVisitSameDc = pluginInterface.GetIpcSubscriber<string, bool>("Lifestream.CanVisitSameDC");
        canVisitCrossDc = pluginInterface.GetIpcSubscriber<string, bool>("Lifestream.CanVisitCrossDC");
        tpAndChangeWorld = pluginInterface.GetIpcSubscriber<string, bool, string?, bool, int?, bool?, bool?, object>("Lifestream.TPAndChangeWorld");
    }

    public bool IsAvailable =>
        pluginInterface.InstalledPlugins.Any(plugin =>
            plugin.IsLoaded &&
            string.Equals(plugin.InternalName, LifestreamPluginName, StringComparison.OrdinalIgnoreCase));

    public bool TryTravelToWorld(string worldName, out string message)
    {
        if (!IsAvailable)
        {
            message = "Lifestream is not loaded.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(worldName))
        {
            message = "This listing does not have a valid world.";
            return false;
        }

        try
        {
            if (isBusy.InvokeFunc())
            {
                message = "Lifestream is already busy.";
                return false;
            }

            var currentWorldId = Plugin.PlayerState.CurrentWorld.RowId;
            var homeWorldId = Plugin.PlayerState.HomeWorld.RowId;
            var targetWorldId = ResolveWorldId(worldName);

            if (targetWorldId != 0 && currentWorldId == targetWorldId)
            {
                message = $"You are already on {worldName}.";
                return false;
            }

            var crossDcTravel = canVisitCrossDc.InvokeFunc(worldName);
            if (targetWorldId != 0 && homeWorldId != 0 && currentWorldId != homeWorldId && targetWorldId == homeWorldId)
            {
                tpAndChangeWorld.InvokeAction(worldName, crossDcTravel, null, true, LimsaGatewayId, null, null);
                message = crossDcTravel
                    ? $"Sent Lifestream to Limsa Lominsa, then return to your home data centre and world, {worldName}."
                    : $"Sent Lifestream to Limsa Lominsa, then return to your home world, {worldName}.";
                return true;
            }

            var sameDcTravel = !crossDcTravel && canVisitSameDc.InvokeFunc(worldName);
            if (!crossDcTravel && !sameDcTravel)
            {
                message = $"Lifestream cannot start travel to {worldName} right now.";
                return false;
            }

            tpAndChangeWorld.InvokeAction(worldName, crossDcTravel, null, true, LimsaGatewayId, null, null);
            message = crossDcTravel
                ? $"Sent Lifestream to Limsa Lominsa, then DC travel to {worldName}."
                : $"Sent Lifestream to Limsa Lominsa, then world visit to {worldName}.";
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Unable to send Lifestream travel request for {WorldName}.", worldName);
            message = "Lifestream did not accept the travel request.";
            return false;
        }
    }

    private static uint ResolveWorldId(string worldName)
    {
        if (string.IsNullOrWhiteSpace(worldName))
        {
            return 0;
        }

        foreach (var world in Plugin.DataManager.GetExcelSheet<World>())
        {
            if (string.Equals(world.Name.ToString(), worldName, StringComparison.OrdinalIgnoreCase))
            {
                return world.RowId;
            }
        }

        return 0;
    }
}
