﻿using System.Reflection;

namespace CineGame.Host {
	public static class Configuration {
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
		/// Specifies whether SDK should contact production or staging env
		/// </summary>
		public static string NODE_ENV {
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value); }
		}

		/// <summary>
		/// Access token for backend communication, set by either Player software or HostEditor
		/// </summary>
		public static string CINEMATAZTIC_ACCESS_TOKEN {
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
			set { System.Environment.SetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ()), value); }
		}

		/// <summary>
		/// Seat layout, set by the Player software
		/// </summary>
		public static string SEAT_LAYOUT {
			get { return System.Environment.GetEnvironmentVariable (PropertyNameFromAccessor (MethodBase.GetCurrentMethod ())); }
		}

		/// <summary>
		/// Current market, set by the Player software
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
