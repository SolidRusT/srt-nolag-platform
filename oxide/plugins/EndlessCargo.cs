
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;


namespace Oxide.Plugins
{
    [Info("Endless Cargo", "OG61", "1.1.1")]
    [Description("Calls new cargo ship into game as soon as one departs.")]
    public class EndlessCargo : CovalencePlugin
    {
      
        #region Config
        const string cargoPrefab = "assets/content/vehicles/boats/cargoship/cargoshiptest.prefab";
        const int MaxShips = 1;
        private const string perm = "endlesscargo.admin";
        readonly System.Random random = new System.Random();

        private class PluginConfig
        { 
            [JsonProperty(PropertyName = "Update Rate (Seconds)")]
            public float UpdateRate = 5f;

            [JsonProperty(PropertyName = "How far off shore to spawn ship in grid blocks 1-10 (Default 5)")]
            public int GridBlocksOffShore = 5;

            [JsonProperty(PropertyName = "NPC spawn on ship (Default true)")]
            public bool NpcSpawn = true;
            
            [JsonProperty(PropertyName = "Egress duration in minutes (Default 10)")]
            public int EgressMin = 10;

            [JsonProperty(PropertyName = "Event duration in minutes (Default 50)")]
            public int DurationMin = 50;

            [JsonProperty(PropertyName = "Loot round spacing in minutes (Default 10)")]
            public int LootSpacing = 10;

            [JsonProperty(PropertyName = "Loot rounds 1-3 (Default 3)")]
            public int LootRounds = 3;

            [JsonProperty(PropertyName = "Enable Log file (true/false)")]
            public bool LogToFile = true;

            [JsonProperty(PropertyName = "Log output to console (true/false)")]
            public bool LogToConsole = true;
        }

        private PluginConfig _config;

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch (Exception e)
            {
                LoadDefaultConfig();
                LogError(e.Message);
            }
            /* Check for more than 3 loot rounds. Setting loot rounds to more than 3 will work but the ships horn
             * will sound everytime the loot round timer triggers and an index out of bounds error will 
             * print to console. This appears to be a bug in the game. 
             */
            if (_config.LootRounds > 3) _config.LootRounds = 3;

            if (_config.GridBlocksOffShore > 10 || _config.GridBlocksOffShore < 1) _config.GridBlocksOffShore = 5;  // set to default if outside reasonible range
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new PluginConfig();

        protected override void SaveConfig() => Config.WriteObject(_config);
        #endregion //Config

        #region Data
        private List<CargoShip> _activeCargoShips = new List<CargoShip>();
        #endregion //Data

        #region uMod Hooks

        private void OnServerInitialized()
        {
            NextTick(() =>
            {
                _activeCargoShips = UnityEngine.Object.FindObjectsOfType<CargoShip>().ToList();
                CheckCargoShip();
            });

            timer.Every(_config.UpdateRate, CheckCargoShip);

            //ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "cargoship.event_enabled false");
            //Setting cargoship.event_enabled to false does not disable the event. For now just kill the new ship
            server.Command($"cargoship.egress_duration_minutes {_config.EgressMin}");
            server.Command($"ai.npc_spawn_on_cargo_ship {_config.NpcSpawn}");
            server.Command($"cargoship.event_duration_minutes {_config.DurationMin}");
            server.Command($"cargoship.loot_round_spacing_minutes {_config.LootSpacing}");
            server.Command($"cargoship.loot_rounds {_config.LootRounds}");
        }
       
        private void OnEntitySpawned(CargoShip ship)
        {  
            if (_activeCargoShips.Count >= MaxShips) 
            {   // This should never execute if the default event is turned off but it does.
                ship.Kill();
                 Logger("NormalGameCall") ;
                return;
            }
            NextTick(() =>
            {
                 _activeCargoShips.Add(ship);
                CheckCargoShip();
            });
        }




        #endregion //Oxide Hooks  

        #region Helpers

        private string GetLang(string key, string id = "", params object[] args)
        {
           return string.Format(lang.GetMessage(key, this, id), args);
        }


        private void Logger(string key, IPlayer player = null, params object[] args)
        {
            string s = GetLang(key, player != null ? player.Id : "",args);
            string ps = "";
            if (player !=null) ps = $"{player.Name} ({player.Id}) ";
            s =  $"[{DateTime.Now}] {ps} {s}";
            if (_config.LogToFile) LogToFile("CargoControl", s, this);
            if (_config.LogToConsole) Log(s);
        }
      
        private void Message(string key, IPlayer player, params object[] args)
        { 
           player.Reply(GetLang(key, player.Id, args));
        }

        private void CheckCargoShip()
        {
            _activeCargoShips.RemoveAll(p => !p.IsValid() || !p.gameObject.activeInHierarchy || p.HasFlag(BaseEntity.Flags.Reserved8));
            if (_activeCargoShips.Count < MaxShips) SpawnCargo(); 
        }

        private void SpawnCargo()
        {

            var worldSize = (ConVar.Server.worldsize);
            var gridWidth = (worldSize * 0.0066666666666667f);
            float step = worldSize / gridWidth;
            int steps = (int)(((worldSize / step) / 2) + _config.GridBlocksOffShore);

            float posX;
            float posZ;
            float rotation;
            var ranXZ = random.Next(-100, 100); //Randomly choose if we are randomizing x or Z 
            var ranPN = random.Next(-100, 100); //Randomly choose if we are positive x or z or negative x or z
            ranPN = ranPN > 0 ? 1 : -1;
            if (ranXZ > 0)
            {
                posX = random.Next(-steps, steps) * step;
                posZ = steps * step * ranPN; //Z will be on the edge of map randomly north or south
                rotation = posZ > 0 ? 180 : 0; //Rotate ship to point toward land
            }
            else
            {
                posX = steps * step * ranPN; //X will be on the edge of map randomly east or west
                posZ = random.Next(-steps, steps) * step;
                rotation = posX > 0 ? 270 : 90; //Rotate ship to point toward land
            }
            Vector3 position = new Vector3(posX, 0f, posZ);
            Quaternion myTransform = new Quaternion();
            Vector3 rot = myTransform.eulerAngles;
            rot = new Vector3(rot.x, rot.y + rotation, rot.z);
            myTransform = Quaternion.Euler(rot);
            CargoShip cargo = (CargoShip)GameManager.server.CreateEntity(cargoPrefab, position, myTransform);
            if (cargo != null)
            {
                cargo.Spawn();
                Logger("ShipSpawned", null, position.x, position.z);
            }
            else
            {
                Logger("ShipSpawnError");
            }

        }     
        
        
        #endregion //Helpers

        #region Commands
    [Command("KillCargo"), Permission(perm)]
        private void KillCargo(IPlayer player)
        {
            if (_activeCargoShips.Count > 0)
            {


                foreach (var c in _activeCargoShips)
                {
                    c.Kill();
                }
                Logger("KillCargo", player);
            }
            else Logger("NoActiveShips");
        }
        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NormalGameCall"] = "Normal cargo event triggered. Ship killed.",
                ["KillcargoRcon"] = "Killcargo issued from RCon",
                ["NoActiveShips"] = "Killcargo command issued but no active ships",
                ["KillCargo"] = "Killcargo command executed",
                ["ShipSpawned"] = "Cargo ship spawned at X={0}  Z={1}",
                ["ConfigError"] = "Error reading config file. Defaut configuration used.",
                ["ShipSpawnError"] = "Cargo ship spawn error. Spawn failed."
            }, this) ;
        }
        #endregion
    }
}
