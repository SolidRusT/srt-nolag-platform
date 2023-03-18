using System;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Auto Fuel", "0x89A", "2.0.0")]
    [Description("Automatically fuels lights using fuel from the tool cupboard's inventory")]
    class AutoFuel : RustPlugin
    {
        private Configuration _config;

        private const string usePerm = "autofuel.use";

        private Dictionary<uint, BuildingPrivlidge> _cachedToolCupboards = new Dictionary<uint, BuildingPrivlidge>();

        private void Init()
        {
            permission.RegisterPermission(usePerm, this);
        }

        private void OnItemUse(Item item, int amountToUse)
        {
            BaseEntity parentEntity = item.GetRootContainer()?.entityOwner;

            if (parentEntity != null && _config.AllowedEntities.Contains(parentEntity.ShortPrefabName))
            {
                BuildingPrivlidge toolCupboard = null;
                if (!GetToolCupboard(parentEntity, ref toolCupboard))
                {
                    return; //Ignore if ent has no TC
                }

                if (parentEntity is FuelGenerator) //Small fuel generator
                {
                    FuelGenerator generator = parentEntity as FuelGenerator;

                    if (item.amount - amountToUse <= 0) //If going to run out of fuel
                    {
                        TryRefill(item.info.itemid, generator, generator.inventory, toolCupboard, amountToUse);
                    }
                }
                else if (parentEntity is FogMachine) //Snow and fog machines
                {
                    FogMachine machine = parentEntity as FogMachine;

                    if (item.amount - amountToUse <= 0)
                    {
                        TryRefill(item.info.itemid, machine, machine.inventory, toolCupboard, amountToUse);
                    }
                }
            }
        }

        private void OnFuelConsume(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            BuildingPrivlidge toolCupboard = null;
            if (GetToolCupboard(oven, ref toolCupboard) && fuel.amount - 1 <= 0) 
            {
                TryRefill(fuel.info.itemid, oven, oven.inventory, toolCupboard);
            }
        }

        //If activated and has no fuel, fetch from TC
        private void OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            OnToggle(oven, oven.inventory, oven.fuelType?.itemid ?? -151838493);
        }

        //If activated and has no fuel, fetch from TC
        private void OnSwitchToggle(FuelGenerator generator, BasePlayer player)
        {
            OnToggle(generator, generator.inventory, -946369541);
        }

        private void TryRefill(int itemToFind, BaseEntity ent, ItemContainer container, BuildingPrivlidge toolCupboard, int amount = 1)
        {
            if (!IsAllowed(toolCupboard, ent) || !_config.AllowedEntities.Contains(ent.ShortPrefabName))
            {
                return;
            }

            List<Item> items = toolCupboard.inventory.FindItemsByItemID(itemToFind);

            int numRequired = amount;
            for (int i = 0; i < items.Count; i++)
            {
                if (numRequired <= 0) break;

                Item item = items[i];

                if (item != null)
                {
                    if (item.amount > numRequired)
                    {
                        item.amount -= numRequired;
                        item.MarkDirty();

                        container.AddItem(item.info, numRequired, item.skin);
                        break;
                    }
                    else
                    {
                        numRequired -= item.amount;
                        item.MoveToContainer(container);
                    }
                }
            }
        }

        private void OnToggle(BaseEntity entity, ItemContainer container, int itemid)
        {
            if (container.FindItemByItemID(itemid) != null) //Has fuel
            {
                return;
            }

            BuildingPrivlidge toolCupboard = null;
            if (GetToolCupboard(entity, ref toolCupboard))
            {
                TryRefill(itemid, entity, container, toolCupboard);
            }
        }

        private bool GetToolCupboard(BaseEntity entity, ref BuildingPrivlidge toolCupboard)
        {
            if (entity.net == null)
            {
                return false;
            }

            uint netId = entity.net.ID;

            if (!_cachedToolCupboards.TryGetValue(netId, out toolCupboard) || toolCupboard == null) //If nothing cached or cached tc is now null
            {
                toolCupboard = entity.GetBuildingPrivilege();

                if (!_cachedToolCupboards.ContainsKey(netId))
                {
                    _cachedToolCupboards.Add(netId, toolCupboard);
                }
                else
                {
                    _cachedToolCupboards[netId] = toolCupboard;
                }
            }

            return toolCupboard != null;
        }

        private bool IsAllowed(BuildingPrivlidge privlidge, BaseEntity ent)
        {
            if (!_config.CheckForPerm)
            {
                return false;
            }

            if (_config.CheckEntityForPerm && permission.UserHasPermission(ent.OwnerID.ToString(), usePerm))
            {
                return true;
            }

            if (_config.AnyoneOnTC)
            {
                foreach (var player in privlidge.authorizedPlayers)
                {
                    if (permission.UserHasPermission(player.userid.ToString(), usePerm))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        #region -Configuration-

        private class Configuration
        {
            [JsonProperty("Allowed Entities")]
            public List<string> AllowedEntities = new List<string>();

            [JsonProperty("Check entity owner for permission")]
            public bool CheckEntityForPerm = false;

            [JsonProperty("Anyone on tool cupboard has permission")]
            public bool AnyoneOnTC = true;

            [JsonIgnore]
            public bool CheckForPerm => CheckEntityForPerm || AnyoneOnTC;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();

                if (!_config.CheckEntityForPerm && !_config.AnyoneOnTC)
                {
                    PrintWarning("Both 'Check entity owner for permission' and 'Anyone on tool cupboard has permission' are set to false, permissions will not be checked");
                }

                SaveConfig();
            }
            catch
            {
                PrintError("Failed to load config, using default values");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration()
            {
                AllowedEntities = new List<string>
                {
                    "bbq.deployed",
                    "campfire",
                    "ceilinglight.deployed",
                    "fireplace.deployed",
                    "furnace",
                    "furnace.large",
                    "jackolantern.angry",
                    "jackolantern.happy",
                    "lantern.deployed",
                    "refinery_small_deployed",
                    "searchlight.deployed",
                    "skull_fire_pit",
                    "tunalight.deployed",
                    "fogmachine",
                    "snowmachine",
                    "chineselantern.deployed",
                    "hobobarrel.deployed"
                }
            };
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion
    }
}
