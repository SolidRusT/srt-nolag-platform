using System;
using System.Collections.Generic;
using System.Reflection;
using Oxide.Core.Plugins;
using Oxide.Core;
using Oxide.Core.Configuration;
using UnityEngine;
using System.Text;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Prod", "Quapi", "2.5.1")]
    class Prod : RustPlugin
    {
        private DynamicConfigFile BuildingData;
        private int prodAuth;
        private string helpProd;
        private string noAccess;
        private string noTargetfound;
        private string noCupboardPlayers;
        private string Toolcupboard;
        private string noBlockOwnerfound;
        private string noCodeAccess;
        private string codeLockList;
        private string informationAdded;
        private bool passiveMode;
        private string passive_Codelock_List;
        private string boxNeedsCode;
        private string boxCode;
        private static bool serverInitialized = false;
        private bool printToConsoleInsteadOfChat;

        private FieldInfo serverinput;
        private FieldInfo codelockwhitelist;
        private FieldInfo codenum;
        //private FieldInfo meshinstances;

        private Vector3 eyesAdjust;
        private bool Changed;

        [PluginReference]
        Plugin PlayerDatabase;

        void Loaded()
        {
            LoadVariables();

            permission.RegisterPermission("prod.passive.use", this);

            BuildingData = Interface.GetMod().DataFileSystem.GetDatafile("Prod_BuildingData");

            eyesAdjust = new Vector3(0f, 1.5f, 0f);
            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            codelockwhitelist = typeof(CodeLock).GetField("whitelistPlayers", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            codenum = typeof(CodeLock).GetField("code", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            //meshinstances = typeof(MeshColliderLookup).GetField("instances", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        private bool isPluginDev;
        private bool dumpAll;
        private void LoadVariables()
        {
            printToConsoleInsteadOfChat = Convert.ToBoolean(GetConfig("Prod", "Print to player console instead of chat? ", false));
            prodAuth = Convert.ToInt32(GetConfig("Prod", "authLevel", 1));
            passiveMode = Convert.ToBoolean(GetConfig("Prod", "Passive Mode", false));
            isPluginDev = Convert.ToBoolean(GetConfig("Plugin Dev", "Are you are plugin dev?", false));
            dumpAll = Convert.ToBoolean(GetConfig("Plugin Dev", "Dump all components of all entities that you are looking at? (false will do only the closest one)", false));
            informationAdded = Convert.ToString(GetConfig("Messages", "Information added message (Only works with printToConsole option on.)", "New information was printed to your console. (Press F1)"));
            helpProd = Convert.ToString(GetConfig("Messages", "helpProd", "/prod on a building or tool cupboard to know who owns it."));
            noAccess = Convert.ToString(GetConfig("Messages", "noAccess", "You don't have access to this command"));
            noTargetfound = Convert.ToString(GetConfig("Messages", "noTargetfound", "You must look at a tool cupboard or building"));
            noCupboardPlayers = Convert.ToString(GetConfig("Messages", "noCupboardPlayers", "No players has access to this cupboard"));
            Toolcupboard = Convert.ToString(GetConfig("Messages", "Toolcupboard", "Tool Cupboard"));
            noBlockOwnerfound = Convert.ToString(GetConfig("Messages", "noBlockOwnerfound", "No owner found for this building block"));
            noCodeAccess = Convert.ToString(GetConfig("Messages", "noCodeAccess", "No players has access to this Lock"));
            codeLockList = Convert.ToString(GetConfig("Messages", "codeLockList", "CodeLock whitelist:"));
            boxNeedsCode = Convert.ToString(GetConfig("Messages", "boxNeedsCode", "Can't find owners of an item without a Code Lock"));
            passive_Codelock_List = Convert.ToString(GetConfig("Messages", "Passive Mode: Codelock List", "Codelock Whitelist:"));
            boxCode = Convert.ToString(GetConfig("Messages", "Code", "Code is: {0}"));

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Prod: Creating a new config file");
            Config.Clear();
            LoadVariables();
        }

        #region -random-
        void OnServerInitialized() { serverInitialized = true; }

        void OnServerSave()
        {
            SaveData();
        }

        void OnServerQuit()
        {
            SaveData();
        }

        void OnEntityBuilt(HeldEntity heldentity, GameObject gameobject)
        {
            if (!serverInitialized) return;

            var block = gameobject.GetComponent<BuildingBlock>();
            if (block == null) return;

            var player = heldentity.GetOwnerPlayer();
            if (player == null) return;

            var blockdata = FindBlockData(block);
            if (blockdata is string) return;

            SetBlockData(block, player.IPlayer.Id);
        }
        #endregion

        #region -Building Data-
        void SaveData()
        {
            Interface.GetMod().DataFileSystem.SaveDatafile("Prod_BuildingData");
        }

        void SetBlockData(BuildingBlock block, string steamid)
        {
            BuildingData[block.buildingID.ToString()] = steamid;
        }

        object FindBlockData(BuildingBlock block)
        {
            var buildingid = block.buildingID.ToString();
            if (BuildingData[buildingid] != null) return BuildingData[buildingid];
            return false;
        }
        #endregion

        #region -General-
        private bool hasAccess(BasePlayer player)
        {
            return player.net.connection.authLevel >= prodAuth;
        }

        [ChatCommand("prod")]
        void cmdChatProd(BasePlayer player, string command, string[] args)
        {
            if (printToConsoleInsteadOfChat)
            {
                player.IPlayer.Message(informationAdded);
            }

            var input = serverinput.GetValue(player) as InputState;
            var currentRot = Quaternion.Euler(input.current.aimAngles) * Vector3.forward;
            var target = DoRay(player.transform.position + eyesAdjust, currentRot);

            if (!hasAccess(player))
            {
                if (passiveMode)
                    if (player.IPlayer.HasPermission("prod.passive.use"))
                    {
                        if (target == null)
                        {
                            SendReply(player, noTargetfound);
                            return;
                        }


                       
                        if (target.GetComponentInParent<BuildingBlock>())
                        {
                            Passive_GetBuildingBlockOwner(player, target.GetComponentInParent<BuildingBlock>());
                            return;
                        }

                        if (target.HasSlot(BaseEntity.Slot.Lock))
                        {
                            Passive_GetCodelockWhitelisted(player, target);
                            return;
                        }

                        return;
                    }
                    else
                    {
                        SendReply(player, noAccess);
                        return;
                    }
                else
                {
                    SendReply(player, noAccess);
                    return;
                }
            }

            if (target == null)
            {
                SendReply(player, noTargetfound);
                return;
            }

            if (isPluginDev && !dumpAll)
            {
                Dump(target);
            }

            if (target.OwnerID != 0L)
            {
                SendReply(player, string.Format("Entity Owner (Builder): {0} {1}", FindPlayerName(target.OwnerID), target.OwnerID));
            }


            var block = target.GetComponentInParent<BuildingBlock>();
            if (block)
            {
                GetBuildingblockOwner(player, block);
                return;
            }

            var priv = target.GetComponentInParent<BuildingPrivlidge>();
            if (priv)
            {
                GetToolCupboardUsers(player, priv);
                return;
            }

            var bag = target.GetComponentInParent<SleepingBag>();
            if (bag)
            {
                GetDeployedItemOwner(player, bag);
                return;
            }

            if (target.HasSlot(BaseEntity.Slot.Lock))
            {
                GetDeployableCode(player, target);
                return;
            }        
        }

        private void GetDeployableCode(BasePlayer player, BaseEntity block)
        {
            BaseEntity slotent = block.GetSlot(BaseEntity.Slot.Lock);
            CodeLock codelock = slotent?.GetComponent<CodeLock>();
            if (codelock != null)
            {
                List<ulong> whitelisted = codelockwhitelist.GetValue(codelock) as List<ulong>;
                string codevalue = codenum.GetValue(codelock) as string;
                SendReply(player, string.Format(boxCode, codevalue));
                SendReply(player, codeLockList);
                if (whitelisted.Count == 0)
                {
                    SendReply(player, noCodeAccess);
                    return;
                }
                SendBasePlayerFind(player, whitelisted);
            }
        }

        private void GetDeployedItemOwner(BasePlayer player, SleepingBag ditem)
        {
            SendReply(player, string.Format("Sleeping Bag '{0}': {1} - {2}", ditem.niceName, FindPlayerName(ditem.deployerUserID), ditem.deployerUserID));
        }

        private object FindOwnerBlock(BuildingBlock block)
        {
            object returnhook = FindBlockData(block);
            if (returnhook != null)
            {
                if (!(returnhook is bool))
                {
                    ulong ownerid = Convert.ToUInt64(returnhook);
                    return ownerid;
                }
            }
            Puts("Prod: Owner not found.");
            return false;
        }

        private string FindPlayerName(ulong userId)
        {
            BasePlayer player = BasePlayer.FindByID(userId);
            if (player)
                return player.displayName + " (Online)";

            player = BasePlayer.FindSleeping(userId);
            if (player)
                return player.displayName + " (Sleeping)";

            var iplayer = covalence.Players.FindPlayer(userId.ToString());
            if (iplayer != null)
                return iplayer.Name + " (Dead)";

            var name2 = PlayerDatabase?.Call("GetPlayerData", userId.ToString(), "default");
            if (name2 is Dictionary<string, object>)
                return ((name2 as Dictionary<string, object>)["name"] as string) + " (Dead)";

            return "Unknown player";
        }

        private void SendBasePlayerFind(BasePlayer player, List<ulong> ownerid)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (ulong id in ownerid)
            {
                stringBuilder.AppendLine(string.Format("{0} {1}", FindPlayerName(id), id));
            }
            SendReply(player, stringBuilder.ToString());
        }

        private void GetBuildingblockOwner(BasePlayer player, BuildingBlock block)
        {
            if (block.GetComponent<Door>() != null)
            {
                if (block.HasSlot(BaseEntity.Slot.Lock))
                {
                    BaseEntity slotent = block.GetSlot(BaseEntity.Slot.Lock);
                    CodeLock codelock = slotent?.GetComponent<CodeLock>();
                    if (codelock != null)
                    {
                        List<ulong> whitelisted = codelockwhitelist.GetValue(codelock) as List<ulong>;
                        string codevalue = codenum.GetValue(codelock) as string;
                        SendReply(player, string.Format(boxCode, codevalue));
                        SendReply(player, codeLockList);
                        if (whitelisted.Count == 0)
                        {
                            SendReply(player, noCodeAccess);
                            return;
                        }
                        foreach (ulong userid in whitelisted)
                        {
                            SendReply(player, string.Format("{0} {1}", FindPlayerName(userid), userid));
                        }
                    }

                }
            }

            object findownerblock = FindOwnerBlock(block);
            if (findownerblock is bool)
            {
                SendReply(player, noBlockOwnerfound);
                return;
            }
            ulong ownerid = (ulong)findownerblock;
            SendReply(player, string.Format("Building Owner: {0} {1}", FindPlayerName(ownerid), ownerid));
            List<ulong> list = new List<ulong>();
            list.Add(ownerid);
            SendBasePlayerFind(player, list);
        }

        private void GetToolCupboardUsers(BasePlayer player, BuildingPrivlidge cupboard)
        {
            SendReply(player, string.Format("{0} - {1} {2} {3}", Toolcupboard, Math.Round(cupboard.transform.position.x), Math.Round(cupboard.transform.position.y), Math.Round(cupboard.transform.position.z)));
            if (cupboard.authorizedPlayers.Count == 0)
            {
                SendReply(player, noCupboardPlayers);
                return;
            }
            foreach (ProtoBuf.PlayerNameID pnid in cupboard.authorizedPlayers)
            {
                SendReply(player, string.Format("{0} - {1}", pnid.username, pnid.userid));
            }
        }

        private void Dump(BaseEntity col)
        {
            Puts(col.GetComponent<StabilityEntity>().ToString());
            Puts("==================================================");
            Puts(col + " " + LayerMask.LayerToName(col.gameObject.layer));
            Puts("========= NORMAL ===========");
            foreach (Component com in col.GetComponents(typeof(Component)))
            {
                Puts(com.GetType() + " " + com);
            }
            Puts("========= PARENT ===========");
            foreach (Component com in col.GetComponentsInParent(typeof(Component)))
            {
                Puts(com.GetType() + " " + com);
            }
            Puts("========= CHILDREN ===========");
            foreach (Component com in col.GetComponentsInChildren(typeof(Component)))
            {
                Puts(com.GetType() + " " + com);
            }
        }

        private BaseEntity DoRay(Vector3 Pos, Vector3 Aim)
        {
            Ray ray = new Ray(Pos, Aim);
            float distance = 100000f;
            BaseEntity target = null;
            var hits = Physics.RaycastAll(ray);
            foreach (var hit in hits)
            {
                if (hit.collider != null && isPluginDev && dumpAll)
                    Dump(hit.GetEntity());
                if (hit.distance < distance)
                {
                    distance = hit.distance;
                    target = hit.GetEntity();
                }
            }
            return target;
        }

        void SendHelpText(BasePlayer player)
        {
            if (hasAccess(player)) SendReply(player, helpProd);
        }


        public new void SendReply(BasePlayer player, string format, params object[] args)
        {
            if (printToConsoleInsteadOfChat)
            {
                player.ConsoleMessage(String.Format(format, args));
            } else
            {
                player.SendMessage(String.Format(format, args));
            }
        }
        #endregion

        #region -Passive Mode-
        private void Passive_GetBuildingBlockOwner(BasePlayer player, BuildingBlock block)
        {
            object findownerblock = FindOwnerBlock(block);
            if (findownerblock is bool)
            {
                SendReply(player, noBlockOwnerfound);
                return;
            }
            ulong ownerid = (ulong)findownerblock;
            SendReply(player, string.Format("Building Owner: {0}", FindPlayerName(ownerid)));
        }

        private void Passive_GetCodelockWhitelisted(BasePlayer player, BaseEntity block)
        {
            BaseEntity slotent = block.GetSlot(BaseEntity.Slot.Lock);
            CodeLock codelock = slotent?.GetComponent<CodeLock>();
            if (codelock != null)
            {
                List<ulong> whitelisted = codelockwhitelist.GetValue(codelock) as List<ulong>;
                SendReply(player, codeLockList);
                if (whitelisted.Count == 0)
                {
                    SendReply(player, noCodeAccess);
                    return;
                }
                foreach (ulong userid in whitelisted)
                {
                    Passive_SendBasePlayerFind(player, userid);
                }
            } else
            {
                SendReply(player, "Codelock was not found.");
                return;
            }
        }

        private void Passive_SendBasePlayerFind(BasePlayer player, ulong ownerid)
        {
            SendReply(player, string.Format("{0}", FindPlayerName(ownerid)));
        }
        #endregion
    }
}
  