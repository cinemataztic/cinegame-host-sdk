using System;
using System.Collections.Generic;
using System.Collections;

using UnityEngine;

using Random = UnityEngine.Random;

namespace CineGame.SDK {

    public class CineGameBots : MonoBehaviour {
        private static CineGameBots instance;

        [Tooltip ("Press SHIFT+P at any time to spawn another bot")]
        public int MinStartingBots = 3;
        public int MaxStartingBots = 10;
        public float MinTimeBeforeJoin = 1;
        public float MaxTimeBeforeJoin = 300;
        [Range (0, 100)]
        public float ProbLeaveAndRejoin = 10;
        [Range (0, 100)]
        public float ProbChat = 20;
        [Space]
        [Header ("Advanced Settings")]
        public bool Verbose = false;

        public string [] StandardAvatars = {
            "ct_pumpkin",
            "ct_ghost",
            "ct_frankenstein",
            "ct_girlpigtails",
            "ct_skull",
            "ct_boycap",
            "ct_boyblack",
            "ct_mummy",
            "ct_boyglasses",
            "ct_girlblond",
            "ct_cat",
            "ct_devil",
        };

        public string [] Names = {
            "Adam",
            "Chris",
            "David",
            "John",
            "Kim",
            "Michael",
            "Nick",
            "Noah",
            "Oscar",
            "Peter",
            "Robert",
            "Thomas",
        };

        public string [] ChatMessages = {
            /*"/m I'm SO excited about this game!",
            "/m My quads are sore",
            "/m LET'S GO !!!",
            "/m I will win, I always do",
            "/m Ready to be beat?",
            "/m It's a fine day for a game",*/
            "/m 🤖❤️",
            "/m ❤️",
            "/m 🕹🥳❤️",
            "/m I❤️U🕹🥳",
            /*"/giphy R6gvnAxj2ISzJdbA63",
            "/giphy 2dQ3FMaMFccpi",
            "/giphy cdNSp4L5vCU7aQrYnV",
            "/tenor 17583147",
            "/tenor 22624142",
            "/tenor 24411575",*/
        };

        private readonly List<Coroutine> BotCoroutines = new List<Coroutine> ();
        private readonly List<int> BotIds = new List<int> ();
        private readonly List<IBot> BotScripts = new List<IBot> ();

        private List<string> NamesShuffled;
        private List<string> AvatarsShuffled;

        private int BotIndex = 1;

        internal static bool VerboseLogging;

        private void Awake () {
            if (instance != null) {
                Debug.LogError ("CineGameBots: More than one instance loaded!");
            } else {
                instance = this;
                VerboseLogging = Verbose;
                NamesShuffled = new List<string> (Names);
                NamesShuffled.Shuffle ();
                AvatarsShuffled = new List<string> (StandardAvatars);
                AvatarsShuffled.Shuffle ();
            }
        }

        private void Start () {
            if (instance == this) {
                CineGameSDK.OnGameReady += OnGameReady;
            }
        }

        private void OnGameReady (Dictionary<string, object> gameConfig) {
            var numStartingBots = Random.Range (MinStartingBots, MaxStartingBots);
            Debug.Log ($"CineGameBots: {numStartingBots} will be joining the party within {MinTimeBeforeJoin:0.##} and {MaxTimeBeforeJoin:0.##} seconds");
            for (int i = 0; i < numStartingBots; i++) {
                SpawnBot (spawnImmediately: false);
            }
        }

        private void Update () {
            if (Input.GetKeyDown (KeyCode.P) && (Input.GetKey (KeyCode.LeftShift) || Input.GetKey (KeyCode.RightShift))) {
                SpawnBot (spawnImmediately: true);
            }
        }

        private void SpawnBot (bool spawnImmediately) {
            //Bots are identified by a negative (virtual) BackendID
            var id = -BotIndex++;
            BotIds.Add (id);
            var botScript = new LobbyBot (
                id,
                Names [BotIndex % NamesShuffled.Count], //"bot" + id,
                AvatarsShuffled [BotIndex % AvatarsShuffled.Count],
                spawnImmediately ? 0f : Random.Range (MinTimeBeforeJoin, MaxTimeBeforeJoin),
                Random.value < ProbLeaveAndRejoin / 100f,
                ProbChat,
                ChatMessages
                );
            BotScripts.Add (botScript);
            BotCoroutines.Add (StartCoroutine (botScript.Start ()));
        }

        /// <summary>
		/// Host broadcasting object message to all bots
		/// </summary>
        internal static void BroadcastObjectMessage (CineGameSDK.PlayerObjectMessage obj, bool toPlayers, bool toSpectators) {
            if (instance != null) {
                foreach (var script in instance.BotScripts) {
                    script.SendObjectMessage (obj);
                }
            }
        }

        /// <summary>
		/// Host sending object message to a bot
		/// </summary>
        internal static void SendObjectMessage (CineGameSDK.PlayerObjectMessage obj, int id) {
            if (instance != null) {
                var idx = instance.BotIds.IndexOf (id);
                instance.BotScripts [idx].SendObjectMessage (obj);
            }
        }

        /// <summary>
		/// Host sending string message to a bot
		/// </summary>
        internal static void SendStringMessage (string message, int id) {
            if (instance != null) {
                var idx = instance.BotIds.IndexOf (id);
                instance.BotScripts [idx].SendStringMessage (message);
            }
        }

        internal interface IBot {
            /// <summary>
            /// Host sending object message to a bot
            /// </summary>
            internal void SendObjectMessage (CineGameSDK.PlayerObjectMessage obj);

            /// <summary>
		    /// Host sending string message to a bot
		    /// </summary>
            internal void SendStringMessage (string message);
        }

        internal class LobbyBot : IBot {
            readonly int BackendID;
            readonly string Name;
            readonly string AvatarID;
            readonly float TimeBeforeJoin;
            readonly float ProbChat;
            readonly string [] ChatMessages;

            internal LobbyBot (int id, string name, string avatarId, float timeBeforeJoin, bool leaveAndRejoin, float probChat, string [] chatMessages) {
                BackendID = id;
                Name = name;
                AvatarID = avatarId;
                TimeBeforeJoin = timeBeforeJoin;
                ChatMessages = chatMessages;
                ProbChat = probChat;
            }

            void IBot.SendObjectMessage (CineGameSDK.PlayerObjectMessage obj) {
                if (obj.ContainsKey ("Ping")) {
                    if (Random.value > .3f) {
                        CineGameSDK.OnPlayerObjectMessage?.Invoke (BackendID, obj);
                    }
                }
            }

            void IBot.SendStringMessage (string message) {
				throw new NotImplementedException ();
			}

            void Log (string msg) {
                if (VerboseLogging) {
                    Debug.Log (msg);
                }
            }

            void LogError (string msg) {
                if (VerboseLogging) {
                    Debug.LogError (msg);
                }
            }

            internal IEnumerator Start () {
                var pl = new CineGameSDK.User
                {
                    BackendID = BackendID,
                    Name = Name,
                };

                if (TimeBeforeJoin > float.Epsilon) {
                    Log ($"CineGameBots: {pl.Name} will attempt to join the game in {TimeBeforeJoin:#.00} seconds");
                    yield return new WaitForSecondsRealtime (TimeBeforeJoin);
                }

                Log ($"CineGameBots: {pl.Name} attempting to join game");
                CineGameSDK.OnPlayerJoined?.Invoke (pl);

                var timeToSetAvatar = Random.Range (.01f, 1f);
                yield return new WaitForSecondsRealtime (timeToSetAvatar);
                CineGameSDK.SetPlayerAvatar (BackendID, AvatarID);

                for (; ; ) {
                    if (Random.value < .01f) {
                        var timeToRejoin = Random.Range (.3f, 30f);
                        Log ($"CineGameBots: {Name} leaving the game, will rejoin in {timeToRejoin:#.00} seconds");
                        CineGameSDK.OnPlayerLeft?.Invoke (BackendID);
                        yield return new WaitForSecondsRealtime (timeToRejoin);

                        Log ($"CineGameBots: {Name} rejoining the game");
                        CineGameSDK.OnPlayerLeft?.Invoke (BackendID);
                    }

                    yield return new WaitForSecondsRealtime (Random.Range (.3f, 3f));

                    var obj = new CineGameSDK.PlayerObjectMessage ();
                    var shouldMove = Random.value < .5f;
                    obj.PutFloat ("x", shouldMove ? Random.Range (0f, 1f) : .5f);
                    obj.PutFloat ("y", shouldMove ? Random.Range (0f, 1f) : .5f);
                    CineGameSDK.OnPlayerObjectMessage?.Invoke (BackendID, obj);

                    if (!shouldMove && Random.value < ProbChat/100f) {
                        var chatMessage = ChatMessages [Random.Range (0, ChatMessages.Length)];
                        Log ($"CineGameBots: {Name} says '{chatMessage}'");
                        yield return new WaitForSecondsRealtime (Random.Range (3f, 10f));
                        CineGameSDK.OnPlayerChatMessage?.Invoke (BackendID, chatMessage);
                    }
                }
            }
		}
    }

    internal static class Extensions {
        private static System.Random rng = new System.Random ();

        public static void Shuffle<T> (this IList<T> list) {
            int n = list.Count;
            while (n > 1) {
                n--;
                int k = rng.Next (n + 1);
                T value = list [k];
                list [k] = list [n];
                list [n] = value;
            }
        }
    }
}