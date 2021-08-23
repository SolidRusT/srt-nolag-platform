using System;
using System.Linq;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;
using Rust;
using System.Collections.Generic;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("PatrolBoat", "Colon Blow & Suparious", "1.0.24")]
    class PatrolBoat : RustPlugin
    {
        //fix for being able to spawn boat on top of iceberg or close to it.
        //fix for null errors
        //fix for spotlight
        //add boombox to front of boat

        #region Configuration

        static float DefaultPatrolBoatMovementSpeed = 8f;
        bool OnlyOneActivePatrolBoat = true;
        static bool ShowWaterSplash = false;
        static bool FuelNotRequired = false;
        static bool RandomOceanLootSpawn = true;
        static int MaterialsForPatrolBoat = 5000;
        static int MaterialID = 69511070;
        static int EngineFuelAmount = 50;
        static int LightFuelAmount = 50;

        bool Changed;

        bool isRestricted;
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            Config.Clear();
            LoadVariables();
        }

        private void LoadConfigVariables()
        {
            CheckCfg("Effect - Show water splash effect when moving ? ", ref ShowWaterSplash);
            CheckCfg("Usage - Only 1 Active PatrolBoat per player ? ", ref OnlyOneActivePatrolBoat);
            CheckCfg("Loot - Toggle Random Deep Ocean Loot Spawns (Past 2000 X or Z coords): ", ref RandomOceanLootSpawn);
            CheckCfg("Materials - PatrolBoat- Amount of Wood needed to build : ", ref MaterialsForPatrolBoat);
            CheckCfg("Materials - Item ID of material needed (default is metal fragments) : ", ref MaterialID);
            CheckCfgFloat("Speed - Default PatrolBoat Movement Speed : ", ref DefaultPatrolBoatMovementSpeed);
            CheckCfg("Fuel - Engine - Default starting amount of Low Grade Fuel : ", ref EngineFuelAmount);
            CheckCfg("Fuel - SearchLight - Default starting amount of Low Grade Fuel : ", ref LightFuelAmount);
            CheckCfg("Fuel - Boat does not need fuel to run : ", ref FuelNotRequired);

        }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        private void CheckCfgFloat(string Key, ref float var)
        {

            if (Config[Key] != null)
                var = Convert.ToSingle(Config[Key]);
            else
                Config[Key] = var;
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
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

        #endregion

        #region Localization

        string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["notauthorized"] = "You are not authorized to use that command !!",
            ["notowner"] = "You must be owner of boat to pilot it !!!",
            ["boatlocked"] = "Boat is currenlty locked. Unlock to access.",
            ["haspatrolboatalready"] = "You already have a patrolboat in the world !!!",
            ["captain"] = "You are now the Captain of this boat !!!",
            ["alreadyadded"] = "That part is already installed !!!",
            ["missingmaterials"] = "You are missing the required materials to uprade to that !! ",
            ["endofworld"] = "Movement blocked !!! You are at the end of the playable world !!!",
            ["notstandingwater"] = "You must be in deeper water but NOT swimming to build a patrolboat !!"
        };

        #endregion

        #region Data

        BaseEntity newPatrolBoat;
        static Dictionary<ulong, string> hasPatrolBoat = new Dictionary<ulong, string>();

        static List<uint> storedPatrolBoats = new List<uint>();
        private DynamicConfigFile data;
        private bool initialized;

        void Loaded()
        {
            LoadVariables();
            permission.RegisterPermission("patrolboat.builder", this);
            permission.RegisterPermission("patrolboat.admin", this);
            lang.RegisterMessages(Messages, this);
            data = Interface.Oxide.DataFileSystem.GetFile("patrolboat_data");
            timer.Once(3f, () => RestorePatrolBoats());
        }

        private void OnServerInitialized()
        {
            initialized = true;
            LoadData();
        }
        private void OnServerSave()
        {
            SaveData();
        }

        private void RestorePatrolBoats()
        {
            if (storedPatrolBoats.Count > 0)
            {
                BaseEntity[] objects = BaseEntity.saveList.Where(x => x is BaseEntity).ToArray();
                if (objects != null)
                {
                    foreach (var obj in objects)
                    {
                        if (!obj.IsValid() || obj.IsDestroyed)
                            continue;

                        if (storedPatrolBoats.Contains(obj.net.ID))
                        {
                            int boatfuellevel = 0;
                            int turretammo = 0;
                            string codestr = "0";
                            string guestcodestr = "0";

                            foreach (Transform child in obj.GetComponent<Transform>())
                            {
                                if (child.name.Contains("woodbox"))
                                {
                                    ItemContainer component1 = child.GetComponent<StorageContainer>().inventory;
                                    if (component1 != null) boatfuellevel = component1.GetAmount(-946369541, true);
                                }
                                if (child.name.Contains("autoturret_deployed"))
                                {
                                    ItemContainer component3 = child.GetComponent<StorageContainer>()?.inventory;
                                    if (component3 != null) turretammo = component3.GetAmount(-1211166256, true);
                                }
                                if (child.name.Contains("keypad/lock.code"))
                                {
                                    CodeLock codelock = child.GetComponent<CodeLock>() ?? null;
                                    if (codelock != null) codestr = codelock.code;
                                    if (codelock != null) guestcodestr = codelock.guestCode;
                                }

                            }

                            var boatfuel = boatfuellevel;
                            var ammo = turretammo;
                            var spawnpos = obj.transform.position;
                            var spawnrot = obj.transform.rotation;
                            var userid = obj.OwnerID;

                            storedPatrolBoats.Remove(obj.net.ID);
                            obj.Invoke("KillMessage", 0.1f);
                            timer.Once(2f, () => RespawnPatrolBoat(spawnpos, spawnrot, userid, boatfuel, 0, ammo, codestr, guestcodestr));
                        }
                    }
                }
            }
        }

        void SaveData() => data.WriteObject(storedPatrolBoats.ToList());
        void LoadData()
        {
            try
            {
                storedPatrolBoats = data.ReadObject<List<uint>>();
            }
            catch
            {
                storedPatrolBoats = new List<uint>();
            }
        }

        bool isAllowed(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        public void AddPlayerPatrolBoat(ulong id)
        {
            if (!OnlyOneActivePatrolBoat) return;
            if (hasPatrolBoat.ContainsKey(id)) return;
            hasPatrolBoat.Add(id, "");
        }

        public void RemovePlayerPatrolBoat(ulong id)
        {
            if (!OnlyOneActivePatrolBoat) return;
            if (!hasPatrolBoat.ContainsKey(id)) return;
            hasPatrolBoat.Remove(id);
        }

        #endregion

        #region Hooks

        public void BuildPatrolBoat(BasePlayer player)
        {
            string prefabstr = "assets/bundled/prefabs/static/chair.invisible.static.prefab";
            var waterheight = TerrainMeta.WaterMap.GetHeight(player.transform.position);
            var spawnpos = new Vector3(player.transform.position.x, waterheight + 0.5f, player.transform.position.z);
            newPatrolBoat = GameManager.server.CreateEntity(prefabstr, spawnpos, new Quaternion(), true);
            var mount = newPatrolBoat.GetComponent<BaseMountable>();
            mount.isMobile = true;
            newPatrolBoat.enableSaving = true;
            newPatrolBoat.OwnerID = player.userID;
            newPatrolBoat?.Spawn();
            var addpatrolboat = newPatrolBoat.gameObject.AddComponent<PatrolBoatEntity>();

            ItemContainer component1 = addpatrolboat.fuelbox.GetComponent<StorageContainer>().inventory;
            Item addfuel = ItemManager.CreateByItemID(-946369541, EngineFuelAmount);
            component1.itemList.Add(addfuel);
            addfuel.parent = component1;
            addfuel.MarkDirty();

            AddPlayerPatrolBoat(player.userID);
            storedPatrolBoats.Add(newPatrolBoat.net.ID);
            mount.MountPlayer(player);
            SaveData();
        }

        public void RespawnPatrolBoat(Vector3 spawnpos, Quaternion spawnrot, ulong userid, int fuel, int lightfuel, int ammo, string codestr, string guestcodestr)
        {
            string prefabstr = "assets/bundled/prefabs/static/chair.invisible.static.prefab";
            newPatrolBoat = GameManager.server.CreateEntity(prefabstr, spawnpos, spawnrot, true);
            var mount = newPatrolBoat.GetComponent<BaseMountable>();
            mount.isMobile = true;
            newPatrolBoat.enableSaving = true;
            newPatrolBoat.OwnerID = userid;
            newPatrolBoat?.Spawn();
            var addpatrolboat = newPatrolBoat.gameObject.AddComponent<PatrolBoatEntity>() ?? null;
            if (fuel > 0)
            {
                ItemContainer component1 = addpatrolboat.fuelbox.GetComponent<StorageContainer>().inventory ?? null;
                if (component1 != null)
                {
                    Item addfuel = ItemManager.CreateByItemID(-946369541, fuel);
                    component1.itemList.Add(addfuel);
                    addfuel.parent = component1;
                    addfuel.MarkDirty();
                }
            }
            if (lightfuel > 0)
            {
                ItemContainer component2 = addpatrolboat.searchlight.GetComponent<StorageContainer>().inventory ?? null;
                if (component2 != null)
                {
                    Item addfuel = ItemManager.CreateByItemID(-946369541, lightfuel);
                    component2.itemList.Add(addfuel);
                    addfuel.parent = component2;
                    addfuel.MarkDirty();
                }
            }
            if (ammo > 0)
            {
                ItemContainer component3 = addpatrolboat.turret.GetComponent<StorageContainer>().inventory ?? null;
                if (component3 != null)
                {
                    Item addammo = ItemManager.CreateByItemID(-1211166256, ammo);
                    component3.itemList.Add(addammo);
                    addammo.parent = component3;
                    addammo.MarkDirty();
                }
            }
            if (codestr != "0")
            {
                var codelock = addpatrolboat.boatlockfront.GetComponent<CodeLock>() ?? null;
                if (codelock != null)
                {
                    codelock.whitelistPlayers.Add(userid);
                    codelock.code = codestr;
                    codelock.guestCode = guestcodestr;
                    codelock.SetFlag(BaseEntity.Flags.Locked, true, false);
                }
                var codelockr = addpatrolboat.boatlockrear.GetComponent<CodeLock>() ?? null;
                if (codelockr != null)
                {
                    codelockr.whitelistPlayers.Add(userid);
                    codelockr.code = codestr;
                    codelockr.guestCode = guestcodestr;
                    codelockr.SetFlag(BaseEntity.Flags.Locked, true, false);
                }
            }

            AddPlayerPatrolBoat(userid);
            storedPatrolBoats.Add(newPatrolBoat.net.ID);
            SaveData();
        }

        bool CheckUpgradeMats(BasePlayer player, int itemID, int amount, string str)
        {
            int HasReq = player.inventory.GetAmount(itemID);
            if (HasReq >= amount)
            {
                player.inventory.Take(null, itemID, amount);
                player.Command("note.inv", itemID, -amount);
                return true;
            }
            ItemDefinition itemDefinition = ItemManager.FindItemDefinition(itemID);

            SendReply(player, "You need " + amount + " " + itemDefinition.shortname + " to build " + str);
            return false;
        }

        public bool IsStandingInWater(BasePlayer player)
        {
            var position = player.transform.position;
            var waterdepth = (TerrainMeta.WaterMap.GetHeight(position) - TerrainMeta.HeightMap.GetHeight(position));
            if (position.y < 0f && waterdepth >= 0.4f) return true;
            return false;
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!player.isMounted) return;
            var activepatrolboat = player.GetMounted().GetComponentInParent<PatrolBoatEntity>() ?? null;
            if (activepatrolboat == null) return;
            if (player.GetMounted() != activepatrolboat.entity) return;
            if (!isAllowed(player, "patrolboat.builder")) return;
            if (activepatrolboat.boatlockfront.IsLocked() == true) return;
            if (input != null)
            {
                if (input.WasJustPressed(BUTTON.FORWARD)) { activepatrolboat.moveforward = true; }
                if (input.WasJustReleased(BUTTON.FORWARD)) activepatrolboat.moveforward = false;
                if (input.WasJustPressed(BUTTON.BACKWARD)) { activepatrolboat.movebackward = true; }
                if (input.WasJustReleased(BUTTON.BACKWARD)) activepatrolboat.movebackward = false;
                if (input.WasJustPressed(BUTTON.RIGHT)) activepatrolboat.rotright = true;
                if (input.WasJustReleased(BUTTON.RIGHT)) activepatrolboat.rotright = false;
                if (input.WasJustPressed(BUTTON.LEFT)) activepatrolboat.rotleft = true;
                if (input.WasJustReleased(BUTTON.LEFT)) activepatrolboat.rotleft = false;
                if (input.WasJustPressed(BUTTON.JUMP))
                {
                    activepatrolboat.moveforward = false;
                    activepatrolboat.movebackward = false;
                    activepatrolboat.rotright = false;
                    activepatrolboat.rotleft = false;
                }
                return;
            }
        }

        private object OnEntityGroundMissing(BaseEntity entity)
        {
            var patrolboat = entity.GetComponentInParent<PatrolBoatEntity>();
            if (patrolboat != null) return false;
            return null;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || entity.net == null) return;
            if (storedPatrolBoats.Contains(entity.net.ID))
            {
                storedPatrolBoats.Remove(entity.net.ID);
                SaveData();
            }
        }

        private void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            if (mountable == null || player == null) return;
            var patrolboatentity = mountable.GetComponentInParent<PatrolBoatEntity>();
            if (patrolboatentity == null) return;
            if (mountable != patrolboatentity.entity) return;
            if (patrolboatentity != null)
            {
                if (!FuelNotRequired) player.gameObject.AddComponent<FuelControl>();
                SendReply(player, msg("captain", player.UserIDString));
                if (patrolboatentity.boatlockfront.IsLocked() == true) SendReply(player, msg("boatlocked", player.UserIDString));
            }
        }

        private void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            if (mountable == null || player == null) return;
            var patrolboatentity = mountable.GetComponentInParent<PatrolBoatEntity>() ?? null;
            if (patrolboatentity == null) return;
            if (mountable != patrolboatentity.entity) return;
            if (patrolboatentity != null)
            {
                RemoveFuelControl(player);
                patrolboatentity.ResetMovement();
                patrolboatentity.RefreshAll();
            }
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null) return;
            var isboat = entity.GetComponentInParent<PatrolBoatEntity>() ?? null;
            if (isboat != null) hitInfo.damageTypes.ScaleAll(0);
            return;
        }

        object CanPickupEntity(BasePlayer player, BaseCombatEntity entity)
        {
            if (entity == null || player == null) return null;
            if (entity.GetComponentInParent<PatrolBoatEntity>()) return false;
            return null;
        }

        void Unload()
        {
            DestroyAll<FuelControl>();
        }

        void DestroyAll<T>()
        {
            var objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }

        #endregion

        #region Commands

        [ChatCommand("patrolboat.build")]
        void cmdPatrolBoatBuild(BasePlayer player, string command, string[] args)
        {
            if (OnlyOneActivePatrolBoat && hasPatrolBoat.ContainsKey(player.userID)) { SendReply(player, msg("haspatrolboatalready", player.UserIDString)); return; }
            if (!isAllowed(player, "patrolboat.builder")) { SendReply(player, msg("notauthorized", player.UserIDString)); return; }
            if (!IsStandingInWater(player) || player.IsSwimming()) { SendReply(player, msg("notstandingwater", player.UserIDString)); return; }
            if (player.isMounted) return;
            if (CheckUpgradeMats(player, MaterialID, MaterialsForPatrolBoat, "Base PatrolBoat")) BuildPatrolBoat(player);
        }

        [ChatCommand("patrolboat.light")]
        void cmdPatrolBoatLight(BasePlayer player, string command, string[] args)
        {
            if (!isAllowed(player, "patrolboat.builder")) { SendReply(player, msg("notauthorized", player.UserIDString)); return; }
            if (!player.isMounted) return;
            var activepatrolboat = player.GetMounted().GetComponentInParent<PatrolBoatEntity>();
            if (activepatrolboat == null) return;
            if (activepatrolboat.boatlockfront != null && activepatrolboat.boatlockfront.IsLocked() == true) { SendReply(player, msg("boatlocked", player.UserIDString)); return; }
            var light = activepatrolboat.searchlight;
            if (light != null)
            {
                if (light.IsOn())
                {
                    activepatrolboat.searchlight.SetFlag(BaseEntity.Flags.On, false, false, true);
                    activepatrolboat.searchlight.SetFlag(BaseEntity.Flags.Reserved8, false);
                    return;
                }
                else
                {
                    activepatrolboat.searchlight.SetFlag(BaseEntity.Flags.On, true, false, true);
                    activepatrolboat.searchlight.SetFlag(BaseEntity.Flags.Reserved8, true);
                }
            }
        }

        [ChatCommand("patrolboat.turret")]
        void cmdPatrolBoatTurret(BasePlayer player, string command, string[] args)
        {
            if (!isAllowed(player, "patrolboat.builder")) { SendReply(player, msg("notauthorized", player.UserIDString)); return; }
            if (!player.isMounted) return;
            var activepatrolboat = player.GetMounted().GetComponentInParent<PatrolBoatEntity>();
            if (activepatrolboat == null) return;
            if (activepatrolboat.boatlockfront != null && activepatrolboat.boatlockfront.IsLocked() == true) { SendReply(player, msg("boatlocked", player.UserIDString)); return; }
            var gun = activepatrolboat.turret;
            if (gun != null)
            {
                if (gun.IsOn())
                {
                    activepatrolboat.turret.SetFlag(BaseEntity.Flags.On, false, false);
                    return;
                }
                else activepatrolboat.turret.SetFlag(BaseEntity.Flags.On, true, true);
            }
        }

        [ChatCommand("patrolboat.loc")]
        void cmdPatrolBoatLoc(BasePlayer player, string command, string[] args)
        {
            if (!isAllowed(player, "patrolboat.builder")) { SendReply(player, msg("notauthorized", player.UserIDString)); return; }
            string location = player.transform.position.x + " / " + player.transform.position.z;
            SendReply(player, "you position is : " + location);
        }

        [ChatCommand("patrolboat.destroy")]
        void cmdPatrolBoatDestroy(BasePlayer player, string command, string[] args)
        {
            BaseEntity[] objects = BaseEntity.saveList.Where(x => x is BaseEntity).ToArray();
            if (objects != null)
            {
                foreach (var obj in objects)
                {
                    if (!obj.IsValid() || obj.IsDestroyed)
                        continue;

                    var isboat = obj.GetComponent<PatrolBoatEntity>();
                    if (isboat && obj.OwnerID == player.userID)
                    {
                        storedPatrolBoats.Remove(obj.net.ID);
                        SaveData();
                        isboat.entity.Invoke("KillMessage", 0.1f);
                    }
                }
            }
        }

        [ChatCommand("patrolboat.destroyall")]
        void cmdPatrolBoatDestroyAll(BasePlayer player, string command, string[] args)
        {
            if (!isAllowed(player, "patrolboat.admin")) { SendReply(player, msg("notauthorized", player.UserIDString)); return; }
            BaseEntity[] objects = BaseEntity.saveList.Where(x => x is BaseEntity).ToArray();
            if (objects != null)
            {
                foreach (var obj in objects)
                {
                    if (!obj.IsValid() || obj.IsDestroyed)
                        continue;

                    var isboat = obj.GetComponent<PatrolBoatEntity>();
                    if (isboat)
                    {
                        storedPatrolBoats.Remove(obj.net.ID);
                        SaveData();
                        isboat.entity.Invoke("KillMessage", 0.1f);
                    }
                }
            }
        }

        #endregion

        #region PatrolBoat Entity

        private class PatrolBoatEntity : BaseEntity
        {
            private PatrolBoat patrolboat;
            public BaseEntity entity;
            public BasePlayer pilot;
            private BaseEntity trianglefloor1, trianglefloor2, floor1, floor1box, floor2, floor3, wall1, wall2, wall3, wall1r, wall2r, wall3r, wallback, wallfrontl, wallfrontr, roof;
            public BaseEntity doorwayfront, doorwayback, searchlight, turret, boatlockfront, boatlockrear, fuelbox, boombox;
            private BaseEntity engine, rudder, ladder, rearcopilotmount, rearpilotmount, copilotmount, deckleft, deckright, windowright, windowleft, frontdoor, backdoor;
            private BaseEntity pilotchairbase, pilotchairright, pilotchairleft, pilotchairback;
            private BaseEntity copilotchairbase, copilotchairright, copilotchairleft, copilotchairback;
            private BaseEntity rearpilotchairbase, rearpilotchairright, rearpilotchairleft, rearpilotchairback;
            private BaseEntity rearcopilotchairbase, rearcopilotchairright, rearcopilotchairleft, rearcopilotchairback;
            private Vector3 entitypos;
            private Quaternion entityrot;
            private ulong ownerid;
            private int counter, refreshcounter;
            public bool ismoving, moveforward, movebackward, rotright, rotleft;
            private float waterheight;
            private Vector3 movedirection, rotdirection, startloc, startrot, endloc;
            private float steps, incrementor;
            public int currentfuel;
            private string prefabboombox = "assets/prefabs/voiceaudio/boombox/boombox.static.prefab";
            private string prefabchair = "assets/bundled/prefabs/static/chair.invisible.static.prefab";
            private string prefabsign = "assets/prefabs/deployable/signs/sign.small.wood.prefab";
            private string prefabfloor = "assets/prefabs/building core/floor/floor.prefab";
            private string prefabfloortriangle = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab";
            private string prefabbox = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
            private string prefabtop = "assets/prefabs/building core/wall.low/wall.low.prefab";
            private string prefabwall = "assets/prefabs/building core/wall/wall.prefab";
            private string prefabwindowwall = "assets/prefabs/building core/wall.window/wall.window.prefab";
            private string prefabdoorway = "assets/prefabs/building core/wall.doorway/wall.doorway.prefab";
            private string prefabladder = "assets/prefabs/building/ladder.wall.wood/ladder.wooden.wall.prefab";
            private string prefabwallframe = "assets/prefabs/building core/wall.frame/wall.frame.prefab";
            private string prefablight = "assets/prefabs/deployable/search light/searchlight.deployed.prefab";
            private string prefabwindow = "assets/prefabs/building/wall.window.reinforcedglass/wall.window.glass.reinforced.prefab";
            private string prefabturret = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab";
            private string prefabengine = "assets/bundled/prefabs/radtown/oil_barrel.prefab";
            private string prefabrudder = "assets/prefabs/deployable/signs/sign.post.single.prefab";
            private string prefabfuelbox = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
            private string prefabdeck = "assets/prefabs/deployable/signs/sign.small.wood.prefab";
            private string prefabdoors = "assets/prefabs/building/wall.frame.garagedoor/wall.frame.garagedoor.prefab";
            private string prefabcodelock = "assets/prefabs/locks/keypad/lock.code.prefab";

            private void Awake()
            {
                entity = GetComponent<BaseEntity>();
                entitypos = entity.transform.position;
                entityrot = Quaternion.identity;
                ownerid = entity.OwnerID;
                gameObject.name = "Patrol Boat";
                patrolboat = new PatrolBoat();
                counter = 0;
                refreshcounter = 0;
                incrementor = 0;
                ismoving = false;
                moveforward = false;
                movebackward = false;
                rotright = false;
                rotleft = false;
                startrot = entity.transform.eulerAngles;
                startloc = entity.transform.position;
                steps = DefaultPatrolBoatMovementSpeed;
                currentfuel = 0;

                SpawnPatrolBoat();
                SpawnChairs();
                RefreshAll();
            }

            private BaseEntity SpawnPart(string prefab, BaseEntity entitypart, bool setactive, int eulangx, int eulangy, int eulangz, float locposx, float locposy, float locposz, BaseEntity parent, ulong skinid)
            {
                entitypart = new BaseEntity();
                entitypart = GameManager.server.CreateEntity(prefab, entity.transform.position, entity.transform.rotation, setactive);
                entitypart.transform.localEulerAngles = new Vector3(eulangx, eulangy, eulangz);
                entitypart.transform.localPosition = new Vector3(locposx, locposy, locposz);
                entitypart.SetParent(parent, 0, false, false);
                entitypart.skinID = skinid;
                entitypart.enableSaving = false;
                entitypart?.Spawn();
                SpawnRefresh(entitypart);
                return entitypart;
            }

            private void SpawnRefresh(BaseNetworkable entity1)
            {
                var hasstab = entity1.GetComponent<StabilityEntity>();
                if (hasstab)
                {
                    hasstab.grounded = true;
                }
                var hasmount = entity1.GetComponent<BaseMountable>();
                if (hasmount)
                {
                    hasmount.isMobile = true;
                }
                var hasblock = entity1.GetComponent<BuildingBlock>();
                if (hasblock)
                {
                    hasblock.SetGrade(BuildingGrade.Enum.Metal);
                    hasblock.SetHealthToMax();
                    hasblock.UpdateSkin();
                    hasblock.ClientRPC(null, "RefreshSkin");
                }
                if (entity1 is Signage)
                {
                    entity1.GetComponent<BaseEntity>().SetFlag(BaseEntity.Flags.Busy, true, true);
                }
            }

            private void SpawnChairs()
            {
                pilotchairright = SpawnPart(prefabsign, pilotchairright, false, 0, 90, 90, 0.40f, 0.0f, -0.3f, entity, 1);
                pilotchairleft = SpawnPart(prefabsign, pilotchairleft, false, 0, 270, 270, -0.30f, 0.0f, -0.3f, entity, 1);
                pilotchairbase = SpawnPart(prefabsign, pilotchairbase, false, 270, 0, 0, 0.0f, 0.4f, 0.2f, entity, 1);
                pilotchairback = SpawnPart(prefabsign, pilotchairback, false, 0, 0, 0, 0.0f, 0.4f, -0.3f, entity, 1);

                copilotmount = SpawnPart(prefabchair, copilotmount, false, 0, 0, 0, -2f, -0.4f, 0f, entity, 1);
                copilotchairright = SpawnPart(prefabsign, copilotchairright, false, 0, 90, 90, -2.5f, 0.0f, -0.3f, entity, 1);
                copilotchairleft = SpawnPart(prefabsign, copilotchairleft, false, 0, 270, 270, -1.6f, 0.0f, -0.3f, entity, 1);
                copilotchairbase = SpawnPart(prefabsign, copilotchairbase, false, 270, 0, 0, -2.05f, 0.0f, 0.2f, entity, 1);
                copilotchairback = SpawnPart(prefabsign, copilotchairback, false, 0, 0, 0, -2.05f, 0.0f, -0.3f, entity, 1);

                rearcopilotmount = SpawnPart(prefabchair, rearcopilotmount, false, 0, 180, 0, -2f, -0.4f, -5.6f, entity, 1);
                rearcopilotchairright = SpawnPart(prefabsign, rearcopilotchairright, false, 0, 90, 90, -2.5f, 0.0f, -5.8f, entity, 1);
                rearcopilotchairleft = SpawnPart(prefabsign, rearcopilotchairleft, false, 0, 270, 270, -1.6f, 0.0f, -5.8f, entity, 1);
                rearcopilotchairbase = SpawnPart(prefabsign, rearcopilotchairbase, false, 270, 0, 0, -2.05f, 0.0f, -5.3f, entity, 1);
                rearcopilotchairback = SpawnPart(prefabsign, rearcopilotchairback, false, 0, 180, 0, -2.05f, 0.0f, -5.3f, entity, 1);

                rearpilotmount = SpawnPart(prefabchair, rearpilotmount, false, 0, 180, 0, 0f, -0.4f, -5.6f, entity, 1);
                rearpilotchairright = SpawnPart(prefabsign, rearpilotchairright, false, 0, 90, 90, 0.40f, 0.0f, -5.8f, entity, 1);
                rearpilotchairleft = SpawnPart(prefabsign, rearpilotchairleft, false, 0, 270, 270, -0.30f, 0.0f, -5.8f, entity, 1);
                rearpilotchairbase = SpawnPart(prefabsign, rearpilotchairbase, false, 270, 0, 0, 0.0f, 0.0f, -5.3f, entity, 1);
                rearpilotchairback = SpawnPart(prefabsign, rearpilotchairback, false, 0, 180, 0, 0.0f, 0.0f, -5.3f, entity, 1);
            }

            private void SpawnPatrolBoat()
            {
                trianglefloor1 = SpawnPart(prefabfloortriangle, trianglefloor1, false, 0, 0, 0, -1f, -0.5f, 1.5f, entity, 1);
                trianglefloor2 = SpawnPart(prefabfloortriangle, trianglefloor2, false, 0, 0, 0, -1f, 0.4f, 1.5f, entity, 1);
                floor1 = SpawnPart(prefabfloor, floor1, false, 0, 0, 0, -1f, -0.5f, 0f, entity, 1);
                floor1box = SpawnPart(prefabbox, floor1box, false, 0, 180, 0, 0f, -0.5f, 0f, entity, 1);
                floor2 = SpawnPart(prefabfloor, floor2, false, 0, 90, 0, -1f, -0.5f, -3f, entity, 1);
                floor3 = SpawnPart(prefabfloor, floor3, false, 0, 90, 0, -1f, -0.5f, -6f, entity, 1);
                wallfrontl = SpawnPart(prefabtop, wallfrontl, false, 0, -30, 0, -0.25f, -0.5f, 2.8f, entity, 1);
                wallfrontr = SpawnPart(prefabtop, wallfrontr, false, 0, 210, 0, -1.75f, -0.5f, 2.8f, entity, 1);
                wall1 = SpawnPart(prefabtop, wall1, false, 0, 180, 0, -2.5f, -0.5f, 0f, entity, 1);
                wall1r = SpawnPart(prefabtop, wall1r, false, 0, 0, 0, 0.5f, -0.5f, 0f, entity, 1);
                wall2 = SpawnPart(prefabwindowwall, wall2, false, 0, 180, 0, -2.5f, -0.5f, -3f, entity, 1);
                wall2r = SpawnPart(prefabwindowwall, wall2r, false, 0, 0, 0, 0.5f, -0.5f, -3f, entity, 1);
                wall3 = SpawnPart(prefabtop, wall3, false, 0, 180, 0, -2.5f, -0.5f, -6f, entity, 1);
                wall3r = SpawnPart(prefabtop, wall3r, false, 0, 0, 0, 0.5f, -0.5f, -6f, entity, 1);
                wallback = SpawnPart(prefabtop, wallback, false, 0, 90, 0, -1f, -0.5f, -7.5f, entity, 1);
                doorwayfront = SpawnPart(prefabwallframe, doorwayfront, false, 0, 270, 0, -1f, -0.5f, -1.5f, entity, 1);
                doorwayback = SpawnPart(prefabwallframe, doorwayback, false, 0, 90, 0, -1f, -0.5f, -4.5f, entity, 1);
                roof = SpawnPart(prefabfloor, roof, false, 0, 0, 0, -1f, 2.3f, -3f, entity, 1);
                windowright = SpawnPart(prefabwindow, windowright, false, 0, 0, 0, 0f, 1f, 0f, wall2, 1);
                windowleft = SpawnPart(prefabwindow, windowleft, false, 0, 0, 0, 0f, 1f, 0f, wall2r, 1);
                boombox = SpawnPart(prefabboombox, boombox, true, 0, 0, 0, -1f, -0.3f, 3f, entity, 1);
                turret = SpawnPart(prefabturret, turret, true, 0, 0, 0, -1f, 0.1f, 3f, entity, 1);
                if (turret)
                {
                    turret.GetComponent<AutoTurret>().targetLostEffect = new GameObjectRef();
                    turret.GetComponent<AutoTurret>().focusSoundFreqMax = 0f;
                    turret.GetComponent<AutoTurret>().authorizedPlayers.Add(new ProtoBuf.PlayerNameID
                    {
                        userid = ownerid,
                        username = pilot == null ? "" : pilot.displayName,
                        ShouldPool = true
                    });
                }
                fuelbox = SpawnPart(prefabfuelbox, fuelbox, true, 90, 0, 0, -1f, 0f, -7.5f, entity, 1);
                var fuelboxcontainer = fuelbox.GetComponent<StorageContainer>();
                if (fuelboxcontainer) fuelboxcontainer.inventory.capacity = 1;
                deckleft = SpawnPart(prefabdeck, deckleft, false, -70, 90, 0, 2.1f, -0.2f, -1.1f, floor3, 1);
                deckright = SpawnPart(prefabdeck, deckright, false, -70, 90, 0, 2.1f, -0.2f, 1.1f, floor3, 1);

                engine = SpawnPart(prefabengine, engine, false, 0, 0, 90, -0.5f, 0.0f, -7.5f, entity, 1);

                rudder = SpawnPart(prefabrudder, rudder, false, 0, 270, 130, -1f, 0.4f, -7.4f, entity, 1);
                frontdoor = SpawnPart(prefabdoors, frontdoor, true, 0, 90, 0, -1f, -0.5f, -1.5f, entity, 1);
                backdoor = SpawnPart(prefabdoors, backdoor, true, 0, 270, 0, -1f, -0.5f, -4.5f, entity, 1);
                boatlockfront = SpawnPart(prefabcodelock, boatlockfront, true, 0, 90, 0, 0.25f, 1f, -1.45f, entity, 1);
                boatlockrear = SpawnPart(prefabcodelock, boatlockrear, true, 0, 90, 0, 0.25f, 1f, -4.6f, entity, 1);

                searchlight = SpawnPart(prefablight, searchlight, false, 0, 0, 0, -1f, 2.3f, -3f, entity, 1);
            }

            private void SpawnLadder()
            {
                ladder = SpawnPart(prefabladder, ladder, false, 0, 0, 0, 0f, 1.0f, -4.6f, entity, 1);
            }

            private bool hitSomething(Vector3 position)
            {
                var directioncheck = new Vector3();
                if (moveforward) directioncheck = position + (transform.forward * 4);
                if (movebackward) directioncheck = position - (transform.forward * 8);
                if (GamePhysics.CheckSphere(directioncheck, 1f, UnityEngine.LayerMask.GetMask("World", "Construction", "Default"), 0)) return true;
                return false;
            }

            private bool isStillInWater(Vector3 position)
            {
                var waterdepth = (TerrainMeta.WaterMap.GetHeight(position) - TerrainMeta.HeightMap.GetHeight(position));
                if (waterdepth >= 0.5f) return true;
                return false;
            }

            private bool PlayerIsMounted()
            {
                bool flag = entity.GetComponent<BaseMountable>().IsMounted();
                return flag;
            }

            private void SplashEffect()
            {
                Effect.server.Run("assets/content/vehicles/boats/effects/splashloop.prefab", deckright.transform.position);
                Effect.server.Run("assets/content/vehicles/boats/effects/splashloop.prefab", deckleft.transform.position);
            }

            private bool HasFuel()
            {
                if (FuelNotRequired) return true;
                ItemContainer component = fuelbox.GetComponent<StorageContainer>().inventory;
                int iteme = component.GetAmount(-946369541, true);
                currentfuel = iteme;
                Item item = component.FindItemByItemID(-946369541);
                if (item == null || item.amount < 1)
                {
                    return false;
                }
                counter = counter + 1;
                if (counter >= 50)
                {
                    item.UseItem(1);
                    counter = 0;
                }
                return true;
            }

            private void CheckLock()
            {
                if (boatlockfront.IsLocked()) { frontdoor.SetFlag(BaseEntity.Flags.Locked, true, false); fuelbox.SetFlag(BaseEntity.Flags.Locked, true, false); }
                else { frontdoor.SetFlag(BaseEntity.Flags.Locked, false, false); fuelbox.SetFlag(BaseEntity.Flags.Locked, false, false); }

                if (boatlockrear.IsLocked()) backdoor.SetFlag(BaseEntity.Flags.Locked, true, false);
                else backdoor.SetFlag(BaseEntity.Flags.Locked, false, false);
            }

            private void FixedUpdate()
            {
                CheckLock();
                var currentloc = entity.transform.position;
                var floatLocation = floor2.transform.position;
                if (searchlight != null && searchlight.IsOn())
                {
                    var pilot = entity.GetComponent<BaseMountable>()._mounted as BasePlayer ?? null;
                    if (pilot == null) { searchlight.SetFlag(BaseEntity.Flags.On, false, false); }
                    if (pilot != null)
                    {
                        Vector3 vector3 = searchlight.GetComponent<SearchLight>().eyePoint.transform.position + (pilot.eyes.BodyForward() * 100f);
                        searchlight.GetComponent<SearchLight>().SetTargetAimpoint(vector3);
                        searchlight.SendNetworkUpdateImmediate();
                    }
                }
                if (!ismoving && !(moveforward || movebackward || rotright || rotleft))
                {
                    var adjFloatLocation = new Vector3(currentloc.x, WaterSystem.GetHeight(floatLocation) + 0.5f, currentloc.z);
                    entity.transform.position = adjFloatLocation;
                    RefreshAll();
                    return;
                }
                if (!PlayerIsMounted()) { ResetMovement(); RefreshAll(); ismoving = false; return; }

                waterheight = TerrainMeta.WaterMap.GetHeight(currentloc);
                startloc = new Vector3(currentloc.x, WaterSystem.GetHeight(floatLocation) + 0.5f, currentloc.z);
                startrot = entity.transform.eulerAngles;
                startrot.x = 0f;
                if (rotright) rotdirection = new Vector3(startrot.x, startrot.y + 1, startrot.z);
                else if (rotleft) rotdirection = new Vector3(startrot.x, startrot.y - 1, startrot.z);

                if (moveforward && HasFuel()) endloc = startloc + (transform.forward * steps) * Time.deltaTime;
                else if (movebackward && HasFuel()) endloc = startloc + (transform.forward * -steps) * Time.deltaTime;

                if (hitSomething(endloc)) { endloc = startloc; ResetMovement(); RefreshAll(); return; }
                if (!isStillInWater(endloc)) { endloc = startloc; ResetMovement(); RefreshAll(); return; }
                if (endloc.x >= 3900 || endloc.x <= -3900 || endloc.z >= 3900 || endloc.z <= -3900) { endloc = startloc; ResetMovement(); RefreshAll(); return; }

                if (endloc == new Vector3(0f, 0f, 0f)) endloc = startloc;
                entity.transform.eulerAngles = rotdirection;
                entity.transform.localPosition = endloc;
                if (ShowWaterSplash) SplashEffect();
                RefreshAll();
            }

            public void ResetMovement()
            {
                ismoving = false;
                moveforward = false;
                movebackward = false;
                rotright = false;
                rotleft = false;
            }

            public void RefreshAll()
            {
                //if (!PlayerIsMounted()) { ResetMovement(); return; }
                entity.transform.hasChanged = true;
                var entitymount = entity.GetComponent<BaseMountable>() ?? null;
                if (entitymount != null)
                {
                    entitymount.isMobile = true;
                }
                entity.SendNetworkUpdateImmediate();

                if (entity.children != null)
                    for (int i = 0; i < entity.children.Count; i++)
                    {
                        entity.children[i].transform.hasChanged = true;
                        var isblock = entity.children[i].GetComponent<BuildingBlock>() ?? null;
                        if (isblock != null)
                        {
                            isblock.UpdateSkin();
                            isblock.ClientRPC(null, "RefreshSkin");
                        }
                        var hasmount = entity.children[i].GetComponent<BaseMountable>() ?? null;
                        if (hasmount != null)
                        {
                            hasmount.isMobile = true;
                        }
                        entity.children[i].SendNetworkUpdateImmediate(false);
                        entity.children[i].UpdateNetworkGroup();
                    }
            }

            public void OnDestroy()
            {
                if (hasPatrolBoat.ContainsKey(ownerid)) hasPatrolBoat.Remove(ownerid);
                if (entity != null && !entity.IsDestroyed) { entity.Invoke("KillMessage", 0.1f); }
            }
        }

        #endregion

        #region FuelControl and Fuel Cui

        class FuelControl : MonoBehaviour
        {
            BasePlayer player;
            PatrolBoatEntity boat;
            public string anchormaxstr;
            public string colorstr;
            Vector3 playerpos;
            PatrolBoat instance;
            int count;

            void Awake()
            {
                instance = new PatrolBoat();
                player = GetComponentInParent<BasePlayer>();
                boat = player.GetMounted().GetComponentInParent<PatrolBoatEntity>();
                playerpos = player.transform.position;
                count = 0;
            }

            void FixedUpdate()
            {
                if (boat == null) { OnDestroy(); return; }
                if (player == null) { OnDestroy(); return; }
                var boatfuel = boat.currentfuel;
                playerpos = player.transform.position;
                if (boatfuel >= 500) boatfuel = 500;
                if (boatfuel <= 0) boatfuel = 0;
                if (FuelNotRequired) boatfuel = 500;
                fuelIndicator(player, boatfuel);
            }

            public void fuelIndicator(BasePlayer player, int fuel)
            {
                DestroyCui(player);
                var displayfuel = fuel;
                var fuelstr = displayfuel.ToString();
                var colorstrred = "0.6 0.1 0.1 0.8";
                var colorstryellow = "0.8 0.8 0.0 0.8";
                var colorstrgreen = "0.0 0.6 0.1 0.8";
                colorstr = colorstrgreen;
                if (fuel >= 451) anchormaxstr = "0.60 0.145";
                if (fuel >= 401 && fuel <= 450) anchormaxstr = "0.58 0.145";
                if (fuel >= 351 && fuel <= 400) anchormaxstr = "0.56 0.145";
                if (fuel >= 301 && fuel <= 350) anchormaxstr = "0.54 0.145";
                if (fuel >= 251 && fuel <= 300) anchormaxstr = "0.52 0.145";
                if (fuel >= 201 && fuel <= 250) anchormaxstr = "0.50 0.145";
                if (fuel >= 151 && fuel <= 200) { anchormaxstr = "0.48 0.145"; colorstr = colorstryellow; }
                if (fuel >= 101 && fuel <= 150) { anchormaxstr = "0.46 0.145"; colorstr = colorstryellow; }
                if (fuel >= 51 && fuel <= 100) { anchormaxstr = "0.44 0.145"; colorstr = colorstrred; }
                if (fuel <= 50) { anchormaxstr = "0.42 0.145"; colorstr = colorstrred; }
                var fuelindicator = new CuiElementContainer();
                fuelindicator.Add(new CuiButton
                {
                    Button = { Command = $"", Color = "0.0 0.0 0.0 0.3" },
                    RectTransform = { AnchorMin = "0.40 0.12", AnchorMax = "0.60 0.15" },
                    Text = { Text = (""), FontSize = 18, Color = "1.0 1.0 1.0 1.0", Align = TextAnchor.MiddleLeft }
                }, "Overall", "fuelGuia");

                fuelindicator.Add(new CuiButton
                {
                    Button = { Command = $"", Color = colorstr },
                    RectTransform = { AnchorMin = "0.40 0.125", AnchorMax = anchormaxstr },
                    Text = { Text = (fuelstr), FontSize = 14, Color = "1.0 1.0 1.0 0.6", Align = TextAnchor.MiddleRight }
                }, "Overall", "fuelGui");

                CuiHelper.AddUi(player, fuelindicator);
            }

            void DestroyChargeCui(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "recharge");
            }

            void DestroyCui(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "fuelGui");
                CuiHelper.DestroyUi(player, "fuelGuia");
            }

            void OnDestroy()
            {
                DestroyChargeCui(player);
                DestroyCui(player);
                Destroy(this);
            }
        }

        #endregion

        #region CuiHelpers

        void RemoveFuelControl(BasePlayer player)
        {
            var hasgyro = player.GetComponent<FuelControl>();
            if (hasgyro != null) GameObject.Destroy(hasgyro);
            return;
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            RemoveFuelControl(player);
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            RemoveFuelControl(player);
        }

        #endregion

    }
}