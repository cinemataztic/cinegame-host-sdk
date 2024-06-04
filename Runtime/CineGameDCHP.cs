using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using UnityEngine;

namespace CineGame.SDK {

    /// <summary>
	/// Class for dynamically adjusting the CineGame/CineClash block. The default block duration can be overridden at start via env, or it can be adjusted dynamically via TCP.
	/// </summary>
    internal static class CineGameDCHP {

        static SocketClient DCHPClient;
        static readonly Queue<string> DCHPMessages = new ();
        static readonly Queue<string> DCHPErrors = new ();

        static float BlockDuration;

        static int IntervalSecs = 10;
        static float NextTimePoll;

        /// <summary>
		/// Set initial block duration and start polling for updates via TCP.
		/// </summary>
        public static void Start () {
            BlockDuration = Configuration.CINEMATAZTIC_BLOCK_DURATION_SEC ?? CineGameMarket.Durations [CineGameSDK.Market];
            Debug.Log ("Initial block duration: " + BlockDuration);

            CineGameSDK.OnBlockDurationUpdated?.Invoke (BlockDuration);

            if (!Application.isEditor) {
                _ = SetupDCHPListener ();
            } else {
                Debug.Log ("DCH-p listener not started in editor");
            }
        }

        /// <summary>
		/// Log async messages and poll periodically for block duration updates via TCP.
		/// </summary>
        public static void Update () {
            while (DCHPMessages.Count != 0) {
                Debug.Log (DCHPMessages.Dequeue ());
            }

            while (DCHPErrors.Count != 0) {
                Debug.LogError (DCHPErrors.Dequeue ());
            }

            if (DCHPClient.Connected && Time.realtimeSinceStartup >= NextTimePoll) {
                NextTimePoll = Time.realtimeSinceStartup + IntervalSecs;
                DCHPClient.Send ("{\"action\":\"getAdjustedBlocks\"}\n");
            }
        }

        /// <summary>
		/// Attempts to connect to DCH-p via TCP
		/// </summary>
        private static async Task SetupDCHPListener () {
            try {
                var port = Configuration.INTERNAL_TCP_SERVER_PORT ?? 4455;
                DCHPMessages.Enqueue ("Attempting to connect to 127.0.0.1:" + port);
                DCHPClient = new SocketClient ();
                await DCHPClient.Open ("127.0.0.1", port, OnDCHPMessage, OnDCHPError);
                if (!DCHPClient.Connected)
                    throw new Exception ("DCH-P Socket not connected");
                DCHPMessages.Enqueue ("DCH-P listener connected via TCP to " + DCHPClient.RemoteEndPoint);

                IntervalSecs = Configuration.BLOCK_DURATIONS_POLL_INTERVAL_SECS ?? 10;
                NextTimePoll = Time.realtimeSinceStartup + IntervalSecs;

                DCHPMessages.Enqueue ($"Polling DCH-P for adjusted blocks every {IntervalSecs} seconds ...");

                DCHPClient.SendTimeout = IntervalSecs;
            } catch (Exception ex) {
                DCHPErrors.Enqueue ("Exception while starting DCH-P listener: " + ex.Message);
            }
        }

        /// <summary>
		/// Stop polling for block updates via TCP.
		/// </summary>
        public static void Stop () {
            try {
                DCHPClient?.Close ();
                DCHPMessages.Enqueue ("DCH-P listener has been closed");
            } catch (Exception ex) {
                DCHPErrors.Enqueue ("Exception while disposing DCH-P TCP listener: " + ex.Message);
            }
        }

        /// <summary>
		/// Handle TCP errors
		/// </summary>
        private static void OnDCHPError (int errorCode, string message) {
            DCHPErrors.Enqueue ($"DCH-P Socket error: {errorCode} {message}");
        }

        /// <summary>
		/// Handle TCP messages
		/// </summary>
        private static void OnDCHPMessage (string message) {
            if (string.IsNullOrWhiteSpace (message)) {
                DCHPMessages.Enqueue ("DCH-P data available, but response is empty or whitespace");
                return;
            }
            try {
                //DCH-p messages are doubly serialized as string @@
                message = JsonConvert.DeserializeObject<string> (message);
                DCHPMessages.Enqueue ("DCH-P response: " + message);

                float newBlockDuration = BlockDuration;
                IEnumerable<BlockDurationUpdate> blockDurationUpdates = null;
                if (message.Contains ("adjusted")) {
                    var adjustedBlocksResponse = JsonConvert.DeserializeObject<AdjustedBlocksResponse> (message);
                    if (adjustedBlocksResponse != null && adjustedBlocksResponse.adjusted) {
                        blockDurationUpdates = adjustedBlocksResponse.data;
                    }
                } else {
                    blockDurationUpdates = JsonConvert.DeserializeObject<List<BlockDurationUpdate>> (message);
                }
                var cineGameUpdate = blockDurationUpdates?.FirstOrDefault (bu => bu.type == "CineGame");
                if (cineGameUpdate != default) {
                    newBlockDuration = Mathf.Clamp (cineGameUpdate.duration, 0, 1200);
                    if (newBlockDuration != BlockDuration) {
                        BlockDuration = newBlockDuration;
                        CineGameSDK.OnBlockDurationUpdated?.Invoke (BlockDuration);
                        DCHPMessages.Enqueue ("New CineGame block duration from DCH-P: " + newBlockDuration);
                    } else {
                        DCHPMessages.Enqueue ("CineGame block duration not changed");
                    }
                } else {
                    DCHPMessages.Enqueue ("No CineGame block type found in block duration updates");
                }
            } catch (Exception ex) {
                DCHPErrors.Enqueue ("Exception while deserializing DCH-P message: " + ex.Message);
            }
        }



        /// <summary>
        /// Message broadcast by DCH-P to TCP listeners when block duration changes
        /// </summary>
        private class AdjustedBlocksResponse {
            public bool adjusted;
            public BlockDurationUpdate [] data;
        }


        /// <summary>
        /// Duration update for each type of block. We only care about CineGame type for now
        /// </summary>
        private class BlockDurationUpdate {
            /// <summary>
            /// duration in seconds.
            /// </summary>
            public int duration;
            /// <summary>
            /// type of block. we only care about "CineGame".
            /// </summary>
            public string type;
            //public string @event;
        }

        /// <summary>
		/// Custom TCP socket using low-level socket interface and tcp protocol. High-level stuff has a tendency to crash the Unity Player.
		/// </summary>
        public class SocketClient {

            public delegate void SocketCallback (string data);
            public delegate void SocketErrorHandler (int errorCode, string message);

            private Socket _socket;
            private readonly byte [] _receiveBuffer = new byte [8192];

            /// <summary>
            /// The callback which receives string data from the socket
            /// </summary>
            private SocketCallback _callback;

            /// <summary>
            /// The callback which receives errors from the socket
            /// </summary>
            private SocketErrorHandler _errorHandler;

            /// <summary>
            /// Avoid SIGPIPE taking down the whole process on Linux systems
            /// </summary>
            const int MSG_NOSIGNAL = 0x4000;

            const SocketFlags _receiveFlags = SocketFlags.None;

            /// <summary>
            /// Socket.Connected seems unreliable, so we bypass that and get the lowlevel socket error code to determine connected state.
            /// </summary>
            public bool Connected {
                get {
                    var errorCode = (SocketError)(int)_socket.GetSocketOption (SocketOptionLevel.Socket, SocketOptionName.Error);
                    return errorCode == SocketError.IsConnected
                        || errorCode == SocketError.Success
                        || errorCode == SocketError.IOPending
                        || errorCode == SocketError.InProgress
                        || errorCode == SocketError.AlreadyInProgress;
                }
            }

            public int SendTimeout {
                set {
                    _socket.SendTimeout = value;
                }
                get {
                    return _socket.SendTimeout;
                }
            }

            public int ReceiveTimeout {
                set {
                    _socket.ReceiveTimeout = value;
                }
                get {
                    return _socket.ReceiveTimeout;
                }
            }

            public System.Net.EndPoint RemoteEndPoint {
                get {
                    return _socket.RemoteEndPoint;
                }
            }

            /// <summary>
			/// Open a TCP socket and start listening.
			/// </summary>
            public async Task<bool> Open (string address, int port, SocketCallback callback, SocketErrorHandler errorHandler) {
                if (_socket == null)
                    _socket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                using (var sp = new SemaphoreSlim (0, 1)) {
                    _socket.BeginConnect (address, port, new AsyncCallback (OpenCallback), sp);
                    await sp.WaitAsync ();
                }

                if (Connected) {
                    _socket.SetSocketOption (SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                    _socket.BeginReceive (_receiveBuffer, 0, _receiveBuffer.Length, _receiveFlags, new AsyncCallback (ReceiveCallback), null);
                    _callback = callback;
                    _errorHandler = errorHandler;
                    return true;
                }
                return false;
            }

            /// <summary>
			/// Close the tcp connection and dispose of the socket.
			/// </summary>
            public void Close () {
                _socket.Dispose ();
                _socket = null;
            }

            /// <summary>
			/// The tcp connection has been established.
			/// </summary>
            void OpenCallback (IAsyncResult AR) {
                try {
                    _socket.EndConnect (AR);
                } catch (SocketException ex) {
                    _errorHandler (ex.ErrorCode, ex.Message);
                }
                ((SemaphoreSlim)AR.AsyncState).Release ();
            }

            /// <summary>
			/// Data has been received on the TCP connection.
			/// </summary>
            void ReceiveCallback (IAsyncResult AR) {
                int received = _socket.EndReceive (AR);
                if (received <= 0)
                    return;

                try {
                    _callback?.Invoke (Encoding.UTF8.GetString (_receiveBuffer, 0, received));
                } finally {
                    // Start receiving again
                    _socket.BeginReceive (_receiveBuffer, 0, _receiveBuffer.Length, _receiveFlags, new AsyncCallback (ReceiveCallback), null);
                }
            }

            /// <summary>
            /// Send string data async (return immediately)
            /// </summary>
            public void Send (string data) {
                var bytes = Encoding.UTF8.GetBytes (data);
                var socketAsyncData = new SocketAsyncEventArgs ();
                socketAsyncData.SetBuffer (bytes, 0, bytes.Length);
                socketAsyncData.SocketFlags = (SocketFlags)MSG_NOSIGNAL;
                _socket.SendAsync (socketAsyncData);
            }

        }
    }
}
