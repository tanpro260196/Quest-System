using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace Crimson.CustomEvents.Extensions
{
	public static class StringExtensions
	{
		public static bool CultureSensitiveContains(this string src, string value)
			=> CultureInfo.CurrentCulture.CompareInfo.IndexOf(src, value, CompareOptions.IgnoreCase) >= 0;

		public static string JoinWithAnd(this IEnumerable<string> src)
		{
			var tmp = src.ToArray();
			StringBuilder sb = new StringBuilder(string.Join(", ", tmp, 0, tmp.Length - 1));
			return sb.Append($" and {tmp[tmp.Length - 1]}").ToString();
		}

		public static Color ToColor(this string hexString)
		{
			if (hexString.StartsWith("#"))
				hexString = hexString.Substring(1);

			uint hex = uint.Parse(hexString, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

			Color color = Color.White;

			switch (hexString.Length)
			{
				case 8:
					color.A = (byte) (hex >> 24);
					color.R = (byte) (hex >> 16);
					color.G = (byte) (hex >> 8);
					color.B = (byte) hex;
					break;
				case 6:
					color.R = (byte) (hex >> 16);
					color.G = (byte) (hex >> 8);
					color.B = (byte) hex;
					break;
				default:
					throw new InvalidOperationException("Invald hex representation of an ARGB or RGB color value.");
			}

			return color;
		}

		internal static string Colorize(this string input, Color color) => $"[c/{color.ToHex()}:{input}]";
	}
}