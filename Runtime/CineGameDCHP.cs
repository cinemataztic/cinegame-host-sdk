using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CineGame.SDK;
using Newtonsoft.Json;
using UnityEngine;

namespace CineGame.SDK
{
    public class CineGameDCHP : MonoBehaviour
    {
        // DCH-P
        private SocketClient DCHPClient;
        private static Queue<string> DCHPMessages = new Queue<string>();
        private static Queue<string> DCHPErrors = new Queue<string>();
        private static IEnumerator ListenToDCHPCoroutine;
        private static float blockDuration;

        public const string APP_DURATION_ENV_VAR = "CINEMATAZTIC_BLOCK_DURATION_SEC";

        public void Start()
        {
            blockDuration = CineGameMarket.Durations[CineGameSDK.Market];

            // Env Duration
            string blockDurationString = Environment.GetEnvironmentVariable(APP_DURATION_ENV_VAR);
            if (!String.IsNullOrEmpty(blockDurationString))
            {
                int newBlockDuration = Int32.Parse(blockDurationString);
                blockDuration = newBlockDuration;
            }

            CineGameSDK.OnBlockDurationUpdated?.Invoke(blockDuration);

            if (!Application.isEditor)
            {
                SetupDCHPListener();
            }
        }

        void Update()
        {
            if (DCHPMessages.Count > 0)
            {
                Debug.Log(DCHPMessages.Dequeue());
            }

            if (DCHPErrors.Count > 0)
            {
                Debug.Log(DCHPErrors.Dequeue());
            }
        }

        private async Task SetupDCHPListener()
        {
            try
            {
                var port = 4455;
                var portString = Environment.GetEnvironmentVariable("INTERNAL_TCP_SERVER_PORT");
                if (!string.IsNullOrWhiteSpace(portString))
                {
                    port = int.Parse(portString);
                }
                DCHPMessages.Enqueue("Attempting to connect to 127.0.0.1:" + port);
                DCHPClient = new SocketClient();
                await DCHPClient.Open("127.0.0.1", port, OnDCHPMessage, OnDCHPError);
                if (!DCHPClient.Connected)
                    throw new Exception("DCH-P Socket not connected");
                DCHPMessages.Enqueue("DCH-P listener connected via TCP to " + DCHPClient.RemoteEndPoint);
                ListenToDCHPCoroutine = ListenToDCHP();
                StartCoroutine(ListenToDCHPCoroutine);
            }
            catch (Exception ex)
            {
                Debug.LogError("Exception while starting DCH-P listener: " + ex.Message);
            }
        }

        private void StopDCHPListener()
        {
            try
            {
                if (ListenToDCHPCoroutine != null)
                {
                    StopCoroutine(ListenToDCHPCoroutine);
                    ListenToDCHPCoroutine = null;
                }
                DCHPClient?.Close();
                DCHPMessages.Enqueue("DCH-P listener has been closed");
            }
            catch (Exception ex)
            {
                DCHPMessages.Enqueue("Exception while disposing DCH-P TCP listener: " + ex.Message);
            }
        }

        private IEnumerator ListenToDCHP()
        {
            var intervalSecs = 10;
            var intervalString = Environment.GetEnvironmentVariable("BLOCK_DURATIONS_POLL_INTERVAL_SECS");
            if (!string.IsNullOrWhiteSpace(intervalString))
            {
                intervalSecs = int.Parse(intervalString);
            }

            DCHPMessages.Enqueue($"Polling DCH-P for adjusted blocks every {intervalSecs} seconds ...");

            DCHPClient.SendTimeout = intervalSecs;

            while (DCHPClient.Connected)
            {
                DCHPClient.Send("{\"action\":\"getAdjustedBlocks\"}\n");
                yield return new WaitForSecondsRealtime(intervalSecs);
            }
            DCHPErrors.Enqueue("DCH-P TCP disconnected, will not receive any more adjusted blocks");
        }

        private void OnDCHPError(int errorCode, string message)
        {
            DCHPErrors.Enqueue($"DCH-P Socket error: {errorCode} {message}");
        }

        private void OnDCHPMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                DCHPMessages.Enqueue("DCH-P data available, but response is empty or whitespace");
                return;
            }
            try
            {
                //DCH-p messages are doubly serialized as string @@
                message = JsonConvert.DeserializeObject<string>(message);
                DCHPMessages.Enqueue("DCH-P response: " + message);

                float newBlockDuration = blockDuration;
                IEnumerable<BlockDurationUpdate> blockDurationUpdates = null;
                if (message.Contains("adjusted"))
                {
                    var adjustedBlocksResponse = JsonConvert.DeserializeObject<AdjustedBlocksResponse>(message);
                    if (adjustedBlocksResponse != null && adjustedBlocksResponse.adjusted)
                    {
                        blockDurationUpdates = adjustedBlocksResponse.data;
                    }
                }
                else
                {
                    blockDurationUpdates = JsonConvert.DeserializeObject<List<BlockDurationUpdate>>(message);
                }
                var cineGameUpdate = blockDurationUpdates?.FirstOrDefault(bu => bu.type == "CineGame");
                if (cineGameUpdate != default)
                {
                    newBlockDuration = Mathf.Clamp(cineGameUpdate.duration, 0, 1200);
                    if (newBlockDuration != blockDuration)
                    {
                        blockDuration = newBlockDuration;
                        CineGameSDK.OnBlockDurationUpdated?.Invoke(blockDuration);
                        DCHPMessages.Enqueue("New CineGame block duration from DCH-P: " + newBlockDuration);
                    }
                    else
                    {
                        DCHPMessages.Enqueue("CineGame block duration not changed");
                    }
                }
                else
                {
                    DCHPMessages.Enqueue("No CineGame block type found in block duration updates");
                }
            }
            catch (Exception ex)
            {
                DCHPErrors.Enqueue("Exception while deserializing DCH-P message: " + ex.Message);
            }
        }

        private void OnApplicationQuit()
        {
            StopDCHPListener();
        }


        /// <summary>
        /// Message broadcast by DCH-P to TCP listeners when block duration changes
        /// </summary>
        private class AdjustedBlocksResponse
        {
            public bool adjusted;
            public BlockDurationUpdate[] data;
        }


        /// <summary>
        /// Duration update for each type of block. We only care about CineGame type for now
        /// </summary>
        private class BlockDurationUpdate
        {
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

        public class SocketClient
        {

            public delegate void SocketCallback(string data);
            public delegate void SocketErrorHandler(int errorCode, string message);

            private Socket _socket;
            private readonly byte[] _receiveBuffer = new byte[8192];

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

            private SocketFlags _receiveFlags = SocketFlags.None;

            /// <summary>
            /// Socket.Connected seems unreliable, so we bypass that and get the lowlevel socket error code to determine connected state.
            /// </summary>
            public bool Connected
            {
                get
                {
                    var errorCode = (SocketError)(int)_socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error);
                    return errorCode == SocketError.IsConnected
                        || errorCode == SocketError.Success
                        || errorCode == SocketError.IOPending
                        || errorCode == SocketError.InProgress
                        || errorCode == SocketError.AlreadyInProgress;
                }
            }

            public int SendTimeout
            {
                set
                {
                    _socket.SendTimeout = value;
                }
                get
                {
                    return _socket.SendTimeout;
                }
            }

            public int ReceiveTimeout
            {
                set
                {
                    _socket.ReceiveTimeout = value;
                }
                get
                {
                    return _socket.ReceiveTimeout;
                }
            }

            public System.Net.EndPoint RemoteEndPoint
            {
                get
                {
                    return _socket.RemoteEndPoint;
                }
            }

            public async Task<bool> Open(string address, int port, SocketCallback callback, SocketErrorHandler errorHandler)
            {
                if (_socket == null)
                    _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                using (var sp = new SemaphoreSlim(0, 1))
                {
                    _socket.BeginConnect(address, port, new AsyncCallback(_openCallback), sp);
                    await sp.WaitAsync();
                }

                if (Connected)
                {
                    _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                    _socket.BeginReceive(_receiveBuffer, 0, _receiveBuffer.Length, _receiveFlags, new AsyncCallback(_receiveCallback), null);
                    _callback = callback;
                    _errorHandler = errorHandler;
                    return true;
                }
                return false;
            }

            public void Close()
            {
                _socket.Dispose();
                _socket = null;
            }

            void _openCallback(IAsyncResult AR)
            {
                try
                {
                    _socket.EndConnect(AR);
                }
                catch (SocketException ex)
                {
                    _errorHandler(ex.ErrorCode, ex.Message);
                }
                ((SemaphoreSlim)AR.AsyncState).Release();
            }

            void _receiveCallback(IAsyncResult AR)
            {
                int received = _socket.EndReceive(AR);
                if (received <= 0)
                    return;

                try
                {
                    _callback?.Invoke(Encoding.UTF8.GetString(_receiveBuffer, 0, received));
                }
                finally
                {
                    // Start receiving again
                    _socket.BeginReceive(_receiveBuffer, 0, _receiveBuffer.Length, _receiveFlags, new AsyncCallback(_receiveCallback), null);
                }
            }

            /// <summary>
            /// Send string data async (return immediately)
            /// </summary>
            public void Send(string data)
            {
                var bytes = Encoding.UTF8.GetBytes(data);
                var socketAsyncData = new SocketAsyncEventArgs();
                socketAsyncData.SetBuffer(bytes, 0, bytes.Length);
                socketAsyncData.SocketFlags = (SocketFlags)MSG_NOSIGNAL;
                _socket.SendAsync(socketAsyncData);
            }

        }
    }
}
