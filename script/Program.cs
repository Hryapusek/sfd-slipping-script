using System.Runtime.CompilerServices;
using SFDGameScriptInterface;

namespace SFDScript
{
    public partial class GameScript : GameScriptInterface
    {
        readonly static string TIMER_TRIGGER_NAME = "TimerTrigger";
        readonly static string TIMER_COOLDOWN_MS_KEY = "TimerSlippingCooldownMs";
        readonly static int DEFAULT_SLIPPING_COOLDOWN_MS = 700;
        readonly static string SET_COOLDOWN_COMMAND = "setslipcd";
        readonly static String version = "0.0.2";
        readonly static int BULLET_TRACE_EFFECT_X_OFFSET = -1;
        readonly static int BULLET_TRACE_EFFECT_Y_OFFSET = 13;

        TeamSwitcher teamSwitcher = null;
        public GameScript() : base(null) { }
        Events.PlayerKeyInputCallback m_playerKeyInputCallback = null;
        Events.PlayerDeathCallback m_playerDeathCallback = null;
        Events.UserMessageCallback m_userMessageCallback = null;

        static List<Action> timerCooldownCallbacks = new List<Action>();

        public void OnStartup()
        {
            PrintGreetings();
            Game.SetMapType(MapType.Custom);
            m_playerKeyInputCallback = Events.PlayerKeyInputCallback.Start(OnPlayerKeyInput);
            m_playerDeathCallback = Events.PlayerDeathCallback.Start(OnPlayerDeath);
            m_userMessageCallback = Events.UserMessageCallback.Start(MessageCallback);

            var users = Game.GetActiveUsers();
            teamSwitcher = new TeamSwitcher(users[0], users[1]);
            timerCooldownCallbacks.Add(() => teamSwitcher.CooldownChangedCallback());
        }

        // Static is needed because otherwise there is no access to this method from inner classes
        private static int ReadTimerCooldown()
        {
            int result;
            if (!Game.LocalStorage.TryGetItemInt(GameScript.TIMER_COOLDOWN_MS_KEY, out result))
            {
                Game.LocalStorage.RemoveItem(GameScript.TIMER_COOLDOWN_MS_KEY);
                Game.LocalStorage.SetItem(GameScript.TIMER_COOLDOWN_MS_KEY, GameScript.DEFAULT_SLIPPING_COOLDOWN_MS);
                Game.ShowChatMessage(string.Format("Initializing timer cooldown"));
                return GameScript.DEFAULT_SLIPPING_COOLDOWN_MS;
            }
            Game.ShowChatMessage(string.Format("Timer cooldown: {0}", result));
            return result;
        }

        // Static is needed because otherwise there is no access to this method from inner classes
        private static void SetTimerCooldown(int newCooldown)
        {
            Game.LocalStorage.RemoveItem(GameScript.TIMER_COOLDOWN_MS_KEY);
            Game.LocalStorage.SetItem(GameScript.TIMER_COOLDOWN_MS_KEY, newCooldown);
            foreach (var callback in timerCooldownCallbacks)
            {
                callback();
            }
        }

        public void PrintGreetings()
        {
            Game.ShowChatMessage(string.Format("Hello from Slipping Script(Hryapusek, HHsss) v{0}", version));
        }

        internal class ProxyUser
        {
            public bool isCooldown = false;
            public bool isSlipping = false;
            public IUser user;
            public IObjectTimerTrigger timerTrigger;

            public ProxyUser(IUser user, IObjectTimerTrigger timerTrigger)
            {
                this.user = user;
                this.timerTrigger = timerTrigger;
            }
        }

        internal class TeamSwitcher
        {
            private ProxyUser firstUser;
            private ProxyUser secondUser;

            public TeamSwitcher(IUser firstUser, IUser secondUser)
            {
                {
                    IObjectTimerTrigger timer = (IObjectTimerTrigger)Game.CreateObject(GameScript.TIMER_TRIGGER_NAME);

                    timer.SetIntervalTime(GameScript.ReadTimerCooldown());
                    timer.SetRepeatCount(1);
                    timer.SetScriptMethod("RestoreCooldown");
                    timer.CustomID = firstUser.UserIdentifier.ToString();
                    this.firstUser = new ProxyUser(firstUser, timer);
                }

                {
                    IObjectTimerTrigger timer = (IObjectTimerTrigger)Game.CreateObject(GameScript.TIMER_TRIGGER_NAME);

                    timer.SetIntervalTime(GameScript.ReadTimerCooldown());
                    timer.SetRepeatCount(1);
                    timer.SetScriptMethod("RestoreCooldown");
                    timer.CustomID = secondUser.UserIdentifier.ToString();
                    this.secondUser = new ProxyUser(secondUser, timer);
                }

                this.firstUser.user.GetPlayer().SetTeam(PlayerTeam.Team1);
                this.secondUser.user.GetPlayer().SetTeam(PlayerTeam.Team2);
            }

            public void CooldownChangedCallback()
            {
                firstUser.timerTrigger.SetIntervalTime(GameScript.ReadTimerCooldown());
                secondUser.timerTrigger.SetIntervalTime(GameScript.ReadTimerCooldown());
            }

            static PlayerTeam GetOppositeTeam(PlayerTeam team)
            {
                if (team == PlayerTeam.Team1)
                {
                    return PlayerTeam.Team2;
                }
                else
                {
                    return PlayerTeam.Team1;
                }
            }

            public bool IsSlipping(IPlayer player)
            {
                return player == firstUser.user.GetPlayer() ? firstUser.isSlipping : secondUser.isSlipping;
            }

            private Tuple<ProxyUser, ProxyUser> DispathPlayers(IUser user)
            {
                return user == firstUser.user ? new Tuple<ProxyUser, ProxyUser>(firstUser, secondUser) : new Tuple<ProxyUser, ProxyUser>(secondUser, firstUser);
            }

            public void SetDefaultMode(IUser player)
            {
                var tuple = DispathPlayers(player);
                var triggerPlayer = tuple.Item1;
                var otherPlayer = tuple.Item2;

                if (otherPlayer.isSlipping)
                {
                    triggerPlayer.isSlipping = false;
                    triggerPlayer.user.GetPlayer().SetTeam(otherPlayer.user.GetPlayer().GetTeam());
                }
                else
                {
                    triggerPlayer.isSlipping = false;
                    triggerPlayer.user.GetPlayer().SetTeam(GetOppositeTeam(otherPlayer.user.GetPlayer().GetTeam()));
                }
            }

            public void SetSlippingMode(IUser player)
            {
                var tuple = DispathPlayers(player);
                var triggerPlayer = tuple.Item1;
                var otherPlayer = tuple.Item2;

                if (triggerPlayer.isCooldown)
                {
                    Game.PlayEffect(EffectName.CustomFloatText, triggerPlayer.user.GetPlayer().GetWorldPosition() + new Vector2(0, 10), "Cooldown!");
                    Game.ShowChatMessage(string.Format("You are on cooldown! {0}", triggerPlayer.user.GetPlayer().Name));
                    return;
                }

                if (otherPlayer.isSlipping)
                {
                    triggerPlayer.user.GetPlayer().SetTeam(GetOppositeTeam(otherPlayer.user.GetPlayer().GetTeam()));
                }
                else
                {
                    triggerPlayer.user.GetPlayer().SetTeam(otherPlayer.user.GetPlayer().GetTeam());
                }
                triggerPlayer.isSlipping = true;
                triggerPlayer.isCooldown = true;
                Game.PlayEffect(EffectName.BulletSlowmoTrace,
                                triggerPlayer.user.GetPlayer().GetWorldPosition()
                                + new Vector2(BULLET_TRACE_EFFECT_X_OFFSET * triggerPlayer.user.GetPlayer().FacingDirection,
                                              BULLET_TRACE_EFFECT_Y_OFFSET));
                triggerPlayer.timerTrigger.StartTimer();
            }

            public bool Contains(IUser user)
            {
                return user == firstUser.user || user == secondUser.user;
            }

            public void RestoreCooldown(TriggerArgs args)
            {
                var timer = (IObjectTimerTrigger)args.Caller;
                int userIdentifier;
                if (!int.TryParse(timer.CustomID, out userIdentifier))
                {
                    Game.ShowChatMessage(string.Format("Failed to restore cooldown for user {0}. Invalid user identifier", timer.CustomID));
                    return;
                }
                if (userIdentifier != this.firstUser.user.UserIdentifier && userIdentifier != this.secondUser.user.UserIdentifier)
                {
                    Game.ShowChatMessage(string.Format("Failed to restore cooldown for user {0}. Unexpected user identifier", timer.CustomID));
                    return;
                }

                var proxyUser = userIdentifier == this.firstUser.user.UserIdentifier ? this.firstUser : this.secondUser;
                proxyUser.isCooldown = false;
            }
        }

        public void OnPlayerDeath(IPlayer player, PlayerDeathArgs args)
        {
            var countNotDead = Game.GetActiveUsers().Where((user) => user.GetPlayer() != null).Count((user) => !user.GetPlayer().IsDead);
            if (countNotDead < 2)
            {
                Game.SetGameOver(string.Format("{0} sosal", player.GetUser().AccountName));
            }
        }

        public void OnPlayerKeyInput(IPlayer player, VirtualKeyInfo[] keyEvents)
        {
            if (!player.IsUser)
            {
                return;
            }
            if (teamSwitcher.Contains(player.GetUser()))
            {
                var isAbleToSlip = !player.IsRolling && player.IsOnGround && (player.IsIdle || player.IsMeleeAttacking || player.IsCrouching);
                if (!isAbleToSlip)
                {
                    teamSwitcher.SetDefaultMode(player.GetUser());
                    return;
                }
            }
            for (int i = 0; i < keyEvents.Length; i++)
            {
                if (keyEvents[i].Key == VirtualKey.CROUCH_ROLL_DIVE)
                {
                    if (keyEvents[i].Event == VirtualKeyEvent.Pressed && player.IsWalking)
                    {
                        teamSwitcher.SetSlippingMode(player.GetUser());
                        Game.WriteToConsole(string.Format("Player {0} crouched - switching teams...", player.Name));
                    }
                    else
                    {
                        teamSwitcher.SetDefaultMode(player.GetUser());
                    }
                }

            }
        }

        public void RestoreCooldown(TriggerArgs args)
        {
            this.teamSwitcher.RestoreCooldown(args);
        }

        public void MessageCallback(UserMessageCallbackArgs args)
        {
            if (!args.IsCommand)
            {
                Game.ShowChatMessage("Not a command");
                return;
            }
            Game.ShowChatMessage("Got new command");
            if (args.Command.ToLower() == GameScript.SET_COOLDOWN_COMMAND)
            {
                var commandArgs = args.CommandArguments.Split(' ');
                if (commandArgs.Length != 1)
                {
                    Game.ShowChatMessage("Invalid arguments");
                    return;
                }
                var millisecondsStr = commandArgs[0];
                int milliseconds;
                if (!int.TryParse(millisecondsStr, out milliseconds))
                {
                    // debug
                    Game.ShowChatMessage("Failed to parse int. Input: " + millisecondsStr);
                    Game.ShowChatMessage("Invalid arguments");
                    return;
                }
                GameScript.SetTimerCooldown(milliseconds);
                // debug
                Game.ShowChatMessage("Cooldown set successfully! New cooldown: " + milliseconds + " milliseconds", Color.Green);
            }
            else
            {
                Game.ShowChatMessage(String.Format("Unknown command. Command: {0}", args.Command));
            }
        }
    }
}