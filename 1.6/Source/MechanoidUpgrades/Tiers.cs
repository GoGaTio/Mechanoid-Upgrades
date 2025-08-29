using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using UnityEngine;
using Verse;

namespace MU
{
    public class MechUpgradeTierParms
    {
        public MechUpgradeTierDef tierDef;

        public List<ThingDefCountClass> costList = new List<ThingDefCountClass>();

        public List<ThingDefCountClass> yieldList = new List<ThingDefCountClass>();

        public List<ThingDefCountClass> assemblyCostList = new List<ThingDefCountClass>();

        public List<UpgradeCompProperties> comps = new List<UpgradeCompProperties>();

        public List<UpgradeRestriction> restrictions = new List<UpgradeRestriction>();

        public List<StatModifier> statOffsets = new List<StatModifier>();

        public List<StatModifier> statFactors = new List<StatModifier>();

        public List<DamageFactor> damageFactors = new List<DamageFactor>();

        public AbilityDef ability;

        public Type upgradeClass = typeof(MechUpgrade);

        public MechUpgradeTierParms()
        {
        }

        /*public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            XmlHelper.ParseElements(this, xmlRoot, "tierDef");
        }*/

        public MechUpgradeDef GenerateUpgrade(MechUpgradeTypeDef typeDef, bool hotReload = false)
        {
            string defName = typeDef.defName + "_" + tierDef.postfix;
            MechUpgradeDef def = (hotReload ? (DefDatabase<MechUpgradeDef>.GetNamed(defName, errorOnFail: false) ?? new MechUpgradeDef()) : new MechUpgradeDef());
            def.defName = defName;
            def.uiIconPath = typeDef.iconPath.Formatted(def.defName);
            def.upgradeClass = upgradeClass;
            def.ability = ability;
            def.statFactors = statFactors.ToList();
            def.statOffsets = statOffsets.ToList();
            def.damageFactors = damageFactors.ToList();
            def.comps = comps.ToList();
            def.comps.AddRange(typeDef.comps);
            def.restrictions = restrictions.ToList();
            def.restrictions.AddRange(typeDef.restrictions);
            def.commonality = tierDef.commonality * typeDef.commonality;
            def.modContentPack = typeDef.modContentPack;
            def.yieldList = yieldList.ToList();
            def.exclusionTags = typeDef.exclusionTags.ToList();
            def.exclusionTags.Add(typeDef.defName);
            def.minBodySize = typeDef.minBodySize;
            def.maxBodySize = typeDef.maxBodySize;
            def.weightClasses = typeDef.weightClasses;
            def.upgradePoints = typeDef.upgradePoints;
            
            return def;
        }

        public ThingDef GenerateThing(MechUpgradeTypeDef typeDef, bool hotReload = false)
        {
            string defName = typeDef.defName + "_" + tierDef.postfix;
            ThingDef def = (hotReload ? (DefDatabase<ThingDef>.GetNamed(defName, errorOnFail: false) ?? new ThingDef()) : new ThingDef());
            def.defName = defName;
            def.uiIconPath = typeDef.iconPath.Formatted(def.defName);
            return def;
        }

        public override string ToString()
        {
            return ((tierDef != null) ? tierDef.defName : "null");
        }
    }

    public class MechUpgradeTypeDef : Def
    {
        public List<string> exclusionTags = new List<string>();

        public float commonality = 1f;

        public int upgradePoints = 1;

        public float minBodySize = 0f;

        public float maxBodySize = 0f;

        [NoTranslate]
        public string iconPath;

        public List<UpgradeCompProperties> comps = new List<UpgradeCompProperties>();

        public List<UpgradeRestriction> restrictions = new List<UpgradeRestriction>();

        public List<MechUpgradeTierParms> tiers = new List<MechUpgradeTierParms>();

        public List<MechWeightClassDef> weightClasses = new List<MechWeightClassDef>();

        public void GenerateDefs(bool hotReload = false)
        {
            foreach(MechUpgradeTierParms parms in tiers)
            {

            }
        }
    }

    public class MechUpgradeTierDef : Def
    {
        public Tradeability tradeability = Tradeability.All;

        public string postfix;

        public float commonality = 1f;
    }
}
