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
using System.Timers;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Xml.Xsl;
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
	public static class MechUpgradeUtility
	{
		public static MechanoidUpgradesSettings Settings
		{
			get
			{
				if (settings == null)
				{
					settings = LoadedModManager.GetMod<MechanoidUpgradesMod>().GetSettings<MechanoidUpgradesSettings>();
				}
				return settings;
			}
		}

		public static float? upgradeChanceOverride = null;

		private static MechanoidUpgradesSettings settings; 
		
		private static List<MechUpgradeDef> upgradesDatabase = null;

		public static List<MechUpgradeDef> UpgradesDatabase
		{
			get
			{
				if (upgradesDatabase == null)
				{
					upgradesDatabase = DefDatabase<MechUpgradeDef>.AllDefsListForReading;
				}
				return upgradesDatabase;
			}
		}

        [DebugAction("Pawns", "Upgrade Mech", false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap, requiresBiotech = true)]
		private static void DEV_UpgradeMech(Pawn p)
		{
            if (p.HasComp<MU.CompUpgradableMechanoid>())
            {
				UpgradeMech(p, 1f);
			}
            else
            {
				Messages.Message("Cannot upgrade pawn", null, MessageTypeDefOf.RejectInput);
            }
		}
		public static void UpgradeMech(Pawn mech, float percent, bool generated = false)
        {
			UpgradeMech(mech, Mathf.Max(1, Mathf.RoundToInt(Mathf.RoundToInt(MaxUpgradePoints(mech) * percent))), generated);
		}
		public static void UpgradeMech(Pawn mech, int amount, bool generated = false)
        {
			CompUpgradableMechanoid comp = mech.TryGetComp<CompUpgradableMechanoid>();
			if(comp == null)
            {
				return;
            }
			List<MechUpgradeDef> list1 = UpgradesDatabase.Where((MechUpgradeDef def) => def.commonality > 0f && def.CommonalityFinal(mech) > 0f && def.CanAdd(mech.def)).ToList();
            if (list1.NullOrEmpty())
            {
				return;
            }
			for(int i = amount; i > 0;)
            {
				list1 = list1.Where((MechUpgradeDef d1) => d1.CanAdd(mech.def, comp.upgrades) && d1.upgradePoints <= (MaxUpgradePoints(mech) - CurrentPoints(mech))).ToList();
                if (list1.NullOrEmpty() || list1.Sum((MechUpgradeDef d)=> d.CommonalityFinal(mech)) < 1f)
                {
					return;
                }
				MechUpgradeDef def = list1.RandomElementByWeight((MechUpgradeDef d2) => d2.CommonalityFinal(mech));
				MechUpgrade u = generated ? MakeUpgrade(def, mech) : MakeUpgrade(def);
				comp.upgrades.Add(u);
				u.OnAdded(mech);
				i -= def.upgradePoints;
            }
        }

		public static void UpgradeMechCerebrex(Pawn mech)
		{
			CompUpgradableMechanoid comp = mech.TryGetComp<CompUpgradableMechanoid>();
			if (comp == null)
			{
				return;
			}
			DEV_RemoveAll(mech);
			List<MechUpgradeDef> list1 = UpgradesDatabase.Where((MechUpgradeDef def) => def.commonality > 0f && def.CommonalityFinal(mech) > 0f && (def.defName.ElementAt(def.defName.Length - 2) != '_' || def.defName.Last() == 'A' || def.defName.Last() == 'S')  && def.CanAdd(mech.def)).ToList();
			if (list1.NullOrEmpty())
			{
				return;
			}
			for (int i = MaxUpgradePoints(mech); i > 0;)
			{
				list1 = list1.Where((MechUpgradeDef d1) => d1.CanAdd(mech.def, comp.upgrades) && d1.upgradePoints <= (MaxUpgradePoints(mech) - CurrentPoints(mech))).ToList();
				if (list1.NullOrEmpty() || list1.Sum((MechUpgradeDef d) => d.CommonalityFinal(mech)) < 1f)
				{
					return;
				}
				MechUpgradeDef def = list1.RandomElementByWeight((MechUpgradeDef d2) => d2.CommonalityFinal(mech));
				MechUpgrade u = MakeUpgrade(def);
				comp.upgrades.Add(u);
				u.OnAdded(mech);
				i -= def.upgradePoints;
			}
		}

		public static void ApplyCombination(Pawn mech, UpgradeCombinationDef combination, bool generated = true)
        {
			CompUpgradableMechanoid comp = mech.TryGetComp<CompUpgradableMechanoid>();
			if (comp == null)
            {
				return;
            }
			int num = MaxUpgradePoints(mech);
			foreach(List<MechUpgradeDef> l in combination.upgrades)
            {
				MechUpgradeDef def = l.RandomElement();
				if (def.upgradePoints > num)
                {
					return;
                }
				comp.AddUpgrade(def);
				num += def.upgradePoints;
			}
		}

		public static Thing ItemFromUpgrade(MU.MechUpgrade u)
        {
			Thing t = ThingMaker.MakeThing(u.def.linkedThingDef);
			t.TryGetComp<MU.CompMechUpgrade>().upgrade = u;
			return t;
        }

		public static IEnumerable<MechUpgradeOperation> GetOperationsFromLists(List<MU.MechUpgrade> list1, List<MU.MechUpgrade> list2)
		{
			List<MU.MechUpgrade> listRemoved = new List<MU.MechUpgrade>();
			List<MU.MechUpgrade> listAdded = new List<MU.MechUpgrade>();
			bool empty1 = false;
			bool empty2 = false;
			if (list1.NullOrEmpty())
			{
				empty1 = true;
			}
			if (list2.NullOrEmpty())
			{
				empty2 = true;
			}
			if (empty1 && empty2)
			{
				yield break;
			}
			if (empty1)
			{
				foreach (MU.MechUpgrade u1 in list2)
				{
					yield return new MechUpgradeOperation(u1, UpgradeOperationType.Add);
				}
				yield break;
			}
			if (empty2)
			{
				foreach (MU.MechUpgrade u2 in list1)
				{
					yield return new MechUpgradeOperation(u2, UpgradeOperationType.Remove);
				}
				yield break;
			}
			foreach (MU.MechUpgrade u3 in list1)
			{
				if (!list2.Contains(u3))
				{
					listRemoved.Add(u3);
				}
			}
			foreach (MU.MechUpgrade u4 in list2)
			{
				if (!list1.Contains(u4))
				{
					listAdded.Add(u4);
				}
			}
			foreach (MU.MechUpgrade u6 in listAdded)
			{
				yield return new MechUpgradeOperation(u6, UpgradeOperationType.Add);
			}
			foreach (MU.MechUpgrade u5 in listRemoved)
			{
				yield return new MechUpgradeOperation(u5, UpgradeOperationType.Remove);
			}
		}

		public static int MaxUpgradePoints(Thing t)
		{
			int result = 0;
			if (t is Pawn)
			{
				result = ((int)t.GetStatValueForPawn(MUStatDefOf.MU_Upgradability, t as Pawn));
			}
			else
			{
				result = t.TryGetComp<MU.CompUpgradesStorage>().Props.maxCapacity;
			}
			return result;
		}

		public static int CurrentPoints(Pawn mech)
        {
			CompUpgradableMechanoid comp = mech.TryGetComp<MU.CompUpgradableMechanoid>();
			if(comp != null)
            {
				return CurrentPoints(comp.upgrades);
			}
			return 0;
        }

		public static int CurrentPoints(List<MU.MechUpgrade> list)
		{
			int cur = 0;
			if (!list.NullOrEmpty())
			{
				foreach (MU.MechUpgrade u in list)
				{
					cur += u.def.upgradePoints;
				}
			}
			return cur;
		}
		public static void DrawPoints(Rect rect, Thing t, List<MU.MechUpgrade> list, TextAnchor anchor)
		{
			Widgets.BeginGroup(rect);
			int max = MaxUpgradePoints(t);
			int cur = CurrentPoints(list);
			Text.Anchor = anchor;
			if(cur > max)
            {
				GUI.color = ColoredText.FactionColor_Hostile;
			}
			Widgets.Label(rect, cur + "/" + max);
			GUI.color = Color.white;
			Text.Anchor = TextAnchor.UpperLeft;
			Widgets.EndGroup();
		}
		public static void DoListing(Rect rect1, List<MU.MechUpgrade> upgrades, bool isActive, List<MU.MechUpgrade> upgradesForChange, ref Vector2 scrollPosition, ref float viewHeight, bool showDropButton = false)
		{
			Widgets.BeginGroup(rect1);
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;
			Rect outRect = new Rect(0f, 30f, rect1.width, rect1.height - 35f);
			Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);
			Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
			float num = 0f;
			if (!upgrades.NullOrEmpty())
			{
				for (int i = 0; i < upgrades.Count; i++)
				{
					Rect rect2 = DoInterface(0f, num, upgrades[i], viewRect.width, isActive, showDropButton, upgradesForChange);
					num += rect2.height + 6f;
					if (Mouse.IsOver(rect2))
					{
						Widgets.DrawHighlight(rect2);

						TooltipHandler.TipRegion(rect2, DescWithStats(upgrades[i]));
					}
					if (Widgets.ButtonInvisible(rect2, false))
					{
						if (isActive)
						{
							SoundDefOf.Click.PlayOneShotOnCamera();
							upgradesForChange.Add(upgrades[i]);
						}
					}
				}
			}
			if (Event.current.type == EventType.Layout)
			{
				viewHeight = num + 60f;
			}
			Widgets.EndScrollView();
			Widgets.EndGroup();
		}

		public static Rect DoInterface(float x, float y, MU.MechUpgrade u, float width, bool isActive = false, bool showDropButton = false, List<MU.MechUpgrade> upgradesForChange = null)
		{
			Rect rect = new Rect(x, y, width, 54f);
			float num = 0f;
			rect.height += num;
			Color color = Color.white;
			Widgets.DrawAltRect(rect);
			Text.Font = GameFont.Small;
			Widgets.BeginGroup(rect);
			Rect rect2 = new Rect(0f, 0f, 24f, 24f);
			GUI.color = Color.white;
			Widgets.Label(new Rect(52f, 0f, rect.width - 28f, rect.height + 5f), u.LabelCap);
			Widgets.Label(new Rect(52f, 27f, rect.width - 28f, rect.height + 5f), "MU_UpgradePoints".Translate() + ": " + u.def.upgradePoints.ToString());
			Rect rect4 = new Rect(rect.width - 24f, 0f, 24f, 24f);
			Rect rect5 = new Rect(3, 3, 48f, 48f);
			Rect rect6 = new Rect(rect.width - 24f, rect.height - 24f, 24f, 24f);
			Widgets.DrawTextureFitted(rect5, u.def.uiIcon, 1);
			if (Widgets.ButtonImage(rect4, TexButton.Info, color, color * GenUI.SubtleMouseoverColor))
			{
				Find.WindowStack.Add(new Dialog_InfoCard(u.def));
				SoundDefOf.Click.PlayOneShotOnCamera();
			}
			if (showDropButton)
			{
				if (Widgets.ButtonImage(rect6, TexButton.Drop, color, color * GenUI.SubtleMouseoverColor))
				{
					upgradesForChange.Add(u);
					SoundDefOf.Click.PlayOneShotOnCamera();
				}
			}
			Widgets.EndGroup();
			Text.Font = GameFont.Small;
			GUI.color = Color.white;
			return rect;
		}

		public static string DescWithStats(MU.MechUpgrade upgrade)
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine(upgrade.def.description);
			if(upgrade.def.allowRemoteControl || upgrade.def.commandRange != null || !upgrade.def.statFactors.NullOrEmpty() || !upgrade.def.statOffsets.NullOrEmpty() || upgrade.def.ability != null || (!upgrade.def.comps.NullOrEmpty() && (upgrade as MechUpgradeWithComps).comps.Any((UpgradeComp c)=> !c.ExtraDescStrings().EnumerableNullOrEmpty())))
            {
				sb.AppendLine();
            }
            else
            {
				return sb.ToString();
			}
			if (upgrade.def.allowRemoteControl)
			{
				sb.AppendLine(" - " + "MU_AllowsRemoteControl".Translate());
			}
			if (upgrade.def.commandRange != null)
			{
				sb.AppendLine(" - " + "MU_CommandRange".Translate().CapitalizeFirst() + ": " + upgrade.def.commandRange.Value.ToStringByStyle(ToStringStyle.Integer));
			}
			if (!upgrade.def.statFactors.NullOrEmpty())
			{
				foreach (StatModifier fa in upgrade.def.statFactors)
				{
					sb.AppendLine(" - " + fa.stat.LabelCap + ": " + fa.ToStringAsFactor);
				}
			}
			if (!upgrade.def.statOffsets.NullOrEmpty())
			{
				foreach (StatModifier of in upgrade.def.statOffsets)
				{
					sb.AppendLine(" - " + of.stat.LabelCap + ": " + of.ValueToStringAsOffset);
				}
			}
			if (upgrade.def.ability != null)
			{
				sb.AppendLine(" - " + "GivesAbility".Translate().CapitalizeFirst() + ": " + upgrade.def.ability.LabelCap);
			}
            if (upgrade is MechUpgradeWithComps muwc)
            {
				foreach (UpgradeComp c in muwc.comps)
				{
					foreach (string s in c.ExtraDescStrings())
					{
						sb.AppendLine(" - " + s);
					}
				}
			}
			return sb.ToString();
		}

		public static List<FloatMenuOption> DebugOptions(Thing t)
        {
			return new List<FloatMenuOption>
			{
				new FloatMenuOption("Add upgrade", delegate
				{
					Find.WindowStack.Add(new Dialog_DebugOptionListLister(DEV_AddUpgrade(t)));
				}),
				new FloatMenuOption("Remove upgrade", delegate
				{
					Find.WindowStack.Add(new Dialog_DebugOptionListLister(DEV_RemoveUpgrade(t)));
				}),
				new FloatMenuOption("Remove all", delegate
				{
					DEV_RemoveAll(t);
				}),
				new FloatMenuOption("Reload all", delegate
				{
					DEV_ReloadAll(t);
				}),
				(t is Pawn p1 ? new FloatMenuOption("Save set", delegate
                {
					CompUpgradableMechanoid comp = p1.TryGetComp<CompUpgradableMechanoid>();
                    if (!comp.upgrades.NullOrEmpty())
                    {
						UpgradeSet set = new UpgradeSet();
						set.upgrades = new List<MechUpgradeDef>();
						foreach (MechUpgrade u in comp.upgrades)
						{
							set.upgrades.Add(u.def);
						}
						Find.WindowStack.Add(new Dialog_SaveUpgradeSet(set));
					}
				}) : new FloatMenuOption("Save set", null)),
				(t is Pawn p2 ? new FloatMenuOption("Load set", delegate
				{
					Find.WindowStack.Add(new Dialog_UpgradeSetList_Load(delegate (UpgradeSet set)
					{
						DEV_RemoveAll(t);
						CompUpgradableMechanoid comp = p2.TryGetComp<CompUpgradableMechanoid>();
						bool flag1 = false;
						foreach(MechUpgradeDef d in set.upgrades)
						{
							if (!d.CanAdd(t.def, comp.upgrades) || MechUpgradeUtility.MaxUpgradePoints(t) - MechUpgradeUtility.CurrentPoints(comp.upgrades) < d.upgradePoints)
							{
								flag1 = true;
							}
							else
							{
								comp.AddUpgrade(d);
							}
						}
						if (flag1)
						{
							Messages.Message("MU_CannotAddSet_CannotAddUpgrade".Translate(), null, MessageTypeDefOf.RejectInput, false);
						}
					}));
				}) : new FloatMenuOption("Load set", null))
			};
		}

		public static MechUpgrade MakeUpgrade(MechUpgradeDef def)
        {
			return MakeUpgrade(def, null);
		}

		public static MechUpgrade MakeUpgrade(MechUpgradeDef def, Pawn startingHolder)
		{
			MechUpgrade u = (MechUpgrade)Activator.CreateInstance(def.upgradeClass);
			u.def = def;
			u.Initialize();
			u.ChangeHolder(startingHolder);
			u.PostGenerated(startingHolder != null);
			return u;
		}

		public static List<DebugMenuOption> DEV_AddUpgrade(Thing t)
        {
			List<DebugMenuOption> list = new List<DebugMenuOption>();
			MU.CompUpgradableMechanoid Mech = t.TryGetComp<MU.CompUpgradableMechanoid>();
			MU.CompUpgradesStorage Storage = t.TryGetComp<MU.CompUpgradesStorage>();
			foreach (MU.MechUpgradeDef u in DefDatabase<MU.MechUpgradeDef>.AllDefs)
			{
				MechUpgrade u1 = MakeUpgrade(u);
				list.Add(new DebugMenuOption(AddLabel(u1, t), DebugMenuOptionMode.Action, delegate
				{
					if (Mech != null)
					{
						Mech.AddUpgrade(u1);
					}
					if (Storage != null)
					{
						Storage.innerContainer.TryAdd(MechUpgradeUtility.ItemFromUpgrade(u1));
					}
				}));
			}
			return list;
		}

		public static string AddLabel(MU.MechUpgrade u, Thing t)
        {
			string s = u.def.defName;
			if (t is Pawn p && (!u.def.CanAdd((t.def), t.TryGetComp<MU.CompUpgradableMechanoid>().upgrades) || MaxUpgradePoints(t) - CurrentPoints(p) < u.def.upgradePoints))
            {
				s += "[NO]";
            }
			if (t.HasComp<CompUpgradesStorage>() && t.TryGetComp<CompUpgradesStorage>().Space < u.def.upgradePoints)
			{
				s += "[NO]";
			}
			return s;
        }

		public static List<DebugMenuOption> DEV_RemoveUpgrade(Thing t)
		{
			MU.CompUpgradableMechanoid Mech = t.TryGetComp<MU.CompUpgradableMechanoid>();
			MU.CompUpgradesStorage Storage = t.TryGetComp<MU.CompUpgradesStorage>();
			List<DebugMenuOption> list1 = new List<DebugMenuOption>();
			List<MU.MechUpgrade> list2 = new List<MechUpgrade>();
			if(Mech != null)
            {
				list2.AddRange(Mech.upgrades);
			}
			if (Storage != null && Storage.innerContainer.Any)
			{
				foreach(Thing item in Storage.innerContainer)
                {
					MU.CompMechUpgrade comp = item.TryGetComp<MU.CompMechUpgrade>();
					if(comp!= null)
                    {
						list2.Add(comp.upgrade);
                    }
                }
			}
			foreach (MU.MechUpgrade u in list2)
			{
				list1.Add(new DebugMenuOption(u.LabelCap, DebugMenuOptionMode.Action, delegate
				{
					RemoveAction(u, t);
				}));
			}
			return list1;
		}

		public static void RemoveAction(MU.MechUpgrade u, Thing t)
        {
			MU.CompUpgradableMechanoid Mech = t.TryGetComp<MU.CompUpgradableMechanoid>();
			MU.CompUpgradesStorage Storage = t.TryGetComp<MU.CompUpgradesStorage>();
			if (Mech != null)
            {
				Mech.RemoveUpgrade(u);
            }
			if(Storage != null)
            {
				if(Storage.innerContainer.TryRandomElement((Thing thing) => thing.HasComp<MU.CompMechUpgrade>() && thing.TryGetComp<MU.CompMechUpgrade>().upgrade == u, out var r))
                {
					Storage.innerContainer.Remove(r);
				}
				
            }
		}
		public static void DEV_RemoveAll(Thing t)
		{
			MU.CompUpgradableMechanoid Mech = t.TryGetComp<MU.CompUpgradableMechanoid>();
			MU.CompUpgradesStorage Storage = t.TryGetComp<MU.CompUpgradesStorage>();
			if (t is Pawn && Mech != null)
			{
				foreach(MU.MechUpgrade u in Mech.upgrades)
                {
					u.OnRemoved(t as Pawn);
                }
				Mech.upgrades.Clear();
				return;
			}
			if(Storage != null)
            {
				Storage.innerContainer.ClearAndDestroyContents();
            }
		}

		public static void DEV_ReloadAll(Thing t)
		{
			CompUpgradableMechanoid comp = t.TryGetComp<CompUpgradableMechanoid>();
			if (comp == null || !(t is Pawn))
            {
				return;
			}
			foreach (MechUpgrade u in comp.upgrades)
			{
				if(u is MechUpgradeWithComps w)
                {
					foreach (UpgradeCompReloadable c in w.GetListOfComp<UpgradeCompReloadable>())
					{
						if (c.NeedsReload)
						{
							c.Reload(null, false, true);
						}
					}
				}
			}
			Pawn p = t as Pawn;
			List<Ability> list = p.abilities.AllAbilitiesForReading.Where((Ability a) => a.CompOfType<MU.CompAbilityEffect_ReloadableUpgrade>() != null && a.CompOfType<MU.CompAbilityEffect_ReloadableUpgrade>().NeedsReload).ToList();
            if (list.NullOrEmpty())
            {
				return;
            }
            foreach(Ability a in list)
            {
				a.CompOfType<MU.CompAbilityEffect_ReloadableUpgrade>().Reload(null, true);
			}
		}
	}
}