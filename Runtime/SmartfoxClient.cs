using UnityEngine;
using System;
using System.Collections.Generic;
using Sfs2X;
using Sfs2X.Util;
using Sfs2X.Requests;
using Sfs2X.Core;
using Sfs2X.Entities;
using Sfs2X.Entities.Data;
using Sfs2X.Entities.Variables;

namespace CineGame.Host {

    internal class SmartfoxClient {

        static SmartFox sfs;

        /// <summary>
		/// The room which hosts the game
		/// </summary>
        internal static Room GameRoom;

        internal static int MaxPlayers = 75;
        internal static int MaxSpectators = 75*5;

        /// <summary>
		/// We send a Keep-Alive dummy message to the server every minute to avoid being kicked
		/// </summary>
        static float LastKeepAliveTime = 0f;

        static string GameServer, GameZone, GameType, GameCode;
        static bool WebglSecure = false;

        /// <summary>
        /// Max lag threshold in ms above which we log an error at the end of the game
        /// </summary>
        public static int LagWarningThreshold = 250;

        /// <summary>
        /// How often lag is measured by Smartfox Server in seconds.
        /// </summary>
        public static int LagMonitorInterval = 30;

        /// <summary>
        /// How many samples to base each lag measurement on.
        /// </summary>
        public static int LagSamplesPerInterval = 3;

        /// <summary>
        /// The current server lag
        /// </summary>
        public static int CurrentLagValue;

        /// <summary>
        /// THe max lag value experienced while connected
        /// </summary>
        public static int MaxLagValue = 0;

        static string LastErrorMsg = null;

        /// <summary>
        /// We will only send a few warnings to admin, no spam! :)
        /// </summary>
        static int numBkIdWarnings = 2;

        static int ConnectionRetryCount = 0;

        /// <summary>
		/// We maintain this dictionary of {BackendID,Sfs2X.Entities.User} so we can map from the persistent BackendID to a temp smartfox User when sending messages
		/// </summary>
        static Dictionary<long, User> userDict = new Dictionary<long, User> ();

        // Update is called once per frame
        internal static void Update () {
            if (sfs != null) {
                try {
                    //Keep connection alive by sending a dummy extension request every 60 seconds
                    if (sfs.IsConnected && sfs.MySelf != null && (LastKeepAliveTime + 60f) < Time.realtimeSinceStartup) {
                        LastKeepAliveTime = Time.realtimeSinceStartup;
                        Debug.Log ("SFS Sending keep-alive dummy request");
                        sfs.Send (new PrivateMessageRequest ("keepalive", sfs.MySelf.Id));
                    }
                    sfs.ProcessEvents ();
                } catch (Exception e) {
                    Debug.LogError (e.ToString ());
                }
            }
        }


        internal static void ConnectAndCreateGame (string gameServer, string gameCode, string gameZone, string gameType, bool webGlSecure = false) {
            GameServer = gameServer;
            GameCode = gameCode;
            GameZone = gameZone;
            GameType = gameType;
            WebglSecure = webGlSecure;
            if (sfs != null && sfs.IsConnected && sfs.MySelf != null) {
                CreateAndJoinRoom ();
            } else {
                Connect ();
            }
        }


        /// <summary>
        /// Game has ended so we expect not much more network activity.
        /// Here is a good place to log network issues, performance etc
        /// </summary>
        internal static void GameOver () {
            if (MaxLagValue > LagWarningThreshold) {
                Debug.LogErrorFormat ("SFS Max lag of {0} ms exceeded threshold of {1} ms", MaxLagValue, LagWarningThreshold);
            }
        }


        static void Connect () {

            //If we're reconnecting and already have a smartfox object, reset it
            reset ();

			// Set connection parameters
			ConfigData cfg = new ConfigData {
				Host = GameServer,
				Zone = GameZone
			};
			bool smartfoxDebug = true;
#if WEBGL
            cfg.Port = WebglSecure ? 8843 : 8080;
            sfs = new SmartFox (WebglSecure ? UseWebSocket.WSS : UseWebSocket.WS, smartfoxDebug);
#else
            cfg.Port = 9933;
            sfs = new SmartFox (smartfoxDebug);
#endif
            // Set ThreadSafeMode explicitly, or Windows Store builds will get a wrong default value (false)
            sfs.ThreadSafeMode = true;

            sfs.AddEventListener (SFSEvent.CONNECTION, OnConnection);
            sfs.AddEventListener (SFSEvent.CONNECTION_LOST, OnConnectionLost);
            sfs.AddEventListener (SFSEvent.CONNECTION_RETRY, OnConnectionRetry);
            sfs.AddEventListener (SFSEvent.CONNECTION_RESUME, OnConnectionResume);
            sfs.AddEventListener (SFSEvent.LOGIN, OnLogin);
            sfs.AddEventListener (SFSEvent.LOGIN_ERROR, OnLoginError);
            sfs.AddEventListener (SFSEvent.ROOM_JOIN, OnRoomJoin);
            //sfs.AddEventListener(SFSEvent.ROOM_JOIN_ERROR, OnRoomJoinError);
            sfs.AddEventListener (SFSEvent.ROOM_CREATION_ERROR, OnRoomCreationError);
            sfs.AddEventListener (SFSEvent.PRIVATE_MESSAGE, OnPrivateMessage);
            sfs.AddEventListener (SFSEvent.OBJECT_MESSAGE, OnObjectMessage);
            //sfs.AddEventListener(SFSEvent.USER_VARIABLES_UPDATE, OnUserVariableUpdate);
            sfs.AddEventListener (SFSEvent.USER_ENTER_ROOM, OnUserEnterRoom);
            sfs.AddEventListener (SFSEvent.USER_EXIT_ROOM, OnUserExitRoom);
            sfs.AddEventListener (SFSEvent.PING_PONG, OnPingPong);

            Debug.LogFormat ("SFS Connecting to {0}:{1} Zone={2} API {3}", cfg.Host, cfg.Port, cfg.Zone, sfs.Version);

            // Connect to SFS2X
            LastErrorMsg = null;
            sfs.Connect (cfg);
        }


        static void reset () {
            if (sfs != null) {
                // Remove SFS2X listeners
                sfs.RemoveEventListener (SFSEvent.CONNECTION, OnConnection);
                sfs.RemoveEventListener (SFSEvent.CONNECTION_LOST, OnConnectionLost);
                sfs.RemoveEventListener (SFSEvent.CONNECTION_RETRY, OnConnectionRetry);
                sfs.RemoveEventListener (SFSEvent.CONNECTION_RESUME, OnConnectionResume);
                sfs.RemoveEventListener (SFSEvent.LOGIN, OnLogin);
                sfs.RemoveEventListener (SFSEvent.LOGIN_ERROR, OnLoginError);
                sfs.RemoveEventListener (SFSEvent.ROOM_JOIN, OnRoomJoin);
                //sfs.RemoveEventListener(SFSEvent.ROOM_JOIN_ERROR, OnRoomJoinError);
                sfs.RemoveEventListener (SFSEvent.ROOM_CREATION_ERROR, OnRoomCreationError);
                sfs.RemoveEventListener (SFSEvent.PRIVATE_MESSAGE, OnPrivateMessage);
                sfs.RemoveEventListener (SFSEvent.OBJECT_MESSAGE, OnObjectMessage);
                //sfs.RemoveEventListener(SFSEvent.USER_VARIABLES_UPDATE, OnUserVariableUpdate);
                sfs.RemoveEventListener (SFSEvent.USER_ENTER_ROOM, OnUserEnterRoom);
                sfs.RemoveEventListener (SFSEvent.USER_EXIT_ROOM, OnUserExitRoom);
                sfs.RemoveEventListener (SFSEvent.PING_PONG, OnPingPong);
                if (sfs.IsConnected || sfs.IsConnecting) {
                    sfs.Disconnect ();
                }
                sfs = null;
            }
            LastErrorMsg = null;
            MaxLagValue = 0;
        }


        static void OnConnectionRetry (BaseEvent evt) {
            Debug.LogWarning ("WARNING: SFS Connection timeout - retry ...");
        }

        static void OnConnectionResume (BaseEvent evt) {
            Debug.Log ("SFS Connection resumed!");
        }

        static void OnConnection (BaseEvent evt) {
            if ((bool)evt.Params ["success"]) {
                Debug.Log ("SFS Connected, logging in ...");
                // Login
                LastErrorMsg = null;
                sfs.Send (new LoginRequest ("Host" + GameCode));
            } else {
                // Remove SFS2X listeners
                reset ();

                if (ConnectionRetryCount++ < 3) {
                    // Log warning and retry
                    LastErrorMsg = "SFS Connection failed ... retrying";
                    Debug.LogWarning (LastErrorMsg);
                    Connect ();
                } else {
                    // Failed three times, show error message
                    LastErrorMsg = "SFS Connection failed three times, giving up";
                    Debug.LogError (LastErrorMsg);
                    CineGameSDK.OnError?.Invoke(9933);
                }
            }
        }


        static void OnPingPong (BaseEvent evt) {
            CurrentLagValue = (int)evt.Params ["lagValue"];
            if (CurrentLagValue > MaxLagValue) {
                MaxLagValue = CurrentLagValue;
                Debug.LogFormat ("SFS New MaxLagValue = {0} ms", MaxLagValue);
            }
        }


        static void OnConnectionLost (BaseEvent evt) {
            // Remove SFS2X listeners and re-enable interface
            reset ();

            string reason = (string)evt.Params ["reason"];

            if (reason != ClientDisconnectionReason.MANUAL) {
                LastErrorMsg = "SFS Connection was lost; reason is: " + reason;
                Debug.LogError (LastErrorMsg);
                if (reason != ClientDisconnectionReason.IDLE) {
                    CineGameSDK.OnError?.Invoke(9933);
                }
            } else {
                LastErrorMsg = null;
            }
        }


        static void OnLogin (BaseEvent evt) {
            User user = (User)evt.Params ["user"];
            Debug.LogFormat ("SFS logged in as {0}. Connection mode = {1}.", user.Name, sfs.ConnectionMode);

            if (LagMonitorInterval > 0 && LagSamplesPerInterval > 0) {
                sfs.EnableLagMonitor (true, LagMonitorInterval, LagSamplesPerInterval);
            }

            CreateAndJoinRoom ();
        }


        static void CreateAndJoinRoom () {
            //Create room and join it
            var roomSettings = new RoomSettings (GameCode);
            roomSettings.IsGame = true;
            roomSettings.MaxUsers = (short)MaxPlayers;
            roomSettings.MaxSpectators = (short)MaxSpectators;
            roomSettings.Variables.Add (new SFSRoomVariable ("GameType", GameType));
            roomSettings.Variables.Add (new SFSRoomVariable ("HostId", sfs.MySelf.Id));
            if (Debug.isDebugBuild) {
                roomSettings.Variables.Add (new SFSRoomVariable ("IsTest", true));
            }
            roomSettings.Permissions = new RoomPermissions {
                AllowResizing = true,
                AllowNameChange = false,
                AllowPasswordStateChange = true,
                AllowPublicMessages = true
            };
            sfs.Send (new CreateRoomRequest (roomSettings, true));
        }

        static void OnLoginError (BaseEvent evt) {
            var errorCode = (short)evt.Params ["errorCode"];

            // Disconnect
            sfs.Disconnect ();

            // Remove SFS2X listeners and re-enable interface
            reset ();

            LastErrorMsg = "SFS Login failed: " + (string)evt.Params ["errorMessage"];
            if (errorCode == 6) {
                //User already logged in - try again, get a new gameCode
                Debug.LogWarning (LastErrorMsg);
                CineGameSDK.RequestGameCodeStatic ();
            } else {
                Debug.LogError (LastErrorMsg);
            }
        }


        static void OnRoomJoin (BaseEvent evt) {
            GameRoom = (Room)evt.Params ["room"];
            Debug.LogFormat ("SFS Room Joined, admin={0}, moderator={1}", sfs.MySelf.IsAdmin (), sfs.MySelf.IsModerator ());
        }


        static void OnRoomCreationError (BaseEvent evt) {
            var errorCode = (short)evt.Params ["errorCode"];

            LastErrorMsg = "SFS Room creation failed: " + (string)evt.Params ["errorMessage"];
            if (errorCode == 12) {
                //Room already exists - try again, get a new gameCode
                Debug.LogWarning (LastErrorMsg);
                CineGameSDK.RequestGameCodeStatic ();
            } else {
                Debug.LogError (LastErrorMsg);
            }
        }


        static void OnPrivateMessage (BaseEvent evt) {
            string message = (string)evt.Params ["message"];
            SFSUser sender = (SFSUser)evt.Params ["sender"];
            if (sender != null && sender != sfs.MySelf) {
                object v;
                if (sender.Properties.TryGetValue ("bkid", out v)) {
                    var backendID = (int)v;

                    if (message.StartsWith ("/m ") || message.StartsWith ("/giphy ") || message.StartsWith ("/tenor ")) {
                        CineGameSDK.OnPlayerChatMessage?.Invoke(backendID, message);
                        return;
                    } else {
                        CineGameSDK.OnPlayerStringMessage?.Invoke(backendID, message);
                    }
                }
            }
        }


        static void OnObjectMessage (BaseEvent evt) {
            ISFSObject dataObj = (ISFSObject)evt.Params ["message"];
            var user = (SFSUser)evt.Params ["sender"];

            if (user != null && user != sfs.MySelf) {
                if (dataObj.ContainsKey ("bkid")) {
                    var backendID = dataObj.GetInt ("bkid");
                    var userName = dataObj.GetUtfString ("name");
                    var userAge = dataObj.GetInt ("age");
                    var userGender = dataObj.GetUtfString ("gender");

                    if (user.Properties == null) {
                        user.Properties = new Dictionary<string, object> ();
                    }
                    user.Properties ["bkid"] = backendID;
                    user.Properties ["name"] = userName;

                    userDict [backendID] = user;

                    if (user.IsPlayer) {
                        if (CineGameSDK.GameEnded) {
                            Debug.LogWarning ($"Received bkid object from {userName} ({backendID}) but game has already ended");
                            return;
                        }

                        string avatarID = null;

                        if (dataObj.ContainsKey ("avatar")) {
                            avatarID = dataObj.GetUtfString ("avatar");
                        }

                        Version appVer = null;
                        if (dataObj.ContainsKey ("appVer")) {
                            string appVerString = dataObj.GetUtfString ("appVer");
                            appVer = new Version (appVerString);
                        }

                        if (CineGameChatController.IsProfanityFilterLoaded) {
                            CineGameChatController.RunProfanityFilter (userName, (filteredUserName) => {
                                CineGameSDK.OnPlayerJoined?.Invoke (new CineGameSDK.Player {
                                    BackendID = backendID,
                                    AppVersion = appVer,
                                    Name = filteredUserName,
                                    Age = userAge,
                                    Gender = userGender,
                                    Score = 0
                                });

                                if (!string.IsNullOrWhiteSpace (avatarID)) {
                                    CineGameSDK.SetPlayerAvatar (backendID, avatarID);
                                }
                            });
                        } else {
                            Debug.LogWarning ("SFS Player name is unfiltered (CineGameChatController instance not found)");

                            CineGameSDK.OnPlayerJoined?.Invoke (new CineGameSDK.Player {
                                BackendID = backendID,
                                AppVersion = appVer,
                                Name = userName,
                                Age = userAge,
                                Gender = userGender,
                                Score = 0
                            });

                            if (!string.IsNullOrWhiteSpace (avatarID)) {
                                CineGameSDK.SetPlayerAvatar (backendID, avatarID);
                            }
                        }
                    } else if (user.IsSpectator) {
                        var supportingID = dataObj.GetInt ("supportingId");

                        if (CineGameChatController.IsProfanityFilterLoaded) {
                            CineGameChatController.RunProfanityFilter (userName, (filteredUserName) => {
                                CineGameSDK.OnSupporterJoined?.Invoke (backendID, supportingID, filteredUserName);
                            });
                        } else {
                            Debug.LogWarning ("SFS Supporter name is unfiltered (CineGameChatController instance not found)");
                            CineGameSDK.OnSupporterJoined?.Invoke (backendID, supportingID, userName);
                        }
                    }
                } else if (dataObj.ContainsKey ("avatar") && user.Properties != null) {
                    var avatarID = dataObj.GetUtfString ("avatar");
                    var backendID = (int)user.Properties ["bkid"];
                    CineGameSDK.SetPlayerAvatar (backendID, avatarID);
                }
                if (user.Properties != null && user.Properties.TryGetValue ("bkid", out object o)) {
                    int backendID = (int)o;
                    CineGameSDK.OnPlayerObjectMessage?.Invoke(backendID, CineGameSDK.PlayerObjectMessage.FromSmartFoxObject (dataObj));
                } else if (numBkIdWarnings-- > 0) {
                    Debug.LogWarning ($"SFS OnObjectMessage: {user.Name} has no bkid property! Happens when packages get lost or are received out of order");
                }
            }
        }

        static void OnUserEnterRoom (BaseEvent evt) {
            User user = (User)evt.Params ["user"];
            if (user != sfs.MySelf) {
                //Room room = (Room) evt.Params["room"];
                Debug.LogFormat ("SFS {0} enters room as {1}", user.Name, user.IsPlayer ? "player" : "spectator");
            }
        }

        static void OnUserExitRoom (BaseEvent evt) {
            User user = (User)evt.Params ["user"];
            if (user != null && user != sfs.MySelf) { 

                Room room = (Room)evt.Params ["room"];
                //GameController.PlayerDisconnected (user.Id);
                try {
                    object uname = string.Empty;
                    user.Properties.TryGetValue ("name", out uname);
                    if (room.Id == GameRoom.Id) {

                        var backendID = (int)user.Properties ["bkid"];
                        CineGameSDK.OnPlayerLeft?.Invoke (backendID);

                        Debug.LogFormat ("SFS Removing {0} ({1}) from room, probably due to idle timeout. This does not affect game.", user.Name, uname);
                        GameRoom.RemoveUser (user);

                        if (userDict.ContainsKey (backendID)) {
                            userDict.Remove (backendID);
                        }
                    }
                } catch (Exception e) {
                    //We don't care about exceptions here, just log it without error
                    Debug.LogFormat ("SFS Exception while trying to remove user {0} from room {1}: {2}", user.Name, room.Name, e.ToString ());
                }
            }
        }

        internal static void Disconnect () {
            if (sfs != null) {
                _Disconnect ();
                Debug.Log ("SFS Disconnected");
            }
        }

        static void _Disconnect () {
            if (sfs != null && sfs.MySelf != null && (sfs.MySelf.IsAdmin () || sfs.MySelf.IsModerator ())) {
                //Kick all other users out of room
                foreach (var user in GameRoom.UserList) {
                    if (user != sfs.MySelf) {
                        sfs.Send (new KickUserRequest (user.Id, "gameover"));
                    }
                }
                //Now the room should be destroyed when we disconnect so the gamecode will be free
            }
            reset ();
        }

        internal static void StopGameRoomJoins () {
            try {
                //We stop further users from joining by setting the room capacity to current number of users
                sfs.Send (new ChangeRoomCapacityRequest (GameRoom, GameRoom.UserCount, GameRoom.MaxSpectators));
            } catch (Exception e) {
                Debug.LogWarningFormat ("SFS Failed to fix room capacity at {0}: {1}", GameRoom.UserCount, e.Message);
            }
        }

        internal static void UpdateRoomCapacity (int maxPlayers, int maxSpectators) {
            MaxPlayers = maxPlayers;
            MaxSpectators = maxSpectators;
            if (sfs != null && GameRoom != null) {
                try {
                    //We stop further users from joining by setting the room capacity to current number of users
                    sfs.Send (new ChangeRoomCapacityRequest (GameRoom, maxPlayers, maxSpectators));
                } catch (Exception e) {
                    Debug.LogWarningFormat ("SFS Failed to fix room capacity at {0}: {1}", GameRoom.UserCount, e.Message);
                    throw;
                }
            }
        }

        internal static void BroadcastObjectMessage (ISFSObject dataObj, bool toPlayers = true, bool toSpectators = false) {
            if (sfs != null && GameRoom != null) {
                try {
                    if (toPlayers && toSpectators) {
                        //Broadcast message to everyone in the room
                        sfs.Send (new ObjectMessageRequest (dataObj, GameRoom));
                    } else {
                        //Broadcast message to either players or spectators
                        sfs.Send (new ObjectMessageRequest (dataObj, GameRoom, toPlayers ? GameRoom.PlayerList : GameRoom.SpectatorList));
                    }
                } catch (Exception e) {
                    Debug.LogErrorFormat ("SFS Exception while sending object message to room: {0}", e.Message);
                }
            }
        }

        internal static void SendObjectMessage (ISFSObject dataObj, int backendId) {
            var users = new List<User> ();
            try {
                users.Add (userDict [backendId]);
                sfs.Send (new ObjectMessageRequest (dataObj, GameRoom, users));
            } catch (Exception e) {
                if (!(e is NullReferenceException) && e.Message != null) {
                    var username = (users.Count > 0 && users [0] != null) ? users [0].Name : "unknown";
                    Debug.LogErrorFormat ("SFS Exception while sending object message to [{0}]: {1}", username, e.Message);
                }
            }
        }

        internal static void SendPrivateMessage (string msg, int backendId) {
            User user = null;
            try {
                user = userDict [backendId];
                sfs.Send (new PrivateMessageRequest (msg, user.Id));
            } catch (Exception e) {
                Debug.LogErrorFormat ("SFS Error while sending private message to {0}: {1}", user != null ? user.Name : backendId, e.Message);
            }
        }

        internal static void SetMaxPlayers (int maxPlayers) {
            MaxPlayers = maxPlayers;
        }

        internal static void KickUser (int backendId) {
            User user = null;
            try {
                user = userDict [backendId];
                sfs.Send (new KickUserRequest (user.Id, "getout"));
            } catch (Exception e) {
                Debug.LogErrorFormat ("SFS Error while sending kick message to {0}: {1}", user != null ? user.Name : backendId, e.Message);
            }
        }
    }
}
