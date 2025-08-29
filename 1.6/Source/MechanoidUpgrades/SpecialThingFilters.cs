using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace MU
{
    public class SpecialThingFilterWorker_CorpsesUpgraded : SpecialThingFilterWorker
    {
        public override bool Matches(Thing t)
        {
            if (t is Corpse corpse)
            {
                return corpse.InnerPawn.GetComp<CompUpgradableMechanoid>()?.upgrades?.Empty() == false;
            }
            return false;
        }

        public override bool CanEverMatch(ThingDef def)
        {
            if (!def.IsCorpse || def.ingestible?.sourceDef == null)
            {
                return false;
            }
            if(def.ingestible.sourceDef.GetCompProperties<CompProperties_UpgradableMechanoid>() == null)
            {
                return false;
            }
            return true;
        }

        public override bool AlwaysMatches(ThingDef def)
        {
            return false;
        }
    }

    public class SpecialThingFilterWorker_NotTier : SpecialThingFilterWorker
    {
        public override bool Matches(Thing t)
        {
            if (t.def.defName.ElementAt(t.def.defName.Length - 2) == '_')
            {
                return false;
            }
            return true;
        }

        public override bool CanEverMatch(ThingDef def)
        {
            return def.defName.ElementAt(def.defName.Length - 2) != '_';
        }
    }

    public abstract class SpecialThingFilterWorker_UpgradeTier : SpecialThingFilterWorker
    {
        private readonly char tier;

        protected SpecialThingFilterWorker_UpgradeTier(char tier)
        {
            this.tier = tier;
        }

        public override bool Matches(Thing t)
        {
            if (t.def.defName.EndsWith("_" + tier))
            {
                return t.HasComp<CompMechUpgrade>();
            }
            return false;
        }

        public override bool CanEverMatch(ThingDef def)
        {
            return def.defName.EndsWith("_" + tier);
        }
    }
    public class SpecialThingFilterWorker_UpgradeTier_S : SpecialThingFilterWorker_UpgradeTier
    {
        public SpecialThingFilterWorker_UpgradeTier_S()
            : base('S')
        {
        }
    }

    public class SpecialThingFilterWorker_UpgradeTier_A : SpecialThingFilterWorker_UpgradeTier
    {
        public SpecialThingFilterWorker_UpgradeTier_A()
            : base('A')
        {
        }
    }

    public class SpecialThingFilterWorker_UpgradeTier_B : SpecialThingFilterWorker_UpgradeTier
    {
        public SpecialThingFilterWorker_UpgradeTier_B()
            : base('B')
        {
        }
    }

    public class SpecialThingFilterWorker_UpgradeTier_C : SpecialThingFilterWorker_UpgradeTier
    {
        public SpecialThingFilterWorker_UpgradeTier_C()
            : base('C')
        {
        }
    }
}
