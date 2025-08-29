using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using UnityEngine;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Jobs;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MU
{
	public class MechUpgradeDef : Def
	{
		public Type upgradeClass = typeof(MechUpgrade); 
		
		public List<StatModifier> statOffsets = new List<StatModifier>();

		public List<StatModifier> statFactors = new List<StatModifier>();

        public List<ThingDefCountClass> yieldList = null;

        public AbilityDef ability;

		public bool notGenerateHyperlink;

		public float marketValueFactor = 0.2f;

		public float? fixedMarketValue = null;

		[Unsaved(false)]
		public Texture2D uiIcon;

		[NoTranslate]
		public string uiIconPath;

		public List<string> exclusionTags = new List<string>();

		public float commonality = 1;

		public int upgradePoints = 1;

		public ThingDef linkedThingDef;

		public float minBodySize = 0f;

		public float maxBodySize = 0f;

		public List<MechWeightClassDef> weightClasses;

		public List<ThingDef> mechanoidDefs;

		public List<ThingDef> extraMechanoidDefs;

		public bool listIsWhitelist = false;

		public bool isForFight;

		public List<MU.UpgradeCompProperties> comps = new List<MU.UpgradeCompProperties>();

		public List<MU.UpgradeRestriction> restrictions = new List<MU.UpgradeRestriction>();

        public List<DamageFactor> damageFactors = new List<DamageFactor>();

        public float? commandRange;

		public bool allowRemoteControl;

        public float CommonalityFinal(Pawn mech)
        {
			float num = commonality;
			if(restrictions != null && !mech.Dead)
            {
				foreach (UpgradeRestriction r in restrictions)
				{
					num *= r.CommonalityFactor(mech);
				}
			}
			return num;
        }

        public override void ResolveReferences()
        {
            base.ResolveReferences();
			LongEventHandler.ExecuteWhenFinished(delegate
			{
				if (notGenerateHyperlink)
				{
					return;
				}
				if (descriptionHyperlinks == null)
				{
					descriptionHyperlinks = new List<DefHyperlink>();
				}
				descriptionHyperlinks.Insert(0, linkedThingDef);
				if (ability != null)
				{
					descriptionHyperlinks.Add(ability);
				}
			});
		}

		public static void AdjustCommonality(MechUpgradeDef def)
		{
			if(def.defName.ElementAt(def.defName.Length - 2) == '_')
            {
				switch (def.defName.Last())
				{
					case 'S':
						def.commonality *= 0.3f;
						break;
					case 'A':
						def.commonality *= 1.2f;
						break;
					case 'B':
						def.commonality *= 1.7f;
						break;
					case 'C':
						def.commonality *= 0.8f;
						break;
					default:
						break;
				}
			}
		}

		public override void PostLoad()
		{
			if (!comps.NullOrEmpty() && !typeof(MechUpgradeWithComps).IsAssignableFrom(upgradeClass))
			{
				upgradeClass = typeof(MechUpgradeWithComps);
			}
			AdjustCommonality(this);
			if (!string.IsNullOrEmpty(uiIconPath))
			{
				LongEventHandler.ExecuteWhenFinished(delegate
				{
                    try
                    {
						uiIcon = ContentFinder<Texture2D>.Get(uiIconPath);
					}
                    finally
                    {
						if(uiIcon == null)
                        {
							uiIcon = BaseContent.BadTex;
						}
                    }
					
				});
			}
            else
            {
				uiIcon = BaseContent.BadTex;
			}
		}

		public virtual AcceptanceReport CanAdd(ThingDef t, List<MU.MechUpgrade> list = null)
		{
            if (!extraMechanoidDefs.NullOrEmpty() && extraMechanoidDefs.Contains(t))
            {
				return true;
            }
			if (!mechanoidDefs.NullOrEmpty() && listIsWhitelist && !mechanoidDefs.Contains(t))
			{
				return "MU_CannotAdd_WrongMech".Translate();
			}
			if (!mechanoidDefs.NullOrEmpty() && !listIsWhitelist && mechanoidDefs.Contains(t))
			{
				return "MU_CannotAdd_WrongMech".Translate();
			}
			if (!weightClasses.NullOrEmpty() && !weightClasses.Contains(t.race.mechWeightClass))
			{
				return "MU_CannotAdd_WrongMechClass".Translate();
			}
			if (maxBodySize > 0f && maxBodySize <= t.race.baseBodySize)
			{
				return "MU_CannotAdd_BiggerSize".Translate();
			}
			if (minBodySize > t.race.baseBodySize)
			{
				return "MU_CannotAdd_SmallerSize".Translate();
			}
			if (!list.NullOrEmpty())
			{
				foreach (MU.MechUpgrade u in list)
				{
					if (u.def == this)
					{
						return "MU_CannotAdd_SameUpgrade".Translate();
					}
					if (!exclusionTags.NullOrEmpty() && !u.def.exclusionTags.NullOrEmpty() && exclusionTags.Any((string x) => u.def.exclusionTags.Contains(x)))
					{
						return "MU_CannotAdd_SameTags".Translate(u.def.label);
					}
				}
			}
			if (!restrictions.NullOrEmpty())
			{
				foreach (MU.UpgradeRestriction r1 in restrictions)
				{
					AcceptanceReport r2 = r1.CanAdd(t, this);
					if (!r2)
					{
						return r2.Reason ?? "MU_CannotAdd_WrongMech".Translate();
					}
				}
			}
			return true;
		}

		public override IEnumerable<string> ConfigErrors()
		{
			if (!typeof(MechUpgrade).IsAssignableFrom(upgradeClass))
			{
				yield return $"MechUpgradeDef {defName} has invalid upgradeClass, it should be MechUpgrade";
			}
			if (commonality < 0f)
			{
				yield return $"MechUpgradeDef {defName} has a commonality < 0.";
			}
			if (linkedThingDef == null)
			{
				yield return $"MechUpgradeDef {defName} has no linked ThingDef.";
			}
			if(maxBodySize != 0f && maxBodySize < minBodySize)
            {
				yield return $"MechUpgradeDef {defName} has maxBodySize smaller than minBodySize.";
			}
			if (maxBodySize != 0f && maxBodySize == minBodySize)
			{
				yield return $"MechUpgradeDef {defName} has same maxBodySize and minBodySize.";
			}
            if (!comps.NullOrEmpty())
            {
				foreach (UpgradeCompProperties c in comps)
				{
					foreach (string item in c.ConfigErrors(this))
					{
						yield return item;
					}
				}
			}
		}

		public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
		{
			foreach (StatDrawEntry item in base.SpecialDisplayStats(req))
			{
				yield return item;
            }
			foreach (StatDrawEntry item2 in UpgradeDisplayStats(req))
			{
				yield return item2;
			}
		}

		public virtual IEnumerable<StatDrawEntry> UpgradeDisplayStats(StatRequest req)
        {
			if (!comps.NullOrEmpty())
			{
				foreach (UpgradeCompProperties c in comps)
				{
					foreach (StatDrawEntry item in c.SpecialDisplayStats(req))
					{
						yield return item;
					}
				}
			}
			if (!restrictions.NullOrEmpty())
			{
				foreach (MU.UpgradeRestriction r in restrictions)
				{
					foreach (StatDrawEntry item in r.SpecialDisplayStats(req))
					{
						yield return item;
					}
				}
			}
			if (commandRange != null)
			{
				yield return new StatDrawEntry(MUStatDefOf.MU_MechUpgrades, "MU_CommandRange".Translate(), commandRange.Value.ToStringByStyle(ToStringStyle.Integer), "MU_CommandRange_Desc".Translate(), 8000);
			}
            if(allowRemoteControl)
            {
				yield return new StatDrawEntry(MUStatDefOf.MU_MechUpgrades, "MU_AllowsRemoteControl".Translate(), "Yes".Translate(), "MU_AllowsRemoteControl".Translate(), 8900);
			}
			yield return new StatDrawEntry(MUStatDefOf.MU_MechUpgrades, "MU_UpgradePoints".Translate(), upgradePoints.ToString(), "MU_UpgradePoints_Desc".Translate(), 9999);
			if (minBodySize > 0f)
			{
				yield return new StatDrawEntry(MUStatDefOf.MU_MechUpgrades, "MU_MinBodySize".Translate(), minBodySize.ToStringByStyle(ToStringStyle.FloatOne), "MU_MinBodySize_Desc".Translate(), 5);
			}
			if (maxBodySize > 0f)
			{
				yield return new StatDrawEntry(MUStatDefOf.MU_MechUpgrades, "MU_MaxBodySize".Translate(), maxBodySize.ToStringByStyle(ToStringStyle.FloatOne), "MU_MaxBodySize_Desc".Translate(), 4);
			}
			StringBuilder sb = new StringBuilder("");
			List<Dialog_InfoCard.Hyperlink> tmpHyperlinks = new List<Dialog_InfoCard.Hyperlink>();
			foreach (ThingDef t1 in DefDatabase<ThingDef>.AllDefs.Where((ThingDef t2) => t2.race != null && t2.race.IsMechanoid && t2.comps.Any((CompProperties c) => c is MU.CompProperties_UpgradableMechanoid)))
			{
				if (CanAdd(t1))
				{
					sb.AppendLine(t1.LabelCap);
					tmpHyperlinks.Add(new Dialog_InfoCard.Hyperlink(t1));
				}
			}
			if(sb.Length > 2)
            {
				sb.Remove(sb.Length - 2, 2);
			}
            else
            {
				sb.AppendLine("No".Translate());
            }
			yield return new StatDrawEntry(MUStatDefOf.MU_MechUpgrades, "MU_MechanoidsToAdd".Translate(), sb.ToString(), "MU_MechanoidsToAdd_Desc".Translate(), 1, null, tmpHyperlinks);
			if (!statOffsets.NullOrEmpty())
			{
				for (int k = 0; k < statOffsets.Count; k++)
				{
					StatDef stat = statOffsets[k].stat;
					float num2 = statOffsets[k].value;
					StringBuilder stringBuilder3 = new StringBuilder(stat.description);
					if (req.HasThing && stat.Worker != null)
					{
						stringBuilder3.AppendLine();
						stringBuilder3.AppendLine();
						stringBuilder3.AppendLine("StatsReport_BaseValue".Translate() + ": " + stat.ValueToString(num2, ToStringNumberSense.Offset, stat.finalizeEquippedStatOffset));
						num2 = statOffsets.GetStatOffsetFromList(stat);
						if (!stat.parts.NullOrEmpty())
						{
							stringBuilder3.AppendLine();
							for (int m = 0; m < stat.parts.Count; m++)
							{
								string text = stat.parts[m].ExplanationPart(req);
								if (!text.NullOrEmpty())
								{
									stringBuilder3.AppendLine(text);
								}
							}
						}
						stringBuilder3.AppendLine();
						stringBuilder3.AppendLine("StatsReport_FinalValue".Translate() + ": " + stat.ValueToString(num2, ToStringNumberSense.Offset, !stat.formatString.NullOrEmpty()));
					}
					yield return new StatDrawEntry(MUStatDefOf.MU_MechUpgrade_Offsets, statOffsets[k].stat, num2, StatRequest.ForEmpty(), ToStringNumberSense.Offset, null, forceUnfinalizedMode: true).SetReportText(stringBuilder3.ToString());
				}
			}
			if (!statFactors.NullOrEmpty())
			{
				for (int k = 0; k < statFactors.Count; k++)
				{
					StatDef stat = statFactors[k].stat;
					float num2 = statFactors[k].value;
					StringBuilder stringBuilder4 = new StringBuilder(stat.description);
					if (req.HasThing && stat.Worker != null)
					{
						stringBuilder4.AppendLine();
						stringBuilder4.AppendLine();
						stringBuilder4.AppendLine("StatsReport_BaseValue".Translate() + ": " + stat.ValueToString(num2, ToStringNumberSense.Factor, false));
						num2 = statFactors.GetStatFactorFromList(stat);
						if (!stat.parts.NullOrEmpty())
						{
							stringBuilder4.AppendLine();
							for (int m = 0; m < stat.parts.Count; m++)
							{
								string text = stat.parts[m].ExplanationPart(req);
								if (!text.NullOrEmpty())
								{
									stringBuilder4.AppendLine(text);
								}
							}
						}
						stringBuilder4.AppendLine();
						stringBuilder4.AppendLine("StatsReport_FinalValue".Translate() + ": " + stat.ValueToString(num2, ToStringNumberSense.Offset, !stat.formatString.NullOrEmpty()));
					}
					yield return new StatDrawEntry(MUStatDefOf.MU_MechUpgrade_Factors, statFactors[k].stat, num2, StatRequest.ForEmpty(), ToStringNumberSense.Factor, null, forceUnfinalizedMode: true).SetReportText(stringBuilder4.ToString());
				}
			}
			if (ability != null)
			{
				yield return new StatDrawEntry(MUStatDefOf.MU_MechUpgrades, "GivesAbility".Translate(), ability.LabelCap, "GivesAbility".Translate(), 2100, null, new List<Dialog_InfoCard.Hyperlink>() { new Dialog_InfoCard.Hyperlink(ability) });
				if (ability.comps.Any((AbilityCompProperties c) => c is MU.CompProperties_AbilityReloadableUpgrade))
				{
					MU.CompProperties_AbilityReloadableUpgrade comp = ability.comps.First((AbilityCompProperties c) => c is MU.CompProperties_AbilityReloadableUpgrade) as MU.CompProperties_AbilityReloadableUpgrade;
					yield return new StatDrawEntry(MUStatDefOf.MU_MechUpgrades, "MU_Stat_ReloadCost".Translate(), $"{comp.ammoCount} {comp.ammoDef.label}", "MU_Stat_ReloadCost_Desc".Translate(), 2000);
				}//Stat_Thing_ReloadRefill_Name
			}
		}
	}
}