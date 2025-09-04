using DelaunatorSharp;
using Gilzoide.ManagedJobs;
using HarmonyLib;
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
using static RimWorld.MechClusterSketch;

namespace MU
{
	public class GenStep_CerebrexUpgrades : GenStep
	{
        public override int SeedPart => 2345676;

        public override void Generate(Map map, GenStepParams parms)
        {
            
        }

        public override void PostMapInitialized(Map map, GenStepParams parms)
        {
            try
            {
				foreach (Pawn p in map.mapPawns.PawnsInFaction(Faction.OfMechanoids))
				{
                    CompUpgradableMechanoid comp = p.GetComp<CompUpgradableMechanoid>();
                    if (comp == null)
					{
						continue;
					}
                    MechUpgradeUtility.DEV_RemoveAll(p);
					MechUpgradeUtility.UpgradeMech(p, 1f, true);
					foreach(MechUpgrade u in comp.upgrades.ToList())
					{
						string s = u.def.defName;
						if(s.EndsWith("_B") || s.EndsWith("_C"))
						{

							MechUpgradeDef def = DefDatabase<MechUpgradeDef>.GetNamedSilentFail(s.Remove(s.Count() - 1) + "A");
							if (def != null)
							{
                                comp.RemoveUpgrade(u);
								comp.AddUpgrade(def);

                            }
                        }
					}
					//comp.AddUpgrade(MUMiscDefOf.MU_CerebrexLink);
                }
			}
			catch(Exception ex)
            {
				Log.Error("Exception upgrading mechs:" + ex);
            }
		}
    }

	public class GenStep_AncientComplex_Upgrades : GenStep_AncientComplex
	{
		protected override void GenerateComplex(Map map, ResolveParams parms)
		{
			RimWorld.BaseGen.BaseGen.globalSettings.map = map;
			RimWorld.BaseGen.BaseGen.symbolStack.Push("ancientUpgradesComplex_MU", parms);
			RimWorld.BaseGen.BaseGen.Generate();
		}
	}
	public class SymbolResolver_AncientComplex_Upgrades : SymbolResolver_AncientComplex_Base
	{
		protected override LayoutDef DefaultLayoutDef => MUMiscDefOf.MU_AncientComplex_Upgrades_Loot;

		public override void Resolve(ResolveParams rp)
		{
			ResolveParams resolveParams = rp;
			resolveParams.floorDef = TerrainDefOf.PackedDirt;
			BaseGen.symbolStack.Push("outdoorsPath", resolveParams);
			BaseGen.symbolStack.Push("ensureCanReachMapEdge", rp);
			ResolveComplex(rp);
		}
	}

	public class QuestNode_Root_Loot_AncientComplex_Upgrades : QuestNode_Root_Loot_AncientComplex
	{
		protected override LayoutDef LayoutDef => MUMiscDefOf.MU_AncientComplex_Upgrades_Loot;

		protected override SitePartDef SitePartDef => MUMiscDefOf.MU_AncientComplex_Upgrades;

		protected override bool BeforeRunInt()
		{
			return true;
		}

		protected override void RunInt()
		{
			Slate slate = QuestGen.slate;
			if (!slate.TryGet<bool>("discovered", out var _))
			{
				slate.Set("discovered", var: false);
			}
			base.RunInt();
		}
	}
	public class GenStep_AncientFacility : GenStep
	{
		public IntRange sizeRange = new IntRange(40, 50);

		public override int SeedPart => 8291734;

		public override void Generate(Map map, GenStepParams parms)
		{
			CellRect cellRect = map.Center.RectAbout(new IntVec2(sizeRange.RandomInRange, sizeRange.RandomInRange));
			StructureGenParams parms2 = new StructureGenParams
			{
				size = cellRect.Size
			};
			LayoutWorker obj = MUMiscDefOf.MU_AncientUpgradesStockpile.Worker;
			LayoutStructureSketch layoutStructureSketch = obj.GenerateStructureSketch(parms2);
			map.layoutStructureSketches.Add(layoutStructureSketch);
			float? threatPoints = null;
			if (parms.sitePart != null)
			{
				threatPoints = parms.sitePart.parms.points;
			}
			obj.Spawn(layoutStructureSketch, map, cellRect.Min, threatPoints);
		}
	}

	public class LayoutWorker_AncientFacility : LayoutWorker_Structure
	{
		public LayoutWorker_AncientFacility(LayoutDef def)
			: base(def)
		{
		}

		protected override StructureLayout GetStructureLayout(StructureGenParams parms, CellRect rect)
		{
			return RoomLayoutGenerator.GenerateRandomLayout(parms.sketch, rect, minRoomHeight: base.Def.minRoomHeight, minRoomWidth: base.Def.minRoomWidth, areaPrunePercent: 0.25f, canRemoveRooms: true, generateDoors: false, corridor: null, corridorExpansion: 2, maxMergeRoomsRange: new IntRange(2, 4), corridorShapes: CorridorShape.All, canDisconnectRooms: false);
		}

		protected override void PostGraphsGenerated(StructureLayout layout, StructureGenParams parms)
		{
			foreach (LayoutRoom room in layout.Rooms)
			{
				room.noExteriorDoors = true;
			}
		}

        public override void Spawn(LayoutStructureSketch layoutStructureSketch, Map map, IntVec3 pos, float? threatPoints = null, List<Thing> allSpawnedThings = null, bool roofs = true, bool canReuseSketch = false, Faction faction = null)
        {
            base.Spawn(layoutStructureSketch, map, pos, threatPoints, allSpawnedThings, roofs, canReuseSketch, faction);
			SpawnRoomRewards(layoutStructureSketch.structureLayout.Rooms, map, allSpawnedThings);
		}

        private void SpawnRoomRewards(List<LayoutRoom> rooms, Map map, List<Thing> allSpawnedThings)
		{
			int num = Mathf.RoundToInt((float)rooms.Count * 0.3f);
			if (num <= 0)
			{
				return;
			}
			ThingSetMakerDef thingSetMakerDef = MUMiscDefOf.MU_Reward_AncientFacility;
			foreach (LayoutRoom item in rooms.InRandomOrder())
			{
				if (item.requiredDef != null)
				{
					continue;
				}
				ThingDef ancientHermeticCrate = ThingDefOf.AncientHermeticCrate;
				Func<IntVec3, bool> validator = CanSpawnAt;
				if (item.TryGetRandomCellInRoom(ancientHermeticCrate, map, out var cell, null, 2, 0, validator))
				{
					Building_Crate building_Crate = (Building_Crate)GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.AncientHermeticCrate), cell, map, Rot4.South);
					List<Thing> list = thingSetMakerDef.root.Generate(default(ThingSetMakerParams));
					for (int i = list.Count - 1; i >= 0; i--)
					{
						Thing thing = list[i];
						if (!building_Crate.TryAcceptThing(thing, allowSpecialEffects: false))
						{
							thing.Destroy();
						}
					}
					num--;
				}
				if (num <= 0)
				{
					break;
				}
			}
			bool CanSpawnAt(IntVec3 c)
			{
				return GenSpawn.CanSpawnAt(ThingDefOf.AncientHermeticCrate, c, map, Rot4.South, canWipeEdifices: false);
			}
		}
	}

	public class RoomContents_FacilityEntrance : RoomContentsWorker
	{
		private static readonly IntRange TurretsRange = new IntRange(1, 2);

		public override void FillRoom(Map map, LayoutRoom room, Faction faction, float? threatPoints = null)
		{
			SpawnExit(map, room);
			SpawnTurrets(map, room, faction);
			base.FillRoom(map, room, faction, threatPoints);
		}

		private void SpawnExit(Map map, LayoutRoom room)
		{
			List<Thing> list = new List<Thing>();
			ThingDef exit = MUThingDefOf.MU_AncientFacilityExit;
			List<Thing> spawned = list;
			RoomGenUtility.FillWithPadding(exit, 1, room, map, null, null, spawned, 3);
			MapGenerator.PlayerStartSpot = list.First().Position;
		}

		private void SpawnTurrets(Map map, LayoutRoom room, Faction faction)
		{
			RoomGenUtility.FillAroundEdges(ThingDefOf.AncientSecurityTurret, TurretsRange.RandomInRange, IntRange.One, room, map, null, null, 1, 0, null, avoidDoors: true, RotationDirection.Opposite, null, faction);
		}
	}

	public class RoomContents_EntranceRoom : RoomContentsWorker
	{
		public override void FillRoom(Map map, LayoutRoom room, Faction faction, float? threatPoints = null)
		{
			SpawnEntrance(map, room);
			base.FillRoom(map, room, faction, threatPoints);
		}

		private void SpawnEntrance(Map map, LayoutRoom room)
		{
			PrefabUtility.SpawnPrefab(MUMiscDefOf.MU_AncientFacilityEntrance, map, room.rects.First().ContractedBy(4).Cells.RandomElement(), Rot4.Random);
		}
	}

	public class RoomContents_UpgraderRoom : RoomContentsWorker
	{
		public override void FillRoom(Map map, LayoutRoom room, Faction faction, float? threatPoints = null)
		{
			SpawnUpgrader(map, room);
			base.FillRoom(map, room, faction, threatPoints);
		}

		private void SpawnUpgrader(Map map, LayoutRoom room)
		{
			PrefabUtility.SpawnPrefab(MUMiscDefOf.MU_AncientMechUpgrader, map, room.rects.First().ContractedBy(4).Cells.RandomElement(), Rot4.Random);
		}
	}
}