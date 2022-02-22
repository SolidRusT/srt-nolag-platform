using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("WaterBikes", "senyaa", "1.1.7")]
    [Description("Turns snowmobiles into waterbikes")]
    class WaterBikes : RustPlugin
    {
        #region Fields
        private static Dictionary<BaseNetworkable, WaterBikeComponent> waterbikes;
        private ItemDefinition priceDef;
        private Vector3 lastKilledSnowmobilePos; // workaround to get water bikes work properly with spray can reskinning
        #endregion

        #region Configuration
        private static WaterbikeConfig config;

        private class WaterbikeConfig
        {
            [JsonProperty("(0) Waterbike price item short name:amount (0 - free)")]
            public KeyValuePair<string, int> Price;
            [JsonProperty("(1) Waterbike prefab")]
            public string WaterbikePrefab;
            [JsonProperty("(2) Make all snowmobiles waterbikes")]
            public bool Make_All_Snowmobiles_Waterbikes;
            [JsonProperty("(3) Allow waterbikes to drive on land")]
            public bool Allow_To_Move_On_Land;
            [JsonProperty("(4) Spawn permission name")]
            public string Spawn_Permission;
            [JsonProperty("(5) This permission allows players to spawn waterbikes for free")]
            public string NoPay_Permission;
            [JsonProperty("(6) Engine thrust")]
            public float engineThrust;
            [JsonProperty("(7) Steering scale")]
            public float steeringScale;
            [JsonProperty("(8) Off axis drag")]
            public float offAxisDrag;
            [JsonProperty("(9) Thrust point position")]
            public Vector3 ThrustPoint;
            [JsonProperty("Buoyancy points")]
            public SerializedBuoyancyPoint[] BuoyancyPoints;
        }

        private WaterbikeConfig GetDefaultConfig()
        {
            return new WaterbikeConfig
            {
                Price = new KeyValuePair<string, int>("scrap", 0),
                WaterbikePrefab = "assets/content/vehicles/snowmobiles/tomahasnowmobile.prefab",
                Make_All_Snowmobiles_Waterbikes = true,
                Allow_To_Move_On_Land = true,
                Spawn_Permission = "waterbikes.spawn",
                NoPay_Permission = "waterbikes.free",
                engineThrust = 5000f,
                steeringScale = 0.1f,
                offAxisDrag = 0.35f,
                ThrustPoint = new Vector3(-0.001150894f, 0.055f, -1.125f),
                BuoyancyPoints = new SerializedBuoyancyPoint[]
                {
                    new SerializedBuoyancyPoint(new Vector3(-0.62f, 0.09f, -1.284f), 730f, 1.3f),
                    new SerializedBuoyancyPoint(new Vector3(0.5f, 0.09f, -1.284f), 730f, 1.3f),
                    new SerializedBuoyancyPoint(new Vector3(-0.68f, 0.09f, -0.028f), 730f, 1.3f),
                    new SerializedBuoyancyPoint(new Vector3(0.54f, 0.09f, -0.028f), 730f, 1.3f),
                    new SerializedBuoyancyPoint(new Vector3(-0.64f, 0.09f, 1.283f), 730f, 1.3f),
                    new SerializedBuoyancyPoint(new Vector3(0.53f, 0.09f, 1.283f), 730f, 1.3f),
                    new SerializedBuoyancyPoint(new Vector3(-0.05f, 0.148f, 3.015f), 730f, 1.3f),
                    new SerializedBuoyancyPoint(new Vector3(-0.05f, 0.129f, 1.81f), 730f, 1.3f),
                    new SerializedBuoyancyPoint(new Vector3(-0.05f, 0.529f, -0.828f), 730f, 1.3f)
                }
            };
        }

        protected override void LoadDefaultConfig() => Config.WriteObject(GetDefaultConfig(), true);
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to run this command",
                ["Spawned"] = "Spawned waterbike",
                ["NoWaterbike"] = "You aren't on a waterbike",
                ["Showing"] = "Showing buoyancy points...",
                ["NotEnough"] = "You don't have enough to buy a waterbike",
                ["Converted"] = "Snowmobile converted into waterbike"
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "У вас нет прав для выполнения этой команды",
                ["Spawned"] = "Заспаунен гидроцикл",
                ["NoWaterbike"] = "Не на гидроцикле",
                ["Showing"] = "Показываются точки плавучести...",
                ["NotEnough"] = "У вас не хватает ресурсов для покупки гидроцикла",
                ["Converted"] = "Снегоход переделан в гидроцикл"
            }, this, "ru");
        }
        #endregion

        #region Types
        private struct SerializedBuoyancyPoint
        {
            public Vector3 Position;
            public float Force;
            public float Size;
            public SerializedBuoyancyPoint(Vector3 Position, float Force, float Size)
            {
                this.Position = Position;
                this.Force = Force; 
                this.Size = Size;
            }
        }

        private class WaterBikeComponent : FacepunchBehaviour
        {
            private Snowmobile _snowmobile;
            private Buoyancy _buoyancy;
            private Rigidbody _rigidbody;
            public GameObject thrustPoint;

            private GameObject parentPoint;

            private float gasPedal;
            private float steering;

            private Vector3 _waterLoggedPointLocalPosition;

            void Awake()
            {
                _snowmobile = GetComponent<Snowmobile>();
                _rigidbody = _snowmobile.gameObject.GetComponent<Rigidbody>();

                if (!config.Allow_To_Move_On_Land)
                    _snowmobile.engineKW = -1;

                if (_snowmobile.waterloggedPoint != null && _snowmobile.waterloggedPoint.parent != null)
                {
                    _waterLoggedPointLocalPosition = _snowmobile.waterloggedPoint.localPosition;
                    _snowmobile.waterloggedPoint.SetParent(null);
                    _snowmobile.waterloggedPoint.position = new Vector3(0, 2000, 0);
                }

                thrustPoint = new GameObject("ThrustPoint");
                thrustPoint.transform.SetParent(_snowmobile.transform, false);
                thrustPoint.transform.localPosition = config.ThrustPoint;
                InitBuoyancy();
                _snowmobile.SetFlag(BaseEntity.Flags.Reserved10, true, true, true);
                waterbikes.Add(_snowmobile, this);
                _snowmobile.SendNetworkUpdateImmediate();
            }

            void OnDestroy()
            {
                Destroy(_buoyancy);
                Destroy(parentPoint);
                Destroy(thrustPoint);
                if (_snowmobile.IsDestroyed) return;
                _snowmobile.waterloggedPoint.SetParent(_snowmobile.transform);
                _snowmobile.waterloggedPoint.transform.localPosition = _waterLoggedPointLocalPosition;
                _snowmobile.engineKW = 49;
                _snowmobile.SendNetworkUpdateImmediate();
                waterbikes.Remove(_snowmobile);
            }

            private void InitBuoyancy()
            {
                _buoyancy = _snowmobile.gameObject.AddComponent<Buoyancy>();

                _buoyancy.forEntity = _snowmobile;
                _buoyancy.rigidBody = _rigidbody;

                _buoyancy.doEffects = true;

                _buoyancy.requiredSubmergedFraction = 0f;

                _buoyancy.useUnderwaterDrag = true;
                _buoyancy.underwaterDrag = 20f;

                _buoyancy.waveHeightScale = 500f;

                var points = new BuoyancyPoint[config.BuoyancyPoints.Length];

                parentPoint = new GameObject("buoyancy");
                
                for (int i = 0; i < points.Length; i++)
                {
                    var go = new GameObject($"buoyancyPoint_{i}");

                    go.transform.SetParent(parentPoint.transform, false);
                    go.transform.localPosition = config.BuoyancyPoints[i].Position;

                    var point = go.AddComponent<BuoyancyPoint>();

                    point.buoyancyForce = config.BuoyancyPoints[i].Force;
                    point.size = config.BuoyancyPoints[i].Size;
                    points[i] = point;
                }

                parentPoint.transform.SetParent(_snowmobile.transform, false);
                parentPoint.transform.localPosition = new Vector3(0.046f, -0.15f, -0.853f);

                _buoyancy.points = points;
            }
            void FixedUpdate()
            {
                if (IsFlipped() && _snowmobile.engineController.IsOn)
                {
                    _snowmobile.engineController.StopEngine();
                    Flip();
                    return;
                }
                _snowmobile.SetFlag(BaseEntity.Flags.Reserved7, _rigidbody.IsSleeping() && !_snowmobile.AnyMounted(), false, true);
                if (!_snowmobile.engineController.IsOn)
                {
                    gasPedal = 0f;
                    steering = 0f;
                }
                _snowmobile.SetFlag(BaseEntity.Flags.Reserved7, _rigidbody.IsSleeping() && !_snowmobile.AnyMounted(), false, true);

                if (gasPedal != 0f && WaterLevel.Test(thrustPoint.transform.position, true, _snowmobile) && _buoyancy.submergedFraction > 0.3f)
                {
                    var force = (transform.forward + transform.right * steering * config.steeringScale).normalized * gasPedal * config.engineThrust;
                    _rigidbody.AddForceAtPosition(force, thrustPoint.transform.position, ForceMode.Force);
                    _snowmobile.engineKW = 65;
                }
                else
                    _snowmobile.engineKW = config.Allow_To_Move_On_Land ? 49 : 1;

                _rigidbody.angularDrag = 0.5f + 0.005f * Mathf.InverseLerp(0f, 2f, _rigidbody.velocity.SqrMagnitude2D());
                _rigidbody.drag = 0.2f + 0.6f * Mathf.InverseLerp(0f, 1f, _buoyancy.submergedFraction);
                parentPoint.transform.rotation = _snowmobile.transform.rotation;
                if (config.offAxisDrag > 0f)
                {
                    var value2 = Vector3.Dot(transform.forward, _rigidbody.velocity.normalized);
                    var num2 = Mathf.InverseLerp(0.98f, 0.92f, value2);
                    _rigidbody.drag += num2 * config.offAxisDrag * _buoyancy.submergedFraction;
                }

                var x = Mathf.InverseLerp(1f, 10f, _rigidbody.velocity.Magnitude2D()) * 0.5f * _snowmobile.healthFraction;

                if (!_snowmobile.engineController.IsOn)
                    x = 0f;

                var y = 1f - 0.3f * (1f - _snowmobile.healthFraction);

                _buoyancy.buoyancyScale = (1f + x) * y;
            }
            public void OnPlayerInput(InputState inputState)
            {
                if (!_snowmobile.AnyMounted()) return;

                if (inputState.IsDown(BUTTON.FORWARD))
                    gasPedal = 1f;
                else if (inputState.IsDown(BUTTON.BACKWARD))
                    gasPedal = -0.5f;
                else
                    gasPedal = 0f;

                if (inputState.IsDown(BUTTON.LEFT))
                {
                    steering = 1f;
                    return;
                }

                if (inputState.IsDown(BUTTON.RIGHT))
                {
                    steering = -1f;
                    return;
                }
                steering = 0f;
            }

            public bool IsFlipped()
            {
                return Vector3.Dot(Vector3.up, transform.up) <= 0f;
            }

            public void Flip()
            {
                _rigidbody.AddRelativeTorque(Vector3.forward, ForceMode.VelocityChange);
                _rigidbody.AddForce(Vector3.up * 4f, ForceMode.VelocityChange);
            }
        }
        #endregion

        #region Hooks
        void Init()
        {
            waterbikes = new Dictionary<BaseNetworkable, WaterBikeComponent>();
            config = Config.ReadObject<WaterbikeConfig>();
            permission.RegisterPermission(config.Spawn_Permission, this);
            permission.RegisterPermission(config.NoPay_Permission, this);

            try
            {
                priceDef = ItemManager.FindItemDefinition(config.Price.Key);
                if (priceDef == null)
                    throw new NullReferenceException();
            }
            catch (Exception)
            {
                PrintError("Item name is invalid! Players won't be charged for water bikes");
            }
        }

        void Unload()
        {
            foreach (var obj in UnityEngine.Object.FindObjectsOfType<WaterBikeComponent>())
                UnityEngine.Object.DestroyImmediate(obj);

            waterbikes = null;
            config = null;
        }

        void OnServerInitialized(bool initial)
        {
            var count = 0;

            foreach (var ent in BaseNetworkable.serverEntities)
            {
                if (!(ent is Snowmobile)) continue;

                if ((config.Make_All_Snowmobiles_Waterbikes || (ent as BaseEntity).HasFlag(BaseEntity.Flags.Reserved10)) && !waterbikes.ContainsKey(ent))
                {
                    ent.gameObject.AddComponent<WaterBikeComponent>();
                    count++;
                }
            }

            Puts($"Loaded {count} waterbikes");
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null) return;
            var mounted = player.GetMountedVehicle();
            if (mounted != null && waterbikes.ContainsKey(mounted))
            {
                waterbikes[mounted].OnPlayerInput(input);
            }
        }

        void OnEngineStarted(BaseVehicle vehicle, BasePlayer driver)
        {
            if (config.Allow_To_Move_On_Land) return;
            if (driver == null || vehicle == null) return;
            if (!(vehicle is Snowmobile)) return;
            if (!waterbikes.ContainsKey(vehicle)) return;

            if (!WaterLevel.Test(waterbikes[vehicle].thrustPoint.transform.position, true, vehicle))
            {
                timer.Once(0.1f, () =>
                {
                    if(vehicle != null)
                        (vehicle as Snowmobile).engineController.StopEngine();
                });
            }
        }
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!(entity is Snowmobile)) return;

            if (lastKilledSnowmobilePos == entity.transform.position)
            {
                entity.gameObject.AddComponent<WaterBikeComponent>();
                lastKilledSnowmobilePos = new Vector3();
                return;
            }

            if (!config.Make_All_Snowmobiles_Waterbikes) return;
            if (waterbikes.ContainsKey(entity)) return;
            entity.gameObject.AddComponent<WaterBikeComponent>();
        }


        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null) return;
            if (!waterbikes.ContainsKey(entity)) return;
            lastKilledSnowmobilePos = entity.transform.position;
        }

        #endregion

        #region API
        [HookMethod("SpawnWaterbike")]
        public BaseEntity SpawnWaterbike(Vector3 position, Quaternion rotation)
        {
            var waterBike = GameManager.server.CreateEntity(config.WaterbikePrefab, position, rotation);
            waterBike.gameObject.AddComponent<WaterBikeComponent>();
            waterBike.Spawn();
            return waterBike;
        }
        #endregion

        #region Methods
        public Vector3 GetSpawnPosition(BasePlayer player)
        {
            RaycastHit hit;
            Vector3 position;
            if (Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out hit, 15f))
                position = hit.point + new Vector3(0, 0.3f, 0);
            else
                position = player.transform.position;

            return position;
        }
        #endregion

        #region Commands
        [ChatCommand("waterbike_debug")]
        private void WaterbikeDebugCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                PrintToChat(player, lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }
            var vehicle = player.GetMountedVehicle();

            if(vehicle == null || !waterbikes.ContainsKey(vehicle))
            {
                PrintToChat(player, lang.GetMessage("NoWaterbike", this, player.UserIDString));
                return;
            }
         
            var buoy = vehicle.gameObject.GetComponent<Buoyancy>();
            var waterbike = waterbikes[vehicle];

            PrintToChat(player, lang.GetMessage("Showing", this, player.UserIDString));

            foreach (var point in buoy.points)
            {
                player.SendConsoleCommand("ddraw.text", 30f, Color.green, point.transform.position, $"<size=13>Force - {point.buoyancyForce}\nSize = {point.size}</size>");
                player.SendConsoleCommand("ddraw.box", 30f, Color.green, point.transform.position, point.size);
            }

            player.SendConsoleCommand("ddraw.box", 30f, Color.blue, waterbike.thrustPoint.transform.position, 0.1f);
            player.SendConsoleCommand("ddraw.text", 30f, Color.blue, waterbike.thrustPoint.transform.position, "<size=13>Thrust point</size>");
        }

        [ChatCommand("waterbike")]
        private void WaterbikeCommand(BasePlayer player, string command, string[] args)
        {
            RaycastHit hit;
            BaseEntity ent = null;

            if (Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out hit, 15f))
            {
                var hit_ent = hit.GetEntity();
                if(hit_ent != null && hit_ent is Snowmobile && !waterbikes.ContainsKey(hit_ent))
                {
                    ent = hit_ent;
                } 
            }

            if(!permission.UserHasPermission(player.UserIDString, config.Spawn_Permission))
            {
                PrintToChat(player, lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }

            if(config.Price.Value > 0 && priceDef != null && !permission.UserHasPermission(player.UserIDString, config.NoPay_Permission))
            {
                if(player.inventory.GetAmount(priceDef.itemid) >= config.Price.Value)
                {
                    player.inventory.Take(null, priceDef.itemid, config.Price.Value);

                    if (ent != null)
                    {
                        ent.gameObject.AddComponent<WaterBikeComponent>();
                        PrintToChat(player, lang.GetMessage("Converted", this, player.UserIDString));
                    }
                    else
                    {
                        SpawnWaterbike(GetSpawnPosition(player), player.eyes.rotation);
                        PrintToChat(player, lang.GetMessage("Spawned", this, player.UserIDString));
                    }
                }
                else
                {
                    PrintToChat(player, lang.GetMessage("NotEnough", this, player.UserIDString));
                }
                return;
            }

            if (ent != null)
            {
                ent.gameObject.AddComponent<WaterBikeComponent>();
                PrintToChat(player, lang.GetMessage("Converted", this, player.UserIDString));
            }
            else
            {
                SpawnWaterbike(GetSpawnPosition(player), player.eyes.rotation);
                PrintToChat(player, lang.GetMessage("Spawned", this, player.UserIDString));
            }
        }
        #endregion
    }
}
