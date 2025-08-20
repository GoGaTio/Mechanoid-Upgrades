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

namespace MU.HeavyWeaponry //classes for add-on "Mechanoid Upgrades - Heavy Weaponry"
{
	public class UpgradeCompProperties_ReactiveArmor : UpgradeCompReloadableProperties_Items
	{
		public int charges = 7;

		public float chanceToDetonateAnother = 0.1f;

		public float chanceToDetonate = 0.95f;

		public int maxPawnsToSpawn = 3;

		public EffecterDef explosionEffecter;

		public List<int> damageThresholds = new List<int>();

		public UpgradeCompProperties_ReactiveArmor()
		{
			compClass = typeof(UpgradeComp_ReactiveArmor);
		}
	}

	public class UpgradeComp_ReactiveArmor : UpgradeCompReloadable_Items
	{
		public new UpgradeCompProperties_ReactiveArmor Props => (UpgradeCompProperties_ReactiveArmor)props;

		public int thresholdsIndex;

		public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
		{
			absorbed = false;
			if (dinfo.IgnoreInstantKillProtection || Mech == null)
			{
				return;
			}
			if (dinfo.Def.isRanged && (Props.damageThresholds[thresholdsIndex] <= dinfo.Amount) && Rand.Chance(Props.chanceToDetonate))
			{
				absorbed = true;
				Detonate(dinfo.Def);
			}
		}

		public void Detonate(DamageDef def)
        {

        }

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			if (!(parent.holder.IsColonyMech))
			{
				yield break;
			}
			if (parent.holder.Faction == Faction.OfPlayer)
			{
				yield return new ReactiveArmorUpgradeGizmo(this, (Props.gizmoKey + "_Title").Translate(), (Props.gizmoKey + "_Tooltip").Translate(), Props.barColor, Props.barHighlightColor);
			}
		}
	}

	[StaticConstructorOnStartup]
	public class ReactiveArmorUpgradeGizmo : Gizmo
	{
		private UpgradeComp_ReactiveArmor comp;

		private Texture2D BarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.34f, 0.42f, 0.43f));

		private Texture2D BarHighlightTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.43f, 0.54f, 0.55f));

		private static readonly Texture2D EmptyBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.03f, 0.035f, 0.05f));

		private static readonly Texture2D DragBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.74f, 0.97f, 0.8f));

		private static bool draggingBar;

		private float lastTargetValue;

		private float targetValue;

		private static List<float> bandPercentages;

		private string title;

		private string tooltip;

		public ReactiveArmorUpgradeGizmo(UpgradeComp_ReactiveArmor comp, string title, string tooltip, Color barColor, Color barHighlightColor)
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
			Rect rect5 = new Rect(rect2.xMax - height, rect3.y, height, height);
			if (Widgets.ButtonText(rect5, comp.Props.damageThresholds[comp.thresholdsIndex].ToString(), false))
            {
				comp.thresholdsIndex = comp.thresholdsIndex >= comp.Props.damageThresholds.Count ? 0 : comp.thresholdsIndex++;
			}
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

	public class UpgradeCompProperties_LaserDefence : UpgradeCompProperties
	{
		public int range;

		public ThingDef mote;

		public bool activeWhileSleep;

		public bool interceptSameFaction;

		public bool interceptExplosive;

		public int delay;

		public int maxCharge;

		public int chargeInterval;

		public int chargeLossPerIntercept = 5;

		[NoTranslate]
		public string gismoTextKey;

		[NoTranslate]
		public string gizmoTexPath;

		public UpgradeCompProperties_LaserDefence()
		{
			compClass = typeof(UpgradeComp_LaserDefence);
		}
	}
	public class UpgradeComp_LaserDefence : UpgradeComp
	{
		public UpgradeCompProperties_LaserDefence Props => (UpgradeCompProperties_LaserDefence)props;

		public bool active = true;

		public int delay;

		public int charge;

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

        public override void PostGenerated(bool generatedForHolder)
        {
            base.PostGenerated(generatedForHolder);
			charge = Props.maxCharge;
        }

        public override void CompTick()
		{
			if (!active || parent.holder.DestroyedOrNull() || !parent.holder.Spawned || (!Props.activeWhileSleep && parent.holder.IsSelfShutdown()))
			{
				return;
			}
            if (charge < Props.maxCharge && Mech.IsHashIntervalTick(Props.chargeInterval))
            {
				charge++;
			}
			if(delay > 0)
            {
				delay--;
				return;
            }

			IntVec3 pos = Mech.Position;
			foreach (IntVec3 cell in new CellRect(pos.x, pos.z, 1, 1).ExpandedBy(Props.range))
			{
				List<Thing> list = Mech.Map.thingGrid.ThingsListAt(cell).Where((v) => v is Projectile).ToList();
				for (int i = 0; i < list.Count; i++)
				{
					Thing thing2 = list[i];
					if (IsBulletAffected(thing2) && Vector3.Distance(thing2.DrawPos, Mech.TrueCenter()) < Props.range)
					{
						Intercept(thing2);
					}
				}
			}
		}

		public void Intercept(Thing proj)
        {

        }

		private bool IsBulletAffected(Thing target)
		{
			Projectile proj = target as Projectile;
			if (proj == null)
			{
				return false;
			}
			if (!Props.interceptSameFaction && proj.Launcher.Faction == Mech.Faction)
			{
				return false;
			}
			if (proj is Bullet || (!Props.interceptExplosive && proj is Projectile_Explosive))
			{
				return true;
			}
			return false;
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			if (Mech.IsColonyMechPlayerControlled || DebugSettings.ShowDevGizmos)
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
}