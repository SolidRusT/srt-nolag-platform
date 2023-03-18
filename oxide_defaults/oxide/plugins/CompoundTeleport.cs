using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Compound Teleport", "kwamaking", "2.0.2")]
    [Description("Teleport through the death screen to Bandit Camp, Outpost, or any configured monument.")]
    class CompoundTeleport : RustPlugin
    {
        #region Variables

        private const string UsePermission = "compoundteleport.use";
        private const string OutPostPrefab = "assets/bundled/prefabs/autospawn/monument/medium/compound.prefab";
        private const string BanditTownPrefab = "assets/bundled/prefabs/autospawn/monument/medium/bandit_town.prefab";
        private const int TeleportLayer = ~(1 << 8 | 1 << 10 | 1 << 18 | 1 << 21 | 1 << 24 | 1 << 28 | 1 << 29);
        private const int TeleportRadius = 50;
        private const int Timer = 150;
        private const int MaxAttemptsToFindTeleportLocation = 25;
        private const float MaxDistance = 100f;
        private const float MaxDistanceFromGround = 100f;
        private readonly string[] ValidTeleportColliders = { "carpark", "concrete_slabs", "road", "train_track", "pavement", "platform", "walkway", "helipad" };
        private Dictionary<ulong, List<SleepingBag>> playerTeleportLocations = new Dictionary<ulong, List<SleepingBag>>();
        private CompoundTeleportConfiguration pluginConfiguration = new CompoundTeleportConfiguration();

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            try
            {
                permission.RegisterPermission(UsePermission, this);
                pluginConfiguration = Config.ReadObject<CompoundTeleportConfiguration>();
            }
            catch (Exception e)
            {
                base.LoadConfig();
                Puts($"Failed to load Compound Teleport configuration, using default configuration: {e.Message} {e.StackTrace}");
            }

            CreateTeleportLocationsForActivePlayers();

        }

        protected override void LoadDefaultConfig()
        {
            pluginConfiguration = CompoundTeleportConfiguration.BuildDefaultConfiguration();
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(pluginConfiguration);

        private void OnPlayerConnected(BasePlayer player) => CreateTeleportLocations(player);

        private void OnPlayerDisconnected(BasePlayer player) => DestroyTeleportLocations(player);

        private void OnPlayerRespawn(BasePlayer player, SleepingBag bag)
        {
            if (!permission.UserHasPermission(player.UserIDString, UsePermission)) return;

            if (!playerTeleportLocations.ContainsKey(player.userID)) return;

            if (playerTeleportLocations[player.userID].Any(b => b.net.ID == bag.net.ID))
            {
                bag.transform.position = AttemptToFindTeleportPosition(player, bag.transform.position);
                RemoveHostility(player);
            }
        }

        void Unload()
        {
            playerTeleportLocations.Keys.ToList().ForEach(
                playerId => playerTeleportLocations[playerId].ForEach(bag => bag.Kill())
            );

            playerTeleportLocations.Clear();
        }

        #endregion

        #region Helpers

        private void RemoveHostility(BasePlayer player)
        {
            if (player.IsHostile() && pluginConfiguration.removeHostility)
            {
                player.State.unHostileTimestamp = 0.0;
                player.DirtyPlayerState();
            }
        }

        private void CreateTeleportLocationsForActivePlayers()
        {
            try
            {
                BasePlayer
                .activePlayerList
                .ToList()
                .ForEach(player => CreateTeleportLocations(player));
            }
            catch (Exception e)
            {
                Puts($"Failed to create teleport locations for active players: {e.Message} {e.StackTrace} ");
            }
        }

        private void CreateTeleportLocations(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, UsePermission)) return;

            DestroyTeleportLocations(player);

            playerTeleportLocations.Add(player.userID, new List<SleepingBag>());

            pluginConfiguration.teleportLocations.ForEach(teleportLocation =>
            {
                var teleportPosition = FindMonumentPosition(teleportLocation.monumentPrefab);
                if (null != teleportPosition && teleportLocation.enabled)
                    playerTeleportLocations[player.userID].Add(
                        CreateTeleportLocation(player, teleportLocation, (Vector3)teleportPosition)
                    );
            });
        }

        private void DestroyTeleportLocations(BasePlayer player)
        {
            if (!playerTeleportLocations.ContainsKey(player.userID))
                return;

            playerTeleportLocations[player.userID].ForEach(bag => bag.Kill());
            playerTeleportLocations.Remove(player.userID);
        }

        private Vector3 FindMonumentPosition(string monumentPrefab)
        {

            var monumentPosition = TerrainMeta.Path.Monuments
            .Where(m => m.name.ToLower() == monumentPrefab)
            .FirstOrDefault()?
            .transform
            .position;

            if (null == monumentPosition)
                Puts($"Failed to find a location for Monument: {monumentPrefab}");

            return monumentPosition ?? new Vector3();
        }

        private Vector3 AttemptToFindTeleportPosition(BasePlayer player, Vector3 monumentLocation)
        {
            for (int i = 0; i < MaxAttemptsToFindTeleportLocation; i++)
            {
                RaycastHit rayHit;
                Vector3 positionAttempt = monumentLocation + (UnityEngine.Random.insideUnitSphere * TeleportRadius);
                positionAttempt.y = monumentLocation.y + MaxDistanceFromGround;
                if (Physics.Raycast(positionAttempt, Vector3.down, out rayHit, MaxDistance, TeleportLayer, QueryTriggerInteraction.Ignore))
                {
                    if (rayHit.collider is TerrainCollider)
                        return rayHit.point;
                    else if (ValidTeleportColliders.Any(c => rayHit.collider.name.Contains(c)))
                        return rayHit.point;
                }
            }

            Puts($"Failed to find a teleport location for {player.displayName}, using default spawn.");

            return ServerMgr.FindSpawnPoint(player).pos;
        }

        private SleepingBag CreateTeleportLocation(BasePlayer player, TeleportLocation teleportLocation, Vector3 position)
        {
            GameObject gameObject = new GameObject();
            SleepingBag sleepingBag = gameObject.AddComponent<SleepingBag>();

            sleepingBag.deployerUserID = player.userID;
            sleepingBag.net = Network.Net.sv.CreateNetworkable();
            sleepingBag.niceName = teleportLocation.name;
            sleepingBag.secondsBetweenReuses = teleportLocation.timer;
            sleepingBag.transform.position = position;
            sleepingBag.RespawnType = ProtoBuf.RespawnInformation.SpawnOptions.RespawnType.Bed;
            sleepingBag.unlockTime = 0;

            SleepingBag.sleepingBags.Add(sleepingBag);

            return sleepingBag;
        }

        #endregion

        #region Config Classes

        private class CompoundTeleportConfiguration
        {
            [JsonProperty("removeHostility")]
            public bool removeHostility { get; set; }

            [JsonProperty("teleportLocations")]
            public List<TeleportLocation> teleportLocations { get; set; }

            public static CompoundTeleportConfiguration BuildDefaultConfiguration()
            {
                return new CompoundTeleportConfiguration
                {
                    removeHostility = true,
                    teleportLocations = TeleportLocation.BuildDefaultTeleportLocations()
                };
            }
        }

        private class TeleportLocation
        {
            [JsonProperty("name")]
            public string name { get; set; }
            [JsonProperty("timer")]
            public int timer { get; set; }
            [JsonProperty("monumentPrefab")]
            public string monumentPrefab { get; set; }
            [JsonProperty("enabled")]
            public bool enabled { get; set; }

            public static List<TeleportLocation> BuildDefaultTeleportLocations()
            {

                return new List<TeleportLocation>
                {
                    new TeleportLocation
                    {
                        name = "Outpost",
                        timer = Timer,
                        monumentPrefab = OutPostPrefab,
                        enabled = true
                    },
                    new TeleportLocation
                    {
                        name = "Bandit Camp",
                        timer = Timer,
                        monumentPrefab = BanditTownPrefab,
                        enabled = true
                    }
                };
            }
        }

        #endregion

        #region API

        private bool umodversion() => true;
        // distinguish between free and paid versions
        // https://umod.org/community/nteleportation/41710-conflit-with-plugin-compound-teleport
        // https://umod.org/community/compound-teleport/40072-conflicts-with-nteleport

        #endregion
    }
}