using System;
using Oxide.Core;
using UnityEngine;
using System.Collections.Generic;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("VisualCupboard", "Colon Blow", "1.0.12")]
    class VisualCupboard : RustPlugin
    {

        #region Loadup

        private void OnServerInitialized() { serverInitialized = true; }

        private void Loaded()
        {
            LoadVariables();
            serverInitialized = true;
            lang.RegisterMessages(messages, this);
            permission.RegisterPermission("visualcupboard.allowed", this);
            permission.RegisterPermission("visualcupboard.admin", this);
        }

        private void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            Config.Clear();
            LoadVariables();
        }

        private Dictionary<string, string> messages = new Dictionary<string, string>()
            {
            {"notallowed", "You are not allowed to access that command." }
            };

        #endregion

        #region Configuration

        private bool Changed;
        private static float UseCupboardRadius = 25f;
        private float DurationToShowRadius = 60f;
        private float ShowCupboardsWithinRangeOf = 50f;
        private int VisualDarkness = 5;

        private static bool serverInitialized = false;

        private void LoadConfigVariables()
        {
            CheckCfgFloat("My Cupboard Radius is (25 is default)", ref UseCupboardRadius);
            CheckCfgFloat("Show Visuals On Cupboards Withing Range Of", ref ShowCupboardsWithinRangeOf);
            CheckCfgFloat("Show Visuals For This Long", ref DurationToShowRadius);
            CheckCfg("How Dark to make Visual Cupboard", ref VisualDarkness);
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
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

        #endregion

        #region Sphere Entity

        private class ToolCupboardSphere : MonoBehaviour
        {
            private BaseEntity sphere;
            private BaseEntity entity;
            public bool showall;
            private Vector3 pos = new Vector3(0, 0, 0);
            private Quaternion rot = new Quaternion();
            private string strPrefab = "assets/prefabs/visualization/sphere.prefab";

            private void Awake()
            {
                SpawnSphere();
            }

            private void SpawnSphere()
            {
                entity = GetComponent<BaseEntity>();
                sphere = GameManager.server.CreateEntity(strPrefab, pos, rot, true);
                SphereEntity ball = sphere.GetComponent<SphereEntity>();
                ball.OwnerID = entity.OwnerID;
                ball.currentRadius = 1f;
                ball.lerpRadius = 2.0f * UseCupboardRadius;
                ball.lerpSpeed = 100f;
                showall = false;
                sphere.SetParent(entity);
                sphere.Spawn();
            }

            private void OnDestroy()
            {
                if (sphere == null) return;
                sphere.Kill(BaseNetworkable.DestroyMode.None);
            }

        }

        #endregion

        #region Hooks

        private object CanNetworkTo(SphereEntity sphereEntity, BasePlayer target)
        {
            var sphereobj = sphereEntity.GetComponentInParent<ToolCupboardSphere>();
            if (sphereobj == null) return null;
            if (sphereobj != null && sphereobj.showall == false)

            {
                if (target.userID != sphereEntity.OwnerID) return false;
            }
            return null;
        }

        #endregion

        #region Commands

        [ChatCommand("showsphere")]
        private void cmdChatShowSphere(BasePlayer player, string command)
        {
            AddSphere(player, false, false);
        }

        [ChatCommand("showsphereall")]
        private void cmdChatShowSphereAll(BasePlayer player, string command)
        {
            AddSphere(player, true, false);
        }

        [ChatCommand("showsphereadmin")]
        private void cmdChatShowSphereAdmin(BasePlayer player, string command)
        {
            if (isAllowed(player, "visualcupboard.admin"))
            {
                AddSphere(player, true, true);
                return;
            }
            else if (!isAllowed(player, "visualcupboard.admin"))
            {
                SendReply(player, lang.GetMessage("notallowed", this));
                return;
            }
        }

        [ChatCommand("killsphere")]
        private void cmdChatDestroySphere(BasePlayer player, string command)
        {
            if (isAllowed(player, "visualcupboard.admin"))
            {
                DestroyAll<ToolCupboardSphere>();
                return;
            }
            else if (!isAllowed(player, "visualcupboard.admin"))
            {
                SendReply(player, lang.GetMessage("notallowed", this));
                return;
            }
        }

        #endregion

        #region Helpers

        private void AddSphere(BasePlayer player, bool showall, bool adminshow)
        {
            if (isAllowed(player, "visualcupboard.allowed") || isAllowed(player, "visualcupboard.admin"))
            {
                List<BaseCombatEntity> cblist = new List<BaseCombatEntity>();
                Vis.Entities<BaseCombatEntity>(player.transform.position, ShowCupboardsWithinRangeOf, cblist);

                foreach (BaseCombatEntity bp in cblist)
                {
                    if (bp is BuildingPrivlidge)
                    {
                        if (bp.GetComponent<ToolCupboardSphere>() == null)
                        {
                            Vector3 pos = bp.transform.position;

                            if (!adminshow)
                            {
                                if (player.userID == bp.OwnerID)
                                {
                                    for (int i = 0; i < VisualDarkness; i++)
                                    {
                                        var sphereobj = bp.gameObject.AddComponent<ToolCupboardSphere>();
                                        if (showall) sphereobj.showall = true;
                                        GameManager.Destroy(sphereobj, DurationToShowRadius);
                                    }
                                }

                            }
                            if (adminshow)
                            {
                                for (int i = 0; i < VisualDarkness; i++)
                                {
                                    var sphereobj = bp.gameObject.AddComponent<ToolCupboardSphere>();
                                    sphereobj.showall = true;
                                    GameManager.Destroy(sphereobj, DurationToShowRadius);
                                }
                                player.SendConsoleCommand("ddraw.text", 10f, Color.red, pos + Vector3.up, FindPlayerName(bp.OwnerID));
                                PrintWarning("Tool Cupboard Owner " + bp.OwnerID + " : " + FindPlayerName(bp.OwnerID));
                            }
                        }
                    }
                }
                return;
            }
            SendReply(player, lang.GetMessage("notallowed", this));
            return;
        }

        private string FindPlayerName(ulong userId)
        {
            BasePlayer player = BasePlayer.FindByID(userId);
            if (player)
                return player.displayName;

            player = BasePlayer.FindSleeping(userId);
            if (player)
                return player.displayName;

            var iplayer = covalence.Players.FindPlayer(userId.ToString());
            if (iplayer != null)
                return iplayer.Name;

            return "Unknown Entity Owner";
        }

        private void Unload()
        {
            DestroyAll<ToolCupboardSphere>();
        }

        static void DestroyAll<T>()
        {
            var objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }

        private bool isAllowed(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        #endregion
    }
}