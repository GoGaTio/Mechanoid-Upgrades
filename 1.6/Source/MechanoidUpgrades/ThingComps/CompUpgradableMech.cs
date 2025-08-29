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
	public class CompProperties_UpgradableMechanoid : CompProperties
	{
		public CompProperties_UpgradableMechanoid()
		{
			compClass = typeof(CompUpgradableMechanoid);
		}

		public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
		{
			foreach (StatDrawEntry item in base.SpecialDisplayStats(req))
			{
				yield return item;
			}
			yield return new StatDrawEntry(StatCategoryDefOf.Mechanoid, "MU_IsUpgradableMechanoid".Translate(), "Yes".Translate(), "MU_IsUpgradableMechanoid_Desc".Translate(), 99999);
		}
	}

	public class CompUpgradableMechanoid : ThingComp
	{
		public List<MechUpgrade> upgrades = new List<MechUpgrade>();
		public float? CommandDistanceSquared => CommandDistance * CommandDistance;

		public UpgradeCompReloadable compForReload;

		private int compForReloadIndex = -1;

		private MechUpgradeDef upgradeForReload;

        [Unsaved(false)]
        private Dictionary<DamageDef, float> cachedDamageFactors = new Dictionary<DamageDef, float>();

		public void DirtyUpgrades()
		{
            cachedDamageFactors.Clear();
        }

        public float FactorForDamage(DamageInfo dinfo)
        {
            if (dinfo.Def == null || upgrades.NullOrEmpty())
            {
                return 1f;
            }
            if (cachedDamageFactors.TryGetValue(dinfo.Def, out var value))
            {
                return value;
            }
            float num = 1f;
            for (int i = 0; i < upgrades.Count; i++)
            {
                MechUpgrade upgrade = upgrades[i];
                if (upgrade.def.damageFactors.NullOrEmpty())
                {
                    continue;
                }
                for (int j = 0; j < upgrade.def.damageFactors.Count; j++)
                {
                    if (upgrade.def.damageFactors[j].damageDef == dinfo.Def)
                    {
                        num *= upgrade.def.damageFactors[j].factor;
                    }
                }
            }
            cachedDamageFactors.Add(dinfo.Def, num);
            return num;
        }

        public bool RemoteControllable
		{
			get
			{
				if (upgrades.NullOrEmpty())
				{
					return false;
				}
				foreach (MechUpgrade u in upgrades)
				{
					if (u.def.allowRemoteControl)
					{
						return true;
					}
				}
				return false;
			}
		}
		public float? CommandDistance
		{
			get
			{
				if (upgrades.NullOrEmpty())
				{
					return null;
				}
				float? num = 0f;
				foreach (MechUpgrade u in upgrades)
				{
					if (u.def.commandRange != null)
					{
						num += u.def.commandRange;
					}
				}
				if (num == 0f)
				{
					return null;
				}
				return num;
			}
		}

		public MechUpgrade AddUpgrade(MechUpgradeDef def)
		{
			MechUpgrade upgrade = MechUpgradeUtility.MakeUpgrade(def);
			upgrades.Add(upgrade);
			upgrade.OnAdded(Mech);
			DirtyUpgrades();
            return upgrade;
		}

		public MechUpgrade AddUpgrade(MechUpgrade upgrade)
		{
			upgrades.Add(upgrade);
			upgrade.OnAdded(Mech);
            DirtyUpgrades();
            return upgrade;
		}

		public void RemoveUpgrade(MechUpgrade upgrade)
		{
			upgrade.OnRemoved(Mech);
			upgrades.Remove(upgrade);
            DirtyUpgrades();
        }

		public Pawn Mech => (Pawn)parent;

		public bool autoReload = true;

		public CompProperties_UpgradableMechanoid Props => (CompProperties_UpgradableMechanoid)props;

		public override void ReceiveCompSignal(string signal)
		{
			if (signal == "MU_ClearUpgrades")
			{
				foreach (MechUpgrade u in upgrades)
				{
					u.OnRemoved(Mech);
				}
				upgrades.Clear();
			}
		}

		public override float GetStatFactor(StatDef stat)
		{
			float num = 1f;
			if (upgrades.NullOrEmpty())
			{
				return num;
			}
			foreach (MU.MechUpgrade u in upgrades)
			{
				if (u.def.statFactors != null)
				{
					num *= u.def.statFactors.GetStatFactorFromList(stat);
				}
				num *= u.GetStatFactor(stat);
			}
			return num;
		}

		public override float GetStatOffset(StatDef stat)
		{
			float num = 0f;
			if (upgrades.NullOrEmpty())
			{
				return num;
			}
			foreach (MU.MechUpgrade u in upgrades)
			{
				if (u.def.statOffsets != null)
				{
					num += u.def.statOffsets.GetStatOffsetFromList(stat);
				}
				num += u.GetStatOffset(stat);
			}
			return num;
		}

        public override void GetStatsExplanation(StatDef stat, StringBuilder sb, string whitespace = "")
        {
			if (GetStatOffset(stat) != 0f)
			{
				sb.AppendLine();
				sb.AppendLine("MU_StatOffsetsFromUpgrades".Translate() + ":");
				foreach (MU.MechUpgrade u in upgrades)
				{
					float num1 = u.def.statOffsets.GetStatOffsetFromList(stat);
					if (num1 != 0f)
					{
						sb.AppendLine("    " + u.LabelCap + ": " + num1.ToStringByStyle(stat.ToStringStyleUnfinalized, ToStringNumberSense.Offset));
					}
					u.GetOffsetsExplanation(stat, sb);
				}
			}
			if (GetStatFactor(stat) != 1f)
			{
				sb.AppendLine();
				sb.AppendLine("MU_StatFactorsFromUpgrades".Translate() + ":");
				foreach (MU.MechUpgrade u in upgrades)
				{
					float num1 = u.def.statFactors.GetStatFactorFromList(stat);
					if (num1 != 1f)
					{
						sb.AppendLine("    " + u.LabelCap + ": " + num1.ToStringByStyle(ToStringStyle.Integer, ToStringNumberSense.Factor));
					}
					u.GetFactorsExplanation(stat, sb);
				}
			}
		}
		public void Notify_ChargeUsed(Ability ability)
		{
			foreach (MechUpgrade u in upgrades.Where((MechUpgrade u1) => u1.def.ability == ability.def))
			{
				u.Notify_ChargeUsed(ability);
			}
		}

		public override void Initialize(CompProperties props)
		{
			base.Initialize(props);
			if (upgrades == null)
			{
				upgrades = new List<MechUpgrade>();
			}
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			foreach (MechUpgrade u in upgrades)
			{
				u.PostSpawnSetup(respawningAfterLoad);
			}
		}

		public override void PostDraw()
		{
			base.PostDraw();
			if (upgrades != null)
			{
				foreach (MechUpgrade u in upgrades)
				{
					u.PostDraw();
				}
			}
			if (CommandDistance != null && Mech.Spawned)
			{
				Pawn overseer = Mech.GetOverseer();
				if (overseer != null && overseer.mechanitor.AnySelectedDraftedMechs)
				{
					GenDraw.DrawRadiusRing(Mech.Position, CommandDistance.Value, Color.white);
				}
			}
		}

		public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
		{
            dinfo.SetAmount(dinfo.Amount * FactorForDamage(dinfo));
            bool a = false;
			if (upgrades != null)
			{
				foreach (MechUpgrade u in upgrades)
				{
					u.PostPreApplyDamage(ref dinfo, out absorbed);
					if (absorbed)
					{
						a = true;
						break;
					}
				}
			}
			absorbed = a;
		}

		public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
		{
			foreach (MU.MechUpgrade u in upgrades)
			{
				u.PostPostApplyDamage(dinfo, totalDamageDealt);
			}
			base.PostPostApplyDamage(dinfo, totalDamageDealt);
		}

		private static readonly CachedTexture ForceReload = new CachedTexture("UI/Gizmos/MU_ForceReload");

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo item in base.CompGetGizmosExtra())
			{
				yield return item;
			}
			if (!upgrades.NullOrEmpty())
			{
				foreach (MU.MechUpgrade u in upgrades)
				{
					foreach (Gizmo g in u.GetGizmosExtra())
					{
						yield return g;
					}
				}
			}
			if (Mech.IsColonyMechPlayerControlled && HasAnyReloadable)
			{
				if (!Mech.DeadOrDowned && NeedsReload)
				{
					Command_Action command_Action2 = new Command_Action();
					command_Action2.defaultLabel = "MU_ForceReload".Translate() + "...";
					command_Action2.defaultDesc = "MU_ForceReload_Desc".Translate();
					command_Action2.icon = ForceReload.Texture;
					command_Action2.action = delegate
					{
						List<FloatMenuOption> list = new List<FloatMenuOption>();
						List<Ability> allAbilities = Mech.abilities.AllAbilitiesForReading.Where((Ability a) => a.CompOfType<MU.CompAbilityEffect_ReloadableUpgrade>() != null && a.CompOfType<MU.CompAbilityEffect_ReloadableUpgrade>().NeedsReload).ToList();
						for (int j = 0; j < allAbilities.Count; j++)
						{
							Ability a = allAbilities[j];
							AcceptanceReport r = CanReload(a, null, out var job);
							if (r)
							{
								list.Add(new FloatMenuOption(a.def.LabelCap, delegate { Mech.jobs.StartJob(job, JobCondition.InterruptForced); }, a.def.uiIcon, Color.white));
							}
							else
							{
								if (!r.Reason.NullOrEmpty())
								{
									list.Add(new FloatMenuOption(a.def.LabelCap + " " + r.Reason, null, a.def.uiIcon, Color.white));
								}
							}
						}
						if (!upgrades.NullOrEmpty())
						{
							foreach (MechUpgrade u in upgrades)
							{
								if (u is MechUpgradeWithComps w)
								{
									foreach (UpgradeCompReloadable c in w.GetListOfComp<UpgradeCompReloadable>())
									{
										AcceptanceReport r = CanReload(null, c, out var job);
										if (r)
										{
											list.Add(new FloatMenuOption(c.ReloadLabel, delegate { compForReload = c; Mech.jobs.StartJob(job, JobCondition.InterruptForced); }, c.ReloadIcon, Color.white));
										}
										else
										{
											if (!r.Reason.NullOrEmpty())
											{
												list.Add(new FloatMenuOption(c.ReloadLabel + " " + r.Reason, null, c.ReloadIcon, Color.white));
											}
										}
									}
								}
							}
						}
						Find.WindowStack.Add(new FloatMenu(list));
					};
					yield return command_Action2;
				}
			}
		}

		public bool NeedsReload
		{
			get
			{
				if (!HasAnyReloadable)
				{
					return false;
				}
				if (Mech.abilities.AllAbilitiesForReading.Any((Ability a) => a.CompOfType<MU.CompAbilityEffect_ReloadableUpgrade>()?.NeedsReload == true))
				{
					return true;
				}
				if (upgrades.Any((MechUpgrade u) => u is MechUpgradeWithComps w && w.GetListOfComp<UpgradeCompReloadable>().Any((UpgradeCompReloadable c) => c.NeedsReload)))
				{
					return true;
				}
				return false;
			}
		}

		private bool HasAnyReloadable
		{
			get
			{
				if (Mech.abilities.AllAbilitiesForReading.Any((Ability a) => a.CompOfType<MU.CompAbilityEffect_ReloadableUpgrade>() != null))
				{
					return true;
				}
				if (upgrades.Any((MechUpgrade u) => u is MechUpgradeWithComps w && !w.GetListOfComp<UpgradeCompReloadable>().NullOrEmpty()))
				{
					return true;
				}
				return false;
			}
		}

		public AcceptanceReport CanReload(Ability ability, UpgradeCompReloadable comp, out Job job)
		{
			job = null;
			int num = 0;
			bool flag = autoReload;
			ThingDef ammoDef = null;
			int reloadTicks = 1;
			if (comp != null)
			{
				if (!comp.NeedsReload)
				{
					return false;
				}
				if (comp.NeedsAutoReload)
				{
					flag = true;
				}
				num = comp.AmmoCount;
				ammoDef = comp.AmmoDef;
				reloadTicks = comp.ReloadTicks;
			}
			else
			{
				MU.CompAbilityEffect_ReloadableUpgrade compA = ability.CompOfType<MU.CompAbilityEffect_ReloadableUpgrade>();
				if (!compA.NeedsReload)
				{
					return false;
				}
				num = compA.Props.ammoCount;
				ammoDef = compA.Props.ammoDef;
				reloadTicks = compA.Props.reloadTicks;
			}
			if (!Mech.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
			{
				return "MU_CannotReload_CannotManipulate".Translate();
			}
			if (Mech.carryTracker.AvailableStackSpace(ammoDef) < num)
			{
				return "MU_CannotReload_NoStackSpace".Translate();
			}
			if (!Mech.MapHeld.listerThings.AnyThingWithDef(ammoDef))
			{
				return "MU_CannotReload_NoEnoughAmmo".Translate(ammoDef.label);
			}
			List<Thing> list = RefuelWorkGiverUtility.FindEnoughReservableThings(Mech, Mech.Position, new IntRange(num, num), (Thing t) => t.def == ammoDef && t.stackCount >= num);
			if (list.NullOrEmpty())
			{
				return "MU_CannotReload_NoEnoughAmmo".Translate(ammoDef.label);
			}
			Thing ammo = GenClosest.ClosestThing_Global_Reachable(Mech.Position, Mech.Map, list, PathEndMode.Touch, TraverseParms.For(Mech));
			job = JobMaker.MakeJob(MUJobDefOf.MU_ReloadUpgrade, ammo);
			job.count = Mathf.FloorToInt(ammo.stackCount / num) * num;
			job.ingestTotalCount = flag;
			if (ability != null)
			{
				job.ability = ability;
			}
			job.takeInventoryDelay = reloadTicks;
			return true;
		}

		public override List<PawnRenderNode> CompRenderNodes()
		{
			if (!upgrades.NullOrEmpty() && parent is Pawn pawn)
			{
				List<PawnRenderNode> list = new List<PawnRenderNode>();
				foreach (MU.MechUpgrade u in upgrades)
				{
					list.AddRange(u.CompRenderNodes());
				}
				return list;
			}
			return base.CompRenderNodes();
		}
		public override void CompTick()
		{
			if (Mech.Spawned && Mech.IsHashIntervalTick(300))
			{
				if (Mech.CurJobDef != MUJobDefOf.MU_ReloadUpgrade)
				{
					compForReload = null;
				}
				LessonAutoActivator.TeachOpportunity(MUMiscDefOf.MU_MechanoidUpgrades, OpportunityType.Important);
			}
			base.CompTick();
			if (!upgrades.NullOrEmpty())
			{
				foreach (MechUpgrade u in upgrades)
				{
					u.Tick();
				}
			}
		}

        public override void Notify_AbandonedAtTile(PlanetTile tile)
        {
			foreach (MechUpgrade u in upgrades)
			{
				u.Notify_AbandonedAtTile(tile);
			}
			base.Notify_AbandonedAtTile(tile);
        }
        public override void Notify_Downed()
		{
			foreach (MechUpgrade u in upgrades)
			{
				u.Notify_Downed();
			}
			base.Notify_Downed();
		}

		public override void Notify_KilledPawn(Pawn pawn)
		{
			foreach (MechUpgrade u in upgrades)
			{
				u.Notify_KilledPawn(pawn);
			}
			base.Notify_KilledPawn(pawn);
		}

		public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
		{
			foreach (MechUpgrade u in upgrades)
			{
				u.Notify_Killed(prevMap, dinfo);
			}
			base.Notify_Killed(prevMap, dinfo);
		}

		public override void DrawGUIOverlay()
		{
			foreach (MechUpgrade u in upgrades)
			{
				u.DrawGUIOverlay();
			}
			base.DrawGUIOverlay();
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			foreach (MechUpgrade u in upgrades)
			{
				u.PostDrawExtraSelectionOverlays();
			}
			base.PostDrawExtraSelectionOverlays();
		}

		public override void PostDestroy(DestroyMode mode, Map previousMap)
		{
			foreach (MechUpgrade u in upgrades)
			{
				u.PostDestroy(mode, previousMap);
			}
			base.PostDestroy(mode, previousMap);
		}

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
			foreach (MechUpgrade u in upgrades)
			{
				u.PostDeSpawn(map);
			}
			base.PostDeSpawn(map, mode);
        }

		public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
		{
			List<Dialog_InfoCard.Hyperlink> tmpHyperlinks = new List<Dialog_InfoCard.Hyperlink>();
			StringBuilder sb = new StringBuilder("");
			if (!upgrades.NullOrEmpty())
			{
				foreach (MechUpgrade u in upgrades)
				{
					sb.Append(u.LabelCap + "; ");
					tmpHyperlinks.Add(new Dialog_InfoCard.Hyperlink(u.def));
					foreach (StatDrawEntry s in u.MechSpecialDisplayStats())
					{
						yield return s;
					}
				}
				sb.Remove(sb.Length - 1, 1);
			}
			else
			{
				sb.Append("MU_NoUpgradesInside".Translate());
			}
			yield return new StatDrawEntry(StatCategoryDefOf.Mechanoid, "MU_UpgradesInsideMech".Translate(), sb.ToString(), "MU_UpgradesInsideMech_Desc".Translate(), 1, null, tmpHyperlinks);
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			if (upgrades == null)
			{
				upgrades = new List<MechUpgrade>();
			}
			if (Scribe.mode == LoadSaveMode.Saving && !upgrades.NullOrEmpty() && compForReload != null)
			{
				for (int i = 0; i < compForReload.parent.comps.Count; i++)
				{
					if (compForReload.parent.comps[i] == compForReload)
					{
						compForReloadIndex = i;
					}
				}
			}
			Scribe_Defs.Look(ref upgradeForReload, "upgradeForReload");
			Scribe_Values.Look(ref compForReloadIndex, "compForReloadIndex", defaultValue: -1);
			Scribe_Collections.Look(ref upgrades, "upgrades", LookMode.Deep);
			Scribe_Values.Look(ref autoReload, "autoReload", defaultValue: true);
			if (Scribe.mode == LoadSaveMode.PostLoadInit && !upgrades.NullOrEmpty())
			{
				if (compForReloadIndex != -1 && upgradeForReload != null)
				{
					MechUpgradeWithComps ru = upgrades.FirstOrDefault((MechUpgrade u) => u.def == upgradeForReload) as MechUpgradeWithComps;
					if (ru != null && ru.comps[compForReloadIndex] is UpgradeCompReloadable)
					{
						compForReload = ru.comps[compForReloadIndex] as UpgradeCompReloadable;
					}
				}
			}
		}
	}
}