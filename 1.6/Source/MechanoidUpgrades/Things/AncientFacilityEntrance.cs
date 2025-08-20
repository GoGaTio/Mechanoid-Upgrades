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
	[StaticConstructorOnStartup]
	public class AncientFacilityEntrance : MapPortal
	{
		private CompHackable hackableInt;

		private GraphicData openGraphicData;

		public static CachedTexture SealHatchIcon = new CachedTexture("UI/Commands/SealHatch");

		private CompHackable Hackable => hackableInt ?? (hackableInt = GetComp<CompHackable>());

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			openGraphicData = new GraphicData();
			openGraphicData.CopyFrom(def.graphicData);
			openGraphicData.texPath = "Things/Building/AncientHatch/AncientHatch_Open";
		}

		public override void Print(SectionLayer layer)
		{
			if (IsEnterable(out var _))
			{
				openGraphicData.Graphic.Print(layer, this, 0f);
			}
			else
			{
				Graphic.Print(layer, this, 0f);
			}
		}

		protected override IEnumerable<GenStepWithParams> GetExtraGenSteps()
		{
			yield return new GenStepWithParams(MUMiscDefOf.MU_AncientFacility, default(GenStepParams));
		}

		public override bool IsEnterable(out string reason)
		{
			if (!Hackable.IsHacked)
			{
				reason = "Locked".Translate();
				return false;
			}
			return base.IsEnterable(out reason);
		}

		public override string GetInspectString()
		{
			StringBuilder stringBuilder = new StringBuilder(base.GetInspectString());
			if (Hackable.IsHacked)
			{
				stringBuilder.AppendLineIfNotEmpty();
				stringBuilder.Append("HatchUnlocked".Translate());
			}
			return stringBuilder.ToString();
		}
	}
}