using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Farm Tools", "Clearshot", "1.2.0")]
    [Description("Farming made easy. Take control of farming with binds.")]
    class FarmTools : CovalencePlugin
    {
        private PluginConfig _config;
        private Game.Rust.Libraries.Player _rustPlayer = Interface.Oxide.GetLibrary<Game.Rust.Libraries.Player>("Player");
        private List<ulong> _usedCommand = new List<ulong>();
        private Dictionary<ulong, float> _cooldown = new Dictionary<ulong, float>();

        private const string PERM_CLONE = "farmtools.clone";
        private const string PERM_CLONE_ALL = "farmtools.clone.all";
        private const string PERM_HARVEST_ALL = "farmtools.harvest.all";
        private const string PERM_PLANT_ALL = "farmtools.plant.all";
        private const string PERM_GENES = "farmtools.genes";

        private void SendChatMsg(BasePlayer pl, string msg, string prefix = null) =>
            _rustPlayer.Message(pl, msg, prefix != null ? prefix : lang.GetMessage("ChatPrefix", this, pl.UserIDString), Convert.ToUInt64(_config.chatIconID), Array.Empty<object>());

        private void Init()
        {
            permission.RegisterPermission(PERM_CLONE, this);
            permission.RegisterPermission(PERM_CLONE_ALL, this);
            permission.RegisterPermission(PERM_HARVEST_ALL, this);
            permission.RegisterPermission(PERM_PLANT_ALL, this);
            permission.RegisterPermission(PERM_GENES, this);
        }

        private void ShowHelp(BasePlayer pl)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(lang.GetMessage("HelpTitle", this, pl.UserIDString));
            sb.AppendLine(lang.GetMessage("Help", this, pl.UserIDString));
            sb.AppendLine(lang.GetMessage("HelpClone", this, pl.UserIDString));

            if (permission.UserHasPermission(pl.UserIDString, PERM_CLONE_ALL))
                sb.AppendLine(lang.GetMessage("HelpCloneAll", this, pl.UserIDString));

            if (permission.UserHasPermission(pl.UserIDString, PERM_HARVEST_ALL))
                sb.AppendLine(lang.GetMessage("HelpHarvestAll", this, pl.UserIDString));

            if (permission.UserHasPermission(pl.UserIDString, PERM_PLANT_ALL))
                sb.AppendLine(lang.GetMessage("HelpPlantAll", this, pl.UserIDString));

            if (permission.UserHasPermission(pl.UserIDString, PERM_GENES))
                sb.AppendLine(lang.GetMessage("HelpGenes", this, pl.UserIDString));

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

        [Command("farmtools.clone", "clone")]
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

            if (!_usedCommand.Contains(pl.userID)) _usedCommand.Add(pl.userID);

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
                    GrowableEntity growableEntity = hitColliders[i]?.gameObject?.GetComponent<GrowableEntity>();
                    if (growableEntity != null)
                    {
                        growableEntity.TakeClones(pl);
                        break;
                    }
                }
            }
        }

        [Command("farmtools.cloneall", "cloneall")]
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

            if (!_usedCommand.Contains(pl.userID)) _usedCommand.Add(pl.userID);

            PlanterBox planter = EyeTraceToEntity(pl, 4f, LayerMask.GetMask("Deployed")) as PlanterBox;
            if (planter == null) return;

            foreach (BaseEntity baseEntity in planter.children.ToList())
            {
                if (baseEntity == null) continue;
                GrowableEntity growableEntity = baseEntity as GrowableEntity;
                if (growableEntity == null) continue;
                growableEntity.TakeClones(pl);
            }
        }

        [Command("farmtools.harvestall", "harvestall")]
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

            if (!_usedCommand.Contains(pl.userID)) _usedCommand.Add(pl.userID);

            PlanterBox planter = EyeTraceToEntity(pl, 4f, LayerMask.GetMask("Deployed")) as PlanterBox;
            if (planter == null) return;

            foreach (BaseEntity baseEntity in planter.children.ToList())
            {
                if (baseEntity == null) continue;
                GrowableEntity growableEntity = baseEntity as GrowableEntity;
                if (growableEntity == null) continue;

                if (growableEntity.State == PlantProperties.State.Dying)
                    growableEntity.RemoveDying(pl);
                else
                    growableEntity.PickFruit(pl);
            }
        }

        [Command("farmtools.plantall", "plantall")]
        private void PlantAllCommand(IPlayer player, string command, string[] args)
        {
            if (player.IsServer) return;
            BasePlayer pl = player.Object as BasePlayer;
            if (pl == null) return;

            if (!player.HasPermission(PERM_PLANT_ALL))
            {
                SendChatMsg(pl, string.Format(lang.GetMessage("NoPerms", this, pl.UserIDString), command));
                return;
            }

            if (!_usedCommand.Contains(pl.userID)) _usedCommand.Add(pl.userID);

            Planner planner = pl.GetHeldEntity() as Planner;
            if (planner == null) return;

            PlanterBox planter = EyeTraceToEntity(pl, 4f, LayerMask.GetMask("Deployed")) as PlanterBox;
            if (planter == null) return;

            /****************************************************************************
            *                                                                           *
            * Credits to: Auto Plant by rostov114 (https://umod.org/plugins/auto-plant) *
            *                                                                           *
            ****************************************************************************/

            Construction construction = PrefabAttribute.server.Find<Construction>(planner.GetDeployable().prefabID);
            List<Construction.Target> targets = Facepunch.Pool.GetList<Construction.Target>();
            foreach (Socket_Base socket in PrefabAttribute.server.FindAll<Socket_Base>(planter.prefabID).Where(x => x.female))
            {
                Vector3 pos = planter.transform.TransformPoint(socket.worldPosition);
                Construction.Target target = new Construction.Target();

                target.entity = planter;
                target.ray = new Ray(pos + Vector3.up * 1.0f, Vector3.down);
                target.onTerrain = false;
                target.position = pos;
                target.normal = Vector3.up;
                target.rotation = new Vector3();
                target.player = pl;
                target.valid = true;
                target.socket = socket;
                target.inBuildingPrivilege = true;

                Socket_Base maleSocket = construction.allSockets.Where(x => x.male).FirstOrDefault();
                if (maleSocket != null && !maleSocket.CheckSocketMods(maleSocket.DoPlacement(target)))
                    continue;

                targets.Add(target);
            }

            foreach (Construction.Target target in targets)
            {
                planner.DoBuild(target, construction);
            }

            Facepunch.Pool.FreeList(ref targets);
            // end credits
        }

        [Command("farmtools.genes", "genes")]
        private void GenesCommand(IPlayer player, string command, string[] args)
        {
            if (player.IsServer) return;
            BasePlayer pl = player.Object as BasePlayer;
            if (pl == null) return;

            if (!player.HasPermission(PERM_GENES))
            {
                SendChatMsg(pl, string.Format(lang.GetMessage("NoPerms", this, pl.UserIDString), command));
                return;
            }

            if (IsPlayerOnCooldown(pl)) return;
            if (!_usedCommand.Contains(pl.userID)) _usedCommand.Add(pl.userID);

            BaseEntity entity = EyeTraceToEntity(pl, 2f, LayerMask.GetMask("Deployed"));
            PlanterBox planter = entity as PlanterBox;
            if (planter != null)
            {
                Dictionary<string, List<string>> genes = new Dictionary<string, List<string>>();
                foreach (BaseEntity baseEntity in planter.children.ToList())
                {
                    if (baseEntity == null) continue;
                    GrowableEntity growableEntity = baseEntity as GrowableEntity;
                    if (growableEntity == null || growableEntity.State == PlantProperties.State.Dying) continue;

                    string g = "";
                    string itemName = growableEntity.SourceItemDef.displayName.english;
                    if (!genes.ContainsKey(itemName))
                    {
                        genes.Add(itemName, new List<string>());
                    }

                    foreach (GrowableGene gene in growableEntity.Genes.Genes)
                    {
                        g += gene.GetDisplayCharacter();
                    }

                    if (!genes[itemName].Contains(g))
                    {
                        genes[itemName].Add(g);
                    }
                }

                GiveGenesNote(pl, genes, lang.GetMessage("PlanterGenes", this, pl.UserIDString));
                return;
            }

            StorageContainer container = entity as StorageContainer;
            if (container != null)
            {
                Dictionary<string, List<string>> genes = new Dictionary<string, List<string>>();
                foreach (Item i in container.inventory.itemList)
                {
                    if (i.info.amountType != ItemDefinition.AmountType.Genetics || i?.instanceData?.dataInt == null) continue;

                    string g = "";
                    string itemName = i.info.displayName.english;
                    if (!genes.ContainsKey(itemName))
                    {
                        genes.Add(itemName, new List<string>());
                    }

                    GrowableGenes growGenes = new GrowableGenes();
                    GrowableGeneEncoding.DecodeIntToGenes(i.instanceData.dataInt, growGenes);
                    foreach (GrowableGene gene in growGenes.Genes)
                    {
                        g += gene.GetDisplayCharacter();
                    }

                    if (!genes[itemName].Contains(g))
                    {
                        genes[itemName].Add(g);
                    }
                }

                GiveGenesNote(pl, genes, lang.GetMessage("ContainerGenes", this, pl.UserIDString));
                return;
            }

            SendChatMsg(pl, lang.GetMessage("InvalidGeneEntity", this, pl.UserIDString));
        }
        #endregion

        #region Hooks
        private void OnUserDisconnected(IPlayer player)
        {
            BasePlayer pl = player.Object as BasePlayer;
            if (pl == null) return;

            _usedCommand.Remove(pl.userID);
            _cooldown.Remove(pl.userID);
        }

        private void CanTakeCutting(BasePlayer pl, GrowableEntity plant)
        {
            if (permission.UserHasPermission(pl.UserIDString, PERM_CLONE) || permission.UserHasPermission(pl.UserIDString, PERM_CLONE_ALL))
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
        public bool IsPlayerOnCooldown(BasePlayer pl)
        {
            if (_cooldown.ContainsKey(pl.userID) && Time.realtimeSinceStartup < _cooldown[pl.userID])
            {
                SendChatMsg(pl, string.Format(lang.GetMessage("CommandCooldown", this, pl.UserIDString), Math.Ceiling(_cooldown[pl.userID] - Time.realtimeSinceStartup)));
                return true;
            }
            return false;
        }

        private void GiveGenesNote(BasePlayer pl, Dictionary<string, List<string>> genes, string title)
        {
            if (genes.Count < 1)
            {
                SendChatMsg(pl, lang.GetMessage("InvalidGeneEntity", this, pl.UserIDString));
                return;
            }

            Item item = ItemManager.CreateByName("note");
            item.text = $"{title}\n\n";
            foreach (KeyValuePair<string, List<string>> i in genes)
            {
                item.text += i.Key + "\n";
                foreach (string gene in i.Value)
                {
                    item.text += gene + "\n";
                }
                item.text += "\n";
            }

            if (_config.printGenesToConsole)
                pl.ConsoleMessage(item.text);

            pl.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
            SendChatMsg(pl, lang.GetMessage("GeneNote", this, pl.UserIDString));
            _cooldown[pl.userID] = Time.realtimeSinceStartup + _config.geneCooldown;
        }

        private BaseEntity EyeTraceToEntity(BasePlayer pl, float distance, int mask = ~0)
        {
            RaycastHit hit;
            return Physics.Raycast(pl.eyes.HeadRay(), out hit, distance, mask) ? hit.GetEntity() : null;
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
                ["Help"] = "<size=12>Bind a key to a FarmTools command using the F1 console</size>\n",
                ["HelpClone"] = "<size=12>Take clone from a single plant\n<color=#00a7fe>bind <key> farmtools.clone</color>\nchat command: <color=#00a7fe>/clone</color></size>",
                ["HelpCloneAll"] = "\n<size=12>Take all clones from a planter\n<color=#00a7fe>bind <key> farmtools.cloneall</color>\nchat command: <color=#00a7fe>/cloneall</color></size>",
                ["HelpHarvestAll"] = "\n<size=12>Harvest all plants or remove dying plants from a planter\n<color=#00a7fe>bind <key> farmtools.harvestall</color>\nchat command: <color=#00a7fe>/harvestall</color></size>",
                ["HelpPlantAll"] = "\n<size=12>Plant all seeds in the target planter\n<color=#00a7fe>bind <key> farmtools.plantall</color>\nchat command: <color=#00a7fe>/plantall</color></size>",
                ["HelpGenes"] = "\n<size=12>Copy genes from a planter box or storage container\n<color=#00a7fe>bind <key> farmtools.genes</color>\nchat command: <color=#00a7fe>/genes</color></size>",
                ["PlanterGenes"] = "][ Planter Genes ][",
                ["ContainerGenes"] = "][ Container Genes ][",
                ["GeneNote"] = "A note with genes has been added to your inventory.\n\nGenes can also be copied from the F1 console with <color=#00a7fe>console.copy</color>.",
                ["InvalidGeneEntity"] = "Unable to find genes! Look at a <color=#00a7fe>Planter Box</color> or <color=#00a7fe>Storage Container</color> and try again.",
                ["CommandCooldown"] = "Please wait <color=#00a7fe>{0}s</color>!"
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
            public bool printGenesToConsole = true;
            public float geneCooldown = 10;
        }
        #endregion
    }
}