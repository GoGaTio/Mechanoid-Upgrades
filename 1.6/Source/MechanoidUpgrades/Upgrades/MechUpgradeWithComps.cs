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
    public class MechUpgradeWithComps : MechUpgrade
    {
		public List<UpgradeComp> comps = new List<UpgradeComp>();

		public T GetComp<T>() where T : UpgradeComp
		{
			if (comps.NullOrEmpty())
			{
				return null;
			}
			for (int i = 0; i < comps.Count; i++)
			{
				if (comps[i] is T)
				{
					return comps[i] as T;
				}
			}
			return null;
		}

		public List<T> GetListOfComp<T>() where T : UpgradeComp
		{
			List<T> list = new List<T>();
			if (comps.NullOrEmpty())
			{
				return list;
			}
			for (int i = 0; i < comps.Count; i++)
			{
				if (comps[i] is T)
				{
					list.Add(comps[i] as T);
				}
			}
			return list;
		}

        public override void Tick()
        {
			if (!comps.NullOrEmpty())
			{
				foreach (MU.UpgradeComp c in comps)
				{
					c.CompTick();
				}
			}
		}

        public override void OnAdded(Pawn p)
        {
            base.OnAdded(p);
			if (!def.comps.NullOrEmpty())
			{
				foreach (MU.UpgradeComp c in comps)
				{
					c.Added();
				}
			}
		}

        public override void OnRemoved(Pawn p)
        {
			if (!def.comps.NullOrEmpty())
			{
				foreach (MU.UpgradeComp c in comps)
				{
					c.Removed();
				}
			}
			base.OnRemoved(p);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
			if (!def.comps.NullOrEmpty())
			{
				foreach (MU.UpgradeComp c in comps)
				{
					c.PostSpawnSetup(respawningAfterLoad);
				}
			}
		}

        public override void PostGenerated(bool generatedForHolder)
        {
			if (!def.comps.NullOrEmpty())
			{
				foreach (MU.UpgradeComp c in comps)
				{
					c.PostGenerated(generatedForHolder);
				}
			}
		}

        public override void ExposeData()
        {
            base.ExposeData();
			if (Scribe.mode == LoadSaveMode.LoadingVars)
			{
				InitializeComps();
			}
			if (comps != null)
			{
				for (int i = 0; i < comps.Count; i++)
				{
					comps[i].PostExposeData();
				}
			}
		}

        public override void PostDraw()
        {
            base.PostDraw();
			if (comps != null)
			{
				foreach (UpgradeComp u in comps)
				{
					u.PostDraw();
				}
			}
		}

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);
			if (absorbed || comps == null)
			{
				return;
			}
			for (int i = 0; i < comps.Count; i++)
			{
				comps[i].PostPreApplyDamage(ref dinfo, out absorbed);
				if (absorbed)
				{
					break;
				}
			}
		}

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
			foreach (UpgradeComp c in comps)
			{
				c.PostPostApplyDamage(dinfo, totalDamageDealt);
			}
		}

        public override void Notify_ChargeUsed(Ability ability)
        {
			foreach (UpgradeComp c in comps)
			{
				c.Notify_ChargeUsed(ability);
			}
		}

		public override float GetStatFactor(StatDef stat)
		{
			float num = 1f;
			foreach (UpgradeComp c in comps)
			{
				num *= c.GetStatFactor(stat);
			}
			return num;
		}

		public override float GetStatOffset(StatDef stat)
		{
			float num = 0f;
			foreach (UpgradeComp c in comps)
			{
				num += c.GetStatOffset(stat);
			}
			return num;
		}

		public override IEnumerable<Gizmo> GetGizmosExtra()
        {
			if (!comps.NullOrEmpty())
			{
				foreach (MU.UpgradeComp c in comps)
				{
					foreach (Gizmo g in c.CompGetGizmosExtra())
					{
						yield return g;
					}
				}
			}
		}

		public override void GetOffsetsExplanation(StatDef stat, StringBuilder sb)
		{
			foreach (UpgradeComp c in comps)
			{
				c.GetOffsetsExplanation(stat, sb);
			}
		}

		public override void GetFactorsExplanation(StatDef stat, StringBuilder sb)
		{
			foreach (UpgradeComp c in comps)
			{
				c.GetFactorsExplanation(stat, sb);
			}
		}

		public override List<PawnRenderNode> CompRenderNodes()
		{
			List<PawnRenderNode> list = base.CompRenderNodes();
			foreach (UpgradeComp c in comps)
			{
				list.AddRange(c.CompRenderNodes());
			}
			return list;
		}

		public override void Notify_AbandonedAtTile(int tile)
		{
			foreach (UpgradeComp c in comps)
			{
				c.Notify_AbandonedAtTile(tile);
			}
		}

		public override void Notify_Downed()
		{
			foreach (UpgradeComp c in comps)
			{
				c.Notify_Downed();
			}
		}

		public override void Notify_KilledPawn(Pawn pawn)
		{
			foreach (UpgradeComp c in comps)
			{
				c.Notify_KilledPawn(pawn);
			}
		}

		public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
		{
			foreach (UpgradeComp c in comps)
			{
				c.Notify_Killed(prevMap, dinfo);
			}
		}

		public override void DrawGUIOverlay()
		{
			foreach (UpgradeComp c in comps)
			{
				c.DrawGUIOverlay();
			}
		}

		public override void PostDeSpawn(Map map)
		{
			foreach (UpgradeComp c in comps)
			{
				c.PostDeSpawn(map);
			}
		}

		public override void PostDestroy(DestroyMode mode, Map previousMap)
		{
			foreach (UpgradeComp c in comps)
			{
				c.PostDestroy(mode, previousMap);
			}
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			foreach (UpgradeComp c in comps)
			{
				c.PostDrawExtraSelectionOverlays();
			}
		}

		public override IEnumerable<StatDrawEntry> MechSpecialDisplayStats()
		{
			foreach (UpgradeComp c in comps)
			{
				foreach (StatDrawEntry s in c.MechSpecialDisplayStats())
				{
					yield return s;
				}
			}
		}

		public override void Initialize()
        {
			InitializeComps();
			base.Initialize();
        }
        private void InitializeComps()
		{
			comps = new List<UpgradeComp>();
			if (def.comps.NullOrEmpty())
			{
				return;
			}
			for (int i = 0; i < def.comps.Count; i++)
			{
				UpgradeComp comp = null;
				try
				{
					comp = (UpgradeComp)Activator.CreateInstance(def.comps[i].compClass);
					comp.parent = this;
					comps.Add(comp);
					comp.Initialize(def.comps[i]);
				}
				catch (Exception ex)
				{
					Log.Error("Could not instantiate or initialize an UpgradeComp: " + ex);
					comps.Remove(comp);
				}
			}
		}
	}
}