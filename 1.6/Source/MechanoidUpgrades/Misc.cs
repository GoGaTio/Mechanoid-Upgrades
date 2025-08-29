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
using HarmonyLib;

namespace MU
{

	public class ScenPart_Upgrades : ScenPart
	{
		public List<PawnKindDef> mechs = new List<PawnKindDef>();

		public List<MechUpgradeDef> upgrades = new List<MechUpgradeDef>();
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref upgrades, "upgrades", LookMode.Def);
			Scribe_Collections.Look(ref mechs, "mechs", LookMode.Def);
			if(upgrades == null)
            {
				upgrades = new List<MechUpgradeDef>();
			}
			if (mechs == null)
			{
				mechs = new List<PawnKindDef>();
			}
		}

		private List<PawnKindDef> mechDefs;

		public List<PawnKindDef> MechDefs
        {
            get
            {
                if (mechDefs.NullOrEmpty())
                {
					mechDefs = DefDatabase<PawnKindDef>.AllDefsListForReading.Where((PawnKindDef p)=> p.race.race.IsMechanoid && p.race.HasComp<CompUpgradableMechanoid>()).ToList();
				}
				return mechDefs;
            }
        }
		public override void DoEditInterface(Listing_ScenEdit listing)
		{
			Rect scenPartRect = listing.GetScenPartRect(this, ((upgrades.Count + mechs.Count + 2) * (ScenPart.RowHeight + 2f)) + 5f);
			float num = scenPartRect.yMin;
			List<PawnKindDef> mechsToRemove = new List<PawnKindDef>();
			List<MechUpgradeDef> upgradesToRemove = new List<MechUpgradeDef>();
			if (!mechs.NullOrEmpty())
            {
				for (int i = 0; i < mechs.Count; i++)
				{
					if(i >= mechs.Count)
                    {
						break;
                    }
					if (Widgets.ButtonText(new Rect(scenPartRect.xMin, num, scenPartRect.width, ScenPart.RowHeight), mechs[i].LabelCap))
					{
						mechsToRemove.Add(mechs[i]);
					}
					num += ScenPart.RowHeight + 2f;
				}
			}
			if (Widgets.ButtonText(new Rect(scenPartRect.xMin, num, scenPartRect.width, ScenPart.RowHeight), "Add".Translate()))
			{
				List<FloatMenuOption> list = new List<FloatMenuOption>();
				list.Add(new FloatMenuOption("RandomMech".Translate().CapitalizeFirst(), delegate
				{
					mechs.Add(MechDefs.RandomElement());
				}));
				foreach (PawnKindDef possibleMech in MechDefs)
				{
					PawnKindDef localKind = possibleMech;
					list.Add(new FloatMenuOption(localKind.LabelCap, delegate
					{
						mechs.Add(localKind);
					}));
				}
				Find.WindowStack.Add(new FloatMenu(list));
			}
			num += ScenPart.RowHeight + 2f;
			Widgets.DrawLineHorizontal(scenPartRect.xMin, num, scenPartRect.width);
			num += 2f;
			if (!upgrades.NullOrEmpty())
			{
				for (int j = 0; j < upgrades.Count; j++)
				{
					if (j >= upgrades.Count)
					{
						break;
					}
					if (Widgets.ButtonText(new Rect(scenPartRect.xMin, num, scenPartRect.width, ScenPart.RowHeight), upgrades[j].LabelCap))
					{
						upgradesToRemove.Add(upgrades[j]);
					}
					num += ScenPart.RowHeight + 2f;
				}
			}
			if (Widgets.ButtonText(new Rect(scenPartRect.xMin, num, scenPartRect.width, ScenPart.RowHeight), "Add".Translate()))
			{
				List<FloatMenuOption> list = new List<FloatMenuOption>();
				list.Add(new FloatMenuOption("Random".Translate().CapitalizeFirst(), delegate
				{
					upgrades.Add(MechUpgradeUtility.UpgradesDatabase.RandomElement());
				}));
				foreach (MechUpgradeDef possibleUpgrade in MechUpgradeUtility.UpgradesDatabase)
				{
					MechUpgradeDef local = possibleUpgrade;
					list.Add(new FloatMenuOption(local.LabelCap, delegate
					{
						upgrades.Add(local);
					}));
				}
				Find.WindowStack.Add(new FloatMenu(list));
			}
			foreach (MechUpgradeDef u in upgradesToRemove)
			{
				upgrades.Remove(u);
			}
			upgradesToRemove.Clear();
			foreach (PawnKindDef m in mechsToRemove)
			{
				mechs.Remove(m);
			}
			mechsToRemove.Clear();
		}
	}
	

	public class UpgradeCombinationDef : Def
    {
		public float commonality = 1f;

		public List<ThingDef> mechs = new List<ThingDef>();

		public List<List<MechUpgradeDef>> upgrades = new List<List<MechUpgradeDef>>();
    }

	[StaticConstructorOnStartup]
	public class ReloadableUpgradeGizmo : Gizmo
	{
		private UpgradeCompReloadable_Items comp;

		private Texture2D BarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.34f, 0.42f, 0.43f));

		private Texture2D BarHighlightTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.43f, 0.54f, 0.55f));

		private static readonly Texture2D EmptyBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.03f, 0.035f, 0.05f));

		private static readonly Texture2D DragBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.74f, 0.97f, 0.8f));

		private const int Increments = 24;

		private static bool draggingBar;

		private float lastTargetValue;

		private float targetValue;

		private static List<float> bandPercentages;

		private string title;

		private string tooltip;

		public ReloadableUpgradeGizmo(UpgradeCompReloadable_Items comp, string title, string tooltip, Color barColor, Color barHighlightColor)
		{
			this.comp = comp;
			this.tooltip = tooltip;
			this.title = title;
			BarTex = SolidColorMaterials.NewSolidColorTexture(barColor);
			BarHighlightTex = SolidColorMaterials.NewSolidColorTexture(barHighlightColor);
			targetValue = (float)comp.maxToFill / (float)comp.Props.maxResource;
			if (bandPercentages == null)
			{
				bandPercentages = new List<float>();
				int num = 12;
				for (int i = 0; i <= num; i++)
				{
					float item = 1f / (float)num * (float)i;
					bandPercentages.Add(item);
				}
			}
		}

		public override float GetWidth(float maxWidth)
		{
			return 160f;
		}

		public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
		{
			Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
			Rect rect2 = rect.ContractedBy(10f);
			Widgets.DrawWindowBackground(rect);
			Text.Font = GameFont.Small;
			TaggedString labelCap = title;
			float height = Text.CalcHeight(labelCap, rect2.width);
			Rect rect3 = new Rect(rect2.x, rect2.y, rect2.width, height);
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(rect3, labelCap);
			Text.Anchor = TextAnchor.UpperLeft;
			lastTargetValue = targetValue;
			float num = rect2.height - rect3.height;
			float num2 = num - 4f;
			float num3 = (num - num2) / 2f;
			Rect rect4 = new Rect(rect2.x, rect3.yMax + num3, rect2.width, num2);
			Widgets.DraggableBar(rect4, BarTex, BarHighlightTex, EmptyBarTex, DragBarTex, ref draggingBar, (float)comp.resource / (float)comp.Props.maxResource, ref targetValue);
			//Widgets.FillableBar(rect4, comp.resource / comp.Props.maxResource, BarTex, EmptyBarTex, true);
			Text.Anchor = TextAnchor.MiddleCenter;
			rect4.y -= 2f;
			Widgets.Label(rect4, comp.resource.ToString() + " / " + comp.Props.maxResource);
			Text.Anchor = TextAnchor.UpperLeft;
			TooltipHandler.TipRegion(rect4, () => tooltip, Gen.HashCombineInt(comp.GetHashCode(), 34242369));
			if (lastTargetValue != targetValue)
			{
				comp.maxToFill = Mathf.RoundToInt(targetValue * (float)comp.Props.maxResource);
			}
			return new GizmoResult(GizmoState.Clear);
		}
	}

	

	public class MechWeightClassExtension : DefModExtension
    {
		public float upgradabilityFactor = 1f;
    }

	public class HediffCompProperties_ShieldBoost : HediffCompProperties
	{
		public float baseEnergyGain;

		public SimpleCurve energyGainFromWeightClass;

		public HediffCompProperties_ShieldBoost()
		{
			compClass = typeof(HediffComp_ShieldBoost);
		}
	}
	public class HediffComp_ShieldBoost : HediffComp
	{
		private CompProjectileInterceptor Shield => parent?.pawn?.TryGetComp<CompProjectileInterceptor>();
		public HediffCompProperties_ShieldBoost Props => (HediffCompProperties_ShieldBoost)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
			if (parent.pawn.IsHashIntervalTick(10) && Shield != null && Shield.currentHitPoints < Shield.HitPointsMax)
			{
				Shield.currentHitPoints += EnergyGain;
			}
			base.CompPostTick(ref severityAdjustment);
        }

		private int EnergyGain => Mathf.RoundToInt(Props.baseEnergyGain * Props.energyGainFromWeightClass.Evaluate(parent.pawn.RaceProps.mechWeightClass.index));

        public override bool CompShouldRemove => Shield == null;
    }
	public enum UpgradeOperationType
    {
		Add,
		Remove,
		Skip
    }
	public class MechUpgradeOperation : IExposable
	{
		public MechUpgrade upgrade;

		public UpgradeOperationType type;

		public MechUpgradeOperation()
        {
        }

		public MechUpgradeOperation(MU.MechUpgrade u, MU.UpgradeOperationType t)
		{
			type = t;
			upgrade = u;
		}

		public virtual void ExposeData()
		{
			Scribe_Deep.Look(ref upgrade, "upgrade");
			Scribe_Values.Look(ref type, "type", defaultValue: UpgradeOperationType.Skip);
		}

		public void DoOperation(Pawn p, Building_MechUpgrader b)
        {
			if (type == UpgradeOperationType.Skip)
            {
				return;
            }
			if(upgrade == null)
            {
				Log.Error("Cannot do mech upgrade operation: null upgrade def - MU");
				return;
            }
			p.Drawer.renderer.SetAllGraphicsDirty();
			if (type == UpgradeOperationType.Add)
			{
				p.TryGetComp<MU.CompUpgradableMechanoid>().upgrades.Add(upgrade);
				upgrade.OnAdded(p);
				if (upgrade.def.ability != null)
				{
					p.abilities.GetAbility(upgrade.def.ability, true).RemainingCharges = upgrade.charges ?? p.abilities.GetAbility(upgrade.def.ability, true).maxCharges;
                }
				return;
			}
			int? i = null;
			if (upgrade.def.ability != null)
			{
				i = p.abilities.GetAbility(upgrade.def.ability, true).RemainingCharges;
			}
			upgrade.OnRemoved(p);
			p.TryGetComp<MU.CompUpgradableMechanoid>().upgrades.Remove(upgrade);
			if(upgrade.def.linkedThingDef.GetCompProperties<CompProperties_MechUpgrade>() == null)
            {
				return;
            }
			List<Thing> list = b.TryGetComp<CompAffectedByFacilities>().LinkedFacilitiesListForReading;
			if (!list.NullOrEmpty())
			{
				Thing storage = BestStorage(list.ToList(), upgrade);
				if(storage != null)
                {
					storage.TryGetComp<MU.CompUpgradesStorage>().innerContainer.TryAdd(MechUpgradeUtility.ItemFromUpgrade(upgrade));
					return;
				}
			}
			GenSpawn.Spawn(MechUpgradeUtility.ItemFromUpgrade(upgrade), b.PositionHeld, b.MapHeld);
		}

		public static Thing BestStorage(List<Thing> facilities, MechUpgrade upgrade)
        {
			byte maxPriority = 0;
			List<Thing> list = facilities.ToList();
			foreach (Thing item in list)
			{
				CompUpgradesStorage comp = item.TryGetComp<CompUpgradesStorage>();
                if (comp == null || comp.Space < upgrade.def.upgradePoints)
                {
					facilities.Remove(item);
				}
                else
                {
                    if (comp.GetStoreSettings().AllowedToAccept(upgrade.def.linkedThingDef))
                    {
						maxPriority = (byte)Mathf.Max((byte)(comp.GetStoreSettings().Priority), maxPriority);
						if (maxPriority >= 5)
						{
							break;
						}
					}
                    else
                    {
						facilities.Remove(item);
					}
				}
			}
			foreach (Thing item2 in facilities)
			{
				if (maxPriority == (byte)(item2.TryGetComp<MU.CompUpgradesStorage>().GetStoreSettings().Priority))
				{
					return item2;
				}
			}
			return null;
		}
	}

	[StaticConstructorOnStartup]
	public class Dialog_ConfigureUpgrades : Window
	{
		public Pawn target;

		private float viewHeight1 = 1000f;

		private float viewHeight2 = 1000f;

		public Building_MechUpgrader upgrader;

		private Vector2 scrollPosition1;

		private Vector2 scrollPosition2;

		private QuickSearchWidget quickSearchWidget = new QuickSearchWidget();

		public void FilterList()
        {
			cachedFilteredList = tmpListB.ToList();
			cachedFilteredList.RemoveAll((MechUpgrade x) => !quickSearchWidget.filter.Matches(x.def.label) || (MechUpgradeUtility.Settings.hideNotInstallable && !x.def.CanAdd(target.def)));
			cachedFilteredList.SortBy((MechUpgrade y) => y.def.label);

		}

		public List<MU.MechUpgrade> tmpListA = new List<MU.MechUpgrade>();

		public List<MU.MechUpgrade> tmpListB = new List<MU.MechUpgrade>();

		public List<MU.MechUpgrade> cachedFilteredList = new List<MU.MechUpgrade>();

		public override Vector2 InitialSize => new Vector2(1096f, 660f);

		public Dialog_ConfigureUpgrades(Pawn p, Building_MechUpgrader b)
		{
			closeOnAccept = false;
			closeOnCancel = false;
			forcePause = true;
			absorbInputAroundWindow = true;
			target = p;
			upgrader = b;
			tmpListA.AddRange(p.TryGetComp<MU.CompUpgradableMechanoid>().upgrades);
            if (!b.TryGetComp<CompAffectedByFacilities>().LinkedFacilitiesListForReading.NullOrEmpty())
            {
				foreach(Thing t in b.TryGetComp<CompAffectedByFacilities>().LinkedFacilitiesListForReading)
                {
                    if (b.TryGetComp<CompAffectedByFacilities>().IsFacilityActive(t))
                    {
						tmpListB.AddRange(t.TryGetComp<MU.CompUpgradesStorage>().Upgrades);
                    }
                }
            }
			soundAppear = SoundDefOf.TabOpen;
			soundClose = SoundDefOf.TabClose;
		}

		public override void PreOpen()
		{
			base.PreOpen();
			quickSearchWidget.Reset();
		}

		public override void DoWindowContents(Rect inRect)
		{
			FilterList();
			Text.Font = GameFont.Medium;
			Text.Anchor = TextAnchor.UpperCenter;
			Widgets.Label(inRect, "MU_UpgradeConfig_Label".Translate());
			Text.Font = GameFont.Tiny;
			Rect rect1 = new Rect(5f, 0f, 420f, 600f);
			Rect rect2 = new Rect(inRect.width - 420f, 0f, 420f, 600f);
			Rect rect3 = new Rect(inRect.center.x - 100f, 530f, 200f, 40f);
			Rect rect4 = new Rect(inRect.center.x - 100f, 580f, 200f, 40f);
			Rect rect5 = new Rect(inRect.center.x - 100f, 260f, 200f, 240f);
			Rect rect6 = new Rect(inRect.center.x - 100f, 280f, 200f, 200f);
			Rect rect7 = new Rect(inRect.center.x - 95f, 285f, 190f, 190f);
			Rect rect8 = new Rect(inRect.center.x - 95f, 300f, 190f, 190f);
			Rect rect9 = new Rect(inRect.center.x - 100f, 480f, 95f, 40f);
			Rect rect10 = new Rect(inRect.center.x + 5f, 480f, 95f, 40f);
			Rect rect11 = new Rect(inRect.width - 420f, 0f, 180f, 24f);
			Rect rect12 = new Rect(inRect.width - 240f, 0f, 216f, 24f);
			quickSearchWidget.OnGUI(rect11, FilterList);
			Text.Anchor = TextAnchor.MiddleRight;
			Widgets.Label(rect12, "MU_HideNotInstallable".Translate());
			Widgets.Checkbox(inRect.width - 24f, 0, checkOn: ref MechUpgradeUtility.Settings.hideNotInstallable);
			Text.Anchor = TextAnchor.UpperCenter;
			Text.Font = GameFont.Small;
			//Widgets.DrawAltRect(rect6);
			Widgets.Label(rect5, target.Name.ToStringFull);
			//RenderTexture image = PortraitsCache.Get(target, rect7.size, Rot4.East, default(Vector3));
			//GUI.DrawTexture(rect7, image);
			Text.Font = GameFont.Medium;
			Text.Anchor = TextAnchor.UpperLeft;
			MechUpgradeUtility.DrawPoints(inRect, target, tmpListA, TextAnchor.MiddleCenter);
			List<MechUpgrade> list1 = new List<MechUpgrade>();
			List<MechUpgrade> list2 = new List<MechUpgrade>();
			Widgets.DrawLineVertical(425f, 30f, 600f);
			Widgets.DrawLineVertical(inRect.width - 425f, 30f, 600f);
			MechUpgradeUtility.DoListing(rect1, tmpListA, true, list1, ref scrollPosition1, ref viewHeight1);
			MechUpgradeUtility.DoListing(rect2, cachedFilteredList, true, list2, ref scrollPosition2, ref viewHeight2);
            if (tmpListA.NullOrEmpty())
            {
				Text.Anchor = TextAnchor.MiddleCenter;
				Text.Font = GameFont.Small;
				GUI.color = ColoredText.SubtleGrayColor;
				Widgets.Label(rect1, "(" + "MU_NoUpgradesInside".Translate() + ")");
				GUI.color = Color.white;
				Text.Anchor = TextAnchor.UpperLeft;
			}
			if (tmpListB.NullOrEmpty())
			{
				Text.Anchor = TextAnchor.MiddleCenter;
				Text.Font = GameFont.Small;
				GUI.color = ColoredText.SubtleGrayColor;
				Widgets.Label(rect2, "(" + "MU_NoUpgradesInside".Translate() + ")");
				GUI.color = Color.white;
				Text.Anchor = TextAnchor.UpperLeft;
			}
			if (!list1.NullOrEmpty())
            {
				tmpListB.AddRange(list1);
				foreach(MechUpgrade u in list1)
                {
					tmpListA.Remove(u);
                }
				FilterList();
            }
			if (!list2.NullOrEmpty())
			{
				foreach (MechUpgrade u in list2)
				{
					if (u.def.CanAdd(target.def, tmpListA) && MechUpgradeUtility.MaxUpgradePoints(target) - MechUpgradeUtility.CurrentPoints(tmpListA) >= u.def.upgradePoints)
					{
						tmpListB.Remove(u);
						tmpListA.Add(u);
					}
                    else
                    {
						if(!u.def.CanAdd(target.def, tmpListA))
                        {
							Messages.Message(u.def.CanAdd(target.def, tmpListA).Reason, MessageTypeDefOf.RejectInput);
						}
                        else
                        {
							Messages.Message("MU_CannotAdd_NotEnoughPoints".Translate(), MessageTypeDefOf.RejectInput);
						}
                    }
				}
				FilterList();
			}
			if (Widgets.ButtonText(rect9, "Load".Translate()))
			{
				Find.WindowStack.Add(new Dialog_UpgradeSetList_Load(delegate (UpgradeSet set)
				{
					tmpListB.AddRange(tmpListA);
					tmpListA.Clear();
					bool flag1 = false;
					bool flag2 = false;
					foreach(MechUpgradeDef d in set.upgrades)
                    {
                        if (!d.CanAdd(target.def, tmpListA) || MechUpgradeUtility.MaxUpgradePoints(target) - MechUpgradeUtility.CurrentPoints(tmpListA) < d.upgradePoints)
                        {
							flag1 = true;
                        }
                        else
                        {
							MechUpgrade u = tmpListB.FirstOrDefault((MechUpgrade u1) => u1.def == d);
							if(u == null)
                            {
								flag2 = true;
                            }
                            else
                            {
								tmpListB.Remove(u);
								tmpListA.Add(u);
							}
                        }
                    }
                    if (flag1)
                    {
						Messages.Message("MU_CannotAddSet_CannotAddUpgrade".Translate(), null, MessageTypeDefOf.RejectInput, false);
                    }
                    if (flag2)
                    {
						Messages.Message("MU_CannotAddSet_NoUpgrade".Translate(), null, MessageTypeDefOf.RejectInput, false);
					}
				}));
			}
			if (Widgets.ButtonText(rect10, "Save".Translate()))
			{
                if (tmpListA.NullOrEmpty())
                {
					Messages.Message("MU_CannotCreateSet".Translate(), null, MessageTypeDefOf.RejectInput, false);
				}
                else
				{
					UpgradeSet set = new UpgradeSet();
					set.upgrades = new List<MechUpgradeDef>();
					foreach (MechUpgrade u in tmpListA)
					{
						set.upgrades.Add(u.def);
					}
					Find.WindowStack.Add(new Dialog_SaveUpgradeSet(set));
				}
			}
			if (Widgets.ButtonText(rect3, "Confirm".Translate()))
			{
				upgrader.operations = MechUpgradeUtility.GetOperationsFromLists(target.TryGetComp<MU.CompUpgradableMechanoid>().upgrades, tmpListA).ToList();
				tmpListA.Clear();
				tmpListB.Clear();
                if (!upgrader.operations.NullOrEmpty())
                {
					foreach (MU.MechUpgradeOperation o in upgrader.operations)
					{
						if (o.type == UpgradeOperationType.Add)
						{
							foreach (Thing t in upgrader.TryGetComp<CompAffectedByFacilities>().LinkedFacilitiesListForReading)
							{
								if (upgrader.TryGetComp<CompAffectedByFacilities>().IsFacilityActive(t) && !t.TryGetComp<MU.CompUpgradesStorage>().Upgrades.NullOrEmpty() && t.TryGetComp<MU.CompUpgradesStorage>().Upgrades.Contains(o.upgrade))
								{
									Thing t3 = t.TryGetComp<MU.CompUpgradesStorage>().innerContainer.FirstOrDefault((Thing t2) => t2.TryGetComp<CompMechUpgrade>()?.upgrade == o.upgrade);
									if(t3 == null)
                                    {
										continue;
                                    }
									t.TryGetComp<MU.CompUpgradesStorage>().innerContainer.Remove(t3);
									break;
								}
							}
						}
					}
					upgrader.fabricationTicksLeft = 2500 * upgrader.operations.Last().upgrade.def.upgradePoints;
				}
				Find.WindowStack.TryRemove(this);
			}
			if (Widgets.ButtonText(rect4, "Cancel".Translate()))
			{
				tmpListA.Clear();
				tmpListB.Clear();
				Find.WindowStack.TryRemove(this);
			}
		}
	}
	public class ITab_MechUpgrades : ITab
	{
		private float viewHeight = 1000f;

		private Vector2 scrollPosition;

		private static readonly Vector2 WinSize = new Vector2(420f, 480f);

		private static readonly CachedTexture ToggleIcon = new CachedTexture("UI/Gizmos/MU_ForceReload");
		protected Thing SelTable
        {
            get
            {
				if(base.SelThing is Corpse corpse)
                {
					return corpse.InnerPawn;
                }
				return base.SelThing;
            }
        }

		public ITab_MechUpgrades()
		{
			size = WinSize;
			labelKey = "MU_TabUpgrades";
			tutorTag = "MU_Upgrades";
		}

		public override bool IsVisible => SelTable.HasComp<MU.CompUpgradableMechanoid>() || SelTable.HasComp<MU.CompUpgradesStorage>();

		protected override void FillTab()
		{
			if(!SelTable.HasComp<MU.CompUpgradableMechanoid>() && !SelTable.HasComp<MU.CompUpgradesStorage>())
            {
				return;
            }
			Text.Font = GameFont.Small;
			PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.BillsTab, KnowledgeAmount.FrameDisplayed);
			Rect rect1 = new Rect(0f, 0f, WinSize.x, WinSize.y);
			if (Prefs.DevMode && Widgets.ButtonText(new Rect(rect1.xMax - 18f - 125f, 5f, 115f, Text.LineHeight), "Dev tool..."))
			{
				Find.WindowStack.Add(new FloatMenu(MU.MechUpgradeUtility.DebugOptions(SelTable)));
			}
			Text.Font = GameFont.Medium;
			if (SelTable.HasComp<MU.CompUpgradesStorage>())
			{
				Rect rect2 = new Rect(0f, 10f, WinSize.x, WinSize.y).ContractedBy(10f);
				MechUpgradeUtility.DrawPoints(rect1, SelTable, SelTable.TryGetComp<MU.CompUpgradesStorage>().Upgrades, TextAnchor.UpperCenter);
				List<MU.MechUpgrade> list1 = new List<MU.MechUpgrade>();
				if(SelTable.TryGetComp<MU.CompUpgradesStorage>().Upgrades.NullOrEmpty())
				{
					Text.Anchor = TextAnchor.MiddleCenter;
					Text.Font = GameFont.Small;
					GUI.color = ColoredText.SubtleGrayColor;
					Widgets.Label(rect1, "(" + "MU_NoUpgradesInside".Translate() + ")");
					GUI.color = Color.white;
					Text.Anchor = TextAnchor.UpperLeft;
				}
				MechUpgradeUtility.DoListing(rect2, SelTable.TryGetComp<MU.CompUpgradesStorage>().Upgrades, false, list1, ref scrollPosition, ref viewHeight, true);
				if (!list1.NullOrEmpty())
                {
					foreach (MechUpgrade u in list1)
					{
						SelTable.TryGetComp<MU.CompUpgradesStorage>().innerContainer.TryDrop(SelTable.TryGetComp<MU.CompUpgradesStorage>().innerContainer.First((Thing t)=> t.TryGetComp<MU.CompMechUpgrade>().upgrade == u), SelTable.Position, SelTable.Map, ThingPlaceMode.Near, out var item);
					}
				}
			}
            else
            {
				CompUpgradableMechanoid comp = SelTable.TryGetComp<MU.CompUpgradableMechanoid>();
				Rect rect3 = new Rect(0f, 10f, WinSize.x, WinSize.y).ContractedBy(10f); 
                if (comp.Mech.IsColonyMech && !comp.Mech.Dead)
                {
					rect3 = new Rect(0f, 15f, WinSize.x, WinSize.y - 5f).ContractedBy(10f);
					Rect rect4 = new Rect(0f, 0f, 48f, 48f);
					Rect rect5 = new Rect(24f, 0f, 24f, 24f);
					Widgets.DrawTextureFitted(rect4, ToggleIcon.Texture, 1.1f);
					Widgets.Checkbox(rect5.position, ref comp.autoReload, 24f);
					if (Mouse.IsOver(rect4))
					{
						Widgets.DrawHighlight(rect4);
						TooltipHandler.TipRegion(rect4, "MU_AutoReload_Desc".Translate());
					}
				}
				MechUpgradeUtility.DrawPoints(rect1, SelTable, comp.upgrades, TextAnchor.UpperCenter);
				MechUpgradeUtility.DoListing(rect3, comp.upgrades, false, new List<MU.MechUpgrade>(), ref scrollPosition, ref viewHeight);
				if(comp.upgrades.NullOrEmpty())
				{
					Text.Anchor = TextAnchor.MiddleCenter;
					Text.Font = GameFont.Small;
					GUI.color = ColoredText.SubtleGrayColor;
					Widgets.Label(rect1, "(" + "MU_NoUpgradesInside".Translate() + ")");
					GUI.color = Color.white;
					Text.Anchor = TextAnchor.UpperLeft;
				}
			}
		}
	}

	public class ITab_MechUpgrader : ITab
	{
		private float viewHeight = 1000f;

		private Vector2 scrollPosition;

		private static readonly Vector2 WinSize = new Vector2(420f, 480f);

		private QuickSearchWidget quickSearchWidget = new QuickSearchWidget();

		public List<MU.MechUpgrade> tmpList = new List<MU.MechUpgrade>();

		public List<MU.MechUpgrade> cachedFilteredList = new List<MU.MechUpgrade>();

		public void FilterList()
		{
			cachedFilteredList = tmpList.ToList();
			cachedFilteredList.RemoveAll((MechUpgrade x) => !quickSearchWidget.filter.Matches(x.def.label));
			cachedFilteredList.SortBy((MechUpgrade y) => y.def.label);
		}

        public override void OnOpen()
        {
            base.OnOpen();
			ResetList();
			quickSearchWidget.Reset();
		}

		public void ResetList()
        {
			if (SelTable.TryGetComp<CompAffectedByFacilities>(out var comp))
			{
				tmpList = new List<MU.MechUpgrade>();
				foreach (Thing t in comp.LinkedFacilitiesListForReading)
				{
					if (comp.IsFacilityActive(t) && t.TryGetComp<MU.CompUpgradesStorage>(out var c))
					{
						tmpList.AddRange(c.Upgrades);
					}
				}
				FilterList();
			}
		}

		protected Thing SelTable
		{
			get
			{
				return base.SelThing;
			}
		}

		public ITab_MechUpgrader()
		{
			size = WinSize;
			labelKey = "MU_TabUpgrades";
			tutorTag = "MU_Upgrades";
		}

		public override bool IsVisible => SelTable is Building_MechUpgrader b && b.HasComp<CompAffectedByFacilities>();

		protected override void FillTab()
		{
			ResetList();
			Rect rect1 = new Rect(0f, 10f, WinSize.x, WinSize.y);
			Rect rect2 = new Rect(70f, 5f, 250f, 24f);
			Text.Font = GameFont.Tiny;
			quickSearchWidget.OnGUI(rect2, FilterList);
			Text.Font = GameFont.Small;
			PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.BillsTab, KnowledgeAmount.FrameDisplayed);
			Text.Font = GameFont.Medium;
			Rect rect3 = rect1.ContractedBy(10f);
			if (cachedFilteredList.NullOrEmpty())
			{
				Text.Anchor = TextAnchor.MiddleCenter;
				Text.Font = GameFont.Small;
				GUI.color = ColoredText.SubtleGrayColor;
				Widgets.Label(rect1, "(" + "MU_NoUpgradesInside".Translate() + ")");
				GUI.color = Color.white;
				Text.Anchor = TextAnchor.UpperLeft;
			}
            else
            {
				MechUpgradeUtility.DoListing(rect3, cachedFilteredList, false, null, ref scrollPosition, ref viewHeight, false);
			}
		}
	}

	public class PawnColumnWorker_AutoReload : PawnColumnWorker
	{
		public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
		{
			if (pawn.Faction != Faction.OfPlayer || !pawn.RaceProps.IsMechanoid || pawn.GetOverseer() != null)
			{
				CompUpgradableMechanoid comp = pawn.GetComp<CompUpgradableMechanoid>();
				if (comp != null)
				{
					rect.xMin += (rect.width - 24f) / 2f;
					rect.yMin += (rect.height - 24f) / 2f;
					Widgets.Checkbox(rect.position, ref comp.autoReload, 24f, disabled: false, def.paintable);
				}
			}
		}

		public override int GetMinWidth(PawnTable table)
		{
			return Mathf.Max(base.GetMinWidth(table), 24);
		}

		public override int GetMaxWidth(PawnTable table)
		{
			return Mathf.Min(base.GetMaxWidth(table), GetMinWidth(table));
		}

		public override int GetMinCellHeight(Pawn pawn)
		{
			return Mathf.Max(base.GetMinCellHeight(pawn), 24);
		}
	}

	public class RecipeWorkerCounter_MakeUpgrade : RecipeWorkerCounter //Only to check for upgrades inside storages
	{
		public override int CountProducts(Bill_Production bill)
		{
			ThingDef thingDef = recipe.products[0].thingDef;
			return bill.Map.resourceCounter.GetCount(thingDef) + GetCarriedCount(bill, thingDef) + GetStoredCount(bill, thingDef);
		}

		private int GetStoredCount(Bill_Production bill, ThingDef prodDef)
		{
			int num = 0;
			foreach (Building item in bill.Map.listerBuildings.allBuildingsColonist)
			{
				CompUpgradesStorage comp = item.TryGetComp<CompUpgradesStorage>();
				if(comp != null && comp.innerContainer?.Any == true)
                {
					num += comp.innerContainer.Count((Thing t) => t.def == prodDef);
                }
			}
			return num;
		}

		private int GetCarriedCount(Bill_Production bill, ThingDef prodDef)
		{
			int num = 0;
			foreach (Pawn item in bill.Map.mapPawns.FreeColonistsSpawned)
			{
				Thing carriedThing = item.carryTracker.CarriedThing;
				if (carriedThing != null)
				{
					int stackCount = carriedThing.stackCount;
					if (CountValidThing(carriedThing, bill, prodDef))
					{
						num += stackCount;
					}
				}
			}
			return num;
		}
	}

	public class GameCondition_UpgradesChange : GameCondition
	{
        public override void Init()
        {
            base.Init();
			MechUpgradeUtility.upgradeChanceOverride = 1f;
		}
        public override void End()
        {
			MechUpgradeUtility.upgradeChanceOverride = null;
			base.End();
		}
    }

	public class UpgradabilityTagExtension : DefModExtension
    {
		public List<string> tags = new List<string>();
    }

	public class FloatMenuOptionProvider_EnterUpgrader : FloatMenuOptionProvider
	{
		protected override bool Drafted => true;

		protected override bool Undrafted => true;

		protected override bool Multiselect => false;

		protected override bool MechanoidCanDo => true;

		protected override bool RequiresManipulation => false;

		protected override bool AppliesInt(FloatMenuContext context)
		{
			if (context.FirstSelectedPawn.RaceProps.IsMechanoid && context.FirstSelectedPawn.HasComp<CompUpgradableMechanoid>())
			{
				return true;
			}
			return false;
		}

		protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
		{
			if(clickedThing is Building_MechUpgrader upgrader)
            {
				if (!context.FirstSelectedPawn.CanReach(clickedThing, PathEndMode.Touch, Danger.Deadly))
				{
					return new FloatMenuOption("CannotUseNoPath".Translate().CapitalizeFirst(), null);
				}
				return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("MU_ConfigureUpgrades".Translate(), delegate
				{
					upgrader.OrderForceTarget(context.FirstSelectedPawn);
				}), context.FirstSelectedPawn, clickedThing);
			}
			return null;
		}
	}
}