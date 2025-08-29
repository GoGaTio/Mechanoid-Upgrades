using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Xml.Xsl;
using DelaunatorSharp;
using Gilzoide.ManagedJobs;
using Ionic.Crc;
using Ionic.Zlib;
using JetBrains.Annotations;
using KTrie;
using LudeonTK;
using NVorbis.NAudioSupport;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using RuntimeAudioClipLoader;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using HarmonyLib;

namespace MU
{
	public class MechanoidUpgradesPatch : Mod
	{
		public MechanoidUpgradesPatch(ModContentPack content)
			: base(content)
		{
			Harmony harmonyInstance = new Harmony("MechanoidUpgradesPatch");
			harmonyInstance.PatchAllUncategorized();
		}
	}

    /*[HarmonyPatch(typeof(ParseHelper))]
    [HarmonyPatch(new Type[] { })]
    public class Patch_XMLLoad
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            ParseHelper.Parsers<StatModifier>.Register(ParsePlanetTile);
        }

        public static StatModifier FromString(string s)
        {
            StatModifier m = new StatModifier();
            m.value = ParseHelper.FromString<float>(xmlRoot.FirstChild.Value);
			return m;
        }
    }*/

    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawn", new Type[] { typeof(PawnGenerationRequest)})]
	public class Patch_GenerateUpgradedPawn
	{
		public static float CombinationsChance(Pawn mech)
        {
			return MechUpgradeUtility.Settings.combinationsCommonality;
		}

		public static float UpgradesChance(Pawn mech)
		{
			return MechUpgradeUtility.upgradeChanceOverride ?? MechUpgradeUtility.Settings.chanceForUpgrades;
		}

		private static List<UpgradeCombinationDef> combinations = null;

		public static List<UpgradeCombinationDef> Combinations
        {
            get
            {
				if (combinations == null)
				{
					combinations = DefDatabase<UpgradeCombinationDef>.AllDefsListForReading;
				}
				return combinations;
			}
        }

		[HarmonyPostfix]
		public static void Postfix(PawnGenerationRequest request, ref Pawn __result)
		{
			if (__result == null || !MechUpgradeUtility.Settings.generateWithUpgrades || !__result.RaceProps.IsMechanoid || request.AllowedDevelopmentalStages == DevelopmentalStage.Newborn)
			{
				return;
			}
			CompUpgradableMechanoid comp = __result.TryGetComp<CompUpgradableMechanoid>();
			if (comp == null || !Rand.Chance(UpgradesChance(__result)))
			{
				return;
			}
            if (Rand.Chance(CombinationsChance(__result)))
            {
				ThingDef td = __result.def;
				List<UpgradeCombinationDef> list = Combinations.Where((UpgradeCombinationDef d) => d.commonality > 0f && d.mechs.Contains(td)).ToList();
                if (!list.NullOrEmpty())
                {
					UpgradeCombinationDef def = list.RandomElementByWeight((UpgradeCombinationDef d2) => d2.commonality);
					MechUpgradeUtility.ApplyCombination(__result, def);
					return;
				}
			}
			MechUpgradeUtility.UpgradeMech(__result, MechUpgradeUtility.upgradeChanceOverride ?? new FloatRange(MechUpgradeUtility.Settings.minUpgradePercent, MechUpgradeUtility.Settings.maxUpgradePercent).RandomInRange);
		}
	}

	[HarmonyPatch(typeof(MechanitorUtility), nameof(MechanitorUtility.InMechanitorCommandRange))]
	public class Patch_CommandRange
	{

		[HarmonyPostfix]
		public static void Postfix(Pawn mech, LocalTargetInfo target, ref bool __result)
		{
			
			if (__result)
			{
				return;
			}
			CompUpgradableMechanoid comp = mech.TryGetComp<MU.CompUpgradableMechanoid>();
			if (comp != null && comp.RemoteControllable)
			{
				__result = true;
				return;
			}
			Pawn overseer = mech.GetOverseer();
			if(overseer == null)
            {
				return;
            }
			List<Pawn> overseenPawns = overseer.mechanitor.OverseenPawns;
			for (int i = 0; i < overseenPawns.Count; i++)
			{
				comp = overseenPawns[i].TryGetComp<MU.CompUpgradableMechanoid>();
				if (comp != null && comp.CommandDistance != null && overseenPawns[i].IsColonyMechPlayerControlled)
				{
					Map mapHeld = overseenPawns[i].MapHeld;
					GlobalTargetInfo globalTargetInfo = target.ToGlobalTargetInfo(Find.CurrentMap);
					if ((float)overseenPawns[i].Position.DistanceToSquared(target.Cell) < overseenPawns[i].TryGetComp<MU.CompUpgradableMechanoid>().CommandDistanceSquared.Value && globalTargetInfo.Map == mapHeld)
					{
						__result = true;
						break;
					}
				}
			}
		}
	}

	[HarmonyPatch(typeof(QualityUtility), "GenerateQualityCreatedByPawn", new Type[]{ typeof(Pawn), typeof(SkillDef), typeof(bool) })]
	public class Patch_QualityOffset
	{
		[HarmonyPostfix]
		public static void Postfix(Pawn pawn, SkillDef relevantSkill, ref QualityCategory __result)
		{
			CompUpgradableMechanoid comp = pawn.TryGetComp<CompUpgradableMechanoid>();
			if (comp == null || comp.upgrades.NullOrEmpty())
            {
				return;
            }
			foreach(MechUpgrade upgrade in comp.upgrades.Where((MechUpgrade u) => u is MechUpgradeWithComps w && w.GetComp<UpgradeComp_QualityOffset>() != null))
            {
				__result = (QualityCategory)Mathf.Min((int)__result + (upgrade as MechUpgradeWithComps).GetComp<UpgradeComp_QualityOffset>().Offset, 6);
			}
		}
	}

	[HarmonyPatch(typeof(ThingDefGenerator_Corpses), nameof(ThingDefGenerator_Corpses.ImpliedCorpseDefs))]
	public static class Patch_AddITabToCorpses
	{
		[HarmonyPostfix]
		public static IEnumerable<ThingDef> Postfix(IEnumerable<ThingDef> __result)
		{
			foreach (ThingDef thingDef in __result)
			{
				if (thingDef.ingestible?.sourceDef != null)
				{
					ThingDef def = thingDef.ingestible.sourceDef;
					if (def.GetCompProperties<CompProperties_UpgradableMechanoid>() != null || def.HasComp<CompUpgradableMechanoid>())
					{
						thingDef.inspectorTabs.Add(typeof(MU.ITab_MechUpgrades));
					}
				}
				yield return thingDef;
			}
		}
	}

	[HarmonyPatch(typeof(RecipeDefGenerator), nameof(RecipeDefGenerator.ImpliedRecipeDefs))]
	public static class Patch_AddCountWorker
	{
		[HarmonyPostfix]
		public static IEnumerable<RecipeDef> Postfix(IEnumerable<RecipeDef> __result)
		{
			foreach (RecipeDef recipeDef in __result)
			{
				if (recipeDef.ProducedThingDef?.GetCompProperties<CompProperties_MechUpgrade>() != null)
				{
					recipeDef.workerCounterClass = typeof(RecipeWorkerCounter_MakeUpgrade);
				}
				yield return recipeDef;
			}
		}
	}

	[HarmonyPatch(typeof(Pawn), nameof(Pawn.ButcherProducts))]
	public static class Patch_ExtraButcherProducts
	{
		public static Thing ThingFromUpgrade(MechUpgrade upgrade, Pawn mech, float efficiency)
        {
			if (!upgrade.def.linkedThingDef.costList.NullOrEmpty())
			{
				int num1 = 0;
				List<ThingDefCount> list = new List<ThingDefCount>();
				foreach (ThingDefCountClass num in upgrade.def.linkedThingDef.costList)
				{
                    if (num.thingDef.smeltable)
                    {
						list.Add(new ThingDefCount(num.thingDef, num.count));
					}
					num1 += num.count;
				}
				if(Rand.ChanceSeeded(MechUpgradeUtility.Settings.upgradesButcherRatio, num1))
                {
					ThingDefCount tdc1 = list.RandomElementByWeight((ThingDefCount tdc) => tdc.Count);
					Thing t = ThingMaker.MakeThing(tdc1.ThingDef);
					t.stackCount = Mathf.RoundToInt(efficiency * tdc1.Count * MechUpgradeUtility.Settings.upgradesButcherYield);
					return t;
				}
			}
			return null;
		}
		[HarmonyPostfix]
		public static IEnumerable<Thing> Postfix(IEnumerable<Thing> __result, Pawn butcher, float efficiency, Pawn __instance)
		{
			foreach (Thing thing in __result)
			{
				yield return thing;
			}
            if (!MechUpgradeUtility.Settings.extraButcherProducts)
            {
				yield break;
            }
			CompUpgradableMechanoid comp = __instance.TryGetComp<CompUpgradableMechanoid>();
			if (comp != null && !comp.upgrades.NullOrEmpty())
            {
				foreach (MechUpgrade u in comp.upgrades)
                {
					Thing t = ThingFromUpgrade(u, __instance, efficiency);
					if (t != null)
                    {
						yield return t;
                    }
				}
            }
			
		}
	}

	[HarmonyPatch(typeof(MassUtility), nameof(MassUtility.Capacity))]
	public class Patch_CarriedMassOffset
	{

		[HarmonyPostfix]
		public static void Postfix(Pawn p, StringBuilder explanation, ref float __result)
		{
			__result = __result + p.GetStatValue(MUStatDefOf.MU_CarriedMassOffset);
		}
	}

	[HarmonyPatch(typeof(ScenPart_StartingMech), nameof(ScenPart_StartingMech.PlayerStartingThings))]
	public static class Patch_UpgradesForStartingMechs
	{

		private static List<ScenPart_Upgrades> list;

		public static List<ScenPart_Upgrades> List
        {
            get
            {
                if (list == null)
                {
					list = new List<ScenPart_Upgrades>();
					foreach (ScenPart allPart in Find.Scenario.AllParts.Where((ScenPart sp) => sp is ScenPart_Upgrades))
					{
						list.Add(allPart as ScenPart_Upgrades);
					}
				}
				return list;
            }
        }
		[HarmonyPostfix]
		public static IEnumerable<Thing> Postfix(IEnumerable<Thing> __result)
		{
			foreach (Thing thing in __result)
			{
				CompUpgradableMechanoid comp = thing.TryGetComp<CompUpgradableMechanoid>();
				if (!List.NullOrEmpty() && comp != null && thing is Pawn mech)
                {
					foreach(ScenPart_Upgrades part in List)
                    {
                        if (part.mechs.Contains(mech.kindDef))
                        {
							foreach (MechUpgradeDef def in part.upgrades)
                            {
								MechUpgrade u = MechUpgradeUtility.MakeUpgrade(def, mech);
								comp.upgrades.Add(u);
								u.OnAdded(mech);
							}
						}
                    }
				}
				yield return thing;
			}
			list = null;
		}
	}

	[HarmonyPatch(typeof(TradeUtility), nameof(TradeUtility.AllLaunchableThingsForTrade))]
	public static class Patch_AllLaunchableThingsForTrad
	{
		[HarmonyPostfix]
		public static IEnumerable<Thing> Postfix(IEnumerable<Thing> __result, Map map, ITrader trader)
		{
			HashSet<Thing> yieldedThings = new HashSet<Thing>(__result);
			foreach (Thing thing in __result)
			{
				yield return thing;
			}
			foreach (Building_OrbitalTradeBeacon item in Building_OrbitalTradeBeacon.AllPowered(map))
			{
				foreach (IntVec3 tradeableCell in item.TradeableCells)
				{
					List<Thing> thingList = tradeableCell.GetThingList(map);
					for (int i = 0; i < thingList.Count; i++)
					{
						Thing t = thingList[i];
						CompUpgradesStorage compUpgradesStorage = t.TryGetComp<CompUpgradesStorage>();
						if (compUpgradesStorage == null)
						{
							continue;
						}
						List<Thing> containedUpgrades = compUpgradesStorage.UpgradesAsThings;
						foreach (Thing item2 in containedUpgrades)
						{
							if (TradeUtility.PlayerSellableNow(item2, trader) && !yieldedThings.Contains(item2))
							{
								yieldedThings.Add(item2);
								yield return item2;
							}
						}
					}
				}
			}
		}
	}

	[HarmonyPatch(typeof(Pawn_TraderTracker), nameof(Pawn_TraderTracker.ColonyThingsWillingToBuy))]
	public static class Patch_ColonyThingsWillingToBuy
	{
		[HarmonyPostfix]
		public static IEnumerable<Thing> Postfix(IEnumerable<Thing> __result, Pawn playerNegotiator, Pawn ___pawn)
		{
			foreach (Thing thing in __result)
			{
				yield return thing;
			}
			List<Building> list = ___pawn.Map.listerBuildings.allBuildingsColonist.ToList();
			foreach (Building item2 in list)
			{
				CompUpgradesStorage compUpgradesStorage = item2.TryGetComp<CompUpgradesStorage>();
				if (compUpgradesStorage == null)
				{
					continue;
				}
				List<Thing> containedUpgrades = compUpgradesStorage.UpgradesAsThings;
				foreach (Thing item3 in containedUpgrades)
				{
					yield return item3;
				}
			}
		}
	}

	[HarmonyPatch(typeof(TradeDeal), "InSellablePosition")]
	public class Patch_InSellablePosition
	{

		[HarmonyPostfix]
		public static void Postfix(Thing t, ref string reason, ref bool __result)
		{
			if (__result) return;
			if(!t.Spawned && t.ParentHolder is CompUpgradesStorage)
            {
				__result = true;
				reason = null;
            }
		}
	}

    [HarmonyPatch(typeof(BillUtility), nameof(BillUtility.MakeNewBill))]
    public class Patch_Bill
    {
        [HarmonyPostfix]
        public static void Postfix(RecipeDef recipe, Precept_ThingStyle precept, ref Bill __result)
        {
            if (recipe is ImproveRecipeDef)
            {
                __result = new Bill_Improve(recipe, precept);
            }
        }
    }
}