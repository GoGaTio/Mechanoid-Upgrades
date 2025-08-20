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
    public class Building_UpgradesImprover : Building, IHaulDestination, IStoreSettingsParent, IHaulEnroute, IThingHolder
    {
		protected StorageSettings storageSettings;

		protected ThingOwner innerContainer;

		public Building_UpgradesImprover()
		{
			innerContainer = new ThingOwner<Thing>(this, oneStackOnly: false);
		}

		public bool StorageTabVisible => true;

		public StorageSettings GetStoreSettings()
		{
			return storageSettings;
		}

		public bool Accepts(Thing thing)
		{
			return innerContainer.CanAcceptAnyOf(thing) && GetStoreSettings().AllowedToAccept(thing);
		}

		public StorageSettings GetParentStoreSettings()
		{
			return def.building.fixedStorageSettings;
		}

		public void Notify_SettingsChanged()
		{
		}

		public ThingOwner GetDirectlyHeldThings()
		{
			return innerContainer;
		}

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
		}

		public int SpaceRemainingFor(ThingDef thing)
		{
			if (innerContainer.Count <= 1)
			{
				return 1;
			}
			return 0;
		}
	}
}