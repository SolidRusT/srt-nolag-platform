using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("NoRaidRepair", "Ryan", "1.0.0")]
    [Description("Prevents the player from repairing their base whilst raidblocked.")]

    class NoRaidRepair : RustPlugin
    {
        [PluginReference] RustPlugin NoEscape;

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CantRepair"] = "You can't repair whilst raidblocked."
            }, this);
        }

        bool isRaidBlocked(BasePlayer player) => (bool)NoEscape.Call("IsRaidBlocked", player);

        void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (NoEscape == null) return;
            if (isRaidBlocked(player)) PrintToChat(player, Lang("CantRepair"));
        }

        object OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            if (NoEscape == null || !isRaidBlocked(player)) return null;
            return false;
        }

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
    }
}