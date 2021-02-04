using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    /*  Copyright 2020, GrumpyGordon
     * 
     *  This software is licensed & protected under the MIT Copyright License (1988)
     * 
     *  Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the
     *  Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software,
     *  and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
     * 
     *  The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
     * 
     *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
     *  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
     *  ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
     *  THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
     * 
     */

    [Info("Cargo Ship CCTV", "GrumpyGordon", "1.2.1")]
    [Description("Adds CCTV to the cargo ship including an onboard monitoring station.")]
    public class CargoShipCCTV : RustPlugin
    {
        #region Configuration / Oxide Hooks

        const string _camPrefab = "assets/prefabs/deployable/cctvcamera/cctv.static.prefab";
        const string _computerStation = "assets/prefabs/deployable/computerstation/computerstation.deployed.prefab";

        protected override void SaveConfig() { Config.WriteObject(_config); }
        private Configuration _config;

        private void Init()
        {
            _config = Config.ReadObject<Configuration>();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("A new config file is being generated using default values.");
            _config = GetDefaultConfiguration();
        }
        void OnEntitySpawned(CargoShip cargo)
        {
            Setup(cargo);
        }
        object CanPickupEntity(BasePlayer player, ComputerStation entity)
        {
            if (entity.OwnerID == 0)
            {
                return false;
            }
            return null;
        }
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AnnounceCargoShipCode"] = "A new Cargo Ship has entered the proximity of the island. The code is {0}",
                ["AnnounceCargoShip"] = "A new Cargo Ship has entered the proximity of the island.",
            }, this);
        }

        #region ConfigClass

        class Configuration
        {
            [JsonProperty(PropertyName = "Announce Cargo Ship Code")]
            public bool AnnounceCargoShipCode;

            [JsonProperty(PropertyName = "Make Camera Names Unique")]
            public bool MakeCameraNamesUnique;

            public List<CCTV_Config> Cameras;

            public List<Station_Config> Stations;
        }

        private Configuration GetDefaultConfiguration()
        {
            var config = new Configuration
            {
                Cameras = new List<CCTV_Config>(),
                Stations = new List<Station_Config>(),
                AnnounceCargoShipCode = true,
                MakeCameraNamesUnique = true
            };

            config.Cameras.Add(new CCTV_Config(n: "CARGODECKA", p: new Vector3(0, 22, -37.5f), pitch: 50));
            config.Cameras.Add(new CCTV_Config(n: "CARGODECKB", p: new Vector3(0, 15, -51), r: new Vector3(0, 180, 0), pitch: 50));
            config.Cameras.Add(new CCTV_Config(n: "CARGODECKC", p: new Vector3(0, 22f, 68.55f), r: new Vector3(0, 180, 0), pitch: 28));
            config.Cameras.Add(new CCTV_Config(n: "CARGOHOLDA", p: new Vector3(0, 5.5f, -33f), pitch: 40));
            config.Cameras.Add(new CCTV_Config(n: "CARGOHOLDB", p: new Vector3(0, 5.5f, 50.9f), r: new Vector3(0, 180, 0), pitch: 40));
            config.Cameras.Add(new CCTV_Config(n: "CARGOLADDERA", p: new Vector3(12.1f, 6.3f, 28.6f), r: new Vector3(0, 90, 0), pitch: 24, yaw: 90));
            config.Cameras.Add(new CCTV_Config(n: "CARGOLADDERB", p: new Vector3(-12.1f, 6.3f, 28.6f), r: new Vector3(0, -90, 0), pitch: 24, yaw: -90));

            config.Stations.Add(new Station_Config(new Vector3(-9.2f, 24.5f, -39.7f), new Vector3(0, 180, 0)));

            return config;
        }

        #region ConfigSubclasses
        class CCTV_Config
        {
            [JsonProperty(PropertyName = "Camera Name")]
            public string Name = "Camera";

            [JsonProperty(PropertyName = "Camera Position")]
            public Vector3 Position = Vector3.zero;

            [JsonProperty(PropertyName = "Camera Rotation")]
            public Vector3 Rotation = Vector3.zero;

            [JsonProperty(PropertyName = "Camera Pitch")]
            public float Pitch = 0;

            [JsonProperty(PropertyName = "Camera Yaw")]
            public float Yaw = 0;

            public CCTV_Config(string n, Vector3 p, Vector3? r = null, float pitch = 0, float yaw = 0)
            {
                if (r == null)
                {
                    r = Vector3.zero;
                }
                Name = n;
                Position = p;
                Rotation = (Vector3)r;
                Pitch = pitch;
                Yaw = yaw;
            }
            public CCTV_Config Get(string suffix)
            {
                Name += suffix;
                return this;
            }
        }
        class Station_Config
        {
            [JsonProperty(PropertyName = "Station Position")]
            public Vector3 Position = Vector3.zero;

            [JsonProperty(PropertyName = "Station Rotation")]
            public Vector3 Rotation = Vector3.zero;

            public Station_Config(Vector3 p, Vector3? r = null)
            {
                if (r == null)
                {
                    r = Vector3.zero;
                }

                Position = p;
                Rotation = (Vector3)r;
            }
        }
        #endregion

        #endregion

        #endregion

        #region Setup
        public void Setup(CargoShip cargo)
        {
            Clear(cargo);
            List<CCTV_RC> cams = new List<CCTV_RC>();
            int suffix = 0;
            if (_config.MakeCameraNamesUnique)
            {
                suffix = UnityEngine.Random.Range(1000, 9999);
            }

            foreach (var item in _config.Cameras)
            {
                var cam = MakeCamera(cargo, item, suffix);
                if (cam != null)
                {
                    cams.Add(cam);
                }
            }
            foreach (var item in _config.Stations)
            {
                MakeComputerStation(cargo, item, cams);
            }

            AnnounceCargoShip(suffix);
        }
        #region Helpers

        void AnnounceCargoShip(int code = 0)
        {
            if (_config.AnnounceCargoShipCode)
            {
                foreach (var ply in BasePlayer.activePlayerList)
                {
                    if (ply.IsConnected)
                    {
                        if(code != 0)
                        {
                            ply.IPlayer.Message(string.Format(lang.GetMessage("AnnounceCargoShipCode", this, ply.IPlayer.Id), code.ToString()));
                        }
                        else
                        {
                            ply.IPlayer.Message(lang.GetMessage("AnnounceCargoShip", this, ply.IPlayer.Id));
                        }
                    }
                }
            }
        }
        void Clear(BaseEntity entity)
        {
            foreach (CCTV_RC item in entity.transform.GetComponents<CCTV_RC>())
            {
                if (item != null)
                {
                    item.GetEntity().Kill();
                }
            }
            foreach (ComputerStation item in entity.transform.GetComponents<ComputerStation>())
            {
                if (item != null)
                {
                    item.GetEntity().Kill();
                }
            }
        }
        public void RemoveCollidersFromEntity(BaseEntity colliderEntity)
        {
            foreach (var meshCollider in colliderEntity.GetComponentsInChildren<MeshCollider>())
            {
                UnityEngine.Object.DestroyImmediate(meshCollider);
            }
            UnityEngine.Object.DestroyImmediate(colliderEntity.GetComponent<GroundWatch>());
        }

        void MakeComputerStation(BaseEntity obj, Station_Config config, List<CCTV_RC> cams)
        {
            ComputerStation cs = GameManager.server.CreateEntity(_computerStation, obj.transform.position) as ComputerStation;
            if (cs == null) { return; }

            cs.SetParent(obj);

            cs.transform.localPosition = config.Position;

            cs.transform.localRotation = Quaternion.Euler(config.Rotation.x, config.Rotation.y, config.Rotation.z);

            RemoveCollidersFromEntity(cs);

            foreach (var item in cams)
            {
                if (!cs.controlBookmarks.ContainsKey(item.rcIdentifier))
                {
                    cs.controlBookmarks.Add(item.rcIdentifier, item.net.ID);
                }
            }
        }
        CCTV_RC MakeCamera(BaseEntity obj, CCTV_Config config, int code = 0)
        {
            CCTV_RC cam = GameManager.server.CreateEntity(_camPrefab, obj.transform.position) as CCTV_RC;
            if (cam == null) { return null; }

            cam.SetParent(obj);

            cam.transform.localPosition = config.Position;

            cam.transform.localRotation = Quaternion.Euler(config.Rotation.x, config.Rotation.y, config.Rotation.z);

            cam.isStatic = false;
            cam.yawAmount = config.Yaw;
            cam.pitchAmount = config.Pitch;
            if(code != 0)
            {
                cam.UpdateIdentifier(config.Name + code.ToString());
            }
            else
            {
                cam.UpdateIdentifier(config.Name);
            }
            RemoveCollidersFromEntity(cam);
            cam.OwnerID = obj.OwnerID;
            cam.Spawn();
            cam.isStatic = true;
            cam.UpdateHasPower(24, 1);
            cam.SendNetworkUpdateImmediate(true);
            return cam;
        }
        #endregion

        #endregion
    }
}