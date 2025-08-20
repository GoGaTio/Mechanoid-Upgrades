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

namespace MU
{
	public enum UpgraderState
	{
		Inactive,
		WaitingForMechanoid,
		WaitingForMechanitor,
		Working
	}

	[StaticConstructorOnStartup]
	public class Building_MechUpgrader : Building_Enterable, IThingHolderWithDrawnPawn, IThingHolder, ITargetingSource
	{
		public bool CasterIsPawn => true;
		public bool IsMeleeAttack => false;
		public bool Targetable => true;
		public bool MultiSelect => false;
		public bool HidePawnTooltips => false;
		public Thing Caster => this;
		public Pawn CasterPawn => null;
		public Verb GetVerb => null;
		public TargetingParameters targetParams => new TargetingParameters()
		{
			canTargetPawns = true,
			canTargetLocations = false
		};
		public Texture2D UIIcon => InsertIcon_Target.Texture;
		public ITargetingSource DestinationSelector => null;
		public bool CanHitTarget(LocalTargetInfo target)
		{
			return ValidateTarget(target, showMessages: false);
		}
		public bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
		{
			if (target.IsValid && target.HasThing && target.Thing is Pawn pawn)
			{
				AcceptanceReport acceptanceReport = CanAcceptPawn(pawn);
				if (!acceptanceReport.Accepted)
				{
					if (showMessages && !acceptanceReport.Reason.NullOrEmpty())
					{
						Messages.Message(acceptanceReport.Reason.CapitalizeFirst(), pawn, MessageTypeDefOf.RejectInput, historical: false);
					}
					return false;
				}
				return true;
			}
			return false;
		}
		public void DrawHighlight(LocalTargetInfo target)
		{
			if (target.IsValid)
			{
				GenDraw.DrawTargetHighlight(target);
			}
		}

		public virtual void OrderForceTarget(LocalTargetInfo target)
		{
			if (target.IsValid && target.HasThing && target.Thing is Pawn pawn)
			{
				if (CanAcceptPawn(pawn))
				{
					SelectPawn(pawn);
				}
			}
		}
		public void OnGUI(LocalTargetInfo target)
		{
			if (ValidateTarget(target, showMessages: false))
			{
				GenUI.DrawMouseAttachment(UIIcon);
			}
			else
			{
				GenUI.DrawMouseAttachment(TexCommand.CannotShoot);
			}
		}


		public int fabricationTicksLeft;

		public List<MU.MechUpgradeOperation> operations;

		private Effecter effectHusk;

		private Effecter progressBarEffecter;

		private Mote workingMote;

		private static Dictionary<Rot4, ThingDef> MotePerRotation = new Dictionary<Rot4, ThingDef>
				{
					{
						Rot4.South,
						MUThingDefOf.MU_MechUpgraderGlow_South
						//ThingDefOf.SoftScannerGlow_South
					},
					{
						Rot4.East,
						MUThingDefOf.MU_MechUpgraderGlow_East
						//ThingDefOf.SoftScannerGlow_East
					},
					{
						Rot4.West,
						MUThingDefOf.MU_MechUpgraderGlow_West
						//ThingDefOf.SoftScannerGlow_West
					},
					{
						Rot4.North,
						MUThingDefOf.MU_MechUpgraderGlow_North
						//ThingDefOf.SoftScannerGlow_North
					}
				};


		public static readonly Texture2D CancelLoadingIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");

		public static readonly CachedTexture InsertIcon_Target = new CachedTexture("UI/Gizmos/MU_InsertMechanoid_Target");

		public static readonly CachedTexture InsertIcon_List = new CachedTexture("UI/Gizmos/MU_InsertMechanoid_List");

		public static readonly CachedTexture ChangeUpgradesIcon = new CachedTexture("UI/Gizmos/MU_ChangeUpgrades");

		private const float ProgressBarOffsetZ = -0.8f;

		public float HeldPawnDrawPos_Y => DrawPos.y + 1f / 26f;

		public float HeldPawnBodyAngle => base.Rotation.AsAngle;

		public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundFaceUp;

		public bool PowerOn => this.TryGetComp<CompPowerTrader>().PowerOn;

		public override Vector3 PawnDrawOffset => Vector3.zero;

		public Pawn Occupant
		{
			get
			{
				for (int i = 0; i < innerContainer.Count; i++)
				{
					if (innerContainer[i] is Pawn result)
					{
						return result;
					}
				}
				return null;
			}
		}

		public UpgraderState State
		{
			get
			{
				if (!operations.NullOrEmpty())
				{
					return UpgraderState.Working;
				}
				if (Occupant != null)
				{
					return UpgraderState.WaitingForMechanitor;
				}
				if (SelectedPawn != null)
				{
					return UpgraderState.WaitingForMechanoid;
				}
				return UpgraderState.Inactive;
			}
		}

		public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
		{
			EjectContents();
			base.Destroy(mode);
		}

		public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
		{
			progressBarEffecter?.Cleanup();
			progressBarEffecter = null;
			base.DeSpawn(mode);
		}

		public override AcceptanceReport CanAcceptPawn(Pawn selPawn)
		{
			if (!selPawn.HasComp<MU.CompUpgradableMechanoid>() || !selPawn.IsColonyMech)
			{
				return false;
			}
			if (selectedPawn != null && selectedPawn != selPawn)
			{
				return false;
			}
			if (!PowerOn)
			{
				return "CannotUseNoPower".Translate();
			}
			if (selPawn.OverseerSubject.State != OverseerSubjectState.Overseen)
			{
				return "MU_IsNotControlled".Translate();
			}
			return true;
		}

		public override void TryAcceptPawn(Pawn pawn)
		{
			if ((bool)CanAcceptPawn(pawn))
			{
				bool num = pawn.DeSpawnOrDeselect();
				innerContainer.TryAddOrTransfer(pawn);
				Find.WindowStack.Add(new Dialog_ConfigureUpgrades(Occupant, this));
				/*Pawn overseer = pawn.GetOverseer();
				if ((overseer == null && pawn.OverseerSubject.State == OverseerSubjectState.Overseen) || (overseer != null && overseer.gender == Gender.None && overseer.story.title == ""))
				{
					Find.WindowStack.Add(new Dialog_ConfigureUpgrades(Occupant, this));
					return;
				}
				if (num)
				{
					Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
				}*/
			}
		}

		public void EjectContents()
		{
			Pawn occupant = Occupant;
			if (occupant == null)
			{
				innerContainer.TryDropAll(InteractionCell, base.Map, ThingPlaceMode.Near);
			}
			else
			{
				for (int num = innerContainer.Count - 1; num >= 0; num--)
				{
					if (innerContainer[num] is Pawn || innerContainer[num] is Corpse)
					{
						innerContainer.TryDrop(innerContainer[num], InteractionCell, base.Map, ThingPlaceMode.Near, 1, out var _);
					}
				}
				innerContainer.ClearAndDestroyContents();
			}
			selectedPawn = null;
            if (!operations.NullOrEmpty())
            {
				foreach (MechUpgradeOperation o in operations)
				{
					if (o.type == UpgradeOperationType.Add && o.upgrade != null)
					{
						GenSpawn.Spawn(MechUpgradeUtility.ItemFromUpgrade(o.upgrade), this.Position, this.Map);
					}
				}
				operations.Clear();
			}
		}

		public bool IsNeededMechanitor(Pawn p)
		{
			if (Occupant == null)
			{
				return false;
			}
			if (Occupant.GetOverseer() == p)
			{
				return true;
			}
			return false;
		}

		public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
		{
			foreach (FloatMenuOption floatMenuOption in base.GetFloatMenuOptions(selPawn))
			{
				yield return floatMenuOption;
			}
			/*if (State != UpgraderState.WaitingForMechanitor)
			{
				yield break;
			}
			if (!IsNeededMechanitor(selPawn))
			{
				yield return new FloatMenuOption("CannotUseReason".Translate("MU_IsNotMechanitor".Translate().CapitalizeFirst()), null);
				yield break;
			}
			if (!selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))
			{
				yield return new FloatMenuOption("CannotUseReason".Translate("NoPath".Translate().CapitalizeFirst()), null);
				yield break;
			}
			if (IsNeededMechanitor(selPawn))
			{
				yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("MU_ConfigureUpgrades".Translate(this), delegate
				{
					selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(MUJobDefOf.MU_ConfigureUpgrades, this), JobTag.Misc);
				}), selPawn, this);
			}*/
		}

		public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
		{
			base.DynamicDrawPhaseAt(phase, drawLoc, flip);
			Occupant?.Drawer.renderer.DynamicDrawPhaseAt(phase, drawLoc, null, neverAimWeapon: true);
		}

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
			if (State == UpgraderState.WaitingForMechanoid && (SelectedPawn.jobs.curJob.def != JobDefOf.EnterBuilding || SelectedPawn.jobs.curJob.targetA != this))
			{
				SelectedPawn = null;
				selectedPawn = null;
			}
			if (Occupant != null && Occupant.OverseerSubject?.State != OverseerSubjectState.Overseen)
			{
				EjectContents();
				operations.Clear();
			}
			if (State == UpgraderState.Working)
			{
				fabricationTicksLeft -= delta;
				if (fabricationTicksLeft <= 0)
				{
					MU.MechUpgradeOperation operation = operations.Last();
					operation.DoOperation(Occupant, this);
					operations.Remove(operation);
					if (!operations.NullOrEmpty())
					{
						fabricationTicksLeft = 2500 * operations.Last().upgrade.def.upgradePoints;
					}
					else
					{
						EjectContents();
					}
				}
				if (workingMote == null || workingMote.Destroyed)
				{
					workingMote = MoteMaker.MakeAttachedOverlay(this, MotePerRotation[base.Rotation], Vector3.zero);
				}
				workingMote.Maintain();
				if (progressBarEffecter == null)
				{
					progressBarEffecter = EffecterDefOf.ProgressBar.Spawn();
				}
				progressBarEffecter.EffectTick(this, TargetInfo.Invalid);
				MoteProgressBar mote = ((SubEffecter_ProgressBar)progressBarEffecter.children[0]).mote;
				int i = 1;
				if (!operations.NullOrEmpty())
				{
					i = operations.Last().upgrade.def.upgradePoints;
				}
				mote.progress = 1f - (float)fabricationTicksLeft / (2500f * i);
				mote.offsetZ = -0.8f;
			}
			else
			{
				effectHusk?.Cleanup();
				effectHusk = null;
				progressBarEffecter?.Cleanup();
				progressBarEffecter = null;
			}
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo gizmo in base.GetGizmos())
			{
				yield return gizmo;
			}
			if (base.SelectedPawn == null)
			{
				Command_Action command_Action1 = new Command_Action();
				command_Action1.defaultLabel = "MU_InsertMech".Translate() + "...";
				command_Action1.defaultDesc = "MU_InsertMech_Desc".Translate(def.label);
				command_Action1.icon = InsertIcon_List.Texture;
				command_Action1.action = delegate
				{
					List<FloatMenuOption> list = new List<FloatMenuOption>();
					IReadOnlyList<Pawn> allPawnsSpawned = base.Map.mapPawns.AllPawnsSpawned;
					for (int j = 0; j < allPawnsSpawned.Count; j++)
					{
						Pawn pawn = allPawnsSpawned[j];
						AcceptanceReport acceptanceReport = CanAcceptPawn(pawn);
						if (!acceptanceReport.Accepted)
						{
							if (!acceptanceReport.Reason.NullOrEmpty())
							{
								list.Add(new FloatMenuOption(pawn.LabelShortCap + ": " + acceptanceReport.Reason, null, pawn, Color.white));
							}
						}
						else
						{
							list.Add(new FloatMenuOption(pawn.LabelShortCap, delegate { SelectPawn(pawn); }, pawn, Color.white));
						}
					}
					if (!list.Any())
					{
						list.Add(new FloatMenuOption("MU_NoPawns".Translate(), null));
					}
					Find.WindowStack.Add(new FloatMenu(list));
				};
				if (!PowerOn)
				{
					command_Action1.Disable("NoPower".Translate().CapitalizeFirst());
				}
				yield return command_Action1;
				Command_Action command_Action2 = new Command_Action();
				command_Action2.defaultLabel = "MU_InsertMech".Translate() + "...";
				command_Action2.defaultDesc = "MU_InsertMech_Desc".Translate(def.label);
				command_Action2.icon = InsertIcon_Target.Texture;
				command_Action2.action = delegate
				{
					Find.Targeter.BeginTargeting(this);
				};
				if (!PowerOn)
				{
					command_Action2.Disable("NoPower".Translate().CapitalizeFirst());
				}
				yield return command_Action2;
			}
			if (State != UpgraderState.Inactive)
			{
				Command_Action command_Action3 = new Command_Action();
				command_Action3.defaultLabel = ((State == UpgraderState.WaitingForMechanoid) ? "MU_CommandCancelWaiting".Translate() : "MU_CommandCancelUpgrading".Translate());
				command_Action3.defaultDesc = ((State == UpgraderState.WaitingForMechanoid) ? "MU_CommandCancelWaiting_Desc".Translate() : "MU_CommandCancelUpgrading_Desc".Translate());
				command_Action3.icon = CancelLoadingIcon;
				command_Action3.action = delegate
				{
					if (State == UpgraderState.WaitingForMechanoid)
					{
						if (SelectedPawn.jobs?.curJob?.def == JobDefOf.EnterBuilding && SelectedPawn.jobs.curJob.targetA == this)
						{
							SelectedPawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
						}
						SelectedPawn = null;
						selectedPawn = null;
					}
					if (State == UpgraderState.WaitingForMechanitor)
					{
						if (Occupant.GetOverseer()?.jobs?.curJob?.def == MUJobDefOf.MU_ConfigureUpgrades)
						{
							Occupant.GetOverseer().jobs.EndCurrentJob(JobCondition.InterruptForced);
						}
						EjectContents();
					}
					if (State == UpgraderState.Working)
					{
						EjectContents();
					}
				};
				command_Action3.activateSound = SoundDefOf.Designate_Cancel;
				yield return command_Action3;
			}
			if (State == UpgraderState.WaitingForMechanitor)
			{
				Pawn overseer = SelectedPawn?.GetOverseer();
				Command_Action command_Action4 = new Command_Action();
				command_Action4.defaultLabel = "MU_ConfigureUpgrades".Translate();
				command_Action4.defaultDesc = "MU_ConfigureUpgrades".Translate();
				command_Action4.icon = ChangeUpgradesIcon.Texture;
				command_Action4.action = delegate
				{
					Find.WindowStack.Add(new Dialog_ConfigureUpgrades(Occupant, this));
					/*if (overseer.Spawned && overseer.Map == base.Map)
					{
						overseer.jobs.TryTakeOrderedJob(JobMaker.MakeJob(MUJobDefOf.MU_ConfigureUpgrades, this), JobTag.Misc);
					}
					else
					{
						Find.WindowStack.Add(new Dialog_ConfigureUpgrades(Occupant, this));
					}*/
				};
				if (!PowerOn)
				{
					command_Action4.Disable("NoPower".Translate().CapitalizeFirst());
				}
				yield return command_Action4;
			}
			if (!DebugSettings.ShowDevGizmos)
			{
				yield break;
			}
			if (State == UpgraderState.Working)
			{
				Command_Action command_Action5 = new Command_Action();
				command_Action5.defaultLabel = "DEV: Complete";
				command_Action5.action = delegate
				{
					fabricationTicksLeft = 0;
				};
				yield return command_Action5;
			}
		}

		public override string GetInspectString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(base.GetInspectString());
			switch (State)
			{
				case UpgraderState.Inactive:
					stringBuilder.AppendLineIfNotEmpty();
					stringBuilder.Append("MU_Upgrader_WaitingForOrder".Translate());
					break;
				case UpgraderState.WaitingForMechanoid:
					stringBuilder.AppendLineIfNotEmpty();
					stringBuilder.Append("MU_Upgrader_WaitingForMechanoid".Translate());
					break;
				case UpgraderState.WaitingForMechanitor:
					stringBuilder.AppendLineIfNotEmpty();
					stringBuilder.Append("MU_Upgrader_WaitingForMechanitor".Translate());
					break;
				case UpgraderState.Working:
					stringBuilder.AppendLineIfNotEmpty();
					if (operations != null && operations.Count > 1)
					{
						stringBuilder.Append("MU_Upgrader_TimeLeft".Translate() + "(" + "MU_ThisOperation".Translate() + "): " + fabricationTicksLeft.ToStringTicksToPeriod());
						int num = fabricationTicksLeft;
						for (int i = 0; i < operations.Count - 1; i++)
						{
							num += operations[i].upgrade.def.upgradePoints * 2500;
						}
						stringBuilder.Append("\n" + "MU_Upgrader_TimeLeft".Translate() + "(" + "MU_AllOperations".Translate() + "): " + num.ToStringTicksToPeriod());
					}
					else
					{
						stringBuilder.Append("MU_Upgrader_TimeLeft".Translate() + ": " + fabricationTicksLeft.ToStringTicksToPeriod());
					}
					break;
			}
			return stringBuilder.ToString();
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref operations, "operations", LookMode.Deep);
			Scribe_Values.Look(ref fabricationTicksLeft, "fabricationTicksLeft", 0);
		}
	}
}