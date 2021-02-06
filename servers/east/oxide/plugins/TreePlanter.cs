using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Tree Planter", "Bazz3l", "1.1.2")]
    [Description("Buy and plant trees in building authed areas using in-game currency.")]
    class TreePlanter : RustPlugin
    {
        [PluginReference]
        Plugin ServerRewards, Economics;

        #region Fields
        const string _permUse = "treeplanter.use";
        ConfigData _config;
        #endregion

        #region Config
        ConfigData GetDefaultConfig()
        {
            return new ConfigData {
                UseServerRewards = true,
                UseEconomics = false,
                UseCurrency = false,
                OwnerOnly = false,
                CurrencyItemID = -932201673,
                Items = new List<TreeConfig> {
                    new TreeConfig("oak", new List<string> {
                        "assets/bundled/prefabs/autospawn/resource/v2_temp_field_large/oak_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_temp_field_large/oak_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_temp_field_large/oak_c.prefab"
                    }),
                    new TreeConfig("birch", new List<string> {
                        "assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_small_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_medium_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_temp_forest/birch_large_temp.prefab"
                    }),
                    new TreeConfig("douglas", new List<string> {
                        "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest/douglas_fir_a_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest/douglas_fir_b_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest/douglas_fir_c_snow.prefab"
                    }),
                    new TreeConfig("swamp", new List<string> {
                        "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_a.prefab",
                        "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_b.prefab",
                        "assets/bundled/prefabs/autospawn/resource/swamp-trees/swamp_tree_c.prefab"
                    }),
                    new TreeConfig("palm", new List<string> {
                        "assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_small_c_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_med_a_entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_arid_forest/palm_tree_tall_a_entity.prefab"
                    }),
                    new TreeConfig("pine", new List<string> {
                        "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest_snow/pine_a_snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest_snow/pine_b snow.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v2_arctic_forest_snow/pine_c_snow.prefab"
                    })
                }
            };
        }

        class ConfigData
        {
            public bool UseServerRewards;
            public bool UseEconomics;
            public bool UseCurrency;
            public bool OwnerOnly;
            public int CurrencyItemID;
            public List<TreeConfig> Items;

            public TreeConfig FindItemByName(string name) => Items.Find(x => x.Name == name);
        }

        class TreeConfig
        {
            public string Name;
            public int Cost = 10;
            public int Amount = 1;
            public List<string> Prefabs;

            public TreeConfig(string name, List<string> prefabs)
            {
                Name = name;
                Prefabs = prefabs;
            }
        }
        #endregion

        #region Oxide
        protected override void LoadDefaultConfig() => Config.WriteObject(GetDefaultConfig(), true);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string> {
                {"Prefix", "<color=#DC143C>Tree Planter</color>:"},
                {"NoPermission", "No permission"},
                {"Balance", "You do not have enough for that."},
                {"Planted", "You planted a tree."},
                {"Authed", "You must have build privlage."},
                {"Planter", "Can not be placed in a planter."},
                {"Given", "You received {0} ({1})."},
                {"Cost", "\n{0}, cost {1}."},
                {"Error", "Something went wrong."},
                {"Invalid", "Invalid type."},
            }, this);
        }

        void OnServerInitialized()
        {
            permission.RegisterPermission(_permUse, this);
        }

        void Init()
        {
            _config = Config.ReadObject<ConfigData>();
        }

        object OnMeleeAttack(BasePlayer player, HitInfo info)
        {
            BaseEntity ent = info?.HitEntity;

            if (ent == null || ent.OwnerID == 0UL || !IsTree(ent.ShortPrefabName))
            {
                return null;
            }

            if (_config.OwnerOnly && !IsOwner(player.userID, ent.OwnerID))
            {
                info.damageTypes.ScaleAll(0.0f);

                return false;
            }

            return null;
        }

        void OnEntityBuilt(Planner plan, GameObject seed)
        {
            BasePlayer player = plan.GetOwnerPlayer();
            if (player == null || !permission.UserHasPermission(player.UserIDString, _permUse))
            {
                return;
            }

            GrowableEntity plant = seed.GetComponent<GrowableEntity>();
            if (plant == null)
            {
                return;
            }

            Item item = player.GetActiveItem();
            if (item == null)
            {
                return;
            }

            TreeConfig tree = _config.FindItemByName(item.name);
            if (tree == null)
            {
                return;
            }

            NextTick(() => {
                if (plant.GetParentEntity() is PlanterBox)
                {
                    RefundItem(player, item.name);

                    plant?.Kill(BaseNetworkable.DestroyMode.None);

                    player.ChatMessage(Lang("Planter", player.UserIDString));
                    return;
                }

                if (!player.IsBuildingAuthed())
                {
                    RefundItem(player, item.name);

                    plant?.Kill(BaseNetworkable.DestroyMode.None);

                    player.ChatMessage(Lang("Authed", player.UserIDString));
                    return;
                }

                PlantTree(player, plant, tree.Prefabs.GetRandom());
            });
        }

        [ChatCommand("tree")]
        void BuyCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, _permUse))
            {
                player.ChatMessage(Lang("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length != 1)
            {
                StringBuilder sb = new StringBuilder();

                sb.Append(Lang("Prefix", player.UserIDString));

                foreach (TreeConfig tc in _config.Items)
                {
                    sb.Append(Lang("Cost", player.UserIDString, tc.Name, tc.Cost));
                }

                player.ChatMessage(sb.ToString());
                return;
            }

            TreeConfig tree = _config.FindItemByName(string.Join(" ", args));
            if (tree == null)
            {
                player.ChatMessage(Lang("Invalid", player.UserIDString));
                return;
            }

            if (!CheckBalance(player, tree.Cost))
            {
                player.ChatMessage(Lang("Balance", player.UserIDString));
                return;
            }

            Item item = CreateItem(tree.Name, tree.Amount);
            if (item == null)
            {
                player.ChatMessage(Lang("Error", player.UserIDString));
                return;
            }

            BalanceTake(player, tree.Cost);

            player.GiveItem(item);

            player.ChatMessage(Lang("Given", player.UserIDString, tree.Amount, tree.Name));
        }
        #endregion

        #region Core
        void PlantTree(BasePlayer player, GrowableEntity plant, string prefabName)
        {
            BaseEntity entity = GameManager.server.CreateEntity(prefabName, plant.transform.position, Quaternion.identity);
            if (entity == null)
            {
                return;
            }

            entity.OwnerID = player.userID;
            entity.Spawn();

            plant?.Kill();

            player.ChatMessage(Lang("Planted", player.UserIDString));
        }

        bool CheckBalance(BasePlayer player, int cost)
        {
            if (_config.UseServerRewards && ServerRewards?.Call<int>("CheckPoints", player.userID) >= cost)
            {
                return true;
            }

            if (_config.UseEconomics && Economics?.Call<double>("Balance", player.userID) >= (double) cost)
            {
                return true;
            }

            if (_config.UseCurrency && player.inventory.GetAmount(_config.CurrencyItemID) >= cost)
            {
                return true;
            }

            return false;
        }

        void BalanceTake(BasePlayer player, int cost)
        {
            if (_config.UseServerRewards)
            {
                ServerRewards?.Call<object>("TakePoints", player.userID, cost, null);
            }

            if (_config.UseEconomics)
            {
                Economics?.Call<object>("Withdraw", player.userID, (double) cost);
            }

            if (_config.UseCurrency)
            {
                player.inventory.Take(new List<Item>(), _config.CurrencyItemID, cost);
            }
        }

        Item CreateItem(string treeType, int treeAmount = 1)
        {
            Item item = ItemManager.CreateByName("clone.hemp", treeAmount);
            item.name = treeType;
            item.info.stackable = 1;
            return item;
        }

        void RefundItem(BasePlayer player, string treeType)
        {
            Item refundItem = CreateItem(treeType);

            if (refundItem == null)
            {
                player.ChatMessage(Lang("Error", player.UserIDString));
                return;
            }

            player.GiveItem(refundItem);
        }

        bool IsOwner(ulong userID, ulong ownerID)
        {
            return userID == ownerID;
        }

        bool IsTree(string prefab)
        {
            if (prefab.Contains("oak_") 
            || prefab.Contains("birch_") 
            || prefab.Contains("douglas_") 
            || prefab.Contains("swamp_") 
            || prefab.Contains("palm_") 
            || prefab.Contains("pine_"))
            {
                return true;
            }

            return false;
        }
        #endregion

        #region Helpers
        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion
    }
}