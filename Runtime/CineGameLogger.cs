using System;
using System.Text;
using System.IO;
using System.Collections.Generic;

using UnityEngine;

namespace CineGame.SDK {

    /// <summary>
	/// Custom compact logger for CineGame. Includes timestamp in each line and file creation timestamp in the filename. Output path can be overriden via env var LOG_DIR
	/// </summary>
    internal class CineGameLogger
    {
        static StreamWriter LogWriter;
        static string LogName;
        static string LogPath;

        /// <summary>
		/// The ID (Title) of the game in the CineGame system
		/// </summary>
        public static string GameID;

        [RuntimeInitializeOnLoadMethod (RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad () {
            if (Application.platform == RuntimePlatform.Android
             || Application.platform == RuntimePlatform.IPhonePlayer
             || Application.platform == RuntimePlatform.WebGLPlayer
             || Application.isEditor) {
                Debug.Log ("Custom logger disabled on this platform");
                return;
            }
            if (LogWriter != null)
                return;
            try {
                LogName = "CineGame-" + DateTime.UtcNow.ToString ("yyyyMMdd-HHmmss") + ".log";

                LogPath = Configuration.LOG_DIR;
                if (string.IsNullOrWhiteSpace (LogPath)) {
                    LogPath = Application.persistentDataPath;
                }

                if (!Directory.Exists (LogPath))
                {
                    Directory.CreateDirectory (LogPath);
                }

                var stream = File.Open (LogPath + "/" + LogName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
				LogWriter = new StreamWriter (stream, Encoding.UTF8) {
					AutoFlush = true
				};
				Application.logMessageReceived -= HandleLogMessage;
                Application.logMessageReceived += HandleLogMessage;

                CineGameChatController.OnChatMessage += HandleChatMessage;

                var buildTimeString = (Resources.Load ("buildtime") as TextAsset).text;

                Debug.Log ($"Engine: {Application.unityVersion} Application build time: {buildTimeString} Hostname: {Environment.MachineName}");
            } catch (Exception e) {
                Debug.LogError (e.ToString ());
            }
        }

        static void HandleLogMessage(string condition, string stackTrace, LogType logType)
        {
            LogWriter.WriteLine("{0} {1} {2}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss,fff"), (logType == LogType.Log) ? "Info" : logType.ToString(), condition);

            if (logType == LogType.Exception || logType == LogType.Assert || logType == LogType.Error)
            {
                float timeSinceStartup = Time.realtimeSinceStartup;
                string timeSinceStartupString = "[ " + string.Format("{0:00}", (int)timeSinceStartup / 60 % 60) + ":" + string.Format("{0:00}", (int)timeSinceStartup % 60) + " ] ";
                LogWriter.WriteLine(timeSinceStartupString + stackTrace);
            }
        }

        static void HandleChatMessage(int backendID, string message, Dictionary<string, Rect> emojiDictionary)
        {
            LogWriter.WriteLine("{0} {1} {2}: {3}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss,fff"), "Chat message from", backendID, message);
        }
    }
}
