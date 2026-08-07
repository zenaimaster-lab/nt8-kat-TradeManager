using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatScaleOutTests
	{
		[Fact]
		public void PlanAtmBracketMerge_ScaleOut_ReducesToLiveQuantity()
		{
			// Position was 4, scaled out 3 -> live 1, brackets still at 4
			var orders = new[]
			{
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "a", IsStop = true, Quantity = 4, Price = 100.0 },
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "a", IsStop = false, Quantity = 4, Price = 110.0 },
			};
			var plan = KatTradeCalculator.PlanAtmBracketMerge(orders, 1);
			Assert.Equal(1, plan.DesiredStopQuantity);
			Assert.Equal(1, plan.DesiredTargetQuantity);
			Assert.Equal(new[] { 0, 1 }, plan.ChangeIndices);
			Assert.Empty(plan.CancelIndices);
			Assert.False(plan.IsNoop);
		}

		[Fact]
		public void PlanAtmBracketMerge_ScaleOut_MultipleBrackets_ReducesAndCancels()
		{
			// 3 brackets qty 2 each, live scaled out from 6 to 2
			var orders = new[]
			{
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "a", IsStop = true, Quantity = 2, Price = 100.0 },
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "a", IsStop = false, Quantity = 2, Price = 110.0 },
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "b", IsStop = true, Quantity = 2, Price = 100.0 },
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "b", IsStop = false, Quantity = 2, Price = 110.0 },
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "c", IsStop = true, Quantity = 2, Price = 100.0 },
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "c", IsStop = false, Quantity = 2, Price = 110.0 },
			};
			var plan = KatTradeCalculator.PlanAtmBracketMerge(orders, 2);
			// Should keep one pair at 2, cancel other 4
			Assert.Equal(2, plan.DesiredStopQuantity);
			Assert.Equal(2, plan.DesiredTargetQuantity);
			Assert.Empty(plan.ChangeIndices); // already at 2, no change needed
			Assert.Equal(4, plan.CancelIndices.Length);
		}

		[Fact]
		public void PlanAtmBracketMerge_ScaleOut_LargeToSmall_Changes()
		{
			var orders = new[]
			{
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "a", IsStop = true, Quantity = 10, Price = 100.0 },
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "a", IsStop = false, Quantity = 10, Price = 110.0 },
			};
			var plan = KatTradeCalculator.PlanAtmBracketMerge(orders, 3);
			Assert.Equal(3, plan.DesiredStopQuantity);
			Assert.Equal(3, plan.DesiredTargetQuantity);
			Assert.Equal(2, plan.ChangeIndices.Length);
		}

		[Fact]
		public void PlanAtmBracketMerge_ScaleIn_StillWorks()
		{
			// Ensure scale-in still correct after fix: live 6, brackets 4 -> should increase to 6
			var orders = new[]
			{
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "a", IsStop = true, Quantity = 4, Price = 100.0 },
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "a", IsStop = false, Quantity = 4, Price = 110.0 },
			};
			var plan = KatTradeCalculator.PlanAtmBracketMerge(orders, 6);
			Assert.Equal(6, plan.DesiredStopQuantity);
			Assert.Equal(6, plan.DesiredTargetQuantity);
			Assert.Equal(2, plan.ChangeIndices.Length);
		}
	}
}
