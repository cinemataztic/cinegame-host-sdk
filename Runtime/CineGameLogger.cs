using System;
using System.Text;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace CineGame.Host {

    internal class CineGameLogger
    {
        /// <summary>
        /// GameType, as reported to the server
        /// </summary>
        public static string GameType;

        static StreamWriter logWriter;
        static string logFilename;

        [RuntimeInitializeOnLoadMethod (RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad () {
            if (Application.platform == RuntimePlatform.Android
             || Application.platform == RuntimePlatform.IPhonePlayer
             || Application.platform == RuntimePlatform.WebGLPlayer
             || Application.isEditor) {
                Debug.Log ("Custom logger disabled on this platform");
                return;
            }
            if (logWriter != null)
                return;
            try {
                string logLocation;
                if (Application.platform == RuntimePlatform.OSXPlayer) {
                    logLocation = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.Personal), string.Format ("Library/Logs/Unity/{0}/{1}", Application.companyName, Application.productName));
                } else {
                    logLocation = Application.persistentDataPath;
                }
                Debug.LogFormat ("Log location: {0}", logLocation);
                if (!Directory.Exists (logLocation)) {
                    Directory.CreateDirectory (logLocation);
                }
                logFilename = Path.Combine (logLocation, "cinemagame-" + DateTime.UtcNow.ToString ("yyyyMMdd-HHmmss") + ".log");
                var stream = File.Open (logFilename, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
                logWriter = new StreamWriter (stream, Encoding.UTF8);
                logWriter.AutoFlush = true;
                Application.logMessageReceived -= HandleLogMessage;
                Application.logMessageReceived += HandleLogMessage;

                //Generate buildtime string from compiler-generated assembly version
                var assemblyVersion = typeof(CineGameLogger).Assembly.GetName ().Version;
                var startDate = new DateTime (2000, 1, 1, 0, 0, 0);
                var span = new TimeSpan (assemblyVersion.Build, 0, 0, assemblyVersion.Revision * 2);
                var buildDate = startDate.Add (span);
                var buildTimeString = buildDate.ToString ("u");

                Debug.Log ($"Engine: {Application.unityVersion} Build time: {buildTimeString} Hostname: {Environment.MachineName}");
            } catch (Exception e) {
                Debug.LogError (e.ToString ());
            }
        }

        static void HandleLogMessage (string condition, string stackTrace, LogType logType) {
            /*var rtss = Time.realtimeSinceStartup;
            var secs = (int)rtss;
            var timeStamp = string.Format ("{0:00}:{1:00}:{2:000}", secs / 60, secs % 60, (int)((rtss - (float)secs) * 1000f));*/

            logWriter.WriteLine ("{0} {1} {2}", DateTime.Now.ToString ("yyyy-MM-dd HH:mm:ss,fff"), (logType == LogType.Log) ? "Info" : logType.ToString (), condition);

            if (logType == LogType.Exception || logType == LogType.Assert || logType == LogType.Error) {
                //Print entire stacktrace
                logWriter.WriteLine (stackTrace);
            }
        }

        internal static void OnApplicationQuit()
        {
            if (logWriter != null) {
                logWriter.Close ();
            }
            if (Application.isEditor || string.IsNullOrEmpty(logFilename))
                return;

            var defaultLogLocation = Configuration.LOG_DIR;
            if (!string.IsNullOrWhiteSpace (defaultLogLocation)) {
                //Player software has specified a custom log directory, copy temp log to this location
                defaultLogLocation = Path.Combine (defaultLogLocation, "Player.log");
            } else {
                //Overwrite default unity player log with our temp log
                switch (Application.platform) {
                case RuntimePlatform.LinuxPlayer:
                    defaultLogLocation = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), string.Format ("unity3d/{0}/{1}/Player.log", Application.companyName, Application.productName));
                    break;
                case RuntimePlatform.WindowsPlayer:
                    defaultLogLocation = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), string.Format ("..\\LocalLow\\{0}\\{1}\\Player.log", Application.companyName, Application.productName));
                    break;
                case RuntimePlatform.OSXPlayer:
                default:
                    defaultLogLocation = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.Personal), "Library/Logs/Unity/Player.log");
                    break;
                }
            }
            try
            {
                File.Copy(logFilename, defaultLogLocation, true);
            }
            catch (Exception ex)
            {
                Debug.LogWarningFormat("Logger.OverwriteUnityLog failed: {0}", ex);
            }
        }
    }
}
