using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using Color = System.Drawing.Color;
using Graphics = System.Drawing.Graphics;

namespace Oxide.Plugins
{
    [Info("Sign Map", "MJSU", "1.0.3")]
    [Description("Allows placing the rust map on a signs")]
    internal class SignMap : RustPlugin
    {
        #region Class Fields
        [PluginReference] private Plugin RustMapApi;

        private PluginConfig _pluginConfig; //Plugin Config
        
        private const string UsePermission = "signmap.use";
        private const string NoResourcesPermission = "signmap.noresources";
        private const string NoCooldownPermission = "signmap.nocooldown";
        private const string AccentColor = "#de8732";
        
        private readonly Hash<string, ItemDefinition> _prefabNameToItem = new Hash<string, ItemDefinition>();
        private readonly Hash<ulong, Coroutine> _activeRoutines = new Hash<ulong, Coroutine>();
        private readonly Hash<ulong, DateTime> _cooldowns = new Hash<ulong, DateTime>();
        private readonly Hash<ulong, List<Signage>> _undoSigns = new Hash<ulong, List<Signage>>();
        
        private const string DefaultMapName = "Default";

        private GameObject _go;
        private SignBehavior _behavior;
        #endregion

        #region Setup & Loading
        private void Init()
        {
            permission.RegisterPermission(UsePermission, this);
            permission.RegisterPermission(NoResourcesPermission, this);
            permission.RegisterPermission(NoCooldownPermission, this);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Chat] = $"<color=#bebebe>[<color={AccentColor}>{Title}</color>] {{0}}</color>",
                [LangKeys.NoPermission] = "You do not have permission to use this command",
                [LangKeys.NoSign] = "You're not looking at a sign",
                [LangKeys.CanNotUpdate] = "You're not allowed to update this sign",
                [LangKeys.InvalidRow] = "You have entered an invalid number for row: {0}",
                [LangKeys.InvalidCol] = "You have entered an invalid number for column: {0}",
                [LangKeys.SignNotSupported] = "This sign is not supported by sign map. Please contact plugin author with the following information. {0}",
                [LangKeys.ImageNotValid] = "This map image is not valid. Please contact plugin author.",
                [LangKeys.ActiveGeneration] = "You already have an active sign map generation in progress. You cannot use this command until it is finished.",
                [LangKeys.NoAvailableUndos] = "You do not have any signs that can be undone.",
                [LangKeys.UndoSuccessful] = "All signs have been undone.",
                [LangKeys.UnderCooldown] = "You still have {0:0} seconds remaining before you can use this command again.",
                [LangKeys.InvalidGenSyntax] = $"Invalid Syntax. <color={AccentColor}>/{{0}} gen 2 3</color> - to create a sign map that is 2 rows and 3 columns.",
                [LangKeys.MaxSize] = "The maximum number of signs generated cannot be more than {0}",
                [LangKeys.NotEnoughItems] = "You do not have enough {0}. You have {1} and you need {2}",
                [LangKeys.NeedsWall] = "Can only create maps greater than 1 row and 1 column with signs that are placed on walls",
                [LangKeys.SignBroke] = "This sign broke or was destroyed",
                [LangKeys.FinishedGenerating] = "We have finished generating your sign map using the {0} map that is {1} row(s) and {2} column(s)",
                [LangKeys.Refunded] = "We have refunded {0} {1}(s) to you",
                [LangKeys.MapHeader] = "The list of available maps are:",
                [LangKeys.MapName] = $"<color={AccentColor}>{{0}}</color>",
                [LangKeys.HelpText] = "Allows add map image to signs or creating a map sign grid\n" +
                                      $"<color={AccentColor}>/{{0}} gen 1 1</color> - will add the full map to the sign you're looking at'\n" +
                                      $"<color={AccentColor}>/{{0}} gen 4 5</color> - will create a 4 row x 5 column map using the sign you're looking at as the bottom left corner\n" +
                                      $"<color={AccentColor}>/{{0}} gen 4 5 nr|norotation</color> - will create a 4 x 5 map and not correct the rotation of the sign\n" +
                                      $"<color={AccentColor}>/{{0}} gen 4 5 Icon</color> - will create a 4 x 5 map using the Icons render\n" +
                                      $"<color={AccentColor}>/{{0}} undo</color> - will undo the most recently generated sign map and refund the cost\n" +
                                      $"<color={AccentColor}>/{{0}} maps</color> - to see the list of available maps to use'\n" +
                                      $"<color={AccentColor}>/{{0}}</color> - to display this help text again."
            }, this);
        }
        
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig config)
        {
            return config;
        }

        private void OnServerInitialized()
        {
            if (RustMapApi == null)
            {
                PrintError("Missing plugin dependency RustMapApi: https://umod.org/plugins/rust-map-api");
                return;
            }
            
            _go = new GameObject();
            _behavior = _go.AddComponent<SignBehavior>();
            
            foreach (ItemDefinition item in ItemManager.GetItemDefinitions())
            {
                ItemModDeployable itemDeployable = item?.GetComponent<ItemModDeployable>();
                if (itemDeployable == null)
                {
                    continue;
                }

                string path = itemDeployable.entityPrefab.resourcePath;
                if (!SignImageSizes.Keys.Any(s => path.Contains(s)))
                {
                    continue;
                }
                
                if (!_prefabNameToItem.ContainsKey(path))
                {
                    _prefabNameToItem[path] = item;
                }
            }
        }
        
        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            Coroutine routine = _activeRoutines[player.userID];
            if (routine != null)
            {
                _behavior.StopCoroutine(routine);
            }
        }

        private void Unload()
        {
            if (_go != null)
            {
                _behavior.StopAllCoroutines();
                GameObject.Destroy(_go);
            }
        }
        #endregion

        #region Chat Command
        [ChatCommand("sm")]
        private void SignMapChatCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!HasPermission(player, UsePermission) && !player.IsAdmin)
            {
                Chat(player, LangKeys.NoPermission);
                return;
            }
            
            if (args.Length == 0)
            {
                Chat(player, LangKeys.HelpText, _pluginConfig.ChatCommand);
                return;
            }
            
            if (_activeRoutines[player.userID] != null)
            {
                Chat(player, LangKeys.ActiveGeneration);
                return;
            }
            
            switch (args[0].ToLower())
            {
                case "undo":
                    HandleUndo(player);
                    break;
                
                case "maps":
                    HandleMaps(player);
                    break;
                
                case "gen":
                    HandleGen(player, args);
                    break;
                
                default:
                    Chat(player, LangKeys.HelpText, _pluginConfig.ChatCommand);
                    break;
            }
        }

        private void HandleUndo(BasePlayer player)
        {
            List<Signage> signs = _undoSigns[player.userID];
            if (signs == null)
            {
                Chat(player, LangKeys.NoAvailableUndos);
                return;
            }

            _activeRoutines[player.userID] = _behavior.StartCoroutine(UndoRoutine(player, signs));
        }

        private IEnumerator UndoRoutine(BasePlayer player, List<Signage> signs)
        {
            ItemDefinition def = null;
            int refund = 0;
            foreach (Signage sign in signs)
            {
                if (sign == null || sign.IsDestroyed)
                {
                    continue;
                }

                if (def == null)
                {
                    def = _prefabNameToItem[sign.PrefabName];
                }
                
                sign.Die();
                refund++;
                yield return null;
            }

            if (!HasPermission(player, NoResourcesPermission))
            {
                yield return HandleRefund(player, def, refund);
            }

            Chat(player, LangKeys.UndoSuccessful);
            _activeRoutines.Remove(player.userID);
        }

        private void HandleMaps(BasePlayer player)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Lang(LangKeys.MapHeader, player));
            foreach (string map in GetMaps())
            {
                sb.AppendLine(Lang(LangKeys.MapName, player, map));
            }
            
            Chat(player, sb.ToString());
        }

        private void HandleGen(BasePlayer player, string[] args)
        {
            if (!HasPermission(player, NoCooldownPermission) && _cooldowns.ContainsKey(player.userID) && _cooldowns[player.userID] > DateTime.Now)
            {
                TimeSpan remaining = _cooldowns[player.userID] - DateTime.Now;
                Chat(player, LangKeys.UnderCooldown, remaining.TotalSeconds);
                return;
            }

            if (args.Length < 3)
            {
                Chat(player, LangKeys.InvalidGenSyntax, _pluginConfig.ChatCommand);
                return;
            }

            string numRowsArg = args[1];
            string numColsArg = args[2];

            int numRows;
            if (!int.TryParse(numRowsArg, out numRows) || numRows <= 0)
            {
                Chat(player, LangKeys.InvalidRow, numRowsArg);
                return;
            }
            
            int numCols;
            if (!int.TryParse(numColsArg, out numCols) || numCols <= 0)
            {
                Chat(player, LangKeys.InvalidCol, numColsArg);
                return;
            }

            if (numRows * numCols > _pluginConfig.MaxSigns)
            {
                Chat(player, LangKeys.MaxSize, _pluginConfig.MaxSigns);
                return;
            }
            
            Signage sign = RaycastAll<Signage>(player.eyes.HeadRay(), 5f);
            if (sign == null)
            {
                Chat(player, LangKeys.NoSign);
                return;
            }

            if (!sign.CanUpdateSign(player))
            {
                Chat(player, LangKeys.CanNotUpdate);
                return;
            }

            ItemDefinition def = _prefabNameToItem[sign.PrefabName];
            if (!HasPermission(player, NoResourcesPermission))
            {
                int amount = player.inventory.GetAmount(def.itemid);
                int need = numRows * numCols - 1;
                if (amount < need)
                {
                    Chat(player, LangKeys.NotEnoughItems, def.displayName.translated, amount, need);
                    return;
                }

                List<Item> items = Pool.GetList<Item>();
                player.inventory.Take(items, def.itemid, need);
                foreach (Item item in items)
                {
                    item.Remove();
                }

                Pool.FreeList(ref items);
            }

            bool correctRotation = !args.Any(a => a.Equals("nr", StringComparison.InvariantCultureIgnoreCase) || a.Equals("norotation", StringComparison.InvariantCultureIgnoreCase ));

            List<string> maps = GetMaps();
            string map = maps.FirstOrDefault(a => args.Any(m => m.Equals(a, StringComparison.InvariantCultureIgnoreCase)));
            if (string.IsNullOrEmpty(map))
            {
                map = maps.FirstOrDefault(m => m.Equals(_pluginConfig.DefaultMap, StringComparison.InvariantCultureIgnoreCase));
                if (string.IsNullOrEmpty(map))
                {
                    map = DefaultMapName;
                }
            }
                    
            _behavior.StartCoroutine(CreateSignGrid(player, sign, numRows, numCols, def, map, correctRotation));
        }

        private IEnumerator CreateSignGrid(BasePlayer player, Signage sign, int numRows, int numCols, ItemDefinition def, string mapName, bool correctRotation)
        {
            try
            {
                List<Signage> signs = new List<Signage>();
                _undoSigns[player.userID] = signs;
                
                 if (numCols > 1 || numRows > 1)
                 {
                     BuildingBlock block = GetNearbyBuildingBlock(sign);
                     if (block == null || !block.ShortPrefabName.Contains("wall"))
                     {
                         Chat(player, LangKeys.NeedsWall);
                         yield break;
                     }

                     if (correctRotation)
                     {
                         Vector3 relativePoint = block.transform.InverseTransformPoint(sign.transform.position);
                         if (relativePoint.x < 0.0)
                         {
                             sign.transform.rotation = block.transform.rotation * Quaternion.Euler(0, 270, 0);
                         }
                         else if (relativePoint.x > 0.0)
                         {
                             sign.transform.rotation = block.transform.rotation * Quaternion.Euler(0, 90, 0);
                         }
                     }

                    sign.transform.position += sign.transform.forward * 0.001f;
                    sign.SendNetworkUpdateImmediate();
                 }
                
                 yield return null;
                
                 if (sign == null || sign.IsDestroyed)
                 {
                     Chat(player, LangKeys.SignBroke);
                     yield break;
                 }

                float width = sign.bounds.size.x;
                float height = sign.bounds.size.y;
                Vector3 signPos = sign.transform.position;
                Vector3 signRight = sign.transform.right;
                Vector3 signUp = sign.transform.up;
                Quaternion signRot = sign.transform.rotation;
                string signPrefab = sign.PrefabName;

                int refund = 0;
                for (int row = 0; row < numRows; row++)
                {
                    for (int col = 0; col < numCols; col++)
                    {
                        Signage newSign;
                        if (row == 0 && col == 0)
                        {
                            newSign = sign;
                        }
                        else
                        {
                            Vector3 pos = signPos + -signRight * (width * col) + signUp * (height * row);
                            newSign = GameManager.server.CreateEntity(signPrefab, pos, signRot) as Signage;
                            newSign.OwnerID = player.userID;
                            newSign.Spawn();
                        }

                        yield return null;
                        
                         Hash<string, object> mapSection = RustMapApi.Call("GetSection", mapName, numRows, numCols, row, col) as Hash<string, object>;
                        
                         byte[] map = mapSection["image"] as byte[];
                         if (map == null)
                         {
                             refund++;
                             continue;
                         }
                        
                         yield return null;
                        
                         if (newSign == null || newSign.IsDestroyed)
                         {
                             refund++;
                             continue;
                         }
                        
                         newSign.BroadcastMessage("OnPhysicsNeighbourChanged", SendMessageOptions.DontRequireReceiver);
                         if (newSign == null || newSign.IsDestroyed)
                         {
                             refund++;
                             continue;
                         }
                        
                        signs.Add(newSign);
                        
                        yield return null;
                        
                        yield return AddImageToSign(player, newSign, map);
                        
                        yield return new WaitForSeconds(_pluginConfig.GenerationDelay);
                    }
                }

                if (refund > 0 && !HasPermission(player, NoResourcesPermission))
                {
                    yield return HandleRefund(player, def, refund);
                }
            }
            finally
            {
                _cooldowns[player.userID] = DateTime.Now + TimeSpan.FromSeconds(_pluginConfig.Cooldown);
                _activeRoutines.Remove(player.userID);
                Chat(player, LangKeys.FinishedGenerating, mapName, numRows, numCols);
            }
        }

        private BuildingBlock GetNearbyBuildingBlock(BaseEntity entity)
        {
            float minDistance = float.MaxValue;
            BuildingBlock buildingBlock = null;
            Vector3 point = entity.PivotPoint();
            List<BuildingBlock> list = Pool.GetList<BuildingBlock>();
            Vis.Entities(point, 1.5f, list, Rust.Layers.Construction);
            for (int i = 0; i < list.Count; i++)
            {
                BuildingBlock item = list[i];
                float distance = item.SqrDistance(point);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    buildingBlock = item;
                }
            }

            Pool.FreeList(ref list);
            return buildingBlock;
        }

        private IEnumerator HandleRefund(BasePlayer player, ItemDefinition def, int amount)
        {
            Item item = ItemManager.CreateByItemID(def.itemid, amount);
            int max = Math.Max(1, item.MaxStackable());
            while (item.amount > max)
            {
                player.GiveItem(item.SplitItem(max));
                yield return null;
            }
                
            player.GiveItem(item);
            Chat(player, LangKeys.Refunded, amount, def.displayName.translated);
            yield return null;
        }

        private List<string> GetMaps()
        {
            return RustMapApi.Call<List<string>>("GetRenderNames");
        }
        #endregion

        #region Map Handler
        private IEnumerator AddImageToSign(BasePlayer player, Signage sign, byte[] data)
        {
            yield return null;

            ImageSize size = SignImageSizes[sign.ShortPrefabName];
            if (size == null)
            {
                Chat(player, LangKeys.SignNotSupported, sign.PrefabName);
                yield break;
            }

            byte[] resized = ResizeImage(data, size.ImageWidth, size.ImageHeight);
            if(!ImageProcessing.IsValidPNG(resized, 1024, 1024))
            {
                Chat(player, LangKeys.ImageNotValid);
                yield break;
            }

            yield return null;
            
            if (sign.textureIDs[0] != 0)
            {
                FileStorage.server.Remove(sign.textureIDs[0], FileStorage.Type.png, sign.net.ID);
                yield return null;
            }
            
            sign.textureIDs[0] = FileStorage.server.Store(resized, FileStorage.Type.png, sign.net.ID);
            sign.SendNetworkUpdate();
        }
        
        private byte[] ResizeImage(byte[] bytes, int targetWidth, int targetHeight)
        {
            using (MemoryStream original = new MemoryStream(), resizedBytesStream = new MemoryStream())
            {
                original.Write(bytes, 0, bytes.Length);
                using (Bitmap img = new Bitmap(Image.FromStream(original)))
                {
                    using (Bitmap resize = ResizeImage(img, targetWidth, targetHeight))
                    {
                        //Rust images are crc and if we have the same image it is deleted from the file storage
                        //Here we changed the last few pixels of the image with colors based off the current milliseconds since wipe
                        //This will generate a unique image every time and allow us to use the same image multiple times
                        byte[] milli = BitConverter.GetBytes((DateTime.Now - SaveRestore.SaveCreatedTime).TotalMilliseconds);
                        for (int i = 0; i < milli.Length / 4; i++)
                        {
                            int red = GetValueAtIndex(milli, i * 4);
                            int green = GetValueAtIndex(milli, i * 4 + 1);
                            int blue = GetValueAtIndex(milli, i * 4 + 2);
                            int alpha = GetValueAtIndex(milli, i * 4 + 3);
                            
                            Color pixel = Color.FromArgb(red, green, blue, alpha);
                            resize.SetPixel(targetWidth - 1 - i, targetHeight - 1, pixel);
                        }
                        
                        resize.Save(resizedBytesStream, ImageFormat.Png);
                        return resizedBytesStream.ToArray();
                    }
                }
            }
        }
        
        /// <summary>
        /// Resize the image to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        private static Bitmap ResizeImage(Image image, int width, int height)
        {
            Rectangle destRect = new Rectangle(0, 0, width, height);
            Bitmap destImage = new Bitmap(width, height);

            //destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (Graphics graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (ImageAttributes wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width,image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

        private int GetValueAtIndex(byte[] bytes, int index)
        {
            if (index >= bytes.Length)
            {
                return 0;
            }

            return Convert.ToInt32(bytes[index]);
        }

        #endregion

        #region Helper Methods
        private T RaycastAll<T>(Ray ray, float distance) where T : BaseEntity
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, distance);
            GamePhysics.Sort(hits);
            return hits
                .Where(h => h.GetEntity() is T)
                .Select(h => h.GetEntity() as T)
                .FirstOrDefault();
        }
        
        private void Chat(BasePlayer player, string key, params object[] args) => PrintToChat(player, Lang(LangKeys.Chat, player, Lang(key, player, args)));

        private string Lang(string key, BasePlayer player = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, player?.UserIDString), args);
            }
            catch(Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception\n:{ex.Message}");
                throw;
            }
        }

        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);
        #endregion

        #region Behavior
        private class SignBehavior : FacepunchBehaviour
        {
            
        }
        #endregion

        #region Classes
        private class PluginConfig
        {
            [DefaultValue("sm")]
            [JsonProperty(PropertyName = "Chat Command")]
            public string ChatCommand { get; set; }
            
            [DefaultValue(0.1f)]
            [JsonProperty(PropertyName = "Delay between sign generation (Seconds)")]
            public float GenerationDelay { get; set; }
            
            [DefaultValue(120f)]
            [JsonProperty(PropertyName = "Command cooldown (Seconds)")]
            public float Cooldown { get; set; }
            
            [DefaultValue(16)]
            [JsonProperty(PropertyName = "Max number of signs in generated grid")]
            public int MaxSigns { get; set; }
            
            [DefaultValue("Icons")]
            [JsonProperty(PropertyName = "Default map to use when non specified")]
            public string DefaultMap { get; set; }
        }
        private class LangKeys
        {
            public const string Chat = "Chat";
            public const string NoPermission = "NoPermission";
            public const string NoSign = "NoSign";
            public const string CanNotUpdate = "CanNotUpdate";
            public const string InvalidRow = "InvalidRow";
            public const string InvalidCol = "InvalidCol";
            public const string HelpText = "HelpText";
            public const string SignNotSupported = "SignNotSupported";
            public const string ImageNotValid = "ImageNotValid";
            public const string ActiveGeneration = "ActiveGeneration";
            public const string NoAvailableUndos = "NoAvailableUndos";
            public const string UnderCooldown = "UnderCooldown";
            public const string UndoSuccessful = "UndoSuccessful";
            public const string InvalidGenSyntax = "InvalidGenSyntax";
            public const string MaxSize = "MaxSize";
            public const string NotEnoughItems = "NotEnoughItems";
            public const string NeedsWall = "NeedsWall";
            public const string SignBroke = "SignBroke";
            public const string FinishedGenerating = "FinishedGenerating";
            public const string Refunded = "Refunded";
            public const string MapHeader = "MapHeader";
            public const string MapName = "MapName";
        }
        #endregion

        #region Sign Artist Code
        private Dictionary<string, ImageSize> SignImageSizes { get; } = new Dictionary<string, ImageSize>
        {
            // Picture Frames
            ["sign.pictureframe.landscape"] = new ImageSize(256, 128), // Landscape Picture Frame
            ["sign.pictureframe.portrait"] = new ImageSize(128, 256), // Portrait Picture Frame
            ["sign.pictureframe.tall"] = new ImageSize(128, 512), // Tall Picture Frame
            ["sign.pictureframe.xl"] = new ImageSize(512, 512), // XL Picture Frame
            ["sign.pictureframe.xxl"] = new ImageSize(1024, 512), // XXL Picture Frame

            // Wooden Signs
            ["sign.small.wood"] = new ImageSize(128, 64), // Small Wooden Sign
            ["sign.medium.wood"] = new ImageSize(256, 128), // Wooden Sign
            ["sign.large.wood"] = new ImageSize(256, 128), // Large Wooden Sign
            ["sign.huge.wood"] = new ImageSize(512, 128), // Huge Wooden Sign

            // Banners
            ["sign.hanging.banner.large"] = new ImageSize(64, 256), // Large Banner Hanging
            ["sign.pole.banner.large"] = new ImageSize(64, 256), // Large Banner on Pole

            // Hanging Signs
            ["sign.hanging"] = new ImageSize(128, 256), // Two Sided Hanging Sign
            ["sign.hanging.ornate"] = new ImageSize(256, 128), // Two Sided Ornate Hanging Sign

            // Town Signs
            ["sign.post.single"] = new ImageSize(128, 64), // Single Sign Post
            ["sign.post.double"] = new ImageSize(256, 256), // Double Sign Post
            ["sign.post.town"] = new ImageSize(256, 128), // One Sided Town Sign Post
            ["sign.post.town.roof"] = new ImageSize(256, 128), // Two Sided Town Sign Post

            // Other paintable assets
            ["spinner.wheel.deployed"] = new ImageSize(512, 512, 285, 285), // Spinning Wheel
        };

        private class ImageSize
        {
            public int Width { get; }
            public int Height { get; }
            public int ImageWidth { get; }
            public int ImageHeight { get; }
            
            public ImageSize(int width, int height) : this(width, height, width, height)
            {
            }
            
            public ImageSize(int width, int height, int imageWidth, int imageHeight)
            {
                Width = width;
                Height = height;
                ImageWidth = imageWidth;
                ImageHeight = imageHeight;
            }
        }
        #endregion
    }
}
