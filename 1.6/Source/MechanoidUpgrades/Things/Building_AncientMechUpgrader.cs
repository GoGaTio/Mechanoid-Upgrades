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
	public class Building_AncientMechUpgrader : Building, IThingHolderWithDrawnPawn, IThingHolder, ISuspendableThingHolder
	{
		public ThingOwner innerContainer;

		public bool unfogged;

		public float chance;

		public int checkTicks = 0;
		public Building_AncientMechUpgrader()
		{
			innerContainer = new ThingOwner<Thing>(this);
		}
		public float HeldPawnDrawPos_Y => DrawPos.y + 1f / 26f;

		public float HeldPawnBodyAngle => base.Rotation.AsAngle;

		public bool IsContentsSuspended => true;

		public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundFaceUp;

		private static readonly List<string> options = new List<string>()
		{
			"Mech_Centurion",
			"Mech_Tunneler",
			"Mech_CentipedeGunner",
			"LFM_Mech_Taintor",
			"IM_Mech_Berserker"
		};

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
		}

		public ThingOwner GetDirectlyHeldThings()
		{
			return innerContainer;
		}

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

		public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
		{
			EjectContents();
			base.DeSpawn(mode);
		}

		public void EjectContents()
		{
			Pawn occupant = Occupant;
			innerContainer.TryDropAll(InteractionCell, base.Map, ThingPlaceMode.Near);
			if(occupant != null && occupant.Spawned)
            {
				LordMaker.MakeNewLord(Faction.OfMechanoids ?? Faction.OfAncientsHostile, new LordJob_AssaultColony(), base.Map, new List<Pawn>() { occupant });
			}
		}

        public override void PostMake()
        {
            base.PostMake();
			PawnKindDef kind = PawnKindDefOf.Mech_Scyther;
			DefDatabase<PawnKindDef>.AllDefsListForReading.TryRandomElement((PawnKindDef p) => options.Contains(p.defName), out kind);
			Pawn mech = PawnGenerator.GeneratePawn(kind, Faction.OfMechanoids ?? Faction.OfAncientsHostile);
			MechUpgradeUtility.UpgradeMech(mech, 1f, true);
			innerContainer.TryAddOrTransfer(mech);
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
		{
			base.DynamicDrawPhaseAt(phase, drawLoc, flip);
			Occupant?.Drawer.renderer.DynamicDrawPhaseAt(phase, drawLoc, null, neverAimWeapon: true);
		}

        protected override void Tick()
        {
			if (unfogged)
			{
				checkTicks--;
				if(checkTicks <= 0)
                {
					chance += 0.01f;
					if (Rand.Chance(chance))
					{
						EjectContents();
						unfogged = false;
					}
					checkTicks = 60;
				}
			}
			base.Tick();
		}

        public override void Notify_Unfogged()
        {
			unfogged = true;
			base.Notify_Unfogged();
            if (Rand.Chance(0.333f))
            {
				checkTicks = 30;
				chance = 1f;
			}
        }

        public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
			Scribe_Values.Look(ref unfogged, "unfogged", defaultValue: false);
			Scribe_Values.Look(ref checkTicks, "checkTicks", defaultValue: 60);
			Scribe_Values.Look(ref chance, "chance", defaultValue: 0f);
		}
	}
}