using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace MU
{
    public class MechanoidUpgradesSettings : ModSettings
    {

        public bool generateWithUpgrades = true;

        public float chanceForUpgrades = 0.25f;

        public float minUpgradePercent = 0.35f;

        public float maxUpgradePercent = 1f;

        public float combinationsCommonality = 0.25f;

        public bool extraButcherProducts = true;

        public float upgradesButcherRatio = 0.4f;

        public float upgradesButcherYield = 0.25f;

        public int baseUpgradability = 8;

        public bool hideNotInstallable = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref generateWithUpgrades, "generateWithUpgrades", true);
            Scribe_Values.Look(ref chanceForUpgrades, "chanceForUpgrades", 0.25f);
            Scribe_Values.Look(ref minUpgradePercent, "minUpgradePercent", 0.35f);
            Scribe_Values.Look(ref maxUpgradePercent, "maxUpgradePercent", 1f);
            Scribe_Values.Look(ref combinationsCommonality, "combinationsCommonality", 0.25f);
            Scribe_Values.Look(ref baseUpgradability, "baseUpgradability", 8);
            Scribe_Values.Look(ref extraButcherProducts, "extraButcherProducts", true);
            Scribe_Values.Look(ref upgradesButcherRatio, "upgradesButcherRatio", 0.4f);
            Scribe_Values.Look(ref upgradesButcherYield, "upgradesButcherYield", 0.25f);
            Scribe_Values.Look(ref hideNotInstallable, "hideNotInstallable", false);
            base.ExposeData();
        }
    }

    public class MechanoidUpgradesMod : Mod
    {

        MechanoidUpgradesSettings settings;

        public MechanoidUpgradesMod(ModContentPack content) : base(content)
        {
            this.settings = GetSettings<MechanoidUpgradesSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            if (listingStandard.ButtonText("ResetButton".Translate()))
            {
                settings.generateWithUpgrades = true;
                settings.chanceForUpgrades = 0.25f;
                settings.minUpgradePercent = 0.35f;
                settings.maxUpgradePercent = 1f;
                settings.combinationsCommonality = 0.25f;
                settings.extraButcherProducts = true;
                settings.upgradesButcherRatio = 0.4f;
                settings.upgradesButcherYield = 0.25f;
                settings.baseUpgradability = 8;
            }
            listingStandard.CheckboxLabeled("MU_Setting_GenerateWithUpgrades".Translate(), ref settings.generateWithUpgrades, "MU_Setting_GenerateWithUpgrades_Desc".Translate());
            if (settings.generateWithUpgrades)
            {
                settings.chanceForUpgrades = listingStandard.SliderLabeled("MU_Setting_ChanceForUpgrades".Translate() + ": " + settings.chanceForUpgrades.ToStringByStyle(ToStringStyle.PercentZero), settings.chanceForUpgrades, 0f, 1f, 0.5f, "MU_Setting_ChanceForUpgrades_Desc".Translate());
                settings.minUpgradePercent = listingStandard.SliderLabeled("MU_Setting_MinPercent".Translate() + ": " + settings.minUpgradePercent.ToStringByStyle(ToStringStyle.PercentZero), settings.minUpgradePercent, 0f, 1f, 0.5f, "MU_Setting_MinPercent_Desc".Translate());
                settings.maxUpgradePercent = listingStandard.SliderLabeled("MU_Setting_MaxPercent".Translate() + ": " + settings.maxUpgradePercent.ToStringByStyle(ToStringStyle.PercentZero), settings.maxUpgradePercent, 0.05f, 1f, 0.5f, "MU_Setting_MaxPercent_Desc".Translate());
                settings.combinationsCommonality = listingStandard.SliderLabeled("MU_Setting_CombinationsCommonality".Translate() + ": " + settings.combinationsCommonality.ToStringByStyle(ToStringStyle.PercentZero), settings.combinationsCommonality, 0f, 1f, 0.5f, "MU_Setting_CombinationsCommonality_Desc".Translate());
            }
            listingStandard.CheckboxLabeled("MU_Setting_ExtraButcherProducts".Translate(), ref settings.extraButcherProducts, "MU_Setting_ExtraButcherProducts_Desc".Translate());
            if (settings.extraButcherProducts)
            {
                settings.upgradesButcherRatio = listingStandard.SliderLabeled("MU_Setting_ChanceForExtraLoot".Translate() + ": " + settings.upgradesButcherRatio.ToStringByStyle(ToStringStyle.PercentZero), settings.upgradesButcherRatio, 0.05f, 1f, 0.5f, "MU_Setting_ChanceForExtraLoot_desc".Translate());
                settings.upgradesButcherYield = listingStandard.SliderLabeled("MU_Setting_ExtraLootAmount".Translate() + ": " + settings.upgradesButcherYield.ToStringByStyle(ToStringStyle.PercentZero), settings.upgradesButcherYield, 0.05f, 1f, 0.5f, "MU_Setting_ExtraLootAmount_Desc".Translate());
            }
            settings.baseUpgradability = Mathf.RoundToInt(listingStandard.SliderLabeled("MU_Setting_BaseUpgradability".Translate() + ": " + settings.baseUpgradability.ToString(), settings.baseUpgradability, 2f, 16f, 0.5f, "MU_Setting_BaseUpgradability_Desc".Translate()));
            if (settings.baseUpgradability != 8)
            {
                listingStandard.Label("MU_Setting_BaseUpgradability_Warning".Translate());
            }
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }
        public override string SettingsCategory()
        {
            return "Mechanoid Upgrades";
        }
    }
}
