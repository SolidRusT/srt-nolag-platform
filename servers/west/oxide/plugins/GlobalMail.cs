using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Global Mail", "Hovmodet", "1.0.4")]
    [Description("Allows you to mail a single item from monuments back to your base.")]
    class GlobalMail : RustPlugin
    {
        List<BuildingPrivlidge> BuildingsWithMailBoxes = new List<BuildingPrivlidge>();
        Dictionary<ulong, StorageContainer> GlobalMailBoxes = new Dictionary<ulong, StorageContainer>();

        List<Vector3> SmallMonumentsPosition = new List<Vector3>();
        List<Vector3> MediumMonumentsPosition = new List<Vector3>();
        List<Vector3> LargeMonumentsPosition = new List<Vector3>();
        Vector3 OutpostPosition = new Vector3();
        Vector3 BanditCampPosition = new Vector3();
        #region Setup
        bool AutoPlaceMailbox = true;
        bool CanEmptyAtMonument = false;
        bool CanFillAtBase = false;
        bool CanPlaceMultipleMailBoxes = false;
        bool CanOpenMailBoxAtBuildingBlock = false;

        bool SpawnMailAtSmallMonuments = true;
        bool SpawnMailAtMediumMonuments = true;
        bool SpawnMailAtLargeMonuments = true;
        bool SpawnMailAtBanditCamp = true;
        bool SpawnMailAtOutpost = true;

        bool GivePlayersMailBoxBlueprint = false;

        int MaxStackSizeInMailbox = 0;

        protected override void LoadDefaultConfig()
        {
            Config["AutoPlaceMailbox"] = true;
            Config["CanEmptyAtMonument"] = false;
            Config["CanFillAtBase"] = false;
            Config["CanPlaceMultipleMailBoxes"] = false;
            Config["CanOpenMailBoxAtBuildingBlock"] = false;
            Config["SpawnMailAtSmallMonuments"] = true;
            Config["SpawnMailAtMediumMonuments"] = true;
            Config["SpawnMailAtLargeMonuments"] = true;
            Config["SpawnMailAtBanditCamp"] = true;
            Config["SpawnMailAtOutpost"] = true;
            Config["GivePlayersMailBoxBlueprint"] = false;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["en_CantPlace"] = "You dont have permission to place a mailbox here.",
                ["en_BuildingOccupied"] = "There is allready a mailbox at this building.",
                ["en_CantEmpty"] = "You can't empty your mailbox from here.",
                ["en_CantSend"] = "You can't send out mail from here.",
                ["en_MailFull"] = "Your mailbox is full, emtpy it at your base.",
                ["en_CantAccess"] = "You don't have permissions to use this mailbox.",
                ["en_NoPermission"] = "You don't have permissions to look into other players mailbox.",
                ["en_InvalidSteamID"] = "'{0}' is not a valid Steam ID",
                ["en_CantStack"] = "You cannot stack items in the mailbox.",
                ["en_NoUsePerm"] = "You dont have permissions to use mailboxes."
            }, this);
        }

        #endregion


        #region ChatCmd
        [ChatCommand("placemail")]
        private void cmdPlaceMail(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, "globalmail.placemail") == false)
            {
                SendReply(player, "no perm");
                return;
            }
            
            if (!previewMailBox.ContainsKey(player))
            {
                PlacementMailbox pm = new PlacementMailbox();
                pm.player = player;
                previewMailBox.Add(player, pm);
            }
        }
            [ChatCommand("mail")]
        private void cmdMail(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 1)
            {
                ulong id;
                if (ulong.TryParse(args[0], out id))
                {
                        OpenPlayersMailbox(player, id);
                }
                else
                {
                    SendReply(player, string.Format(lang.GetMessage("en_InvalidSteamID", this), args[0]));
                }
            }

        }
        #endregion

        #region Oxide Hooks


        object OnPlayerTick(BasePlayer player, PlayerTick msg, bool wasPlayerStalled)
        {
            if (previewMailBox.ContainsKey(player))
            {
                if (previewMailBox[player].placed == false)
                {
                    UpdateMailBoxPosition(player);
                }
            }
            return null;
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if(input.IsDown(BUTTON.FIRE_PRIMARY) & previewMailBox.ContainsKey(player))
            {
                previewMailBox[player].mailbox.OwnerID = 0;
                previewMailBox[player].placed = true;
                previewMailBox.Remove(player);
            }
            else if (input.IsDown(BUTTON.FIRE_SECONDARY) & previewMailBox.ContainsKey(player))
            {
                previewMailBox[player].mailbox.Kill();
                previewMailBox[player].placed = true;
                previewMailBox.Remove(player);
            }
        }

        private void Init()
        {
            permission.RegisterPermission("globalmail.lootmail", this);
            permission.RegisterPermission("globalmail.placemail", this);
            permission.RegisterPermission("globalmail.use", this);

            AutoPlaceMailbox = (bool)Config["AutoPlaceMailbox"];
            CanEmptyAtMonument = (bool)Config["CanEmptyAtMonument"];
            CanFillAtBase = (bool)Config["CanFillAtBase"];
            CanPlaceMultipleMailBoxes = (bool)Config["CanPlaceMultipleMailBoxes"];
            CanOpenMailBoxAtBuildingBlock = (bool)Config["CanOpenMailBoxAtBuildingBlock"];

            SpawnMailAtSmallMonuments = (bool)Config["SpawnMailAtSmallMonuments"];
            SpawnMailAtMediumMonuments = (bool)Config["SpawnMailAtMediumMonuments"];
            SpawnMailAtLargeMonuments = (bool)Config["SpawnMailAtLargeMonuments"];
            SpawnMailAtBanditCamp = (bool)Config["SpawnMailAtBanditCamp"];
            SpawnMailAtOutpost = (bool)Config["SpawnMailAtOutpost"];

            GivePlayersMailBoxBlueprint = (bool)Config["GivePlayersMailBoxBlueprint"];


        }

        object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (planner.GetOwnerPlayer() == null)
                return null;

            if(prefab.prefabID == 2697131904)
            {
                BasePlayer player = planner.GetOwnerPlayer();
                if(!player.IsBuildingAuthed())
                {
                    
                    SendReply(player, lang.GetMessage("en_CantPlace", this));
                    return false;
                }

                if(BuildingsWithMailBoxes.Contains(player.GetBuildingPrivilege()) & !CanPlaceMultipleMailBoxes )
                {
                    SendReply(player, lang.GetMessage("en_BuildingOccupied", this));
                    return false;
                }

                    BuildingsWithMailBoxes.Add(player.GetBuildingPrivilege());

            }
            return null;
        }

        private object CanAcceptItem(ItemContainer container, Item item)
        {
            if(container.entityOwner == null)
            return null;

            if (container.entityOwner.ShortPrefabName == "woodbox_deployed" & container.entityOwner.transform.position == new Vector3(0,0,0))
            {
                if (item.GetOwnerPlayer() == null)
                    return null;

                BasePlayer player = item.GetOwnerPlayer();

                if(player.IsBuildingAuthed() & !CanFillAtBase)
                {
                    SendReply(player, lang.GetMessage("en_CantSend", this));
                    return ItemContainer.CanAcceptResult.CannotAccept;
                }
                else if (container.itemList.Count > 0)
                {
                    if (container.itemList[0].IsLocked())
                    {
                        SendReply(player, lang.GetMessage("en_MailFull", this));
                        return ItemContainer.CanAcceptResult.CannotAccept;
                    }
                }

            }

            return null;
        }

        object CanMoveItem(Item item, PlayerInventory playerLoot, uint targetContainer, int targetSlot, int amount)
        {
            if (item.IsLocked())
                return false;

            BasePlayer player = null;
            if (item.GetOwnerPlayer() != null)
                player = item.GetOwnerPlayer();
            else player = playerLoot.GetComponent<BasePlayer>();

            ItemContainer container = playerLoot.FindContainer(targetContainer);
            ItemContainer originalContainer = item.GetRootContainer();

            if (container == null) return null;
            if (originalContainer == null) return null;
            if (container == originalContainer)
                return null;

            string containerIs = "";
            string orginalContainerIs = "";
            if (container.entityOwner == null)
                containerIs = "player";

            else if(container.entityOwner.ShortPrefabName == "woodbox_deployed" & container.entityOwner.transform.position == new Vector3(0, 0, 0))
                containerIs = "mail";


            if (originalContainer.entityOwner == null)
                orginalContainerIs = "player";

            else if (originalContainer.entityOwner.ShortPrefabName == "woodbox_deployed" & originalContainer.entityOwner.transform.position == new Vector3(0, 0, 0))
                orginalContainerIs = "mail";


            if (containerIs != "mail" & orginalContainerIs != "mail")
                return null;

            if (containerIs == "mail" & item.amount > 1)
            {
                SendReply(player, lang.GetMessage("en_CantStack", this));
                return false;
            }

            Item i = container.GetSlot(targetSlot);

            if (i == null)
                return null;
            
            if (item.info.itemid != i.info.itemid)
                return false;

            if (item.info.itemid == i.info.itemid & item.amount + i.amount <= item.MaxStackable())
                return null;

            if (containerIs == "mail" & i.amount > 1)
            {
                SendReply(player, lang.GetMessage("en_CantStack", this));
                return false;
            }

            if (item.amount + i.amount > item.MaxStackable())
                return false;

            return null;
        }

        object CanStackItem(Item item, Item targetItem)
        {
            if (item.IsLocked() | targetItem.IsLocked())
                return false;


            return null;
        }

        void OnEntityKill(Mailbox mailBox)
        {
            if (BuildingsWithMailBoxes.Contains(mailBox.GetBuildingPrivilege()))
                BuildingsWithMailBoxes.Remove(mailBox.GetBuildingPrivilege());
        }

        object OnEntityTakeDamage(Mailbox mailBox, HitInfo info)
        {
            if (mailBox.OwnerID == 0)
                return false;
            return null;
        }

        object CanLootEntity(BasePlayer player, Mailbox container)
        {
            return false;
        }

        bool CanUseMailbox(BasePlayer player, Mailbox mailbox)
        {
            if (player.IsBuildingAuthed() | mailbox.OwnerID == 0)
            {
                if (permission.UserHasPermission(player.UserIDString, "globalmail.use") == true)
                    OpenContainer(player);
                else SendReply(player, lang.GetMessage("en_NoUsePerm", this));
            }
            else
                SendReply(player, lang.GetMessage("en_CantAccess", this));

            return false;
        }

        void OnServerInitialized(bool initial)
        {
            FindMonuments();
            LoadAllMailBoxes();

            if(AutoPlaceMailbox)
                FindRecyclers();
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if(GivePlayersMailBoxBlueprint)
            {
                learnMailBox(player);
            }
        }

        #endregion

        #region Functions

        Dictionary<BasePlayer,PlacementMailbox> previewMailBox = new Dictionary<BasePlayer, PlacementMailbox>();
        private void UpdateMailBoxPosition(BasePlayer player)
        {
            if (!previewMailBox.ContainsKey(player))
                return;

            if (previewMailBox[player].placed)
            {
                previewMailBox.Remove(player);
                return;
            }

            Mailbox m = previewMailBox[player].mailbox;

            RaycastHit hit;
            var layers = LayerMask.GetMask("Construction", "Default", "Resource", "Terrain", "Water", "World");
            Physics.Raycast(player.eyes.HeadRay(), out hit, 10f,layers);
            if (hit.point == Vector3.zero)
                return;

            Quaternion rot = player.GetNetworkRotation();
            rot.z = 0;
            rot.x = 0;
            if (previewMailBox[player].mailbox == null)
            {
                BaseEntity box = GameManager.server.CreateEntity("assets/prefabs/deployable/mailbox/mailbox.deployed.prefab", hit.point, rot);
                box.OwnerID = 1;
                UnityEngine.Object.Destroy(box.GetComponent<DestroyOnGroundMissing>());
                UnityEngine.Object.Destroy(box.GetComponent<GroundWatch>());
                box.Spawn();
                previewMailBox[player].mailbox = (Mailbox)box;
                m = previewMailBox[player].mailbox;
            }
            else
            {
                if (hit.point == m.transform.position & player.transform.rotation == m.transform.rotation)
                    return;

                m.transform.position = hit.point;
                m.transform.rotation = rot;
                m.SendNetworkUpdateImmediate();
            }
        }

        private void learnMailBox(BasePlayer player)
        {
            var playerInfo = SingletonComponent<ServerMgr>.Instance.persistance.GetPlayerInfo(player.userID);
            if (playerInfo.unlockedItems.Contains(-586784898) == false)
            {
                playerInfo.unlockedItems.Add(-586784898);
                SingletonComponent<ServerMgr>.Instance.persistance.SetPlayerInfo(player.userID, playerInfo);
                player.SendNetworkUpdateImmediate();
                player.ClientRPCPlayer(null, player, "UnlockedBlueprint", 0);
            }
        }

        private string FindMonumentClosestToRecycler(Vector3 RecyclerPosition)
        {
            float distance = float.MaxValue;
            float dist;

            dist = Vector3.Distance(BanditCampPosition, RecyclerPosition);
            if (dist < distance) distance = dist;
            if (distance < 150)
            {
                return "bandit";
            }

            dist = Vector3.Distance(OutpostPosition, RecyclerPosition);
            if (dist < distance) distance = dist;
            if (distance < 150)
            {
                return "outpost";
            }

            for (int i = 0; i < SmallMonumentsPosition.Count; i++)
            {
                dist = Vector3.Distance(SmallMonumentsPosition[i], RecyclerPosition);
                if (dist < distance) distance = dist;
            }
            if(distance < 150)
            {
                return "small";
            }

            for (int i = 0; i < MediumMonumentsPosition.Count; i++)
            {
                dist = Vector3.Distance(MediumMonumentsPosition[i], RecyclerPosition);
                if (dist < distance) distance = dist;
            }
            if (distance < 150)
            {
                return "medium";
            }

            for (int i = 0; i < LargeMonumentsPosition.Count; i++)
            {
                dist = Vector3.Distance(LargeMonumentsPosition[i], RecyclerPosition);
                if (dist < distance) distance = dist;
            }
            if (distance < 150)
            {
                return "large";
            }

            return "ingen "+ RecyclerPosition.ToString();
        }

        private void FindRecyclers()
        {
            Mailbox[] mBox = UnityEngine.Object.FindObjectsOfType<Mailbox>();
            foreach(Mailbox m in mBox)
            {
                if (m.OwnerID == 0)
                    m.Kill();
            }
            Recycler[] Recyclers = UnityEngine.Object.FindObjectsOfType<Recycler>();
            foreach(Recycler recycler in Recyclers)
            {
                string type = FindMonumentClosestToRecycler(recycler.transform.position);
                if (type == "small" & SpawnMailAtSmallMonuments || type == "medium" & SpawnMailAtMediumMonuments || type == "large" & SpawnMailAtLargeMonuments || type == "bandit" & SpawnMailAtBanditCamp || type == "outpost" & SpawnMailAtOutpost)
                {
                    BaseEntity box = GameManager.server.CreateEntity("assets/prefabs/deployable/mailbox/mailbox.deployed.prefab", recycler.transform.position - recycler.transform.right * 1.2f, recycler.transform.rotation);
                    box.OwnerID = 0;
                    UnityEngine.Object.Destroy(box.GetComponent<DestroyOnGroundMissing>());
                    UnityEngine.Object.Destroy(box.GetComponent<GroundWatch>());
                    box.Spawn();
                }
            }
        }

        private void FindMonuments()
        {
            GameObject[] allobjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            for (int i = 0; i < allobjects.Length; i++)
            {
                GameObject gobject = allobjects[i];

                if (gobject.name.Contains("autospawn/monument"))
                {
                    var pos = gobject.transform.position;
                    if (!pos.Equals(new Vector3(0, 0, 0)))
                    {
                        if (gobject.name.Contains("/small/") | gobject.name.Contains("lighthouse") | gobject.name.Contains("warehouse") | gobject.name.Contains("supermarket") | gobject.name.Contains("gas_station") && SpawnMailAtSmallMonuments)
                        {
                            if (!SmallMonumentsPosition.Contains(pos))
                                SmallMonumentsPosition.Add(pos);
                        }
                        else if (gobject.name.Contains("bandit_town") & SpawnMailAtBanditCamp)
                        {
                            BanditCampPosition = pos;
                        }
                        else if (gobject.name.Contains("compound") & SpawnMailAtOutpost)
                        {
                            OutpostPosition = pos;
                        }
                        else if (gobject.name.Contains("/medium/") & !gobject.name.Contains("bandit_town") & !gobject.name.Contains("compound") & SpawnMailAtMediumMonuments)
                        {
                            if (!MediumMonumentsPosition.Contains(pos))
                            { 
                                MediumMonumentsPosition.Add(pos);
                            }
                        }
                        else if (gobject.name.Contains("/large/") & SpawnMailAtLargeMonuments)
                        {
                            if (!LargeMonumentsPosition.Contains(pos))
                                LargeMonumentsPosition.Add(pos);
                        }
                    }
                }
            }
        }

        private void LoadAllMailBoxes()
        {
            BaseEntity[] Boxes = UnityEngine.Object.FindObjectsOfType<BaseEntity>();
            Mailbox[] MailBoxes = UnityEngine.Object.FindObjectsOfType<Mailbox>();
            foreach (BaseEntity Box in Boxes)
            {
                if(Box.ShortPrefabName == "woodbox_deployed" & Box.transform.position == new Vector3(0,0,0))
                {
                    if (!GlobalMailBoxes.ContainsKey(Box.OwnerID))
                    {
                        StorageContainer container = null;
                        container = Box.GetComponent<StorageContainer>();
                        GlobalMailBoxes.Add(Box.OwnerID, container);
                    }
                }
            }
            foreach (Mailbox Mail in MailBoxes)
            {
                if(Mail.OwnerID != 0 & Mail.GetBuildingPrivilege() != null)
                        if(!BuildingsWithMailBoxes.Contains(Mail.GetBuildingPrivilege()))
                            BuildingsWithMailBoxes.Add(Mail.GetBuildingPrivilege());
            }
        }

        private void OpenContainer(BasePlayer player, ulong steamid = 0)
        {
            StorageContainer container = null;
            if (steamid != 0)
            {
                if (GlobalMailBoxes.ContainsKey(steamid))
                    container = GlobalMailBoxes[steamid];
            }
            else
            {
                if (GlobalMailBoxes.ContainsKey(player.userID))
                    container = GlobalMailBoxes[player.userID];
            }
            
            if (container == null)
            {
                BaseEntity box = GameManager.server.CreateEntity("assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab", new Vector3(0, 0, 0));
                if (steamid == 0)
                    box.OwnerID = player.userID;
                else box.OwnerID = steamid;
                (box as BaseNetworkable).limitNetworking = true;
                UnityEngine.Object.Destroy(box.GetComponent<DestroyOnGroundMissing>());
                UnityEngine.Object.Destroy(box.GetComponent<GroundWatch>());
                box.Spawn();
                container = box.GetComponent<StorageContainer>();
                if (steamid == 0)
                {
                    if (!GlobalMailBoxes.ContainsKey(player.userID))
                        GlobalMailBoxes.Add(player.userID, container);
                }
                else
                {
                    if (!GlobalMailBoxes.ContainsKey(steamid))
                        GlobalMailBoxes.Add(steamid, container);
                }
            }

            container.inventory.capacity = 1;


            if (!CanEmptyAtMonument & steamid == 0)
            {
                foreach (Item i in container.inventory.itemList)
                {
                    i.LockUnlock(!player.IsBuildingAuthed());
                }
            }


          timer.Once(0.1f, () =>
            {
                player.EndLooting();
                if (!player.inventory.loot.StartLootingEntity(container, false)) { return; }
                player.inventory.loot.AddContainer(container.inventory);
                player.inventory.loot.SendImmediate();
                player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel",container.panelName);
                player.SendNetworkUpdate();
            });
        }

        private void OpenPlayersMailbox(BasePlayer player, ulong steamid)
        {
            if (permission.UserHasPermission(player.UserIDString, "globalmail.lootmail") == true)
                OpenContainer(player, steamid);
            else 
                SendReply(player, lang.GetMessage("en_NoPermission", this));
        }
        #endregion
    }
    #region Classes
    class PlacementMailbox
    {
       public Mailbox mailbox;
       public BasePlayer player;
       public bool placed;
    }
    #endregion
}
