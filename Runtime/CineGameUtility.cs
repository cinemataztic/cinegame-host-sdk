using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace CineGame.Host
{
    public static class CineGameUtility
	{
		/// <summary>
		/// Decode a URL-friendly Base64 string
		/// </summary>
		public static byte[] Base64UrlDecode(string input)
		{
			var output = new System.Text.StringBuilder(input, input.Length + 2);
			output = output.Replace('-', '+'); // 62nd char of encoding
			output = output.Replace('_', '/'); // 63rd char of encoding

			// Pad with trailing '='s
			switch (output.Length % 4)
			{
				case 0:
					break; // No pad chars in this case
				case 2:
					output.Append("==");
					break; // Two pad chars
				case 3:
					output.Append('=');
					break; // One pad char
				default:
					throw new Exception(string.Format("Illegal base-64 string: {0}", input));
			}
			return Convert.FromBase64String(output.ToString());
		}

		/// <summary>
		/// Compute MD5 Checksum from an input string
		/// </summary>
		public static string ComputeMD5Hash(string s)
		{
			// Form hash
			using (var md5h = MD5.Create())
			{
				var data = md5h.ComputeHash(System.Text.Encoding.Default.GetBytes(s));
				// Create string representation
				var sb = new System.Text.StringBuilder();
				for (int i = 0; i < data.Length; ++i)
				{
					sb.Append(data[i].ToString("x2"));
				}
				return sb.ToString();
			}
		}

		/// <summary>
		/// Cap string to a maximum length and add a postfix if truncated
		/// </summary>
		public static string Truncate (this string s, int maxLength, string postfix = "…") {
			if (s.Length <= maxLength) return s;
			return s.Substring (0, maxLength) + postfix;
		}
	}
}
