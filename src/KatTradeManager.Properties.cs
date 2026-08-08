/* KatTradeManager.Properties.cs - NinjaScript properties (partial class) v1.53 (2026-08-08) */

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.NinjaScript;
using NinjaTrader.Gui;

namespace NinjaTrader.NinjaScript.Indicators.KAT
{
	public sealed class AtmTemplateNameConverter : TypeConverter
	{
		private static List<string> GetTemplateNames()
		{
			// ponytail: delegate to unified 5s cache — avoids Directory.GetFiles on every property-grid open (was laggy + file-lock prone)
			try { return KatAtmTemplateService.GetNames(); } catch { return new List<string>(); }
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
		[Range(6, 12)]
		[Display(Name="Quick Set Font Size", Order=2, GroupName="HUD", Description="Font size for quick-set/program preset buttons only (smaller = more space for custom labels).")]
		public double QuickSetFontSize { get; set; }

		private Brush quickSetLabelColor = Brushes.White;
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="Quick Set Label Color", Order=3, GroupName="HUD", Description="Base label color for quick-set/program buttons (combined with opacity below).")]
		public Brush QuickSetLabelColor
		{
			get { return quickSetLabelColor; }
			set
			{
				try
				{
					if (value == null) { quickSetLabelColor = Brushes.White; return; }
					if (value is SolidColorBrush scb)
					{
						var c = scb.Color;
						var nb = new SolidColorBrush(c);
						if (nb.CanFreeze) nb.Freeze();
						quickSetLabelColor = nb;
					}
					else if (value is Freezable f && f.CanFreeze)
					{
						var clone = f.Clone() as Freezable;
						if (clone != null && clone.CanFreeze) clone.Freeze();
						quickSetLabelColor = clone as Brush ?? value;
					}
					else quickSetLabelColor = value;
				}
				catch { quickSetLabelColor = Brushes.White; }
			}
		}

		[Browsable(false)]
		public string QuickSetLabelColorSerializable
		{
			get
			{
				try
				{
					if (quickSetLabelColor is SolidColorBrush scb)
						return scb.Color.ToString();
					return Colors.White.ToString();
				}
				catch { return Colors.White.ToString(); }
			}
			set
			{
				try
				{
					if (!string.IsNullOrWhiteSpace(value))
					{
						var c = (Color)ColorConverter.ConvertFromString(value);
						var nb = new SolidColorBrush(c);
						if (nb.CanFreeze) nb.Freeze();
						quickSetLabelColor = nb;
					}
					else quickSetLabelColor = Brushes.White;
				}
				catch { quickSetLabelColor = Brushes.White; }
			}
		}

		[NinjaScriptProperty]
		[Range(10, 100)]
		[Display(Name="Quick Set Label Opacity %", Order=4, GroupName="HUD", Description="Opacity for quick-set/program label text (100=opaque, 50=50% transparent).")]
		public int QuickSetLabelOpacityPercent { get; set; }

		private Brush programLabelColor = Brushes.White;
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="Program Label Color", Order=5, GroupName="HUD", Description="Base label color for Program (P1..P8) buttons (combined with opacity below). Default white 80% transparent.")]
		public Brush ProgramLabelColor
		{
			get { return programLabelColor; }
			set
			{
				try
				{
					if (value == null) { programLabelColor = Brushes.White; return; }
					if (value is SolidColorBrush scb)
					{
						var c = scb.Color;
						var nb = new SolidColorBrush(c);
						if (nb.CanFreeze) nb.Freeze();
						programLabelColor = nb;
					}
					else if (value is Freezable f && f.CanFreeze)
					{
						var clone = f.Clone() as Freezable;
						if (clone != null && clone.CanFreeze) clone.Freeze();
						programLabelColor = clone as Brush ?? value;
					}
					else programLabelColor = value;
				}
				catch { programLabelColor = Brushes.White; }
			}
		}

		[Browsable(false)]
		public string ProgramLabelColorSerializable
		{
			get
			{
				try
				{
					if (programLabelColor is SolidColorBrush scb)
						return scb.Color.ToString();
					return Colors.White.ToString();
				}
				catch { return Colors.White.ToString(); }
			}
			set
			{
				try
				{
					if (!string.IsNullOrWhiteSpace(value))
					{
						var c = (Color)ColorConverter.ConvertFromString(value);
						var nb = new SolidColorBrush(c);
						if (nb.CanFreeze) nb.Freeze();
						programLabelColor = nb;
					}
					else programLabelColor = Brushes.White;
				}
				catch { programLabelColor = Brushes.White; }
			}
		}

		[NinjaScriptProperty]
		[Range(10, 100)]
		[Display(Name="Program Label Opacity %", Order=6, GroupName="HUD", Description="Opacity for Program label text (100=opaque, 20=80% transparent). Default 20.")]
		public int ProgramLabelOpacityPercent { get; set; }

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

		#region ATM Quick Set Properties
		private string atmSet1Name = "A";
		private string atmSet2Name = "B";
		private string atmSet3Name = "C";
		private string atmSet4Name = "D";
		private string atmSet5Name = "E";
		private string atmSet6Name = "F";
		private string atmSet7Name = "G";
		private string atmSet8Name = "H";

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

		[NinjaScriptProperty]
		[Display(Name="Set 7 Name", Order=13, GroupName="ATM Quick Sets", Description="Button label (max 3 chars)")]
		public string AtmSet7Name
		{
			get { return atmSet7Name; }
			set { atmSet7Name = KatTradeCalculator.NormalizeAtmSetName(value, "G"); }
		}

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.StringStandardValuesEditorKey")]
		[TypeConverter(typeof(AtmTemplateNameConverter))]
		[Display(Name="Set 7 ATM", Order=14, GroupName="ATM Quick Sets")]
		public string AtmSet7Atm { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Set 8 Name", Order=15, GroupName="ATM Quick Sets", Description="Button label (max 3 chars)")]
		public string AtmSet8Name
		{
			get { return atmSet8Name; }
			set { atmSet8Name = KatTradeCalculator.NormalizeAtmSetName(value, "H"); }
		}

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.StringStandardValuesEditorKey")]
		[TypeConverter(typeof(AtmTemplateNameConverter))]
		[Display(Name="Set 8 ATM", Order=16, GroupName="ATM Quick Sets")]
		public string AtmSet8Atm { get; set; }
		#endregion

		#region Trading Profile Properties
		private string profile1Name = "P1";
		private string profile2Name = "P2";
		private string profile3Name = "P3";
		private string profile4Name = "P4";
		private string profile5Name = "P5";
		private string profile6Name = "P6";
		private string profile7Name = "P7";
		private string profile8Name = "P8";

		[NinjaScriptProperty]
		[Display(Name="Profile 1 Name", Order=1, GroupName="Trading Profile 1", Description="HUD button label (max 8 chars)")]
		public string TradingProfile1Name
		{
			get { return profile1Name; }
			set { profile1Name = KatTradeCalculator.NormalizeProfileName(value, "P1"); }
		}
		[NinjaScriptProperty]
		[TypeConverter(typeof(NinjaTrader.NinjaScript.AccountNameConverter))]
		[Display(Name="Profile 1 Account", Order=2, GroupName="Trading Profile 1", Description="Account for this preset (dropdown)")]
		public string TradingProfile1Account { get; set; }
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.StringStandardValuesEditorKey")]
		[TypeConverter(typeof(AtmTemplateNameConverter))]
		[Display(Name="Profile 1 ATM", Order=3, GroupName="Trading Profile 1")]
		public string TradingProfile1Atm { get; set; }
		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name="Profile 1 Quantity", Order=4, GroupName="Trading Profile 1")]
		public int TradingProfile1Quantity { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 1 Timeframe", Order=5, GroupName="Trading Profile 1")]
		public KatTimeframe TradingProfile1Timeframe { get; set; }
		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name="Profile 1 Buffer Ticks", Order=6, GroupName="Trading Profile 1")]
		public int TradingProfile1BufferTicks { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 1 Stop-Limit", Order=7, GroupName="Trading Profile 1")]
		public bool TradingProfile1StopLimitEnabled { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 1 EMA Protect", Order=8, GroupName="Trading Profile 1")]
		public bool TradingProfile1EmaProtectEnabled { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 1 Max DD Enabled", Order=9, GroupName="Trading Profile 1")]
		public bool TradingProfile1DailyMaxDDEnabled { get; set; }
		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Profile 1 Max DD ($)", Order=10, GroupName="Trading Profile 1")]
		public double TradingProfile1DailyMaxDD { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 1 Max Profit Enabled", Order=11, GroupName="Trading Profile 1")]
		public bool TradingProfile1DailyMaxProfitEnabled { get; set; }
		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Profile 1 Max Profit ($)", Order=12, GroupName="Trading Profile 1")]
		public double TradingProfile1DailyMaxProfit { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 1 Sizing Protect", Order=13, GroupName="Trading Profile 1")]
		public bool TradingProfile1SizingProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 1 SL-Pull Protect", Order=14, GroupName="Trading Profile 1")]
		public bool TradingProfile1SlPullProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 1 Loss-DCA Protect", Order=15, GroupName="Trading Profile 1")]
		public bool TradingProfile1LossDcaProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 1 TP-Early Protect", Order=16, GroupName="Trading Profile 1")]
		public bool TradingProfile1TpEarlyProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 1 LossTimes Protect", Order=17, GroupName="Trading Profile 1")]
		public bool TradingProfile1LossTimesProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 1 Timing Protect", Order=18, GroupName="Trading Profile 1")]
		public bool TradingProfile1TimingProtect { get; set; }
		[NinjaScriptProperty]
		[Range(1, 20)]
		[Display(Name="Profile 1 LossTimes Max Losses", Order=19, GroupName="Trading Profile 1")]
		public int TradingProfile1LossTimesMaxLosses { get; set; }
		[NinjaScriptProperty]
		[Range(1, 1440)]
		[Display(Name="Profile 1 LossTimes Lock (min)", Order=20, GroupName="Trading Profile 1")]
		public int TradingProfile1LossTimesLockMinutes { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Profile 2 Name", Order=1, GroupName="Trading Profile 2", Description="HUD button label (max 8 chars)")]
		public string TradingProfile2Name
		{
			get { return profile2Name; }
			set { profile2Name = KatTradeCalculator.NormalizeProfileName(value, "P2"); }
		}
		[NinjaScriptProperty]
		[TypeConverter(typeof(NinjaTrader.NinjaScript.AccountNameConverter))]
		[Display(Name="Profile 2 Account", Order=2, GroupName="Trading Profile 2")]
		public string TradingProfile2Account { get; set; }
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.StringStandardValuesEditorKey")]
		[TypeConverter(typeof(AtmTemplateNameConverter))]
		[Display(Name="Profile 2 ATM", Order=3, GroupName="Trading Profile 2")]
		public string TradingProfile2Atm { get; set; }
		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name="Profile 2 Quantity", Order=4, GroupName="Trading Profile 2")]
		public int TradingProfile2Quantity { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 2 Timeframe", Order=5, GroupName="Trading Profile 2")]
		public KatTimeframe TradingProfile2Timeframe { get; set; }
		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name="Profile 2 Buffer Ticks", Order=6, GroupName="Trading Profile 2")]
		public int TradingProfile2BufferTicks { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 2 Stop-Limit", Order=7, GroupName="Trading Profile 2")]
		public bool TradingProfile2StopLimitEnabled { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 2 EMA Protect", Order=8, GroupName="Trading Profile 2")]
		public bool TradingProfile2EmaProtectEnabled { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 2 Max DD Enabled", Order=9, GroupName="Trading Profile 2")]
		public bool TradingProfile2DailyMaxDDEnabled { get; set; }
		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Profile 2 Max DD ($)", Order=10, GroupName="Trading Profile 2")]
		public double TradingProfile2DailyMaxDD { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 2 Max Profit Enabled", Order=11, GroupName="Trading Profile 2")]
		public bool TradingProfile2DailyMaxProfitEnabled { get; set; }
		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Profile 2 Max Profit ($)", Order=12, GroupName="Trading Profile 2")]
		public double TradingProfile2DailyMaxProfit { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 2 Sizing Protect", Order=13, GroupName="Trading Profile 2")]
		public bool TradingProfile2SizingProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 2 SL-Pull Protect", Order=14, GroupName="Trading Profile 2")]
		public bool TradingProfile2SlPullProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 2 Loss-DCA Protect", Order=15, GroupName="Trading Profile 2")]
		public bool TradingProfile2LossDcaProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 2 TP-Early Protect", Order=16, GroupName="Trading Profile 2")]
		public bool TradingProfile2TpEarlyProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 2 LossTimes Protect", Order=17, GroupName="Trading Profile 2")]
		public bool TradingProfile2LossTimesProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 2 Timing Protect", Order=18, GroupName="Trading Profile 2")]
		public bool TradingProfile2TimingProtect { get; set; }
		[NinjaScriptProperty]
		[Range(1, 20)]
		[Display(Name="Profile 2 LossTimes Max Losses", Order=19, GroupName="Trading Profile 2")]
		public int TradingProfile2LossTimesMaxLosses { get; set; }
		[NinjaScriptProperty]
		[Range(1, 1440)]
		[Display(Name="Profile 2 LossTimes Lock (min)", Order=20, GroupName="Trading Profile 2")]
		public int TradingProfile2LossTimesLockMinutes { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Profile 3 Name", Order=1, GroupName="Trading Profile 3", Description="HUD button label (max 8 chars)")]
		public string TradingProfile3Name
		{
			get { return profile3Name; }
			set { profile3Name = KatTradeCalculator.NormalizeProfileName(value, "P3"); }
		}
		[NinjaScriptProperty]
		[TypeConverter(typeof(NinjaTrader.NinjaScript.AccountNameConverter))]
		[Display(Name="Profile 3 Account", Order=2, GroupName="Trading Profile 3")]
		public string TradingProfile3Account { get; set; }
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.StringStandardValuesEditorKey")]
		[TypeConverter(typeof(AtmTemplateNameConverter))]
		[Display(Name="Profile 3 ATM", Order=3, GroupName="Trading Profile 3")]
		public string TradingProfile3Atm { get; set; }
		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name="Profile 3 Quantity", Order=4, GroupName="Trading Profile 3")]
		public int TradingProfile3Quantity { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 3 Timeframe", Order=5, GroupName="Trading Profile 3")]
		public KatTimeframe TradingProfile3Timeframe { get; set; }
		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name="Profile 3 Buffer Ticks", Order=6, GroupName="Trading Profile 3")]
		public int TradingProfile3BufferTicks { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 3 Stop-Limit", Order=7, GroupName="Trading Profile 3")]
		public bool TradingProfile3StopLimitEnabled { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 3 EMA Protect", Order=8, GroupName="Trading Profile 3")]
		public bool TradingProfile3EmaProtectEnabled { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 3 Max DD Enabled", Order=9, GroupName="Trading Profile 3")]
		public bool TradingProfile3DailyMaxDDEnabled { get; set; }
		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Profile 3 Max DD ($)", Order=10, GroupName="Trading Profile 3")]
		public double TradingProfile3DailyMaxDD { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 3 Max Profit Enabled", Order=11, GroupName="Trading Profile 3")]
		public bool TradingProfile3DailyMaxProfitEnabled { get; set; }
		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Profile 3 Max Profit ($)", Order=12, GroupName="Trading Profile 3")]
		public double TradingProfile3DailyMaxProfit { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 3 Sizing Protect", Order=13, GroupName="Trading Profile 3")]
		public bool TradingProfile3SizingProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 3 SL-Pull Protect", Order=14, GroupName="Trading Profile 3")]
		public bool TradingProfile3SlPullProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 3 Loss-DCA Protect", Order=15, GroupName="Trading Profile 3")]
		public bool TradingProfile3LossDcaProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 3 TP-Early Protect", Order=16, GroupName="Trading Profile 3")]
		public bool TradingProfile3TpEarlyProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 3 LossTimes Protect", Order=17, GroupName="Trading Profile 3")]
		public bool TradingProfile3LossTimesProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 3 Timing Protect", Order=18, GroupName="Trading Profile 3")]
		public bool TradingProfile3TimingProtect { get; set; }
		[NinjaScriptProperty]
		[Range(1, 20)]
		[Display(Name="Profile 3 LossTimes Max Losses", Order=19, GroupName="Trading Profile 3")]
		public int TradingProfile3LossTimesMaxLosses { get; set; }
		[NinjaScriptProperty]
		[Range(1, 1440)]
		[Display(Name="Profile 3 LossTimes Lock (min)", Order=20, GroupName="Trading Profile 3")]
		public int TradingProfile3LossTimesLockMinutes { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Profile 4 Name", Order=1, GroupName="Trading Profile 4", Description="HUD button label (max 8 chars)")]
		public string TradingProfile4Name
		{
			get { return profile4Name; }
			set { profile4Name = KatTradeCalculator.NormalizeProfileName(value, "P4"); }
		}
		[NinjaScriptProperty]
		[TypeConverter(typeof(NinjaTrader.NinjaScript.AccountNameConverter))]
		[Display(Name="Profile 4 Account", Order=2, GroupName="Trading Profile 4")]
		public string TradingProfile4Account { get; set; }
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.StringStandardValuesEditorKey")]
		[TypeConverter(typeof(AtmTemplateNameConverter))]
		[Display(Name="Profile 4 ATM", Order=3, GroupName="Trading Profile 4")]
		public string TradingProfile4Atm { get; set; }
		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name="Profile 4 Quantity", Order=4, GroupName="Trading Profile 4")]
		public int TradingProfile4Quantity { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 4 Timeframe", Order=5, GroupName="Trading Profile 4")]
		public KatTimeframe TradingProfile4Timeframe { get; set; }
		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name="Profile 4 Buffer Ticks", Order=6, GroupName="Trading Profile 4")]
		public int TradingProfile4BufferTicks { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 4 Stop-Limit", Order=7, GroupName="Trading Profile 4")]
		public bool TradingProfile4StopLimitEnabled { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 4 EMA Protect", Order=8, GroupName="Trading Profile 4")]
		public bool TradingProfile4EmaProtectEnabled { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 4 Max DD Enabled", Order=9, GroupName="Trading Profile 4")]
		public bool TradingProfile4DailyMaxDDEnabled { get; set; }
		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Profile 4 Max DD ($)", Order=10, GroupName="Trading Profile 4")]
		public double TradingProfile4DailyMaxDD { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 4 Max Profit Enabled", Order=11, GroupName="Trading Profile 4")]
		public bool TradingProfile4DailyMaxProfitEnabled { get; set; }
		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Profile 4 Max Profit ($)", Order=12, GroupName="Trading Profile 4")]
		public double TradingProfile4DailyMaxProfit { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 4 Sizing Protect", Order=13, GroupName="Trading Profile 4")]
		public bool TradingProfile4SizingProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 4 SL-Pull Protect", Order=14, GroupName="Trading Profile 4")]
		public bool TradingProfile4SlPullProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 4 Loss-DCA Protect", Order=15, GroupName="Trading Profile 4")]
		public bool TradingProfile4LossDcaProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 4 TP-Early Protect", Order=16, GroupName="Trading Profile 4")]
		public bool TradingProfile4TpEarlyProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 4 LossTimes Protect", Order=17, GroupName="Trading Profile 4")]
		public bool TradingProfile4LossTimesProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 4 Timing Protect", Order=18, GroupName="Trading Profile 4")]
		public bool TradingProfile4TimingProtect { get; set; }
		[NinjaScriptProperty]
		[Range(1, 20)]
		[Display(Name="Profile 4 LossTimes Max Losses", Order=19, GroupName="Trading Profile 4")]
		public int TradingProfile4LossTimesMaxLosses { get; set; }
		[NinjaScriptProperty]
		[Range(1, 1440)]
		[Display(Name="Profile 4 LossTimes Lock (min)", Order=20, GroupName="Trading Profile 4")]
		public int TradingProfile4LossTimesLockMinutes { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Profile 5 Name", Order=1, GroupName="Trading Profile 5", Description="HUD button label (max 8 chars)")]
		public string TradingProfile5Name
		{
			get { return profile5Name; }
			set { profile5Name = KatTradeCalculator.NormalizeProfileName(value, "P5"); }
		}
		[NinjaScriptProperty]
		[TypeConverter(typeof(NinjaTrader.NinjaScript.AccountNameConverter))]
		[Display(Name="Profile 5 Account", Order=2, GroupName="Trading Profile 5")]
		public string TradingProfile5Account { get; set; }
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.StringStandardValuesEditorKey")]
		[TypeConverter(typeof(AtmTemplateNameConverter))]
		[Display(Name="Profile 5 ATM", Order=3, GroupName="Trading Profile 5")]
		public string TradingProfile5Atm { get; set; }
		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name="Profile 5 Quantity", Order=4, GroupName="Trading Profile 5")]
		public int TradingProfile5Quantity { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 5 Timeframe", Order=5, GroupName="Trading Profile 5")]
		public KatTimeframe TradingProfile5Timeframe { get; set; }
		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name="Profile 5 Buffer Ticks", Order=6, GroupName="Trading Profile 5")]
		public int TradingProfile5BufferTicks { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 5 Stop-Limit", Order=7, GroupName="Trading Profile 5")]
		public bool TradingProfile5StopLimitEnabled { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 5 EMA Protect", Order=8, GroupName="Trading Profile 5")]
		public bool TradingProfile5EmaProtectEnabled { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 5 Max DD Enabled", Order=9, GroupName="Trading Profile 5")]
		public bool TradingProfile5DailyMaxDDEnabled { get; set; }
		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Profile 5 Max DD ($)", Order=10, GroupName="Trading Profile 5")]
		public double TradingProfile5DailyMaxDD { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 5 Max Profit Enabled", Order=11, GroupName="Trading Profile 5")]
		public bool TradingProfile5DailyMaxProfitEnabled { get; set; }
		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Profile 5 Max Profit ($)", Order=12, GroupName="Trading Profile 5")]
		public double TradingProfile5DailyMaxProfit { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 5 Sizing Protect", Order=13, GroupName="Trading Profile 5")]
		public bool TradingProfile5SizingProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 5 SL-Pull Protect", Order=14, GroupName="Trading Profile 5")]
		public bool TradingProfile5SlPullProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 5 Loss-DCA Protect", Order=15, GroupName="Trading Profile 5")]
		public bool TradingProfile5LossDcaProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 5 TP-Early Protect", Order=16, GroupName="Trading Profile 5")]
		public bool TradingProfile5TpEarlyProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 5 LossTimes Protect", Order=17, GroupName="Trading Profile 5")]
		public bool TradingProfile5LossTimesProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 5 Timing Protect", Order=18, GroupName="Trading Profile 5")]
		public bool TradingProfile5TimingProtect { get; set; }
		[NinjaScriptProperty]
		[Range(1, 20)]
		[Display(Name="Profile 5 LossTimes Max Losses", Order=19, GroupName="Trading Profile 5")]
		public int TradingProfile5LossTimesMaxLosses { get; set; }
		[NinjaScriptProperty]
		[Range(1, 1440)]
		[Display(Name="Profile 5 LossTimes Lock (min)", Order=20, GroupName="Trading Profile 5")]
		public int TradingProfile5LossTimesLockMinutes { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Profile 6 Name", Order=1, GroupName="Trading Profile 6", Description="HUD button label (max 8 chars)")]
		public string TradingProfile6Name
		{
			get { return profile6Name; }
			set { profile6Name = KatTradeCalculator.NormalizeProfileName(value, "P6"); }
		}
		[NinjaScriptProperty]
		[TypeConverter(typeof(NinjaTrader.NinjaScript.AccountNameConverter))]
		[Display(Name="Profile 6 Account", Order=2, GroupName="Trading Profile 6")]
		public string TradingProfile6Account { get; set; }
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.StringStandardValuesEditorKey")]
		[TypeConverter(typeof(AtmTemplateNameConverter))]
		[Display(Name="Profile 6 ATM", Order=3, GroupName="Trading Profile 6")]
		public string TradingProfile6Atm { get; set; }
		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name="Profile 6 Quantity", Order=4, GroupName="Trading Profile 6")]
		public int TradingProfile6Quantity { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 6 Timeframe", Order=5, GroupName="Trading Profile 6")]
		public KatTimeframe TradingProfile6Timeframe { get; set; }
		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name="Profile 6 Buffer Ticks", Order=6, GroupName="Trading Profile 6")]
		public int TradingProfile6BufferTicks { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 6 Stop-Limit", Order=7, GroupName="Trading Profile 6")]
		public bool TradingProfile6StopLimitEnabled { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 6 EMA Protect", Order=8, GroupName="Trading Profile 6")]
		public bool TradingProfile6EmaProtectEnabled { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 6 Max DD Enabled", Order=9, GroupName="Trading Profile 6")]
		public bool TradingProfile6DailyMaxDDEnabled { get; set; }
		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Profile 6 Max DD ($)", Order=10, GroupName="Trading Profile 6")]
		public double TradingProfile6DailyMaxDD { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 6 Max Profit Enabled", Order=11, GroupName="Trading Profile 6")]
		public bool TradingProfile6DailyMaxProfitEnabled { get; set; }
		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Profile 6 Max Profit ($)", Order=12, GroupName="Trading Profile 6")]
		public double TradingProfile6DailyMaxProfit { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 6 Sizing Protect", Order=13, GroupName="Trading Profile 6")]
		public bool TradingProfile6SizingProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 6 SL-Pull Protect", Order=14, GroupName="Trading Profile 6")]
		public bool TradingProfile6SlPullProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 6 Loss-DCA Protect", Order=15, GroupName="Trading Profile 6")]
		public bool TradingProfile6LossDcaProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 6 TP-Early Protect", Order=16, GroupName="Trading Profile 6")]
		public bool TradingProfile6TpEarlyProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 6 LossTimes Protect", Order=17, GroupName="Trading Profile 6")]
		public bool TradingProfile6LossTimesProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 6 Timing Protect", Order=18, GroupName="Trading Profile 6")]
		public bool TradingProfile6TimingProtect { get; set; }
		[NinjaScriptProperty]
		[Range(1, 20)]
		[Display(Name="Profile 6 LossTimes Max Losses", Order=19, GroupName="Trading Profile 6")]
		public int TradingProfile6LossTimesMaxLosses { get; set; }
		[NinjaScriptProperty]
		[Range(1, 1440)]
		[Display(Name="Profile 6 LossTimes Lock (min)", Order=20, GroupName="Trading Profile 6")]
		public int TradingProfile6LossTimesLockMinutes { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Profile 7 Name", Order=1, GroupName="Trading Profile 7", Description="HUD button label (max 8 chars)")]
		public string TradingProfile7Name
		{
			get { return profile7Name; }
			set { profile7Name = KatTradeCalculator.NormalizeProfileName(value, "P7"); }
		}
		[NinjaScriptProperty]
		[TypeConverter(typeof(NinjaTrader.NinjaScript.AccountNameConverter))]
		[Display(Name="Profile 7 Account", Order=2, GroupName="Trading Profile 7")]
		public string TradingProfile7Account { get; set; }
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.StringStandardValuesEditorKey")]
		[TypeConverter(typeof(AtmTemplateNameConverter))]
		[Display(Name="Profile 7 ATM", Order=3, GroupName="Trading Profile 7")]
		public string TradingProfile7Atm { get; set; }
		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name="Profile 7 Quantity", Order=4, GroupName="Trading Profile 7")]
		public int TradingProfile7Quantity { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 7 Timeframe", Order=5, GroupName="Trading Profile 7")]
		public KatTimeframe TradingProfile7Timeframe { get; set; }
		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name="Profile 7 Buffer Ticks", Order=6, GroupName="Trading Profile 7")]
		public int TradingProfile7BufferTicks { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 7 Stop-Limit", Order=7, GroupName="Trading Profile 7")]
		public bool TradingProfile7StopLimitEnabled { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 7 EMA Protect", Order=8, GroupName="Trading Profile 7")]
		public bool TradingProfile7EmaProtectEnabled { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 7 Max DD Enabled", Order=9, GroupName="Trading Profile 7")]
		public bool TradingProfile7DailyMaxDDEnabled { get; set; }
		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Profile 7 Max DD ($)", Order=10, GroupName="Trading Profile 7")]
		public double TradingProfile7DailyMaxDD { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 7 Max Profit Enabled", Order=11, GroupName="Trading Profile 7")]
		public bool TradingProfile7DailyMaxProfitEnabled { get; set; }
		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Profile 7 Max Profit ($)", Order=12, GroupName="Trading Profile 7")]
		public double TradingProfile7DailyMaxProfit { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 7 Sizing Protect", Order=13, GroupName="Trading Profile 7")]
		public bool TradingProfile7SizingProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 7 SL-Pull Protect", Order=14, GroupName="Trading Profile 7")]
		public bool TradingProfile7SlPullProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 7 Loss-DCA Protect", Order=15, GroupName="Trading Profile 7")]
		public bool TradingProfile7LossDcaProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 7 TP-Early Protect", Order=16, GroupName="Trading Profile 7")]
		public bool TradingProfile7TpEarlyProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 7 LossTimes Protect", Order=17, GroupName="Trading Profile 7")]
		public bool TradingProfile7LossTimesProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 7 Timing Protect", Order=18, GroupName="Trading Profile 7")]
		public bool TradingProfile7TimingProtect { get; set; }
		[NinjaScriptProperty]
		[Range(1, 20)]
		[Display(Name="Profile 7 LossTimes Max Losses", Order=19, GroupName="Trading Profile 7")]
		public int TradingProfile7LossTimesMaxLosses { get; set; }
		[NinjaScriptProperty]
		[Range(1, 1440)]
		[Display(Name="Profile 7 LossTimes Lock (min)", Order=20, GroupName="Trading Profile 7")]
		public int TradingProfile7LossTimesLockMinutes { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Profile 8 Name", Order=1, GroupName="Trading Profile 8", Description="HUD button label (max 8 chars)")]
		public string TradingProfile8Name
		{
			get { return profile8Name; }
			set { profile8Name = KatTradeCalculator.NormalizeProfileName(value, "P8"); }
		}
		[NinjaScriptProperty]
		[TypeConverter(typeof(NinjaTrader.NinjaScript.AccountNameConverter))]
		[Display(Name="Profile 8 Account", Order=2, GroupName="Trading Profile 8")]
		public string TradingProfile8Account { get; set; }
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.StringStandardValuesEditorKey")]
		[TypeConverter(typeof(AtmTemplateNameConverter))]
		[Display(Name="Profile 8 ATM", Order=3, GroupName="Trading Profile 8")]
		public string TradingProfile8Atm { get; set; }
		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name="Profile 8 Quantity", Order=4, GroupName="Trading Profile 8")]
		public int TradingProfile8Quantity { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 8 Timeframe", Order=5, GroupName="Trading Profile 8")]
		public KatTimeframe TradingProfile8Timeframe { get; set; }
		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name="Profile 8 Buffer Ticks", Order=6, GroupName="Trading Profile 8")]
		public int TradingProfile8BufferTicks { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 8 Stop-Limit", Order=7, GroupName="Trading Profile 8")]
		public bool TradingProfile8StopLimitEnabled { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 8 EMA Protect", Order=8, GroupName="Trading Profile 8")]
		public bool TradingProfile8EmaProtectEnabled { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 8 Max DD Enabled", Order=9, GroupName="Trading Profile 8")]
		public bool TradingProfile8DailyMaxDDEnabled { get; set; }
		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Profile 8 Max DD ($)", Order=10, GroupName="Trading Profile 8")]
		public double TradingProfile8DailyMaxDD { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 8 Max Profit Enabled", Order=11, GroupName="Trading Profile 8")]
		public bool TradingProfile8DailyMaxProfitEnabled { get; set; }
		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Profile 8 Max Profit ($)", Order=12, GroupName="Trading Profile 8")]
		public double TradingProfile8DailyMaxProfit { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 8 Sizing Protect", Order=13, GroupName="Trading Profile 8")]
		public bool TradingProfile8SizingProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 8 SL-Pull Protect", Order=14, GroupName="Trading Profile 8")]
		public bool TradingProfile8SlPullProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 8 Loss-DCA Protect", Order=15, GroupName="Trading Profile 8")]
		public bool TradingProfile8LossDcaProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 8 TP-Early Protect", Order=16, GroupName="Trading Profile 8")]
		public bool TradingProfile8TpEarlyProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 8 LossTimes Protect", Order=17, GroupName="Trading Profile 8")]
		public bool TradingProfile8LossTimesProtect { get; set; }
		[NinjaScriptProperty]
		[Display(Name="Profile 8 Timing Protect", Order=18, GroupName="Trading Profile 8")]
		public bool TradingProfile8TimingProtect { get; set; }
		[NinjaScriptProperty]
		[Range(1, 20)]
		[Display(Name="Profile 8 LossTimes Max Losses", Order=19, GroupName="Trading Profile 8")]
		public int TradingProfile8LossTimesMaxLosses { get; set; }
		[NinjaScriptProperty]
		[Range(1, 1440)]
		[Display(Name="Profile 8 LossTimes Lock (min)", Order=20, GroupName="Trading Profile 8")]
		public int TradingProfile8LossTimesLockMinutes { get; set; }
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

		#region Daily Risk Quick Set Properties
		private string dailyRiskSet1Name = "1";
		private string dailyRiskSet2Name = "2";
		private string dailyRiskSet3Name = "3";
		private string dailyRiskSet4Name = "4";
		private string dailyRiskSet5Name = "5";
		private string dailyRiskSet6Name = "6";

		[NinjaScriptProperty]
		[Display(Name="Set 1 Name", Order=1, GroupName="Daily Risk Quick Sets", Description="Button label (max 3 chars)")]
		public string DailyRiskSet1Name
		{
			get { return dailyRiskSet1Name; }
			set { dailyRiskSet1Name = KatTradeCalculator.NormalizeAtmSetName(value, "1"); }
		}

		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Set 1 Max DD ($)", Order=2, GroupName="Daily Risk Quick Sets")]
		public double DailyRiskSet1MaxDD { get; set; }

		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Set 1 Max Profit ($)", Order=3, GroupName="Daily Risk Quick Sets")]
		public double DailyRiskSet1MaxProfit { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Set 2 Name", Order=4, GroupName="Daily Risk Quick Sets", Description="Button label (max 3 chars)")]
		public string DailyRiskSet2Name
		{
			get { return dailyRiskSet2Name; }
			set { dailyRiskSet2Name = KatTradeCalculator.NormalizeAtmSetName(value, "2"); }
		}

		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Set 2 Max DD ($)", Order=5, GroupName="Daily Risk Quick Sets")]
		public double DailyRiskSet2MaxDD { get; set; }

		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Set 2 Max Profit ($)", Order=6, GroupName="Daily Risk Quick Sets")]
		public double DailyRiskSet2MaxProfit { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Set 3 Name", Order=7, GroupName="Daily Risk Quick Sets", Description="Button label (max 3 chars)")]
		public string DailyRiskSet3Name
		{
			get { return dailyRiskSet3Name; }
			set { dailyRiskSet3Name = KatTradeCalculator.NormalizeAtmSetName(value, "3"); }
		}

		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Set 3 Max DD ($)", Order=8, GroupName="Daily Risk Quick Sets")]
		public double DailyRiskSet3MaxDD { get; set; }

		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Set 3 Max Profit ($)", Order=9, GroupName="Daily Risk Quick Sets")]
		public double DailyRiskSet3MaxProfit { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Set 4 Name", Order=10, GroupName="Daily Risk Quick Sets", Description="Button label (max 3 chars)")]
		public string DailyRiskSet4Name
		{
			get { return dailyRiskSet4Name; }
			set { dailyRiskSet4Name = KatTradeCalculator.NormalizeAtmSetName(value, "4"); }
		}

		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Set 4 Max DD ($)", Order=11, GroupName="Daily Risk Quick Sets")]
		public double DailyRiskSet4MaxDD { get; set; }

		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Set 4 Max Profit ($)", Order=12, GroupName="Daily Risk Quick Sets")]
		public double DailyRiskSet4MaxProfit { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Set 5 Name", Order=13, GroupName="Daily Risk Quick Sets", Description="Button label (max 3 chars)")]
		public string DailyRiskSet5Name
		{
			get { return dailyRiskSet5Name; }
			set { dailyRiskSet5Name = KatTradeCalculator.NormalizeAtmSetName(value, "5"); }
		}

		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Set 5 Max DD ($)", Order=14, GroupName="Daily Risk Quick Sets")]
		public double DailyRiskSet5MaxDD { get; set; }

		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Set 5 Max Profit ($)", Order=15, GroupName="Daily Risk Quick Sets")]
		public double DailyRiskSet5MaxProfit { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Set 6 Name", Order=16, GroupName="Daily Risk Quick Sets", Description="Button label (max 3 chars)")]
		public string DailyRiskSet6Name
		{
			get { return dailyRiskSet6Name; }
			set { dailyRiskSet6Name = KatTradeCalculator.NormalizeAtmSetName(value, "6"); }
		}

		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Set 6 Max DD ($)", Order=17, GroupName="Daily Risk Quick Sets")]
		public double DailyRiskSet6MaxDD { get; set; }

		[NinjaScriptProperty]
		[Range(0, 1000000)]
		[Display(Name="Set 6 Max Profit ($)", Order=18, GroupName="Daily Risk Quick Sets")]
		public double DailyRiskSet6MaxProfit { get; set; }
		#endregion

		#region Discipline Protects Properties
		[NinjaScriptProperty]
		[Display(Name="Sizing Protect", Order=1, GroupName="Discipline Protects", Description="When ON, blocks adding size after first fill (max = first fill / ATM qty).")]
		public bool SizingProtectEnabled { get; set; }

		[NinjaScriptProperty]
		[Display(Name="SL-Pull Protect", Order=2, GroupName="Discipline Protects", Description="When ON, blocks moving SL farther from entry (pulling tighter allowed, trailing still works).")]
		public bool SlPullProtectEnabled { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Loss-DCA Protect", Order=3, GroupName="Discipline Protects", Description="When ON, blocks DCA adds when price is against position (toward SL).")]
		public bool LossDcaProtectEnabled { get; set; }

		[NinjaScriptProperty]
		[Display(Name="TP-Early Protect", Order=4, GroupName="Discipline Protects", Description="When ON, blocks Close/flatten & scale-out (must run to TP; trailing SL still works).")]
		public bool TpEarlyProtectEnabled { get; set; }

		[NinjaScriptProperty]
		[Display(Name="LossTimes Protect", Order=5, GroupName="Discipline Protects", Description="When ON, locks new entries for N minutes after N consecutive losses.")]
		public bool LossTimesProtectEnabled { get; set; }

		[NinjaScriptProperty]
		[Display(Name="TimingWindows Protect", Order=6, GroupName="Discipline Protects", Description="When ON, blocks entries outside configured Trading Windows (NY time).")]
		public bool TimingWindowsProtectEnabled { get; set; }

		[NinjaScriptProperty]
		[Range(1, 20)]
		[Display(Name="LossTimes Max Losses", Order=7, GroupName="Discipline Protects", Description="Consecutive losses to trigger lock (default 3).")]
		public int LossTimesMaxLosses { get; set; }

		[NinjaScriptProperty]
		[Range(1, 1440)]
		[Display(Name="LossTimes Lock (min)", Order=8, GroupName="Discipline Protects", Description="Lock duration in minutes after LossTimes breach (default 30).")]
		public int LossTimesLockMinutes { get; set; }
		#endregion

		#region Trading Windows Properties
		[NinjaScriptProperty]
		[Display(Name="Window 1 Enabled", Order=1, GroupName="Trading Windows", Description="Enable Trading Window 1 (NY time).")]
		public bool TradingWindow1Enabled { get; set; }

		[NinjaScriptProperty]
		[Range(0, 23)]
		[Display(Name="Window 1 Start Hour (NY)", Order=2, GroupName="Trading Windows")]
		public int TradingWindow1StartHour { get; set; }

		[NinjaScriptProperty]
		[Range(0, 59)]
		[Display(Name="Window 1 Start Minute", Order=3, GroupName="Trading Windows")]
		public int TradingWindow1StartMinute { get; set; }

		[NinjaScriptProperty]
		[Range(0, 23)]
		[Display(Name="Window 1 End Hour (NY)", Order=4, GroupName="Trading Windows")]
		public int TradingWindow1EndHour { get; set; }

		[NinjaScriptProperty]
		[Range(0, 59)]
		[Display(Name="Window 1 End Minute", Order=5, GroupName="Trading Windows")]
		public int TradingWindow1EndMinute { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Window 2 Enabled", Order=6, GroupName="Trading Windows")]
		public bool TradingWindow2Enabled { get; set; }

		[NinjaScriptProperty]
		[Range(0, 23)]
		[Display(Name="Window 2 Start Hour (NY)", Order=7, GroupName="Trading Windows")]
		public int TradingWindow2StartHour { get; set; }

		[NinjaScriptProperty]
		[Range(0, 59)]
		[Display(Name="Window 2 Start Minute", Order=8, GroupName="Trading Windows")]
		public int TradingWindow2StartMinute { get; set; }

		[NinjaScriptProperty]
		[Range(0, 23)]
		[Display(Name="Window 2 End Hour (NY)", Order=9, GroupName="Trading Windows")]
		public int TradingWindow2EndHour { get; set; }

		[NinjaScriptProperty]
		[Range(0, 59)]
		[Display(Name="Window 2 End Minute", Order=10, GroupName="Trading Windows")]
		public int TradingWindow2EndMinute { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Window 3 Enabled", Order=11, GroupName="Trading Windows")]
		public bool TradingWindow3Enabled { get; set; }

		[NinjaScriptProperty]
		[Range(0, 23)]
		[Display(Name="Window 3 Start Hour (NY)", Order=12, GroupName="Trading Windows")]
		public int TradingWindow3StartHour { get; set; }

		[NinjaScriptProperty]
		[Range(0, 59)]
		[Display(Name="Window 3 Start Minute", Order=13, GroupName="Trading Windows")]
		public int TradingWindow3StartMinute { get; set; }

		[NinjaScriptProperty]
		[Range(0, 23)]
		[Display(Name="Window 3 End Hour (NY)", Order=14, GroupName="Trading Windows")]
		public int TradingWindow3EndHour { get; set; }

		[NinjaScriptProperty]
		[Range(0, 59)]
		[Display(Name="Window 3 End Minute", Order=15, GroupName="Trading Windows")]
		public int TradingWindow3EndMinute { get; set; }
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

		#region HUD Master Toggles
		[NinjaScriptProperty]
		[Display(Name="Stop-Limit Enabled", Order=1, GroupName="HUD Master Toggles", Description="Pending StopMarket entries become StopLimit when ON.")]
		public bool StopLimitEnabled { get; set; }

		[NinjaScriptProperty]
		[Display(Name="EMA Protect Enabled", Order=2, GroupName="HUD Master Toggles", Description="Enforce EMA Place rules for entries when ON.")]
		public bool EmaProtectEnabled { get; set; }
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
