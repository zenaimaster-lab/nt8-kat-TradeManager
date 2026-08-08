/* KatTradeManager.HudFactory.cs - HUD grid/card/button factory (partial class) v1.96 (2026-08-08) */
// ponytail: extracted from KatTradeManagerUI.cs 1739-1976 — 7 helpers + 2 templates. Was 1967L god, now ~1650L.
// Keeps UI.cs focused on wiring/handlers; factory is pure WPF layout, no business logic.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Indicators.KAT
{
	public partial class KatTradeManager
	{
		private Button CreateButton(string text, Brush bg, RoutedEventHandler handler, double height = 24, double fontSize = 10)
		{
			var tb = new TextBlock { Text = text, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap, Margin = new Thickness(0), Padding = new Thickness(0) };
			Button btn = new Button
			{
				Content = tb,
				Background = bg,
				Foreground = Brushes.White,
				FontWeight = FontWeights.Normal,
				FontSize = fontSize,
				Margin = new Thickness(0),
				Padding = new Thickness(2, 0, 2, 0),
				Height = height,
				BorderThickness = new Thickness(0),
				HorizontalContentAlignment = HorizontalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Center,
				Template = GetHudButtonTemplate()
			};
			if (handler != null)
				btn.Click += handler;
			return btn;
		}

		// Own the Button template: NT8 theme variants place ContentPresenter per their own bindings,
		// which shifted quick-set labels right on some installs. A fixed centered presenter guarantees
		// labels stay centered and unclipped regardless of the active theme.
		private static ControlTemplate _hudButtonTemplate;
		private static ControlTemplate GetHudButtonTemplate()
		{
			if (_hudButtonTemplate != null) return _hudButtonTemplate;
			var border = new FrameworkElementFactory(typeof(Border), "root");
			border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
			border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
			border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
			border.SetValue(Border.SnapsToDevicePixelsProperty, true);
			border.SetValue(Border.UseLayoutRoundingProperty, true);
			var cp = new FrameworkElementFactory(typeof(ContentPresenter));
			cp.SetBinding(ContentPresenter.HorizontalAlignmentProperty, new System.Windows.Data.Binding("HorizontalContentAlignment") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
			cp.SetBinding(ContentPresenter.VerticalAlignmentProperty, new System.Windows.Data.Binding("VerticalContentAlignment") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
			cp.SetValue(ContentPresenter.MarginProperty, new Thickness(2, 0, 2, 0));
			border.AppendChild(cp);
			_hudButtonTemplate = new ControlTemplate(typeof(Button)) { VisualTree = border };
			return _hudButtonTemplate;
		}

		private static ControlTemplate GetQuickSetButtonTemplate()
		{
			if (_quickSetButtonTemplate != null) return _quickSetButtonTemplate;
			var border = new FrameworkElementFactory(typeof(Border), "root");
			border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
			border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
			border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
			border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
			border.SetValue(Border.SnapsToDevicePixelsProperty, true);
			border.SetValue(Border.UseLayoutRoundingProperty, true);
			var tb = new FrameworkElementFactory(typeof(TextBlock), "label");
			tb.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Content") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
			tb.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
			tb.SetBinding(TextBlock.FontSizeProperty, new System.Windows.Data.Binding("FontSize") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
			tb.SetBinding(TextBlock.FontWeightProperty, new System.Windows.Data.Binding("FontWeight") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
			tb.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
			tb.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
			tb.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
			tb.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
			tb.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
			tb.SetValue(TextBlock.MarginProperty, new Thickness(1, 0, 1, 0));
			border.AppendChild(tb);
			_quickSetButtonTemplate = new ControlTemplate(typeof(Button)) { VisualTree = border };
			return _quickSetButtonTemplate;
		}

		// uniform HUD gap — all inter-column gaps and vertical row gaps = HudGap (2px)
		private Grid CreateTwoColumnGrid(double bottomMargin = 2, double centerGap = 2)
		{
			Grid grid = new Grid { Margin = new Thickness(0, 0, 0, bottomMargin), HorizontalAlignment = HorizontalAlignment.Stretch, UseLayoutRounding = true, SnapsToDevicePixels = true };
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(centerGap) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			return grid;
		}

		private Grid CreateFourColumnGrid(double bottomMargin = 2, double centerGap = 2, double subGap = 2)
		{
			Grid grid = new Grid { Margin = new Thickness(0, 0, 0, bottomMargin), HorizontalAlignment = HorizontalAlignment.Stretch, UseLayoutRounding = true, SnapsToDevicePixels = true };
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(subGap) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(centerGap) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(subGap) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			return grid;
		}

		private Grid CreateSixColumnGrid(double bottomMargin = 2, double centerGap = 2, double subGap = 2)
		{
			Grid grid = new Grid { Margin = new Thickness(0, 0, 0, bottomMargin), HorizontalAlignment = HorizontalAlignment.Stretch, UseLayoutRounding = true, SnapsToDevicePixels = true };
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(subGap) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(subGap) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(centerGap) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(subGap) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(subGap) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			return grid;
		}

		private Grid CreateEightColumnGrid(double bottomMargin = 2, double centerGap = 2, double subGap = 2)
		{
			Grid grid = new Grid { Margin = new Thickness(0, 0, 0, bottomMargin), HorizontalAlignment = HorizontalAlignment.Stretch, UseLayoutRounding = true, SnapsToDevicePixels = true };
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(subGap) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(subGap) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(subGap) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(centerGap) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(subGap) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(subGap) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(subGap) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			return grid;
		}

		private Border CreateSectionCard(FrameworkElement child, double bottomMargin = 2)
		{
			// black footer pad at bottom of every section — empty decorative strip (no text), pure black contrast vs card bg 10,12,18
			var contentHost = new Border
			{
				Padding = new Thickness(HudGap),
				Background = Brushes.Transparent,
				Child = child,
				UseLayoutRounding = true,
				SnapsToDevicePixels = true
			};
			var footer = new Border
			{
				Height = 10,
				Background = new SolidColorBrush(Color.FromRgb(0, 0, 0)),
				CornerRadius = new CornerRadius(0, 0, 4, 4),
				UseLayoutRounding = true,
				SnapsToDevicePixels = true
			};
			var inner = new Grid { UseLayoutRounding = true, SnapsToDevicePixels = true };
			inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
			Grid.SetRow(contentHost, 0);
			Grid.SetRow(footer, 1);
			inner.Children.Add(contentHost);
			inner.Children.Add(footer);
			return new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(10, 12, 18)),
				BorderBrush = new SolidColorBrush(Color.FromRgb(35, 42, 56)),
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(5),
				Margin = new Thickness(0, 0, 0, bottomMargin),
				Child = inner,
				UseLayoutRounding = true,
				SnapsToDevicePixels = true
			};
		}
	}
}
