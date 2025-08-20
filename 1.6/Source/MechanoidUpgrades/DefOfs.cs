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

namespace MU
{
	[DefOf]
	public static class MUMiscDefOf
	{
		[MayRequireAttribute("GoGaTio.MechanoidUpgrades.Archotech")]
		public static HediffDef MU_Perplex;

		public static ConceptDef MU_MechanoidUpgrades;

		public static GenStepDef MU_AncientFacility;

		public static LayoutDef MU_AncientUpgradesStockpile;

		public static LayoutDef MU_AncientComplex_Upgrades_Loot;

		public static SitePartDef MU_AncientComplex_Upgrades;

		public static ThingSetMakerDef MU_Reward_AncientFacility;

		public static PrefabDef MU_AncientMechUpgrader;

		public static PrefabDef MU_AncientFacilityEntrance;
	}

	[DefOf]
	public static class MUJobDefOf
	{
		public static JobDef MU_ConfigureUpgrades;

		public static JobDef MU_CarryUpgradeToStorage;

		public static JobDef MU_ReloadUpgrade;
	}

	[DefOf]
	public static class MUThingDefOf
	{
		public static ThingDef MU_MechUpgraderGlow_South;

		public static ThingDef MU_MechUpgraderGlow_North;

		public static ThingDef MU_MechUpgraderGlow_East;

		public static ThingDef MU_MechUpgraderGlow_West;

		public static ThingDef MU_AncientFacilityExit;
	}

	[DefOf]
	public static class MUStatDefOf
	{
		public static StatDef MU_Upgradability;

		public static StatDef MU_CarriedMassOffset;

		public static StatCategoryDef MU_MechUpgrades;

		public static StatCategoryDef MU_MechUpgrade_Offsets;

		public static StatCategoryDef MU_MechUpgrade_Factors;
	}
}
