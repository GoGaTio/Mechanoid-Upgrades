using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace MU
{
    public class StatPart_Upgrades : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.HasThing && req.Thing.HasComp<CompUpgradableMechanoid>())
            {
                foreach (MechUpgrade u in req.Thing.TryGetComp<CompUpgradableMechanoid>().upgrades)
                {
                    val += MarketValueOffset(u);
                }
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (req.HasThing && req.Thing.HasComp<CompUpgradableMechanoid>())
            {
                string s = "";
                foreach (MechUpgrade u in req.Thing.TryGetComp<CompUpgradableMechanoid>().upgrades)
                {
                    float num = MarketValueOffset(u);
                    s += u.LabelCap + ": " + MarketValueOffset(u).ToStringByStyle(parentStat.toStringStyle, ToStringNumberSense.Offset) + "\n";

                }
                return s;
            }
            return null;
        }

        private static float MarketValueOffset(MechUpgrade u) => u.def.fixedMarketValue ?? u.def.linkedThingDef.BaseMarketValue * u.def.marketValueFactor;
    }

    public class StatPart_FactorFromHolder : StatPart
    {
        public StatDef statDef;

        public override string ExplanationPart(StatRequest req)
        {
            if (req.Thing is ThingWithComps weapon)
            {
                CompEquippable comp = weapon.TryGetComp<CompEquippable>();
                if (comp == null)
                {
                    return null;
                }
                Pawn p = (comp.parent?.ParentHolder as Pawn_EquipmentTracker)?.pawn;
                if (p != null && p.GetStatValue(statDef) != 1f)
                {
                    return "MU_StatsReport_FactorFromHolder".Translate() + ": x" + p.GetStatValue(statDef).ToStringPercent();
                }
            }
            return null;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is ThingWithComps weapon)
            {
                CompEquippable comp = weapon.TryGetComp<CompEquippable>();
                if (comp == null)
                {
                    return;
                }
                Pawn p = (comp.parent?.ParentHolder as Pawn_EquipmentTracker)?.pawn;
                if (p != null)
                {
                    val *= p.GetStatValue(statDef);
                }
            }
        }
    }

    public class StatWorker_Upgradability : StatWorker
    {
        public override bool ShouldShowFor(StatRequest req)
        {
            if (!base.ShouldShowFor(req))
            {
                return false;
            }
            if (req.Thing != null && req.Thing is Pawn pawn)
            {
                return pawn.HasComp<CompUpgradableMechanoid>();
            }
            return false;
        }
    }

    public class StatPart_AdjustableBaseValue : StatPart
    {
        public override string ExplanationPart(StatRequest req)
        {
            return null;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            val = MechUpgradeUtility.Settings.baseUpgradability;
        }
    }
    public class StatPart_UpgradesFromComp : StatPart
    {
        public override string ExplanationPart(StatRequest req)
        {
            return null;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is Pawn pawn && !req.Thing.HasComp<MU.CompUpgradableMechanoid>())
            {
                val *= 0f;
            }
        }
    }

    public class StatPart_UpgradesFromBodySize : StatPart
    {
        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is Pawn pawn) || !req.Thing.HasComp<MU.CompUpgradableMechanoid>())
            {
                return null;
            }
            return "BodySize".Translate() + ": x" + Factor(pawn).ToStringByStyle(ToStringStyle.PercentZero) + " (" + "HealthOffsetScale".Translate("66%") + ")";
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is Pawn pawn && req.Thing.HasComp<MU.CompUpgradableMechanoid>())
            {
                val *= Factor(pawn);
            }
        }

        public static float Factor(Pawn pawn)
        {
            float num = 1f;
            num = ((pawn.BodySize * 2f) + 1f) / 3f;
            return num;
        }
    }

    public class StatPart_UpgradesFromWeightClass : StatPart
    {
        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is Pawn pawn) || !req.Thing.HasComp<MU.CompUpgradableMechanoid>())
            {
                return null;
            }
            return "MechWeightClass".Translate() + ": x" + Factor(pawn).ToStringByStyle(ToStringStyle.PercentZero);
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is Pawn pawn && req.Thing.HasComp<MU.CompUpgradableMechanoid>())
            {
                val *= Factor(pawn);
            }
        }

        public static float Factor(Pawn pawn)
        {
            return pawn.RaceProps?.mechWeightClass?.GetModExtension<MechWeightClassExtension>()?.upgradabilityFactor ?? 1f;
        }
    }

    public class StatPart_RoundToInt : StatPart
    {
        public override string ExplanationPart(StatRequest req)
        {
            return null;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            val = Mathf.RoundToInt(val);
        }
    }

    public class StatPart_CompOffset : StatPart
    {
        public override string ExplanationPart(StatRequest req)
        {
            float offset = 0;
            if(req.Thing is Pawn pawn && (offset = CompOffset(pawn)) > 0)
            {
                return "MU_CompOffset".Translate() + ": x" + offset.ToStringByStyle(ToStringStyle.Integer);
            }
            return null;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is Pawn pawn)
            {
                val += CompOffset(pawn);
            }
        }

        public static float CompOffset(Pawn pawn)
        {
            if(pawn.TryGetComp<CompUpgradableMechanoid>(out CompUpgradableMechanoid comp))
            {
                return comp.upgradabilityOffset;
            }
            return 0;
        }
    }

    public class StatPart_FromMechanitor : StatPart
    {
        public override string ExplanationPart(StatRequest req)
        {
            return null;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            Pawn overseer;
            if (req.Thing is Pawn pawn && (overseer = pawn.GetOverseer()) != null)
            {
                val = overseer.GetStatValue(parentStat);
            }
        }
    }
}
