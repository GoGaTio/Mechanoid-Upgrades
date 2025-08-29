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
	public class CompProperties_UpgradesStorage : CompProperties
	{
		public int maxCapacity;

		public CompProperties_UpgradesStorage()
		{
			compClass = typeof(CompUpgradesStorage);
		}
	}
	public class CompUpgradesStorage : ThingComp, IThingHolder, IStoreSettingsParent
	{
		public ThingOwner innerContainer;

		protected StorageSettings storageSettings;

		public CompProperties_UpgradesStorage Props => (CompProperties_UpgradesStorage)props;

		public bool StorageTabVisible => true;

		public StorageSettings GetStoreSettings()
		{
			return storageSettings;
		}

		public StorageSettings GetParentStoreSettings()
		{
			return parent.def.building.fixedStorageSettings;
		}

		public void Notify_SettingsChanged()
		{
		}

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
		}

		public ThingOwner GetDirectlyHeldThings()
		{
			return innerContainer;
		}

		public override void PostPostMake()
		{
			base.PostPostMake();
			innerContainer = new ThingOwner<Thing>(this);
			storageSettings = new StorageSettings(this);
			if (parent.def.building.defaultStorageSettings != null)
			{
				storageSettings.CopyFrom(parent.def.building.defaultStorageSettings);
			}
		}

		public List<MU.MechUpgrade> Upgrades
        {
            get
            {
				List<MU.MechUpgrade> list = new List<MechUpgrade>();
				foreach (Thing t in innerContainer)
				{
					if (t.HasComp<MU.CompMechUpgrade>())
					{
						list.Add(t.TryGetComp<MU.CompMechUpgrade>().upgrade);
					}
				}
				return list;
			}
        }

		[Unsaved(false)]
		private List<Thing> tmpUpgrades = new List<Thing>();

		public List<Thing> UpgradesAsThings
		{
			get
			{
				tmpUpgrades.Clear();
				for (int i = 0; i < innerContainer.Count; i++)
				{
					if (innerContainer[i].HasComp<MU.CompMechUpgrade>())
					{
						tmpUpgrades.Add(innerContainer[i]);
					}
				}
				return tmpUpgrades;
			}
		}

		public int Amount
        {
            get
            {
				int n = 0;
                if (Upgrades.NullOrEmpty())
                {
					return n;
                }
				foreach(MU.MechUpgrade u in Upgrades)
                {
					n += u.def.upgradePoints;
				}
				return n;
            }
        }

		public int Space
		{
			get
			{
				return Props.maxCapacity - Amount;
			}
		}

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
			if (mode != DestroyMode.WillReplace)
			{
				EjectContents(map);
			}
		}

		public void EjectContents(Map destMap = null)
		{
			if (destMap == null)
			{
				destMap = parent.Map;
			}
			IntVec3 dropLoc = (parent.def.hasInteractionCell ? parent.InteractionCell : parent.Position);
			innerContainer.TryDropAll(dropLoc, destMap, ThingPlaceMode.Near);
		}

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
			foreach (Gizmo gizmo in base.CompGetGizmosExtra())
			{
				yield return gizmo;
			}
			foreach (Gizmo item in StorageSettingsClipboard.CopyPasteGizmosFor(GetStoreSettings()))
			{
				yield return item;
			}
		}

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
			if(storageSettings == null)
            {
				storageSettings = new StorageSettings(this);
				if (parent.def.building.defaultStorageSettings != null)
				{
					storageSettings.CopyFrom(parent.def.building.defaultStorageSettings);
				}
			}
			base.PostSpawnSetup(respawningAfterLoad);
        }
        public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Deep.Look(ref storageSettings, "storageSettings", this);
			Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
		}
	}

	public class CompProperties_MechUpgrade : CompProperties
	{
		public MechUpgradeDef upgradeDef;
		public CompProperties_MechUpgrade()
		{
			compClass = typeof(CompMechUpgrade);
		}

		public override void ResolveReferences(ThingDef parentDef)
		{
			base.ResolveReferences(parentDef);
			if (parentDef.descriptionHyperlinks == null)
			{
				parentDef.descriptionHyperlinks = new List<DefHyperlink>();
			}
			parentDef.descriptionHyperlinks.Insert(0, upgradeDef);
		}

		public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
		{
			foreach (StatDrawEntry item in base.SpecialDisplayStats(req))
			{
				yield return item;
			}
			foreach(StatDrawEntry item2 in upgradeDef.UpgradeDisplayStats(req))
            {
				yield return item2;
            }
		}
	}
	public class CompMechUpgrade : ThingComp
	{
		public CompProperties_MechUpgrade Props => (CompProperties_MechUpgrade)props;

		public MechUpgrade upgrade;

        public override void PostPostMake()
        {
            base.PostPostMake();
			if (upgrade == null)
			{
				upgrade = MechUpgradeUtility.MakeUpgrade(Props.upgradeDef);
			}
		}

        public override void PostExposeData()
        {
            base.PostExposeData();
			Scribe_Deep.Look(ref upgrade, "upgrade");
		}
    }

	public class CompProperties_FacilityMoteEmitter : CompProperties_MoteEmitter
	{
		public CompProperties_FacilityMoteEmitter()
		{
			compClass = typeof(CompMoteEmitter_Facility);
		}
	}
	public class CompMoteEmitter_Facility : CompMoteEmitter
	{
		public CompFacility facility;

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			facility = parent.TryGetComp<CompFacility>();
			if(facility == null)
            {
				Log.Error("CompMoteEmitter_Facility parent has no CompFacility");
            }
		}

		public override void CompTick()
		{
			if (!parent.Spawned)
			{
				return;
			}
            if (facility.LinkedBuildings.NullOrEmpty() || !facility.CanBeActive)
            {
				return;
            }
            if (!facility.LinkedBuildings.Any((Thing t)=> t.TryGetComp<CompMoteEmitter>()?.MoteLive == true))
            {
				return;
            }
			if (!MoteLive)
			{
				Emit();
			}
			Maintain();
		}
	}

	public class CompProperties_ReloadStation : CompProperties
	{
		public int cooldownTicks;

		public CompProperties_ReloadStation()
		{
			compClass = typeof(CompReloadStation);
		}

        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);
			string s = "Recipes:";
			foreach(RecipeDef def in DefDatabase<RecipeDef>.AllDefs)
			{
				s += "\n" + def.defName;
            }
			Log.Message(s);
        }
	}
	public class CompReloadStation : ThingComp
	{
		public int cooldown;
		public CompProperties_ReloadStation Props => (CompProperties_ReloadStation)props;

		private CompStunnable stunnableComp;

		private bool StunnedByEMP
		{
			get
			{
				if (stunnableComp != null)
				{
					if (stunnableComp.StunHandler.Stunned)
					{
						return stunnableComp.StunHandler.StunFromEMP;
					}
					return false;
				}
				return false;
			}
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			stunnableComp = parent.GetComp<CompStunnable>();
		}

        public override void CompTick()
        {
            base.CompTick();
            if (StunnedByEMP)
            {
				return;
            }
			if(cooldown > 0)
            {
				cooldown--;
            }
        }

        public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Deep.Look(ref cooldown, "cooldown", 10000);
		}
	}
}