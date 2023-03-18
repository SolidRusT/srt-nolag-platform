
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AutoBaseUpgrade", "CASHR#6906", "1.0.6")]
    internal class AutoBaseUpgrade : RustPlugin
    {
        #region Static
        [PluginReference] private Plugin ImageLibrary, NoEscape;
        private Dictionary<BuildingPrivlidge, BuildSettings> TCList = new Dictionary<BuildingPrivlidge, BuildSettings>();
        private Configuration _config;
        private const string perm = "autobaseupgrade.use";
        #endregion

        #region Config

        private class Configuration
        {
            [JsonProperty("Allow the repair of deployable objects?")]
            public bool DepRepair = true;
            [JsonProperty("Upgrade cooldown (seconds)")]
            public Dictionary<string, float> CDList = new Dictionary<string, float>()
            {
                ["autobaseupgrade.use"] = 1.55f,
                ["autobaseupgrade.vip"] = 0.55f,
            };
            
            [JsonProperty("Cost Modifier for repairs")]
            public Dictionary<string, float> CostList = new Dictionary<string, float>()
            {
                ["autobaseupgrade.use"] = 1.5f,
                ["autobaseupgrade.vip"] = 1.0f,
            };
            [JsonProperty("Run upgrade effect")]
            public bool Effect = true;
            [JsonProperty("AnchorMin")] public string AnchorMin ="0.56 0.8";
            [JsonProperty("AnchorMax")] public string AnchorMax = "0.7 0.665";
            [JsonProperty("OffsetMin")] public string OffsetMin = "293.746 57.215";
            [JsonProperty("OffsetMax")] public string OffsetMax = "406.44 77.785";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        #endregion

        #region OxideHooks

        private void OnServerInitialized()
        {
            foreach (var check in _config.CDList)
            {
                if (!permission.PermissionExists(check.Key, this))
                    permission.RegisterPermission(check.Key, this);
            }
            foreach (var check in _config.CostList)
            {
                if (!permission.PermissionExists(check.Key, this))
                    permission.RegisterPermission(check.Key, this);
            }
            permission.RegisterPermission(perm, this);
        }

        private void Unload()
        {
            foreach (var check in TCList)
            {
                if (check.Value._cor != null)
                {
                    ServerMgr.Instance.StopCoroutine(check.Value._cor);
                }
                if(check.Value._corRepair != null)
                    ServerMgr.Instance.StopCoroutine(check.Value._corRepair);
            }
        }

        #endregion


        #region Function

        private float GetCD(BasePlayer player)
        {
            float time = 100.0f;
            foreach (var check in _config.CDList)
            {
                if (permission.UserHasPermission(player.UserIDString, check.Key))
                    time = Math.Min(time, check.Value);

            }

            return time;
        }

        private float GetCost(BasePlayer player)
        {
            float cost = 100.0f;
            foreach (var check in _config.CostList)
            {
                if (permission.UserHasPermission(player.UserIDString, check.Key))
                    cost = Math.Min(cost, check.Value);

            }

            return cost;
        }

        private bool  DoRepair(BasePlayer player, BaseCombatEntity entity, BuildingPrivlidge tc, float cost)
        {
            if (entity == null || !entity.IsValid() || entity.IsDestroyed || entity.transform == null)
            {
                return false;
            }

            if (!entity.repair.enabled || entity.health == entity.MaxHealth())
            {
                return false;
            }

            if (Interface.CallHook("OnStructureRepair", entity, player) != null)
            {
                return false;
            }

            float missingHealth = entity.MaxHealth() - entity.health;
            float healthPercentage = missingHealth / entity.MaxHealth();
            if (missingHealth <= 0f || healthPercentage <= 0f)
            {
                entity.OnRepairFailed(null, string.Empty);
                return false;
            }


            List<ItemAmount> itemAmounts = entity.RepairCost(healthPercentage);
            if (itemAmounts.Sum(x => x.amount) <= 0f)
            {
                entity.health += missingHealth;
                entity.SendNetworkUpdate();
                entity.OnRepairFinished();
                return true;
            }

            foreach (ItemAmount amount in itemAmounts)
            {
                amount.amount *= cost;
            }


            if (itemAmounts.Any(ia => tc.inventory.GetAmount(ia.itemid, false) < (int)ia.amount))
            {
                entity.OnRepairFailed(null, string.Empty);
                player.ChatMessage(GetMessage("MSG_REPAIRNOTFUND",player.UserIDString));
                TCList[tc].isRepair = false;
                return false;
            }

            foreach (ItemAmount amount in itemAmounts)
            {
                tc.inventory.Take(null, amount.itemid, (int)amount.amount);
            }
            entity.health += missingHealth;
            entity.SendNetworkUpdate();
            if (entity.health < entity.MaxHealth())
            {
                entity.OnRepair();
            }
            else
            {
                entity.OnRepairFinished();
            }

            return true;

        }

        private IEnumerator RepairProgress(BasePlayer player, BuildingPrivlidge tc)
        {
            var building = tc.GetBuilding();
            yield return CoroutineEx.waitForSeconds(0.15f);
            var cd = GetCD(player);
            var cost = GetCost(player);
            for (int index = 0; index < building.buildingBlocks.Count; index++)
            {
                var entity = building.buildingBlocks[index];
                if (!TCList[tc].isRepair) break;
                if (!DoRepair(player, entity, tc, cost)) continue;
                yield return CoroutineEx.waitForSeconds(cd);
            }

            if (_config.DepRepair)
            {
                for (int index = 0; index < building.decayEntities.Count; index++)
                {
                    var entity = building.decayEntities[index];
                    if (!TCList[tc].isRepair) break;
                    if (!DoRepair(player, entity, tc, cost)) continue;
                    yield return CoroutineEx.waitForSeconds(cd);
                }
            }

            TCList[tc].isRepair = false;
            player.ChatMessage(GetMessage("MSG_REPAIRDONE", player.UserIDString));
            yield return 0;
        }

        private IEnumerator UpdateProgress(BasePlayer player, BuildingPrivlidge tc)
        {
            var set = tc.GetBuilding().buildingBlocks;
            yield return CoroutineEx.waitForSeconds(0.15f);
            var cd = GetCD(player);
            for (var index = 0; index < set.Count; index++)
            {
                var check = set[index];
                if (tc == null)
                {
                    yield break;
                }
                if(!TCList[tc].isUpgrade)break;
                var grade = TCList[tc].currentGrade;
                if (grade <= check.grade) continue;
                PayForUpgrade(check, tc, grade, tc.inventory.itemList, player);
                yield return CoroutineEx.waitForSeconds(cd);
            }

            TCList[tc].isUpgrade = false;
            player.ChatMessage(GetMessage("MSG_UPGRADEDONE",player.UserIDString));
            yield return 0;
        }
        private class BuildSettings
        {
            public BuildingGrade.Enum currentGrade;
            public Coroutine _cor;
            public Coroutine _corRepair;
            public bool isUpgrade;
            public bool isRepair;
        }
        private void PayForUpgrade(BuildingBlock block,BuildingPrivlidge tc, BuildingGrade.Enum grade, List<Item> itemList, BasePlayer initiator)
        {
            if (!CanUpgrade(tc,block,grade))
            {
                initiator.ChatMessage(GetMessage("MSG_RESOURSENOTFUND",initiator.UserIDString));
                TCList[tc].isUpgrade = false;
                return;
            }

            var list = block.GetGrade(grade).costToBuild;
            for (var index = 0; index < list.Count; index++)
            {
                var check = list[index];
                Take(tc.inventory.itemList, check.itemDef.shortname, (int)check.amount);
            }

            block.SetGrade(grade);
            block.SetHealthToMax();
            block.UpdateSkin();
            if (_config.Effect)
            {
                var effect = grade == BuildingGrade.Enum.Metal
                    ?
                    "assets/bundled/prefabs/fx/build/promote_metal.prefab"
                    :
                    TCList[tc].currentGrade == BuildingGrade.Enum.Stone
                        ? "assets/bundled/prefabs/fx/build/promote_stone.prefab"
                        :
                        grade == BuildingGrade.Enum.Wood
                            ? "assets/bundled/prefabs/fx/build/frame_place.prefab"
                            : "assets/bundled/prefabs/fx/build/promote_toptier.prefab";
                Effect.server.Run(effect, block.transform.position);
            }

            block.SendNetworkUpdateImmediate();
        }

        private void ShowUI(BasePlayer player, BuildingPrivlidge tc)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = {Color = "1 1 1 0"},
                RectTransform =
                {
                    AnchorMin = _config.AnchorMin, AnchorMax = _config.AnchorMax, OffsetMin = _config.OffsetMin,
                    OffsetMax = _config.OffsetMax
                }
            }, "Overlay", "Panel_507");
            var text = TCList[tc].isUpgrade ? "<color=#6FBD57>UPGRADE             </color>" : "<color=white>UPGRADE             </color>";
            var textRepair = TCList[tc].isRepair ? "<color=#6FBD57>REPAIR ALL      </color>" : "<color=white>REPAIR ALL      </color>";
            
            
            container.Add(new CuiButton
            {
                Button = {Color = "0.3372549 0.3411765 0.2705882 1", Command = "UI_UPGRADEBASEUP REPAIR"},
                Text =
                {
                    Text = textRepair, Font = "robotocondensed-bold.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-158.351 -10.285", OffsetMax = "-58.349 10"
                }
            }, "Panel_507", "Button_6571");
            
            container.Add(new CuiButton
            {
                Button = {Color = "0.3372549 0.3411765 0.2705882 1", Command = "UI_UPGRADEBASEUP SWITCH"},
                Text =
                {
                    Text = text, Font = "robotocondensed-bold.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-56.351 -10.285", OffsetMax = "56.349 10"
                }
            }, "Panel_507", "Button_6571");

            var grade = TCList[tc].currentGrade;
            var image = grade == BuildingGrade.Enum.Metal ? "metal.fragments" :
                TCList[tc].currentGrade == BuildingGrade.Enum.Stone ? "stones" :
                grade == BuildingGrade.Enum.Wood ? "wood" : "metal.refined";
            container.Add(new CuiElement
            {
                Name = "Image_4494",
                Parent = "Panel_507",
                Components =
                {
                    new CuiRawImageComponent {Color = "1 1 1 1", Png = ImageLibrary?.Call<string>("GetImage", image)},
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "36.347 -10", OffsetMax = "56.347 10"
                    }
                }
            });
            container.Add(new CuiButton
            {
                Button = {Color = "0.3372549 0.3411765 0.2705882 0", Command = $"UI_UPGRADEBASEUP CHANGE"},
                Text =
                {
                    Text = "", Font = "robotocondensed-bold.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1"
                }
            }, "Image_4494", "Button_6571");
            CuiHelper.DestroyUi(player, "Panel_507");
            CuiHelper.AddUi(player, container);
        }


        [ConsoleCommand("UI_UPGRADEBASEUP")]
        private void cmdConsoleUI_UPGRADEBASEUP(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!permission.UserHasPermission(player.UserIDString, perm)) return;

            var tc = player.GetBuildingPrivilege();
            if (!TCList.ContainsKey(tc))
            {
                player.ChatMessage("An error occurred, open the TC again");
                return;
            }

            switch (arg.Args[0])
            {
                case "skykey":
                {
                    player.ChatMessage("У вас нет разрешение на использование");
                    break;
                    
                }
                case "REPAIR":
                {
                    TCList[tc].isRepair = !TCList[tc].isRepair;

                    if (TCList[tc].isRepair)
                    {
                        TCList[tc]._corRepair = ServerMgr.Instance.StartCoroutine(RepairProgress(player, tc));
                    }
                    else
                    {
                        if (TCList[tc]._corRepair != null)
                        {
                            ServerMgr.Instance.StopCoroutine(TCList[tc]._corRepair);
                        }
                    }

                    break;
                    
                }
                case "CHANGE":
                { 
                    var grade = TCList[tc].currentGrade;
                    grade++;
                    grade = grade >= BuildingGrade.Enum.Count ? BuildingGrade.Enum.Wood : grade;
                    TCList[tc].currentGrade = grade;
                    break;
                }
                case "SWITCH":
                {
                    TCList[tc].isUpgrade = !TCList[tc].isUpgrade;

                    if (TCList[tc].isUpgrade)
                    {
                        TCList[tc]._cor = ServerMgr.Instance.StartCoroutine(UpdateProgress(player, tc));
                    }
                    else
                    {
                        if (TCList[tc]._cor != null)
                        {
                            ServerMgr.Instance.StopCoroutine(TCList[tc]._cor);
                        }
                    }

                    break;
                }
            }
            ShowUI(player, tc);

          
        }
        private bool CanUpgrade(BuildingPrivlidge tc, BuildingBlock block,BuildingGrade.Enum iGrade )
        {
            var list = block.GetGrade(iGrade).costToBuild;
            for (var index = 0; index < list.Count; index++)
            {
                ItemAmount itemAmount = list[index];
                if (tc.inventory.GetAmount(itemAmount.itemid, false) < (double) itemAmount.amount)
                    return false;
            }

            return true;
        }
        
        private void OnLootEntity(BasePlayer player, BuildingPrivlidge tc)
        {
            if (player == null || tc == null) return;
            if (!permission.UserHasPermission(player.UserIDString, perm)) return;
            if (NoEscape != null)
            {
                if (NoEscape.Call<bool>("IsRaidBlocked", player.UserIDString))
                    return;
            }
            if (!TCList.ContainsKey(tc))
            {
                TCList.Add(tc, new BuildSettings()
                {
                    currentGrade = BuildingGrade.Enum.Wood,
                    isUpgrade = false
                });
            }
            ShowUI(player, tc);
        }
        private  void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, "Panel_507");
        }
        
        private static void Take(IEnumerable<Item> itemList, string shortname, int iAmount)
        {
            var num1 = 0;
            if (iAmount == 0) return;

            var list = Facepunch.Pool.GetList<Item>();
            foreach (var obj in itemList)
            {
                if (obj.info.shortname != shortname) continue;
                var num2 = iAmount - num1;
                if (num2 <= 0) continue;
                if (obj.amount > num2)
                {
                    obj.MarkDirty();
                    obj.amount -= num2;
                    break;
                }

                if (obj.amount <= num2)
                {
                    num1 += obj.amount;
                    list.Add(obj);
                }

                if (num1 == iAmount)
                    break;
            }

            foreach (var obj in list)
                obj.Remove();
            Facepunch.Pool.FreeList(ref list);
        }

       

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MSG_UPGRADEDONE"] = "The improvement of the buildings is finished",
                ["MSG_REPAIRDONE"] = "The repair of the building is finished",
                ["MSG_REPAIRNOTFUND"] = "Not enough resources to repair the building",
                ["MSG_RESOURSENOTFUND"] = "There are not enough resources to improve the structure",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MSG_UPGRADEDONE"] = "Улучшение построек закончено",
                ["MSG_REPAIRDONE"] = "Ремонт здания завершен",
                ["MSG_REPAIRNOTFUND"] = "Недостаточно ресурсов для ремонта здания",
                ["MSG_RESOURSENOTFUND"] = "Не достаточно ресурсов для того, чтобы улучшить строение",
            }, this, "ru");
        }

        private string GetMessage(string langKey, string steamID) => lang.GetMessage(langKey, this, steamID);

        private string GetMessage(string langKey, string steamID, params object[] args)
        {
            return (args.Length == 0)
                ? GetMessage(langKey, steamID)
                : string.Format(GetMessage(langKey, steamID), args);
        }
        #endregion


    }
}