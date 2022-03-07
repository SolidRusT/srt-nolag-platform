using UnityEngine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Rust;

namespace Oxide.Plugins
{
    [Info("Monuments Recycler", "Dana", "0.2.6")]
    [Description("Adds recyclers to monuments including the cargo ship.")]
    internal class MonumentsRecycler : RustPlugin
    {
        private bool _serverInitialized = false;
        private const string RecyclerPrefab = "assets/bundled/prefabs/static/recycler_static.prefab";
        private const string SmallOilRigKey = "oil_rig_small";
        private const string LargeOilRigKey = "large_oil_rig";
        private const string DomeKey = "dome_monument_name";
        const string FishingVillageLargePrefab = "assets/bundled/prefabs/autospawn/monument/fishing_village/fishing_village_a.prefab";
        const string FishingVillageSmallBPrefab = "assets/bundled/prefabs/autospawn/monument/fishing_village/fishing_village_b.prefab";
        const string FishingVillageSmallAPrefab = "assets/bundled/prefabs/autospawn/monument/fishing_village/fishing_village_c.prefab";

        private List<BaseEntity> _recyclers = new List<BaseEntity>();
		private SpawnData _domeSpawnData = new SpawnData(new Vector3(19.9f, 37.8f, 16.57f), new Vector3(0, 235, 0));
        private readonly Dictionary<string, SpawnData> _smallOilRigRecyclerPositions = new Dictionary<string, SpawnData>
        {
            {"3",  new SpawnData(new Vector3(21.01f, 22.5f, -30.8f),new Vector3(0, 180, 0) )},
            {"4",new SpawnData(new Vector3(32.1f, 27f, -34.5f),new Vector3(0, 270, 0)) }
        };
        private readonly Dictionary<string, SpawnData> _largeOilRigRecyclerPositions = new Dictionary<string, SpawnData>
        {
            {"4",  new SpawnData(new Vector3(20.57f, 27.1f, -44.52f),new Vector3(0, 0, 0) )},
            {"6",new SpawnData(new Vector3(-13.6f, 36.1f, -3.4f),new Vector3(0, 180, 0)) }
        };
        private readonly Dictionary<string, SpawnData> _fishingVillageRecyclerPositions = new Dictionary<string, SpawnData>
        {
            {"smallB", new SpawnData(new Vector3(-21f,0.3f,6.5f),new Vector3(0f,130f,0f)) },
            {"smallA", new SpawnData(new Vector3(-8.5f,2f,16.06067f),new Vector3(0,90f,0)) },
            {"large", new SpawnData(new Vector3(-20.7f,0.2f,-9.6f), new Vector3(0,45f,0)) }
        };
        private Configuration _config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Cargo Ship - Recycler Position - Front")]
            public bool RecyclerPositionFront = true;

            [JsonProperty(PropertyName = "Cargo Ship - Recycler Position - Back")]
            public bool RecyclerPositionBack = true;

            [JsonProperty(PropertyName = "Cargo Ship - Recycler Position - Bottom")]
            public bool RecyclerPositionBottom = true;

            [JsonProperty(PropertyName = "Large Oil Rig - Recycler Position - Level 4")]
            public bool LargeOilRigRecyclerPositionLevel4 = true;

            [JsonProperty(PropertyName = "Large Oil Rig - Recycler Position - Level 6")]
            public bool LargeOilRigRecyclerPositionLevel6 = true;

            [JsonProperty(PropertyName = "Small Oil Rig - Recycler Position - Level 3")]
            public bool SmallOilRigRecyclerPositionLevel3 = true;

            [JsonProperty(PropertyName = "Small Oil Rig - Recycler Position - Level 4")]
            public bool SmallOilRigRecyclerPositionLevel4 = true;

            [JsonProperty(PropertyName = "Fishing Village - Recycler - Large")]
            public bool FishingVillageRecyclerLarge { get; set; } = true;

            [JsonProperty(PropertyName = "Fishing Village - Recycler - Small A")]
            public bool FishingVillageRecyclerSmallA { get; set; } = true;

            [JsonProperty(PropertyName = "Fishing Village - Recycler - Small B")]
            public bool FishingVillageRecyclerSmallB { get; set; } = true;

            [JsonProperty(PropertyName = "Dome - Recycler")]
            public bool DomeRecycler = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                //if (_config != null && _serverInitialized)
                //{
                //    ShowRecyclers();
                //}
            }
            catch
            {
                PrintError("Loading default config! Error loading your config, it's corrupt!");
                LoadDefaultConfig();
            }

            if (_config == null)
            {
                LoadDefaultConfig();
            }

            SaveConfig();
            var pluginEnabled = _config.RecyclerPositionFront || _config.RecyclerPositionBack || _config.RecyclerPositionBottom ||
                                _config.LargeOilRigRecyclerPositionLevel4 || _config.LargeOilRigRecyclerPositionLevel6 ||
                                _config.SmallOilRigRecyclerPositionLevel3 || _config.SmallOilRigRecyclerPositionLevel4 ||
                                _config.DomeRecycler || _config.FishingVillageRecyclerLarge || _config.FishingVillageRecyclerSmallA
                                || _config.FishingVillageRecyclerSmallB;
            if (!pluginEnabled)
            {
                PrintWarning("No Recycler Position Found");
                Unsubscribe(nameof(OnEntitySpawned));
            }
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        Tuple<Vector3, Quaternion> GetCargoFrontPosition(Vector3 position, Quaternion rotation) => Tuple.Create(new Vector3(position.x - .2172264f, position.y + 9.574753f, position.z + 79.05922f), new Quaternion(rotation.x, rotation.y + 180, rotation.z, rotation.w));

        Tuple<Vector3, Quaternion> GetCargoBackPosition(Vector3 position, Quaternion rotation) => Tuple.Create(new Vector3(position.x + .2407f, position.y + 9.5f, position.z - 36.87305f), rotation);

        Tuple<Vector3, Quaternion> GetCargoBottomPosition(Vector3 position, Quaternion rotation) => Tuple.Create(new Vector3(position.x, position.y + .57767f, position.z + 10f), new Quaternion(rotation.x, rotation.y + 180, rotation.z, rotation.w));

        private void OnEntitySpawned(CargoShip ship)
        {
            if (ship == null) return;
            Vector3 pos = ship.transform.position;
            Quaternion rot = new Quaternion();
            Timer thisTimer = null;

            thisTimer = timer.Every(0.01f, () =>
            {
                if (ship == null || ship.IsDestroyed)
                {
                    thisTimer.Destroy();
                    return;
                }

                ship.transform.position = pos;
                ship.transform.rotation = rot;
            });

            timer.Once(3, () =>
            {
                if (ship == null || ship.IsDestroyed)
                {
                    return;
                }

                if (_config.RecyclerPositionFront)
                {
                    Vector3 position = ship.transform.position;
                    Quaternion rotation = ship.transform.rotation;
                    SpawnRecycler(ship, GetCargoFrontPosition(position, rotation));
                }

                if (_config.RecyclerPositionBack)
                {
                    Vector3 position = ship.transform.position;
                    Quaternion rotation = ship.transform.rotation;
                    SpawnRecycler(ship, GetCargoBackPosition(position, rotation));
                }

                if (_config.RecyclerPositionBottom)
                {
                    Vector3 position = ship.transform.position;
                    Quaternion rotation = ship.transform.rotation;
                    SpawnRecycler(ship, GetCargoBottomPosition(position, rotation));
                }

                thisTimer.Destroy();
            });
        }

        private void OnServerInitialized()
        {
            _serverInitialized = true;
            ShowRecyclers();
        }
        private void ShowRecyclers()
        {
            var oilRigEnabled = _config.LargeOilRigRecyclerPositionLevel4 || _config.LargeOilRigRecyclerPositionLevel6 ||
                               _config.SmallOilRigRecyclerPositionLevel3 || _config.SmallOilRigRecyclerPositionLevel4;
            var fishingVillageEnabled = _config.FishingVillageRecyclerLarge || _config.FishingVillageRecyclerSmallA || _config.FishingVillageRecyclerSmallB;
            if (oilRigEnabled)
            {

                SpawnData spawnData;
                var monuments = TerrainMeta.Path.Monuments?.Where(x => x.shouldDisplayOnMap && x.displayPhrase.english.Contains("Oil Rig")).ToList() ?? new List<MonumentInfo>();
                foreach (var monument in monuments)
                {
                    if (monument.displayPhrase?.token == SmallOilRigKey)
                    {
                        if (_config.SmallOilRigRecyclerPositionLevel3)
                        {
                            if (_smallOilRigRecyclerPositions.TryGetValue("3", out spawnData))
                            {
                                SpawnRecycler(monument.transform, spawnData.Position, spawnData.Rotation);
                            }
                        }
                        if (_config.SmallOilRigRecyclerPositionLevel4)
                        {
                            if (_smallOilRigRecyclerPositions.TryGetValue("4", out spawnData))
                            {
                                SpawnRecycler(monument.transform, spawnData.Position, spawnData.Rotation);
                            }
                        }
                    }
                    else if (monument.displayPhrase?.token == LargeOilRigKey)
                    {
                        if (_config.LargeOilRigRecyclerPositionLevel4)
                        {
                            if (_largeOilRigRecyclerPositions.TryGetValue("4", out spawnData))
                            {
                                SpawnRecycler(monument.transform, spawnData.Position, spawnData.Rotation);
                            }
                        }
                        if (_config.LargeOilRigRecyclerPositionLevel6)
                        {
                            if (_largeOilRigRecyclerPositions.TryGetValue("6", out spawnData))
                            {
                                SpawnRecycler(monument.transform, spawnData.Position, spawnData.Rotation);
                            }
                        }
                    }
                }
            }

            if (_config.DomeRecycler)
            {
                var monuments = TerrainMeta.Path.Monuments?.Where(x => x.shouldDisplayOnMap && x.displayPhrase.english.Contains("Dome")).ToList() ?? new List<MonumentInfo>();
                foreach (var monument in monuments)
                {
                    if (monument.displayPhrase?.token == DomeKey)
                    {
                        SpawnRecycler(monument.transform, _domeSpawnData.Position, _domeSpawnData.Rotation);
                    }
                }
            }
            if (fishingVillageEnabled)
            {
                SpawnData spawnData;
                var monuments = TerrainMeta.Path.Monuments?.Where(x => x.shouldDisplayOnMap && x.displayPhrase.english.ToLower().Contains("fishing")).ToList() ?? new List<MonumentInfo>();
                foreach (var monument in monuments)
                {
                    if (monument.name == FishingVillageLargePrefab)
                    {
                        if (_config.FishingVillageRecyclerLarge)
                        {
                            if (_fishingVillageRecyclerPositions.TryGetValue("large", out spawnData))
                            {
                                SpawnFishingVillageRecycler(monument.transform, spawnData.Position, spawnData.Rotation);
                            }
                        }
                    }
                    else if (monument.name == FishingVillageSmallAPrefab)
                    {
                        if (_config.FishingVillageRecyclerSmallA)
                        {
                            if (_fishingVillageRecyclerPositions.TryGetValue("smallA", out spawnData))
                            {
                                SpawnFishingVillageRecycler(monument.transform, spawnData.Position, spawnData.Rotation);
                            }
                        }
                    }
                    else if (monument.name == FishingVillageSmallBPrefab)
                    {
                        if (_config.FishingVillageRecyclerSmallB)
                        {
                            if (_fishingVillageRecyclerPositions.TryGetValue("smallB", out spawnData))
                            {
                                SpawnFishingVillageRecycler(monument.transform, spawnData.Position, spawnData.Rotation);
                            }
                        }
                    }
                }
            }
        }

        private void SpawnFishingVillageRecycler(Transform objTransform, Vector3 spawnOffset, Vector3 rotationOffset)
        {

            var mtx = objTransform.localToWorldMatrix;
            var finalPos = mtx.MultiplyPoint3x4(spawnOffset);
            var rotation = mtx.rotation * Quaternion.Euler(rotationOffset);
            var entity = GameManager.server.CreateEntity(RecyclerPrefab, finalPos, rotation);
            if (entity != null)
            {
                entity.EnableSaving(false);
                entity.Spawn();
                _recyclers.Add(entity);
            }
        }
        private void SpawnRecycler(Transform objTransform, Vector3 spawnOffset, Vector3 rotationOffset)
        {

            var mtx = objTransform.localToWorldMatrix;
            var finalPos = mtx.MultiplyPoint3x4(spawnOffset);
            var oilRot = mtx.rotation * Quaternion.Euler(rotationOffset);
            if (!GamePhysics.CheckSphere(finalPos, .1f, Layers.Server.Deployed, QueryTriggerInteraction.Ignore))
            {
                var entity = GameManager.server.CreateEntity(RecyclerPrefab, finalPos, oilRot);
                if (entity != null)
                {
                    entity.EnableSaving(false);
                    entity.Spawn();
                    _recyclers.Add(entity);
                }
            }
        }
        private void SpawnRecycler(CargoShip ship, Tuple<Vector3, Quaternion> selectedTransform)
        {
            BaseEntity rec = GameManager.server.CreateEntity(RecyclerPrefab, selectedTransform.Item1, selectedTransform.Item2, true);
            if (rec != null)
            {
                rec.EnableSaving(false);
                rec.Spawn();
                rec.SetParent(ship, true, true);
                Rigidbody comp = rec.GetComponent<Rigidbody>();
                if (comp)
                {
                    comp.isKinematic = true;
                }

                _recyclers.Add(rec);
            }
            else
            {
                Puts("Unable to create RecyclerPrefab for Front position!");
            }
        }

        private void Unload()
        {

            foreach (var recycler in _recyclers)
            {
                if (recycler is Recycler)
                {
                    ((Recycler)recycler).DropItems();
                }
                recycler.Kill();
            }
        }

        private class SpawnData
        {
            public SpawnData(Vector3 position, Vector3 rotation)
            {
                Position = position;
                Rotation = rotation;
            }

            public Vector3 Position { get; set; }
            public Vector3 Rotation { get; set; }
        }
    }
}