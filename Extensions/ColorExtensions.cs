using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace Crimson.CustomEvents.Extensions
{
	internal static class ColorExtensions
	{
		/// <summary>
		/// Returns the hex color representation of the given <see cref="Color"/>.
		/// </summary>
		/// <param name="color"><see cref="Color"/> to get the hex representation of.</param>
		/// <param name="includeHash">Whether to include the leading "#" symbol.</param>
		/// <param name="rgbOnly">Whether to ignore the <see cref="Color.A"/> property and return a RGB string.</param>
		/// <returns></returns>
		public static string ToHex(this Color color, bool includeHash = false, bool rgbOnly = true)
		{
			string[] ret = rgbOnly ? new string[3] : new string[4];

			if (rgbOnly)
			{
				ret[0] = color.R.ToString("X2");
				ret[1] = color.G.ToString("X2");
				ret[2] = color.B.ToString("X2");
			}
			else
			{
				ret[0] = color.A.ToString("X2");
				ret[1] = color.R.ToString("X2");
				ret[2] = color.G.ToString("X2");
				ret[3] = color.B.ToString("X2");
			}

			return (includeHash ? "#" : string.Empty) + string.Join(string.Empty, ret);
		}

		/// <summary>
		/// Applies the Terraria color format to a string with the specified <see cref="Color"/>.
		/// </summary>
		/// <param name="color"><see cref="Color"/> to apply to the string.</param>
		/// <param name="input">String to apply color to.</param>
		/// <returns></returns>
		internal static string Colorize(this Color color, string input) => $"[c/{color.ToHex()}:{input}]";
	}
}