using System.Reflection;

namespace CineGame.SDK {
	/// <summary>
	/// Statically typed compile-time definitions of environment variables
	/// </summary>
	public static class Configuration {
		/// <summary>
		/// Gets the property name from an accessor method (trim the compiler-generated 'get_' or 'set_' prefix).
		/// </summary>
		private static string PropertyNameFromAccessor (MethodBase accessor) {
			return accessor.Name.Substring (4);
		}

		/// <summary>
		/// Target directory where DCH expects all logs to end up
		/// </summary>
		public static string LOG_DIR {
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
		}

		/// <summary>
		/// Specifies the local system time (in JavaScript Ticks, ie milliseconds since Jan 1 1970) where the CineGame block should ideally have started
		/// </summary>
		public static long BLOCK_START_TICKS {
			get { return long.Parse (System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()))); }
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value.ToString ()); }
		}

		/// <summary>
		/// Overrides the default block duration at startup
		/// </summary>
		public static int? CINEMATAZTIC_BLOCK_DURATION_SEC {
			get {
				var env = System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()));
				if (!string.IsNullOrWhiteSpace (env))
					return int.Parse (env);
				else return null;
			}
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value.ToString ()); }
		}

		/// <summary>
		/// Specifies which CinemaTaztic cluster the client is currently communicating with
		/// </summary>
		public static string CLUSTER_NAME
		{
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value); }
		}

		/// <summary>
		/// Access token for backend communication, set by either DCH or Editor SDK
		/// </summary>
		public static string CINEMATAZTIC_ACCESS_TOKEN {
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value); }
		}

		/// <summary>
		/// Exhibitor id/name from DCH if available
		/// </summary>
		public static string CINEMATAZTIC_EXHIBITOR {
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value); }
		}

		/// <summary>
		/// Screen ID from DCH
		/// </summary>
		public static string CINEMATAZTIC_SCREEN_ID {
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value); }
		}

		/// <summary>
		/// Show ID from DCH if available
		/// </summary>
		public static string CINEMATAZTIC_SHOW_ID {
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value); }
		}

		/// <summary>
		/// Block ID from DCH if available
		/// </summary>
		public static string CINEMATAZTIC_BLOCK_ID {
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value); }
		}

		/// <summary>
		/// Seat layout, if available, set by the Player software
		/// </summary>
		public static string SEAT_LAYOUT {
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
		}

		/// <summary>
		/// Current market, set by the DCH. If not available, the default market from CineGameSettings is used
		/// </summary>
		public static string MARKET_ID {
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value); }
		}

		/// <summary>
		/// The SDK will set this when testing a WebGL build in the Editor
		/// </summary>
		public static string IS_WEBGL {
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value); }
		}

		/// <summary>
		/// Port on which the local DCH TCP server is listening
		/// </summary>
		public static int? INTERNAL_TCP_SERVER_PORT {
			get {
				var env = System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()));
				if (!string.IsNullOrWhiteSpace (env))
					return int.Parse (env);
				else return null;
			}
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value.ToString ()); }
		}

		/// <summary>
		/// Interval in seconds between each block duration poll on the TCP connection
		/// </summary>
		public static int? BLOCK_DURATIONS_POLL_INTERVAL_SECS {
			get {
				var env = System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()));
				if (!string.IsNullOrWhiteSpace (env))
					return int.Parse (env);
				else return null;
			}
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value.ToString ()); }
		}
	}
}
