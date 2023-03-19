using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using VLB;
using Color = UnityEngine.Color;

namespace Oxide.Plugins
{
    [Info("Marker Manager", "Orange", "2.1.1")]
    [Description("Allows to place markers on in-game map")]
    public class MarkerManager : RustPlugin
    {
        #region Vars

        private const string genericPrefab = "assets/prefabs/tools/map/genericradiusmarker.prefab";
        private const string vendingPrefab = "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";
        private const string permUse = "markermanager.use";
        private const string chatCommand = "marker";

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(permUse, this);
            cmd.AddChatCommand(chatCommand, this, nameof(cmdMarkerChat));
        }

        private void OnServerInitialized()
        {
            LoadData();
            LoadCustomMarkers();
        }

        private void Unload()
        {
            SaveData();
            RemoveMarkers();
        }

        #endregion

        #region Commands

        private void cmdMarkerChat(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, permUse) == false)
            {
                Message(player, "Permission");
                return;
            }

            if (args == null || args?.Length == 0)
            {
                Message(player, "Usage");
                return;
            }

            switch (args[0].ToLower())
            {
                default:
                    Message(player, "Usage");
                    break;

                case "add":
                case "create":
                    if (args.Length < 8)
                    {
                        Message(player, "Usage");
                    }
                    else
                    {
                        var def = new CachedMarker
                        {
                            position = player.transform.position,
                            name = args[1],
                            duration = Convert.ToInt32(args[2]),
                            refreshRate = Convert.ToSingle(args[3]),
                            radius = Convert.ToSingle(args[4]),
                            displayName = args[5],
                            color1 = args[6],
                            color2 = args[7],
                        };

                        CreateCustomMarker(def, player);
                        SaveCustomMarker(def);
                    }

                    return;

                case "remove":
                case "delete":
                    if (args.Length < 2)
                    {
                        Message(player, "Usage");
                    }
                    else
                    {
                        RemoveCustomMarker(args[1], player);
                    }

                    return;
            }
        }

        #endregion

        #region Core

        private void CreateMarker(Vector3 position, int duration, float refreshRate, string name, string displayName,
            float radius = 0.3f, string colorMarker = "00FFFF", string colorOutline = "00FFFFFF")
        {
            var marker = new GameObject().AddComponent<CustomMapMarker>();
            marker.name = name;
            marker.displayName = displayName;
            marker.radius = radius;
            marker.position = position;
            marker.duration = duration;
            marker.refreshRate = refreshRate;
            ColorUtility.TryParseHtmlString($"#{colorMarker}", out marker.color1);
            ColorUtility.TryParseHtmlString($"#{colorOutline}", out marker.color2);
        }

        private void CreateMarker(BaseEntity entity, int duration, float refreshRate, string name, string displayName,
            float radius = 0.3f, string colorMarker = "00FFFF", string colorOutline = "00FFFFFF")
        {
            var marker = entity.gameObject.GetOrAddComponent<CustomMapMarker>();
            marker.name = name;
            marker.displayName = displayName;
            marker.radius = radius;
            marker.refreshRate = refreshRate;
            marker.parent = entity;
            marker.position = entity.transform.position;
            marker.duration = duration;
            ColorUtility.TryParseHtmlString($"#{colorMarker}", out marker.color1);
            ColorUtility.TryParseHtmlString($"#{colorOutline}", out marker.color2);
        }

        private void RemoveMarker(string name)
        {
            foreach (var marker in UnityEngine.Object.FindObjectsOfType<CustomMapMarker>())
            {
                if (marker.name == name)
                {
                    UnityEngine.Object.Destroy(marker);
                }
            }
        }

        private void RemoveMarkers()
        {
            foreach (var marker in UnityEngine.Object.FindObjectsOfType<CustomMapMarker>())
            {
                UnityEngine.Object.Destroy(marker);
            }
        }

        private void CreateCustomMarker(CachedMarker def, BasePlayer player = null)
        {
            var marker = new GameObject().AddComponent<CustomMapMarker>();
            marker.name = def.name;
            marker.displayName = def.displayName;
            marker.radius = def.radius;
            marker.position = def.position;
            marker.duration = def.duration;
            marker.refreshRate = def.refreshRate;
            marker.placedByPlayer = true;
            ColorUtility.TryParseHtmlString($"#{def.color1}", out marker.color1);
            ColorUtility.TryParseHtmlString($"#{def.color2}", out marker.color2);
            Message(player, "Added", marker.displayName, marker.position);
        }

        private void RemoveCustomMarker(string name, BasePlayer player = null)
        {
            var i = 0;
            foreach (var marker in UnityEngine.Object.FindObjectsOfType<CustomMapMarker>())
            {
                if (marker.placedByPlayer == false)
                {
                    continue;
                }

                if (marker.name == name)
                {
                    UnityEngine.Object.Destroy(marker);
                    i++;
                }
            }

            RemoveSavedMarker(name);
            Message(player, "Removed", i);
        }

        private void SaveCustomMarker(CachedMarker def)
        {
            data.Add(def);
        }

        private void LoadCustomMarkers()
        {
            foreach (var def in data)
            {
                CreateCustomMarker(def);
            }
        }

        private void RemoveSavedMarker(string name)
        {
            data.RemoveAll(x => x.name == name);
        }

        #endregion

        #region Localization 1.1.1

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {
                    "Usage", "<color=#00ffff>Usage:</color>\n" +
                             " <color=#00ffff>/marker add</color> name(code name) duration(seconds, 0 to permanent) refreshRate(30) radius(0.4) displayName (on map) colorInline (HEX) colorOutline (HEX) - Add marker on map\n" +
                             " <color=#00ffff>/marker remove</color> name (code name, only for custom markers) - Remove marker from map"
                },
                {"Permission", "You don't have permission to use that!"},
                {"Added", "Marker '{0}' was added on {1}!"},
                {"Removed", "{0} markers with that name was removed!"}
            }, this);
        }

        private string GetMessage(string messageKey, string playerID, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this, playerID), args);
        }

        private void Message(BasePlayer player, string messageKey, params object[] args)
        {
            if (player == null)
            {
                return;
            }

            var message = GetMessage(messageKey, player.UserIDString, args);
            player.SendConsoleCommand("chat.add", (object) 0, (object) message);
        }

        #endregion

        #region Data 1.0.0

        private const string filename = "MarkerManager/Custom";
        private static List<CachedMarker> data = new List<CachedMarker>();

        private class CachedMarker
        {
            public float radius;
            public string color1;
            public string color2;
            public string displayName;
            public string name;
            public float refreshRate;
            public Vector3 position;
            public int duration;
        }

        private void LoadData()
        {
            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<List<CachedMarker>>(filename);
            }
            catch (Exception e)
            {
                PrintWarning(e.Message);
            }

            SaveData();
            timer.Every(Core.Random.Range(500, 700f), SaveData);
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(filename, data);
        }

        #endregion

        #region API

        private void API_CreateMarker(Vector3 position, string name,
            int duration = 0, float refreshRate = 3f, float radius = 0.4f,
            string displayName = "Marker", string colorMarker = "00FFFF", string colorOutline = "00FFFFFF")
        {
            CreateMarker(position, duration, refreshRate, name, displayName, radius, colorMarker, colorOutline);
        }

        private void API_CreateMarker(BaseEntity entity, string name,
            int duration = 0, float refreshRate = 3f, float radius = 0.4f,
            string displayName = "Marker", string colorMarker = "00FFFF", string colorOutline = "00FFFFFF")
        {
            CreateMarker(entity, duration, refreshRate, name, displayName, radius, colorMarker, colorOutline);
        }

        private void API_RemoveMarker(string name)
        {
            RemoveMarker(name);
        }

        #endregion

        #region Scripts

        private class CustomMapMarker : MonoBehaviour
        {
            private VendingMachineMapMarker vending;
            private MapMarkerGenericRadius generic;
            public BaseEntity parent;
            private bool asChild;

            public float radius;
            public Color color1;
            public Color color2;
            public string displayName;
            public float refreshRate;
            public Vector3 position;
            public int duration;
            public bool placedByPlayer;

            private void Start()
            {
                transform.position = position;
                asChild = parent != null;
                CreateMarkers();
            }

            private void CreateMarkers()
            {
                vending = GameManager.server.CreateEntity(vendingPrefab, position)
                    .GetComponent<VendingMachineMapMarker>();
                vending.markerShopName = displayName;
                vending.enableSaving = false;
                vending.Spawn();

                generic = GameManager.server.CreateEntity(genericPrefab).GetComponent<MapMarkerGenericRadius>();
                generic.color1 = color1;
                generic.color2 = color2;
                generic.radius = radius;
                generic.alpha = 1f;
                generic.enableSaving = false;
                generic.SetParent(vending);
                generic.Spawn();

                if (duration != 0)
                {
                    Invoke(nameof(DestroyMakers), duration);
                }

                UpdateMarkers();

                if (refreshRate > 0f)
                {
                    if (asChild)
                    {
                        InvokeRepeating(nameof(UpdatePosition), refreshRate, refreshRate);
                    }
                    else
                    {
                        InvokeRepeating(nameof(UpdateMarkers), refreshRate, refreshRate);
                    }
                }
            }

            private void UpdatePosition()
            {
                if (asChild == true)
                {
                    if (parent.IsValid() == false)
                    {
                        Destroy(this);
                        return;
                    }
                    else
                    {
                        var pos = parent.transform.position;
                        transform.position = pos;
                        vending.transform.position = pos;
                    }
                }

                UpdateMarkers();
            }

            private void UpdateMarkers()
            {
                vending.SendNetworkUpdate();
                generic.SendUpdate();
            }

            private void DestroyMakers()
            {
                if (vending.IsValid())
                {
                    vending.Kill();
                }

                if (generic.IsValid())
                {
                    generic.Kill();
                }

                if (placedByPlayer)
                {
                    data.RemoveAll(x => x.name == name);
                }
            }

            private void OnDestroy()
            {
                DestroyMakers();
            }
        }

        #endregion
    }
}