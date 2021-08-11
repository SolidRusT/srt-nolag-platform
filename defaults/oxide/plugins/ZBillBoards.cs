﻿using UnityEngine;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using System.Collections;
using UnityEngine.Networking;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using System;

namespace Oxide.Plugins
{
    [Info("ZBillBoards", "JOSH-Z", "1.2.0", ResourceId = 0)]
    [Description("Create huge (or small) billboards")]
    public class ZBillBoards : RustPlugin
    {

        private static ZBillBoards ins;
        private GameObject downloadControllerObject;
        private DownloadController downloadController;
        private GameObject pasteControllerObject;
        private PasteController pasteController;

        const string permAdmin = "zbillboards.admin";
        const string permConsole = "zbillboards.console";
        const string permTier1 = "zbillboards.tier1";
        const string permTier2 = "zbillboards.tier2";
        const string permTier3 = "zbillboards.tier3";
        const string dataFileName = "ZBillBoards";
       

        #region Hooks
        void Init()
        {
            if (!LoadConfigVariables())
            {
                Puts("An error was found in your config file!");
                return;
            }

            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permConsole, this);
            permission.RegisterPermission(permTier1, this);
            permission.RegisterPermission(permTier2, this);
            permission.RegisterPermission(permTier3, this);
        }

        void OnServerInitialized()
        {
            ins = this;

            downloadControllerObject = new GameObject("DownloadController");
            downloadController = downloadControllerObject.AddComponent<DownloadController>();

            pasteControllerObject = new GameObject("PasteController");
            pasteController = pasteControllerObject.AddComponent<PasteController>();

            ValidateAllBillboards();
        }

        void Unload()
        {
            SaveData();

            UnityEngine.Object.Destroy(downloadControllerObject);
            UnityEngine.Object.Destroy(pasteControllerObject);          
        }
        #endregion

        #region Download controller
        private static void ProcessImage(byte[] bytes, int targetWidth, int targetHeight, DownloadRequest request)
        {
            //byte[] bytesEditedImage;

            using (MemoryStream startBytes = new MemoryStream(), newBytes = new MemoryStream())
            {
                startBytes.Write(bytes, 0, bytes.Length);

                Bitmap image = new Bitmap(startBytes);
                Bitmap finalImage = new Bitmap(targetWidth, targetHeight);

                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(finalImage))
                {
                    graphics.DrawImage(image, new Rectangle(0, 0, targetWidth, targetHeight));
                }


                // worry about this later, very heavy because pixel per pixel
                //if (request.adjustBrightness)
                //{                    
                //    bytesEditedImage = RecolorImage(newBytes, targetWidth, targetHeight);
                //    newBytes.Write(bytesEditedImage, 0, bytesEditedImage.Length);
                //}

                finalImage.Save(newBytes, ImageFormat.Png);

                SplitImageAndPaste(request, finalImage, ins.configData.imageSize, ins.configData.imageSize);

                image.Dispose();
                finalImage.Dispose();
            }
        }


        private static void SplitImageAndPaste(DownloadRequest request, Bitmap sourceImage, int defWidth, int defHeight)
        {

            if(ins.configData.debug)
                Debug.Log("Image url: " + request.imageURL);

            if (request.targetSign == null) return;

            //if (!ins.storedData.billBoards.ContainsKey(request.targetSign.net.ID)) return;
            List<uint> billBoardSigns;
            if (!ins.storedData.billBoards.TryGetValue(request.targetSign.net.ID, out billBoardSigns)) return;

            var sTotCols = request.width;
            var curCol = 1;
            var curRow = 1;



            //foreach (uint signID in ins.storedData.billBoards[request.targetSign.net.ID])
            foreach (uint signID in billBoardSigns)
            {
                byte[] imagePart = GetImagePart(sourceImage, ins.configData.imageSize, ins.configData.imageSize, curCol, curRow);
                ins.pasteController.AddToPasteList(imagePart, signID, curCol, curRow);

                if (curCol == sTotCols)
                {
                    curCol = 1;
                    curRow++;
                }
                else
                {
                    curCol++;
                }
            }

            sourceImage.Dispose();

            ins.pasteController.PasteNextFromList();
        }


        private static byte[] RecolorImage(MemoryStream stream, int targetWidth, int targetHeight)
        {
            var bytes = stream.ToArray();

            float b = ins.configData.brightness;
            ColorMatrix cm = new ColorMatrix(new float[][]
            {
                new float[] {b, 0, 0, 0, 0},
                new float[] {0, b, 0, 0, 0},
                new float[] {0, 0, b, 0, 0},
                new float[] {0, 0, 0, 1, 0},
                new float[] {0, 0, 0, 0, 1},
            });
            ImageAttributes attributes = new ImageAttributes();
            attributes.SetColorMatrix(cm);

            using (MemoryStream originalBytesStream = new MemoryStream())
            {
                originalBytesStream.Write(bytes, 0, bytes.Length);
                Bitmap sourceImage = new Bitmap(originalBytesStream);
                Bitmap targetImage = new Bitmap(targetWidth, targetHeight);


                Point[] targetRectPoints = { new Point(0, 0), new Point(targetWidth, 0), new Point(0, targetHeight) };
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(targetImage))
                {
                    var sourceRectangle = new Rectangle(0, 0, targetWidth, targetHeight);
                    graphics.DrawImage(sourceImage, targetRectPoints, sourceRectangle, GraphicsUnit.Pixel, attributes);
                }

                var recoloredImageBytes = originalBytesStream.ToArray();
                //targetImage.Dispose();
                //sourceImage.Dispose();

                return recoloredImageBytes;
            }           
        }




        private class DownloadController : FacepunchBehaviour
        {            
            private byte downloading;
            private readonly Queue<DownloadRequest> downloadQueue = new Queue<DownloadRequest>();

            public void QueueDownload(string url, BasePlayer player, Signage sign, int width, int height, bool adjustBrightness)
            {
                downloadQueue.Enqueue(new DownloadRequest(url, player, sign, width, height, adjustBrightness));
                StartNewDownload();
            }

            private byte[] GetDownloadResponse(UnityWebRequest webRequest)
            {
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(webRequest.downloadHandler.data);

                byte[] image;
                image = texture.EncodeToPNG();

                DestroyImmediate(texture);
                return image;
            }

            private void StartNewDownload()
            {
                if (downloading >= 1) return;                
                if (downloadQueue.Count <= 0)
                {
                    if(ins.configData.debug)
                        Debug.Log("Billboard download queue empty, stopped Coroutine");
                    return;
                }
                downloading++;
                StartCoroutine(StartDownload(downloadQueue.Dequeue()));
            }

            private IEnumerator StartDownload(DownloadRequest request)
            {
                Debug.Log("Billboard image download started for " + request.player.displayName + " - Image url: " + request.imageURL);

                UnityWebRequest webRequest = UnityWebRequest.Get(request.imageURL);
                yield return webRequest.SendWebRequest();

                if (webRequest.isNetworkError || webRequest.isHttpError)
                {
                    Debug.Log("Billboard image download failed for image url: " + request.imageURL + " - Error: " + (webRequest.error != null ? webRequest.error : "N/A"));

                    webRequest.Dispose();
                    StartNewDownload();
                    yield break;
                }

                byte[] downloadResponse;
                downloadResponse = GetDownloadResponse(webRequest);

                int defWidth = ins.configData.imageSize * request.width;
                int defHeight = ins.configData.imageSize * request.height;

                ProcessImage(downloadResponse, defWidth, defHeight, request);

                //if(request.adjustBrightness)
                //    resizedImageBytes = RecolorImage(downloadResponse, defWidth, defHeight);


                downloading--;

                StartNewDownload();
                webRequest.Dispose();

            }                   
        }

        private class DownloadRequest
        {
            public BasePlayer player { get; }
            public Signage targetSign { get; }
            public string imageURL { get; set; }
            public int width { get; set; } = 1;
            public int height { get; set; } = 1;
            public bool adjustBrightness { get; set; }

            public DownloadRequest(string url, BasePlayer inputPlayer, Signage inputSign, int inputWidth, int inputHeight, bool inpBrightness)
            {
                imageURL = url;
                player = inputPlayer;
                targetSign = inputSign;
                width = inputWidth;
                height = inputHeight;
                adjustBrightness = inpBrightness;
            }
        }
        #endregion

        #region Paste Controller
        private class PasteController : FacepunchBehaviour
        {
            private bool isPasting = false;
            private List<PasteRequest> pasteList = new List<PasteRequest>();

            public void AddToPasteList(byte[] imgData, uint signID, int coordsX, int coordsY)
            {
                pasteList.Add(new PasteRequest(imgData, signID, coordsX, coordsY));
            }

            public void PasteNextFromList()
            {
                if (isPasting) return;
                if (pasteList.Count == 0) return;

                isPasting = true;

                var request = pasteList[0];
                pasteList.RemoveAt(0);

                PasteImage(request);
            }

            public void ClearPasteList()
            {
                isPasting = true;
                pasteList.Clear();
                isPasting = false;
            }

            public int GetEmptyFrame(Signage sign)
            {
                for (int index = 0; index < sign.textureIDs.Length; index++)
                {
                    if (sign.textureIDs[index] == 0)
                        return index;
                }
                return 0;
            }

            private void PasteImage(PasteRequest request)
            {
                Signage targetSign = BaseNetworkable.serverEntities.Find(request.signID) as Signage;
                if (targetSign == null)
                    return;

                targetSign.EnsureInitialized();

                var frameNumber = GetEmptyFrame(targetSign);
                var currentTextureID = targetSign.textureIDs[frameNumber];

                if (currentTextureID != 0)
                    FileStorage.server.RemoveExact(currentTextureID, FileStorage.Type.png, targetSign.net.ID, (uint)frameNumber);

                var textureId = FileStorage.server.Store(request.imgData, FileStorage.Type.png, targetSign.net.ID, (uint)frameNumber);
                targetSign.textureIDs[frameNumber] = textureId;
                targetSign.SendNetworkUpdate();

                if(ins.configData.debug)
                    Debug.Log("Pasted image on sign " + targetSign.net.ID + ", " + ins.pasteController.pasteList.Count + " signs left");


                isPasting = false;

                Invoke("PasteNextFromList", ins.configData.pasteDelay);
            }
        }

        private class PasteRequest
        {
            public uint signID { get; }
            public byte[] imgData { get; }
            public int signX { get; }
            public int signY { get; }

            public PasteRequest(byte[] inpImgData, uint inpSignID, int coordsX, int coordsY)
            {
                imgData = inpImgData;
                signID = inpSignID;
                signX = coordsX;
                signY = coordsY;
            }
        }

        private static byte[] GetImagePart(Bitmap sourceImage, int targetWidth, int targetHeight, int splitX, int splitY)
        {
            byte[] bytesClipped;

            var streamResized = new MemoryStream();
            Bitmap targetImage = new Bitmap(targetWidth, targetHeight);

            int pixelsX = (splitX - 1) * targetWidth;
            int pixelsY = (splitY - 1) * targetHeight;
            Point[] targetRectPoints = { new Point(0, 0), new Point(targetWidth, 0), new Point(0, targetHeight) };

            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(targetImage))
            {
                var sourceRectangle = new Rectangle(pixelsX, pixelsY, targetWidth, targetHeight);
                graphics.DrawImage(sourceImage, targetRectPoints, sourceRectangle, GraphicsUnit.Pixel);
            }

            AddRandomPixel(targetImage);

            targetImage.Save(streamResized, ImageFormat.Png);
            bytesClipped = streamResized.ToArray();
            targetImage.Dispose();

            return bytesClipped;
        }

        #endregion

        #region Commands
        [ConsoleCommand("billboard.toggle")]
        private void consoleCmdZDP(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;

            if (!HasPermission(player.UserIDString, permConsole))
                return;

            if (arg.Args.Length > 0)
            {
                uint posID;
                if (!uint.TryParse(arg.Args[0], out posID)) return;

                NeonSign mainSign = GetMainSign(posID);
                if (mainSign == null) return;

                if (configData.debug)
                    Puts("Console command used to toggle power on billboard " + posID);

                SwitchBillBoardPower(mainSign);
            }
        }

        [ChatCommand("billboard")]
        private void cmdBillBoard(BasePlayer player, string command, string[] args)
        {
            var maxSigns = 0;
            if(HasPermission(player.UserIDString, permAdmin)) { maxSigns = configData.maxSignsAdmin; }
            else if (HasPermission(player.UserIDString, permTier3)) { maxSigns = configData.maxSignsTier3; }
            else if (HasPermission(player.UserIDString, permTier2)) { maxSigns = configData.maxSignsTier2; }
            else if (HasPermission(player.UserIDString, permTier1)) { maxSigns = configData.maxSignsTier1; }
            else
            {
                player.ChatMessage("No permission");
                return;
            }

            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "create": CreateBillBoard(player, args, maxSigns); break;
                    case "remove":
                    case "destroy": DestroyBillBoard(player); break;
                    case "destroyall": DestroyBillBoards(player); break;
                    case "info": ShowBillboardInfo(player); break;
                    case "debug": ToggleDebug(player); break;
                    case "toggle": ToggleBillBoardPower(player); break;
                    case "speed": ChangeBillBoardSpeed(player, args); break;
                    case "dimmer": ChangeBillBoardDimmer(player, args); break;
                    case "next": CommandPasteNext(player); break;
                    case "emptypaste": CommandPasteEmpty(player); break;
                    case "sil": SilBillBoard(player, args); break;
                }
            }
        }
        #endregion

        #region Functions
        public void ValidateAllBillboards()
        {
            var allBB = new Dictionary<uint, List<uint>>(storedData.billBoards);
            Puts("Validating " + allBB.Count + " billboards...");
            foreach(var mainSign  in allBB)
            {
                ValidateBillboard(mainSign.Key);               
            }

            Puts("All billboards validated");
        }

        public void ValidateBillboard(uint mainSignID)
        {
            List<uint> signList;
            if(!storedData.billBoards.TryGetValue(mainSignID, out signList))
            {
                Puts("Trying to validate a non-existing billboard");
                return;
            }            

            Signage signage = BaseNetworkable.serverEntities.Find(mainSignID) as Signage;
            if (signage == null)
            {
                storedData.billBoards.Remove(mainSignID);
                Puts("Deleted billboard " + mainSignID + " as it does not exist anymore");
                return;
            }

            Puts("Billboard " + mainSignID + " has " + signList.Count + " signs, validating...");
            foreach (var subSign in signList)
            {
                Signage subSignage = BaseNetworkable.serverEntities.Find(subSign) as Signage;
                if (subSignage == null)
                {
                    storedData.billBoards[mainSignID].Remove(subSign);
                    Puts("Deleted sign " + subSign + " from billboard " + mainSignID);
                    continue;
                }

                subSignage.UpdateHasPower(0, 0);
                subSignage.UpdateHasPower(25, 0);
                subSignage.SendNetworkUpdate();
            }
            Puts("Billboard validated");
        }

        public void ToggleBillBoardPower(BasePlayer player)
        {
            var billboardSign = GetBillBoard(player);
            if (billboardSign == null) return;

            SwitchBillBoardPower(billboardSign);

            player.ChatMessage("Billboard power toggled");
            return;
        }

        public void ToggleDebug(BasePlayer player = null)
        {
            configData.debug = !configData.debug;
            SaveConf();

            player.ChatMessage("Debug mode set to: " + configData.debug);
            return;
        }

        private void SwitchBillBoardPower(NeonSign billboardSign)
        {
            var newPower = billboardSign.IsPowered() ? 0 : 25;

            List<uint> billBoardSigns;
            if (!ins.storedData.billBoards.TryGetValue(billboardSign.net.ID, out billBoardSigns)) return;


           // foreach (uint signID in storedData.billBoards[billboardSign.net.ID])
            foreach (uint signID in billBoardSigns)
            {
                Signage signage = BaseNetworkable.serverEntities.Find(signID) as Signage;
                if (signage == null) continue;

                Interface.CallHook("OnBillboardPowerToggled", billboardSign.net.ID, signage, newPower > 0);

                signage.UpdateHasPower(newPower, 0);
                signage.SendNetworkUpdate();
            }
        }

        public void ChangeBillBoardSpeed(BasePlayer player, string[] args)
        {
            var billboardSign = GetBillBoard(player);
            if (billboardSign == null) return;

            if (args.Length != 2)
            {
                player.ChatMessage("Wrong usage: /billboard speed 3");
                return;
            }

            float newSpeed = 1f;
            float speed = 1f;
            if (float.TryParse(args[1], out speed) && speed >= 0) newSpeed = speed;


            List<uint> billBoardSigns;
            if (!ins.storedData.billBoards.TryGetValue(billboardSign.net.ID, out billBoardSigns)) return;

            //foreach (uint signID in storedData.billBoards[billboardSign.net.ID])
            foreach (uint signID in billBoardSigns)
            {
                NeonSign signage = BaseNetworkable.serverEntities.Find(signID) as NeonSign;
                if (signage == null) continue;

                signage.animationSpeed = newSpeed;
                signage.SendNetworkUpdate();                
            }

            SwitchBillBoardPower(billboardSign);
            SwitchBillBoardPower(billboardSign);

            player.ChatMessage("Billboard speed set to: "+ newSpeed);
            return;
        }

        public void ChangeBillBoardDimmer(BasePlayer player, string[] args)
        {
            var billboardSign = GetBillBoard(player);
            if (billboardSign == null) return;

            if (args.Length != 2)
            {
                player.ChatMessage("Wrong usage: /billboard dimmer 2");
                return;
            }

            float newIntensity = 2f;
            float intensity = 1f;
            if (float.TryParse(args[1], out intensity) && intensity >= 0) newIntensity = intensity;


            List<uint> billBoardSigns;
            if (!ins.storedData.billBoards.TryGetValue(billboardSign.net.ID, out billBoardSigns)) return;

      
            //foreach (uint signID in storedData.billBoards[billboardSign.net.ID])
            foreach (uint signID in billBoardSigns)
            {
                NeonSign signage = BaseNetworkable.serverEntities.Find(signID) as NeonSign;
                if (signage == null) return;

                signage.lightIntensity = newIntensity;
                signage.SendNetworkUpdate();
            }

            player.ChatMessage("Billboard dimmer set to: " + newIntensity);
            return;
        }

        public void SilBillBoard(BasePlayer player, string[] args)
        {
            if (args.Length < 4)
            {
                //player.ChatMessage("Incorrect syntax: /billboard sil 3 2 <url> [darker]");
                player.ChatMessage("Incorrect syntax: /billboard sil 3 2 <url>");
                return;
            }

            int cols;
            int rows;
            if (!int.TryParse(args[1], out cols) || !int.TryParse(args[2], out rows))
            {
                player.ChatMessage("Incorrect syntax: /billboard sil 3 2 <url>");
                return;
            }

            bool adjustBrightness = false;
            if (args.Length > 4 && args[4] == "true")
                adjustBrightness = true;

            var startSign = GetBillBoard(player);
            if (startSign == null) return;

            downloadController.QueueDownload(args[3], player, startSign, cols, rows, adjustBrightness);
            player.ChatMessage("Loading images...");           
        }

        public void DestroyBillBoards(BasePlayer player)
        {
            if (!HasPermission(player.UserIDString, permAdmin))
                return;            

            foreach (KeyValuePair<uint, List<uint>> billboard in storedData.billBoards)
            {
                foreach (uint signID in billboard.Value)
                {
                    BaseEntity ent = BaseNetworkable.serverEntities.Find(signID) as BaseEntity;
                    if (ent != null)
                    {
                        ent.Kill();
                    }
                }
            }
            storedData.billBoards.Clear();
        }

        public void DestroyBillBoard(BasePlayer player)
        {
            var sign = GetBillBoard(player);
            if (sign == null) return;


            uint sid = sign.net.ID;
            List<uint> billBoardSigns;
            if (!ins.storedData.billBoards.TryGetValue(sid, out billBoardSigns)) return;

            //foreach (uint signID in storedData.billBoards[sign.net.ID])
            foreach (uint signID in billBoardSigns)
            {
                BaseEntity ent = BaseNetworkable.serverEntities.Find(signID) as BaseEntity;
                if (ent != null) ent.Kill();                
            }
            storedData.billBoards.Remove(sid);

            player.ChatMessage("Billboard destroyed");
        }

        public NeonSign GetBillBoard(BasePlayer player, bool checkIfBillBoard = true)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 6f))
            {
                player.ChatMessage("Please look at a billboard!");
                return null;
            }
            
            NeonSign billboardSign = null;
            billboardSign = hit.GetEntity() as NeonSign;
            if (billboardSign == null || !billboardSign.ShortPrefabName.Contains("sign.neon.xl.animated"))
            {
                player.ChatMessage("Cannot find Billboard / XL Animated Neon sign!");
                return null;
            }

            if(!checkIfBillBoard)
            {
                return billboardSign;
            }

            NeonSign mainSign = GetMainSign(billboardSign.net.ID);
            if (checkIfBillBoard && mainSign == null)
            {
                player.ChatMessage("Please look at a billboard!");
                return null;
            }
           
            return mainSign;            
        }

        public NeonSign GetMainSign(uint raySignID)
        {
            uint parentSignID = 0;

            foreach (KeyValuePair<uint, List<uint>> billboard in storedData.billBoards)
            {
                foreach (uint subSignID in billboard.Value)
                {
                    if(subSignID == raySignID)
                    {
                        parentSignID = billboard.Key;
                        break;
                    }                    
                }

                if (parentSignID != 0) break;
            }

            if (parentSignID == 0)
                return null;

            NeonSign ent = BaseNetworkable.serverEntities.Find(parentSignID) as NeonSign;
            if (ent == null)
                return null;

            return ent;        
        }

        public void ShowBillboardInfo(BasePlayer player)
        {
            var billboard = GetBillBoard(player);
            if(billboard != null)
                player.ChatMessage("Billboard ID: " + billboard.net.ID + "\nLocation: " + billboard.transform.position);
        }

        public void CreateBillBoard(BasePlayer player, string[] args, int maxSigns)
        {
            int horizontal = 3;
            int vertical = 2;

            if (args.Length == 3)
            {
                int inputH;
                int inputV;
                if (int.TryParse(args[1], out inputH) && int.TryParse(args[2], out inputV) && inputH > 0 && inputV > 0 && (inputH * inputV) < maxSigns)
                {
                    horizontal = inputH;
                    vertical = inputV;
                }
            }


            if(!HasPermission(player.UserIDString, permAdmin) && player.IsBuildingBlocked())
            {
                player.ChatMessage("You can not create billboards in building blocked zones");
                return;
            }

            if(horizontal * vertical > maxSigns)
            {
                player.ChatMessage("The total of signs can not be higher than " + maxSigns);
                return;
            }


            var startSign = GetBillBoard(player, false);
            if (startSign == null)
            {
                player.ChatMessage("Can not find the main billboard sign");
                return;
            }



            if (storedData.billBoards.ContainsKey(startSign.net.ID))
            {
                player.ChatMessage("This is already a billboard");
                return;
            }

            storedData.billBoards.Add(startSign.net.ID, new List<uint>());
            storedData.billBoards[startSign.net.ID].Add(startSign.net.ID);


            if (configData.lockSigns)
                startSign.SetFlag(BaseEntity.Flags.Locked, true, false, true);

            startSign.UpdateHasPower(25, 0);
            startSign.SendNetworkUpdate();

            for (int col = 0; col < vertical; col++)
            {
                for (int row = 0; row < horizontal; row++)
                {
                    if (row == 0 && col == 0) continue;

                    var newRotation = startSign.transform.rotation;
                    var newPosition = startSign.transform.position + (startSign.transform.right * row * -2.5f) + (startSign.transform.up * col * -2.5f);
                    NeonSign sign = GameManager.server.CreateEntity("assets/prefabs/misc/xmas/neon_sign/sign.neon.xl.animated.prefab", newPosition, newRotation) as NeonSign;
                    if (sign == null) return;
                    sign.Spawn();
                    sign.OwnerID = player.userID;

                    if (configData.lockSigns)
                        sign.SetFlag(BaseEntity.Flags.Locked, true, false, true);

                    sign.UpdateHasPower(25, 0);
                    sign.SendNetworkUpdate();

                    storedData.billBoards[startSign.net.ID].Add(sign.net.ID);
                    
                    if(configData.debug)
                        Puts("Billboard part " + row + " x " + col + " spawned at " + newPosition);
                }
            }
        }

        public void CommandPasteNext(BasePlayer player)
        {
            if (!HasPermission(player.UserIDString, permAdmin))
                return;

            if (pasteController != null)
            {
                pasteController.PasteNextFromList();
                player.ChatMessage("Pasting next frame...");
                return;
            }
        }

        public void CommandPasteEmpty(BasePlayer player)
        {
            if (!HasPermission(player.UserIDString, permAdmin))
                return;

            if (pasteController != null)
            {
                pasteController.ClearPasteList();
                player.ChatMessage("PasteList cleared...");
                return;
            }
        }
        #endregion

        #region Helpers
        public UnityEngine.Random rnd = new UnityEngine.Random();
        private static void AddRandomPixel(Bitmap image)
        {
            var color = System.Drawing.Color.FromArgb(
                UnityEngine.Random.Range(0, 255),
                UnityEngine.Random.Range(0, 255),
                UnityEngine.Random.Range(0, 255),
                128 //Alpha (transparency)
            );
            image.SetPixel(image.Width - 1, 1, color);
        }

        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        #endregion

        #region Config
        private ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Maximum amount of signs in total (width x height) Tier 1")]
            public int maxSignsTier1 = 6;

            [JsonProperty(PropertyName = "Maximum amount of signs in total (width x height) Tier 2")]
            public int maxSignsTier2 = 12;

            [JsonProperty(PropertyName = "Maximum amount of signs in total (width x height) Tier 3")]
            public int maxSignsTier3 = 16;

            [JsonProperty(PropertyName = "Maximum amount of signs in total (width x height) Admin")]
            public int maxSignsAdmin = 150;

            [JsonProperty(PropertyName = "Width and height of each neon sign image in pixels")]
            public int imageSize = 150;

            [JsonProperty(PropertyName = "Seconds between pasting images")]
            public float pasteDelay = 0.5f;

            [JsonProperty(PropertyName = "Brightness of the images pasted on the billboards (experimental)")]
            public float brightness = 0.5f;

            [JsonProperty(PropertyName = "Lock signs to owner after creating billboard")]
            public bool lockSigns = false;

            [JsonProperty(PropertyName = "Log extra output to console")]
            public bool debug = false;
        }

        private bool LoadConfigVariables()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                return false;
            }
            SaveConf();
            return true;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            configData = new ConfigData();
            SaveConf();
        }

        void SaveConf() => Config.WriteObject(configData, true);
        #endregion

        #region Data
        StoredData storedData;
        class StoredData
        {
            public Dictionary<uint, List<uint>> billBoards = new Dictionary<uint, List<uint>>();           
        }

        void Loaded()
        {
            LoadData();
        }

        public void LoadData()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(dataFileName);
            }
            catch
            {
                storedData = new StoredData();
                Puts("Invalid datafile, new file generated");

                SaveData();
            }

            if (storedData == null)
            {
                storedData = new StoredData();
                Puts("Invalid datafile, new file generated");

                SaveData();
            }
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(dataFileName, storedData);
        }
        #endregion
    }
}