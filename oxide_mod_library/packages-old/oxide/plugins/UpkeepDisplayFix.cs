namespace Oxide.Plugins
{
    [Info("Upkeep Display Fix", "WhiteThunder", "1.0.1")]
    [Description("Fixes display bug where Tool Cupboard upkeep doesn't factor in decay scale.")]
    internal class UpkeepDisplayFix : CovalencePlugin
    {
        private const int DisplayMinutesWhenNoDecay = 43200; // 30 days

        private void OnEntitySaved(BuildingPrivlidge buildingPrivilege, BaseNetworkable.SaveInfo saveInfo)
        {
            var decayScale = ConVar.Decay.scale;
            var tcSaveInfo = saveInfo.msg.buildingPrivilege;

            if (decayScale == 0)
            {
                var originalProtectedMinutes = tcSaveInfo.protectedMinutes;
                tcSaveInfo.protectedMinutes = DisplayMinutesWhenNoDecay;
                if (originalProtectedMinutes != 0)
                    tcSaveInfo.upkeepPeriodMinutes *= (DisplayMinutesWhenNoDecay / originalProtectedMinutes);
            }
            else
            {
                tcSaveInfo.protectedMinutes /= decayScale;
                tcSaveInfo.upkeepPeriodMinutes /= decayScale;
            }
        }
    }
}
