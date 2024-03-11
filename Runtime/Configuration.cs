using System.Reflection;

namespace CineGame.Host {
	internal static class Configuration {
		/// <summary>
		/// Gets the property name from an accessor method (trim the compiler-generated 'get_' or 'set_' prefix).
		/// </summary>
		private static string PropertyNameFromAccessor (MethodBase accessor) {
			return accessor.Name.Substring (4);
		}

		/// <summary>
		/// Target directory where Player software expects all logs to end up
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
		/// Specifies whether SDK should contact production or staging env
		/// </summary>
		public static string NODE_ENV {
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value); }
		}

		/// <summary>
		/// Specifies which cluster the machine is set to work with, so we can determine which API group to contact (dev, staging or production)
		/// </summary>
		public static string CLUSTER_NAME
		{
			get { return System.Environment.GetEnvironmentVariable(PropertyNameFromAccessor(MethodBase.GetCurrentMethod())); }
			set { System.Environment.SetEnvironmentVariable(PropertyNameFromAccessor(MethodBase.GetCurrentMethod()), value); }
		}

		/// <summary>
		/// Access token for backend communication, set by either Player software or HostEditor
		/// </summary>
		public static string CINEMATAZTIC_ACCESS_TOKEN {
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value); }
		}

		/// <summary>
		/// Screen ID from player software/dch
		/// </summary>
		public static string CINEMATAZTIC_SCREEN_ID {
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value); }
		}

		/// <summary>
		/// Show ID from player software/dch if available
		/// </summary>
		public static string CINEMATAZTIC_SHOW_ID {
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value); }
		}

		/// <summary>
		/// Block ID from player software/dch if available
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
		/// Current market, set by the Player software. If not available, the default market from CineGameSettings is used
		/// </summary>
		public static string MARKET_ID {
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value); }
		}

		/// <summary>
		/// HostEditor SDK will set this when testing a WebGL build in the Editor
		/// </summary>
		public static string IS_WEBGL {
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value); }
		}
	}
}
