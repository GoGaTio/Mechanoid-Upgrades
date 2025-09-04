using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Xml.Xsl;
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

namespace MU.Archotech //classes for add-on "Mechanoid Upgrades - Archotech"
{
	public class CompProperties_LanceEffect : CompProperties_AbilityEffect
	{
		public bool berserk;

		public FleckDef fleckDefTarget;

		public FleckDef fleckDefLine;

		public CompProperties_LanceEffect()
		{
			compClass = typeof(CompAbilityEffect_LanceEffect);
		}
	}
	public class CompAbilityEffect_LanceEffect : CompAbilityEffect
	{
		public new CompProperties_LanceEffect Props => (CompProperties_LanceEffect)props;

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			base.Apply(target, dest);
			Pawn pawn = target.Pawn;
			if (pawn == null || pawn.Dead)
			{
				return;
			}
			FleckMaker.ConnectingLine(parent.pawn.DrawPos, pawn.DrawPos, Props.fleckDefLine, parent.pawn.Map);
			FleckMaker.AttachedOverlay(pawn, Props.fleckDefTarget, Vector3.zero);
			Faction faction = pawn.HomeFaction;
			if (parent.pawn.Faction == Faction.OfPlayer && faction != null && !faction.HostileTo(parent.pawn.Faction) && (pawn == null || !pawn.IsSlaveOfColony))
			{
				Faction.OfPlayer.TryAffectGoodwillWith(faction, -200, canSendMessage: true, canSendHostilityLetter: true, HistoryEventDefOf.UsedHarmfulItem);
			}
			if (!pawn.Dead && Rand.Value <= 0.3f)
			{
				BodyPartRecord brain = pawn.health.hediffSet.GetBrain();
				if (brain != null)
				{
					int num = Rand.RangeInclusive(1, 5);
					pawn.TakeDamage(new DamageInfo(DamageDefOf.Flame, num, 0f, -1f, parent.pawn, brain));
				}
			}
			if (Props.berserk)
			{
				pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk, null, forced: false, forceWake: true);
			}
            else
            {
				Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.PsychicShock, pawn);
				pawn.RaceProps.body.GetPartsWithTag(BodyPartTagDefOf.ConsciousnessSource).TryRandomElement(out var result);
				pawn.health.AddHediff(hediff, result);
			}
		}

		public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
		{
			return Valid(target);
		}

		public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
		{
			Pawn pawn = target.Pawn;
			if (pawn == null || pawn.Dead)
			{
				return false;
			}
			if (pawn.kindDef.isBoss)
			{
				return false;
			}
			if (pawn.GetStatValue(StatDefOf.PsychicSensitivity) <= 0f)
			{
				if (throwMessages)
				{
					Messages.Message("CannotShootPawnIsPsychicallyDeaf".Translate(pawn), pawn, MessageTypeDefOf.RejectInput, historical: false);
				}
				return false;
			}
			if (!Props.berserk)
			{
				if (pawn.kindDef.forceDeathOnDowned)
				{
					return false;
				}
				if (pawn.IsMutant && pawn.mutant.Def.psychicShockUntargetable)
				{
					return false;
				}
			}
			return true;
		}

		public override bool AICanTargetNow(LocalTargetInfo target)
		{
			Pawn pawn = target.Pawn;
			if (pawn == null || pawn.Dead)
			{
				return false;
			}
			return (pawn.Faction == Faction.OfPlayer && pawn.RaceProps.ToolUser) || (!Props.berserk && pawn.kindDef.combatPower > 150f);
		}
	}

	public class HediffCompProperties_Perplex : HediffCompProperties
	{
		public DamageDef damageDef;

		public FloatRange damageAmountRange;

		public IntRange intervalTicksRange;

		public FloatRange severityLossRange;

		public HediffCompProperties_Perplex()
		{
			compClass = typeof(HediffComp_Perplex);
		}
	}
	public class HediffComp_Perplex : HediffComp
	{
		public int ticksTillEffect = 10;
		public HediffCompProperties_Perplex Props => (HediffCompProperties_Perplex)props;

		public override void CompPostTick(ref float severityAdjustment)
		{
            if (parent.pawn.Dead)
            {
				return;
            }
			if (ticksTillEffect > 0)
			{
				ticksTillEffect--;
				return;
			}
            if (parent.pawn.Spawned)
            {
				DoEffect(parent.pawn, Props.damageDef, Props.damageAmountRange.RandomInRange);
				parent.Severity -= Props.severityLossRange.RandomInRange;
				ticksTillEffect = Props.intervalTicksRange.RandomInRange;
			}
		}

		public static void DoEffect(Pawn target, DamageDef damage, float amount, bool skip = true)
		{
			if(skip)
			{
				if(CellFinder.TryFindRandomReachableNearbyCell(target.PositionHeld, target.MapHeld, 7f, TraverseParms.For(TraverseMode.NoPassClosedDoorsOrWater), (IntVec3 cell) => GenSight.LineOfSight(cell, target.PositionHeld, target.MapHeld), null, out var result))
                {
					SkipUtility.SkipTo(target, result, target.MapHeld);
				}
			}
			DamageInfo dinfo = new DamageInfo(damage, amount, 999f);
			target.TakeDamage(dinfo);
		}

		public override void CompExposeData()
        {
            base.CompExposeData();
			Scribe_Values.Look(ref ticksTillEffect, "ticksTillEffect", 60);
		}
    }

	public class UpgradeCompProperties_PerplexGiver : UpgradeCompProperties
	{
		public DamageDef damageDef;

		public FloatRange damageAmountRange;

		public IntRange intervalTicksRange;

		public UpgradeCompProperties_PerplexGiver()
		{
			compClass = typeof(UpgradeComp_PerplexGiver);
		}
	}
	public class UpgradeComp_PerplexGiver : UpgradeComp
	{
		public UpgradeCompProperties_PerplexGiver Props => (UpgradeCompProperties_PerplexGiver)props;

		public int ticksLeft = 25000;

		public override void CompTick()
		{
			if (ticksLeft > 0)
			{
				ticksLeft--;
			}
            else
            {
                if (Mech.Spawned)
                {
					HediffComp_Perplex.DoEffect(Mech, Props.damageDef, Props.damageAmountRange.RandomInRange, true);
					ticksLeft = Props.intervalTicksRange.RandomInRange;
				}
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref ticksLeft, "ticksLeft", 25000);
		}
	}

	public class CompTargetable_UpgradedMechCorpse : CompTargetable
	{
		protected override bool PlayerChoosesTarget => true;

		protected override TargetingParameters GetTargetingParameters()
		{
			return new TargetingParameters
			{
				canTargetPawns = false,
				canTargetBuildings = false,
				canTargetItems = false,
				canTargetCorpses = true,
				mapObjectTargetsMustBeAutoAttackable = false
			};
		}

		public override IEnumerable<Thing> GetTargets(Thing targetChosenByPlayer = null)
		{
			yield return targetChosenByPlayer;
		}

		public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
		{
			if (target.Thing is Corpse corpse && corpse.InnerPawn?.TryGetComp<CompUpgradableMechanoid>()?.upgrades?.NullOrEmpty() == false)
			{
				return base.ValidateTarget(target.Thing, showMessages);
			}
			return false;
		}
	}

	public class Verb_CastTargetEffectExtractUpgrade : Verb_CastTargetEffect
	{
		public override void OnGUI(LocalTargetInfo target)
		{
			if (CanHitTarget(target) && verbProps.targetParams.CanTarget(target.ToTargetInfo(caster.Map)))
			{
				Pawn pawn = target.Pawn;
				if (pawn != null)
				{
					bool flag = false;
					foreach (CompTargetEffect comp in base.EquipmentSource.GetComps<CompTargetEffect>())
					{
						if (!comp.CanApplyOn(pawn))
						{
							flag = true;
							break;
						}
					}
					if (flag)
					{
						GenUI.DrawMouseAttachment(TexCommand.CannotShoot);
						if (!string.IsNullOrEmpty(verbProps.invalidTargetPawn))
						{
							Widgets.MouseAttachedLabel(verbProps.invalidTargetPawn.CapitalizeFirst(), 0f, -20f);
						}
					}
				}
				else
				{
					base.OnGUI(target);
				}
			}
			else
			{
				GenUI.DrawMouseAttachment(TexCommand.CannotShoot);
			}
		}

		public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
		{
			Pawn pawn = target.Pawn;
			if (pawn != null)
			{
				foreach (CompTargetEffect comp in base.EquipmentSource.GetComps<CompTargetEffect>())
				{
					if (!comp.CanApplyOn(target.Pawn))
					{
						return false;
					}
				}
			}
			return base.ValidateTarget(target, showMessages);
		}
	}

	public class CompProperties_TargetEffectExtractUpgrade : CompProperties
	{
		public ThingDef moteDef;

		public IntRange damageRange;

		public DamageDef damageDef;

		public JobDef job = null; //if job is null it work as lance effect

		public List<float> options;

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string s in base.ConfigErrors(parentDef))
            {
				yield return s;
            }
            if (options.NullOrEmpty())
            {
				yield return "MU.Archotech.CompProperties_TargetEffectExtractUpgrade should have at least one valid option node in <options>";
			}
		}

        public CompProperties_TargetEffectExtractUpgrade()
		{
			compClass = typeof(CompTargetEffect_ExtractUpgrade);
		}
	}

	public class CompTargetEffect_ExtractUpgrade : CompTargetEffect
	{
		public CompProperties_TargetEffectExtractUpgrade Props => (CompProperties_TargetEffectExtractUpgrade)props;

		public override void DoEffectOn(Pawn user, Thing target)
		{
			if (Props.job != null && user.IsColonistPlayerControlled)
			{
				Job job = JobMaker.MakeJob(Props.job, target, parent);
				job.count = 1;
				job.playerForced = true;
				user.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			}
            else
            {
				if(target is Pawn mech)
                {
					if(TryExtractUpgrade(mech) && Props.damageDef != null)
                    {
						mech.TakeDamage(new DamageInfo(Props.damageDef, Props.damageRange.RandomInRange, 999f, -1, user));
					}
                }
            }
		}

        public override bool CanApplyOn(Thing target)
        {
			if(Props.job == null)
            {
				if(target is Pawn mech && mech.TryGetComp<CompUpgradableMechanoid>()?.upgrades?.Any((MechUpgrade u) => u.def.linkedThingDef != null) == true)
                {
					return true;
                }
				return false;
            }
			if(target is Corpse corpse && corpse.InnerPawn?.TryGetComp<CompUpgradableMechanoid>()?.upgrades?.Any((MechUpgrade u) => u.def.linkedThingDef != null) == true)
            {
				return true;
            }
            return false;
        }
        public bool TryExtractUpgrade(Pawn mech)
        {
			CompUpgradableMechanoid comp = mech.TryGetComp<CompUpgradableMechanoid>();
			if(comp == null || comp.upgrades.NullOrEmpty())
            {
				return false;
            }
			int amount = Mathf.Min(Props.options.IndexOf(Props.options.RandomElementByWeight((float o) => o)) + 1, comp.upgrades.Count);
			for (int i= 0; i < amount; i++)
            {
				MechUpgrade upgrade = comp.upgrades.RandomElementByWeight((MechUpgrade u) => u.def.linkedThingDef == null ? 0 : 3f / (float)(u.def.upgradePoints + 2));//upgrades with more points will have smaller chances
				upgrade.OnRemoved(mech); 
				comp.upgrades.Remove(upgrade);
				GenSpawn.Spawn(MechUpgradeUtility.ItemFromUpgrade(upgrade), mech.PositionHeld, mech.MapHeld);
            }
			return true;
        }
	}

	public class JobDriver_ExtractUpgrade : JobDriver
	{
		private const TargetIndex CorpseInd = TargetIndex.A;

		private const TargetIndex ItemInd = TargetIndex.B;

		private const int DurationTicks = 600;

		private Mote warmupMote;

		private Corpse Corpse => (Corpse)job.GetTarget(TargetIndex.A).Thing;

		private Thing Item => job.GetTarget(TargetIndex.B).Thing;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			if (pawn.Reserve(Corpse, job, 1, -1, null, errorOnFailed))
			{
				return pawn.Reserve(Item, job, 1, -1, null, errorOnFailed);
			}
			return false;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch).FailOnDespawnedOrNull(TargetIndex.B).FailOnDespawnedOrNull(TargetIndex.A);
			yield return Toils_Haul.StartCarryThing(TargetIndex.B);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnDespawnedOrNull(TargetIndex.A);
			Toil toil = Toils_General.Wait(600);
			toil.WithProgressBarToilDelay(TargetIndex.A);
			toil.FailOnDespawnedOrNull(TargetIndex.A);
			toil.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
			toil.tickAction = delegate
			{
				CompUsable compUsable = Item.TryGetComp<CompUsable>();
				if (compUsable != null && warmupMote == null && compUsable.Props.warmupMote != null)
				{
					warmupMote = MoteMaker.MakeAttachedOverlay(Corpse, compUsable.Props.warmupMote, Vector3.zero);
				}
				warmupMote?.Maintain();
			};
			yield return toil;
			yield return Toils_General.Do(Extract);
		}

		private void Extract()
		{
			Pawn innerPawn = Corpse.InnerPawn;
			CompTargetEffect_ExtractUpgrade compTargetEffect = Item.TryGetComp<CompTargetEffect_ExtractUpgrade>();
			if (compTargetEffect.TryExtractUpgrade(innerPawn))
			{
				Item.SplitOff(1).Destroy();
			}
		}
	}

	public class DamageWorker_Perplex : DamageWorker_Bite
	{
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
			if (thing is Pawn pawn)
			{
				Hediff perplex = pawn.health.GetOrAddHediff(MUMiscDefOf.MU_Perplex);
				perplex.Severity += new FloatRange(0.1f, 0.15f).RandomInRange;
			}
			return base.Apply(dinfo, thing);
		}
        protected override BodyPartRecord ChooseHitPart(DamageInfo dinfo, Pawn pawn)
		{
			return pawn.health.hediffSet.GetRandomNotMissingPart(dinfo.Def, dinfo.Height, BodyPartDepth.Outside);
		}
	}
}