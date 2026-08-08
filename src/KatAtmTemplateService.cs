/* KatAtmTemplateService.cs - Unified ATM template cache (5s TTL) v1.66 (2026-08-08) */
// ponytail: merges HasAtmTemplate (File.Exists per template) + GetCachedAtmTemplateNames (Directory.GetFiles) into one listing cache.
// Before: 2 locks, 2 TTLs, duplicated IO. After: single Directory.GetFiles every 5s, Exists() is HashSet lookup (no IO).
// Ceiling: TTL 5s means newly saved template appears after 5s — acceptable for HUD dropdown; restart indicator for instant.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NinjaTrader.NinjaScript.Indicators
{
	internal static class KatAtmTemplateService
	{
		private static readonly object cacheLock = new object();
		private static HashSet<string> cachedNames;
		private static List<string> cachedSorted;
		private static DateTime cachedUtc = DateTime.MinValue;
		private const double TtlSeconds = 5.0;

		private static HashSet<string> GetOrRefresh()
		{
			lock (cacheLock)
			{
				if (cachedNames != null && cachedSorted != null && (DateTime.UtcNow - cachedUtc).TotalSeconds < TtlSeconds)
					return cachedNames;
			}
			// ponytail: reflection avoids CS0433 Globals ambiguous (Core vs Client both define it) when service is linked into test project
			string baseDir = null;
			try
			{
				var t = Type.GetType("NinjaTrader.Core.Globals, NinjaTrader.Core");
				if (t != null)
				{
					var pi = t.GetProperty("UserDataDir");
					if (pi != null) baseDir = pi.GetValue(null) as string;
				}
			}
			catch { }
			if (string.IsNullOrEmpty(baseDir))
				baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NinjaTrader 8");
			string atmDir = Path.Combine(baseDir, "templates", "AtmStrategy");
			HashSet<string> fresh = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				if (Directory.Exists(atmDir))
					foreach (var f in Directory.GetFiles(atmDir, "*.xml"))
						fresh.Add(Path.GetFileNameWithoutExtension(f));
			}
			catch { }
			List<string> sorted = new List<string>(fresh);
			sorted.Sort(StringComparer.OrdinalIgnoreCase);
			lock (cacheLock)
			{
				// double-check: another thread may have refreshed while we did IO outside lock
				if (cachedNames != null && cachedSorted != null && (DateTime.UtcNow - cachedUtc).TotalSeconds < TtlSeconds)
					return cachedNames;
				cachedNames = fresh; cachedSorted = sorted; cachedUtc = DateTime.UtcNow;
			}
			return fresh;
		}

		public static bool Exists(string templateName)
		{
			if (string.IsNullOrEmpty(templateName)) return false;
			return GetOrRefresh().Contains(templateName);
		}

		public static List<string> GetNames()
		{
			GetOrRefresh();
			lock (cacheLock) { return new List<string>(cachedSorted ?? new List<string>()); }
		}

		// For tests: force refresh
		internal static void Clear() { lock (cacheLock) { cachedNames = null; cachedSorted = null; cachedUtc = DateTime.MinValue; } }
	}
}
