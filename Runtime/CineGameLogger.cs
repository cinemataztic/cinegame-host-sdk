using System;
using System.Text;
using System.IO;

using UnityEngine;

namespace CineGame.SDK {

    internal class CineGameLogger
    {
        static StreamWriter LogWriter;
        static string ENV_VAR_LOG_DIR = "LOG_DIR";

        public static string GameID;
        public static string LogName;
        public static string LogPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad()
        {
            if (Application.isEditor || LogWriter != null)
            {
                return;
            }

            LogName = "CineGame-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".log";

            if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable(ENV_VAR_LOG_DIR)))
            {
                LogPath = Application.persistentDataPath;
            }
            else
            {
                LogPath = Environment.GetEnvironmentVariable(ENV_VAR_LOG_DIR);
            }

            if (!Directory.Exists(LogPath))
            {
                Directory.CreateDirectory(LogPath);
            }

            FileStream stream = File.Open(LogPath + "/" + LogName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            LogWriter = new StreamWriter(stream, Encoding.UTF8);
            LogWriter.AutoFlush = true;
            Application.logMessageReceived -= HandleLogMessage;
            Application.logMessageReceived += HandleLogMessage;

            var buildTimeString = (Resources.Load ("buildtime") as TextAsset).text;

            Debug.Log ($"Engine: {Application.unityVersion} Application build time: {buildTimeString} Hostname: {Environment.MachineName}");
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
    }
}
