/* KatTradeManager.Properties.cs - NinjaScript properties (partial class) v0.94 (2026-07-31) */

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using NinjaTrader.NinjaScript;
using NinjaTrader.Gui;

namespace NinjaTrader.NinjaScript.Indicators.KAT
{
	public sealed class AtmTemplateNameConverter : TypeConverter
	{
		private static string GetAtmTemplateDirectory()
		{
			return Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "templates", "AtmStrategy");
		}

		private static List<string> GetTemplateNames()
		{
			List<string> names = new List<string>();
			string directory = GetAtmTemplateDirectory();
			if (!Directory.Exists(directory)) return names;

			foreach (string file in Directory.GetFiles(directory, "*.xml"))
				names.Add(Path.GetFileNameWithoutExtension(file));

			names.Sort(StringComparer.OrdinalIgnoreCase);
			return names;
		}

		public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
		{
			return true;
		}

		public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
		{
			return true;
		}

		public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
		{
			return new StandardValuesCollection(GetTemplateNames());
		}

		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
		}

		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
		{
			return value == null ? string.Empty : value.ToString();
		}

		public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == typeof(string))
				return value == null ? string.Empty : value.ToString();
			return base.ConvertTo(context, culture, value, destinationType);
		}
	}
	public partial class KatTradeManager
	{
		#region NinjaScript Properties
		[NinjaScriptProperty]
		[Display(Name="Show Control Panel", Order=0, GroupName="Parameters")]
		public bool IsPanelVisible { get; set; }

		[NinjaScriptProperty]
		[Display(Name="HUD Location", Order=0, GroupName="Parameters", Description="Select where to display HUD: ChartTrader (right-side panel) or InChart (chart overlay)")]
		public KatHudLocation PanelLocation { get; set; }

		[NinjaScriptProperty]
		[Range(0, 2000)]
		[Display(Name="HUD Left Inset (px)", Order=0, GroupName="HUD", Description="Default left inset for a fresh HUD position. Dragged positions are preserved.")]
		public int HudLeftInset { get; set; }

		[NinjaScriptProperty]
		[Display(Name="HUD Drag Enabled", Order=1, GroupName="HUD", Description="Allow dragging HUD background. Controls remain clickable.")]
		public bool HudDragEnabled { get; set; }

		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name="Default Quantity", Order=1, GroupName="Parameters")]
		public int DefaultQuantity { get; set; }

		[NinjaScriptProperty]
		[TypeConverter(typeof(NinjaTrader.NinjaScript.AccountNameConverter))]
		[Display(Name="Account Name", Order=2, GroupName="Parameters")]
		public string AccountName { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Account Filter (Keywords)", Order=3, GroupName="Parameters", Description="Filter accounts in HUD by comma-separated keywords (e.g. '79424, Sim101' or '!BX, !LTE'). Empty = show all.")]
		public string AccountFilter { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Timeframe", Order=3, GroupName="Parameters")]
		public KatTimeframe DefaultTimeframe { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name="Default Buffer (Ticks)", Order=4, GroupName="Parameters")]
		public int DefaultBufferTicks { get; set; }

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.StringStandardValuesEditorKey")]
		[TypeConverter(typeof(AtmTemplateNameConverter))]
		[Display(Name="Default ATM Template", Order=5, GroupName="Parameters")]
		public string DefaultAtmTemplate { get; set; }

		[NinjaScriptProperty]
		[Range(1, 10000)]
		[Display(Name="Default Distance (Ticks)", Order=6, GroupName="Parameters")]
		public int DefaultDistanceTicks { get; set; }

		[NinjaScriptProperty]
		[Range(1, 99)]
		[Display(Name="Partial Candle Pullback (%)", Order=7, GroupName="Parameters")]
		public int DefaultPartialCandlePercent { get; set; }

		#region ATM Quick Set Properties
		private string atmSet1Name = "A";
		private string atmSet2Name = "B";
		private string atmSet3Name = "C";
		private string atmSet4Name = "D";
		private string atmSet5Name = "E";
		private string atmSet6Name = "F";

		[NinjaScriptProperty]
		[Display(Name="Set 1 Name", Order=1, GroupName="ATM Quick Sets", Description="Button label (max 3 chars)")]
		public string AtmSet1Name
		{
			get { return atmSet1Name; }
			set { atmSet1Name = KatTradeCalculator.NormalizeAtmSetName(value, "A"); }
		}

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.StringStandardValuesEditorKey")]
		[TypeConverter(typeof(AtmTemplateNameConverter))]
		[Display(Name="Set 1 ATM", Order=2, GroupName="ATM Quick Sets")]
		public string AtmSet1Atm { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Set 2 Name", Order=3, GroupName="ATM Quick Sets", Description="Button label (max 3 chars)")]
		public string AtmSet2Name
		{
			get { return atmSet2Name; }
			set { atmSet2Name = KatTradeCalculator.NormalizeAtmSetName(value, "B"); }
		}

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.StringStandardValuesEditorKey")]
		[TypeConverter(typeof(AtmTemplateNameConverter))]
		[Display(Name="Set 2 ATM", Order=4, GroupName="ATM Quick Sets")]
		public string AtmSet2Atm { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Set 3 Name", Order=5, GroupName="ATM Quick Sets", Description="Button label (max 3 chars)")]
		public string AtmSet3Name
		{
			get { return atmSet3Name; }
			set { atmSet3Name = KatTradeCalculator.NormalizeAtmSetName(value, "C"); }
		}

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.StringStandardValuesEditorKey")]
		[TypeConverter(typeof(AtmTemplateNameConverter))]
		[Display(Name="Set 3 ATM", Order=6, GroupName="ATM Quick Sets")]
		public string AtmSet3Atm { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Set 4 Name", Order=7, GroupName="ATM Quick Sets", Description="Button label (max 3 chars)")]
		public string AtmSet4Name
		{
			get { return atmSet4Name; }
			set { atmSet4Name = KatTradeCalculator.NormalizeAtmSetName(value, "D"); }
		}

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.StringStandardValuesEditorKey")]
		[TypeConverter(typeof(AtmTemplateNameConverter))]
		[Display(Name="Set 4 ATM", Order=8, GroupName="ATM Quick Sets")]
		public string AtmSet4Atm { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Set 5 Name", Order=9, GroupName="ATM Quick Sets", Description="Button label (max 3 chars)")]
		public string AtmSet5Name
		{
			get { return atmSet5Name; }
			set { atmSet5Name = KatTradeCalculator.NormalizeAtmSetName(value, "E"); }
		}

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.StringStandardValuesEditorKey")]
		[TypeConverter(typeof(AtmTemplateNameConverter))]
		[Display(Name="Set 5 ATM", Order=10, GroupName="ATM Quick Sets")]
		public string AtmSet5Atm { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Set 6 Name", Order=11, GroupName="ATM Quick Sets", Description="Button label (max 3 chars)")]
		public string AtmSet6Name
		{
			get { return atmSet6Name; }
			set { atmSet6Name = KatTradeCalculator.NormalizeAtmSetName(value, "F"); }
		}

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.StringStandardValuesEditorKey")]
		[TypeConverter(typeof(AtmTemplateNameConverter))]
		[Display(Name="Set 6 ATM", Order=12, GroupName="ATM Quick Sets")]
		public string AtmSet6Atm { get; set; }
		#endregion

		#region Daily Risk Control Properties
		[NinjaScriptProperty]
		[Display(Name="Daily Max DD Enabled", Order=1, GroupName="Daily Risk Control", Description="Enable Daily Max Drawdown limit protection.")]
		public bool DailyMaxDDEnabled { get; set; }

		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Daily Max DD ($)", Order=2, GroupName="Daily Risk Control", Description="Max daily drawdown limit in dollars (e.g. 500 for $500 max loss limit).")]
		public double DailyMaxDD { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Daily Max Profit Enabled", Order=3, GroupName="Daily Risk Control", Description="Enable Daily Max Profit limit protection.")]
		public bool DailyMaxProfitEnabled { get; set; }

		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Daily Max Profit ($)", Order=4, GroupName="Daily Risk Control", Description="Max daily profit limit in dollars (e.g. 1000 for $1000 max profit limit).")]
		public double DailyMaxProfit { get; set; }
		#endregion

		#region EMA Place Filter Properties

		[NinjaScriptProperty]
		[Display(Name="1st EMA Place Enabled", Order=10, GroupName="EMA Place Filter")]
		public bool EmaPlace1Enabled { get; set; }

		[NinjaScriptProperty]
		[Range(1, 500)]
		[Display(Name="1st EMA Place Period", Order=11, GroupName="EMA Place Filter")]
		public int EmaPlace1Period { get; set; }

		[NinjaScriptProperty]
		[Display(Name="1st EMA Place Timeframe", Order=12, GroupName="EMA Place Filter")]
		public KatEmaTimeframe EmaPlace1Timeframe { get; set; }

		[NinjaScriptProperty]
		[Display(Name="2nd EMA Place Enabled", Order=13, GroupName="EMA Place Filter")]
		public bool EmaPlace2Enabled { get; set; }

		[NinjaScriptProperty]
		[Range(1, 500)]
		[Display(Name="2nd EMA Place Period", Order=14, GroupName="EMA Place Filter")]
		public int EmaPlace2Period { get; set; }

		[NinjaScriptProperty]
		[Display(Name="2nd EMA Place Timeframe", Order=15, GroupName="EMA Place Filter")]
		public KatEmaTimeframe EmaPlace2Timeframe { get; set; }

		[NinjaScriptProperty]
		[Display(Name="3rd EMA Place Enabled", Order=16, GroupName="EMA Place Filter")]
		public bool EmaPlace3Enabled { get; set; }

		[NinjaScriptProperty]
		[Range(1, 500)]
		[Display(Name="3rd EMA Place Period", Order=17, GroupName="EMA Place Filter")]
		public int EmaPlace3Period { get; set; }

		[NinjaScriptProperty]
		[Display(Name="3rd EMA Place Timeframe", Order=18, GroupName="EMA Place Filter")]
		public KatEmaTimeframe EmaPlace3Timeframe { get; set; }
		#endregion

		#region EMA Angle Filter Properties
		[NinjaScriptProperty]
		[Display(Name="1st EMA Angle Enabled", Order=20, GroupName="EMA Angle Filter")]
		public bool EmaAngle1Enabled { get; set; }

		[NinjaScriptProperty]
		[Range(1, 500)]
		[Display(Name="1st EMA Angle Period", Order=21, GroupName="EMA Angle Filter")]
		public int EmaAngle1Period { get; set; }

		[NinjaScriptProperty]
		[Display(Name="1st EMA Angle Timeframe", Order=22, GroupName="EMA Angle Filter")]
		public KatEmaTimeframe EmaAngle1Timeframe { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, 90.0)]
		[Display(Name="1st EMA Angle Min Angle (°)", Order=23, GroupName="EMA Angle Filter")]
		public double EmaAngle1MinAngle { get; set; }

		[NinjaScriptProperty]
		[Display(Name="2nd EMA Angle Enabled", Order=24, GroupName="EMA Angle Filter")]
		public bool EmaAngle2Enabled { get; set; }

		[NinjaScriptProperty]
		[Range(1, 500)]
		[Display(Name="2nd EMA Angle Period", Order=25, GroupName="EMA Angle Filter")]
		public int EmaAngle2Period { get; set; }

		[NinjaScriptProperty]
		[Display(Name="2nd EMA Angle Timeframe", Order=26, GroupName="EMA Angle Filter")]
		public KatEmaTimeframe EmaAngle2Timeframe { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, 90.0)]
		[Display(Name="2nd EMA Angle Min Angle (°)", Order=27, GroupName="EMA Angle Filter")]
		public double EmaAngle2MinAngle { get; set; }

		[NinjaScriptProperty]
		[Display(Name="3rd EMA Angle Enabled", Order=28, GroupName="EMA Angle Filter")]
		public bool EmaAngle3Enabled { get; set; }

		[NinjaScriptProperty]
		[Range(1, 500)]
		[Display(Name="3rd EMA Angle Period", Order=29, GroupName="EMA Angle Filter")]
		public int EmaAngle3Period { get; set; }

		[NinjaScriptProperty]
		[Display(Name="3rd EMA Angle Timeframe", Order=30, GroupName="EMA Angle Filter")]
		public KatEmaTimeframe EmaAngle3Timeframe { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, 90.0)]
		[Display(Name="3rd EMA Angle Min Angle (°)", Order=31, GroupName="EMA Angle Filter")]
		public double EmaAngle3MinAngle { get; set; }
		#endregion

		#region Hotkey Properties
		[NinjaScriptProperty]
		[Display(Name="Enable Hotkeys", Order=40, GroupName="Hotkeys")]
		public bool HotkeyEnabled { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Buy EMA 34", Order=41, GroupName="Hotkeys")]
		public Key HotkeyBuyEma34 { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Sell EMA 34", Order=42, GroupName="Hotkeys")]
		public Key HotkeySellEma34 { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Buy EMA 89", Order=43, GroupName="Hotkeys")]
		public Key HotkeyBuyEma89 { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Sell EMA 89", Order=44, GroupName="Hotkeys")]
		public Key HotkeySellEma89 { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Buy Previous Candle", Order=45, GroupName="Hotkeys")]
		public Key HotkeyBuyPrev { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Sell Previous Candle", Order=46, GroupName="Hotkeys")]
		public Key HotkeySellPrev { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Buy Current Candle", Order=47, GroupName="Hotkeys")]
		public Key HotkeyBuyCurr { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Sell Current Candle", Order=48, GroupName="Hotkeys")]
		public Key HotkeySellCurr { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Buy +Distance", Order=49, GroupName="Hotkeys")]
		public Key HotkeyBuyDist { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Sell -Distance", Order=50, GroupName="Hotkeys")]
		public Key HotkeySellDist { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Buy Market", Order=51, GroupName="Hotkeys")]
		public Key HotkeyBuyMarket { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Sell Market", Order=52, GroupName="Hotkeys")]
		public Key HotkeySellMarket { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Set Breakeven (BE)", Order=53, GroupName="Hotkeys")]
		public Key HotkeyBE { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Revert Position", Order=54, GroupName="Hotkeys")]
		public Key HotkeyRevert { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Close / Flatten", Order=55, GroupName="Hotkeys")]
		public Key HotkeyClose { get; set; }
		#endregion
		#endregion
	}
}
