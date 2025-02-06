using SFDGameScriptInterface;

namespace SFDScript
{
    public partial class Program : GameScriptInterface
    {
        readonly String version = "0.0.1";

        public void PrintGreetings()
        {
            Game.ShowChatMessage(string.Format("Hello from Slipping Script(Hryapusek, HHsss) v{0}", version));
        }

        public void OnStartup()
        {
            Game.SetMapType(MapType.Custom);
            Game.ShowPopupMessage("OnStartup is run when the map or script is started.");
            m_playerKeyInputCallback = Events.PlayerKeyInputCallback.Start(OnPlayerKeyInput);
            m_playerDeathCallback = Events.PlayerDeathCallback.Start(OnPlayerDeath);

            var users = Game.GetActiveUsers();
            var fighterCount = users.Length;
            Game.WriteToConsole(string.Format("There are currently {0} users in the game", fighterCount));
            var fighter0 = users[0];
            var fighter1 = users[1];

            Game.WriteToConsole("Creating switcher");
            teamSwitcher = new TeamSwitcher(fighter0.GetPlayer(), fighter1.GetPlayer());
            Game.WriteToConsole("Players placed");
        }

        internal class ProxyPlayer
        {
            public bool isSlipping = false;
            public IPlayer player;

            public ProxyPlayer(IPlayer player)
            {
                this.player = player;
            }
        }

        internal class TeamSwitcher
        {
            private ProxyPlayer firstPlayer;
            private ProxyPlayer secondPlayer;

            public TeamSwitcher(IPlayer firstPlayer, IPlayer secondPlayer)
            {
                this.firstPlayer = new ProxyPlayer(firstPlayer);
                this.secondPlayer = new ProxyPlayer(secondPlayer);
                this.firstPlayer.player.SetTeam(PlayerTeam.Team1);
                this.secondPlayer.player.SetTeam(PlayerTeam.Team2);
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
                return player == firstPlayer.player ? firstPlayer.isSlipping : secondPlayer.isSlipping;
            }

            private Tuple<ProxyPlayer, ProxyPlayer> DispathPlayers(IPlayer player)
            {
                return player == firstPlayer.player ? new Tuple<ProxyPlayer, ProxyPlayer>(firstPlayer, secondPlayer) : new Tuple<ProxyPlayer, ProxyPlayer>(secondPlayer, firstPlayer);
            }

            public void SetDefaultMode(IPlayer player)
            {
                var tuple = DispathPlayers(player);
                var triggerPlayer = tuple.Item1;
                var otherPlayer = tuple.Item2;

                if (otherPlayer.isSlipping)
                {
                    triggerPlayer.isSlipping = false;
                    triggerPlayer.player.SetTeam(otherPlayer.player.GetTeam());
                } else {
                    triggerPlayer.isSlipping = false;
                    triggerPlayer.player.SetTeam(GetOppositeTeam(otherPlayer.player.GetTeam()));
                }
            }

            public void SetSlippingMode(IPlayer player)
            {
                var tuple = DispathPlayers(player);
                var triggerPlayer = tuple.Item1;
                var otherPlayer = tuple.Item2;

                if (otherPlayer.isSlipping)
                {
                    triggerPlayer.isSlipping = true;
                    triggerPlayer.player.SetTeam(GetOppositeTeam(otherPlayer.player.GetTeam()));
                }
                else
                {
                    triggerPlayer.isSlipping = true;
                    triggerPlayer.player.SetTeam(otherPlayer.player.GetTeam());
                }

            }

            public bool Contains(IPlayer player) {
                return player == firstPlayer.player || player == secondPlayer.player;
            }
        }

        TeamSwitcher teamSwitcher = null;

        public Program() : base(null) { }
        Events.PlayerKeyInputCallback m_playerKeyInputCallback = null;
        Events.PlayerDeathCallback m_playerDeathCallback = null;

        public void OnPlayerDeath(IPlayer player, PlayerDeathArgs args) {
            Game.SetGameOver(string.Format("{0} sosal", player.GetUser().AccountName));
        }


        public void OnPlayerKeyInput(IPlayer player, VirtualKeyInfo[] keyEvents)
        {
            if (teamSwitcher.Contains(player))
            {
                var isAbleToSlip = !player.IsRolling && player.IsOnGround && (player.IsIdle || player.IsMeleeAttacking || player.IsCrouching);
                if (!isAbleToSlip)
                {
                    teamSwitcher.SetDefaultMode(player);
                    return;
                }
            }
            for (int i = 0; i < keyEvents.Length; i++)
            {

                if (keyEvents[i].Key == VirtualKey.CROUCH_ROLL_DIVE)
                {
                    if (keyEvents[i].Event == VirtualKeyEvent.Pressed)
                    {
                        teamSwitcher.SetSlippingMode(player);
                        Game.WriteToConsole(string.Format("Player {0} crouched - switching teams...", player.Name));
                    }
                    else
                    {
                        teamSwitcher.SetDefaultMode(player);
                    }
                }

            }
        }
    }
}