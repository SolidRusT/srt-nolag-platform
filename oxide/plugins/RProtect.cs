#region License (GPL v3)
/*
    DESCRIPTION
    Copyright (c) 2020 RFC1920 <desolationoutpostpve@gmail.com>

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/
#endregion License Information (GPL v3)
//#define DEBUG
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Research Protection", "RFC1920", "0.1.4")]
    class RProtect : RustPlugin
    {
        private const string RPGUI = "blueblocker.gui";
        private const string RPGUI2 = "blueblocker.gui2";
        private Dictionary<uint, ulong> rsloot = new Dictionary<uint, ulong>();
        private Dictionary<ulong, Timer> rstimer = new Dictionary<ulong, Timer>();
        private List<ulong> canres = new List<ulong>();

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));
        #endregion

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["alldone"] = "You have already researched this item!",
                ["override"] = "Click again to override -->",
                ["proton"] = "Research protection enabled"
            }, this);
        }

        private object CanResearchItem(BasePlayer player, Item item)
        {
            if (player == null) return null;
            if (item == null) return null;
            if(player.blueprints.HasUnlocked(item.info))
            {
                if (canres.Contains(player.userID)) return null;

                if (rsloot.ContainsValue(player.userID))
                {
                    var rst = rsloot.FirstOrDefault(x => x.Value == player.userID).Key;
                    canres.Add(player.userID);
                    RsGUI(player, rst, Lang("alldone"));
                    timer.Once(3f, () => RsGUI(player, rst, null, player.inventory.loot.IsLooting()));
                    timer.Once(3f, () => canres.Remove(player.userID));
                }
                return false;
            }
            return null;
        }

        void Unload()
        {
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, RPGUI);
                CuiHelper.DestroyUi(player, RPGUI2);
            }
        }

        private object CanLootEntity(BasePlayer player, ResearchTable rst)
        {
            if(rst == null) return null;
            if(rsloot.ContainsKey(rst.net.ID)) return null;

            rsloot.Add(rst.net.ID, player.userID);

            if (!rstimer.ContainsKey(player.userID))
            {
#if DEBUG
                Puts($"Creating CheckLooting timer for {player.displayName}");
#endif
                rstimer.Add(player.userID, timer.Every(0.5f, () => CheckLooting(player)));
                RsGUI(player, rst.net.ID);
            }

            return null;
        }

        void CheckLooting(BasePlayer player)
        {
#if DEBUG
            Puts($"Running CheckLooting for {player.displayName}");
#endif
            if (!player.inventory.loot.IsLooting())
            {
#if DEBUG
                Puts("Not looting.  Killing GUI and CheckLooting timer...");
#endif
                CuiHelper.DestroyUi(player, RPGUI);
                CuiHelper.DestroyUi(player, RPGUI2);
                player.EndLooting();
                rstimer[player.userID].Destroy();
                rstimer.Remove(player.userID);
            }
        }

        void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            // This setup is causing occaisional continuance of the GUI when looting the RT has ended...
            ulong networkID;
            if (entity == null || !rsloot.TryGetValue(entity.net.ID, out networkID))
            {
                return;
            }

            if (networkID == player.userID)
            {
                CuiHelper.DestroyUi(player, RPGUI);
                CuiHelper.DestroyUi(player, RPGUI2);
                rsloot.Remove(entity.net.ID);
                canres.Remove(player.userID);
            }
        }

        void RsGUI(BasePlayer player, uint rst, string label = null, bool looting = true)
        {
            if (!looting) return;
            CuiHelper.DestroyUi(player, RPGUI);
            CuiHelper.DestroyUi(player, RPGUI2);

            CuiElementContainer container = UI.Container(RPGUI, UI.Color("FFF5E1", 0.16f), "0.77 0.798", "0.9465 0.835", false, "Overlay");
            string uicolor = "#ff3333";
            if(label == null)
            {
                label = Lang("proton");
                uicolor = "#dddddd";
            }
            UI.Label(ref container, RPGUI, UI.Color(uicolor, 1f), label, 12, "0 0", "1 1");

            CuiHelper.AddUi(player, container);

            if (canres.Contains(player.userID))
            {
                CuiElementContainer cont2 = UI.Container(RPGUI2, UI.Color("ff4444", 1f), "0.657 0.163", "0.765 0.205", false, "Overlay");
                UI.Label(ref cont2, RPGUI2, UI.Color("#ffffff", 1f), Lang("override"), 12, "0 0", "1 1");
                CuiHelper.AddUi(player, cont2);
            }
        }

        #region Classes
        public static class UI
        {
            public static CuiElementContainer Container(string panel, string color, string min, string max, bool useCursor = false, string parent = "Overlay")
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = color },
                            RectTransform = {AnchorMin = min, AnchorMax = max},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panel
                    }
                };
                return container;
            }
            public static void Label(ref CuiElementContainer container, string panel, string color, string text, int size, string min, string max, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = min, AnchorMax = max }
                },
                panel);
            }
            public static string Color(string hexColor, float alpha)
            {
                if(hexColor.StartsWith("#"))
                {
                    hexColor = hexColor.Substring(1);
                }
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
        #endregion
    }
}
