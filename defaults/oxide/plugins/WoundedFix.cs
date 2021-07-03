namespace Oxide.Plugins
{
    [Info("Wounded Fix", "WhiteThunder", "1.0.0")]
    [Description("Hot fix for players respawning wounded.")]
    internal class WoundedFix : CovalencePlugin
    {
        private void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                FixPlayerIfNeeded(player);
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            FixPlayerIfNeeded(player);
        }

        private void FixPlayerIfNeeded(BasePlayer player)
        {
            if (player.IsIncapacitated() && !player.IsWounded())
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Incapacitated, false);
            }
        }
    }
}
