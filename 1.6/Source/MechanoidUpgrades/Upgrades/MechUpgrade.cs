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
	public class MechUpgrade : IExposable
	{
		public string LabelCap => def.LabelCap;

		public MechUpgradeDef def;

		public int? charges;

		public Pawn holder;

		public List<UpgradeCompReloadable> Reloadables()
        {
			if(this is MechUpgradeWithComps u)
            {
				return u.GetListOfComp<UpgradeCompReloadable>();
			}
			return new List<UpgradeCompReloadable>();
        }

		public virtual void ChangeHolder(Pawn newHolder)
		{
			holder = newHolder;
		}

		

		public virtual IEnumerable<Gizmo> GetGizmosExtra()
		{
			return Enumerable.Empty<Gizmo>();
		}

		public virtual void Tick()
		{
		}

		public virtual void OnAdded(Pawn p)
		{
			ChangeHolder(p);
			if (def.ability != null)
			{
				p.abilities.GainAbility(def.ability);
				p.abilities.GetAbility(def.ability).RemainingCharges = charges ?? def.ability.charges;
			}
			p.Drawer.renderer.SetAllGraphicsDirty();

		}
		public virtual void OnRemoved(Pawn p)
		{
			ChangeHolder(null);
			if (def.ability != null)
			{
				charges = p.abilities.GetAbility(def.ability).RemainingCharges;
				p.abilities.RemoveAbility(def.ability);
			}
			p.Drawer.renderer.SetAllGraphicsDirty();
		}

		public virtual void PostSpawnSetup(bool respawningAfterLoad)
		{
			
		}

		public virtual void PostGenerated(bool generatedForHolder)
		{
			
		}
		public virtual void ExposeData()
		{
			Scribe_Defs.Look(ref def, "def");
			Scribe_References.Look(ref holder, "holder", saveDestroyedThings: true);
			Scribe_Values.Look(ref charges, "charges");
		}

		public virtual void PostDraw()
		{
		}

		public virtual void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
		{
			absorbed = false;
		}

		public virtual void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
		{
		}

		public virtual void Notify_ChargeUsed(Ability ability)
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

		public virtual void DrawGUIOverlay()
		{
		}

		public virtual void PostDeSpawn(Map map)
		{
		}

		public virtual void PostDestroy(DestroyMode mode, Map previousMap)
		{
		}

		public virtual void PostDrawExtraSelectionOverlays()
		{
		}

		public virtual IEnumerable<StatDrawEntry> MechSpecialDisplayStats()
		{
			return Enumerable.Empty<StatDrawEntry>();
		}

		public virtual void Initialize()
		{
			if (def.ability != null && def.ability.comps.Any((AbilityCompProperties comp) => comp is CompProperties_AbilityReloadableUpgrade))
			{
				charges = def.ability.charges;
			}
		}
	}
}