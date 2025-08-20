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

namespace MU
{
	public class CompProperties_AbilityReloadableUpgrade : CompProperties_AbilityEffect
	{
		public ThingDef ammoDef;

		public int ammoCount;

		public int reloadTicks = 180;

		public SoundDef soundReload;

		[MustTranslate]
		public string chargeNoun = "charge";

		public CompProperties_AbilityReloadableUpgrade()
		{
			compClass = typeof(CompAbilityEffect_ReloadableUpgrade);
		}
	}
	public class CompAbilityEffect_ReloadableUpgrade : CompAbilityEffect
	{
		public bool NeedsReload => parent.RemainingCharges < parent.maxCharges;
		public new CompProperties_AbilityReloadableUpgrade Props => (CompProperties_AbilityReloadableUpgrade)props;

		public int AbilityChargesNeed => parent.RemainingCharges;

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
			return true;
        }

        public bool HaveAmmoToReload => !RefuelWorkGiverUtility.FindEnoughReservableThings(parent.pawn, parent.pawn.Position, new IntRange(Props.ammoCount, Props.ammoCount), (Thing t) => t.def == Props.ammoDef).NullOrEmpty();

		public void Reload(Thing ammo, bool devReload = false)
        {
			if (!NeedsReload)
			{
				return;
			}
            if (devReload)
            {
				parent.RemainingCharges = parent.maxCharges;
			}
            else
            {
				for(int i = parent.RemainingCharges; i < parent.maxCharges; i++)
                {
					if (ammo.stackCount < Props.ammoCount)
					{
						break;
					}
					ammo.SplitOff(Props.ammoCount).Destroy();
					int charges = parent.RemainingCharges + 1;
					parent.RemainingCharges = charges;
				}
			}
			MechUpgrade upgrade = parent.pawn.TryGetComp<MU.CompUpgradableMechanoid>().upgrades.FirstOrDefault((MechUpgrade u) => u.def.ability == parent.def);
			if (upgrade != null)
			{
				parent.pawn.Drawer.renderer.SetAllGraphicsDirty();
				upgrade.charges = parent.RemainingCharges;
			}
			if (Props.soundReload != null)
			{
				Props.soundReload.PlayOneShot(new TargetInfo(parent.pawn.Position, parent.pawn.Map));
			}
        }

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			base.Apply(target, dest);
			MechUpgrade upgrade = parent.pawn.TryGetComp<MU.CompUpgradableMechanoid>().upgrades.FirstOrDefault((MechUpgrade u) => u.def.ability == parent.def);
			if (upgrade != null)
			{
				upgrade.charges = parent.RemainingCharges;
			}
			parent.pawn.TryGetComp<MU.CompUpgradableMechanoid>().Notify_ChargeUsed(parent);
		}
	}

	public class CompProperties_AbilityProjectileWithMissRadius : CompProperties_AbilityEffect
	{
		public ThingDef projectileDef;

		public float forcedMissRadius;

		public CompProperties_AbilityProjectileWithMissRadius()
		{
			compClass = typeof(CompAbilityEffect_ProjectileWithMissRadius);
		}
	}
	public class CompAbilityEffect_ProjectileWithMissRadius : CompAbilityEffect
	{
		public new CompProperties_AbilityProjectileWithMissRadius Props => (CompProperties_AbilityProjectileWithMissRadius)props;

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			base.Apply(target, dest);
			LaunchProjectile(target);
		}

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
			GenDraw.DrawRadiusRing(target.Cell, Props.projectileDef.projectile.explosionRadius);
			if(Props.forcedMissRadius > 0)
            {
				GenDraw.DrawRadiusRing(target.Cell, Props.projectileDef.projectile.explosionRadius + Props.forcedMissRadius, Color.red);
			}
			base.DrawEffectPreview(target);
        }
        private void LaunchProjectile(LocalTargetInfo target)
		{
			if (Props.projectileDef != null && Props.forcedMissRadius > 0)
			{
				Pawn pawn = parent.pawn; 
				int num = Rand.Range(0, GenRadial.NumCellsInRadius(Props.forcedMissRadius));
				IntVec3 c = target.Cell + GenRadial.RadialPattern[num];
				((Projectile)GenSpawn.Spawn(Props.projectileDef, pawn.Position, pawn.Map)).Launch(pawn, pawn.DrawPos, c, c, ProjectileHitFlags.All);
			}
		}

		public override bool AICanTargetNow(LocalTargetInfo target)
		{
			return target.Pawn != null;
		}
	}

	public class CompProperties_AbilityLaunchDisruptorFlare : CompProperties_AbilityEffect
	{
		public ThingDef projectileDef;

		public float ringRadius;

		public CompProperties_AbilityLaunchDisruptorFlare()
		{
			compClass = typeof(CompAbilityEffect_LaunchDisruptorFlare);
		}
	}
	public class CompAbilityEffect_LaunchDisruptorFlare : CompAbilityEffect
	{
		public new CompProperties_AbilityLaunchDisruptorFlare Props => (CompProperties_AbilityLaunchDisruptorFlare)props;

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			base.Apply(target, dest);
            if (!ModsConfig.IsActive("CETeam.CombatExtended") && !ModsConfig.IsActive("CETeam.CombatExtended_steam"))
            {
				LaunchProjectile(target);
			}
		}

		public override void DrawEffectPreview(LocalTargetInfo target)
		{
			GenDraw.DrawRadiusRing(target.Cell, Props.projectileDef.projectile.explosionRadius, Color.yellow);
			base.DrawEffectPreview(target);
		}
		private void LaunchProjectile(LocalTargetInfo target)
		{
			if (Props.projectileDef != null)
			{
				Pawn pawn = parent.pawn;
				IntVec3 c = target.Cell;
				((Projectile)GenSpawn.Spawn(Props.projectileDef, pawn.Position, pawn.Map)).Launch(pawn, pawn.DrawPos, c, c, ProjectileHitFlags.All);
			}
		}

		public override bool AICanTargetNow(LocalTargetInfo target)
		{
			return target.Pawn != null;
		}
	}


	public class CompProperties_AICastableAbility : CompProperties_AbilityEffect //base for future ai-castable abilities
	{
		public int cooldown = 0;

		public bool incendiary;

		public bool castableByPlayerMechs = false;

		public CompProperties_AICastableAbility()
		{
			compClass = typeof(CompAbilityEffect_AICastable);
		}
	}

	public class CompAbilityEffect_AICastable : CompAbilityEffect
	{
		public new CompProperties_AICastableAbility Props => (CompProperties_AICastableAbility)props;

		public int cooldown = 0;

		public override bool AICanTargetNow(LocalTargetInfo target)
		{
			if(!Props.castableByPlayerMechs && parent.pawn.Faction == Faction.OfPlayer)
            {
				return false;
            }
            if (Props.incendiary && target.Thing?.FlammableNow != true)
            {
				return false;
            }
			if (cooldown > 0)
			{
				return false;
			}
			return true;
		}
		public override void CompTick()
		{
			if (cooldown > 0)
			{
				cooldown--;
			}
			base.CompTick();
		}
		public override void PostApplied(List<LocalTargetInfo> targets, Map map)
		{
			cooldown = Props.cooldown;
			base.PostApplied(targets, map);
		}
	}
}