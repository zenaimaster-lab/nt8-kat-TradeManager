using System;
using System.IO;
using System.Xml;

namespace NinjaTrader.NinjaScript.Indicators
{
	public class AtmTemplateData
	{
		public int StopLoss { get; set; }
		public int Target { get; set; }
		public int BETrigger { get; set; }
		public int SL1Trigger { get; set; }
		public int SL2Trigger { get; set; }
		public int Quantity { get; set; } = 1;
	}

	public static class KatAtmXmlParser
	{
		public static AtmTemplateData ParseXml(string xmlContent)
		{
			AtmTemplateData result = new AtmTemplateData();
			if (string.IsNullOrWhiteSpace(xmlContent)) return result;

			try
			{
				XmlDocument doc = new XmlDocument();
				doc.LoadXml(xmlContent);
				return ParseXmlDocument(doc);
			}
			catch
			{
				return result;
			}
		}

		public static AtmTemplateData ParseFile(string filePath)
		{
			AtmTemplateData result = new AtmTemplateData();
			if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return result;

			try
			{
				XmlDocument doc = new XmlDocument();
				doc.Load(filePath);
				return ParseXmlDocument(doc);
			}
			catch
			{
				return result;
			}
		}

		public static AtmTemplateData ParseXmlDocument(XmlDocument doc)
		{
			AtmTemplateData result = new AtmTemplateData();
			if (doc == null) return result;

			XmlNode slNode = doc.SelectSingleNode("//AtmStrategy/Brackets/Bracket/StopLoss");
			if (slNode != null && int.TryParse(slNode.InnerText, out int sl)) result.StopLoss = sl;

			XmlNode targetNode = doc.SelectSingleNode("//AtmStrategy/Brackets/Bracket/Target");
			if (targetNode != null && int.TryParse(targetNode.InnerText, out int tp)) result.Target = tp;

			XmlNode entryQtyNode = doc.SelectSingleNode("//AtmStrategy/EntryQuantity");
			if (entryQtyNode != null && int.TryParse(entryQtyNode.InnerText, out int eq) && eq > 0)
			{
				result.Quantity = eq;
			}
			else
			{
				XmlNodeList qtyNodes = doc.SelectNodes("//AtmStrategy/Brackets/Bracket/Quantity");
				if (qtyNodes != null && qtyNodes.Count > 0)
				{
					int sum = 0;
					foreach (XmlNode qn in qtyNodes)
					{
						if (int.TryParse(qn.InnerText, out int val)) sum += val;
					}
					if (sum > 0) result.Quantity = sum;
				}
			}

			XmlNode beNode = doc.SelectSingleNode("//AtmStrategy/Brackets/Bracket/StopStrategy/AutoBreakEvenProfitTrigger");
			if (beNode != null && int.TryParse(beNode.InnerText, out int be)) result.BETrigger = be;

			XmlNodeList trailSteps = doc.SelectNodes("//AtmStrategy/Brackets/Bracket/StopStrategy/AutoTrailSteps/AutoTrailStep");
			if (trailSteps != null)
			{
				if (trailSteps.Count > 0)
				{
					XmlNode st1 = trailSteps[0].SelectSingleNode("ProfitTrigger");
					if (st1 != null && int.TryParse(st1.InnerText, out int s1)) result.SL1Trigger = s1;
				}
				if (trailSteps.Count > 1)
				{
					XmlNode st2 = trailSteps[1].SelectSingleNode("ProfitTrigger");
					if (st2 != null && int.TryParse(st2.InnerText, out int s2)) result.SL2Trigger = s2;
				}
			}

			return result;
		}
	}
}
