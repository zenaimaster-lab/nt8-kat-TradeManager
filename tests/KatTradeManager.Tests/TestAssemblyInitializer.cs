using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace KatTradeManager.Tests
{
	public static class TestAssemblyInitializer
	{
		[ModuleInitializer]
		public static void Initialize()
		{
			AssemblyLoadContext.Default.Resolving += OnContextResolving;
			AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
		}

		private static Assembly OnContextResolving(AssemblyLoadContext context, AssemblyName name)
		{
			return ResolveNinjaTraderAssembly(name.Name);
		}

		private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
		{
			string name = new AssemblyName(args.Name).Name;
			return ResolveNinjaTraderAssembly(name);
		}

		private static Assembly ResolveNinjaTraderAssembly(string name)
		{
			if (string.IsNullOrEmpty(name)) return null;

			try
			{
				string ntBinPath = @"C:\Program Files\NinjaTrader 8\bin";
				string filePath = Path.Combine(ntBinPath, name + ".dll");

				if (File.Exists(filePath))
				{
					return AssemblyLoadContext.Default.LoadFromAssemblyPath(filePath);
				}
			}
			catch
			{
				// Fallback to null if resolution fails
			}

			return null;
		}
	}
}
