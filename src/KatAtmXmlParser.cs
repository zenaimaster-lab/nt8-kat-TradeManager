/* KatAtmXmlParser.cs - ATM XML parser v1.42 (2026-08-08) */
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
		public int Quantity { get; set; }
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
				doc.XmlResolver = null;
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
				doc.XmlResolver = null;
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
			if (entryQtyNode != null && int.TryParse(entryQtyNode.InnerText, out int entryQty) && entryQty > 0)
			{
				result.Quantity = entryQty;
			}
			else
			{
				XmlNodeList qtyNodes = doc.SelectNodes("//AtmStrategy/Brackets/Bracket/Quantity");
				if (qtyNodes != null)
				{
					long total = 0;
					foreach (XmlNode qtyNode in qtyNodes)
					{
						if (qtyNode != null && int.TryParse(qtyNode.InnerText, out int qty) && qty > 0)
							total = Math.Min(int.MaxValue, total + qty);
					}
					result.Quantity = (int)total;
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
