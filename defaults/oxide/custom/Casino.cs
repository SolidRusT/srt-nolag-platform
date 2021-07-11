//Requires: PlayingCards
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
    [Info("Casino", "k1lly0u", "0.1.7")]
    [Description("Core card game and table management system")]
    class Casino : RustPlugin
    {
        #region Fields
        private StoredData storedData;
        private DynamicConfigFile data;

        [PluginReference]
        private Plugin ServerRewards, Economics;

        public static Casino Instance { get; private set; }

        private Hash<GameType, Action<BaseEntity, StoredData.GameData>> registeredGames = new Hash<GameType, Action<BaseEntity, StoredData.GameData>>();

        private List<CardGame> cardGames = new List<CardGame>();

        private readonly KeyValuePair<Vector3, Vector3>[] chairPositions = new KeyValuePair<Vector3, Vector3>[]
        {
            new KeyValuePair<Vector3, Vector3>(new Vector3(0.75f, 0, -1.1f), new Vector3(0, 0, 0)),
            new KeyValuePair<Vector3, Vector3>(new Vector3(-0.75f, 0, -1.1f), new Vector3(0, 0, 0)),
            new KeyValuePair<Vector3, Vector3>(new Vector3(0.75f, 0, 1.1f), new Vector3(0, 180, 0)),
            new KeyValuePair<Vector3, Vector3>(new Vector3(-0.75f, 0, 1.1f), new Vector3(0, 180, 0))
        };

        private bool wipeData = false;

        private const string CHAIR_PREFAB = "assets/prefabs/deployable/chair/chair.deployed.prefab";

        private const string TABLE_PREFAB = "assets/prefabs/deployable/table/table.deployed.prefab";        
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            data = Interface.Oxide.DataFileSystem.GetFile("Casino/casino_data");
            Instance = this;

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Error.NotEnoughAmount"] = "<color=#D3D3D3>You do not have have {0} to play this table. Minimum bet is :</color> <color=#ce422b>{1}</color>",
                ["Table.Information"] = "<size=25>{0}</size><size=20>\nMin Bet: {1} {3}\nMax Bet: {2} {3}\nTake a seat to play!</size>",
                ["Bet.RP"] = "RP",
                ["Bet.Eco"] = "Eco"
            }, this);
        }

        private void OnServerInitialized()
        {            
            LoadData();

            if (wipeData)
            {
                storedData.tables.Clear();
                SaveData();
            }

            PlayingCards.OnImagesReady += LoadTables;
        }

        private void OnNewSave(string filename) => wipeData = true;

        private void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            CardGame cardGame = GameFromChair(mountable);
            if (cardGame == null)
                return;

            int position = cardGame.GetChairPosition(mountable);
            if (position == -1)
            {
                PrintError("Player tried mounting a CardGame with a chair position of -1. Perhaps a old chair that was cleaned up properly? Tell k1lly0u");
                return;
            }
            cardGame.OnPlayerEnter(player, position);
        }

        private object CanMountEntity(BasePlayer player, BaseMountable mountable)
        {
            CardGame cardGame = mountable.GetComponent<GameChair>()?.CardGame;
            if (cardGame != null)
            {
                int amount = cardGame.GetUserAmount(player);
                if (amount < cardGame.gameData.minimumBet)
                {
                    SendReply(player, string.Format(lang.GetMessage("Error.NotEnoughAmount", this, player.UserIDString), cardGame.FormatBetString(player), cardGame.gameData.minimumBet));
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
            CardGame cardGame = GameFromChair(mountable);
            if (cardGame == null)
                return;

            cardGame.OnPlayerExit(player);
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

            for (int i = 0; i < cardGames.Count; i++)            
                UnityEngine.Object.Destroy(cardGames[i]);
            
            cardGames.Clear();

            Instance = null;
        }
        #endregion

        #region Functions
        public void RegisterGame(GameType gameType, Action<BaseEntity, StoredData.GameData> callback)
        {
            PlayingCards.CardGames.Add(gameType.ToString().ToLower());
            registeredGames[gameType] = callback;
        }

        public void UnregisterGame(GameType gameType)
        {
            registeredGames.Remove(gameType);

            for (int i = cardGames.Count - 1; i >= 0; i--)
            {
                CardGame cardGame = cardGames[i];
                if (cardGame.gameType == gameType)
                {
                    cardGames.Remove(cardGame);
                    UnityEngine.Object.Destroy(cardGame);                    
                }
            }
        }

        private BaseMountable CreateSeat(BaseEntity entity, int number, CardGame cardgame)
        {
            Vector3 position = entity.transform.position + (entity.transform.rotation * chairPositions[number].Key);
            Quaternion rotation = entity.transform.rotation * Quaternion.Euler(chairPositions[number].Value);

            BaseMountable baseMountable = GameManager.server.CreateEntity(CHAIR_PREFAB, position, rotation) as BaseMountable;
            baseMountable.enableSaving = false;
            baseMountable.pickup.enabled = false;

            baseMountable.Spawn();

            DestroyComponents(baseMountable);

            baseMountable.gameObject.AddComponent<GameChair>().SetCardGame(cardgame, number);
            return baseMountable;
        }

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

        private CardGame GameFromChair(BaseMountable mountable) => mountable.GetComponent<GameChair>()?.CardGame;

        public CardGame GameFromPlayer(BasePlayer player)
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

        public void DestroyComponents(BaseEntity baseEntity)
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

        #region Game Base
        public class CardGame : MonoBehaviour
        {
            internal StoredData.GameData gameData;

            public GameType gameType;

            internal GameState gameState = GameState.Waiting;

            internal TimerElement timer;

            private TableInformer informer;

            public CardPlayer[] cardPlayers;

            private BaseMountable[] availableSeats;

            public DecorDeployable table;

            private int bettingItemID;

            private string bettingItemName;

            private BettingType bettingType;

            public void Awake()
            {
                table = GetComponent<DecorDeployable>();
                timer = gameObject.AddComponent<TimerElement>();
                informer = gameObject.AddComponent<TableInformer>();
                enabled = false;
            }

            public virtual void OnDestroy()
            {
                Destroy(informer);

                for (int i = 0; i < cardPlayers.Length; i++)
                {
                    CardPlayer cardPlayer = cardPlayers[i];
                    if (cardPlayer != null)
                        Destroy(cardPlayer);
                }

                for (int i = availableSeats.Length - 1; i >= 0; i--)
                {
                    BaseMountable baseMountable = availableSeats[i];

                    if (baseMountable != null)
                    {
                        baseMountable.DismountAllPlayers();
                        baseMountable.Kill(BaseNetworkable.DestroyMode.None);
                    }
                }

                Destroy(timer);
            }

            public virtual void OnPlayerEnter(BasePlayer player, int position) { }

            public virtual void OnPlayerExit(BasePlayer player) { }

            public virtual bool CanDismountStandard() => false;

            public int GetChairPosition(BaseMountable baseMountable)
            {
                for (int i = 0; i < availableSeats.Length; i++)
                {
                    if (availableSeats[i].EqualNetID(baseMountable))
                        return i;
                }
                return -1;
            }

            public int CurrentPlayerCount => cardPlayers.Where(x => x != null).Count();

            public int CurrentPlayingCount => cardPlayers.Where(x => x != null && x.IsPlaying).Count();

            public void InitializeGame(StoredData.GameData gameData)
            {
                this.gameData = gameData;

                SetBettingType();

                cardPlayers = new CardPlayer[gameData.maxPlayers];
                availableSeats = new BaseMountable[gameData.maxPlayers];

                for (int i = 0; i < gameData.maxPlayers; i++)                
                    CreateSeat(i);                

                informer.OnGameInitialized();

                OnGameInitialized();
            }

            public void CreateSeat(int number)
            {
                BaseMountable baseMountable = Instance.CreateSeat(table, number, this);
                availableSeats[number] = baseMountable;
            }

            public virtual void OnGameInitialized()
            {
                Casino.Instance.cardGames.Add(this);
            }

            public void AdjustBet(CardPlayer cardPlayer, int amount)
            {
                if (gameState != GameState.PlacingBets)             
                    return;

                amount += cardPlayer.BetAmount;
                if (cardPlayer.BankBalance < amount)
                    return;

                cardPlayer.BetAmount = Mathf.Clamp(amount, gameData.minimumBet, gameData.maximumBet);
            }

            public void ResetBet(CardPlayer cardPlayer)
            {
                if (gameState != GameState.PlacingBets)
                    return;

                cardPlayer.BetAmount = gameData.minimumBet;
            }

            #region Betting

            private void SetBettingType()
            {
                string type = string.IsNullOrEmpty(gameData.bettingTypeOverride) ? Instance.configData.BettingType : gameData.bettingTypeOverride;

                bettingType = ParseType<BettingType>(type);

                if (bettingType == BettingType.ServerRewards && !Instance.ServerRewards)
                {
                    Instance.PrintError("Betting Type set to ServerRewards but ServerRewards can not be found? Defaulting to Item");
                    bettingType = BettingType.Item;
                }

                if (bettingType == BettingType.Economics && !Instance.Economics)
                {
                    Instance.PrintError("Betting Type set to Economics but Economics can not be found? Defaulting to Item");
                    bettingType = BettingType.Item;
                }

                if (bettingType == BettingType.Item)
                {
                    string shortname = string.IsNullOrEmpty(gameData.bettingItemOverride) ? Instance.configData.BettingItem : gameData.bettingItemOverride;

                    ItemDefinition itemDefinition = ItemManager.FindItemDefinition(shortname);
                    if (itemDefinition == null)
                    {
                        Instance.PrintError($"Betting item shortname is not a valid item shortname :{shortname}. Defaulting to scrap");
                        itemDefinition = ItemManager.FindItemDefinition("scrap");
                    }

                    bettingItemID = itemDefinition.itemid;
                    bettingItemName = itemDefinition.displayName.english;
                }
            }

            public int GetUserAmount(BasePlayer player)
            {
                switch (bettingType)
                {
                    case BettingType.Item:
                        return player.inventory.GetAmount(bettingItemID);
                    case BettingType.ServerRewards:
                        return (int)Instance.ServerRewards?.Call("CheckPoints", player.userID);
                    case BettingType.Economics:
                        return Convert.ToInt32((double)Instance.Economics?.Call("Balance", player.UserIDString));
                }
                return 0;
            }

            public void TakeAmount(BasePlayer player, int amount)
            {
                switch (bettingType)
                {
                    case BettingType.Item:
                        player.inventory.Take(null, bettingItemID, amount);
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
                switch (bettingType)
                {
                    case BettingType.Item:
                        Item item = ItemManager.CreateByItemID(bettingItemID, amount);
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
                switch (bettingType)
                {
                    case BettingType.Item:
                        return bettingItemName;
                    case BettingType.ServerRewards:
                        return Instance.lang.GetMessage("Bet.RP", Instance, player?.UserIDString);
                    case BettingType.Economics:
                        return Instance.lang.GetMessage("Bet.Eco", Instance, player?.UserIDString);
                }
                return string.Empty;
            }
            #endregion
        }

        internal class GameChair : MonoBehaviour
        {
            private BaseMountable mountable;

            public CardGame CardGame { get; private set; }

            public int Number { get; private set; }

            private void Awake()
            {
                mountable = GetComponent<BaseMountable>();

                enabled = false;
            }

            internal void SetCardGame(CardGame cardGame, int number)
            {
                this.CardGame = cardGame;
                this.Number = number;
            }
        }

        internal class TableInformer : MonoBehaviour
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
                bounds.Expand(Instance.configData.DDrawBoundsMultiplier);

                worldSpaceBounds = new OBB(transform.position, transform.lossyScale, transform.rotation, bounds);                 
            }

            public void OnGameInitialized()
            {
                informationStr = string.Format(Instance.lang.GetMessage("Table.Information", Instance), cardGame.gameData.gameType, cardGame.gameData.minimumBet, cardGame.gameData.maximumBet, cardGame.FormatBetString());

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

        internal class TimerElement : MonoBehaviour
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

                for (int i = 0; i < CardGame.cardPlayers.Length; i++)
                {
                    CardPlayer cardPlayer = CardGame.cardPlayers[i];
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
                CuiElementContainer container = UI.ElementContainer(TIMER_OVERLAY, TIMER_POSITION);
                UI.Label(ref container, TIMER_OVERLAY, $"{timeRemaining}", 18, UI4.FullScreen);

                for (int i = 0; i < CardGame.cardPlayers.Length; i++)
                {
                    CardPlayer cardPlayer = CardGame.cardPlayers[i];
                    if (cardPlayer == null)
                        continue;

                    cardPlayer.AddUI(TIMER_OVERLAY, container);
                }
            }
        }


        public class CardPlayer : MonoBehaviour
        {            
            internal List<Card> hand = new List<Card>();

            internal List<string> uiPanels = new List<string>();

            internal BasePlayer Player { get; private set; }

            internal Casino.CardGame CardGame { get; set; }

            internal bool IsLeaving { get; private set; }

            internal int UID { get; private set; }

            internal int BetAmount { get; set; }

            internal bool BetLocked { get; set; }

            internal bool IsPlaying { get; set; } = false;

            internal int Position { get; set; }

            public int BankBalance
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

            internal virtual void Awake()
            {
                Player = GetComponent<BasePlayer>();
                CardGame = Player.GetMounted().GetComponent<Casino.GameChair>().CardGame;
                BetAmount = CardGame.gameData.minimumBet;

                GenerateUID();
                enabled = false;
            }

            internal virtual void OnDestroy()
            {
                IsLeaving = true;

                DestroyUI();

                if (Player.isMounted)
                {
                    Player.GetMounted().DismountPlayer(Player, false);
                }
            }

            internal void GenerateUID() => UID = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

            internal virtual void ResetHand()
            {
                hand.Clear();
                BetAmount = CardGame.gameData.minimumBet;
                BetLocked = false;
                IsPlaying = false;
            }

            internal void SetBet()
            {
                BetLocked = true;
                CardGame.TakeAmount(Player, BetAmount);                
            }

            internal void IssueWin(int amount)
            {
                CardGame.GiveAmount(Player, amount);               
            }

            internal void AddUI(string str, CuiElementContainer container)
            {
                DestroyUI(str);
                uiPanels.Add(str);                   
                CuiHelper.AddUi(Player, container);
            }

            internal void DestroyUI(string str)
            {
                uiPanels.Remove(str);
                CuiHelper.DestroyUi(Player, str);
            }

            internal void DestroyUI()
            {
                foreach (string str in uiPanels)                
                    CuiHelper.DestroyUi(Player, str);                

                uiPanels.Clear();
            }
        }

        public class Deck
        {
            private Queue<Card> deckOfCards = new Queue<Card>();

            private List<int> cardsAsInt = new List<int>();

            private System.Random generator = new System.Random();

            public Deck()
            {
                Shuffle();
            }

            private void GenerateCardsAsInt()
            {
                cardsAsInt.Clear();
                for (int i = 0; i < 52; i++)
                {
                    cardsAsInt.Add(i);
                }
            }

            public void Shuffle()
            {
                deckOfCards.Clear();
                GenerateCardsAsInt();
                for (int i = 51; i >= 0; i--)
                {
                    int index = generator.Next(0, i);
                    int temp = cardsAsInt[i];
                    cardsAsInt[i] = cardsAsInt[index];
                    cardsAsInt[index] = temp;
                }
                FillDeck();
            }

            private void FillDeck()
            {
                for (int i = 0; i < cardsAsInt.Count; i++)
                {
                    CardSuit suit = (CardSuit)(cardsAsInt[i] % 4);
                    CardValue value = (CardValue)(cardsAsInt[i] % 13 + 1);
                    deckOfCards.Enqueue(new Card(suit, value));
                }
            }

            public Card DealCard()
            {
                return deckOfCards.Dequeue();
            }
        }

        public struct Card
        {            
            public CardSuit Suit { get; private set; }

            public CardValue Value { get; private set; }

            public static Dictionary<CardSuit, Dictionary<CardValue, string>> CardImages = new Dictionary<CardSuit, Dictionary<CardValue, string>>()
            {
                [CardSuit.Clubs] = new Dictionary<CardValue, string>(),
                [CardSuit.Diamonds] = new Dictionary<CardValue, string>(),
                [CardSuit.Hearts] = new Dictionary<CardValue, string>(),
                [CardSuit.Spades] = new Dictionary<CardValue, string>()
            };


            public Card(CardSuit suit, CardValue value) : this()
            {
                this.Suit = suit;
                this.Value = value;
            }

            public override string ToString() => string.Format("Suit: {0}, Value: {1}", this.Suit, this.Value);

            public string GetCardImage()
            {
                if (!Card.CardImages[this.Suit].ContainsKey(this.Value))
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

                    switch (this.Suit)
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
                    string str = PlayingCards.GetCardImage(value, suit);

                    Card.CardImages[this.Suit].Add(this.Value, str);

                    return str;
                }

                else return Card.CardImages[this.Suit][this.Value];
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
            public static CuiElementContainer ElementContainer(string panel, string color, UI4 dimensions, bool useCursor = false, string parent = "Overlay")
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax()},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panel
                    }
                };
                return container;
            }

            public static CuiElementContainer ElementContainer(string panel, UI4 dimensions, bool useCursor = false, string parent = "Overlay")
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

            public static CuiElementContainer Popup(string panelName, string text, int size, UI4 dimensions, TextAnchor align = TextAnchor.MiddleCenter, string parent = "Overlay")
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel { Image = {Color = "0 0 0 0" }, RectTransform = {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax()} },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                },
                panelName);
                return container;
            }

            public static void Panel(ref CuiElementContainer container, string panel, string color, UI4 dimensions, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() },
                    CursorEnabled = cursor
                },
                panel);
            }

            public static void Label(ref CuiElementContainer container, string panel, string text, int size, UI4 dimensions, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                },
                panel);
            }

            public static void OutlineLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string distance, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, string parent = "Overlay")
            {
                CuiElement textElement = new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    FadeOut = 0.2f,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = text,
                            FontSize = size,
                            Align = TextAnchor.MiddleCenter,
                            FadeIn = 0.2f
                        },
                        new CuiOutlineComponent
                        {
                            Distance = distance,
                            Color = color
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = aMin,
                            AnchorMax = aMax
                        }
                    }
                };
                container.Add(textElement);
            }

            public static void Button(ref CuiElementContainer container, string panel, string color, string text, int size, UI4 dimensions, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 0f },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }

            public static void Button(ref CuiElementContainer container, string panel, UI4 dimensions, string command)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = command, FadeIn = 0f },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() },
                    Text = { Text = string.Empty, FontSize = 0, Align = TextAnchor.LowerCenter }
                },
                panel);
            }

            public static void Image(ref CuiElementContainer container, string panel, string png, UI4 dimensions)
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

            public static void Input(ref CuiElementContainer container, string panel, string text, int size, string command, UI4 dimensions)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Align = TextAnchor.MiddleLeft,
                            CharsLimit = 300,
                            Command = command + text,
                            FontSize = size,
                            IsPassword = false,
                            Text = text
                        },
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

            private static UI4 _fullScreen;

            public static UI4 FullScreen
            {
                get
                {
                    if (_fullScreen == null)
                        _fullScreen = new UI4(0, 0, 1, 1);
                    return _fullScreen;
                }
            }
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
                    GameType type = cardgame.gameType;

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
        public enum BettingType { Item, ServerRewards, Economics }

        private ConfigData configData;

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

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                BettingItem = "scrap",
                BettingType = "Item",
                WipeOnNewSave = true,
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            if (configData.Version < new VersionNumber(0, 1, 7))
            {
                configData.DDrawBoundsMultiplier = 1.5f;

                if (string.IsNullOrEmpty(configData.BettingType))
                    configData.BettingType = "Item";
            }

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Data Management
        public enum GameType { BlackJack/*, TexasHoldem*/ }

        public enum GameState { Waiting, PlacingBets, Prestart, Playing }

        private void SaveData() => data.WriteObject(storedData);

        private void LoadData()
        {
            try
            {
                storedData = data.ReadObject<StoredData>();
            }
            catch
            {
                storedData = new StoredData();
            }
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
            }
        }
        #endregion
    }
}
