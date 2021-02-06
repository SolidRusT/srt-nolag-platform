// Reference: System.Drawing
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Plugins.SignArtistClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using UnityEngine.Networking;
using Color = System.Drawing.Color;
using Graphics = System.Drawing.Graphics;
using Steamworks;

namespace Oxide.Plugins
{
    [Info("Sign Artist", "Whispers88", "1.2.6")]
    [Description("Allows players with the appropriate permission to import images from the internet on paintable objects")]

    /*********************************************************************************
     * This plugin was originally created by Bombardir and then maintained by Nogrod.
     * It was rewritten from scratch by Mughisi on January 12th, 2018.
     *********************************************************************************/

    internal class SignArtist : RustPlugin
    {
        private Dictionary<ulong, float> cooldowns = new Dictionary<ulong, float>();
        private GameObject imageDownloaderGameObject;
        private ImageDownloader imageDownloader;
        SignArtistConfig Settings { get; set; }
        Dictionary<string, ImageSize> ImageSizePerAsset { get; set; }

        Dictionary<ulong, string> SkiniconUrls = new Dictionary<ulong, string>();

        private const string ItemIconUrl = "https://www.rustedit.io/images/imagelibrary/{0}.png";


        /// <summary>
        /// Plugin configuration
        /// </summary>
        public class SignArtistConfig
        {
            [JsonProperty(PropertyName = "Time in seconds between download requests (0 to disable)")]
            public int Cooldown { get; set; }

            [JsonProperty(PropertyName = "Maximum concurrent downloads")]
            public int MaxActiveDownloads { get; set; }

            [JsonProperty(PropertyName = "Maximum distance from the sign")]
            public int MaxDistance { get; set; }

            [JsonProperty(PropertyName = "Maximum filesize in MB")]
            public float MaxSize { get; set; }

            [JsonProperty(PropertyName = "Enforce JPG file format")]
            public bool EnforceJpeg { get; set; }

            [JsonProperty(PropertyName = "JPG image quality (0-100)")]
            public int Quality
            {
                get
                {
                    return quality;
                }
                set
                {
                    // Validate the value, it can't be less than 0 and not more than 100.
                    if (value >= 0 && value <= 100)
                    {
                        quality = value;
                    }
                    else
                    {
                        // Set the quality to a default value of 85% when an invalid value was specified.
                        quality = value > 100 ? 100 : 85;
                    }
                }
            }

            [JsonProperty("Enable logging file")]
            public bool FileLogging { get; set; }

            [JsonProperty("Enable logging console")]
            public bool ConsoleLogging { get; set; }

            [JsonProperty("Enable discord logging")]
            public bool Discordlogging { get; set; }

            [JsonProperty("Discord Webhook")]
            public string DiscordWebhook { get; set; }

            [JsonProperty("Avatar URL")]
            public string AvatarUrl { get; set; }

            [JsonProperty("Discord Username")]
            public string DiscordUsername { get; set; }


            [JsonIgnore]
            public float MaxFileSizeInBytes
            {
                get
                {
                    return MaxSize * 1024 * 1024;
                }
            }

            private int quality = 85;

            /// <summary>
            /// Creates a default configuration file
            /// </summary>
            /// <returns>Default config</returns>
            public static SignArtistConfig DefaultConfig()
            {
                return new SignArtistConfig
                {
                    Cooldown = 0,
                    MaxSize = 1,
                    MaxDistance = 3,
                    MaxActiveDownloads = 5,
                    EnforceJpeg = false,
                    Quality = 85,
                    FileLogging = false,
                    ConsoleLogging = false,
                    Discordlogging = false,
                    DiscordWebhook = "",
                    AvatarUrl = "https://i.imgur.com/dH7V1Dh.png",
                    DiscordUsername = "Sign Artist"
                };
            }
        }

        /// <summary>
        /// A type used to request new images to download.
        /// </summary>
        private class DownloadRequest
        {
            public BasePlayer Sender { get; }
            public IPaintableEntity Sign { get; }
            public string Url { get; set; }
            public bool Raw { get; }
            public bool Hor { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="DownloadRequest" /> class.
            /// </summary>
            /// <param name="url">The URL to download the image from. </param>
            /// <param name="player">The player that requested the download. </param>
            /// <param name="sign">The sign to add the image to. </param>
            /// <param name="raw">Should the image be stored with or without conversion to jpeg. </param>
            public DownloadRequest(string url, BasePlayer player, IPaintableEntity sign, bool raw, bool hor)
            {
                Url = url;
                Sender = player;
                Sign = sign;
                Raw = raw;
                Hor = hor;
            }
        }

        /// <summary>
        /// A type used to request new images to be restored.
        /// </summary>
        private class RestoreRequest
        {
            public BasePlayer Sender { get; }
            public IPaintableEntity Sign { get; }
            public bool Raw { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="RestoreRequest" /> class.
            /// </summary>
            /// <param name="player">The player that requested the restore. </param>
            /// <param name="sign">The sign to restore the image from. </param>
            /// <param name="raw">Should the image be stored with or without conversion to jpeg. </param>
            public RestoreRequest(BasePlayer player, IPaintableEntity sign, bool raw)
            {
                Sender = player;
                Sign = sign;
                Raw = raw;
            }
        }

        /// <summary>
        /// A type used to determine the size of the image for a sign
        /// </summary>
        public class ImageSize
        {
            public int Width { get; }
            public int Height { get; }
            public int ImageWidth { get; }
            public int ImageHeight { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="ImageSize" /> class.
            /// </summary>
            /// <param name="width">The width of the canvas and the image. </param>
            /// <param name="height">The height of the canvas and the image. </param>
            public ImageSize(int width, int height) : this(width, height, width, height)
            {
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="ImageSize" /> class.
            /// </summary>
            /// <param name="width">The width of the canvas. </param>
            /// <param name="height">The height of the canvas. </param>
            /// <param name="imageWidth">The width of the image. </param>
            /// <param name="imageHeight">The height of the image. </param>
            public ImageSize(int width, int height, int imageWidth, int imageHeight)
            {
                Width = width;
                Height = height;
                ImageWidth = imageWidth;
                ImageHeight = imageHeight;
            }
        }


        #region Image Download Behaviour
        /// <summary>
        /// UnityEngine script to be attached to a GameObject to download images and apply them to signs.
        /// </summary>
        private class ImageDownloader : MonoBehaviour
        {
            private byte activeDownloads;
            private byte activeRestores;
            private readonly SignArtist signArtist = (SignArtist)Interface.Oxide.RootPluginManager.GetPlugin(nameof(SignArtist));
            private readonly Queue<DownloadRequest> downloadQueue = new Queue<DownloadRequest>();
            private readonly Queue<RestoreRequest> restoreQueue = new Queue<RestoreRequest>();

            /// <summary>
            /// Queue a new image to download and add to a sign
            /// </summary>
            /// <param name="url">The URL to download the image from. </param>
            /// <param name="player">The player that requested the download. </param>
            /// <param name="sign">The sign to add the image to. </param>
            /// <param name="raw">Should the image be stored with or without conversion to jpeg. </param>
            public void QueueDownload(string url, BasePlayer player, IPaintableEntity sign, bool raw, bool hor = false)
            {
                // Check if there is already a request for this sign and show an error if there is.
                bool existingRequest = downloadQueue.Any(request => request.Sign == sign) || restoreQueue.Any(request => request.Sign == sign);
                if (existingRequest)
                {
                    signArtist.SendMessage(player, "ActionQueuedAlready");

                    return;
                }

                // Instantiate a new DownloadRequest and add it to the queue.
                downloadQueue.Enqueue(new DownloadRequest(url, player, sign, raw, hor));

                // Attempt to start the next download.
                StartNextDownload();
            }

            /// <summary>
            /// Attempts to restore a sign.
            /// </summary>
            /// <param name="player">The player that requested the restore. </param>
            /// <param name="sign">The sign to restore the image from. </param>
            /// <param name="raw">Should the image be stored with or without conversion to jpeg. </param>
            public void QueueRestore(BasePlayer player, IPaintableEntity sign, bool raw)
            {
                // Check if there is already a request for this sign and show an error if there is.
                bool existingRequest = downloadQueue.Any(request => request.Sign == sign) || restoreQueue.Any(request => request.Sign == sign);
                if (existingRequest)
                {
                    signArtist.SendMessage(player, "ActionQueuedAlready");

                    return;
                }

                // Instantiate a new RestoreRequest and add it to the queue.
                restoreQueue.Enqueue(new RestoreRequest(player, sign, raw));

                // Attempt to start the next restore.
                StartNextRestore();
            }

            /// <summary>
            /// Starts the next download if available.
            /// </summary>
            /// <param name="reduceCount"></param>
            private void StartNextDownload(bool reduceCount = false)
            {
                // Check if we need to reduce the active downloads counter after a succesful or failed download.
                if (reduceCount)
                {
                    activeDownloads--;
                }

                // Check if we don't have the maximum configured amount of downloads running already.
                if (activeDownloads >= signArtist.Settings.MaxActiveDownloads)
                {
                    return;
                }

                // Check if there is still an image in the queue.
                if (downloadQueue.Count <= 0)
                {
                    return;
                }

                // Increment the active downloads by 1 and start the download process.
                activeDownloads++;
                StartCoroutine(DownloadImage(downloadQueue.Dequeue()));
            }

            /// <summary>
            /// Starts the next restore if available.
            /// </summary>
            /// <param name="reduceCount"></param>
            private void StartNextRestore(bool reduceCount = false)
            {
                // Check if we need to reduce the active restores counter after a succesful or failed restore.
                if (reduceCount)
                {
                    activeRestores--;
                }

                // Check if we don't have the maximum configured amount of restores running already.
                if (activeRestores >= signArtist.Settings.MaxActiveDownloads)
                {
                    return;
                }

                // Check if there is still an image in the queue.
                if (restoreQueue.Count <= 0)
                {
                    return;
                }

                // Increment the active restores by 1 and start the restore process.
                activeRestores++;
                StartCoroutine(RestoreImage(restoreQueue.Dequeue()));
            }

            /// <summary>
            /// Downloads the image and adds it to the sign.
            /// </summary>
            /// <param name="request">The requested <see cref="DownloadRequest"/> instance. </param>
            private IEnumerator DownloadImage(DownloadRequest request)
            {
                if (ItemManager.itemDictionaryByName.ContainsKey(request.Url))
                {
                    request.Url = string.Format(ItemIconUrl, request.Url);
                }

                UnityWebRequest www = UnityWebRequest.Get(request.Url);

                yield return www.SendWebRequest();

                // Verify that there is a valid reference to the plugin from this class.
                if (signArtist == null)
                {
                    throw new NullReferenceException("signArtist");
                }

                // Verify that the webrequest was succesful.
                if (www.isNetworkError || www.isHttpError)
                {
                    // The webrequest wasn't succesful, show a message to the player and attempt to start the next download.
                    signArtist.SendMessage(request.Sender, "WebErrorOccurred", www.error);
                    www.Dispose();
                    StartNextDownload(true);
                    yield break;
                }

                // Verify that the file doesn't exceed the maximum configured filesize.
                if (www.downloadedBytes > signArtist.Settings.MaxFileSizeInBytes)
                {
                    // The file is too large, show a message to the player and attempt to start the next download.
                    signArtist.SendMessage(request.Sender, "FileTooLarge", signArtist.Settings.MaxSize);
                    www.Dispose();
                    StartNextDownload(true);
                    yield break;
                }

                // Get the bytes array for the image from the webrequest and lookup the target image size for the targeted sign.
                byte[] imageBytes;

                if (request.Raw)
                {
                    imageBytes = www.downloadHandler.data;
                }
                else
                {
                    imageBytes = GetImageBytes(www);
                }

                ImageSize size = GetImageSizeFor(request.Sign);

                // Verify that we have image size data for the targeted sign.
                if (size == null)
                {
                    // No data was found, show a message to the player and print a detailed message to the server console and attempt to start the next download.
                    signArtist.SendMessage(request.Sender, "ErrorOccurred");
                    signArtist.PrintWarning($"Couldn't find the required image size for {request.Sign.PrefabName}, please report this in the plugin's thread.");
                    StartNextDownload(true);
                    www.Dispose();
                    yield break;
                }

                RotateFlipType rotation = RotateFlipType.RotateNoneFlipNone;
                if (request.Hor)
                {
                    rotation = RotateFlipType.RotateNoneFlipX;
                }

                object rotateObj = Interface.Call("GetImageRotation", request.Sign.Entity);
                if (rotateObj is RotateFlipType)
                {
                    rotation = (RotateFlipType)rotateObj;
                }

                // Get the bytes array for the resized image for the targeted sign.
                byte[] resizedImageBytes = imageBytes.ResizeImage(size.Width, size.Height, size.ImageWidth, size.ImageHeight, signArtist.Settings.EnforceJpeg && !request.Raw, rotation);
                // Verify that the resized file doesn't exceed the maximum configured filesize.
                if (resizedImageBytes.Length > signArtist.Settings.MaxFileSizeInBytes)
                {
                    // The file is too large, show a message to the player and attempt to start the next download.
                    signArtist.SendMessage(request.Sender, "FileTooLarge", signArtist.Settings.MaxSize);
                    www.Dispose();
                    StartNextDownload(true);

                    yield break;
                }

                // Check if the sign already has a texture assigned to it.
                if (request.Sign.TextureId() > 0)
                {
                    // A texture was already assigned, remove this file to make room for the new one.
                    FileStorage.server.Remove(request.Sign.TextureId(), FileStorage.Type.png, request.Sign.NetId);
                }

                // Create the image on the filestorage and send out a network update for the sign.
                request.Sign.SetImage(FileStorage.server.Store(resizedImageBytes, FileStorage.Type.png, request.Sign.NetId));
                request.Sign.SendNetworkUpdate();

                // Notify the player that the image was loaded.
                signArtist.SendMessage(request.Sender, "ImageLoaded");

                // Call the Oxide hook 'OnSignUpdated' to notify other plugins of the update event.
                Interface.Oxide.CallHook("OnSignUpdated", request.Sign, request.Sender);

                if (request.Sender != null)
                {
                    // Check if logging to console is enabled.
                    if (signArtist.Settings.ConsoleLogging)
                    {
                        // Console logging is enabled, show a message in the server console.
                        signArtist.Puts(signArtist.GetTranslation("LogEntry"), request.Sender.displayName,
                            request.Sender.userID, request.Sign.TextureId(), request.Sign.ShortPrefabName, request.Url);
                    }

                    // Check if logging to file is enabled.
                    if (signArtist.Settings.FileLogging)
                    {
                        // File logging is enabled, add an entry to the logfile.
                        signArtist.LogToFile("log",
                            string.Format(signArtist.GetTranslation("LogEntry"), request.Sender.displayName,
                                request.Sender.userID, request.Sign.TextureId(), request.Sign.ShortPrefabName,
                                request.Url), signArtist);
                    }

                    if (signArtist.Settings.Discordlogging)
                    {
                        // Discord logging is enabled, add an entry to the logfile.
                        StartCoroutine(LogToDiscord(request));
                    }
                }
                // Attempt to start the next download.
                StartNextDownload(true);
                www.Dispose();

            }

            private IEnumerator LogToDiscord(DownloadRequest request)
            {
                BasePlayer player = request.Sender;
                IPaintableEntity sign = request.Sign;
                var msg = DiscordMessage(ConVar.Server.hostname, player.displayName, player.UserIDString, sign.ShortPrefabName, request.Url, sign.Entity.transform.position.ToString());
                string jsonmsg = JsonConvert.SerializeObject(msg);
                UnityWebRequest wwwpost = new UnityWebRequest(signArtist.Settings.DiscordWebhook, "POST");
                byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonmsg.ToString());
                wwwpost.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
                wwwpost.SetRequestHeader("Content-Type", "application/json");
                yield return wwwpost.SendWebRequest();

                if (wwwpost.isNetworkError || wwwpost.isHttpError)
                {
                    signArtist.PrintError(wwwpost.error);
                    signArtist.PrintError(jsonmsg);
                    yield break;
                }
                wwwpost.Dispose();
            }

            private Message DiscordMessage(string servername, string playername, string userid, string itemname, string imgurl, string location)
            {
                string steamprofile = "https://steamcommunity.com/profiles/" + userid;
                var fields = new List<Message.Fields>()
                {
                    new Message.Fields("Player: " + playername, $"[{userid}]({steamprofile})", true),
                    new Message.Fields("Entity", itemname, true),
                    new Message.Fields("Image Url", imgurl, false),
                    new Message.Fields("Teleport position", "teleportpos " + location.Replace(" ", string.Empty), false)
                };
                var footer = new Message.Footer($"Logged @{DateTime.UtcNow:dd/MM/yy HH:mm:ss}");
                var image = new Message.Image(imgurl);
                var embeds = new List<Message.Embeds>()
                {
                    new Message.Embeds("Server - " + servername, "A sign has been updated" , fields, footer, image)
                };
                Message msg = new Message(signArtist.Settings.DiscordUsername, signArtist.Settings.AvatarUrl, embeds);
                return msg;
            }


            /// <summary>
            /// Restores the image and adds it to the sign again.
            /// </summary>
            /// <param name="request">The requested <see cref="RestoreRequest"/> instance. </param>
            /// <returns></returns>
            private IEnumerator RestoreImage(RestoreRequest request)
            {
                // Verify that there is a valid reference to the plugin from this class.
                if (signArtist == null)
                {
                    throw new NullReferenceException("signArtist");
                }

                byte[] imageBytes;

                // Check if the sign already has a texture assigned to it.
                if (request.Sign.TextureId() == 0)
                {
                    // No texture was previously assigned, show a message to the player.
                    signArtist.SendMessage(request.Sender, "RestoreErrorOccurred");
                    StartNextRestore(true);

                    yield break;
                }

                // Cache the byte array of the currently stored file.
                imageBytes = FileStorage.server.Get(request.Sign.TextureId(), FileStorage.Type.png, request.Sign.NetId);
                ImageSize size = GetImageSizeFor(request.Sign);

                // Verify that we have image size data for the targeted sign.
                if (size == null)
                {
                    // No data was found, show a message to the player and print a detailed message to the server console and attempt to start the next download.
                    signArtist.SendMessage(request.Sender, "ErrorOccurred");
                    signArtist.PrintWarning($"Couldn't find the required image size for {request.Sign.PrefabName}, please report this in the plugin's thread.");
                    StartNextRestore(true);

                    yield break;
                }

                // Remove the texture from the FileStorage.
                FileStorage.server.Remove(request.Sign.TextureId(), FileStorage.Type.png, request.Sign.NetId);

                // Get the bytes array for the resized image for the targeted sign.
                byte[] resizedImageBytes = imageBytes.ResizeImage(size.Width, size.Height, size.ImageWidth, size.ImageHeight, signArtist.Settings.EnforceJpeg && !request.Raw);

                // Create the image on the filestorage and send out a network update for the sign.
                request.Sign.SetImage(FileStorage.server.Store(resizedImageBytes, FileStorage.Type.png, request.Sign.NetId));
                request.Sign.SendNetworkUpdate();

                // Notify the player that the image was loaded.
                signArtist.SendMessage(request.Sender, "ImageRestored");

                // Call the Oxide hook 'OnSignUpdated' to notify other plugins of the update event.
                Interface.Oxide.CallHook("OnSignUpdated", request.Sign, request.Sender);

                // Attempt to start the next download.
                StartNextRestore(true);
            }

            /// <summary>
            /// Gets the target image size for a <see cref="Signage"/>.
            /// </summary>
            /// <param name="signage"></param>
            private ImageSize GetImageSizeFor(IPaintableEntity signage)
            {
                if (signArtist.ImageSizePerAsset.ContainsKey(signage.ShortPrefabName))
                {
                    return signArtist.ImageSizePerAsset[signage.ShortPrefabName];
                }

                return null;
            }

            /// <summary>
            /// Converts the <see cref="Texture2D"/> from the webrequest to a <see cref="byte"/> array.
            /// </summary>
            /// <param name="www">The completed webrequest. </param>
            private byte[] GetImageBytes(UnityWebRequest www)
            {
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(www.downloadHandler.data);

                byte[] image;

                if (texture.format == TextureFormat.ARGB32 && !signArtist.Settings.EnforceJpeg)
                {
                    image = texture.EncodeToPNG();
                }
                else
                {
                    image = texture.EncodeToJPG(signArtist.Settings.Quality);
                }

                DestroyImmediate(texture);

                return image;
            }
        }

        #endregion Image Download Behaviour
        private interface IBasePaintableEntity
        {
            BaseEntity Entity { get; }
            string PrefabName { get; }
            string ShortPrefabName { get; }
            uint NetId { get; }
            void SendNetworkUpdate();
        }

        private interface IPaintableEntity : IBasePaintableEntity
        {
            void SetImage(uint id);
            bool CanUpdate(BasePlayer player);
            uint TextureId();
        }

        private class BasePaintableEntity : IBasePaintableEntity
        {
            public BaseEntity Entity { get; }
            public string PrefabName { get; }
            public string ShortPrefabName { get; }
            public uint NetId { get; }

            protected BasePaintableEntity(BaseEntity entity)
            {
                Entity = entity;
                PrefabName = Entity.PrefabName;
                ShortPrefabName = Entity.ShortPrefabName;
                NetId = Entity.net.ID;
            }

            public void SendNetworkUpdate()
            {
                Entity.SendNetworkUpdate();
            }
        }

        private class PaintableSignage : BasePaintableEntity, IPaintableEntity
        {
            public Signage Sign { get; set; }

            public PaintableSignage(Signage sign) : base(sign)
            {
                Sign = sign;
            }

            public void SetImage(uint id)
            {
                Sign.textureIDs = new uint[] { id };
            }

            public bool CanUpdate(BasePlayer player)
            {
                return Sign.CanUpdateSign(player);
            }

            public uint TextureId()
            {
                return Sign.textureIDs.First();
            }
        }

        private class PaintableFrame : BasePaintableEntity, IPaintableEntity
        {
            public PhotoFrame Sign { get; set; }

            public PaintableFrame(PhotoFrame sign) : base(sign)
            {
                Sign = sign;
            }

            public void SetImage(uint id)
            {
                Sign._overlayTextureCrc = id;
            }

            public bool CanUpdate(BasePlayer player)
            {
                return Sign.CanUpdateSign(player);
            }

            public uint TextureId()
            {
                return Sign._overlayTextureCrc;
            }
        }


        #region Init
        /// <summary>
        /// Oxide hook that is triggered when the plugin is loaded.
        /// </summary>
        /// 
        private void Init()
        {
            // Register all the permissions used by the plugin
            permission.RegisterPermission("signartist.file", this);
            permission.RegisterPermission("signartist.ignorecd", this);
            permission.RegisterPermission("signartist.ignoreowner", this);
            permission.RegisterPermission("signartist.raw", this);
            permission.RegisterPermission("signartist.restore", this);
            permission.RegisterPermission("signartist.restoreall", this);
            permission.RegisterPermission("signartist.text", this);
            permission.RegisterPermission("signartist.url", this);

            AddCovalenceCommand("sil", "SilCommand");
            AddCovalenceCommand("silt", "SiltCommand");
            AddCovalenceCommand("sili", "SilItemCommand");
            AddCovalenceCommand("silrestore", "RestoreCommand");

            // Initialize the dictionary with all paintable object assets and their target sizes
            ImageSizePerAsset = new Dictionary<string, ImageSize>
            {
                // Picture Frames
                ["sign.pictureframe.landscape"] = new ImageSize(256, 128), // Landscape Picture Frame
                ["sign.pictureframe.portrait"] = new ImageSize(128, 256),  // Portrait Picture Frame
                ["sign.pictureframe.tall"] = new ImageSize(128, 512),      // Tall Picture Frame
                ["sign.pictureframe.xl"] = new ImageSize(512, 512),        // XL Picture Frame
                ["sign.pictureframe.xxl"] = new ImageSize(1024, 512),      // XXL Picture Frame

                // Wooden Signs
                ["sign.small.wood"] = new ImageSize(128, 64),              // Small Wooden Sign
                ["sign.medium.wood"] = new ImageSize(256, 128),            // Wooden Sign
                ["sign.large.wood"] = new ImageSize(256, 128),             // Large Wooden Sign
                ["sign.huge.wood"] = new ImageSize(512, 128),              // Huge Wooden Sign

                // Banners
                ["sign.hanging.banner.large"] = new ImageSize(64, 256),    // Large Banner Hanging
                ["sign.pole.banner.large"] = new ImageSize(64, 256),       // Large Banner on Pole

                // Hanging Signs
                ["sign.hanging"] = new ImageSize(128, 256),                // Two Sided Hanging Sign
                ["sign.hanging.ornate"] = new ImageSize(256, 128),         // Two Sided Ornate Hanging Sign

                // Town Signs
                ["sign.post.single"] = new ImageSize(128, 64),             // Single Sign Post
                ["sign.post.double"] = new ImageSize(256, 256),            // Double Sign Post
                ["sign.post.town"] = new ImageSize(256, 128),              // One Sided Town Sign Post
                ["sign.post.town.roof"] = new ImageSize(256, 128),         // Two Sided Town Sign Post

                ["photoframe.large"] = new ImageSize(320, 240),
                ["photoframe.portrait"] = new ImageSize(320, 384),
                ["photoframe.landscape"] = new ImageSize(320, 240),


                // Other paintable assets
                ["spinner.wheel.deployed"] = new ImageSize(512, 512, 285, 285), // Spinning Wheel
            };
        }

        private void GetSteamworksImages()
        {
            foreach (InventoryDef item in Steamworks.SteamInventory.Definitions)
            {
                string shortname = item.GetProperty("itemshortname");
                if (item == null || string.IsNullOrEmpty(shortname))
                    continue;

                if (item.Id < 100)
                    continue;

                ulong workshopid;
                if (!ulong.TryParse(item.GetProperty("workshopid"), out workshopid))
                    continue;

                if (string.IsNullOrEmpty(item.IconUrl)) continue;
                SkiniconUrls[workshopid] = item.IconUrl;
            }
        }

        /// <summary>
        /// Oxide hook that is triggered to automatically load the configuration file.
        /// </summary>
        protected override void LoadConfig()
        {
            base.LoadConfig();
            Settings = Config.ReadObject<SignArtistConfig>();
            SaveConfig();
        }

        /// <summary>
        /// Oxide hook that is triggered to automatically load the default configuration file when no file exists.
        /// </summary>
        protected override void LoadDefaultConfig()
        {
            Settings = SignArtistConfig.DefaultConfig();
        }

        /// <summary>
        /// Oxide hook that is triggered to save the configuration file.
        /// </summary>
        protected override void SaveConfig()
        {
            Config.WriteObject(Settings);
        }

        /// <summary>
        /// Oxide hook that is triggered when the server has fully initialized.
        /// </summary>
        private void OnServerInitialized()
        {
            // Create a new GameObject and attach the UnityEngine script to it for handling the image downloads.
            imageDownloaderGameObject = new GameObject("ImageDownloader");
            imageDownloader = imageDownloaderGameObject.AddComponent<ImageDownloader>();
            if ((Steamworks.SteamInventory.Definitions?.Length ?? 0) == 0)
            {
                PrintWarning("Waiting for Steamworks to update item definitions....");
                Steamworks.SteamInventory.OnDefinitionsUpdated += GetSteamworksImages;
            }
            else GetSteamworksImages();
        }

        /// <summary>
        /// Oxide hook that is triggered when the plugin is unloaded.
        /// </summary>
        private void Unload()
        {
            // Destroy the created GameObject and cleanup.
            UnityEngine.Object.Destroy(imageDownloaderGameObject);
            imageDownloader = null;
            cooldowns = null;

            Steamworks.SteamInventory.OnDefinitionsUpdated -= GetSteamworksImages;
        }

        /// <summary>
        /// Handles the /sil command.
        /// </summary>
        /// <param name="iplayer">The player that has executed the command. </param>
        /// <param name="command">The name of the command that was executed. </param>
        /// <param name="args">All arguments that were passed with the command. </param>
        /// 
        #endregion Init

        #region Localization 
        /// <summary>
        /// Oxide hook that is triggered automatically after it has been loaded to initialize the messages for the Lang API.
        /// </summary>
        protected override void LoadDefaultMessages()
        {
            // Register all messages used by the plugin in the Lang API.
            lang.RegisterMessages(new Dictionary<string, string>
            {
                // Messages used throughout the plugin.
                ["WebErrorOccurred"] = "Failed to download the image! Error {0}.",
                ["FileTooLarge"] = "The file exceeds the maximum file size of {0}Mb.",
                ["ErrorOccurred"] = "An unknown error has occured, if this error keeps occuring please notify the server admin.",
                ["RestoreErrorOccurred"] = "Can't restore the sign because no texture is assigned to it.",
                ["DownloadQueued"] = "Your image was added to the download queue!",
                ["RestoreQueued"] = "Your sign was added to the restore queue!",
                ["RestoreBatchQueued"] = "You added all {0} signs to the restore queue!",
                ["ImageLoaded"] = "The image was succesfully loaded to the sign!",
                ["ImageRestored"] = "The image was succesfully restored for the sign!",
                ["LogEntry"] = "Player `{0}` (SteamId: {1}) loaded {2} into {3} from {4}",
                ["NoSignFound"] = "Unable to find a sign! Make sure you are looking at one and that you are not too far away from it.",
                ["Cooldown"] = "You can't use the command yet! Remaining cooldown: {0}.",
                ["SignNotOwned"] = "You can't change this sign as it is protected by a tool cupboard.",
                ["NoItemHeld"] = "You're not holding an item.",
                ["ActionQueuedAlready"] = "An action has already been queued for this sign, please wait for this action to complete.",
                ["SyntaxSilCommand"] = "Syntax error!\nSyntax: /sil <url> [raw]",
                ["SyntaxSiltCommand"] = "Syntax error!\nSyntax: /silt <message> [<fontsize:number>] [<color:hex value>] [<bgcolor:hex value>] [raw]",
                ["NoPermission"] = "You don't have permission to use this command.",
                ["NoPermissionFile"] = "You don't have permission to use images from the server's filesystem.",
                ["NoPermissionRaw"] = "You don't have permission to use raw images, loading normally instead.",
                ["NoPermissionRestoreAll"] = "You don't have permission to use restore all signs at once.",

                // Cooldown formatting 'translations'.
                ["day"] = "day",
                ["days"] = "days",
                ["hour"] = "hour",
                ["hours"] = "hours",
                ["minute"] = "minute",
                ["minutes"] = "minutes",
                ["second"] = "second",
                ["seconds"] = "seconds",
                ["and"] = "and"
            }, this);
        }
        #endregion Localization

        #region Commands
        [Command("sil"), Permission("signartist.url")]
        private void SilCommand(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            // Verify if the correct syntax is used.
            if (args.Length < 1)
            {
                // Invalid syntax was used, show an error message to the player.
                SendMessage(player, "SyntaxSilCommand");

                return;
            }

            // Verify if the player has permission to use this command.
            if (!HasPermission(player, "signartist.url"))
            {
                // The player doesn't have permission to use this command, show an error message.
                SendMessage(player, "NoPermission");

                return;
            }

            // Verify that the command isn't on cooldown for the user.
            if (HasCooldown(player))
            {
                // The command is still on cooldown for the player, show an error message.
                SendMessage(player, "Cooldown", FormatCooldown(GetCooldown(player)));

                return;
            }

            // Check if the player is looking at a sign.
            IPaintableEntity sign;
            if (!IsLookingAtSign(player, out sign))
            {
                // The player isn't looking at a sign or is too far away from it, show an error message.
                SendMessage(player, "NoSignFound");

                return;
            }

            // Check if the player is able to update the sign.
            if (!CanChangeSign(player, sign))
            {
                // The player isn't able to update the sign, show an error message.
                SendMessage(player, "SignNotOwned");

                return;
            }

            // Check if the player wants to add the image from the server's filesystem and has the permission to do so.
            if (args[0].StartsWith("file://") && !HasPermission(player, "signartist.file"))
            {
                // The player doesn't have permission for this, show an error message.
                SendMessage(player, "NoPermissionFile");

                return;
            }

            // Check if the player wants to add the image as a raw image and has the permission to do so.
            bool raw = args.Length > 1 && args[1].Equals("raw", StringComparison.OrdinalIgnoreCase);
            if (raw && !HasPermission(player, "signartist.raw"))
            {
                // The player doesn't have permission for this, show a message and disable raw.
                SendMessage(player, "NoPermissionRaw");
                raw = false;
            }

            // This sign pastes in reverse, so we'll check and set a var to flip it
            bool hor = sign.ShortPrefabName == "sign.hanging";

            // Notify the player that it is added to the queue.
            SendMessage(player, "DownloadQueued");

            // Queue the download of the specified image.
            imageDownloader.QueueDownload(args[0], player, sign, raw, hor);

            // Call external hook
            Interface.Oxide.CallHook("OnImagePost", player, args[0]);

            // Set the cooldown on the command for the player if the cooldown setting is enabled.
            SetCooldown(player);
        }

        [Command("sili"), Permission("signartist.url")]
        private void SilItemCommand(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (!HasPermission(player, "signartist.url"))
            {
                SendMessage(player, "NoPermission");
                return;
            }

            if (HasCooldown(player))
            {
                SendMessage(player, "Cooldown", FormatCooldown(GetCooldown(player)));
                return;
            }

            IPaintableEntity sign;
            if (!IsLookingAtSign(player, out sign))
            {
                SendMessage(player, "NoSignFound");
                return;
            }

            if (!CanChangeSign(player, sign))
            {
                SendMessage(player, "SignNotOwned");
                return;
            }

            Item held = player.GetActiveItem();
            if (held == null)
            {
                SendMessage(player, "NoItemHeld");
                return;
            }

            string shortname = held.info.shortname;

            bool hor = sign.ShortPrefabName == "sign.hanging";

            SendMessage(player, "DownloadQueued");
            bool defaultskin = false;
            if (args.Length == 1 && args[0] == "default") defaultskin = true;
            if (held.skin != 0uL && !defaultskin)
            {
                string url;
                if (SkiniconUrls.TryGetValue(held.skin, out url))
                {
                    shortname = url;
                }
                else
                {
                    ServerMgr.Instance.StartCoroutine(DownloadWorkshopskin(held, sign, hor));
                    return;
                }
            }

            imageDownloader.QueueDownload(shortname, player, sign, false, hor);

            Interface.Oxide.CallHook("OnImagePost", player, shortname);

            SetCooldown(player);
        }

        private const string FindWorkshopSkinUrl = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";

        private IEnumerator DownloadWorkshopskin(Item held, IPaintableEntity sign, bool hor)
        {
            BasePlayer player = held.GetOwnerPlayer();
            WWWForm form = new WWWForm();
            form.AddField("itemcount", "1");
            form.AddField("publishedfileids[0]", held.skin.ToString());
            UnityWebRequest www = UnityWebRequest.Post(FindWorkshopSkinUrl, form);
            yield return www.SendWebRequest();
            string url = "";
            // Verify that the webrequest was succesful.
            if (www.isNetworkError || www.isHttpError)
            {
                // The webrequest wasn't succesful, show a message to the player and attempt to start the next download.
                PrintError(www.error.ToString());
                url = held.info.shortname;
            }
            var json = JsonConvert.DeserializeObject<GetPublishedFileDetailsClass>(www.downloadHandler.text);
            url = json.response.publishedfiledetails[0].preview_url;
            imageDownloader.QueueDownload(url, player, sign, false, hor);

            Interface.Oxide.CallHook("OnImagePost", player, held.info.shortname);

            SetCooldown(player);
        }

        /// <summary>
        /// Handles the /silt command
        /// </summary>
        /// <param name="iplayer">The player that has executed the command. </param>
        /// <param name="command">The name of the command that was executed. </param>
        /// <param name="args">All arguments that were passed with the command. </param>
        [Command("silt"), Permission("signartist.text")]
        private void SiltCommand(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            // Verify if the correct syntax is used.
            if (args.Length < 1)
            {
                // Invalid syntax was used, show an error message to the player.
                SendMessage(player, "SyntaxSiltCommand");

                return;
            }

            // Verify if the player has permission to use this command.
            if (!HasPermission(player, "signartist.text"))
            {
                // The player doesn't have permission to use this command, show an error message.
                SendMessage(player, "NoPermission");

                return;
            }

            // Verify that the command isn't on cooldown for the user.
            if (HasCooldown(player))
            {
                // The command is still on cooldown for the player, show an error message.
                SendMessage(player, "Cooldown", FormatCooldown(GetCooldown(player)));

                return;
            }

            // Check if the player is looking at a sign.
            IPaintableEntity sign;
            if (!IsLookingAtSign(player, out sign))
            {
                // The player isn't looking at a sign or is too far away from it, show an error message.
                SendMessage(player, "NoSignFound");

                return;
            }

            // Check if the player is able to update the sign.
            if (!CanChangeSign(player, sign))
            {
                // The player isn't able to update the sign, show an error message.
                SendMessage(player, "SignNotOwned");

                return;
            }

            // Build the URL for the /silt command
            string message = args[0].EscapeForUrl();
            int fontsize = 80;
            string color = "000";
            string bgcolor = "0FFF";
            string format = "png32";

            // Replace the default fontsize if the player specified one.
            if (args.Length > 1)
            {
                int.TryParse(args[1], out fontsize);
            }

            // Replace the default color if the player specified one.
            if (args.Length > 2)
            {
                color = args[2].Trim(' ', '#');
            }

            // Replace the default color if the player specified one.
            if (args.Length > 3)
            {
                bgcolor = args[3].Trim(' ', '#');
            }

            // Check if the player wants to add the image as a raw image and has the permission to do so.
            bool raw = args.Length > 4 && args[4].Equals("raw", StringComparison.OrdinalIgnoreCase);
            if (raw && !HasPermission(player, "signartist.raw"))
            {
                // The player doesn't have permission for this, show a message and disable raw.
                SendMessage(player, "NoPermissionRaw");
                raw = false;
            }

            // Correct the format if required
            if (Settings.EnforceJpeg)
            {
                format = "jpg";
            }

            // Get the size for the image
            ImageSize size = null;
            if (ImageSizePerAsset.ContainsKey(sign.ShortPrefabName))
            {
                size = ImageSizePerAsset[sign.ShortPrefabName];
            }

            // Verify that we have image size data for the targeted sign.
            if (size == null)
            {
                // No data was found, show a message to the player and print a detailed message to the server console and attempt to start the next download.
                SendMessage(player, "ErrorOccurred");
                PrintWarning($"Couldn't find the required image size for {sign.PrefabName}, please report this in the plugin's thread.");

                return;
            }

            // Combine all the values into the url;
            string url = $"http://assets.imgix.net/~text?fm={format}&txtalign=middle,center&txtsize={fontsize}&txt={message}&w={size.ImageWidth}&h={size.ImageHeight}&txtclr={color}&bg={bgcolor}";

            // Notify the player that it is added to the queue.
            SendMessage(player, "DownloadQueued");

            // This sign pastes in reverse, so we'll check and set a var to flip it
            bool hor = sign.ShortPrefabName == "sign.hanging";

            // Queue the download of the specified image.
            imageDownloader.QueueDownload(url, player, sign, raw, hor);

            // Call external hook
            Interface.Oxide.CallHook("OnImagePost", player, url);

            // Set the cooldown on the command for the player if the cooldown setting is enabled.
            SetCooldown(player);
        }

        /// <summary>
        /// Handles the /silrestore command
        /// </summary>
        /// <param name="iplayer">The player that has executed the command. </param>
        /// <param name="command">The name of the command that was executed. </param>
        /// <param name="args">All arguments that were passed with the command. </param>
        [Command("silrestore"), Permission("signartist.raw")]
        private void RestoreCommand(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            // Verify if the player has permission to use this command.
            if (!HasPermission(player, "signartist.restore"))
            {
                // The player doesn't have permission to use this command, show an error message.
                SendMessage(player, "NoPermission");

                return;
            }

            // Check if the user wants to restore the sign or signs as raw images and has the permission to do so
            bool raw = string.IsNullOrEmpty(args.FirstOrDefault(s => s.Equals("raw", StringComparison.OrdinalIgnoreCase)));
            if (raw && !HasPermission(player, "signartist.raw"))
            {
                // The player doesn't have permission for this, show a message and disable raw.
                SendMessage(player, "NoPermissionRaw");
                raw = false;
            }

            // Check if the user wants to restore all signs and has the permission to do so.
            bool all = args.Any(s => s.Equals("all", StringComparison.OrdinalIgnoreCase));
            if (all && !HasPermission(player, "signartist.restoreall"))
            {
                // The player doesn't have permission for this, show a message and disable raw.
                SendMessage(player, "NoPermissionRestoreAll");

                return;
            }

            // Check if the player is looking at a sign if not all signs should be restored.
            if (!all)
            {
                IPaintableEntity sign;
                if (!IsLookingAtSign(player, out sign))
                {
                    // The player isn't looking at a sign or is too far away from it, show an error message.
                    SendMessage(player, "NoSignFound");

                    return;
                }

                // Notify the player that it is added to the queue.
                SendMessage(player, "RestoreQueued");

                // Queue the restore of the image on the specified sign.
                imageDownloader.QueueRestore(player, sign, raw);

                return;
            }

            // The player wants to restore all signs.
            Signage[] allSigns = UnityEngine.Object.FindObjectsOfType<Signage>();

            // Notify the player that they were added to the queue
            SendMessage(player, "RestoreBatchQueued", allSigns.Length);

            // Queue every sign to be restored.
            foreach (Signage sign in allSigns)
            {
                imageDownloader.QueueRestore(player, new PaintableSignage(sign), raw);
            }
        }

        #endregion Commands

        #region Methods
        /// <summary>
        /// Check if the given <see cref="BasePlayer"/> is able to use the command.
        /// </summary>
        /// <param name="player">The player to check. </param>
        private bool HasCooldown(BasePlayer player)
        {
            // Check if cooldown is enabled.
            if (Settings.Cooldown <= 0)
            {
                return false;
            }

            // Check if cooldown is ignored for the player.
            if (HasPermission(player, "signartist.ignorecd"))
            {
                return false;
            }

            // Make sure there is an entry for the player in the dictionary.
            if (!cooldowns.ContainsKey(player.userID))
            {
                cooldowns.Add(player.userID, 0);
            }

            // Check if the command is on cooldown or not.
            return Time.realtimeSinceStartup - cooldowns[player.userID] < Settings.Cooldown;
        }

        /// <summary>
        /// Returns the cooldown in seconds for the given <see cref="BasePlayer"/>.
        /// </summary>
        /// <param name="player">The player to obtain the cooldown of. </param>
        private float GetCooldown(BasePlayer player)
        {
            return Settings.Cooldown - (Time.realtimeSinceStartup - cooldowns[player.userID]);
        }

        /// <summary>
        /// Sets the last use for the cooldown handling of the command for the given <see cref="BasePlayer"/>.
        /// </summary>
        /// <param name="player">The player to put the command on cooldown for. </param>
        private void SetCooldown(BasePlayer player)
        {
            // Check if cooldown is enabled.
            if (Settings.Cooldown <= 0)
            {
                return;
            }

            // Check if cooldown is ignored for the player.
            if (HasPermission(player, "signartist.ignorecd"))
            {
                return;
            }

            // Make sure there is an entry for the player in the dictionary.
            if (!cooldowns.ContainsKey(player.userID))
            {
                cooldowns.Add(player.userID, 0);
            }

            // Set the last use
            cooldowns[player.userID] = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// Returns a formatted string for the given cooldown.
        /// </summary>
        /// <param name="seconds">The cooldown in seconds. </param>
        private string FormatCooldown(float seconds)
        {
            // Create a new TimeSpan from the remaining cooldown.
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            List<string> output = new List<string>();

            // Check if it is more than a single day and add it to the result.
            if (t.Days >= 1)
            {
                output.Add($"{t.Days} {(t.Days > 1 ? "days" : "day")}");
            }

            // Check if it is more than an hour and add it to the result.
            if (t.Hours >= 1)
            {
                output.Add($"{t.Hours} {(t.Hours > 1 ? "hours" : "hour")}");
            }

            // Check if it is more than a minute and add it to the result.
            if (t.Minutes >= 1)
            {
                output.Add($"{t.Minutes} {(t.Minutes > 1 ? "minutes" : "minute")}");
            }

            // Check if there is more than a second and add it to the result.
            if (t.Seconds >= 1)
            {
                output.Add($"{t.Seconds} {(t.Seconds > 1 ? "seconds" : "second")}");
            }

            // Format the result and return it.
            return output.Count >= 3 ? output.ToSentence().Replace(" and", ", and") : output.ToSentence();
        }

        /// <summary>
        /// Checks if the <see cref="BasePlayer"/> is looking at a valid <see cref="Signage"/> object.
        /// </summary>
        /// <param name="player">The player to check. </param>
        /// <param name="sign">When this method returns, contains the <see cref="Signage"/> the player contained in <paramref name="player" /> is looking at, or null if the player isn't looking at a sign. </param>
        private bool IsLookingAtSign(BasePlayer player, out IPaintableEntity sign)
        {
            RaycastHit hit;
            sign = null;

            // Get the object that is in front of the player within the maximum distance set in the config.
            //if (Physics.Raycast(player.eyes.HeadRay(), out hit))//, Settings.MaxDistance))
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, Settings.MaxDistance))
            {
                // Attempt to grab the Signage entity, if there is none this will set the sign to null,
                // otherwise this will set it to the sign the player is looking at.
                BaseEntity entity = hit.GetEntity();
                if (entity is Signage)
                {
                    sign = new PaintableSignage(entity as Signage);
                }
                else if (entity is PhotoFrame)
                {
                    sign = new PaintableFrame(entity as PhotoFrame);
                }
            }

            // Return true or false depending on if we found a sign.
            return sign != null;
        }

        /// <summary>
        /// Checks if the <see cref="BasePlayer"/> is allowed to change the drawing on the <see cref="Signage"/> object.
        /// </summary>
        /// <param name="player">The player to check. </param>
        /// <param name="sign">The sign to check. </param>
        /// <returns></returns>
        private bool CanChangeSign(BasePlayer player, IPaintableEntity sign)
        {
            return sign.CanUpdate(player) || HasPermission(player, "signartist.ignoreowner");
        }

        /// <summary>
        /// Checks if the given <see cref="BasePlayer"/> has the specified permission.
        /// </summary>
        /// <param name="player">The player to check a permission on. </param>
        /// <param name="perm">The permission to check for. </param>
        private bool HasPermission(BasePlayer player, string perm)
        {
            return permission.UserHasPermission(player.UserIDString, perm);
        }

        /// <summary>
        /// Send a formatted message to a single player.
        /// </summary>
        /// <param name="player">The player to send the message to. </param>
        /// <param name="key">The key of the message from the Lang API to get the message for. </param>
        /// <param name="args">Any amount of arguments to add to the message. </param>
        private void SendMessage(BasePlayer player, string key, params object[] args)
        {
            if (player == null) return;
            player.ChatMessage(string.Format(GetTranslation(key, player), args));
        }

        /// <summary>
        /// Gets the message for a specific player from the Lang API.
        /// </summary>
        /// <param name="key">The key of the message from the Lang API to get the message for. </param>
        /// <param name="player">The player to get the message for. </param>
        /// <returns></returns>
        private string GetTranslation(string key, BasePlayer player = null)
        {
            return lang.GetMessage(key, this, player?.UserIDString);
        }
        #endregion Methods

        #region Steam Workshop API Class

        public class GetPublishedFileDetailsClass
        {
            public Response response { get; set; }
        }

        public class Response
        {
            public int result { get; set; }
            public int resultcount { get; set; }
            public Publishedfiledetail[] publishedfiledetails { get; set; }
        }

        public class Publishedfiledetail
        {
            public string publishedfileid { get; set; }
            public int result { get; set; }
            public string creator { get; set; }
            public int creator_app_id { get; set; }
            public int consumer_app_id { get; set; }
            public string filename { get; set; }
            public int file_size { get; set; }
            public string preview_url { get; set; }
            public string hcontent_preview { get; set; }
            public string title { get; set; }
            public string description { get; set; }
            public int time_created { get; set; }
            public int time_updated { get; set; }
            public int visibility { get; set; }
            public int banned { get; set; }
            public string ban_reason { get; set; }
            public int subscriptions { get; set; }
            public int favorited { get; set; }
            public int lifetime_subscriptions { get; set; }
            public int lifetime_favorited { get; set; }
            public int views { get; set; }
            public Tag[] tags { get; set; }
        }

        public class Tag
        {
            public string tag { get; set; }
        }

        #endregion Steam Workshop API Class

        #region Discord Class
        public class Message
        {
            public string username { get; set; }
            public string avatar_url { get; set; }
            public List<Embeds> embeds { get; set; }

            public class Fields
            {
                public string name { get; set; }
                public string value { get; set; }
                public bool inline { get; set; }
                public Fields(string name, string value, bool inline)
                {
                    this.name = name;
                    this.value = value;
                    this.inline = inline;
                }
            }

            public class Footer
            {
                public string text { get; set; }
                public Footer(string text)
                {
                    this.text = text;
                }
            }

            public class Image
            {
                public string url { get; set; }
                public Image(string url)
                {
                    this.url = url;
                }
            }

            public class Embeds
            {
                public string title { get; set; }
                public string description { get; set; }
                public Image image { get; set; }
                public List<Fields> fields { get; set; }
                public Footer footer { get; set; }
                public Embeds(string title, string description, List<Fields> fields, Footer footer, Image image)
                {
                    this.title = title;
                    this.description = description;
                    this.image = image;
                    this.fields = fields;
                    this.footer = footer;
                }
            }

            public Message(string username, string avatar_url, List<Embeds> embeds)
            {
                this.username = username;
                this.avatar_url = avatar_url;
                this.embeds = embeds;
            }
        }

        #endregion

        #region Public Helpers
        // This can be Call(ed) by other plugins to put text on a sign
        public void API_SignText(BasePlayer player, Signage sign, string message, int fontsize = 30, string color = "FFFFFF", string bgcolor = "000000")
        {
            //Puts($"signText called with {message}");
            string format = "png32";

            ImageSize size = null;
            if (ImageSizePerAsset.ContainsKey(sign.ShortPrefabName))
            {
                size = ImageSizePerAsset[sign.ShortPrefabName];
            }

            // Combine all the values into the url;
            string url = $"http://assets.imgix.net/~text?fm={format}&txtalign=middle,center&txtsize={fontsize}&txt={message}&w={size.ImageWidth}&h={size.ImageHeight}&txtclr={color}&bg={bgcolor}";
            imageDownloader.QueueDownload(url, player, new PaintableSignage(sign), false);
        }

        public void API_SkinSign(BasePlayer player, Signage sign, string url, bool raw = false)
        {
            if (sign == null)
            {
                PrintWarning("Signage is null in API call");
                return;
            }

            if (string.IsNullOrEmpty(url))
            {
                PrintWarning("Url is empty in API call");
                return;
            }

            // This sign pastes in reverse, so we'll check and set a var to flip it
            bool hor = sign.ShortPrefabName == "sign.hanging" ? true : false;

            // Queue the download of the specified image.
            imageDownloader.QueueDownload(url, player, new PaintableSignage(sign), raw, hor);
        }


        //TODO add image byte[] api 
        #endregion

    }

    namespace SignArtistClasses
    {
        /// <summary>
        /// Extension class with extension methods used by the <see cref="SignArtist"/> plugin.
        /// </summary>
        public static class Extensions
        {
            /// <summary>
            /// Resizes an image from the <see cref="byte"/> array to a new image with a specific width and height.
            /// </summary>
            /// <param name="bytes">Source image. </param>
            /// <param name="width">New image canvas width. </param>
            /// <param name="height">New image canvas height. </param>
            /// <param name="targetWidth">New image width. </param>
            /// <param name="targetHeight">New image height. </param>
            /// <param name="enforceJpeg"><see cref="bool"/> value, true to save the images as JPG, false for PNG. </param>
            /// <param name="rotation"></param>
            public static byte[] ResizeImage(this byte[] bytes, int width, int height, int targetWidth, int targetHeight, bool enforceJpeg, RotateFlipType rotation = RotateFlipType.RotateNoneFlipNone)
            {
                byte[] resizedImageBytes;

                using (MemoryStream originalBytesStream = new MemoryStream(), resizedBytesStream = new MemoryStream())
                {
                    // Write the downloaded image bytes array to the memorystream and create a new Bitmap from it.
                    originalBytesStream.Write(bytes, 0, bytes.Length);
                    Bitmap image = new Bitmap(originalBytesStream);

                    if (rotation != RotateFlipType.RotateNoneFlipNone)
                    {
                        image.RotateFlip(rotation);
                    }

                    // Check if the width and height match, if they don't we will have to resize this image.
                    if (image.Width != targetWidth || image.Height != targetHeight)
                    {
                        // Create a new Bitmap with the target size.
                        Bitmap resizedImage = new Bitmap(width, height);

                        // Draw the original image onto the new image and resize it accordingly.
                        using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(resizedImage))
                        {
                            graphics.DrawImage(image, new Rectangle(0, 0, targetWidth, targetHeight));
                        }

                        TimestampImage(resizedImage);

                        // Save the bitmap to a MemoryStream as either Jpeg or Png.
                        if (enforceJpeg)
                        {
                            resizedImage.Save(resizedBytesStream, ImageFormat.Jpeg);
                        }
                        else
                        {
                            resizedImage.Save(resizedBytesStream, ImageFormat.Png);
                        }

                        // Grab the bytes array from the new image's MemoryStream and dispose of the resized image Bitmap.
                        resizedImageBytes = resizedBytesStream.ToArray();
                        resizedImage.Dispose();
                    }
                    else
                    {
                        TimestampImage(image);
                        // The image has the correct size so we can just return the original bytes without doing any resizing.
                        resizedImageBytes = bytes;
                    }

                    // Dispose of the original image Bitmap.
                    image.Dispose();
                }

                // Return the bytes array.
                return resizedImageBytes;
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

                destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

                using (Graphics graphics = System.Drawing.Graphics.FromImage(destImage))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    using (ImageAttributes wrapMode = new ImageAttributes())
                    {
                        wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                        graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                    }
                }

                return destImage;
            }

            private static void TimestampImage(Bitmap image)
            {
                //Rust images are crc and if we have the same image it is deleted from the file storage
                //Here we changed the last few pixels of the image with colors based off the current milliseconds since wipe
                //This will generate a unique image every time and allow us to use the same image multiple times
                Color pixel = Color.FromArgb(UnityEngine.Random.Range(0, 256), UnityEngine.Random.Range(0, 256), UnityEngine.Random.Range(0, 256), UnityEngine.Random.Range(0, 256));
                image.SetPixel(image.Width - 1, image.Height - 1, pixel);
            }

            private static int GetValueAtIndex(byte[] bytes, int index)
            {

                if (index >= bytes.Length)
                {
                    return 0;
                }

                return Convert.ToInt32(bytes[index]);

            }

            /// <summary>
            /// Converts a string to its escaped representation for the image placeholder text value.
            /// </summary>
            /// <param name="stringToEscape">The string to escape.</param>
            public static string EscapeForUrl(this string stringToEscape)
            {
                // Escape initial values.
                stringToEscape = Uri.EscapeDataString(stringToEscape);

                // Convert \r\n, \r and \n into linebreaks.
                stringToEscape = stringToEscape.Replace("%5Cr%5Cn", "%5Cn").Replace("%5Cr", "%5Cn").Replace("%5Cn", "%0A");

                // Return the converted message
                return stringToEscape;
            }
        }
    }
}
