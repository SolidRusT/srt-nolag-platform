using Facepunch;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Casino", "k1lly0u", "0.1.10")]
    [Description("Core card game and table management system")]
    public class Casino : RustPlugin
    {
        #region Fields        
        [PluginReference]
        private Plugin ServerRewards, Economics, ImageLibrary;

        private readonly Hash<GameType, Action<BaseEntity, StoredData.GameData>> registeredGames = new Hash<GameType, Action<BaseEntity, StoredData.GameData>>();

        private bool wipeData = false;

        public static Casino Instance { get; private set; }

        public static PlayingCards Images { get; private set; }

        private static List<CardGame> ActiveGames;

        private static readonly KeyValuePair<Vector3, Vector3>[] ChairPositions = new KeyValuePair<Vector3, Vector3>[]
        {
            new KeyValuePair<Vector3, Vector3>(new Vector3(0.5f, 0, -1.1f), new Vector3(0, 0, 0)),
            new KeyValuePair<Vector3, Vector3>(new Vector3(-0.5f, 0, -1.1f), new Vector3(0, 0, 0)),
            new KeyValuePair<Vector3, Vector3>(new Vector3(0.5f, 0, 1.1f), new Vector3(0, 180, 0)),
            new KeyValuePair<Vector3, Vector3>(new Vector3(-0.5f, 0, 1.1f), new Vector3(0, 180, 0))
        };

        private const string CHAIR_PREFAB = "assets/prefabs/deployable/chair/chair.deployed.prefab";

        private const string TABLE_PREFAB = "assets/prefabs/deployable/table/table.deployed.prefab";

        public enum GameType { BlackJack, Roulette, Baccarat }

        public enum GameState { Waiting, PlacingBets, Prestart, Playing }

        public enum BettingType { Item, ServerRewards, Economics }
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            Instance = this;

            ActiveGames = new List<CardGame>();            
        }

        protected override void LoadDefaultMessages() => lang.RegisterMessages(new Dictionary<string, string>
        {
            ["Error.NotEnoughAmount"] = "<color=#D3D3D3>You do not have have {0} to play this table. Minimum bet is :</color> <color=#ce422b>{1}</color>",
            ["Table.Information"] = "<size=25>{0}</size><size=20>\nMin Bet: {1} {3}\nMax Bet: {2} {3}\nTake a seat to play!</size>",
            ["Bet.RP"] = "RP",
            ["Bet.Eco"] = "Eco"
        }, this);

        private void OnServerInitialized()
        {
            LoadData();

            if (wipeData)
            {
                storedData.tables.Clear();
                SaveData();
            }

            timer.In(3f, ()=> LoadImages(0));
        }

        private void LoadImages(int attempts = 0)
        {
            if (!ImageLibrary)
            {
                if (attempts > 3)
                {
                    Puts("[Casino] - This plugin requires ImageLibrary to manage the card images. Please install ImageLibrary to continue...");
                    return;
                }

                timer.In(10, () => LoadImages(attempts++));
                return;
            }

            Images = new PlayingCards(ImageLibrary);
            Images.ImportCardImages(registeredGames.Keys, Configuration.ForceUpdate, LoadTables);
        }

        private void OnNewSave(string filename) => wipeData = true;

        private void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            if (!mountable)
                return;

            GameChair gameChair = mountable.GetComponent<GameChair>();
            if (gameChair == null)
                return;

            CardGame cardGame = gameChair.CardGame;
            if (cardGame == null)
                return;

            int position = cardGame.GetChairPosition(mountable);
            if (position == -1)
            {
                PrintError("Player tried mounting a CardGame with a chair position of -1. Perhaps a old chair that was cleaned up properly? Tell k1lly0u");
                return;
            }
            cardGame.OnPlayerEnter(player, position);
            gameChair.OnPlayerEnter(player);
        }

        private object CanMountEntity(BasePlayer player, BaseMountable mountable)
        {
            CardGame cardGame = mountable.GetComponent<GameChair>()?.CardGame;
            if (cardGame != null)
            {
                int amount = cardGame.GetUserAmount(player);
                if (amount < cardGame.Data.minimumBet)
                {
                    SendReply(player, string.Format(lang.GetMessage("Error.NotEnoughAmount", this, player.UserIDString), cardGame.FormatBetString(player), cardGame.Data.minimumBet));
                    return false;
                }
            }
            return null;
        }

        private object CanDismountEntity(BasePlayer player, BaseMountable mountable)
        {
            CardGame cardGame = mountable.GetComponent<GameChair>()?.CardGame;
            if (cardGame != null)
            {
                if (!cardGame.CanDismountStandard())
                {
                    CardPlayer cardPlayer = player.GetComponent<CardPlayer>();
                    if (cardPlayer != null && cardPlayer.IsLeaving)
                        return null;

                    return false;
                }
            }
            return null;
        }

        private void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            GameChair gameChair = mountable.GetComponent<GameChair>();
            if (gameChair == null)
                return;

            CardGame cardGame = gameChair.CardGame;
            if (cardGame == null)
                return;

            cardGame.OnPlayerExit(player);
            gameChair.OnPlayerExit(player);
        }

        private object OnEntityTakeDamage(BaseCombatEntity baseCombatEntity, HitInfo info)
        {
            if (!baseCombatEntity.IsValid())
                return null;

            if (storedData.IsRegisteredTable(baseCombatEntity.net.ID))
                return true;

            if (baseCombatEntity.GetComponent<CardGame>() || baseCombatEntity.GetComponent<GameChair>())
                return true;            

            BasePlayer player = baseCombatEntity.ToPlayer();
            if (player != null && GameFromPlayer(player))
                return true;                       

            return null;
        }

        private void OnEntityKill(BaseNetworkable baseNetworkable)
        {
            GameChair gameChair = baseNetworkable.GetComponent<GameChair>();
            if (gameChair == null)
                return;

            CardGame cardGame = gameChair.CardGame;
            int number = gameChair.Number;

            timer.In(1f, () =>
            {
                if (cardGame == null)
                    return;

                cardGame.CreateSeat(number);
            });
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            CardGame cardGame = GameFromPlayer(player);
            if (cardGame == null)
                return;

            cardGame.OnPlayerExit(player);
        }

        private void Unload()
        {
            Unsubscribe(nameof(OnEntityKill));

            for (int i = 0; i < ActiveGames.Count; i++)            
                UnityEngine.Object.Destroy(ActiveGames[i]);
            
            ActiveGames.Clear();

            Instance = null;
            Images = null;
            Configuration = null;
        }
        #endregion

        #region Game Registration
        public void RegisterGame(GameType gameType, Action<BaseEntity, StoredData.GameData> callback) => registeredGames[gameType] = callback;

        public void UnregisterGame(GameType gameType)
        {
            registeredGames.Remove(gameType);

            for (int i = ActiveGames.Count - 1; i >= 0; i--)
            {
                CardGame cardGame = ActiveGames[i];
                if (cardGame.Type == gameType)
                {
                    ActiveGames.Remove(cardGame);
                    UnityEngine.Object.Destroy(cardGame);                    
                }
            }
        }
        #endregion

        #region Table Restoration
        private void LoadTables()
        {
            List<uint> allTables = storedData.tables.Keys.ToList();

            BaseEntity[] objects = BaseEntity.saveList.Where(x => x is DecorDeployable).ToArray();
            if (objects != null)
            {
                foreach (BaseEntity baseEntity in objects)
                {
                    if (baseEntity == null || !baseEntity.IsValid() || baseEntity.IsDestroyed)
                        continue;

                    if (storedData.IsRegisteredTable(baseEntity.net.ID))
                    {
                        StoredData.GameData gameData = storedData.tables[baseEntity.net.ID];
                        if (registeredGames.ContainsKey(gameData.gameType))
                        {
                            CreateCardGame(baseEntity, gameData);
                            allTables.Remove(baseEntity.net.ID);
                        }
                    }
                }
            }

            bool save = false;
            for (int i = 0; i < allTables.Count; i++)
            {
                uint netId = allTables[i];
                StoredData.GameData gameData = storedData.tables[netId];

                Vector3 position = gameData.Position;
                Quaternion rotation = Quaternion.Euler(gameData.Rotation);

                if (position == Vector3.zero)
                    continue;

                if (!registeredGames.ContainsKey(gameData.gameType))
                {
                    Debug.LogError($"Failed to create card game {gameData.gameType} ({gameData.Position}). Is the respective card game plugin loaded?");
                    continue;
                }

                DecorDeployable tableEntity = GameManager.server.CreateEntity(TABLE_PREFAB, position, rotation) as DecorDeployable;
                tableEntity.skinID = gameData.skinId;
                tableEntity.Spawn();

                CreateCardGame(tableEntity, gameData);

                storedData.tables.Remove(netId);
                storedData.tables.Add(tableEntity.net.ID, gameData);
                save = true;
            }

            if (save)
                SaveData();
        }
        #endregion

        #region Game Setup
        private void CreateCardGame(BaseEntity baseEntity, StoredData.GameData gameData)
        {
            if (!registeredGames.ContainsKey(gameData.gameType))
            {
                Debug.LogError($"Failed to create card game {gameData.gameType} ({gameData.Position}). Is the respective card game plugin loaded?");
                return;
            }

            DestroyComponents(baseEntity);
            
            registeredGames[gameData.gameType].Invoke(baseEntity, gameData);
        }

        private static BaseMountable CreateSeatEntity(BaseEntity entity, int number, CardGame cardgame)
        {
            Vector3 position = entity.transform.position + (entity.transform.rotation * ChairPositions[number].Key);
            Quaternion rotation = entity.transform.rotation * Quaternion.Euler(ChairPositions[number].Value);

            BaseMountable baseMountable = GameManager.server.CreateEntity(CHAIR_PREFAB, position, rotation) as BaseMountable;
            baseMountable.enableSaving = false;
            baseMountable.canWieldItems = false;
            baseMountable.pickup.enabled = false;
            baseMountable.skinID = Configuration.ChairSkinID;

            baseMountable.Spawn();

            DestroyComponents(baseMountable);

            baseMountable.gameObject.AddComponent<GameChair>().SetCardGame(cardgame, number);
            return baseMountable;
        }
        #endregion

        #region Helpers
        private CardGame GameFromPlayer(BasePlayer player)
        {
            CardPlayer cardPlayer = player.GetComponent<CardPlayer>();
            if (cardPlayer == null)
                return null;

            return cardPlayer.CardGame;
        }

        private BaseEntity FindEntityFromRay(BasePlayer player)
        {
            Ray ray = new Ray(player.eyes.position, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 3f))
                return null;

            BaseEntity hitEnt = hit.collider.GetComponentInParent<BaseEntity>();
            if (hitEnt != null)
                return hitEnt;
            return null;
        }

        private static void DestroyComponents(BaseEntity baseEntity)
        {
            BaseCombatEntity baseCombatEntity = baseEntity as BaseCombatEntity;
            if (baseCombatEntity != null)            
                baseCombatEntity.pickup.enabled = false;
            
            DecayEntity decayEntity = baseEntity as DecayEntity;
            if (decayEntity != null)
                decayEntity.decay = null;

            StabilityEntity stabilityEntity = baseEntity as StabilityEntity;
            if (stabilityEntity != null)
            {
                stabilityEntity.grounded = true;
                stabilityEntity.cachedStability = 1f;
                stabilityEntity.cachedDistanceFromGround = 1;
            }

            UnityEngine.Object.Destroy(baseEntity.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.Destroy(baseEntity.GetComponent<GroundWatch>());
        }

        private static T ParseType<T>(string type)
        {
            try
            {
                T value = (T)Enum.Parse(typeof(T), type, true);
                if (Enum.IsDefined(typeof(T), value))
                    return value;
            }
            catch
            {                
                return default(T);
            }
            return default(T);
        }
        #endregion        

        #region Base Game
        public class CardGame : MonoBehaviour
        {
            public DecorDeployable Table { get; private set; }

            public StoredData.GameData Data { get; private set; }

            public TimerElement Timer { get; private set; }

            public TableInformer Informer { get; private set; }

            public GameState State { get; set; } = GameState.Waiting;

            public CardPlayer[] Players { get; private set; }

            private BaseMountable[] Seats { get; set; }

            public virtual GameType Type { get; }

            public void Awake()
            {
                Table = GetComponent<DecorDeployable>();
                Timer = gameObject.AddComponent<TimerElement>();
                Informer = gameObject.AddComponent<TableInformer>();

                enabled = false;
            }

            public virtual void OnDestroy()
            {
                Destroy(Informer);

                for (int i = 0; i < Players.Length; i++)
                {
                    CardPlayer cardPlayer = Players[i];
                    if (cardPlayer != null)
                        Destroy(cardPlayer);
                }

                for (int i = Seats.Length - 1; i >= 0; i--)
                {
                    BaseMountable baseMountable = Seats[i];
                    if (baseMountable != null)
                    {
                        baseMountable.DismountAllPlayers();
                        baseMountable.Kill(BaseNetworkable.DestroyMode.None);
                    }
                }

                Destroy(Timer);
            }

            public void SetTableSkinID(ulong skinID)
            {
                if (Table.skinID == skinID || skinID == 0UL)
                    return;

                Table.skinID = skinID;
                Table.SendNetworkUpdateImmediate();

                Table.ClientRPC<int, uint>(null, "Client_ReskinResult", 1, Table.net.ID);
            }

            #region Game Initialization
            public void InitializeGame(StoredData.GameData gameData)
            {
                Data = gameData;
                Data.InitializeBetData();

                Players = new CardPlayer[gameData.maxPlayers];
                Seats = new BaseMountable[gameData.maxPlayers];

                for (int i = 0; i < gameData.maxPlayers; i++)
                    CreateSeat(i);

                OnGameInitialized();
            }

            public void CreateSeat(int number) => Seats[number] = CreateSeatEntity(Table, number, this);

            public virtual void OnGameInitialized()
            {
                Informer.OnGameInitialized();

                ActiveGames.Add(this);
            }
            #endregion

            public virtual void OnPlayerEnter(BasePlayer player, int position) { }

            public virtual void OnPlayerExit(BasePlayer player) { }

            public virtual bool CanDismountStandard() => false;

            public int GetChairPosition(BaseMountable baseMountable)
            {
                for (int i = 0; i < Seats.Length; i++)
                {
                    if (Seats[i].EqualNetID(baseMountable))
                        return i;
                }
                return -1;
            }

            public int CurrentPlayerCount
            {
                get
                {
                    int count = 0;
                    for (int i = 0; i < Players.Length; i++)
                    {
                        if (Players[i] != null)
                            count++;
                    }
                    return count;
                }
            }

            public int CurrentPlayingCount
            {
                get
                {
                    int count = 0;
                    for (int i = 0; i < Players.Length; i++)
                    {
                        if (Players[i] != null && Players[i].IsPlaying)
                            count++;
                    }
                    return count;
                }
            }

            #region Betting  
            public void AdjustBet(CardPlayer cardPlayer, int amount, int minimum, int maximum)
            {
                if (State != GameState.PlacingBets)
                    return;

                amount = Mathf.Clamp(amount + cardPlayer.BetAmount, minimum, maximum);
                if (cardPlayer.BankBalance < amount)
                    return;

                cardPlayer.BetAmount = amount;
            }

            public void ResetBet(CardPlayer cardPlayer)
            {
                if (State != GameState.PlacingBets)
                    return;

                cardPlayer.BetAmount = Data.minimumBet;
            }

            public int GetUserAmount(BasePlayer player)
            {
                switch (Data.BetType)
                {
                    case BettingType.Item:
                        return player.inventory.GetAmount(Data.BetItemID);
                    case BettingType.ServerRewards:
                        return (int)Instance.ServerRewards?.Call("CheckPoints", player.userID);
                    case BettingType.Economics:
                        return Convert.ToInt32((double)Instance.Economics?.Call("Balance", player.UserIDString));
                }
                return 0;
            }

            public void TakeAmount(BasePlayer player, int amount)
            {
                switch (Data.BetType)
                {
                    case BettingType.Item:
                        player.inventory.Take(null, Data.BetItemID, amount);
                        break;
                    case BettingType.ServerRewards:
                        Instance.ServerRewards?.Call("TakePoints", player.userID, amount);
                        break;
                    case BettingType.Economics:
                        Instance.Economics?.Call("Withdraw", player.UserIDString, (double)amount);
                        break;
                }
            }

            public void GiveAmount(BasePlayer player, int amount)
            {
                switch (Data.BetType)
                {
                    case BettingType.Item:
                        Item item = ItemManager.CreateByItemID(Data.BetItemID, amount);
                        player.inventory.GiveItem(item, null);
                        break;
                    case BettingType.ServerRewards:
                        Instance.ServerRewards?.Call("AddPoints", player.userID, amount);
                        break;
                    case BettingType.Economics:
                        Instance.Economics?.Call("Deposit", player.UserIDString, (double)amount);
                        break;
                }
            }

            public string FormatBetString(BasePlayer player = null)
            {
                switch (Data.BetType)
                {
                    case BettingType.Item:
                        return Data.BetItemName;
                    case BettingType.ServerRewards:
                        return Instance.lang.GetMessage("Bet.RP", Instance, player?.UserIDString);
                    case BettingType.Economics:
                        return Instance.lang.GetMessage("Bet.Eco", Instance, player?.UserIDString);
                }
                return string.Empty;
            }
            #endregion
        }
        #endregion

        #region Game Chair
        public class GameChair : MonoBehaviour
        {
            private BaseMountable mountable;

            private TriggerSafeZone triggerSafeZone;

            public CardGame CardGame { get; private set; }

            public int Number { get; private set; }

            private const int PLAYER_MASK = 131072;

            private void Awake()
            {
                mountable = GetComponent<BaseMountable>();

                enabled = false;

                triggerSafeZone = gameObject.AddComponent<TriggerSafeZone>();
                triggerSafeZone.interestLayers = PLAYER_MASK;
                triggerSafeZone.enabled = true;
            }

            private void OnDestroy()
            {
                if (triggerSafeZone.entityContents != null)
                {
                    for (int i = triggerSafeZone.entityContents.Count - 1; i >= 0; i--)
                    {
                        BaseEntity baseEntity = triggerSafeZone.entityContents.ElementAt(i);
                        if (baseEntity)
                        {
                            triggerSafeZone.entityContents.Remove(baseEntity);
                            baseEntity.LeaveTrigger(triggerSafeZone);
                        }
                    }                    
                }

                Destroy(triggerSafeZone);
            }

            public void SetCardGame(CardGame cardGame, int number)
            {
                this.CardGame = cardGame;
                this.Number = number;
            }

            public void OnPlayerEnter(BasePlayer player)
            {
                if (triggerSafeZone.entityContents == null)
                    triggerSafeZone.entityContents = new HashSet<BaseEntity>();

                if (!triggerSafeZone.entityContents.Contains(player))
                {
                    triggerSafeZone.entityContents.Add(player);
                    player.EnterTrigger(triggerSafeZone);

                    if (player.IsItemHoldRestricted(player.inventory.containerBelt.FindItemByUID(player.svActiveItemID)))
                        player.UpdateActiveItem(0);

                    player.SetPlayerFlag(BasePlayer.PlayerFlags.SafeZone, true);
                }
            }

            public void OnPlayerExit(BasePlayer player)
            {
                if (triggerSafeZone.entityContents != null && triggerSafeZone.entityContents.Contains(player))
                {
                    triggerSafeZone.entityContents.Remove(player);
                    player.LeaveTrigger(triggerSafeZone);

                    if (!player.InSafeZone())
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.SafeZone, false);
                }
            }
        }
        #endregion

        #region Table Informer
        public class TableInformer : MonoBehaviour
        {
            private DecorDeployable table;

            private CardGame cardGame;

            private OBB worldSpaceBounds;

            private Vector3 drawPosition;

            private string informationStr;

            private const float REFRESH_RATE = 2f;

            private void Awake()
            {
                table = GetComponent<DecorDeployable>();

                cardGame = GetComponent<CardGame>();

                drawPosition = transform.position + (Vector3.up * 1.5f);

                Bounds bounds = table.bounds;
                bounds.Expand(Configuration.DDrawBoundsMultiplier);

                worldSpaceBounds = new OBB(transform.position, transform.lossyScale, transform.rotation, bounds);                 
            }

            public void OnGameInitialized()
            {
                informationStr = string.Format(Instance.lang.GetMessage("Table.Information", Instance), cardGame.Data.gameType, cardGame.Data.minimumBet, cardGame.Data.maximumBet, cardGame.FormatBetString());

                InvokeHandler.InvokeRepeating(this, InformationTick, UnityEngine.Random.Range(0.1f, 2f), REFRESH_RATE);
            }

            private void InformationTick()
            {               
                List<BasePlayer> basePlayers = Pool.GetList<BasePlayer>();
                Vis.Entities(worldSpaceBounds, basePlayers);
                for (int i = 0; i < basePlayers.Count; i++)
                {
                    BasePlayer player = basePlayers[i];
                    if (player == null || player.IsDead() || player.isMounted)
                        continue;

                    if (player.IsAdmin)
                        player.SendConsoleCommand("ddraw.text", REFRESH_RATE, Color.white, drawPosition, informationStr);
                    else
                    {
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                        player.SendNetworkUpdateImmediate();
                        player.SendConsoleCommand("ddraw.text", REFRESH_RATE, Color.white, drawPosition, informationStr);
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                        player.SendNetworkUpdateImmediate();
                    }
                }

                Pool.FreeList(ref basePlayers);
            }
        }
        #endregion

        #region Game Timer
        public class TimerElement : MonoBehaviour
        {
            public Casino.CardGame CardGame { get; private set; }

            private int timeRemaining;

            private Action callback;

            private const string TIMER_OVERLAY = "casino.timer";

            private UI4 TIMER_POSITION = new UI4(0.475f, 0f, 0.525f, 0.05f);

            private void Awake() => CardGame = GetComponent<Casino.CardGame>();

            public void StartTimer(int time, Action callback = null)
            {
                this.timeRemaining = time + 1;

                this.callback = callback;

                InvokeHandler.InvokeRepeating(this, TimerTick, 0f, 1f);
            }

            public void StopTimer()
            {
                if (!InvokeHandler.IsInvoking(this, TimerTick))
                    return;

                InvokeHandler.CancelInvoke(this, TimerTick);

                for (int i = 0; i < CardGame.Players.Length; i++)
                {
                    CardPlayer cardPlayer = CardGame.Players[i];
                    if (cardPlayer == null)
                        continue;

                    cardPlayer.DestroyUI(TIMER_OVERLAY);
                }
            }

            private void TimerTick()
            {
                timeRemaining--;

                if (timeRemaining == 0)
                {
                    StopTimer();
                    callback?.Invoke();
                }
                else UpdateTimerUI();
            }

            private void UpdateTimerUI()
            {
                CuiElementContainer container = UI.Container(TIMER_OVERLAY, TIMER_POSITION);
                UI.Label(container, TIMER_OVERLAY, $"{timeRemaining}", 18, UI4.FullScreen);

                for (int i = 0; i < CardGame.Players.Length; i++)
                {
                    CardPlayer cardPlayer = CardGame.Players[i];
                    if (cardPlayer == null)
                        continue;

                    cardPlayer.AddUI(TIMER_OVERLAY, container);
                }
            }
        }
        #endregion

        #region Card Player
        public class CardPlayer : MonoBehaviour
        {            
            public List<Card> hand = Pool.GetList<Card>();

            public List<string> uiPanels = Pool.GetList<string>();

            public BasePlayer Player { get; private set; }

            public Casino.CardGame CardGame { get; set; }

            public bool IsLeaving { get; private set; }

            public int UID { get; private set; }

            public int BetAmount { get; set; }

            public bool BetLocked { get; set; }

            public bool IsPlaying { get; set; } = false;

            public int Position { get; set; }

            public virtual int BankBalance
            {
                get
                {
                    return CardGame.GetUserAmount(Player);
                }
            }

            public int BalanceAndBet
            {
                get
                {
                    return BankBalance + BetAmount;
                }
            }

            public virtual void Awake()
            {
                Player = GetComponent<BasePlayer>();
                CardGame = Player.GetMounted().GetComponent<Casino.GameChair>().CardGame;
                BetAmount = CardGame.Data.minimumBet;

                GenerateUID();
                enabled = false;
            }

            public virtual void OnDestroy()
            {
                IsLeaving = true;

                DestroyUI();

                if (Player.isMounted)
                {
                    Player.GetMounted().DismountPlayer(Player, false);
                }

                Pool.FreeList(ref hand);
                Pool.FreeList(ref uiPanels);
            }

            public void GenerateUID() => UID = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

            public virtual void ResetHand()
            {
                hand.Clear();
                BetAmount = CardGame.Data.minimumBet;
                BetLocked = false;
                IsPlaying = false;
            }

            public virtual void SetBet()
            {
                BetLocked = true;
                CardGame.TakeAmount(Player, BetAmount);                
            }

            public void IssueWin(int amount)
            {
                CardGame.GiveAmount(Player, amount);               
            }

            public void AddUI(string str, CuiElementContainer container)
            {
                DestroyUI(str);
                uiPanels.Add(str);                   
                CuiHelper.AddUi(Player, container);
            }

            public virtual void DestroyUI(string str)
            {
                uiPanels.Remove(str);
                CuiHelper.DestroyUi(Player, str);
            }

            public virtual void DestroyUI()
            {
                foreach (string str in uiPanels)                
                    CuiHelper.DestroyUi(Player, str);                

                uiPanels.Clear();
            }
        }
        #endregion

        #region Deck Of Cards
        public class Deck
        {
            private readonly Queue<Card> _deck = new Queue<Card>();

            private readonly List<int> _cardIndexes = new List<int>();

            private readonly System.Random _random = new System.Random();

            public Deck()
            {
                IndexCards();
                Shuffle();
            }

            private void IndexCards()
            {
                _cardIndexes.Clear();

                for (int i = 0; i < 52; i++)                
                    _cardIndexes.Add(i);                
            }

            public void Shuffle()
            {
                _deck.Clear();                

                for (int i = 51; i >= 0; i--)
                {
                    int index = _random.Next(0, i);
                    int temp = _cardIndexes[i];
                    _cardIndexes[i] = _cardIndexes[index];
                    _cardIndexes[index] = temp;
                }

                Fill();
            }

            private void Fill()
            {
                for (int i = 0; i < _cardIndexes.Count; i++)
                {
                    CardSuit suit = (CardSuit)(_cardIndexes[i] % 4);
                    CardValue value = (CardValue)(_cardIndexes[i] % 13 + 1);

                    _deck.Enqueue(new Card(suit, value));
                }
            }

            public Card Deal() => _deck.Dequeue();            
        }

        public struct Card
        {
            public CardSuit Suit { get; private set; }

            public CardValue Value { get; private set; }

            private string ImageID { get; set; }

            public Card(CardSuit suit, CardValue value) : this()
            {
                this.Suit = suit;
                this.Value = value;
                this.ImageID = string.Empty;
            }

            public override string ToString() => string.Format("Suit: {0}, Value: {1}", this.Suit, this.Value);

            public string GetCardImage()
            {
                if (string.IsNullOrEmpty(ImageID))
                {
                    string value = string.Empty;
                    string suit = string.Empty;

                    switch (Value)
                    {
                        case CardValue.Ace:
                            value = "A";
                            break;
                        case CardValue.Deuce:
                        case CardValue.Three:
                        case CardValue.Four:
                        case CardValue.Five:
                        case CardValue.Six:
                        case CardValue.Seven:
                        case CardValue.Eight:
                        case CardValue.Nine:
                        case CardValue.Ten:
                            value = ((int)Value).ToString();
                            break;
                        case CardValue.Jack:
                            value = "J";
                            break;
                        case CardValue.Queen:
                            value = "Q";
                            break;
                        case CardValue.King:
                            value = "K";
                            break;
                    }

                    switch (Suit)
                    {
                        case CardSuit.Spades:
                            suit = "S";
                            break;
                        case CardSuit.Diamonds:
                            suit = "D";
                            break;
                        case CardSuit.Hearts:
                            suit = "H";
                            break;
                        case CardSuit.Clubs:
                            suit = "C";
                            break;
                    }

                    ImageID = Images.GetCardImage(value, suit);
                }

                return ImageID;
            }
        }
        
        public enum CardSuit
        {
            Spades = 0,
            Diamonds = 1,
            Hearts = 2,
            Clubs = 3
        }

        public enum CardValue
        {
            Ace = 1,
            Deuce = 2,
            Three = 3,
            Four = 4,
            Five = 5,
            Six = 6,
            Seven = 7,
            Eight = 8,
            Nine = 9,
            Ten = 10,
            Jack = 11,
            Queen = 12,
            King = 13
        }
        #endregion

        #region UI     
        public static class UI
        {            
            public static CuiElementContainer Container(string panel, UI4 dimensions, bool useCursor = false, string parent = "Overlay")
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = "0 0 0 0"},
                            RectTransform = {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax()},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panel
                    }
                };
                return container;
            }

            public static void Panel(CuiElementContainer container, string panel, string color, UI4 dimensions, bool blur = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color, Material = blur ? "assets/content/ui/uibackgroundblur-ingamemenu.mat" : string.Empty },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                },
                panel);
            }

            public static void Label(CuiElementContainer container, string panel, string text, int size, UI4 dimensions, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                },
                panel);
            }

            public static void OutlinedLabel(CuiElementContainer container, string panel, string text, int size, UI4 dimensions, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel.ToString(),
                    FadeOut = 0.2f,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = text,
                            FontSize = size,
                            Align = align,
                            FadeIn = 0.2f
                        },
                        new CuiOutlineComponent
                        {
                            Distance = "1 1",
                            Color = Color("000000", 0.7f)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = dimensions.GetMin(),
                            AnchorMax = dimensions.GetMax()
                        }
                    }
                });
            }

            public static void Button(CuiElementContainer container, string panel, string color, string text, int size, UI4 dimensions, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 0f },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }

            public static void Button(CuiElementContainer container, string panel, UI4 dimensions, string command)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = command, FadeIn = 0f },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() },
                    Text = { Text = string.Empty, FontSize = 0, Align = TextAnchor.LowerCenter }
                },
                panel);
            }

            public static void Image(CuiElementContainer container, string panel, string png, UI4 dimensions)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent {Png = png },
                        new CuiRectTransformComponent {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                    }
                });
            }

            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.Substring(1);
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }

        public class UI4
        {
            public float xMin, yMin, xMax, yMax;

            public UI4(float xMin, float yMin, float xMax, float yMax)
            {
                this.xMin = xMin;
                this.yMin = yMin;
                this.xMax = xMax;
                this.yMax = yMax;
            }

            public string GetMin() => $"{xMin} {yMin}";
            public string GetMax() => $"{xMax} {yMax}";

            private static UI4 _fullScreen = new UI4(0, 0, 1, 1);

            private static UI4 _zero = new UI4(0, 0, 0, 0);

            public static UI4 FullScreen { get { return _fullScreen; } }

            public static UI4 Zero { get { return _zero; } }
        }
        #endregion

        #region Commands
        [ChatCommand("casino")]
        private void cmdCasino(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;

            if (args.Length == 0)
            {
                SendReply(player, "/casino create <type> <maximum players> <minimum bet> <maximum bet> - Creates the specified type of card game, with the details provided");
                SendReply(player, $"Available Games : {registeredGames.Keys.ToSentence()}");
                SendReply(player, "/casino setbet <type> <opt:shortname> - Sets the bet type for the card game you are looking at");
                SendReply(player, "Types : Item / ServerRewards / Economics. Note that only the type Item requires a item shortname");
                SendReply(player, "/casino remove - Removes the card game you are looking at");
                return;
            }

            BaseEntity baseEntity = FindEntityFromRay(player);

            if (baseEntity == null || !baseEntity.PrefabName.Equals(TABLE_PREFAB))
            {
                SendReply(player, "No table found! You must be within 3m and looking at a table");
                return;
            }
                   
            switch (args[0].ToLower())
            {
                case "create":
                    if (args.Length != 5)
                    {
                        SendReply(player, "Invalid Syntax!");
                        SendReply(player, "/casino create <type> <maximum players> <minimum bet> <maximum bet> - Creates the specified type of card game, with the details provided");
                        SendReply(player, $"Available Games : {registeredGames.Keys.ToSentence()}");
                        return;
                    }

                    if (storedData.IsRegisteredTable(baseEntity.net.ID))
                    {
                        SendReply(player, "This table is already a registered card game");
                        return;
                    }

                    GameType gameType = ParseType<GameType>(args[1]);
                    if (!registeredGames.ContainsKey(gameType))
                    {
                        SendReply(player, $"The game type {gameType} has not been registered");
                        return;
                    }

                    int maxPlayers;
                    if (!int.TryParse(args[2], out maxPlayers))
                    {
                        SendReply(player, "You must enter a amount of players");
                        return;
                    }
                    else
                    {
                        if (maxPlayers < 1 || maxPlayers > 4)
                        {
                            SendReply(player, "You can only set the amount of players between 1 and 4");
                            return;
                        }
                    }

                    int minimumBet;
                    if (!int.TryParse(args[3], out minimumBet))
                    {
                        SendReply(player, "You must enter a valid minimum bet");
                        return;
                    }

                    minimumBet = Mathf.Abs(minimumBet);

                    int maximumBet;
                    if (!int.TryParse(args[4], out maximumBet))
                    {
                        SendReply(player, "You must enter a valid maximum bet");
                        return;
                    }

                    maximumBet = Mathf.Abs(maximumBet);

                    StoredData.GameData gameData = new StoredData.GameData(baseEntity, gameType, maxPlayers, minimumBet, maximumBet);
                    storedData.RegisterTable(baseEntity.net.ID, gameData);
                    SaveData();

                    CreateCardGame(baseEntity, gameData);

                    SendReply(player, string.Format("You have created a new {0} game", gameType));
                    return;
                case "setbet":
                    if (args.Length < 2)
                    {
                        SendReply(player, "/casino setbet <type> <opt:shortname> - Sets the bet type for the card game you are looking at");
                        SendReply(player, "Types : Item / ServerRewards / Economics. Note that only the type Item requires a item shortname");
                        return;
                    }

                    if (!storedData.IsRegisteredTable(baseEntity.net.ID))
                    {
                        SendReply(player, "The table you are looking at is not a registered card game");
                        return;
                    }

                    BettingType bettingType = ParseType<BettingType>(args[1]);
                    string shortname = string.Empty;

                    if (bettingType == BettingType.Item)
                    {
                        if (args.Length != 3)
                        {
                            SendReply(player, "You must enter a item shortname to set a item betting type");
                            return;
                        }

                        shortname = args[2];

                        ItemDefinition itemDefinition = ItemManager.FindItemDefinition(shortname);
                        if (itemDefinition == null)
                        {
                            SendReply(player, $"The item shortname '{shortname}' is invalid!");
                            return;
                        }
                    }

                    storedData.SetBetType(baseEntity.net.ID, bettingType.ToString(), shortname);
                    SaveData();

                    CardGame cardgame = baseEntity.GetComponent<CardGame>();
                    GameType type = cardgame.Type;

                    UnityEngine.Object.Destroy(cardgame);

                    timer.In(5f, () =>
                    {
                        if (registeredGames.ContainsKey(type))
                        {
                            CreateCardGame(baseEntity, storedData.tables[baseEntity.net.ID]);
                        }
                    });                    

                    if (bettingType == BettingType.Item)
                        SendReply(player, $"You have set the bet type to {bettingType} and item to {shortname}");
                    else SendReply(player, $"You have set the bet type to {bettingType}");
                    return;
                case "remove":
                    if (!storedData.IsRegisteredTable(baseEntity.net.ID))
                    {
                        SendReply(player, "The table you are looking at is not a registered card game");
                        return;
                    }

                    storedData.RemoveTable(baseEntity.net.ID);

                    SaveData();

                    CardGame cardGame = baseEntity.GetComponent<CardGame>();
                    if (cardGame != null)
                        UnityEngine.Object.Destroy(cardGame);

                    BaseEntity newTable = GameManager.server.CreateEntity(baseEntity.PrefabName, baseEntity.transform.position, baseEntity.transform.rotation);
                    newTable.skinID = baseEntity.skinID;

                    if (baseEntity != null && !baseEntity.IsDestroyed)
                        baseEntity?.Kill();

                    newTable.Spawn();

                    SendReply(player, "You have removed the card game you are looking at");
                    return;
                default:
                    break;
            }            
        }

        [ConsoleCommand("casino.leavetable")]
        private void ccmdLeaveTable(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            CardGame cardGame = GameFromPlayer(player);
            if (cardGame == null)
                return;

            cardGame.OnPlayerExit(player);
        }
        #endregion

        #region Config  

        private static ConfigData Configuration;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Betting Item (shortname)")]
            public string BettingItem { get; set; }

            [JsonProperty(PropertyName = "Betting Item Type (Item/ServerRewards/Economics)")]
            public string BettingType { get; set; }

            [JsonProperty(PropertyName = "Wipe data on map wipe")]
            public bool WipeOnNewSave { get; set; }

            [JsonProperty(PropertyName = "DDraw bounds multiplier (size of table bounds multiplied by this number)")]
            public float DDrawBoundsMultiplier { get; set; }

            [JsonProperty(PropertyName = "Force image update on load")]
            public bool ForceUpdate { get; set; }

            [JsonProperty(PropertyName = "Chair skin ID")]
            public ulong ChairSkinID { get; set; }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Configuration = Config.ReadObject<ConfigData>();

            if (Configuration.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(Configuration, true);
        }

        protected override void LoadDefaultConfig() => Configuration = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                BettingItem = "scrap",
                BettingType = "Item",
                WipeOnNewSave = true,
                ForceUpdate = false,
                DDrawBoundsMultiplier = 1.5f,
                ChairSkinID = 1385535192,
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(Configuration, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            if (Configuration.Version < new VersionNumber(0, 1, 9))
            {
                Configuration.DDrawBoundsMultiplier = 1.5f;
                Configuration.ChairSkinID = 1385535192;
                if (string.IsNullOrEmpty(Configuration.BettingType))
                    Configuration.BettingType = "Item";
            }

            Configuration.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Data Management        
        private StoredData storedData;

        private DynamicConfigFile data;

        private void SaveData() => data.WriteObject(storedData);

        private void LoadData()
        {
            data = Interface.Oxide.DataFileSystem.GetFile("Casino/casino_data");

            try
            {
                storedData = data.ReadObject<StoredData>();
            }
            catch
            {
                storedData = new StoredData();
            }

            if (storedData?.tables == null)
                storedData = new StoredData();
        }

        public class StoredData
        {
            public Hash<uint, GameData> tables = new Hash<uint, GameData>();

            public bool IsRegisteredTable(uint netId) => tables.ContainsKey(netId);

            public void RegisterTable(uint netId, GameData gameData) => tables.Add(netId, gameData);

            public void RemoveTable(uint netId) => tables.Remove(netId);

            public void SetBetType(uint netId, string bettingType, string shortname)
            {
                tables[netId].bettingTypeOverride = bettingType;
                tables[netId].bettingItemOverride = shortname;
            }

            public class GameData
            {
                public GameType gameType;
                public int maxPlayers;
                public int minimumBet;
                public int maximumBet;
                public string bettingItemOverride;
                public string bettingTypeOverride;

                public ulong skinId;

                public float x, y, z, rx, ry, rz;

                [JsonIgnore]
                public int BetItemID { get; private set; }

                [JsonIgnore]
                public string BetItemName { get; private set; }

                [JsonIgnore]
                public BettingType BetType { get; private set; }

                [JsonIgnore]
                public Vector3 Position
                {
                    get
                    {
                        return new Vector3(x, y, z);
                    }
                }

                [JsonIgnore]
                public Vector3 Rotation
                {
                    get
                    {
                        return new Vector3(rx, ry, rz);
                    }
                }

                public GameData() { }

                public GameData(BaseEntity baseEntity, GameType gameType, int maxPlayers, int minimumBet, int maximumBet)
                {
                    this.gameType = gameType;
                    this.maxPlayers = maxPlayers;
                    this.minimumBet = minimumBet;
                    this.maximumBet = maximumBet;

                    this.skinId = baseEntity.skinID;

                    this.x = baseEntity.transform.position.x;
                    this.y = baseEntity.transform.position.y;
                    this.z = baseEntity.transform.position.z;

                    this.rx = baseEntity.transform.eulerAngles.x;
                    this.ry = baseEntity.transform.eulerAngles.y;
                    this.rz = baseEntity.transform.eulerAngles.z;
                }

                public void InitializeBetData()
                {
                    string type = string.IsNullOrEmpty(bettingTypeOverride) ? Configuration.BettingType : bettingTypeOverride;

                    BetType = ParseType<BettingType>(type);

                    if (BetType == BettingType.ServerRewards && !Instance.ServerRewards)
                    {
                        Debug.LogError("[Casino] - Betting Type set to ServerRewards but ServerRewards can not be found? Defaulting to Item");
                        BetType = BettingType.Item;
                    }

                    if (BetType == BettingType.Economics && !Instance.Economics)
                    {
                        Debug.LogError("[Casino] - Betting Type set to Economics but Economics can not be found? Defaulting to Item");
                        BetType = BettingType.Item;
                    }

                    if (BetType == BettingType.Item)
                    {
                        string shortname = string.IsNullOrEmpty(bettingItemOverride) ? Configuration.BettingItem : bettingItemOverride;

                        ItemDefinition itemDefinition = ItemManager.FindItemDefinition(shortname);
                        if (itemDefinition == null)
                        {
                            Debug.LogError($"[Casino] - Betting item shortname is not a valid item shortname :{shortname}. Defaulting to scrap");
                            itemDefinition = ItemManager.FindItemDefinition("scrap");
                        }

                        BetItemID = itemDefinition.itemid;
                        BetItemName = itemDefinition.displayName.english;
                    }
                }               
            }
        }
        #endregion

        #region Playing Cards
        public class PlayingCards
        {
            private Action _callback;

            private Plugin _imageLibrary;

            public static bool IsReady { get; private set; }

            private Plugin ImageLibrary
            {
                get
                {
                    if (_imageLibrary == null)
                    {
                        _imageLibrary = Interface.Oxide.RootPluginManager.GetPlugin("Image Library");

                        if (_imageLibrary == null)                        
                            throw new Exception("Casino requires the Image Library plugin, but it could not be found!");
                    }

                    return _imageLibrary;
                }                
            }

            private const string URI = "https://www.rustedit.io/images/casino/";

            public PlayingCards(Plugin plugin)
            {
                _imageLibrary = plugin;                
            }

            public void ImportCardImages(ICollection<GameType> cardgames, bool forceUpdate, Action callback)
            {
                _callback = callback;

                Dictionary<string, Dictionary<ulong, string>> loadOrder = new Dictionary<string, Dictionary<ulong, string>>();

                string[] values = new string[] { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };
                string[] suits = new string[] { "C", "D", "H", "S" };
                int[] chips = new int[] { 1, 10, 50, 100, 500 };

                for (int i = 0; i < suits.Length; i++)
                {
                    for (int j = 0; j < values.Length; j++)
                    {
                        loadOrder.Add($"{values[j]}{suits[i]}", new Dictionary<ulong, string>() { [0U] = $"{URI}{values[j]}{suits[i]}.png" });
                    }
                }

                loadOrder.Add("blue_back", new Dictionary<ulong, string>() { [0U] = $"{URI}blue_back.png" });
                loadOrder.Add("gray_back", new Dictionary<ulong, string>() { [0U] = $"{URI}gray_back.png" });
                loadOrder.Add("green_back", new Dictionary<ulong, string>() { [0U] = $"{URI}green_back.png" });
                loadOrder.Add("purple_back", new Dictionary<ulong, string>() { [0U] = $"{URI}purple_back.png" });
                loadOrder.Add("red_back", new Dictionary<ulong, string>() { [0U] = $"{URI}red_back.png" });
                loadOrder.Add("yellow_back", new Dictionary<ulong, string>() { [0U] = $"{URI}yellow_back.png" });

                foreach (GameType gameType in cardgames)
                    loadOrder.Add($"board_{gameType}".ToLower(), new Dictionary<ulong, string>() { [0U] = $"{URI}board_{gameType}.png".ToLower() });

                loadOrder.Add("betting_stack", new Dictionary<ulong, string>() { [0U] = $"{URI}betting_stack.png" });

                for (int i = 0; i < chips.Length; i++)                
                    loadOrder.Add($"chip_{chips[i]}", new Dictionary<ulong, string>() { [0U] = $"{URI}chip_{chips[i]}.png" });                

                ImageLibrary.Call("ImportItemList", "Casino - Playing card imagery", loadOrder, forceUpdate, new Action(OnImagesLoaded));
            }

            private void OnImagesLoaded()
            {
                IsReady = true;
                _callback?.Invoke();
            }

            public void AddImage(string imageName, string fileName) => ImageLibrary.Call("AddImage", fileName, imageName, 0U);

            public string GetCardImage(string value, string suit) => (string)ImageLibrary?.Call("GetImage", $"{value}{suit}");

            public string GetChipImage(int value) => (string)ImageLibrary?.Call("GetImage", $"chip_{value}");

            public string GetChipStackImage() => (string)ImageLibrary?.Call("GetImage", "betting_stack");

            public string GetBoardImage(string gameType) => (string)ImageLibrary?.Call("GetImage", $"board_{gameType}");
            
            public string GetCardBackground(string color) => (string)ImageLibrary?.Call("GetImage", $"{color}_back");
        }
        #endregion
    }
}
