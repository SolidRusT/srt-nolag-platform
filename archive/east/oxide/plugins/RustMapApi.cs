using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using Facepunch.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;
using UnityEngine.Networking;
using Color = UnityEngine.Color;

namespace Oxide.Plugins
{
    [Info("Rust Map Api", "MJSU", "1.3.2")]
    [Description("An API to generate the rust server map image")]
    internal class RustMapApi : RustPlugin
    {
        #region Class Fields

        [PluginReference] private Plugin ImgurApi;
        
        private PluginConfig _pluginConfig; //Plugin Config
        private StoredData _storedData;
        
        private TerrainTexturing _terrainTexture;
        private Terrain _terrain;
        private TerrainHeightMap _heightMap;
        private TerrainSplatMap _splatMap;
        
        private readonly Hash<string, RenderInfo> _renders = new Hash<string, RenderInfo>();
        private readonly Hash<string, Hash<string, Hash<string, Hash<string, object>>>> _imageCache = new Hash<string, Hash<string, Hash<string, Hash<string, object>>>>();
        private List<Hash<string, object>> _iconOverlay;
        
        private enum EncodingMode {Jpg = 1, Png = 2}
        private enum MapColorsVersion {Version1, Current}

        private bool _isReady;

        private Coroutine _storeImageRoutine;
        private readonly Queue<StorageInfo> _storageQueue = new Queue<StorageInfo>();
        
        private const string DefaultMapName = "Default";
        private const string IconMapName = "Icons";
        
        private readonly Hash<MapColorsVersion, MapColors> _mapColorVersions = new Hash<MapColorsVersion, MapColors>
        {
            //Map colors before the 10/2020 update
            [MapColorsVersion.Version1] = new MapColors
            {
                StartColor = new Vector3(0.324313372f, 0.397058845f, 0.195609868f),
                WaterColor = new Vector4(0.269668937f, 0.4205476f, 0.5660378f, 1f),
                GravelColor = new Vector4(0.139705867f, 0.132621378f, 0.114024632f, 0.372f),
                DirtColor = new Vector4(0.322227329f, 0.375f, 0.228860289f, 1f),
                SandColor = new Vector4(1f, 0.8250507f, 0.448529422f, 1f),
                GrassColor = new Vector4(0.4509804f, 0.5529412f, 0.270588249f, 1f),
                ForestColor = new Vector4(0.5529412f, 0.440000027f, 0.270588249f, 1f),
                RockColor = new Vector4(0.42344287f, 0.4852941f, 0.314013839f, 1f),
                SnowColor = new Vector4(0.8088235f, 0.8088235f, 0.8088235f, 1f),
                PebbleColor = new Vector4(0.121568628f, 0.419607848f, 0.627451f, 1f),
                OffShoreColor = new Vector4(0.166295841f, 0.259337664f, 0.3490566f, 1f),
                SunDirection = Vector3.Normalize(new Vector3(0.95f, 2.87f, 2.37f)),
                Half = new Vector3(0.5f, 0.5f, 0.5f),
                SunPower = 0.5f,
                Brightness = 1f,
                Contrast = 0.87f,
                OceanWaterLevel = 0
            },

            //Current map colors
            [MapColorsVersion.Current] = new MapColors
            {
                StartColor = new Vector3(0.286274523f, 0.270588249f, 0.247058839f),
                WaterColor = new Vector4(0.16941601f, 0.317557573f, 0.362000018f, 1f),
                GravelColor = new Vector4(0.25f, 0.243421048f, 0.220394745f, 1f),
                DirtColor = new Vector4(0.6f, 0.479594618f, 0.33f, 1f),
                SandColor = new Vector4(0.7f, 0.65968585f, 0.5277487f, 1f),
                GrassColor = new Vector4(0.354863644f, 0.37f, 0.2035f, 1f),
                ForestColor = new Vector4(0.248437509f, 0.3f, 0.0703125f, 1f),
                RockColor = new Vector4(0.4f, 0.393798441f, 0.375193775f, 1f),
                SnowColor = new Vector4(0.862745166f, 0.9294118f, 0.941176534f, 1f),
                PebbleColor = new Vector4(0.137254909f, 0.2784314f, 0.2761563f, 1f),
                OffShoreColor = new Vector4(0.04090196f, 0.220600322f, 0.274509817f, 1f),
                SunDirection = Vector3.Normalize(new Vector3(0.95f, 2.87f, 2.37f)),
                Half = new Vector3(0.5f, 0.5f, 0.5f),
                SunPower = 0.65f,
                Brightness = 1.05f,
                Contrast = 0.94f,
                OceanWaterLevel = 0.0f
            }
        };
        
        private readonly Hash<string, IconConfig> _defaultIcons = new Hash<string, IconConfig>
            {
                ["Harbor"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/ND4c70v.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Giant Excavator Pit"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/hmUKFwS.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Junkyard"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/V8D4ZGc.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Launch Site"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/gjdynsc.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Water Treatment Plant"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/5L2Gdag.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Military Tunnel"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/6RwXvC2.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Airfield"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/KhQXhIs.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Power Plant"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/ZxqiBc6.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Train Yard"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/wVifXqr.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Outpost"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/hb7JZ9i.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Bandit Camp"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/cIR4YOt.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Sewer Branch"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/PbKZQdZ.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["HQM Quarry"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/bGFogbM.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Satellite Dish"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/XwSpCJY.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["The Dome"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/mPRgBF2.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Sulfur Quarry"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/bGFogbM.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Stone Quarry"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/bGFogbM.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Power Sub Station"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/LQUknms.png",
                    Width = 60,
                    Height = 60,
                    Show = false
                },
                ["Water Well"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/TASWRD0.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Abandoned Cabins"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/xigwDcW.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Wild Swamp"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/2tcTYKA.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Abandoned Supermarket"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/ZyP2W9F.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Mining Outpost"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/C0acqvj.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Oxum's Gas Station"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/oW1bDdF.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Cave"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/ByKJj9C.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Lighthouse"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/r5vbzhm.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Large Oil Rig"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/AAhZO7k.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Oil Rig"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/AAhZO7k.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Fishing Village"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/7UCs5BO.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Large Fishing Village"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/7UCs5BO.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Ranch"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/VQjSjzA.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
                ["Large Barn"] = new IconConfig
                {
                    ImageUrl = "https://i.imgur.com/VQjSjzA.png",
                    Width = 60,
                    Height = 60,
                    Show = true
                },
            };
        #endregion

        #region Setup & Loading

        private void Init()
        {
            _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
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
            config.StartingSplits = config.StartingSplits ?? new List<string>
            {
                "2x2"
            };

            config.IconSettings = config.IconSettings ?? _defaultIcons;
            
            config.CustomIcons = config.CustomIcons ?? new List<CustomIcons>
            {
                new CustomIcons
                {
                    Height = 200,
                    Width = 200,
                    ImageUrl = "https://www.google.com/images/branding/googlelogo/1x/googlelogo_color_272x92dp.png",
                    Show = false,
                    XPos = 0,
                    ZPos = 0
                }
            };
            return config;
        }

        private void OnServerInitialized()
        {
            _terrainTexture = TerrainTexturing.Instance;
            if (_terrainTexture == null)
            {
                return;
            }
                
            _terrain = _terrainTexture.GetComponent<Terrain>();
            _heightMap = _terrainTexture.GetComponent<TerrainHeightMap>();
            if (_heightMap == null)
            {
                return;
            }
            
            _splatMap = _terrainTexture.GetComponent<TerrainSplatMap>();
            if (_splatMap == null)
            {
                return;
            }

            InvokeHandler.Instance.StartCoroutine(CreateStartupImages());
        }
        
        private IEnumerator CreateStartupImages()
        {
            yield return ValidateImages();
            yield return ValidateConfig();
            yield return LoadIcons();
            yield return CreateStartingRenders();
            yield return CreateStartupSplits();
            
            _isReady = true;
            Interface.Call("OnRustMapApiReady");
            SaveData();
        }

        private void OnNewSave(string filename)
        {
            _storedData = new StoredData();
        }

        private void Unload()
        {
            SaveData();
        }
        #endregion

        #region Handle Startup

        private IEnumerator ValidateImages()
        {
            Puts("Validating existing images");
            foreach (KeyValuePair<string,Hash<string,Hash<string,uint>>> maps in _storedData.MapIds.ToList())
            {
                foreach (KeyValuePair<string, Hash<string, uint>> split in maps.Value.ToList())
                {
                    bool remove = false;
                    foreach (KeyValuePair<string, uint> section in split.Value)
                    {
                        byte[] data = FileStorage.server.Get(section.Value, FileStorage.Type.jpg, CommunityEntity.ServerInstance.net.ID);
                        if (data == null)
                        {
                            remove = true;
                        }
                        
                        yield return null;
                    }

                    if (remove)
                    {
                        Puts($"{maps.Key} {split.Key} not found removing");
                        _storedData.MapIds[maps.Key].Remove(split.Key);
                    }
                }
            }

            foreach (KeyValuePair<int,uint> icons in _storedData.IconIds.ToList())
            {
                byte[] data = FileStorage.server.Get(icons.Value, FileStorage.Type.jpg, CommunityEntity.ServerInstance.net.ID);
                if (data == null)
                {
                    _storedData.IconIds.Remove(icons.Key);
                }
                
                yield return null;
            }
        }

        private IEnumerator ValidateConfig()
        {
            Puts("Validating config has latest monuments");
            bool changed = false;
            if (TerrainMeta.Path != null && TerrainMeta.Path.Monuments != null)
            {
                foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
                {
                    yield return null;
                    string name = GetMonumentName(monument);

                    IconConfig config = _pluginConfig.IconSettings[name];
                    if (config == null)
                    {
                        config = new IconConfig
                        {
                            Height = 90,
                            Width = 90,
                            Show = false,
                            ImageUrl = string.Empty
                        };
                        _pluginConfig.IconSettings[name] = config;
                        changed = true;
                    }

                    if (string.IsNullOrEmpty(config.ImageUrl))
                    {
                        var defaultValue = _defaultIcons[name];
                        if (defaultValue != null)
                        {
                            config.ImageUrl = defaultValue.ImageUrl;
                            config.Show = true;
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
            {
                Config.WriteObject(_pluginConfig);
            }
        }

        private IEnumerator LoadIcons()
        {
            foreach (IconConfig icon in _pluginConfig.IconSettings.Values)
            {
                if (!icon.Show || string.IsNullOrEmpty(icon.ImageUrl))
                {
                    continue;
                }
                
                yield return LoadIcon(icon);
                yield return null;
            }

            foreach (CustomIcons icon in _pluginConfig.CustomIcons)
            {
                if (!icon.Show || string.IsNullOrEmpty(icon.ImageUrl))
                {
                    continue;
                }
                
                yield return LoadIcon(icon);
                yield return null;
            }
            
            Puts("Loaded image icons");
        }

        private IEnumerator CreateStartingRenders()
        {
            Stopwatch sw = Stopwatch.StartNew();
            
            ImageConfig config = new ImageConfig(GetDefaultColors());
            Array2D<Color> render = Render(config, GetDefaultResolution());
            sw.Stop();
            if (render.IsEmpty())
            {
                PrintError("Failed to generate map render");
                yield break;
            }

            _renders[DefaultMapName] = new RenderInfo(render, config);
            Puts($"Map Render Took: {GetDuration(sw.ElapsedMilliseconds)}");
            
            yield return new WaitForSeconds(.1f);
            
            sw.Restart();
            _iconOverlay = BuildIconMonuments(render.Height);
            List<OverlayConfig> overlayConfig = _iconOverlay.Select(o => new OverlayConfig(o)).ToList();
            render = RenderOverlay(render, overlayConfig);
            sw.Stop();
            
            if (render.IsEmpty())
            {
                PrintError("Failed to generate icon render");
                yield break;
            }

            _renders[IconMapName] = new RenderInfo(render, config, overlayConfig);
            
            Puts($"Icon Render Took: {GetDuration(sw.ElapsedMilliseconds)}");
        }

        private IEnumerator CreateStartupSplits()
        {
            yield return new WaitForSeconds(.1f);

            if (!HasSplit(DefaultMapName, 1, 1))
            { 
                SaveSingleImage(DefaultMapName);
                yield return new WaitForSeconds(.1f);
            }

            if (!HasSplit(IconMapName, 1, 1))
            { 
                SaveSingleImage(IconMapName);
                yield return new WaitForSeconds(.1f);
            }

            foreach (string splitText in _pluginConfig.StartingSplits)
            {
                if (!splitText.ToLower().Contains("x"))
                {
                    PrintError($"split {splitText} does not contain an x");
                    continue;
                }

                string[] splits = splitText.Split('x', 'X');
                if (splits.Length < 2)
                {
                    PrintError($"split {splitText} is not valid. Format should be 2x2 for 2 rows x 2 columns");
                    continue;
                }

                int row;
                if (!int.TryParse(splits[0], out row))
                {
                    PrintError($"Row of {splits[0]} is not a valid number");
                    continue;
                }
                
                int col;
                if (!int.TryParse(splits[1], out col))
                {
                    PrintError($"Column of {splits[1]} is not a valid number");
                    continue;
                }
                
                if (!HasSplit(DefaultMapName, row, col))
                {
                    SaveSplitImage(DefaultMapName, row, col);
                    yield return new WaitForSeconds(.1f);
                }

                if (!HasSplit(IconMapName, row, col))
                {
                    SaveSplitImage(IconMapName, row, col);
                    yield return new WaitForSeconds(.1f);
                }
            }
                
            yield return new WaitForSeconds(1f);
        }
        #endregion

        #region Console Command
        [ConsoleCommand("rma_regenerate")]
        private void RegenerateConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
            {
                return;
            }

            InvokeHandler.Instance.StartCoroutine(HandleRegenerate(arg));
        }

        private IEnumerator HandleRegenerate(ConsoleSystem.Arg arg)
        {
            Puts("Removing maps from file storage");   
            foreach (Hash<string, Hash<string, uint>> maps in _storedData.MapIds.Values)
            {
                foreach (Hash<string,uint> split in maps.Values)
                {
                    foreach (uint section in split.Values)
                    {
                        FileStorage.server.Remove(section, FileStorage.Type.jpg, CommunityEntity.ServerInstance.net.ID);
                        yield return null;
                    }
                }
            }

            Puts("Removing Icons from file storage");   
            foreach (uint icon in _storedData.IconIds.Values)
            {
                FileStorage.server.Remove(icon, FileStorage.Type.jpg, CommunityEntity.ServerInstance.net.ID);
                yield return null;
            }
            
            Puts("Wiping stored data");   
            _storedData = new StoredData();

            Puts("Regenerating images");
            yield return CreateStartupImages();
        }
        
        [ConsoleCommand("rma_upload")]
        private void UploadConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
            {
                return;
            }

            if (ImgurApi == null)
            {
                PrintWarning("Cannot upload missing plugin dependency ImgurApi: https://umod.org/plugins/imgur-api");
                return;
            }

            if (arg.Args == null || arg.Args.Length < 1)
            {
                arg.ReplyWith("Invalid Syntax. rma_upload <mapName> <resolution:optional> <numRows:optional> <numCols:optional>\nEx: rma_upload default 2000 1 1 - to upload an image with a resolution of 2000x2000 and split into a 1x1 square to imgur");
                return;
            }

            string mapName = _renders.Keys.FirstOrDefault(k =>
                k.Equals(arg.GetString(0), StringComparison.InvariantCultureIgnoreCase));
            if (string.IsNullOrEmpty(mapName))
            {
                arg.ReplyWith($"Map name does not exist. Valid map names are {string.Join(", ", _renders.Keys.ToArray())}");
                return;
            }

            int defaultResolution = GetDefaultResolution();
            int resolution = arg.GetInt(1, defaultResolution);
            int numRows = arg.GetInt(2, 1);
            int numCols = arg.GetInt(3, 1);
            
            if (numRows < 1)
            {
                arg.ReplyWith($"Invalid number of rows: {arg.Args[1]}");
                return;
            }

            if (numRows > 64)
            {
                arg.ReplyWith($"Num rows cannot be > 64: {arg.Args[1]}");
                return;
            }

            if (numCols < 1)
            {
                arg.ReplyWith($"Invalid number of columns: {arg.Args[2]}");
                return;
            }
            
            if (numCols > 64)
            {
                arg.ReplyWith($"Num cols cannot be > 64: {arg.Args[2]}");
                return;
            }

            if (resolution < 1)
            {
                arg.ReplyWith($"Invalid resolution: {arg.Args[3]}");
                return;
            }

            NextTick(() =>
            {
                Array2D<Color> render = new Array2D<Color>();
                if (resolution != defaultResolution)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    RenderInfo info = _renders[mapName];

                    render = Render(info.RenderConfig, resolution);
                    if (info.OverlayConfig != null)
                    {
                        List<Hash<string, object>> monuments = BuildIconMonuments(resolution);
                        render = RenderOverlay(render, monuments.Select(m => new OverlayConfig(m)).ToList());
                    }
                    sw.Stop();
                    Puts($"Upload render took: {GetDuration(sw.ElapsedMilliseconds)}");
                }

                if (numRows == 1 && numCols == 1)
                {
                    Hash<string, object> map = resolution != defaultResolution ? CreateSingle(render, _pluginConfig.DefaultImageEncoding)[GetIndex(0, 0)] : GetFullMap(mapName);
                    string title = GetImageTitle(mapName, numRows, numCols);
                    UploadSingle(map, title, HandleSingleResponse);
                    Puts("Uploading to imgur");
                }
                else
                {
                    Hash<string, Hash<string, object>> map = resolution != defaultResolution ? CreateSplice(render, numRows, numCols, _pluginConfig.DefaultImageEncoding) : GetSplit(mapName, numRows, numCols);
                    string albumTitle = GetImageTitle(mapName, numRows, numCols);
                    UploadAlbum(map, albumTitle, mapName, HandleAlbumResponse);
                    Puts("Uploading to imgur album");
                }
            });
        }

        private string GetImageTitle(string mapName, int row, int col)
        {
           return $"Map: {mapName} Save:{Protocol.save} Size:{World.Size} Seed:{World.Seed} {row}x{col}";
        }

        private string GetSectionTitle(string mapName, string section)
        {
            return $"{mapName} {World.Size} {World.Seed} {section}";
        }
        
        private void HandleSingleResponse(Hash<string, object> response)
        {
            bool success = (bool)response["Success"];
            if (!success)
            {
                PrintError($"An error occured uploading the image \n\n{JsonConvert.SerializeObject(response)}");
                return;
            }

            Hash<string, object> data = response["Data"] as Hash<string, object>;
            
            Puts($"{data?["Link"]}");
            Interface.Call("OnRustFullMapUploaded", response);
        }

        private void HandleAlbumResponse(Hash<string, Hash<string, object>> response)
        {
            bool success = (bool)response["Album"]["Success"];
            if (!success)
            {
                PrintError($"An error occured uploading the image \n\n{JsonConvert.SerializeObject(response)}");
                return;
            }

            Hash<string, object> data = response["Album"]["Data"] as Hash<string, object>;
            
            Puts($"{data?["Link"]}");
            Interface.Call("OnRustSplitMapUploaded", response);
        }
        #endregion

        #region API

        private bool IsReady()
        {
            return _isReady;
        }
        
        private int GetDefaultResolution()
        {
            return (int)(World.Size / 2);
        }
        
        private int GetDefaultImageFormat()
        {
            return (int) _pluginConfig.DefaultImageEncoding;
        }
        
        private Hash<string, object> CreateRender(string mapName, Hash<string, object> config)
        {
            if (!_isReady)
            {
                return null;
            }

            int resolution = config.ContainsKey("ImageResolution") ? (int) config["ImageResolution"] : GetDefaultResolution();
            MapColorsVersion version = _pluginConfig.MapColorsVersion;
            if (config.ContainsKey("Version"))
            {
                string versionString = (string) config["Version"];
                Enum.TryParse(versionString, true, out version);
            }
            
            ImageConfig imageConfig = new ImageConfig(config, _mapColorVersions[version]);
            Array2D<Color> render = Render(imageConfig, resolution);
            _renders[mapName] = new RenderInfo(render, imageConfig);
            
            return RenderToHash(render);
        }

        private object CreatePluginRender(Plugin plugin, string mapName, int resolution)
        {
            if (!_isReady)
            {
                return null;
            }
            
            Stopwatch sw = Stopwatch.StartNew();

            mapName = _renders.Keys.FirstOrDefault(k => k.Equals(mapName, StringComparison.InvariantCultureIgnoreCase));
            
            if (string.IsNullOrEmpty(mapName))
            {
                return $"Map name does not exist. Valid map names are {string.Join(", ", _renders.Keys.ToArray())}";
            }
            
            RenderInfo info = _renders[mapName];

            Puts($"Starting render for plugin {plugin.Name}");
            Array2D<Color> render = Render(info.RenderConfig, resolution);
            if (info.OverlayConfig != null)
            {
                List<Hash<string, object>> monuments = BuildIconMonuments(resolution);
                render = RenderOverlay(render, monuments.Select(m => new OverlayConfig(m)).ToList());
            }
            sw.Stop();
            Puts($"Render for plugin {plugin.Name} took: {GetDuration(sw.ElapsedMilliseconds)}");
            return render;
        }
        
        private object CreatePluginImage(Plugin plugin, string mapName, int resolution, int encoding)
        {
            object response = CreatePluginRender(plugin, mapName, resolution);
            if (!(response is Array2D<Color>))
            {
                return response;
            }

            EncodingMode mode = (EncodingMode) encoding;
            
            return CreateSingle((Array2D<Color>)response, mode)[GetIndex(0, 0)];
        }

        private void UploadPluginImageSingle(Plugin plugin, string mapName, int resolution, int encoding, Action<Hash<string,object>> callback, string title = null)
        {
            object response = CreatePluginImage(plugin, mapName, resolution, encoding);
            if (!(response is Hash<string, object>))
            {
                PrintError($"UploadPluginImageSingle has error {response}");
                return;
            }

            Hash<string, object> map = (Hash<string, object>) response;
            UploadSingle(map, title, callback);
        }

        private Hash<string, object> CreateRenderOverlay(string renderSource, string newMapName, List<Hash<string, object>> overlay)
        {
            if (!_isReady)
            {
                return null;
            }

            RenderInfo renderInfo = _renders[renderSource];
            if (renderInfo == null)
            {
                return null;
            }

            List<OverlayConfig> overlayConfig = overlay.Select(o => new OverlayConfig(o)).ToList();

            Array2D<Color> overlayRender = RenderOverlay(renderInfo.Colors, overlayConfig);

            _renders[newMapName] = new RenderInfo(overlayRender, renderInfo.RenderConfig, overlayConfig);
            return RenderToHash(overlayRender);
        }
        
        private Hash<string, object> GetRender(string mapName)
        {
            if (!_renders.ContainsKey(mapName))
            {
                return null;
            }

            return RenderToHash(_renders[mapName].Colors);
        }
        
        private Hash<string, Hash<string, object>> CreateSingle(string mapName, int encodingMode)
        {
            if (!_renders.ContainsKey(mapName))
            {
                return null;
            }
            
            Array2D<Color> render = _renders[mapName].Colors;
            EncodingMode mode = (EncodingMode) encodingMode;

            return CreateSingle(render, mode);
        }

        private Hash<string, Hash<string, object>> CreateSingle(Array2D<Color> render, EncodingMode mode)
        {
            return new Hash<string, Hash<string, object>>
            {
                [GetIndex(0,0)] = CreateMapData(render, mode)
            };
        }
        
        private Hash<string, Hash<string,object>> CreateSplice(string mapName, int numRows, int numCols, int encodingMode)
        {
            if (!_renders.ContainsKey(mapName))
            {
                return null;
            }
            
            Array2D<Color> render = _renders[mapName].Colors;
            EncodingMode mode = (EncodingMode) encodingMode;

            return CreateSplice(render, numRows, numCols, mode);
        }

        private Hash<string, Hash<string, object>> CreateSplice(Array2D<Color> render, int numRows, int numCols, EncodingMode mode)
        {
            Hash<string, Hash<string,object>> splice = new Hash<string, Hash<string, object>>();
            int rowSize = render.Height / numRows;
            int colSize = render.Width / numCols;
            for (int x = 0; x < numRows; x++)
            {
                for (int y = 0; y < numCols; y++)
                {
                    Array2D<Color> splicedColors = render.Splice(y * colSize, x * rowSize, colSize, rowSize);
                    splice[GetIndex(x,y)] = CreateMapData(splicedColors, mode);
                }
            }
            
            return splice;
        }
        
        private Hash<string, Hash<string, object>> SaveSingleImage(string mapName)
        {
            if (!_renders.ContainsKey(mapName))
            {
                return null;
            }

            Stopwatch sw = Stopwatch.StartNew();
            Hash<string, Hash<string, object>> single = CreateSingle(mapName, (int) _pluginConfig.DefaultImageEncoding);
            sw.Stop();
            
            Puts($"{mapName} Encoding Took: {GetDuration(sw.ElapsedMilliseconds)}");
            
            SaveCache(mapName, GetIndex(1, 1), single);
            SaveSplit(mapName, GetIndex(1, 1), single);
            return single;
        }

        private Hash<string, Hash<string, object>> SaveSplitImage(string mapName, int numRows, int numCols)
        {
            Stopwatch sw = Stopwatch.StartNew();
            Hash<string, Hash<string, object>> split = CreateSplice(mapName, numRows, numCols, (int) _pluginConfig.DefaultImageEncoding);
            sw.Stop();
            
            string index = GetIndex(numRows, numCols);
            Puts($"{mapName} {index} Split Took: {GetDuration(sw.ElapsedMilliseconds)}");

            SaveCache(mapName, index, split);
            SaveSplit(mapName, index, split);
            return split;
        }
        
        private List<string> GetRenderNames()
        {
            return _renders.Keys.ToList();
        }
        
        private List<string> GetSavedSplits(string mapName)
        {
            return _storedData.GetSavedSplits(mapName);
        }
        
        private Hash<string, object> GetFullMap(string mapName)
        {
            return GetSection(mapName, 1, 1, 0, 0);
        }

        private List<Hash<string, object>> GetIconOverlay()
        {
            return _iconOverlay;
        }

        private bool HasSplit(string mapName, int numRows, int numCols)
        {
            Hash<string, uint> split = _storedData.MapIds[mapName]?[GetIndex(numRows, numCols)];
            if (split != null)
            {
                return split.Count == numRows * numCols;
            }

            return false;
        }

        private Hash<string, object> GetSection(string mapName, int numRows, int numCols, int row, int col)
        {
            if (numRows <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numRows), "numRows cannot be less <= 0!");
            }
            
            if (numCols <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numCols), "numCols cannot be less <= 0!");
            }
            
            if (row < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(row), "Row cannot be less < 0!");
            }
            
            if (col < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(col), "Col cannot be less < 0!");
            }

            if (row >= numRows)
            {
                throw new ArgumentOutOfRangeException(nameof(numRows), "row cannot be >= numRows");
            }
            
            if (col >= numCols)
            {
                throw new ArgumentOutOfRangeException(nameof(numRows), "col cannot be >= numCols");
            }
            
            string splitIndex = GetIndex(numRows, numCols);
            string sectionIndex = GetIndex(row, col);
            Hash<string, object> cacheSection = GetCacheSection(mapName, splitIndex, sectionIndex);
            if (cacheSection != null)
            {
                return cacheSection;
            }
            
            if (HasSplit(mapName, numRows, numCols))
            {
                Hash<string, object> section = LoadSection(mapName, splitIndex, sectionIndex);
                if (section != null)
                {
                    return section;
                }
            }

            if (numRows == 1 && numCols == 1)
            {
                return SaveSingleImage(mapName)[sectionIndex];
            }

            return SaveSplitImage(mapName, numRows, numCols)[sectionIndex];
        }

        private Hash<string, Hash<string, object>> GetSplit(string mapName, int numRows, int numCols)
        {
            if (numRows <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numRows), "numRows cannot be less <= 0!");
            }
            
            if (numCols <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numCols), "numCols cannot be less <= 0!");
            }
            
            string splitIndex = GetIndex(numRows, numCols);
            Hash<string, Hash<string, object>> cacheSplit = GetCacheSplit(mapName, splitIndex);
            if (cacheSplit != null)
            {
                return cacheSplit;
            }
            
            if (HasSplit(mapName, numRows, numCols))
            {
                Hash<string, Hash<string, object>> split = new Hash<string, Hash<string, object>>();
                foreach (KeyValuePair<string,uint> pair in _storedData.MapIds[mapName][splitIndex])
                {
                    split[pair.Key] = LoadSection(mapName, splitIndex, pair.Key);
                }

                return split;
            }

            if (numRows == 1 && numCols == 1)
            {
                return SaveSingleImage(mapName);
            }

            return SaveSplitImage(mapName, numRows, numCols);
        }
        #endregion

        #region Icon Handling

        private List<Hash<string, object>> BuildIconMonuments(int size)
        {
            float posScale = (float)size / World.Size;
            List<Hash<string, object>> overlays = new  List<Hash<string, object>>();
            if (TerrainMeta.Path != null && TerrainMeta.Path.Monuments != null)
            {
                foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
                {
                    string name = GetMonumentName(monument);

                    IconConfig config = _pluginConfig.IconSettings[name];
                    if (config == null)
                    {
                        _pluginConfig.IconSettings[name] = new IconConfig
                        {
                            Height = 90,
                            Width = 90,
                            Show = false,
                            ImageUrl = string.Empty
                        };
                        Config.WriteObject(_pluginConfig);
                        continue;
                    }

                    if (string.IsNullOrEmpty(config.ImageUrl))
                    {
                        var defaultValue = _defaultIcons[name];
                        if (defaultValue != null)
                        {
                            config.ImageUrl = defaultValue.ImageUrl;
                            config.Show = true;
                            Config.WriteObject(_pluginConfig);
                        }
                    }

                    if (!config.Show)
                    {
                        continue;
                    }
                    
                    float x = monument.transform.position.z;
                    float z = monument.transform.position.x;

                    AddImageToOverlay(x, z, posScale, overlays, config, name);
                }
            }

            foreach (CustomIcons customIcon in _pluginConfig.CustomIcons)
            {
                if (!customIcon.Show)
                {
                    continue;
                }
                
                AddImageToOverlay(customIcon.XPos, customIcon.ZPos, posScale, overlays, customIcon, $"Custom Image: {customIcon.ImageUrl}");
            }
            return overlays;
        }

        private static void AddImageToOverlay(float x,float z, float posScale , List<Hash<string, object>> overlays, IconConfig config, string name)
        {
            float half = World.Size / 2.0f;

            x = (x + half) * posScale;
            z = (z + half) * posScale;
            
            overlays.Add(new Hash<string, object>
            {
                [nameof(OverlayConfig.Height)] = (int) (config.Height / posScale),
                [nameof(OverlayConfig.Width)] = (int) (config.Width / posScale),
                [nameof(OverlayConfig.Image)] = config.Image,
                [nameof(OverlayConfig.XPos)] = (int) x,
                [nameof(OverlayConfig.YPos)] = (int) z,
                [nameof(OverlayConfig.DebugName)] = name
            });
        }
        
        private string GetMonumentName(MonumentInfo monument)
        {
            string name = monument.displayPhrase.english.Replace("\n", "");
            if (string.IsNullOrEmpty(name))
            {
                if (monument.Type == MonumentType.Cave)
                {
                    name = "Cave";
                }
                else if(monument.name.Contains("power_sub"))
                {
                    name = "Power Sub Station";
                }
                else
                {
                    name = monument.name;
                }
            }

            return name;
        }

        #endregion
        
        #region Storage Handling
        private Hash<string, object> LoadSection(string mapName, string split, string section)
        {
            Hash<string, object> cache = GetCacheSection(mapName, split, section);
            if (cache != null)
            {
                return cache;
            }

            uint? imageId = _storedData.MapIds[mapName]?[split]?[section];
            if (imageId == null)
            {
                return null;
            }

            byte[] data = LoadImage(imageId.Value, FileStorage.Type.jpg);

            Hash<string, object> imageData = ImageDataFromBytes(data);
            SaveCache(mapName, split, section, imageData);

            return imageData;
        }
        
        private void SaveSplit(string mapName, string split, Hash<string, Hash<string, object>> splitData)
        {
            _storageQueue.Enqueue(new StorageInfo
            {
                MapName = mapName,
                Split = split,
                SplitData = splitData
            });
            
            if (_storeImageRoutine == null)
            {
                _storeImageRoutine = InvokeHandler.Instance.StartCoroutine(HandleSave());
            }
        }

        private IEnumerator HandleSave()
        {
            while (_storageQueue.Count > 0)
            {
                StorageInfo next = _storageQueue.Dequeue();
                foreach (KeyValuePair<string,Hash<string,object>> data in next.SplitData)
                {
                    StoreSection(next.MapName, next.Split, data.Key, data.Value);
                    yield return null;
                }
            }

            if (_isReady)
            {
                SaveData();
            }

            _storeImageRoutine = null;
        }
        
        private void StoreSection(string mapName, string split, string section, Hash<string, object> imageData)
        {
            Hash<string, Hash<string, uint>> map = _storedData.MapIds[mapName];
            if (map == null)
            {
                map = new Hash<string, Hash<string, uint>>();
                _storedData.MapIds[mapName] = map;
            }
            
            byte[] storageBytes = BytesFromImageData(imageData);
            Hash<string, uint> splitData = map[split];
            if (splitData == null)
            {
                splitData = new Hash<string, uint>();
                map[split] = splitData;
            }

            splitData[section] = StoreImage(storageBytes, FileStorage.Type.jpg);
        }

        private Hash<string, Hash<string, object>> GetCacheSplit(string mapName, string split)
        {
            return _imageCache[mapName]?[split];
        }
        
        private Hash<string, object> GetCacheSection(string mapName, string split, string section)
        {
            return GetCacheSplit(mapName,split)?[section];
        }

        private void SaveCache(string mapName, string split, Hash<string, Hash<string, object>> data)
        {
            Hash<string, Hash<string, Hash<string, object>>> map = _imageCache[mapName];
            if (map == null)
            {
                map = new Hash<string, Hash<string, Hash<string, object>>>();
                _imageCache[mapName] = map;
            }

            map[split] = data;
        }
        
        private void SaveCache(string mapName, string split, string section,  Hash<string, object> data)
        {
            Hash<string, Hash<string, Hash<string, object>>> map = _imageCache[mapName];
            if (map == null)
            {
                map = new Hash<string, Hash<string, Hash<string, object>>>();
                _imageCache[mapName] = map;
            }

            Hash<string, Hash<string, object>> cache = map[split];
            if (cache == null)
            {
                cache = new Hash<string, Hash<string, object>>();
                map[split] = cache;
            }

            cache[section] = data;
        }

        private uint StoreImage(byte[] bytes, FileStorage.Type type)
        {
            return FileStorage.server.Store(Compression.Compress(bytes), type, CommunityEntity.ServerInstance.net.ID);
        }

        private byte[] LoadImage(uint id, FileStorage.Type type)
        {
            return Compression.Uncompress(FileStorage.server.Get(id, type, CommunityEntity.ServerInstance.net.ID));
        }

        private Hash<string, object> ImageDataFromBytes(byte[] bytes)
        {
            return new Hash<string, object>
            {
                ["width"] = BitConverter.ToInt32(bytes, 0),
                ["height"] = BitConverter.ToInt32(bytes, 4),
                ["image"] = bytes.Skip(8).ToArray()
            };
        }
        
        private byte[] BytesFromImageData(Hash<string, object> data)
        {
            byte[] width = BitConverter.GetBytes((int) data["width"]);
            byte[] height = BitConverter.GetBytes((int) data["height"]);
            byte[] image = (byte[]) data["image"];
            
            byte[] bytes = new byte[width.Length + height.Length + image.Length];
            Array.Copy(width, 0, bytes, 0, width.Length);
            Array.Copy(height, 0, bytes, width.Length, height.Length);
            Array.Copy(image, 0, bytes, width.Length + height.Length, image.Length);
            return bytes;
        }
        #endregion

        #region Helper Methods
        private Hash<string, object> RenderToHash(Array2D<Color> colors)
        {
            return new Hash<string, object>
            {
                ["colors"] = colors.Items,
                ["width"] = colors.Width,
                ["height"] = colors.Height
            };
        }

        private Hash<string, object> CreateMapData(Array2D<Color> colors, EncodingMode mode)
        {
            return  new Hash<string, object>
            {
                ["image"] = EncodeTo(colors.Items, colors.Width, colors.Height, mode),
                ["width"] = colors.Width,
                ["height"] = colors.Height
            };
        }
        
        private string GetIndex(int row, int col)
        {
            return $"{row}x{col}";
        }

        private MapColors GetDefaultColors()
        {
            return _mapColorVersions[_pluginConfig.MapColorsVersion];
        }

        private string GetDuration(double milliseconds)
        {
            return $"{TimeSpan.FromMilliseconds(milliseconds).TotalSeconds:0.00} Seconds";
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);
        }
        #endregion

        #region Imgur Helpers
        private void UploadSingle(Hash<string, object> map, string title, Action<Hash<string, object>> callback)
        {
            ImgurApi.Call("UploadImage", map["image"], new Action<Hash<string, object>>(callback), title);
        }
        
        private void UploadAlbum(Hash<string, Hash<string, object>> map, string title, string mapName, Action<Hash<string, Hash<string, object>>> callback)
        {
            List<Hash<string, object>> images = new List<Hash<string, object>>();
            foreach (KeyValuePair<string, Hash<string, object>> section in map)
            {
                images.Add(new Hash<string, object>
                {
                    ["Image"] = section.Value["image"],
                    ["Title"] = GetSectionTitle(mapName, section.Key)
                });
            }

            ImgurApi.Call("UploadAlbum", images, new Action<Hash<string, Hash<string, object>>>(callback), title);
        }
        #endregion

        #region Map Renderer

        private Array2D<Color> Render(ImageConfig config, int mapSize)
        {
            int waterOffset = config.WaterOffset;
            int halfWaterOffset = waterOffset / 2;

            if (_heightMap == null || _splatMap == null || _terrain == null)
            {
                return new Array2D<Color>();
            }
            
            if (mapSize <= 0)
            {
                return new Array2D<Color>();
            }
            
            int imageWidth = mapSize + waterOffset;
            int imageHeight = mapSize + waterOffset;
            int widthWithWater = mapSize + halfWaterOffset;

            float scale = 1f / mapSize;

            Array2D<Color> output = new Array2D<Color>(imageWidth, imageHeight);

            Parallel.For(-halfWaterOffset, imageHeight - halfWaterOffset, row =>
            {
                float offsetRow = row * scale;
                for (int col = -halfWaterOffset; col < widthWithWater; col++)
                {
                    float offsetCol = col * scale;
                    float terrainHeight = GetHeight(offsetRow, offsetCol);
                    float sun = Math.Max(Vector3.Dot(GetNormal(offsetRow, offsetCol), config.SunDirection), 0.0f);
                    Vector3 pixel = Vector3.Lerp(config.StartColor, config.GravelColor, GetSplat(row, col, 128) * config.GravelColor.w);
                    pixel = Vector3.Lerp(pixel, config.PebbleColor, GetSplat(offsetRow, offsetCol, 64) * config.PebbleColor.w);
                    pixel = Vector3.Lerp(pixel, config.RockColor, GetSplat(offsetRow, offsetCol, 8) * config.RockColor.w);
                    pixel = Vector3.Lerp(pixel, config.DirtColor, GetSplat(offsetRow, offsetCol, 1) * config.DirtColor.w);
                    pixel = Vector3.Lerp(pixel, config.GrassColor, GetSplat(offsetRow, offsetCol, 16) * config.GrassColor.w);
                    pixel = Vector3.Lerp(pixel, config.ForestColor, GetSplat(offsetRow, offsetCol, 32) * config.ForestColor.w);
                    pixel = Vector3.Lerp(pixel, config.SandColor, GetSplat(offsetRow, offsetCol, 4) * config.SandColor.w);
                    pixel = Vector3.Lerp(pixel, config.SnowColor, GetSplat(offsetRow, offsetCol, 2) * config.SnowColor.w);
                    float waterDepth = -terrainHeight;
                    if (waterDepth > config.OceanWaterLevel)
                    {
                        pixel = Vector3.Lerp(pixel, config.WaterColor, Mathf.Clamp(0.5f + waterDepth / 5.0f, 0.0f, 1f));
                        pixel = Vector3.Lerp(pixel, config.OffShoreColor, Mathf.Clamp(waterDepth / 50f, 0.0f, 1f));
                        sun = config.SunPower;
                    }

                    pixel += (sun - config.SunPower) * config.SunPower * pixel;
                    pixel = (pixel - config.Half) * config.Contrast + config.Half;
                    pixel *= config.Brightness;
                    
                    output[row + halfWaterOffset, col + halfWaterOffset] = new Color(pixel.x, pixel.y, pixel.z);
                }
            });
            
            return output;
        }

        private Array2D<Color> RenderOverlay(Array2D<Color> previous, List<OverlayConfig> overlays)
        {
            Array2D<Color> colors = previous.Clone();
            foreach (OverlayConfig overlay in overlays)          
            {
                if (overlay.Image == null || overlay.Image.Length == 0)
                {
                    Puts($"{overlay.DebugName} contains an invalid image");
                    continue;
                }
                
                using (Bitmap image = ResizeImage(overlay.Image, overlay.Width, overlay.Height))
                {
                    int startRow = overlay.YPos - overlay.Height / 2;
                    int startCol = overlay.XPos - overlay.Width / 2;

                    if (startRow < -overlay.Width)
                    {
                        startRow = 5;
                    }

                    if (startRow > colors.Width - overlay.Width)
                    {
                        startRow = colors.Width - overlay.Width - 5;
                    }

                    if (startCol < -overlay.Height)
                    {
                        startCol = 5;
                    }

                    if (startCol > colors.Height - overlay.Height)
                    {
                        startCol = colors.Height - overlay.Height - 5;
                    }
                    
                    for (int row = 0; row < image.Height; row++)
                    {
                        for (int col = 1; col <= image.Width; col++)
                        {
                            System.Drawing.Color pixel = image.GetPixel(row, overlay.Width - col);
                            int overlayRow = row + startRow;
                            int overlayCol = col + startCol;
                            if (pixel.A != 0)
                            {
                                if (overlayRow >= colors.Height || overlayCol >= colors.Width || overlayRow < 0 || overlayCol < 0)
                                {
                                    continue;
                                }
                                
                                colors[overlayRow, overlayCol] = new Color32(pixel.R, pixel.G, pixel.B, pixel.A);
                            }
                        }
                    }
                }
            }

            return colors;
        }

        float GetHeight(float x, float y)
        {
            return _heightMap.GetHeight(x, y);
        }

        Vector3 GetNormal(float x, float y)
        {
            return _heightMap.GetNormal(x, y);
        }

        float GetSplat(float x, float y, int mask)
        {
            return _splatMap.GetSplat(x, y, mask);
        }
        
        private byte[] EncodeTo(Color[] color, int width, int height, EncodingMode mode)
        {
            Texture2D tex = null;
            try
            {
                tex = new Texture2D(width, height);
                tex.SetPixels(color);
                tex.Apply();
                return mode == EncodingMode.Jpg ? tex.EncodeToJPG(85) : tex.EncodeToPNG();
            }
            finally
            {
                if (tex != null)
                    UnityEngine.Object.Destroy(tex);
            }
        }
        
        private Bitmap ResizeImage(byte[] bytes, int targetWidth, int targetHeight)
        {
            using (MemoryStream original = new MemoryStream())
            {
                original.Write(bytes, 0, bytes.Length);
                using (Bitmap img = new Bitmap(Image.FromStream(original)))
                {
                    return new Bitmap(img, new Size(targetWidth, targetHeight));
                }
            }
        }
        #endregion

        #region Icon Handling

        private IEnumerator LoadIcon(IconConfig config)
        {
            int code = config.ImageUrl.GetHashCode();
            uint iconId = _storedData.IconIds[code];
            if (iconId != 0)
            {
                config.Image = LoadImage(iconId, FileStorage.Type.png);
            }
            else
            {
                yield return DownloadIcon(config, code);
            }
        }

        private IEnumerator DownloadIcon(IconConfig config, int code)
        {
            using (UnityWebRequest www = UnityWebRequest.Get(config.ImageUrl))
            {
                yield return www.SendWebRequest();

                if (www.isNetworkError || www.isHttpError)
                {
                    PrintError($"Failed to download icon: {www.error}");
                    www.Dispose();
                    yield break;
                }
                
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(www.downloadHandler.data);
                if (texture != null)
                {
                    byte[] bytes = texture.EncodeToPNG();
                    GameObject.Destroy(texture);
                    config.Image = bytes;
                    _storedData.IconIds[code] = StoreImage(bytes, FileStorage.Type.png);
                }
            }
        }

        #endregion
        
        #region Classes
        private class PluginConfig
        {
            [DefaultValue(MapColorsVersion.Current)]
            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty(PropertyName = "Map Colors Version")]
            public MapColorsVersion MapColorsVersion { get; set; }
            
            [DefaultValue(EncodingMode.Jpg)]
            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty(PropertyName = "Default Image Encoding (Jpg, Png)")]
            public EncodingMode DefaultImageEncoding { get; set; }
            
            [JsonProperty(PropertyName = "Starting Splits (Rows x Columns)")]
            public List<string> StartingSplits { get; set; }
            
            [JsonProperty(PropertyName = "IconSettings")]
            public Hash<string, IconConfig> IconSettings { get; set; }
            
            [JsonProperty(PropertyName = "Custom Icons")]
            public List<CustomIcons> CustomIcons { get; set; }
        }

        private class IconConfig
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public string ImageUrl { get; set; }
            public bool Show { get; set; }
            
            [JsonIgnore]
            public byte[] Image { get; set; }
        }

        private class CustomIcons : IconConfig
        {
            public float XPos { get; set; }
            public float ZPos { get; set; }
        }

        private class StoredData
        {
            public Hash<string, Hash<string, Hash<string, uint>>> MapIds = new Hash<string, Hash<string, Hash<string, uint>>>();
            public Hash<int, uint> IconIds = new Hash<int, uint>();
            
            public List<string> GetSavedSplits(string mapName)
            {
                Hash<string, Hash<string, uint>> splits = MapIds[mapName];
                return splits == null ? new List<string>() : splits.Keys.ToList();
            }
        }

        private class StorageInfo
        {
            public string MapName { get; set; }
            public string Split { get; set; }
            public Hash<string, Hash<string, object>> SplitData { get; set; }
        }

        private class RenderInfo
        {
            public Array2D<Color> Colors { get; set; }
            public ImageConfig RenderConfig { get; set; }
            public List<OverlayConfig> OverlayConfig { get; set; }

            public RenderInfo()
            {
                
            }
            
            public RenderInfo(Array2D<Color> colors, ImageConfig renderConfig)
            {
                Colors = colors;
                RenderConfig = renderConfig;
            }

            public RenderInfo(Array2D<Color> colors, ImageConfig renderConfig, List<OverlayConfig> overlayConfig)
            {
                Colors = colors;
                RenderConfig = renderConfig;
                OverlayConfig = overlayConfig;
            }
        }
        
        private class MapColors
        {
            public Vector3 StartColor { get; set; }
            public Vector4 WaterColor { get; set; }
            public Vector4 GravelColor  { get; set; }
            public Vector4 DirtColor  { get; set; }
            public Vector4 SandColor  { get; set; }
            public Vector4 GrassColor  { get; set; }
            public Vector4 ForestColor  { get; set; }
            public Vector4 RockColor  { get; set; }
            public Vector4 SnowColor  { get; set; }
            public Vector4 PebbleColor  { get; set; }
            public Vector4 OffShoreColor { get; set; }
            public Vector3 SunDirection  { get; set; }
            public Vector3 Half  { get; set; }
            public int WaterOffset { get; set; }
            public float SunPower { get; set; }
            public float Brightness { get; set; }
            public float Contrast { get; set; }
            public float OceanWaterLevel { get; set; }
        }

        private class ImageConfig : MapColors
        {
            public ImageConfig(MapColors defaultColors) : this(new Hash<string, object>(), defaultColors)
            {
                
            }
            
            public ImageConfig(Hash<string, object> config, MapColors defaultColors)
            {
                StartColor = config.ContainsKey(nameof(StartColor)) ? (Vector3) config[nameof(StartColor)] : defaultColors.StartColor;
                WaterColor = config.ContainsKey(nameof(WaterColor)) ? (Vector4) config[nameof(WaterColor)] : defaultColors.WaterColor;
                GravelColor = config.ContainsKey(nameof(GravelColor)) ? (Vector4) config[nameof(GravelColor)] : defaultColors.GravelColor;
                DirtColor = config.ContainsKey(nameof(DirtColor)) ? (Vector4) config[nameof(DirtColor)] : defaultColors.DirtColor;
                SandColor = config.ContainsKey(nameof(SandColor)) ? (Vector4) config[nameof(SandColor)] : defaultColors.SandColor;
                GrassColor = config.ContainsKey(nameof(GrassColor)) ? (Vector4) config[nameof(GrassColor)] : defaultColors.GrassColor;
                ForestColor = config.ContainsKey(nameof(ForestColor)) ? (Vector4) config[nameof(ForestColor)] : defaultColors.ForestColor;
                RockColor = config.ContainsKey(nameof(RockColor)) ? (Vector4) config[nameof(RockColor)] : defaultColors.RockColor;
                SnowColor = config.ContainsKey(nameof(SnowColor)) ? (Vector4) config[nameof(SnowColor)] : defaultColors.SnowColor;
                PebbleColor = config.ContainsKey(nameof(PebbleColor)) ? (Vector4) config[nameof(PebbleColor)] : defaultColors.PebbleColor;
                OffShoreColor = config.ContainsKey(nameof(OffShoreColor)) ? (Vector4) config[nameof(OffShoreColor)] : defaultColors.OffShoreColor;
                SunDirection = config.ContainsKey(nameof(SunDirection)) ? (Vector3) config[nameof(SunDirection)] : defaultColors.SunDirection;
                SunPower = config.ContainsKey(nameof(SunPower)) ? (float) config[nameof(SunPower)] : defaultColors.SunPower;
                Brightness = config.ContainsKey(nameof(Brightness)) ? (float) config[nameof(Brightness)] : defaultColors.Brightness;
                Contrast = config.ContainsKey(nameof(Contrast)) ? (float) config[nameof(Contrast)] : defaultColors.Contrast;
                OceanWaterLevel = config.ContainsKey(nameof(OceanWaterLevel)) ? (float) config[nameof(OceanWaterLevel)] : defaultColors.OceanWaterLevel;
                Half = config.ContainsKey(nameof(Half)) ? (Vector3) config[nameof(Half)] : defaultColors.Half;
                WaterOffset = config.ContainsKey(nameof(WaterOffset)) ? (int) config[nameof(WaterOffset)] : 0;
            }
        }

        private class OverlayConfig
        {
            public int XPos { get; set; }
            public int YPos { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public byte[] Image { get; set; }
            public string DebugName { get; set; }
            
            public OverlayConfig()
            {
                
            }

            public OverlayConfig(Hash<string, object> data)
            {
                XPos = data.ContainsKey(nameof(XPos)) ? (int)data[nameof(XPos)] : 0;
                YPos = data.ContainsKey(nameof(YPos)) ? (int)data[nameof(YPos)] : 0;
                Width = data.ContainsKey(nameof(Width)) ? (int)data[nameof(Width)] : 0;
                Height = data.ContainsKey(nameof(Height)) ? (int)data[nameof(Height)] : 0;
                Image = data.ContainsKey(nameof(Image)) ? (byte[])data[nameof(Image)] : null;
                DebugName = data.ContainsKey(nameof(DebugName)) ? (string)data[nameof(DebugName)] : null;
            }
        }
        
        private struct Array2D<T>
        {
            public readonly T[] Items;

            public readonly int Width;

            public readonly int Height;

            public Array2D(T[] items, int width, int height)
            {
                Items = items;
                Width = width;
                Height = height;
            }
            
            public Array2D(int width, int height)
            {
                Items = new T[width * height];
                Width = width;
                Height = height;
            }
            
            public T this[int row, int col]
            {
                get
                {
                    if (row < 0 || row > Width - 1)
                    {
                        throw new IndexOutOfRangeException( $"Get row out of range at {row} Min: 0 Max: {Width - 1}");
                    }

                    if (col < 0 || col > Height - 1)
                    {
                        throw new IndexOutOfRangeException($"Get col out of range at {col} Min: 0 Max: {Height - 1}");
                    }
                    
                    return Items[col * Width + row];
                }
                set
                {
                    if (row < 0 || row > Width - 1)
                    {
                        throw new IndexOutOfRangeException( $"Set row out of range at {row} Min: 0 Max: {Width - 1}");
                    }

                    if (col < 0 || col > Height - 1)
                    {
                        throw new IndexOutOfRangeException($"Set col out of range at {col} Min: 0 Max: {Height - 1}");
                    }
                    
                    Items[col * Width + row] = value;
                }
            }

            public bool IsEmpty()
            {
                return Items == null || Width == 0 && Height == 0;
            }

            public Array2D<T> Splice(int startX, int startY, int width, int height)
            {
                if (startX < 0 || startX >= Width)
                {
                    throw new IndexOutOfRangeException($"startX is < 0 or greater than {Width}: {startX}");
                }

                if (startY < 0 || startY >= Height)
                {
                    throw new IndexOutOfRangeException($"startY is < 0 or greater than {Height}: {startY}");
                }

                if (width == 0 || startX + width > Width)
                {
                    throw new IndexOutOfRangeException($"width is < 0 or greater than {Width}: {width}");
                }

                if (height == 0 || startY + height > Height)
                {
                    throw new IndexOutOfRangeException($"height is < 0 or greater than {Height}: {height}");
                }
                
                Array2D<T> splice = new Array2D<T>(width, height);
                Array2D<T> copyThis = this;
                Parallel.For(0, width, x =>
                {
                    for (int y = 0; y < height; y++)
                    {
                        splice[x, y] = copyThis[startX + x, startY + y];
                    }
                });

                return splice;
            }

            public Array2D<T> Clone()
            {
                return new Array2D<T>((T[])Items.Clone(), Width, Height);
            }

        }
        #endregion
    }
}
