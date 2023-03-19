//Requires: Casino
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BlackJack", "k1lly0u", "0.1.9")]
    [Description("A Black Jack card game used with the Casino plugin")]
    class BlackJack : RustPlugin
    {
        #region Fields
        public static BlackJack Instance { get; private set; }       
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            Instance = this;
            lang.RegisterMessages(Messages, this);
        }

        private void OnServerInitialized()
        {
            Casino.Instance.RegisterGame(Casino.GameType.BlackJack, InitializeGame);
        }

        private void Unload()
        {
            Casino.Instance?.UnregisterGame(Casino.GameType.BlackJack);
            Instance = null;
            Configuration = null;
        }
        #endregion

        #region Functions
        private void InitializeGame(BaseEntity table, Casino.StoredData.GameData gameData)
        {
            BlackJackGame blackjackGame = table.gameObject.AddComponent<BlackJackGame>();
            blackjackGame.InitializeGame(gameData);
        }
        
        #endregion

        #region Game Manager
        private const string GAME_BG = "blackjack.background";

        private const string PLAYER_OVERLAY = "blackjack.players";

        private const string BALANCE_OVERLAY = "blackjack.balance";

        private const string BETTING_OVERLAY = "blackjack.betting";

        private const string MESSAGE_OVERLAY = "blackjack.message";

        private const string NOTIFICATION_OVERLAY = "blackjack.notification";

        private const string PLAY_OVERLAY = "blackjack.play";

        private const string STATUS_OVERLAY = "blackjack.status";

        private static string BLACK_COLOR = Casino.UI.Color("000000", 0.7f);

        public class BlackJackGame : Casino.CardGame
        {
            public override Casino.GameType Type => Casino.GameType.BlackJack;

            public BlackJackAI _dealerAI;

            private int _playerIndex = -1;

            private readonly Casino.Deck _deck = new Casino.Deck();

            private readonly Dictionary<string, CuiElementContainer> _containers = new Dictionary<string, CuiElementContainer>();

            private CuiElementContainer _backgroundContainer;

            public readonly Casino.UI4[] _cardPositions = new Casino.UI4[]
            {
                new Casino.UI4(0.315f, 0.15f, 0.38f, 0.33f),
                new Casino.UI4(0.55f, 0.15f, 0.615f, 0.33f),
                new Casino.UI4(0.1f, 0.25f, 0.165f, 0.43f),
                new Casino.UI4(0.765f, 0.25f, 0.83f, 0.43f),
                new Casino.UI4(0.435f, 0.7f, 0.5f, 0.88f)
            };

            #region Game Initialization
            public override void OnGameInitialized()
            {
                SetTableSkinID(Configuration.SkinID);

                _dealerAI = new GameObject("Blackjack_Dealer").AddComponent<BlackJackAI>();
                _dealerAI.Position = 4;
                _dealerAI.RegisterCardGame(this);

                base.OnGameInitialized();

                _backgroundContainer = Casino.UI.Container(GAME_BG, Casino.UI4.FullScreen, true);
                Casino.UI.Image(_backgroundContainer, GAME_BG, Casino.Images.GetBoardImage(Instance.Title.ToLower()), Casino.UI4.FullScreen);
            }

            private void ResetGame()
            {
                Timer.StopTimer();

                _dealerAI.ResetHand();

                for (int i = 0; i < Players.Length; i++)
                {
                    Casino.CardPlayer cardPlayer = Players[i];
                    if (cardPlayer != null)
                        cardPlayer.ResetHand();
                }

                _playerIndex = -1;

                InvokeHandler.CancelInvoke(this, PlaceBets);

                InvokeHandler.CancelInvoke(this, PreStartRound);

                InvokeHandler.CancelInvoke(this, StartRound);

                InvokeHandler.CancelInvoke(this, NextPlayerTurn);

                InvokeHandler.CancelInvoke(this, _dealerAI.PlayTurn);

                InvokeHandler.CancelInvoke(this, FinalizeGame);

                InvokeHandler.CancelInvoke(this, ResetGame);

                DestroyUIElements();

                State = Casino.GameState.Waiting;

                _deck.Shuffle();

                if (CurrentPlayerCount > 0)
                {
                    CreatePlayerUI();

                    CreateUIMessage(string.Format(msg("UI.Notification.StartsIn"), 5));

                    InvokeHandler.Invoke(this, PlaceBets, 5f);
                }
            }
            #endregion

            #region Player Enter/Exit
            public override void OnPlayerEnter(BasePlayer player, int position)
            {
                BlackJackPlayer blackjackPlayer = player.gameObject.AddComponent<BlackJackPlayer>();
                Players[position] = blackjackPlayer;

                blackjackPlayer.Position = position;

                blackjackPlayer.AddUI(GAME_BG, _backgroundContainer);
                                
                if (State == Casino.GameState.Waiting)
                {
                    CreatePlayerUI();

                    if (CurrentPlayerCount == 1)
                    {
                        CreateUIMessage(string.Format(msg("UI.Notification.StartsIn"), 10));

                        if (!InvokeHandler.IsInvoking(this, PlaceBets))
                            InvokeHandler.Invoke(this, PlaceBets, 10f);
                    }
                    else CreateUIMessage(msg("UI.Notification.IsStarting"));
                }
                else
                {
                    if (State == Casino.GameState.PlacingBets)
                    {
                        CreatePlayerUI();

                        CreateUIMessage(blackjackPlayer, msg("UI.Notification.UsersPlacingBets"));

                        for (int i = 0; i < Players.Length; i++)
                        {
                            Casino.CardPlayer cardPlayer = Players[i];
                            if (cardPlayer == null || !cardPlayer.IsPlaying)
                                continue;

                            PlaceBets(cardPlayer as BlackJackPlayer);
                        }
                    }
                    else if (State == Casino.GameState.Prestart)
                    {
                        CreatePlayerUI(true);

                        CreateUIMessage(blackjackPlayer, msg("UI.Notification.GameInProgress"));

                        foreach (KeyValuePair<string, CuiElementContainer> kvp in _containers)
                            blackjackPlayer.AddUI(kvp.Key, kvp.Value);
                    }
                    else
                    {
                        CreateUIMessage(blackjackPlayer, msg("UI.Notification.GameInProgress"));

                        foreach (KeyValuePair<string, CuiElementContainer> kvp in _containers)
                            blackjackPlayer.AddUI(kvp.Key, kvp.Value);

                        CreatePlayerUI(blackjackPlayer, true);
                    }
                }
            }

            public override void OnPlayerExit(BasePlayer player)
            {
                BlackJackPlayer blackjackPlayer = player.GetComponent<BlackJackPlayer>();
                if (blackjackPlayer == null)
                    return;

                OnPlayerExit(blackjackPlayer);
            }

            private void OnPlayerExit(BlackJackPlayer blackjackPlayer)
            {                
                if (blackjackPlayer.Position < 0 || blackjackPlayer.Position > Players.Length - 1)
                {
                    for (int i = 0; i < Players.Length; i++)
                    {
                        Casino.CardPlayer cardPlayer = Players[i];
                        if (cardPlayer.Player == blackjackPlayer.Player)
                        {
                            Players[i] = null;
                            break;
                        }
                    }                    
                }
                else Players[blackjackPlayer.Position] = null;

                CreateUIMessageOffset(string.Format(msg("UI.Notification.LeftGame"), blackjackPlayer.Player.displayName), 5f);

                Destroy(blackjackPlayer);

                if (CurrentPlayerCount == 0 && State != Casino.GameState.Waiting)
                {
                    ResetGame();
                    return;
                }

                if (State == Casino.GameState.Playing)
                {
                    CreatePlayerUI(true);

                    if (_playerIndex == blackjackPlayer.Position)
                    {
                        Timer.StopTimer();
                        InvokeHandler.Invoke(this, NextPlayerTurn, 2f);
                    }
                    else
                    {
                        Casino.CardPlayer currentPlayer = Players[_playerIndex];

                        if (currentPlayer != null && currentPlayer.IsPlaying)
                            CreatePlayOverlay(currentPlayer as BlackJackPlayer);
                    }
                }
                else
                {
                    if (State == Casino.GameState.PlacingBets)
                    {
                        for (int i = 0; i < Players.Length; i++)
                        {
                            Casino.CardPlayer cardPlayer = Players[i];
                            if (cardPlayer == null || !cardPlayer.IsPlaying)
                                continue;
                            
                            PlaceBets(cardPlayer as BlackJackPlayer);
                        }
                    }
                    else CreatePlayerUI(State == Casino.GameState.Prestart);
                }
            }
            #endregion

            #region Betting
            private void PlaceBets()
            {
                if (CurrentPlayerCount == 0)
                    return;

                DestroyUIElement(MESSAGE_OVERLAY);                

                for (int i = 0; i < Players.Length; i++)
                {
                    BlackJackPlayer cardPlayer = Players[i] as BlackJackPlayer;
                    if (cardPlayer == null)
                        continue;

                    if (cardPlayer.BankBalance < Data.minimumBet)
                    {
                        cardPlayer.Player.ChatMessage(string.Format(msg("Chat.Kicked.NotEnough", cardPlayer.Player.UserIDString), FormatBetString(cardPlayer.Player)));
                        OnPlayerExit(cardPlayer);
                    }
                }

                for (int i = 0; i < Players.Length; i++)
                {
                    BlackJackPlayer cardPlayer = Players[i] as BlackJackPlayer;
                    if (cardPlayer == null)
                        continue;

                    cardPlayer.IsPlaying = true;

                    PlaceBets(cardPlayer);
                }

                State = Casino.GameState.PlacingBets;

                Timer.StartTimer(30);

                InvokeHandler.Invoke(this, PreStartRound, 30f);
            }

            public void PlaceBets(BlackJackPlayer blackjackPlayer)
            {
                blackjackPlayer.DestroyUI(MESSAGE_OVERLAY);

                CuiElementContainer container = Casino.UI.Container(BETTING_OVERLAY, Casino.UI4.FullScreen);

                if (!blackjackPlayer.BetLocked)
                {
                    Casino.UI.Label(container, BETTING_OVERLAY, msg("UI.Notification.PlaceBets"), 20, new Casino.UI4(0.3f, 0.4f, 0.7f, 0.6f));

                    Casino.UI4 chipsPosition = new Casino.UI4(0.385f, 0.36f, 0.615f, 0.44f);
                    Casino.UI.Image(container, BETTING_OVERLAY, Casino.Images.GetChipStackImage(), chipsPosition);

                    for (int i = 0; i < 5; i++)
                    {
                        Casino.UI.Button(container, BETTING_OVERLAY, new Casino.UI4(chipsPosition.xMin + (i * 0.046f), chipsPosition.yMin, chipsPosition.xMax + (i * 0.046f), chipsPosition.yMax), $"blackjack.bet {i}");
                    }

                    Casino.UI.Panel(container, BETTING_OVERLAY, BLACK_COLOR, new Casino.UI4(0.385f, 0.33f, 0.4975f, 0.355f));
                    Casino.UI.Panel(container, BETTING_OVERLAY, BLACK_COLOR, new Casino.UI4(0.5005f, 0.33f, 0.615f, 0.355f));
                    Casino.UI.Label(container, BETTING_OVERLAY, string.Format(msg("UI.Player.BalanceAmount"), blackjackPlayer.BankBalance, FormatBetString(blackjackPlayer.Player)), 12, new Casino.UI4(0.39f, 0.33f, 0.5f, 0.355f), TextAnchor.MiddleLeft);
                    Casino.UI.Label(container, BETTING_OVERLAY, string.Format(msg("UI.Player.BetAmount"), blackjackPlayer.BetAmount), 12, new Casino.UI4(0.505f, 0.33f, 0.615f, 0.355f), TextAnchor.MiddleLeft);
                    Casino.UI.Button(container, BETTING_OVERLAY, Casino.UI.Color("#ce422b", 0.7f), " - ", 12, new Casino.UI4(0.6f, 0.33f, 0.615f, 0.355f), "blackjack.deductbet");

                    Casino.UI.Button(container, BETTING_OVERLAY, BLACK_COLOR, msg("UI.Player.ResetBet"), 12, new Casino.UI4(0.385f, 0.3f, 0.4975f, 0.325f), "blackjack.bet -1");
                    Casino.UI.Button(container, BETTING_OVERLAY, BLACK_COLOR, msg("UI.Player.LockBet"), 12, new Casino.UI4(0.5005f, 0.3f, 0.615f, 0.325f), "blackjack.setbet");
                }
                else
                {
                    bool waitingForBets = false;
                    for (int i = 0; i < Players.Length; i++)
                    {
                        Casino.CardPlayer cardPlayer = Players[i];
                        if (cardPlayer == null || !cardPlayer.IsPlaying)
                            continue;

                        if (!cardPlayer.BetLocked)
                        {
                            waitingForBets = true;
                            break;
                        }
                    }

                    if (waitingForBets)
                    {
                        Casino.UI.Label(container, BETTING_OVERLAY, msg("UI.Notification.Waiting"), 20, new Casino.UI4(0.3f, 0.45f, 0.7f, 0.55f));
                    }
                    else
                    {
                        InvokeHandler.CancelInvoke(this, PreStartRound);

                        DestroyUIElement(BETTING_OVERLAY);

                        PreStartRound();
                    }
                }
                Casino.UI.Button(container, BETTING_OVERLAY, new Casino.UI4(0.9f, 0.95f, 0.99f, 0.98f), "casino.leavetable");

                blackjackPlayer.AddUI(BETTING_OVERLAY, container);
            }
            #endregion

            #region Round Start
            private void PreStartRound()
            {
                if (State == Casino.GameState.Prestart)
                    return;

                Timer.StopTimer();

                DestroyUIElement(BETTING_OVERLAY);

                if (State != Casino.GameState.PlacingBets || CurrentPlayerCount == 0)
                {                    
                    ResetGame();
                    return;
                }

                State = Casino.GameState.Prestart;

                for (int i = 0; i < Players.Length; i++)
                {
                    Casino.CardPlayer cardPlayer = Players[i];
                    if (cardPlayer != null && cardPlayer.IsPlaying)
                    {
                        if (!cardPlayer.BetLocked)
                            cardPlayer.SetBet();
                    }
                }

                CreatePlayerUI(true);

                CreateUIMessage(msg("UI.Notification.BeginsIn"));

                InvokeHandler.Invoke(this, StartRound, 5f);
            }

            private void StartRound()
            {
                ServerMgr.Instance.StartCoroutine(DealCards());
            }
            #endregion

            #region Deal Cards
            private IEnumerator DealCards()
            {
                DestroyUIElement(MESSAGE_OVERLAY);

                State = Casino.GameState.Playing;

                for (int count = 0; count < 2; count++)
                {
                    for (int i = 0; i < Players.Length; i++)
                    {
                        Casino.CardPlayer cardPlayer = Players[i];
                        if (cardPlayer != null && cardPlayer.IsPlaying)
                        {
                            DealCard(cardPlayer as BlackJackPlayer);

                            yield return CoroutineEx.waitForSeconds(0.5f);
                        }
                    }

                    DealCard(_dealerAI, count == 1);

                    yield return CoroutineEx.waitForSeconds(0.5f);
                }

                if (CurrentPlayingCount == 0)
                    ResetGame();
                else NextPlayerTurn();
            }

            public void DealCard(BlackJackPlayer blackjackPlayer, bool hidden = false)
            {
                Casino.Card card = _deck.Deal();

                blackjackPlayer.Hit(card);

                int handCount = blackjackPlayer.Count;

                string panel = $"{blackjackPlayer.UID}.card.{handCount}";

                float offset = (float)(handCount - 1) * 0.015f;

                Casino.UI4 cardPosition = _cardPositions[blackjackPlayer.Position];

                Casino.UI4 elementPosition = new Casino.UI4(cardPosition.xMin + offset, cardPosition.yMin, cardPosition.xMax + offset, cardPosition.yMax);
               
                CuiElementContainer container = Casino.UI.Container(panel, elementPosition);
                Casino.UI.Image(container, panel, hidden ? Casino.Images.GetCardBackground(Configuration.CardColor) : card.GetCardImage(), Casino.UI4.FullScreen);

                AddUIElement(panel, container, true);
            }
            #endregion

            #region Player Turns
            private void NextPlayerTurn()
            {
                if (_playerIndex >= 0)
                {
                    Casino.CardPlayer cardPlayer = Players[_playerIndex];
                    if (cardPlayer != null && cardPlayer.IsPlaying)
                    {
                        cardPlayer.DestroyUI(PLAY_OVERLAY);
                    }
                }
               
                _playerIndex++;
                UserPlayTurn();
            }

            private void UserPlayTurn()
            {
                Timer.StopTimer();

                DestroyUIElement(MESSAGE_OVERLAY);

                if (_playerIndex >= Players.Length)
                {
                    _playerIndex = -1;

                    CreateUIMessage(string.Format(msg("UI.Notification.CurrentlyPlaying"), msg("UI.Dealer")));

                    InvokeHandler.Invoke(this, _dealerAI.PlayTurn, 1f);                    
                    return;
                }

                Casino.CardPlayer cardPlayer = Players[_playerIndex];
                if (cardPlayer == null || !cardPlayer.IsPlaying)
                {
                    NextPlayerTurn();
                    return;
                }

                if ((cardPlayer as BlackJackPlayer).HasBlackJack())
                {
                    CreateUIMessage(string.Format(msg("UI.Notification.HasBlackJack"), cardPlayer.Player.displayName));
                    InvokeHandler.Invoke(this, NextPlayerTurn, 5f);
                    return;
                }

                CreateUIMessage(string.Format(msg("UI.Notification.CurrentlyPlaying"), cardPlayer.Player.displayName));

                CreatePlayOverlay(cardPlayer as BlackJackPlayer);

                Timer.StartTimer(30);

                if (InvokeHandler.IsInvoking(this, NextPlayerTurn))
                    InvokeHandler.CancelInvoke(this, NextPlayerTurn);

                InvokeHandler.Invoke(this, NextPlayerTurn, 30f);
            }

            public void Hit(BlackJackPlayer blackjackPlayer)
            {
                Timer.StopTimer();

                DealCard(blackjackPlayer);

                int score = blackjackPlayer.GetScore();

                if (score > 21 || score == 21)
                    Stay(blackjackPlayer);
                else UserPlayTurn();
            }

            public void Stay(BlackJackPlayer blackjackPlayer)
            {
                Timer.StopTimer();

                blackjackPlayer.DestroyUI(PLAY_OVERLAY);

                if (blackjackPlayer.IsBusted)
                    CreateUIMessage(string.Format(msg("UI.Notification.HasBust"), blackjackPlayer.Player.displayName));
                else CreateUIMessage(string.Format(msg("UI.Notification.StayingOn"), blackjackPlayer.Player.displayName, blackjackPlayer.GetScore()));

                InvokeHandler.Invoke(this, NextPlayerTurn, 5f);
            }

            public void DoubleDown(BlackJackPlayer blackjackPlayer)
            {
                Timer.StopTimer();

                blackjackPlayer.PerformDoubleDown();

                CreatePlayerUI(true);

                DealCard(blackjackPlayer);

                if (blackjackPlayer.IsBusted)
                    CreateUIMessage(string.Format(msg("UI.Notification.DblDown.HasBust"), blackjackPlayer.Player.displayName));
                else CreateUIMessage(string.Format(msg("UI.Notification.DblDown"), blackjackPlayer.Player.displayName, blackjackPlayer.GetScore()));

                blackjackPlayer.DestroyUI(PLAY_OVERLAY);
                InvokeHandler.Invoke(this, NextPlayerTurn, 5f);
            }

            public void Insurance(BlackJackPlayer blackjackPlayer)
            {
                Timer.StopTimer();

                blackjackPlayer.PerformInsurance();

                CreatePlayerUI(true);

                CreateUIMessage(string.Format(msg("UI.Notification.Insurance"), blackjackPlayer.Player.displayName));

                UserPlayTurn();
            }
            #endregion

            #region Round Finish
            public void FinalizeGame()
            {
                DestroyUIElement(MESSAGE_OVERLAY);

                CalculateScores();

                CreateUIMessage(string.Format(msg("UI.Notification.StartsIn"), 5));

                InvokeHandler.Invoke(this, ResetGame, 5f);
            }

            private void CalculateScores()
            {
                int dealerScore = _dealerAI.GetScore();

                CuiElementContainer container = Casino.UI.Container(STATUS_OVERLAY, Casino.UI4.FullScreen);

                Casino.UI4 cardPosition;
                Casino.UI4 position;

                for (int i = 0; i < Players.Length; i++)
                {
                    Casino.CardPlayer cardPlayer = Players[i];
                    if (cardPlayer == null || !cardPlayer.IsPlaying)
                        continue;

                    cardPlayer.DestroyUI(BALANCE_OVERLAY);

                    BlackJackPlayer blackjackPlayer = cardPlayer as BlackJackPlayer;

                    cardPosition = _cardPositions[blackjackPlayer.Position];
                    position = new Casino.UI4(cardPosition.xMin, cardPosition.yMax + 0.01f, cardPosition.xMin + 0.12f, cardPosition.yMax + 0.035f);

                    Casino.UI.Panel(container, STATUS_OVERLAY, BLACK_COLOR, position);

                    if (blackjackPlayer.IsBusted)
                        Casino.UI.Label(container, STATUS_OVERLAY, msg("UI.Status.Bust"), 12, position);
                    else
                    {
                        int playerScore = blackjackPlayer.GetScore();

                        int insuranceWin = blackjackPlayer.HasInsurance && _dealerAI.HasBlackJack() ? blackjackPlayer.BetAmount : 0;

                        if (blackjackPlayer.HasBlackJack() && !_dealerAI.HasBlackJack())
                        {
                            if (Configuration.ShowWinnings)
                                Casino.UI.Label(container, STATUS_OVERLAY, string.Format(msg("UI.Status.BlackJack.Amount"), "+" + Mathf.CeilToInt((float)blackjackPlayer.BetAmount * 1.5f)), 12, position);
                            else Casino.UI.Label(container, STATUS_OVERLAY, msg("UI.Status.BlackJack"), 12, position);

                            blackjackPlayer.IssueWin(blackjackPlayer.BetAmount + Mathf.CeilToInt((float)blackjackPlayer.BetAmount * 1.5f));
                        }
                        else if (playerScore == dealerScore)
                        {
                            if (Configuration.ShowWinnings)
                                Casino.UI.Label(container, STATUS_OVERLAY, string.Format(msg("UI.Status.Tie.Amount"), 0), 12, position);
                            else Casino.UI.Label(container, STATUS_OVERLAY, msg("UI.Status.Tie"), 12, position);

                            blackjackPlayer.IssueWin(blackjackPlayer.BetAmount + insuranceWin);
                        }
                        else if (playerScore > dealerScore || _dealerAI.IsBusted)
                        {
                            if (Configuration.ShowWinnings)
                                Casino.UI.Label(container, STATUS_OVERLAY, string.Format(msg("UI.Status.Win.Amount"), "+" + blackjackPlayer.BetAmount), 12, position);
                            else Casino.UI.Label(container, STATUS_OVERLAY, msg("UI.Status.Win"), 12, position);

                            blackjackPlayer.IssueWin(blackjackPlayer.BetAmount * 2);
                        }
                        else if (playerScore < dealerScore)
                        {
                            if (Configuration.ShowWinnings)
                            {
                                if (insuranceWin > 0)
                                {
                                    Casino.UI.Label(container, STATUS_OVERLAY, msg("UI.Status.Lost.Insurance"), 12, position);
                                    blackjackPlayer.IssueWin(insuranceWin);
                                }
                                else Casino.UI.Label(container, STATUS_OVERLAY, string.Format(msg("UI.Status.Lost.Amount"), "-" + blackjackPlayer.BetAmount), 12, position);
                            }
                            else
                            {
                                if (insuranceWin > 0)
                                {
                                    Casino.UI.Label(container, STATUS_OVERLAY, msg("UI.Status.Lost.Insurance"), 12, position);
                                    blackjackPlayer.IssueWin(insuranceWin);
                                }
                                else Casino.UI.Label(container, STATUS_OVERLAY, msg("UI.Status.Lost"), 12, position);
                            }
                        }
                    }
                }

                cardPosition = _cardPositions[4];
                position = new Casino.UI4(cardPosition.xMin, cardPosition.yMin - 0.065f, cardPosition.xMin + 0.12f, cardPosition.yMin - 0.04f);

                if (_dealerAI.IsBusted)
                {
                    Casino.UI.Panel(container, STATUS_OVERLAY, BLACK_COLOR, position);
                    Casino.UI.Label(container, STATUS_OVERLAY, msg("UI.Status.Bust"), 12, position);
                }
                else if (_dealerAI.HasBlackJack())
                {
                    Casino.UI.Panel(container, STATUS_OVERLAY, BLACK_COLOR, position);
                    Casino.UI.Label(container, STATUS_OVERLAY, msg("UI.Status.BlackJack"), 12, position);
                }

                Casino.UI.Button(container, STATUS_OVERLAY, new Casino.UI4(0.9f, 0.95f, 0.99f, 0.98f), "casino.leavetable");

                AddUIElement(STATUS_OVERLAY, container);
            }
            #endregion

            #region Player UI
            private void CreatePlayerUI(bool betsLocked = false)
            {
                CuiElementContainer container = CreatePlayerUIElement(betsLocked);

                AddUIElement(PLAYER_OVERLAY, container, false);
            }

            private void CreatePlayerUI(Casino.CardPlayer targetPlayer, bool betsLocked = false)
            {
                CuiElementContainer container = CreatePlayerUIElement(betsLocked);

                targetPlayer.AddUI(PLAYER_OVERLAY, container);
            }

            private CuiElementContainer CreatePlayerUIElement(bool betsLocked)
            {
                CuiElementContainer container = Casino.UI.Container(PLAYER_OVERLAY, Casino.UI4.FullScreen);
                Casino.UI4 cardPosition;
                Casino.UI4 position;

                for (int i = 0; i < Players.Length; i++)
                {
                    Casino.CardPlayer cardPlayer = Players[i];
                    if (Players[i] == null)
                        continue;

                    cardPosition = _cardPositions[i];
                    position = new Casino.UI4(cardPosition.xMin, cardPosition.yMin - 0.035f, cardPosition.xMin + 0.12f, cardPosition.yMin - 0.01f);
                    Casino.UI.Panel(container, PLAYER_OVERLAY, BLACK_COLOR, position);
                    Casino.UI.Label(container, PLAYER_OVERLAY, cardPlayer.Player.displayName, 12, position, TextAnchor.MiddleCenter);

                    if (betsLocked)
                    {
                        position = new Casino.UI4(cardPosition.xMin, cardPosition.yMin - 0.065f, cardPosition.xMin + 0.12f, cardPosition.yMin - 0.04f);
                        Casino.UI.Panel(container, PLAYER_OVERLAY, BLACK_COLOR, position);
                        Casino.UI.Label(container, PLAYER_OVERLAY, cardPlayer.IsPlaying ? string.Format(msg("UI.Player.Bet"), cardPlayer.BetAmount) : msg("UI.Player.PlayingNext"), 12, position, TextAnchor.MiddleCenter);

                        if (Configuration.ShowBalance)
                        {
                            CuiElementContainer balance = Casino.UI.Container(BALANCE_OVERLAY, new Casino.UI4(cardPosition.xMin, cardPosition.yMax + 0.01f, cardPosition.xMin + 0.12f, cardPosition.yMax + 0.035f));
                            Casino.UI.Panel(balance, BALANCE_OVERLAY, BLACK_COLOR, Casino.UI4.FullScreen);
                            Casino.UI.Label(balance, BALANCE_OVERLAY, string.Format(msg("UI.Player.BalanceAmount"), cardPlayer.BankBalance, FormatBetString(cardPlayer.Player)), 12, Casino.UI4.FullScreen, TextAnchor.MiddleCenter);
                            cardPlayer.AddUI(BALANCE_OVERLAY, balance);
                        }
                    }
                }

                cardPosition = _cardPositions[4];
                position = new Casino.UI4(cardPosition.xMin, cardPosition.yMin - 0.035f, cardPosition.xMin + 0.12f, cardPosition.yMin - 0.01f);
                Casino.UI.Panel(container, PLAYER_OVERLAY, BLACK_COLOR, position);
                Casino.UI.Label(container, PLAYER_OVERLAY, msg("UI.Dealer"), 12, position);

                Casino.UI.Button(container, PLAYER_OVERLAY, BLACK_COLOR, msg("UI.LeaveTable"), 14, new Casino.UI4(0.9f, 0.95f, 0.99f, 0.98f), "casino.leavetable");

                return container;
            }

            private void CreatePlayOverlay(BlackJackPlayer blackjackPlayer)
            {
                Casino.UI4 cardPosition = _cardPositions[blackjackPlayer.Position];
                Casino.UI4 position = new Casino.UI4(cardPosition.xMin, cardPosition.yMin - 0.095f, cardPosition.xMin + 0.12f, cardPosition.yMin - 0.07f);

                CuiElementContainer container = Casino.UI.Container(PLAY_OVERLAY, position);

                Casino.UI.Button(container, PLAY_OVERLAY, BLACK_COLOR, msg("UI.Player.Hit"), 12, new Casino.UI4(0f, 0f, 0.495f, 1f), "blackjack.hit");
                Casino.UI.Button(container, PLAY_OVERLAY, BLACK_COLOR, msg("UI.Player.Stay"), 12, new Casino.UI4(0.505f, 0f, 1f, 1f), "blackjack.stay");

                if (blackjackPlayer.CanInsurance())
                    Casino.UI.Button(container, PLAY_OVERLAY, BLACK_COLOR, msg("UI.Player.Insurance"), 12, new Casino.UI4(0f, -1.1f, 0.495f, -0.1f), "blackjack.insurance");

                if (blackjackPlayer.CanDoubleDown())
                    Casino.UI.Button(container, PLAY_OVERLAY, BLACK_COLOR, msg("UI.Player.DblDown"), 12, new Casino.UI4(0.505f, -1.1f, 1f, -0.1f), "blackjack.dbldown");

                blackjackPlayer.AddUI(PLAY_OVERLAY, container);
            }

            #endregion

            #region Player UI Messages
            public void CreateUIMessage(string message)
            {
                CuiElementContainer container = Casino.UI.Container(MESSAGE_OVERLAY, new Casino.UI4(0.2f, 0.45f, 0.8f, 0.55f));
                Casino.UI.Label(container, MESSAGE_OVERLAY, message, 20, Casino.UI4.FullScreen);

                AddUIElement(MESSAGE_OVERLAY, container);
            }

            public void CreateUIMessage(BlackJackPlayer blackjackPlayer, string message)
            {
                CuiElementContainer container = Casino.UI.Container(MESSAGE_OVERLAY, new Casino.UI4(0.2f, 0.45f, 0.8f, 0.55f));
                Casino.UI.Label(container, MESSAGE_OVERLAY, message, 20, Casino.UI4.FullScreen);

                blackjackPlayer.AddUI(MESSAGE_OVERLAY, container);
            }

            public void CreateUIMessageOffset(string message, float time)
            {
                CuiElementContainer container = Casino.UI.Container(NOTIFICATION_OVERLAY, new Casino.UI4(0.3f, 0.89f, 0.7f, 0.97f));
                Casino.UI.Label(container, NOTIFICATION_OVERLAY, message, 18, Casino.UI4.FullScreen);

                AddUIElement(NOTIFICATION_OVERLAY, container);

                if (InvokeHandler.IsInvoking(this, () => DestroyUIElement(NOTIFICATION_OVERLAY)))
                    InvokeHandler.CancelInvoke(this, () => DestroyUIElement(NOTIFICATION_OVERLAY));

                InvokeHandler.Invoke(this, () => DestroyUIElement(NOTIFICATION_OVERLAY), time);
            }
            #endregion

            #region Table UI          
            public void DestroyUIElement(string str)
            {
                _containers.Remove(str);

                for (int i = 0; i < Players.Length; i++)
                {
                    Casino.CardPlayer cardPlayer = Players[i];
                    if (cardPlayer == null)
                        continue;

                    cardPlayer.DestroyUI(str);
                }
            }

            public void DestroyUIElements()
            {
                for (int i = 0; i < Players.Length; i++)
                {
                    Casino.CardPlayer cardPlayer = Players[i];
                    if (cardPlayer == null)
                        continue;

                    (cardPlayer as BlackJackPlayer).DestroyUIElements();
                }

                DestroyUIElement(STATUS_OVERLAY);

                _containers.Clear();
            }

            public void AddUIElement(string panel, CuiElementContainer container, bool addToList = false)
            {
                for (int i = 0; i < Players.Length; i++)
                {
                    Casino.CardPlayer cardPlayer = Players[i];
                    if (cardPlayer == null)
                        continue;

                    cardPlayer.AddUI(panel, container);
                }

                if (addToList)
                    _containers.Add(panel, container);
            }
            #endregion
        }

        public class BlackJackPlayer : Casino.CardPlayer
        {
            public bool IsBusted => GetScore() > 21;

            public bool HasInsurance = false;
            
            public void Hit(Casino.Card dealtCard) => hand.Add(dealtCard);

            public List<Casino.Card> Show() => hand;

            public Casino.Card LastCard() => hand[hand.Count - 1];

            public int Count => hand.Count;

            public override void OnDestroy()
            {
                DestroyCards();
                base.OnDestroy();
            }

            public override void ResetHand()
            {
                HasInsurance = false;
                DestroyCards();
                base.ResetHand();
            }

            public void DestroyCards()
            {
                for (int i = 0; i < hand.Count; i++)
                {
                    string panel = $"{UID}.card.{i + 1}";
                    (CardGame as BlackJackGame).DestroyUIElement(panel);
                }
            }

            public void DestroyUIElements()
            {
                for (int i = uiPanels.Count - 1; i >= 0; i--)
                {
                    string panel = uiPanels[i];
                    if (panel == GAME_BG)
                        continue;

                    DestroyUI(panel);
                }
            }

            public bool HasBlackJack()
            {
                return hand.Count == 2 && ((hand[0].Value == Casino.CardValue.Ace && (int)hand[1].Value >= 10) || (hand[1].Value == Casino.CardValue.Ace && (int)hand[0].Value >= 10));
            }

            public int GetScore()
            {
                if (HasBlackJack())
                    return 21;

                int score = 0;
                int aces = 0;

                for (int i = 0; i < hand.Count; i++)
                {
                    if (hand[i].Value == Casino.CardValue.Ace)
                    {
                        score += 11;
                        aces++;
                    }
                    else
                    {
                        if ((int)hand[i].Value >= 10)
                            score += 10;
                        else score += (int)hand[i].Value;
                    }
                }

                for (int i = 0; i < aces; i++)
                {
                    if (score > 21)
                        score -= 10;
                }

                return score;
            }

            public bool CanDoubleDown() => CardGame.GetUserAmount(Player) >= BetAmount && hand.Count == 2;

            public bool CanInsurance() => CardGame.GetUserAmount(Player) >= (BetAmount * 0.5f) && (CardGame as BlackJackGame)._dealerAI.hand[0].Value == Casino.CardValue.Ace && !HasInsurance;

            public void PerformDoubleDown()
            {
                CardGame.TakeAmount(Player, BetAmount);
                BetAmount *= 2;
            }

            public void PerformInsurance()
            {
                CardGame.TakeAmount(Player, Mathf.CeilToInt(BetAmount * 0.5f));
                HasInsurance = true;
            }
        }

        public class BlackJackAI : BlackJackPlayer
        {
            private Coroutine playRoutine;

            public override void Awake()
            {
                GenerateUID();
            }

            public override void OnDestroy()
            {
                Destroy(this.gameObject);
            }

            public override void ResetHand()
            {
                if (playRoutine != null)
                    ServerMgr.Instance.StopCoroutine(playRoutine);
                
                base.ResetHand();
            }

            public void RegisterCardGame(Casino.CardGame cardGame)
            {
                this.CardGame = cardGame;
            }

            public void PlayTurn() => playRoutine = ServerMgr.Instance.StartCoroutine(RunAI());

            public IEnumerator RunAI()
            {
                RevealHiddenCard();

                yield return CoroutineEx.waitForSeconds(0.5f);

                if (!HasBlackJack())
                {
                    while (GetScore() < 17)
                    {
                        (CardGame as BlackJackGame).DealCard(this);
                        yield return CoroutineEx.waitForSeconds(0.5f);
                    }

                    if (IsBusted)
                        (CardGame as BlackJackGame).CreateUIMessage(string.Format(msg("UI.Notification.HasBust"), msg("Dealer")));
                    else (CardGame as BlackJackGame).CreateUIMessage(string.Format(msg("UI.Notification.StayingOn"), msg("Dealer"), GetScore()));
                }
                else (CardGame as BlackJackGame).CreateUIMessage(string.Format(msg("UI.Notification.HasBlackJack"), msg("Dealer")));

                InvokeHandler.Invoke(CardGame, (CardGame as BlackJackGame).FinalizeGame, 4f);
                
                playRoutine = null;
            }

            private void RevealHiddenCard()
            {
                if (hand.Count == 0)
                    return;

                Casino.Card card = hand[1];

                string panel = $"{UID}.card.2";

                (CardGame as BlackJackGame).DestroyUIElement(panel);

                Casino.UI4 cardPosition = (CardGame as BlackJackGame)._cardPositions[4];
                Casino.UI4 elementPosition = new Casino.UI4(cardPosition.xMin + 0.015f, cardPosition.yMin, cardPosition.xMax + 0.015f, cardPosition.yMax);

                CuiElementContainer container = Casino.UI.Container(panel, elementPosition);
                Casino.UI.Image(container, panel, card.GetCardImage(), Casino.UI4.FullScreen);

                (CardGame as BlackJackGame).AddUIElement(panel, container, true);                
            }
        }    
        #endregion

        #region Commands        
        [ConsoleCommand("blackjack.hit")]
        private void ccmdHit(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            BlackJackPlayer blackjackPlayer = player.GetComponent<BlackJackPlayer>();
            if (blackjackPlayer == null)
                return;

            (blackjackPlayer.CardGame as BlackJackGame).Hit(blackjackPlayer);
        }

        [ConsoleCommand("blackjack.stay")]
        private void ccmdStay(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            BlackJackPlayer blackjackPlayer = player.GetComponent<BlackJackPlayer>();
            if (blackjackPlayer == null)
                return;

            (blackjackPlayer.CardGame as BlackJackGame).Stay(blackjackPlayer);
        }

        [ConsoleCommand("blackjack.dbldown")]
        private void ccmdDoubleDown(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            BlackJackPlayer blackjackPlayer = player.GetComponent<BlackJackPlayer>();
            if (blackjackPlayer == null)
                return;

            (blackjackPlayer.CardGame as BlackJackGame).DoubleDown(blackjackPlayer);
        }

        [ConsoleCommand("blackjack.insurance")]
        private void ccmdInsurance(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            BlackJackPlayer blackjackPlayer = player.GetComponent<BlackJackPlayer>();
            if (blackjackPlayer == null)
                return;

            (blackjackPlayer.CardGame as BlackJackGame).Insurance(blackjackPlayer);
        }

        [ConsoleCommand("blackjack.bet")]
        private void ccmdBet(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            if (arg.Args == null || arg.Args.Length == 0)
                return;

            BlackJackPlayer blackjackPlayer = player.GetComponent<BlackJackPlayer>();
            if (blackjackPlayer == null)
                return;

            BlackJackGame cardGame = (blackjackPlayer.CardGame as BlackJackGame);

            int amount = int.Parse(arg.Args[0]);
            int minimum = cardGame.Data.minimumBet;
            int maximum = cardGame.Data.maximumBet;

            switch (amount)
            {
                case -1:
                    cardGame.ResetBet(blackjackPlayer);
                    break;
                case 0:
                    cardGame.AdjustBet(blackjackPlayer, 1, minimum, maximum);
                    break;
                case 1:
                    cardGame.AdjustBet(blackjackPlayer, 10, minimum, maximum);
                    break;
                case 2:
                    cardGame.AdjustBet(blackjackPlayer, 50, minimum, maximum);
                    break;
                case 3:
                    cardGame.AdjustBet(blackjackPlayer, 100, minimum, maximum);
                    break;
                case 4:
                    cardGame.AdjustBet(blackjackPlayer, 500, minimum, maximum);
                    break;                
            }

            cardGame.PlaceBets(blackjackPlayer);
        }

        [ConsoleCommand("blackjack.deductbet")]
        private void ccmdDeductBet(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            BlackJackPlayer blackjackPlayer = player.GetComponent<BlackJackPlayer>();
            if (blackjackPlayer == null)
                return;

            int minimum = blackjackPlayer.CardGame.Data.minimumBet;
            int maximum = blackjackPlayer.CardGame.Data.maximumBet;

            blackjackPlayer.CardGame.AdjustBet(blackjackPlayer, -1, minimum, maximum);
            (blackjackPlayer.CardGame as BlackJackGame).PlaceBets(blackjackPlayer);
        }

        [ConsoleCommand("blackjack.setbet")]
        private void ccmdSetBet(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            BlackJackPlayer blackjackPlayer = player.GetComponent<BlackJackPlayer>();
            if (blackjackPlayer == null)
                return;

            blackjackPlayer.SetBet();
            (blackjackPlayer.CardGame as BlackJackGame).PlaceBets(blackjackPlayer);
        }
        #endregion

        #region Config        
        private static ConfigData Configuration;
        private class ConfigData
        {
            [JsonProperty("Card Color (blue, gray, green, purple, red, yellow)")]
            public string CardColor { get; set; }

            [JsonProperty("Show Winnings")]
            public bool ShowWinnings { get; set; }

            [JsonProperty("Show Balance")]
            public bool ShowBalance { get; set; }

            [JsonProperty("Table Skin ID")]
            public ulong SkinID { get; set; }

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
                CardColor = "blue",
                ShowBalance = true,
                ShowWinnings = true,
                SkinID = 2808399350,
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(Configuration, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            if (Configuration.Version < new Core.VersionNumber(0, 1, 9))
            {
                Configuration.ShowBalance = true;
                Configuration.ShowWinnings = true;
                Configuration.SkinID = 2808399350;
            }

            Configuration.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        #region Localization
        private static string msg(string key, string playerId = null) => Instance.lang.GetMessage(key, Instance, playerId);

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["UI.Notification.StartsIn"] = "Next game starts in <color=#00E500>{0}</color> seconds!",
            ["UI.Notification.IsStarting"] = "Next game is starting!",
            ["UI.Notification.BeginsIn"] = "Bets are locked! Game starts in <color=#00E500>5</color> seconds...",
            ["UI.Notification.PlaceBets"] = "Place Your Bets!",
            ["UI.Notification.Waiting"] = "Waiting for players to place their bets!",
            ["UI.Notification.CurrentlyPlaying"] = "Currently Playing : <color=#00E500>{0}</color>",
            ["UI.Notification.HasBlackJack"] = "{0} has <color=#00E500>Black Jack!</color>",
            ["UI.Notification.HasBust"] = "{0} has <color=#ce422b>Bust!</color>",
            ["UI.Notification.StayingOn"] = "{0} is staying on <color=#00E500>{1}</color>",
            ["UI.Notification.DblDown.HasBust"] = "{0} has doubled down and has <color=#ce422b>Bust!</color>",
            ["UI.Notification.DblDown"] = "{0} has doubled down with <color=#00E500>{1}</color>",
            ["UI.Notification.Insurance"] = "{0} has claimed <color=#00E500>insurance</color>",
            ["UI.Notification.UsersPlacingBets"] = "Players are placing bets. You will enter the next round!",
            ["UI.Notification.GameInProgress"] = "This round has started. You will enter the next round!",
            ["UI.Notification.LeftGame"] = "<color=#ce422b>{0}</color> has left the game!",

            ["UI.Player.Bet"] = "Bet : {0}",
            ["UI.Player.PlayingNext"] = "Playing Next Round",
            ["UI.Player.BalanceAmount"] = "Balance : {0} {1}",
            ["UI.Player.BetAmount"] = "Bet : {0}",
            ["UI.Player.ResetBet"] = "Set Minimum",
            ["UI.Player.LockBet"] = "Lock Bet",
            ["UI.Player.Hit"] = "Hit",
            ["UI.Player.Stay"] = "Stay",
            ["UI.Player.DblDown"] = "Dbl Down",
            ["UI.Player.Insurance"] = "Insurance",

            ["UI.Dealer"] = "Dealer",
            ["UI.LeaveTable"] = "Leave Table",

            ["UI.Status.Bust"] = "<color=#ce422b>Bust!</color>",
            ["UI.Status.BlackJack"] = "<color=#00E500>Black Jack!</color>",
            ["UI.Status.Tie"] = "Tie!",
            ["UI.Status.Win"] = "<color=#00E500>Win!</color>",
            ["UI.Status.Lost"] = "<color=#ce422b>Lost!</color>",
            ["UI.Status.Lost.Insurance"] = "<color=#ce422b>Lost!</color> (Insurance Win)",
            ["UI.Status.BlackJack.Amount"] = "<color=#00E500>Black Jack!</color> ({0})",
            ["UI.Status.Tie.Amount"] = "Tie! ({0})",
            ["UI.Status.Win.Amount"] = "<color=#00E500>Win!</color> ({0})",
            ["UI.Status.Lost.Amount"] = "<color=#ce422b>Lost!</color> ({0})",

            ["Chat.Kicked.NotEnough"] = "<color=#D3D3D3>You have run out of {0} to bet with!</color>",
        };
        #endregion
    }
}
