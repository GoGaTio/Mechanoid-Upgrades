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

namespace MU 
{
	public class ThinkNode_ConditionalUpgradeNeedsReload : ThinkNode_Conditional
    { 
		protected override bool Satisfied(Pawn pawn)
		{
			if(!pawn.Spawned || pawn.Downed)
            {
				return false;
            }
			if (PawnUtility.EnemiesAreNearby(pawn, 9, passDoors: true, 45f, 1))
			{
				return false;
			}
			CompUpgradableMechanoid comp = pawn.TryGetComp<MU.CompUpgradableMechanoid>();
			if(comp == null)
            {
				return false;
            }
            if (comp.NeedsReload)
            {
				return comp.autoReload;
            }
			return false;
		}
	}

	public class JobGiver_ReloadUpgrades : ThinkNode_JobGiver
	{
		private const bool ForceReloadWhenLookingForWork = false;

		public override float GetPriority(Pawn pawn)
		{
			return 5.9f;
		}

		protected override Job TryGiveJob(Pawn pawn)
		{
			CompUpgradableMechanoid comp = pawn.TryGetComp<CompUpgradableMechanoid>();
			if(comp.autoReload == false)
            {
				return null;
            }
			List<UpgradeCompReloadable> list = new List<UpgradeCompReloadable>();
			foreach(MechUpgrade u in comp.upgrades)
            {
				list.AddRange(u.Reloadables());
            }
			Ability ability = null;
			if(list.FirstOrDefault((UpgradeCompReloadable c)=> c.NeedsAutoReload) == null)
			{
				ability = pawn.abilities.abilities.FirstOrDefault((Ability a) => a.CompOfType<MU.CompAbilityEffect_ReloadableUpgrade>() != null && a.CompOfType<MU.CompAbilityEffect_ReloadableUpgrade>().NeedsReload);
				if (ability == null)
				{
					return null;
				}
			}
			if(comp.CanReload(ability, list.FirstOrDefault((UpgradeCompReloadable c) => c.NeedsAutoReload), out Job job))
            {
				if (list.FirstOrDefault((UpgradeCompReloadable c) => c.NeedsAutoReload) != null)
                {
					comp.compForReload = list.FirstOrDefault((UpgradeCompReloadable c) => c.NeedsAutoReload);
				}
				return job;
			}
			return null;
		}
	}
	public class JobDriver_ReloadAbilityUpgrade : JobDriver
	{
		private const TargetIndex ItemInd = TargetIndex.A;

        public override string GetReport()
        {
            return base.GetReport();
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(job.GetTarget(ItemInd).Thing, job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnIncapable(PawnCapacityDefOf.Manipulation);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.A).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
			yield return Toils_Haul.StartCarryThing(TargetIndex.A, putRemainderInQueue: false, subtractNumTakenFromJobCount: true).FailOnDestroyedNullOrForbidden(TargetIndex.A);
			yield return Toils_General.Wait(job.takeInventoryDelay).WithProgressBarToilDelay(TargetIndex.A); 
			if (job.ability != null)
			{
				Ability ability = job.ability;
				MU.CompAbilityEffect_ReloadableUpgrade Acomp = ability.CompOfType<MU.CompAbilityEffect_ReloadableUpgrade>();
				this.FailOn(() => Acomp == null);
				this.FailOn(() => !Acomp.NeedsReload);
				Toil toil1 = ToilMaker.MakeToil("Reload");
				toil1.initAction = delegate
				{
					Thing carriedThing = pawn.carryTracker.CarriedThing;
					Acomp.Reload(carriedThing);
				};
				toil1.defaultCompleteMode = ToilCompleteMode.Instant;
				yield return toil1;
			}
            else
            {
				CompUpgradableMechanoid comp = pawn.TryGetComp<CompUpgradableMechanoid>();
				UpgradeCompReloadable reloadable = comp.compForReload;
				this.FailOn(() => reloadable == null);
				this.FailOn(() => !reloadable.NeedsReload);
				Toil toil1 = ToilMaker.MakeToil("Reload");
				toil1.initAction = delegate
				{
					Thing carriedThing = pawn.carryTracker.CarriedThing;
					reloadable.Reload(carriedThing, job.ingestTotalCount);
					comp.compForReload = null;
				};
				toil1.defaultCompleteMode = ToilCompleteMode.Instant;
				yield return toil1;
				
			}
			Toil toil2 = ToilMaker.MakeToil("MakeNewToils");
			toil2.initAction = delegate
			{
				Thing carriedThing = pawn.carryTracker.CarriedThing;
				if (carriedThing != null && !carriedThing.Destroyed)
				{
					pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var _);
				}
			};
			toil2.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return toil2;
		}
	}
	public class WorkGiver_HaulToUpgradeStorage : WorkGiver_Scanner
	{ 
		public override PathEndMode PathEndMode => PathEndMode.Touch;

		//public override bool ShouldSkip(Pawn pawn, bool forced = false){return !ModsConfig.IsActive()}

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (Thing t1 in pawn.Map.listerThings.AllThings.Where((Thing t2) => t2.HasComp<MU.CompMechUpgrade>() && pawn.CanReach(t2, PathEndMode.Touch, Danger.Deadly)))
            {
				yield return t1;
			}
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			if (t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced))
			{
				return false;
			}
			return FindStorage(pawn, t, false) != null;
		}

		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			Thing thing = FindStorage(pawn, t);
			if (thing != null)
			{
				Job job = JobMaker.MakeJob(MUJobDefOf.MU_CarryUpgradeToStorage, t, thing, thing.InteractionCell);
				job.count = 1;
				return job;
			}
			return null;
		}

		private Thing FindStorage(Pawn pawn, Thing t1, bool findBetter = true)
		{
			if (!t1.HasComp<MU.CompMechUpgrade>())
			{
				return null;
			}
			List<Thing> list = t1.Map.listerBuildings.allBuildingsColonist.Where((Thing t2) => ValidStorageFor(t2, t1) && !t2.IsForbidden(pawn) && pawn.CanReach(t2, PathEndMode.Touch, Danger.Deadly)).ToList();
			if (list.NullOrEmpty())
            {
				return null;
            }
            if (!findBetter)
            {
				return list.First();
            }
			
			return GenClosest.ClosestThing_Global(t1.Position, StoragesWithPriorities(list));
		}

		public bool ValidStorageFor(Thing storage, Thing upgrade)
        {
			CompUpgradesStorage comp = storage.TryGetComp<MU.CompUpgradesStorage>();
			if(comp == null)
            {
				return false;
            }
            if (comp.Space < upgrade.TryGetComp<MU.CompMechUpgrade>().Props.upgradeDef.upgradePoints)
            {
				return false;
            }
			if (comp.GetStoreSettings().AllowedToAccept(upgrade))
            {
				return true;
            }
			return false;
        }

		public IEnumerable<Thing> StoragesWithPriorities(List<Thing> storages)
        {
			byte maxPriority = 0;
			foreach(Thing item in storages)
            {
				maxPriority = (byte)Mathf.Max((byte)(item.TryGetComp<MU.CompUpgradesStorage>().GetStoreSettings().Priority), maxPriority);
				if (maxPriority >= 5)
                {
					break;
                }
            }
			foreach (Thing item2 in storages)
			{
				if (maxPriority == (byte)(item2.TryGetComp<MU.CompUpgradesStorage>().GetStoreSettings().Priority))
				{
					yield return item2;
				}
			}
		}
	}
	public class JobDriver_CarryUpgradeToStorage : JobDriver
	{
		private const TargetIndex GenepackInd = TargetIndex.A;

		private const TargetIndex ContainerInd = TargetIndex.B;

		private const int InsertTicks = 30;

		private Thing Container => job.GetTarget(TargetIndex.B).Thing;

		private CompUpgradesStorage ContainerComp => Container.TryGetComp<CompUpgradesStorage>();

		private Thing Item => job.GetTarget(TargetIndex.A).Thing;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			if (!ModLister.CheckBiotech("Genepack"))
			{
				yield break;
			}
			this.FailOn(delegate
			{
				return ContainerComp.Space < Item.TryGetComp<MU.CompMechUpgrade>().Props.upgradeDef.upgradePoints;
			});
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell).FailOnDespawnedNullOrForbidden(TargetIndex.A);
			yield return Toils_Haul.StartCarryThing(TargetIndex.A, putRemainderInQueue: false, subtractNumTakenFromJobCount: true);
			yield return Toils_Goto.Goto(TargetIndex.B, PathEndMode.Touch);
			Toil toil = Toils_General.Wait(30, TargetIndex.B).WithProgressBarToilDelay(TargetIndex.B).FailOnDespawnedOrNull(TargetIndex.B);
			toil.handlingFacing = true;
			yield return toil;
			Toil trade = ToilMaker.MakeToil("MakeNewToils");
			trade.initAction = delegate
			{
				if(ContainerComp.innerContainer.TryAddOrTransfer(Item, 1, false) > 0)
                {
					Item.def.soundDrop.PlayOneShot(SoundInfo.InMap(Container));
					MoteMaker.ThrowText(Container.DrawPos, pawn.Map, "InsertedThing".Translate($"{ContainerComp.Amount} / {ContainerComp.Props.maxCapacity}"));
				}
			};
			yield return trade;
		}
	}

	public class JobDriver_ConfigureUpgrades : JobDriver
	{
		private Building_MechUpgrader Upgrader => (Building_MechUpgrader)base.TargetThingA;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(Upgrader, job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedOrNull(TargetIndex.A);
			this.FailOn(() => Upgrader.State != UpgraderState.WaitingForMechanitor);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell).FailOn(() => Upgrader.State != UpgraderState.WaitingForMechanitor);
			Toil trade = ToilMaker.MakeToil("MakeNewToils");
			trade.initAction = delegate
			{
				Pawn actor = trade.actor;
				if (Upgrader.State == UpgraderState.WaitingForMechanitor)
				{
					Find.WindowStack.Add(new Dialog_ConfigureUpgrades(Upgrader.Occupant, Upgrader));
				}
			};
			yield return trade;
		}
	}
}