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
	public abstract class UpgradeRestriction
	{
		public bool invert = false;

		public virtual AcceptanceReport CanAdd(ThingDef t, MechUpgradeDef u)
		{
			return !invert;
		}

		public virtual float CommonalityFactor(Pawn mech)
        {
			return 1f;
        }

		public virtual IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
		{
			return Enumerable.Empty<StatDrawEntry>();
		}
	}

	public class UpgradeRestriction_FactorFromBodySize : UpgradeRestriction
	{
		public SimpleCurve curve;

        public override float CommonalityFactor(Pawn mech)
        {
			return curve.Evaluate(mech.BodySize);
        }
    }

	public class UpgradeRestriction_HaveBodyParts : UpgradeRestriction
	{
		public BodyPartTagDef tag;
		public override AcceptanceReport CanAdd(ThingDef t, MechUpgradeDef u)
		{
			bool flag = t.race.body.GetPartsWithTag(tag).NullOrEmpty();
			if (flag && invert == false)
			{
				return "MU_CannotAdd_NoRequiredBodyPart".Translate();
			}
			if (!flag && invert == true)
			{
				return "MU_CannotAdd_HaveConflictingBodyPart".Translate();
			}
			return true;
		}
	}

	public class UpgradeRestriction_HaveBodyParts_Amount : UpgradeRestriction
	{
		public BodyPartTagDef tag;

		public int amount;
		public override AcceptanceReport CanAdd(ThingDef t, MechUpgradeDef u)
		{
			bool flag = !t.race.body.GetPartsWithTag(tag).NullOrEmpty();
			if (flag && t.race.body.GetPartsWithTag(tag).Count >= amount)
			{
				flag = true;
			}
			else
			{
				flag = false;
			}
			if (!flag && invert == false)
			{
				return "MU_CannotAdd_NoRequiredBodyPart".Translate();
			}
			if (flag && invert == true)
			{
				return "MU_CannotAdd_HaveConflictingBodyPart".Translate();
			}
			return true;
		}
	}

	public class UpgradeRestriction_WorkType : UpgradeRestriction
	{
		public WorkTypeDef workType;

		public override AcceptanceReport CanAdd(ThingDef t, MechUpgradeDef u)
		{
			bool flag = t.race.mechEnabledWorkTypes.Contains(workType);
			if (!flag && invert == false)
			{
				return "MU_CannotAdd_NoRequiredWorkType".Translate();
			}
			if (flag && invert == true)
			{
				return "MU_CannotAdd_HaveConflictingWorkType".Translate();
			}
			return true;
		}
	}

	public class UpgradeRestriction_WorkTypeAny : UpgradeRestriction
	{
		public List<WorkTypeDef> workTypes;

		public override AcceptanceReport CanAdd(ThingDef t, MechUpgradeDef u)
		{
			bool flag = t.race.mechEnabledWorkTypes.ContainsAny((WorkTypeDef w) => workTypes.Contains(w));
			if (!flag && invert == false)
			{
				return "MU_CannotAdd_NoRequiredWorkType".Translate();
			}
			if (flag && invert == true)
			{
				return "MU_CannotAdd_HaveConflictingWorkType".Translate();
			}
			return true;
		}
	}

	public class UpgradeRestriction_IsWorker : UpgradeRestriction
	{
		public override AcceptanceReport CanAdd(ThingDef t, MechUpgradeDef u)
		{
			bool flag = t.race.mechEnabledWorkTypes.NullOrEmpty();
			if (flag && invert == false)
			{
				return "MU_CannotAdd_IsNotWorker".Translate();
			}
			if (!flag && invert == true)
			{
				return "MU_CannotAdd_IsWorker".Translate();
			}
			return true;
		}
	}

	public class UpgradeRestriction_IsRangedFighter : UpgradeRestriction
	{
		public override AcceptanceReport CanAdd(ThingDef t, MechUpgradeDef u)
		{
			List<string> weaponTags = DefDatabase<PawnKindDef>.AllDefs.Where((PawnKindDef pk) => pk.race == t).First().weaponTags;
			if (weaponTags.NullOrEmpty())
			{
				if (invert)
				{
					return true;
				}
				return "MU_CannotAdd_IsNotRangedFighter".Translate();
			}
			Predicate<ThingDef> isWeapon = (ThingDef td) => td.equipmentType == EquipmentType.Primary && !td.weaponTags.NullOrEmpty() && td.weaponTags.ContainsAny((string s) => weaponTags.Contains(s));
			foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where((ThingDef td) => isWeapon(td)))
			{
				if (thingDef.IsRangedWeapon)
				{
					if(invert == true)
                    {
						return "MU_CannotAdd_IsRangedFighter".Translate();
					}
					return true;
				}
			}
			return "MU_CannotAdd_IsNotRangedFighter".Translate();
		}
	}

	public class UpgradeRestriction_MeleeFighter : UpgradeRestriction
	{
		public float factor = 0f;
        public override float CommonalityFactor(Pawn mech)
        {
			if (mech.kindDef.weaponTags.NullOrEmpty())
			{
				return 1f;
			}
            if (mech.equipment.Primary.def.IsMeleeWeapon)
            {
				return 1f;
            }
			return factor;
		}
    }

	public class UpgradeRestriction_RangedFighter : UpgradeRestriction
	{
		public float factor = 0f;
		public override float CommonalityFactor(Pawn mech)
		{
			if (mech.kindDef.weaponTags.NullOrEmpty())
			{
				return factor;
			}
			if (mech.equipment.Primary.def.IsRangedWeapon)
			{
				return 1f;
			}
			return factor;
		}
	}

	public class UpgradeRestriction_HeadFighter : UpgradeRestriction
	{
		public BodyPartGroupDef bodyPartGroup;
		public override AcceptanceReport CanAdd(ThingDef t, MechUpgradeDef u)
		{
			return t.tools.Any((Tool tool) => tool.linkedBodyPartsGroup != bodyPartGroup) == invert;
		}
	}

	public class UpgradeRestriction_Shield : UpgradeRestriction
	{
		public override AcceptanceReport CanAdd(ThingDef t, MechUpgradeDef u)
		{
			bool flag = t.HasComp(typeof(CompProjectileInterceptor));
			if (!flag && invert == false)
			{
				return "MU_CannotAdd_NoShieldComp".Translate();
			}
			if (flag && invert == true)
			{
				return "MU_CannotAdd_HasShieldComp".Translate();
			}
			return true;
		}
	}

	public class UpgradeRestriction_Tag : UpgradeRestriction
	{
		public string tag;

		public override AcceptanceReport CanAdd(ThingDef t, MechUpgradeDef u)
		{
			UpgradabilityTagExtension ext = t.GetModExtension<UpgradabilityTagExtension>();
			if (ext != null && ext.tags.Contains(tag))
			{
				return true;
			}
			else return "MU_CannotAdd_WrongMech".Translate();
		}
	}
}