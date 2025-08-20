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

	public class UpgradeCompProperties
	{
		[TranslationHandle]
		public Type compClass = typeof(UpgradeComp);

		public UpgradeCompProperties()
		{
		}

		public UpgradeCompProperties(Type compClass)
		{
			this.compClass = compClass;
		}

		public virtual IEnumerable<string> ConfigErrors(MechUpgradeDef parentDef)
		{
			if (compClass == null)
			{
				yield return parentDef.defName + " has UpgradeCompProperties with null compClass.";
			}
		}

		public virtual IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
		{
			return Enumerable.Empty<StatDrawEntry>();
		}
	}

	public abstract class UpgradeComp
	{
		public UpgradeCompProperties props;

		public MechUpgradeWithComps parent;

		public Pawn Mech => parent.holder;

		public virtual void PostGenerated(bool generatedForHolder)
		{

		}
		public virtual void Notify_ChargeUsed(Ability ability)
        {

        }

		public virtual void PostSpawnSetup(bool respawningAfterLoad)
		{
		}

		public virtual IEnumerable<string> ExtraDescStrings()//new
		{
			return Enumerable.Empty<string>();
		}

		public virtual IEnumerable<Gizmo> CompGetGizmosExtra()//
		{
			return Enumerable.Empty<Gizmo>();
		}

		public virtual void Added()//new
		{
		}

		public virtual void Removed()//new
		{
		}

		public virtual void Initialize(UpgradeCompProperties props)//
		{
			this.props = props;
		}

		public virtual void CompTick()//
		{
        }

		public virtual void PostExposeData()//
		{
			
		}

		public virtual void PostDrawExtraSelectionOverlays()
		{
		}

		public virtual void DrawGUIOverlay()//
		{
		}

		public virtual void PostDraw()//
        {
        }

		public virtual void PostDeSpawn(Map map)
		{
		}

		public virtual void PostDestroy(DestroyMode mode, Map previousMap)
		{
		}

		public virtual IEnumerable<StatDrawEntry> UpgradeSpecialDisplayStats()
		{
			return Enumerable.Empty<StatDrawEntry>();
		}


		public virtual IEnumerable<StatDrawEntry> MechSpecialDisplayStats()
		{
			return Enumerable.Empty<StatDrawEntry>();
		}

		public virtual void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)//
		{
			absorbed = false;
		}

		public virtual void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)//
		{
		}

		public virtual float GetStatFactor(StatDef stat)
		{
			return 1f;
		}

		public virtual float GetStatOffset(StatDef stat)
		{
			return 0f;
		}

		public virtual void GetOffsetsExplanation(StatDef stat, StringBuilder sb)
		{
		}

		public virtual void GetFactorsExplanation(StatDef stat, StringBuilder sb)
		{
		}

		public virtual List<PawnRenderNode> CompRenderNodes()
		{
			return new List<PawnRenderNode>();
		}

		public virtual void Notify_AbandonedAtTile(int tile)
		{
		}

		public virtual void Notify_Downed()
		{
		}

		public virtual void Notify_KilledPawn(Pawn pawn)
		{
		}

		public virtual void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
		{
		}
	}

	public class UpgradeCompProperties_ShieldUpgrade : UpgradeCompProperties
	{
		public float factor;

		public UpgradeCompProperties_ShieldUpgrade()
		{
			compClass = typeof(UpgradeComp_ShieldUpgrade);
		}
	}
	public class UpgradeComp_ShieldUpgrade : UpgradeComp
	{
		public UpgradeCompProperties_ShieldUpgrade Props => (UpgradeCompProperties_ShieldUpgrade)props;

        public override void Added()
        {
			CompProjectileInterceptor comp = parent.holder.TryGetComp<CompProjectileInterceptor>();
			if(comp != null)
            {
				comp.maxHitPointsOverride = Mathf.RoundToInt(comp.Props.hitPoints * Props.factor);
				comp.currentHitPoints = comp.HitPointsMax;
			}
        }

        public override void Removed()
        {
			CompProjectileInterceptor comp = parent.holder.TryGetComp<CompProjectileInterceptor>();
			if (comp != null)
			{
				comp.maxHitPointsOverride = null;
				if (comp.currentHitPoints > comp.HitPointsMax)
				{
					comp.currentHitPoints = comp.HitPointsMax;
				}
			}
		}
    }

	public class UpgradeCompProperties_HediffInRange : UpgradeCompProperties
	{
		public HediffDef hediffDef;

		public float range;

		public bool targetBrain;

		public bool onlyTargetMechs;

		public bool onlyTargetFlesh;

		public bool targetSelf;

		public bool onlySameFaction;

		public bool activeWhileSleep;

		[NoTranslate]
		public string gismoTextKey;

		[NoTranslate]
		public string gizmoTexPath;

		public UpgradeCompProperties_HediffInRange()
		{
			compClass = typeof(UpgradeComp_HediffInRange);
		}
	}
	public class UpgradeComp_HediffInRange : UpgradeComp
	{
		public UpgradeCompProperties_HediffInRange Props => (UpgradeCompProperties_HediffInRange)props;

		public bool active = true;

		[Unsaved(false)]
		private Texture2D activateTex;

		public Texture2D UIIcon
		{
			get
			{
				if (!(activateTex != null))
				{
					return activateTex = ContentFinder<Texture2D>.Get(Props.gizmoTexPath);
				}
				return activateTex;
			}
		}

		public override void CompTick()
        {
            if(!active || parent.holder.DestroyedOrNull() || !parent.holder.Spawned || parent.holder.MapHeld == null || (!Props.activeWhileSleep && parent.holder.IsSelfShutdown()))
            {
				return;
            }
			foreach (Pawn item in parent.holder.MapHeld.mapPawns.AllPawnsSpawned)
			{
				if (IsPawnAffected(item))
				{
					GiveOrUpdateHediff(item);
				}
				if (item.carryTracker.CarriedThing is Pawn target && IsPawnAffected(target))
				{
					GiveOrUpdateHediff(target);
				}
			}
		}

		private void GiveOrUpdateHediff(Pawn target)
		{
			Hediff hediff = target.health.hediffSet.GetFirstHediffOfDef(Props.hediffDef);
			if (hediff == null)
			{
				hediff = target.health.AddHediff(Props.hediffDef, Props.targetBrain ? target.health.hediffSet.GetBrain() : null);
				hediff.Severity = 1f;
				HediffComp_Link hediffComp_Link1 = hediff.TryGetComp<HediffComp_Link>();
				if (hediffComp_Link1 != null)
				{
					hediffComp_Link1.drawConnection = false;
					hediffComp_Link1.other = parent.holder;
				}
			}
            if (parent.holder.IsHashIntervalTick(120))
            {
				HediffComp_Link hediffComp_Link2 = hediff.TryGetComp<HediffComp_Link>();
				if (hediffComp_Link2 != null)
				{
					hediffComp_Link2.other = parent.holder;
				}
			}
			HediffComp_Disappears hediffComp_Disappears = hediff.TryGetComp<HediffComp_Disappears>();
			if (hediffComp_Disappears == null)
			{
				Log.ErrorOnce("CompCauseHediff_AoE has a hediff in props which does not have a HediffComp_Disappears", 78945945);
			}
			else
			{
				hediffComp_Disappears.ticksToDisappear = 5;
			}
		}

		private bool IsPawnAffected(Pawn target)
		{
			if (target.Dead || target.health == null)
			{
				return false;
			}
			if (target == parent.holder && !Props.targetSelf)
			{
				return false;
			}
			if (Props.onlySameFaction && target.Faction != parent.holder.Faction)
            {
				return false;
            }
			if (Props.onlyTargetFlesh && !target.RaceProps.IsFlesh)
			{
				return false;
			}
			if (Props.onlyTargetMechs && target.RaceProps.IsMechanoid)
			{
				return target.PositionHeld.DistanceTo(parent.holder.PositionHeld) <= Props.range;
			}
			return false;
		}

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
			if(parent.holder.Faction == Faction.OfPlayer || DebugSettings.ShowDevGizmos)
            {
				Command_ToggleWithMouseoverAction command = new Command_ToggleWithMouseoverAction();
				command.defaultLabel = (Props.gismoTextKey + "_Label").Translate();
				command.defaultDesc = (Props.gismoTextKey + "_Desc").Translate();
				command.icon = UIIcon;
				command.isActive = () => active;
				command.range = Props.range;
				command.cell = parent.holder.PositionHeld;
				command.toggleAction = delegate
				{
					active = !active;
				};
				yield return command;
			}
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
			Scribe_Values.Look(ref active, "active", true);
        }
    }

	public class Command_ToggleWithMouseoverAction : Command_Toggle
    {
		public float range;

		public IntVec3 cell;
        public override void GizmoUpdateOnMouseover()
        {
			GenDraw.DrawRadiusRing(cell, range);
            base.GizmoUpdateOnMouseover();
        }
    }

	public class UpgradeCompReloadableProperties : UpgradeCompProperties
	{
		public ThingDef ammoDef;

		public int ammoCount = 1;

		public int reloadTicks = 180;

		public UpgradeCompReloadableProperties()
		{
			compClass = typeof(UpgradeCompReloadable);
		}
	}
	public abstract class UpgradeCompReloadable : UpgradeComp
	{
		public UpgradeCompReloadableProperties Props => (UpgradeCompReloadableProperties)props;

		public virtual int AmmoCount => Props.ammoCount;

		public virtual ThingDef AmmoDef => Props.ammoDef;

		public virtual int ReloadTicks => Props.reloadTicks;

		public virtual string ReloadLabel => parent.LabelCap;

		public virtual Texture2D ReloadIcon => parent.def.uiIcon;

		public abstract bool NeedsAutoReload { get; }

		public abstract bool NeedsReload { get; }

		public abstract void Reload(Thing ammo, bool autoReload, bool devReload = false);// if devReload is true, ammo is null, so use return
	}

	public class UpgradeCompReloadableProperties_Items : UpgradeCompReloadableProperties
	{
		public int maxResource;

		public int startingResource;

		[NoTranslate]
		public string gizmoKey;

		public Color barColor = Color.gray;

		public Color barHighlightColor = Color.white;

		public UpgradeCompReloadableProperties_Items()
		{
			compClass = typeof(UpgradeCompReloadable_Items);
		}

		public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
		{
			yield return new StatDrawEntry(MUStatDefOf.MU_MechUpgrades, "MU_Stat_ReloadCost".Translate() + " " + (gizmoKey + "_Label").Translate(), ((ammoCount > 1) ? ammoCount.ToString() + " " : "") + ammoDef.label, "MU_Stat_ReloadCost_Desc".Translate(), 2);
		}
	}

	public class UpgradeCompReloadable_Items : UpgradeCompReloadable
	{
		public new UpgradeCompReloadableProperties_Items Props => (UpgradeCompReloadableProperties_Items)props;

		public int maxToFill;

		public int resource;
		public override bool NeedsAutoReload => resource < maxToFill;

		public override bool NeedsReload => resource < Props.maxResource;

        public override void PostGenerated(bool generatedForHolder)
        {
            base.PostGenerated(generatedForHolder);
			resource = Props.startingResource;
			maxToFill = Props.startingResource;
		}
        public override void Reload(Thing ammo, bool autoReload, bool devReload = false)
		{
            if (devReload)
            {
				resource = Props.maxResource;
				return;
            }
            if (!NeedsReload || (!NeedsAutoReload && autoReload))
            {
				return;
            }
			int num = Props.ammoCount;
            if (autoReload)
            {
				num *= Mathf.Min(Mathf.FloorToInt(ammo.stackCount / num), Mathf.FloorToInt((maxToFill - resource) / num));
            }
			ammo.SplitOff(num).Destroy();
			resource += num / Props.ammoCount;
		}

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach(Gizmo g in base.CompGetGizmosExtra())
            {
				yield return g;
            }
			if(parent.holder.Faction == Faction.OfPlayer)
            {
				yield return new ReloadableUpgradeGizmo(this, (Props.gizmoKey + "_Title").Translate(), (Props.gizmoKey + "_Tooltip").Translate(), Props.barColor, Props.barHighlightColor);
			}
        }
        public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref maxToFill, "maxToFill", defaultValue: 0);
			Scribe_Values.Look(ref resource, "resource", defaultValue: 0);
		}
	}

	public class UpgradeCompProperties_MechCarrier : UpgradeCompReloadableProperties_Items
	{
		public int costPerPawn;

		public PawnKindDef spawnPawnKind;

		public int cooldownTicks = 900;

		public int maxPawnsToSpawn = 3;

		public EffecterDef spawnEffecter;

		public EffecterDef spawnedMechEffecter;

		public bool attachSpawnedEffecter;

		public bool attachSpawnedMechEffecter;

		[NoTranslate]
		public string gizmoTexPath = "UI/Gizmos/ReleaseWarUrchins";

		public UpgradeCompProperties_MechCarrier()
		{
			compClass = typeof(UpgradeComp_MechCarrier);
		}
	}

	public class UpgradeComp_MechCarrier : UpgradeCompReloadable_Items
	{
		public new UpgradeCompProperties_MechCarrier Props => (UpgradeCompProperties_MechCarrier)props;

		public int cooldownTicksRemaining;

		[Unsaved(false)]
		private Texture2D activateTex;

		public Texture2D UIIcon
		{
			get
			{
				if (!(activateTex != null))
				{
					return activateTex = ContentFinder<Texture2D>.Get(Props.gizmoTexPath);
				}
				return activateTex;
			}
		}

		public int MaxCanSpawn => Mathf.Min(Mathf.FloorToInt(resource / Props.costPerPawn), Props.maxPawnsToSpawn);

		public AcceptanceReport CanSpawn
		{
			get
			{
				if (parent.holder is Pawn pawn)
				{
					if (pawn.IsSelfShutdown())
					{
						return "SelfShutdown".Translate();
					}
					if (pawn.Faction == Faction.OfPlayer && !pawn.IsColonyMechPlayerControlled)
					{
						return false;
					}
					if (!pawn.Awake() || pawn.Downed || pawn.Dead || !pawn.Spawned)
					{
						return false;
					}
				}
				if (MaxCanSpawn <= 0)
				{
					return "MechCarrierNotEnoughResources".Translate();
				}
				if (cooldownTicksRemaining > 0)
				{
					return "CooldownTime".Translate() + " " + cooldownTicksRemaining.ToStringSecondsFromTicks();
				}
				return true;
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref cooldownTicksRemaining, "cooldownTicksRemaining", 0);
		}

		public override void CompTick()
		{
			base.CompTick();
			if (cooldownTicksRemaining > 0)
			{
				cooldownTicksRemaining--;
			}
		}
		public void TrySpawnPawns()
		{
			int maxCanSpawn = MaxCanSpawn;
			if (maxCanSpawn <= 0)
			{
				return;
			}
			PawnGenerationRequest request = new PawnGenerationRequest(Props.spawnPawnKind, parent.holder.Faction, PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: true, allowDead: false, allowDowned: false, canGeneratePawnRelations: true, mustBeCapableOfViolence: false, 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: false, allowFood: true, allowAddictions: true, inhabitant: false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, null, forceNoIdeo: false, forceNoBackstory: false, forbidAnyTitle: false, forceDead: false, null, null, null, null, null, 0f, DevelopmentalStage.Newborn);
			Lord lord = ((parent.holder is Pawn p) ? p.GetLord() : null);
			for (int i = 0; i < maxCanSpawn; i++)
			{
				Pawn pawn = PawnGenerator.GeneratePawn(request);
				GenSpawn.Spawn(pawn, parent.holder.Position, parent.holder.Map);
				lord?.AddPawn(pawn);
				resource -= Props.costPerPawn;
				if (Props.spawnedMechEffecter != null)
				{
					Effecter effecter = new Effecter(Props.spawnedMechEffecter);
					effecter.Trigger(Props.attachSpawnedMechEffecter ? ((TargetInfo)pawn) : new TargetInfo(pawn.Position, pawn.Map), TargetInfo.Invalid);
					effecter.Cleanup();
				}
			}
			cooldownTicksRemaining = Props.cooldownTicks;
			if (Props.spawnEffecter != null)
			{
				Effecter effecter2 = new Effecter(Props.spawnEffecter);
				effecter2.Trigger(Props.attachSpawnedEffecter ? ((TargetInfo)parent.holder) : new TargetInfo(parent.holder.Position, parent.holder.Map), TargetInfo.Invalid);
				effecter2.Cleanup();
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			if (!(parent.holder.IsColonyMech) || parent.holder.GetOverseer() == null)
			{
				yield break;
			}
			foreach (Gizmo item in base.CompGetGizmosExtra())
			{
				yield return item;
			}
			AcceptanceReport canSpawn = CanSpawn;
			Command_ActionWithCooldown act = new Command_ActionWithCooldown
			{
				cooldownPercentGetter = () => Mathf.InverseLerp(Props.cooldownTicks, 0f, cooldownTicksRemaining),
				action = delegate
				{
					TrySpawnPawns();
				},
				Disabled = !canSpawn.Accepted,
				icon = UIIcon,
				defaultLabel = "MechCarrierRelease".Translate(Props.spawnPawnKind.labelPlural),
				defaultDesc = "MechCarrierDesc".Translate(Props.maxPawnsToSpawn, Props.spawnPawnKind.labelPlural, Props.spawnPawnKind.label, Props.costPerPawn, Props.ammoDef.label)
			};
			if (!canSpawn.Reason.NullOrEmpty())
			{
				act.Disable(canSpawn.Reason);
			}
			if (DebugSettings.ShowDevGizmos)
			{
				if (cooldownTicksRemaining > 0)
				{
					Command_Action command_Action = new Command_Action();
					command_Action.defaultLabel = "DEV: Reset cooldown";
					command_Action.action = delegate
					{
						cooldownTicksRemaining = 0;
					};
					yield return command_Action;
				}
			}
			yield return act;
		}
	}

	public class UpgradeCompProperties_QualityOffset : UpgradeCompProperties
	{
		public int offset;

		public UpgradeCompProperties_QualityOffset()
		{
			compClass = typeof(UpgradeComp_QualityOffset);
		}

		public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
		{
			yield return new StatDrawEntry(MUStatDefOf.MU_MechUpgrades, "MU_QualityOffset".Translate(), "+" + offset.ToString(), "MU_QualityOffset_Desc".Translate(), 17);
		}
	}
	public class UpgradeComp_QualityOffset : UpgradeComp
	{
		public UpgradeCompProperties_QualityOffset Props => (UpgradeCompProperties_QualityOffset)props;

		public int Offset => Props.offset;

		public override IEnumerable<string> ExtraDescStrings()
		{
			yield return "MU_QualityOffset".Translate() + ": +" + Offset.ToString();
		}
	}

	public class UpgradeCompProperties_Shield : UpgradeCompProperties
	{
		public int startingTicksToReset = 3200;

		public float minDrawSize = 1.55f;

		public float maxDrawSize = 1.7f;

		public float energyLossPerDamage = 0.033f;

		public float energyOnReset = 0.2f;

		public StatDef maxEnergyStat;

		public StatDef energyGainStat;

		public float baseMaxEnergy = 1f;

		public float baseEnergyGain = 0.35f;

		public UpgradeCompProperties_Shield()
		{
			compClass = typeof(UpgradeComp_Shield);
		}

		public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
		{
			yield return new StatDrawEntry(MUStatDefOf.MU_MechUpgrades, "MU_GivesPersonalShield".Translate(), "Yes".Translate(), "MU_GivesPersonalShield_Desc".Translate(), 18);
		}
	}
	[StaticConstructorOnStartup]
	public class UpgradeComp_Shield : UpgradeComp
	{
		public UpgradeCompProperties_Shield Props => (UpgradeCompProperties_Shield)props;

		protected float energy;

		public override IEnumerable<string> ExtraDescStrings()
		{
			yield return "MU_GivesPersonalShield".Translate();
		}

		protected int ticksToReset = -1;

		protected int lastKeepDisplayTick = -9999;

		private Vector3 impactAngleVect;

		private int lastAbsorbDamageTick = -9999;

		private const float MaxDamagedJitterDist = 0.05f;

		private const int JitterDurationTicks = 8;

		private static readonly Material BubbleMat = MaterialPool.MatFrom("Other/ShieldBubble", ShaderDatabase.Transparent);

		public float EnergyMax => Props.baseMaxEnergy * (Props.maxEnergyStat != null ? Mech.GetStatValue(Props.maxEnergyStat) : 1f);//holder.GetStatValue(StatDefOf.EnergyShieldEnergyMax);

		private float EnergyGainPerTick => Props.baseEnergyGain * (Props.energyGainStat != null ? Mech.GetStatValue(Props.energyGainStat) : 1f) / 60f;

		public float Energy => energy;

        public override void Added()
        {
			energy = EnergyMax;
            base.Added();
        }

        public ShieldState ShieldState
		{
			get
			{
				if(Mech == null)
                {
					Log.Error(parent.LabelCap + " has null holder but is inside pawn");
				}
				if (Mech.DeadOrDowned || Mech.IsCharging() || Mech.IsSelfShutdown())
				{
					return ShieldState.Disabled;
				}
				CompCanBeDormant comp = Mech.GetComp<CompCanBeDormant>();
				if (comp != null && !comp.Awake)
				{
					return ShieldState.Disabled;
				}
				if (ticksToReset <= 0)
				{
					return ShieldState.Active;
				}
				return ShieldState.Resetting;
			}
		}

		protected bool ShouldDisplay
		{
			get
			{
				if (!Mech.Spawned || Mech.Dead || Mech.Downed)
				{
					return false;
				}
				return true;
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref energy, "energy", 0f);
			Scribe_Values.Look(ref ticksToReset, "ticksToReset", -1);
			Scribe_Values.Look(ref lastKeepDisplayTick, "lastKeepDisplayTick", 0);
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo item in base.CompGetGizmosExtra())
			{
				yield return item;
			}
			if (Find.Selector.SingleSelectedThing == Mech)
			{
				Gizmo_MechShieldStatus gizmo_EnergyShieldStatus = new Gizmo_MechShieldStatus();
				gizmo_EnergyShieldStatus.shield = this;
				yield return gizmo_EnergyShieldStatus;
			}
		}

		public override void CompTick()
		{
			base.CompTick();
			if (Mech == null)
			{
				energy = 0f;
			}
			else if (ShieldState == ShieldState.Resetting)
			{
				ticksToReset--;
				if (ticksToReset <= 0)
				{
					Reset();
				}
			}
			else if (ShieldState == ShieldState.Active)
			{
				energy += EnergyGainPerTick;
				if (energy > EnergyMax)
				{
					energy = EnergyMax;
				}
			}
		}

		public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
		{
			absorbed = false;
			if (ShieldState != 0 || Mech == null || !Mech.Spawned)
			{
				return;
			}
			if (dinfo.Def == DamageDefOf.EMP)
			{
				energy = 0f;
				Break();
			}
			else if (!dinfo.Def.ignoreShields && (dinfo.Def.isRanged || dinfo.Def.isExplosive))
			{
				energy -= dinfo.Amount * Props.energyLossPerDamage;
				if (energy < 0f)
				{
					Break();
				}
				else
				{
					AbsorbedDamage(dinfo);
				}
				absorbed = true;
			}
		}

		public void KeepDisplaying()
		{
			lastKeepDisplayTick = Find.TickManager.TicksGame;
		}

		private void AbsorbedDamage(DamageInfo dinfo)
		{
			SoundDefOf.EnergyShield_AbsorbDamage.PlayOneShot(new TargetInfo(Mech.Position, Mech.Map));
			impactAngleVect = Vector3Utility.HorizontalVectorFromAngle(dinfo.Angle);
			Vector3 loc = Mech.TrueCenter() + impactAngleVect.RotatedBy(180f) * 0.5f;
			float num = Mathf.Min(10f, 2f + dinfo.Amount / 10f);
			FleckMaker.Static(loc, Mech.Map, FleckDefOf.ExplosionFlash, num);
			int num2 = (int)num;
			for (int i = 0; i < num2; i++)
			{
				FleckMaker.ThrowDustPuff(loc, Mech.Map, Rand.Range(0.8f, 1.2f));
			}
			lastAbsorbDamageTick = Find.TickManager.TicksGame;
			KeepDisplaying();
		}

		private void Break()
		{
			float scale = Mathf.Lerp(Props.minDrawSize, Props.maxDrawSize, energy);
			EffecterDefOf.Shield_Break.SpawnAttached(Mech, Mech.MapHeld, scale);
			FleckMaker.Static(Mech.TrueCenter(), Mech.Map, FleckDefOf.ExplosionFlash, 12f);
			for (int i = 0; i < 6; i++)
			{
				FleckMaker.ThrowDustPuff(Mech.TrueCenter() + Vector3Utility.HorizontalVectorFromAngle(Rand.Range(0, 360)) * Rand.Range(0.3f, 0.6f), Mech.Map, Rand.Range(0.8f, 1.2f));
			}
			energy = 0f;
			ticksToReset = Props.startingTicksToReset;
		}

		private void Reset()
		{
			if (Mech.Spawned)
			{
				SoundDefOf.EnergyShield_Reset.PlayOneShot(new TargetInfo(Mech.Position, Mech.Map));
				FleckMaker.ThrowLightningGlow(Mech.TrueCenter(), Mech.Map, 3f);
			}
			ticksToReset = -1;
			energy = Mathf.Min(Props.energyOnReset, EnergyMax);
		}

		public override void PostDraw()
		{
			if (ShieldState == ShieldState.Active && ShouldDisplay)
			{
				float num = Mathf.Lerp(Props.minDrawSize, Props.maxDrawSize, energy);
				Vector3 drawPos = Mech.Drawer.DrawPos;
				drawPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
				int num2 = Find.TickManager.TicksGame - lastAbsorbDamageTick;
				if (num2 < 8)
				{
					float num3 = (float)(8 - num2) / 8f * 0.05f;
					drawPos += impactAngleVect * num3;
					num -= num3;
				}
				float angle = Rand.Range(0, 360);
				Vector3 s = new Vector3(num, 1f, num);
				Matrix4x4 matrix = default(Matrix4x4);
				matrix.SetTRS(drawPos, Quaternion.AngleAxis(angle, Vector3.up), s);
				Graphics.DrawMesh(MeshPool.plane10, matrix, BubbleMat, 0);
			}
		}
	}

	[StaticConstructorOnStartup]
	public class Gizmo_MechShieldStatus : Gizmo
	{
		public UpgradeComp_Shield shield;

		private static readonly Texture2D FullShieldBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.2f, 0.24f));

		private static readonly Texture2D EmptyShieldBarTex = SolidColorMaterials.NewSolidColorTexture(Color.clear);

		public Gizmo_MechShieldStatus()
		{
			Order = -100f;
		}

		public override float GetWidth(float maxWidth)
		{
			return 140f;
		}

		public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
		{
			Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
			Rect rect2 = rect.ContractedBy(6f);
			Widgets.DrawWindowBackground(rect);
			Rect rect3 = rect2;
			rect3.height = rect.height / 2f;
			Text.Font = GameFont.Tiny;
			Widgets.Label(rect3, "ShieldInbuilt".Translate().Resolve());
			Rect rect4 = rect2;
			rect4.yMin = rect2.y + rect2.height / 2f;
			float fillPercent = shield.Energy / Mathf.Max(1f, shield.EnergyMax);
			Widgets.FillableBar(rect4, fillPercent, FullShieldBarTex, EmptyShieldBarTex, doBorder: false);
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(rect4, (shield.Energy * 100f).ToString("F0") + " / " + (shield.EnergyMax * 100f).ToString("F0"));
			Text.Anchor = TextAnchor.UpperLeft;
			TooltipHandler.TipRegion(rect2, "ShieldPersonalTip".Translate());
			return new GizmoResult(GizmoState.Clear);
		}
	}

	public class UpgradeCompProperties_SingleGraphic : UpgradeCompProperties
    {
		public List<string> texPaths = new List<string>();

		public Vector2? drawSize = null;

		public Color? color = null;

		public bool drawSizeIsFactor = false;

		public UpgradeCompProperties_SingleGraphic()
		{
			compClass = typeof(UpgradeComp_SingleGraphic);
		}

		public override IEnumerable<string> ConfigErrors(MechUpgradeDef parentDef)
		{
			foreach (string s in base.ConfigErrors(parentDef))
			{
				yield return s;
			}
			if (texPaths.NullOrEmpty())
			{
				yield return $"MechUpgradeDef {parentDef.defName} has no texPaths in UpgradeCompProperties_SingleGraphic.";
			}
		}
	}

	public class UpgradeComp_SingleGraphic : UpgradeComp
    {
		public UpgradeCompProperties_SingleGraphic Props => (UpgradeCompProperties_SingleGraphic)props;
	}

	public class UpgradeCompProperties_Graphics : UpgradeCompProperties
	{
		public List<Option> graphicOptions;

		public class Option
		{
			public ThingDef mech;

			public PawnRenderNodeProperties props;
		}

		public PawnRenderNodeProperties defaultProps;

		public UpgradeCompProperties_Graphics()
		{
			compClass = typeof(UpgradeComp_Graphics);
		}

        public override IEnumerable<string> ConfigErrors(MechUpgradeDef parentDef)
        {
			foreach(string s in base.ConfigErrors(parentDef))
            {
				yield return s;
            }
			if (graphicOptions.NullOrEmpty())
			{
				yield return $"MechUpgradeDef {parentDef.defName} has no graphicOptions in UpgradeCompProperties_Graphics.";
			}
		}
    }
	public class UpgradeComp_Graphics : UpgradeComp
	{
		public UpgradeCompProperties_Graphics Props => (UpgradeCompProperties_Graphics)props;

		public override List<PawnRenderNode> CompRenderNodes()
        {
			List<PawnRenderNode> list = base.CompRenderNodes();
			PawnRenderNodeProperties properties = null;
			if (Props.graphicOptions.TryRandomElement((UpgradeCompProperties_Graphics.Option o)=> o.mech == Mech.def, out var result))
            {
				properties = result.props;
			}
            else if(Props.defaultProps != null)
            {
				properties = Props.defaultProps;
			}
			properties = PropertiesFinalized(properties);
			list.Add((PawnRenderNode)Activator.CreateInstance(properties.nodeClass, Mech, properties, Mech.Drawer.renderer.renderTree));
			return list;
		}
		public virtual PawnRenderNodeProperties PropertiesFinalized(PawnRenderNodeProperties properties)
        {
			UpgradeComp_SingleGraphic comp = parent.GetComp<UpgradeComp_SingleGraphic>();
			if(comp != null)
            {
				UpgradeCompProperties_SingleGraphic cp = comp.Props;
				if (cp.color != null)
                {
					properties.color = cp.color;
				}
				if (cp.drawSize != null)
				{
                    if (cp.drawSizeIsFactor)
                    {
						properties.drawSize.x *= cp.drawSize.Value.x;
						properties.drawSize.y *= cp.drawSize.Value.y;
					}
                    else
                    {
						properties.drawSize = cp.drawSize.Value;
					}
				}
				if (!cp.texPaths.NullOrEmpty())
				{
					properties.texPath = cp.texPaths[Mathf.Min(parent.charges ?? 0, cp.texPaths.Count - 1)];
				}
			}
			return properties;
        }

		public override void Notify_ChargeUsed(Ability ability)
        {
			Mech.Drawer.renderer.SetAllGraphicsDirty();
		}
    }

	public class UpgradeCompProperties_TurretGun : UpgradeCompProperties
	{
		public ThingDef turretDef;

		public float angleOffset;

		public bool autoAttack = true;

		public List<PawnRenderNodeProperties> renderNodeProperties;

		public UpgradeCompProperties_TurretGun()
		{
			compClass = typeof(UpgradeCompTurretGun);
		}

        public override IEnumerable<string> ConfigErrors(MechUpgradeDef parentDef)
        {
			if (renderNodeProperties.NullOrEmpty())
			{
				yield break;
			}
			foreach (PawnRenderNodeProperties renderNodeProperty in renderNodeProperties)
			{
				if (!typeof(PawnRenderNode_UpgradeTurret).IsAssignableFrom(renderNodeProperty.nodeClass))
				{
					yield return "contains nodeClass which is not PawnRenderNode_UpgradeTurret or subclass thereof.";
				}
			}
		}

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
			if (turretDef != null)
			{
				yield return new StatDrawEntry(StatCategoryDefOf.PawnCombat, "Turret".Translate(), turretDef.LabelCap, "Stat_Thing_TurretDesc".Translate(), 5600, null, Gen.YieldSingle(new Dialog_InfoCard.Hyperlink(turretDef)));
			}
		}
    }
	public class UpgradeCompTurretGun : UpgradeComp, IAttackTargetSearcher
	{
		private const int StartShootIntervalTicks = 10;

		private static readonly CachedTexture ToggleTurretIcon = new CachedTexture("UI/Gizmos/ToggleTurret");

		public Thing gun;

		protected int burstCooldownTicksLeft;

		protected int burstWarmupTicksLeft;

		protected LocalTargetInfo currentTarget = LocalTargetInfo.Invalid;

		private bool fireAtWill = true;

		private LocalTargetInfo lastAttackedTarget = LocalTargetInfo.Invalid;

		private int lastAttackTargetTick;

		public float curRotation;

		public Thing Thing => Mech;

		public UpgradeCompProperties_TurretGun Props => (UpgradeCompProperties_TurretGun)props;

		public Verb CurrentEffectiveVerb => AttackVerb;

		public LocalTargetInfo LastAttackedTarget => lastAttackedTarget;

		public int LastAttackTargetTick => lastAttackTargetTick;

		public CompEquippable GunCompEq => gun?.TryGetComp<CompEquippable>();

		public Verb AttackVerb => GunCompEq?.PrimaryVerb;

		private bool WarmingUp => burstWarmupTicksLeft > 0;

		private bool CanShoot
		{
			get
			{
				if (Mech != null)
				{
					if (!Mech.Spawned || Mech.Downed || Mech.Dead || !Mech.Awake())
					{
						return false;
					}
					if (Mech.stances.stunner.Stunned)
					{
						return false;
					}
					if (Mech.IsColonyMechPlayerControlled && !fireAtWill)
					{
						return false;
					}
				}
                else
                {
					return false;
                }
				CompCanBeDormant compCanBeDormant = Mech.TryGetComp<CompCanBeDormant>();
				if (compCanBeDormant != null && !compCanBeDormant.Awake)
				{
					return false;
				}
				return true;
			}
		}

		public bool AutoAttack => Props.autoAttack;

        private void MakeGun()
		{
			gun = ThingMaker.MakeThing(Props.turretDef);
			if (Mech != null) { UpdateGunVerbs(); }
		}

		private void UpdateGunVerbs()
		{
			List<Verb> allVerbs = gun.TryGetComp<CompEquippable>().AllVerbs;
			for (int i = 0; i < allVerbs.Count; i++)
			{
				Verb verb = allVerbs[i];
				verb.caster = Mech;
				verb.castCompleteCallback = delegate
				{
					burstCooldownTicksLeft = AttackVerb.verbProps.defaultCooldownTime.SecondsToTicks();
				};
			}
		}

		public override void CompTick()
		{
			base.CompTick();
			if (!CanShoot)
			{
				return;
			}
			if (currentTarget.IsValid)
			{
				curRotation = (currentTarget.Cell.ToVector3Shifted() - Mech.DrawPos).AngleFlat() + Props.angleOffset;
			}
			AttackVerb.VerbTick();
			if (AttackVerb.state == VerbState.Bursting)
			{
				return;
			}
			if (WarmingUp)
			{
				burstWarmupTicksLeft--;
				if (burstWarmupTicksLeft == 0)
				{
					AttackVerb.TryStartCastOn(currentTarget, surpriseAttack: false, canHitNonTargetPawns: true, preventFriendlyFire: false, nonInterruptingSelfCast: true);
					lastAttackTargetTick = Find.TickManager.TicksGame;
					lastAttackedTarget = currentTarget;
				}
				return;
			}
			if (burstCooldownTicksLeft > 0)
			{
				burstCooldownTicksLeft--;
			}
			if (burstCooldownTicksLeft <= 0 && Mech.IsHashIntervalTick(10))
			{
				currentTarget = (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(this, TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable);
				if (currentTarget.IsValid)
				{
					burstWarmupTicksLeft = 1;
				}
				else
				{
					ResetCurrentTarget();
				}
			}
		}

        public override void Added()
        {
            base.Added();
			MakeGun();
			UpdateGunVerbs();
		}

        public override void Removed()
        {
            base.Removed();
			gun.Destroy();
			gun = null;
        }

        private void ResetCurrentTarget()
		{
			currentTarget = LocalTargetInfo.Invalid;
			burstWarmupTicksLeft = 0;
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo item in base.CompGetGizmosExtra())
			{
				yield return item;
			}
			if (Mech.IsColonyMechPlayerControlled)
			{
				Command_ToggleWithMouseoverAction command_Toggle = new Command_ToggleWithMouseoverAction();
				command_Toggle.defaultLabel = "CommandToggleTurret".Translate();
				command_Toggle.defaultDesc = "CommandToggleTurretDesc".Translate();
				command_Toggle.isActive = () => fireAtWill;
				command_Toggle.cell = Mech.PositionHeld;
				command_Toggle.range = AttackVerb.EffectiveRange;
				command_Toggle.icon = ToggleTurretIcon.Texture;
				command_Toggle.toggleAction = delegate
				{
					fireAtWill = !fireAtWill;
				};
				yield return command_Toggle;
			}
		}

		public override List<PawnRenderNode> CompRenderNodes()
		{
			if (!Props.renderNodeProperties.NullOrEmpty())
			{
				List<PawnRenderNode> list = new List<PawnRenderNode>();
				{
					foreach (PawnRenderNodeProperties renderNodeProperty in Props.renderNodeProperties)
					{
						PawnRenderNode_UpgradeTurret pawnRenderNode_Turret = (PawnRenderNode_UpgradeTurret)Activator.CreateInstance(renderNodeProperty.nodeClass, Mech, renderNodeProperty, Mech.Drawer.renderer.renderTree);
						pawnRenderNode_Turret.turretComp = this;
						list.Add(pawnRenderNode_Turret);
					}
					return list;
				}
			}
			return base.CompRenderNodes();
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref burstCooldownTicksLeft, "burstCooldownTicksLeft", 0);
			Scribe_Values.Look(ref burstWarmupTicksLeft, "burstWarmupTicksLeft", 0);
			Scribe_TargetInfo.Look(ref currentTarget, "currentTarget");
			Scribe_Deep.Look(ref gun, "gun");
			Scribe_Values.Look(ref fireAtWill, "fireAtWill", defaultValue: true);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (Mech != null)
				{
					if (gun == null)
					{
						Log.Error("CompTurrentGun had null gun after loading. Recreating.");
						MakeGun();
					}
					else
					{
						UpdateGunVerbs();
					}
				}
			}
		}
	}

	public class PawnRenderNode_UpgradeTurret : PawnRenderNode
	{
		public UpgradeCompTurretGun turretComp;

		public PawnRenderNode_UpgradeTurret(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
			: base(pawn, props, tree)
		{
		}

		public override Graphic GraphicFor(Pawn pawn)
		{
			return GraphicDatabase.Get<Graphic_Single>(turretComp.Props.turretDef.graphicData.texPath, ShaderDatabase.Cutout);
		}
	}

	public class PawnRenderNodeWorker_UpgradeTurret : PawnRenderNodeWorker
	{
		public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
		{
			Quaternion result = base.RotationFor(node, parms);
			if (node is PawnRenderNode_UpgradeTurret pawnRenderNode_Turret)
			{
				result *= pawnRenderNode_Turret.turretComp.curRotation.ToQuat();
			}
			return result;
		}
	}
}