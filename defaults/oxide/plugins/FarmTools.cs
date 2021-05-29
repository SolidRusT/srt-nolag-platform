using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Farm Tools", "Clearshot", "1.0.4")]
    [Description("Farming made easy")]
    class FarmTools : CovalencePlugin
    {
        private PluginConfig _config;
        private Game.Rust.Libraries.Player _rustPlayer = Interface.Oxide.GetLibrary<Game.Rust.Libraries.Player>("Player");
        private List<ulong> _usedCommand = new List<ulong>();

        private const string PERM_CLONE = "farmtools.clone";
        private const string PERM_CLONE_ALL = "farmtools.clone.all";
        private const string PERM_HARVEST_ALL = "farmtools.harvest.all";

        private void SendChatMsg(BasePlayer pl, string msg, string prefix = null) =>
            _rustPlayer.Message(pl, msg, prefix != null ? prefix : lang.GetMessage("ChatPrefix", this, pl.UserIDString), Convert.ToUInt64(_config.chatIconID), Array.Empty<object>());

        private void Init()
        {
            permission.RegisterPermission(PERM_CLONE, this);
            permission.RegisterPermission(PERM_CLONE_ALL, this);
            permission.RegisterPermission(PERM_HARVEST_ALL, this);
        }

        private void ShowHelp(BasePlayer pl)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(lang.GetMessage("HelpTitle", this, pl.UserIDString));
            sb.AppendLine(lang.GetMessage("Help1", this, pl.UserIDString));
            sb.AppendLine(lang.GetMessage("Help2", this, pl.UserIDString));
            sb.AppendLine(lang.GetMessage("Help3", this, pl.UserIDString));
            sb.AppendLine(lang.GetMessage("Help4", this, pl.UserIDString));

            if (permission.UserHasPermission(pl.UserIDString, PERM_CLONE_ALL))
            {
                sb.AppendLine(lang.GetMessage("Help5", this, pl.UserIDString));
                sb.AppendLine(lang.GetMessage("Help6", this, pl.UserIDString));
            }

            if (permission.UserHasPermission(pl.UserIDString, PERM_HARVEST_ALL))
            {
                sb.AppendLine(lang.GetMessage("Help7", this, pl.UserIDString));
                sb.AppendLine(lang.GetMessage("Help8", this, pl.UserIDString));
            }

            SendChatMsg(pl, sb.ToString(), "");
        }

        #region Commands
        [Command("farmtools")]
        private void FarmToolsCommand(IPlayer player, string command, string[] args)
        {
            if (player.IsServer) return;
            BasePlayer pl = player.Object as BasePlayer;
            if (pl == null) return;

            ShowHelp(pl);
        }

        [Command("farmtools.clone")]
        private void CloneCommand(IPlayer player, string command, string[] args)
        {
            if (player.IsServer) return;
            BasePlayer pl = player.Object as BasePlayer;
            if (pl == null) return;

            if (!player.HasPermission(PERM_CLONE))
            {
                SendChatMsg(pl, string.Format(lang.GetMessage("NoPerms", this, pl.UserIDString), command));
                return;
            }

            if (!_usedCommand.Contains(pl.userID))
            {
                _usedCommand.Add(pl.userID);
            }

            RaycastHit hit;
            if (Physics.Raycast(pl.eyes.HeadRay(), out hit, 4f))
            {
                int mask = LayerMask.GetMask(new string[] {
                    "Ragdoll"
                });
                Collider[] hitColliders = new Collider[5];
                int numColliders = Physics.OverlapSphereNonAlloc(hit.point, 0.65f, hitColliders, mask);
                for (int i = 0; i < numColliders; i++)
                {
                    GrowableEntity plant = hitColliders[i]?.gameObject?.GetComponent<GrowableEntity>();
                    if (plant != null)
                    {
                        plant.TakeClones(pl);
                        break;
                    }
                }
            }
        }

        [Command("farmtools.cloneall")]
        private void CloneAllCommand(IPlayer player, string command, string[] args)
        {
            if (player.IsServer) return;
            BasePlayer pl = player.Object as BasePlayer;
            if (pl == null) return;

            if (!player.HasPermission(PERM_CLONE_ALL))
            {
                SendChatMsg(pl, string.Format(lang.GetMessage("NoPerms", this, pl.UserIDString), command));
                return;
            }

            PlanterBox planter = EyeTraceToEntity(pl, 4f, typeof(PlanterBox)) as PlanterBox;
            if (planter == null) return;

            foreach (BaseEntity baseEntity in planter.children.ToList())
            {
                if (!(baseEntity == null))
                {
                    GrowableEntity growableEntity = baseEntity as GrowableEntity;
                    if (!(growableEntity == null))
                    {
                        growableEntity.TakeClones(pl);
                    }
                }
            }
        }

        [Command("farmtools.harvestall")]
        private void HarvestAllCommand(IPlayer player, string command, string[] args)
        {
            if (player.IsServer) return;
            BasePlayer pl = player.Object as BasePlayer;
            if (pl == null) return;

            if (!player.HasPermission(PERM_HARVEST_ALL))
            {
                SendChatMsg(pl, string.Format(lang.GetMessage("NoPerms", this, pl.UserIDString), command));
                return;
            }

            if (!_usedCommand.Contains(pl.userID))
            {
                _usedCommand.Add(pl.userID);
            }

            PlanterBox planter = EyeTraceToEntity(pl, 4f, typeof(PlanterBox)) as PlanterBox;
            if (planter == null) return;

            foreach (BaseEntity baseEntity in planter.children.ToList())
            {
                if (!(baseEntity == null))
                {
                    GrowableEntity growableEntity = baseEntity as GrowableEntity;
                    if (!(growableEntity == null))
                    {
                        if (growableEntity.State == PlantProperties.State.Dying)
                        {
                            growableEntity.RemoveDying(pl);
                        }
                        else
                        {
                            growableEntity.PickFruit(pl);
                        }
                    }
                }
            }
        }
        #endregion

        #region Hooks
        private void OnUserDisconnected(IPlayer player)
        {
            BasePlayer pl = player.Object as BasePlayer;
            if (pl == null) return;

            if (_usedCommand.Contains(pl.userID))
            {
                _usedCommand.Remove(pl.userID);
            }
        }

        private void CanTakeCutting(BasePlayer pl, GrowableEntity plant)
        {
            if (permission.UserHasPermission(pl.UserIDString, PERM_CLONE)
                || permission.UserHasPermission(pl.UserIDString, PERM_CLONE_ALL)
                || permission.UserHasPermission(pl.UserIDString, PERM_HARVEST_ALL))
            {
                if (!_usedCommand.Contains(pl.userID))
                {
                    ShowHelp(pl);
                    _usedCommand.Add(pl.userID);
                }
            }
        }
        #endregion

        #region Helpers
        private BaseEntity EyeTraceToEntity(BasePlayer player, float distance, Type filter)
        {
            RaycastHit[] hits = Physics.RaycastAll(player.eyes.HeadRay(), distance);
            for (int i = 0; i < hits.Length; i++)
            {
                if (!(hits[i].GetEntity() == null) && hits[i].GetEntity().GetType() == filter)
                {
                    return hits[i].GetEntity();
                }
            }

            return null;
        }
        #endregion

        #region Config
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ChatPrefix"] = $"<color=#00a7fe>[{Title}]</color>",
                ["NoPerms"] = "<size=12>You do not have permission to use <color=#00a7fe>{0}</color>!</size>",
                ["HelpTitle"] = $"<size=16><color=#00a7fe>{Title}</color> Help</size>\n",
                ["Help1"] = "<size=12>Bind a key to a FarmTools command and press the key while looking at a plant or planter</size>\n",
                ["Help2"] = "<size=12>Open the F1 console to bind keys</size>\n",
                ["Help3"] = "<size=12>Take clone from plant</size>",
                ["Help4"] = "<size=12><color=#00a7fe>bind <key> farmtools.clone</color></size>",
                ["Help5"] = "\n<size=12>Take all clones from planter</size>",
                ["Help6"] = "<size=12><color=#00a7fe>bind <key> farmtools.cloneall</color></size>",
                ["Help7"] = "\n<size=12>Harvest all plants or remove dying plants from planter</size>",
                ["Help8"] = "<size=12><color=#00a7fe>bind <key> farmtools.harvestall</color></size>"
            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_config, true);
        }

        private class PluginConfig
        {
            public string chatIconID = "0";
        }
        #endregion
    }
}