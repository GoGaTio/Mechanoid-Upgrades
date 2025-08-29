using LudeonTK;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

namespace MU
{

    [StaticConstructorOnStartup]
    public class Building_UpgradesImprover : Building_WorkTableAutonomous
    {
        private Mote workingMote;

        private Sustainer workingSound;

        private Graphic topGraphic;

        private CompPowerTrader power;

        public float fabricationSpeed = 1f;

        private static Material FormingCycleBarFilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.98f, 0.46f, 0f));

        private static Material FormingCycleUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0f, 0f, 0f, 0f));

        public CompPowerTrader Power
        {
            get
            {
                if (power == null)
                {
                    power = this.TryGetComp<CompPowerTrader>();
                }
                return power;
            }
        }

        public bool PoweredOn => Power.PowerOn;

        public Thing Upgrade => innerContainer.FirstOrDefault((Thing t) => t.HasComp<CompMechUpgrade>());

        public Corpse ResurrectingMechCorpse => (Corpse)innerContainer.FirstOrDefault((Thing t) => t is Corpse);

        public override void Notify_StartForming(Pawn billDoer)
        {
            SoundDefOf.MechGestatorCycle_Started.PlayOneShot(this);
            fabricationSpeed = billDoer.GetStatValue(MUStatDefOf.MU_UpgradeFabricationSpeed);
        }

        public override void Notify_FormingCompleted()
        {
            Thing thing = Upgrade;
            Thing product = null;
            if (thing != null)
            {
                char c;
                string s = thing.def.defName;
                if (s.EndsWith("C"))
                {
                    c = 'B';
                }
                else if (s.EndsWith("B"))
                {
                    c = 'A';
                }
                else
                {
                    c = 'S';
                }
                ThingDef productDef = DefDatabase<ThingDef>.GetNamedSilentFail(s.Remove(s.Count() - 1) + c);
                if(productDef != null)
                {
                    product = ThingMaker.MakeThing(productDef);
                }
            }
            innerContainer.ClearAndDestroyContents();
            if (product != null)
            {
                innerContainer.TryAdd(product);
            }
            if(ActiveBill.recipe is MU.ImproveRecipeDef def)
            {
                foreach(ThingDefCountClass item in def.yieldThings)
                {
                    Thing item2 = ThingMaker.MakeThing(item.thingDef);
                    item2.stackCount = item.count;
                    innerContainer.TryAdd(item2);
                }
            }
        }

        public override void Notify_HauledTo(Pawn hauler, Thing thing, int count)
        {
            SoundDefOf.MechGestator_MaterialInserted.PlayOneShot(this);
        }

        public override void EjectContents()
        {
            for (int num = innerContainer.Count - 1; num >= 0; num--)
            {
                if (innerContainer[num] is Pawn pawn)
                {
                    innerContainer.RemoveAt(num);
                    pawn.Destroy();
                }
            }
            base.EjectContents();
        }

        protected override void Tick()
        {
            base.Tick();
            if (ActiveBill != null && ActiveBill.State == FormingState.Forming && PoweredOn && activeBill.State != 0)
            {
                if (workingMote == null || workingMote.Destroyed)
                {
                    workingMote = MoteMaker.MakeAttachedOverlay(this, def.building.gestatorFormingMote.GetForRotation(base.Rotation), Vector3.zero);
                }
                workingMote?.Maintain();
                if (workingSound == null || workingSound.Ended)
                {
                    workingSound = SoundDefOf.MechGestator_Ambience.TrySpawnSustainer(this);
                }
                workingSound.Maintain();
            }
            else if (workingSound != null)
            {
                workingSound.End();
                workingSound = null;
            }
        }

        protected override string GetInspectStringExtra()
        {
            if (ActiveBill == null)
            {
                return null;
            }
            return ActiveBill.State.ToString();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            if (activeBill != null && activeBill.State != 0)
            {
                Thing upgrade = Upgrade;
                upgrade.Graphic.Draw(new Vector3(drawLoc.x, AltitudeLayer.BuildingOnTop.AltitudeFor() - 0.01f, drawLoc.z), Rot4.North, upgrade);
            }
            if (topGraphic == null)
            {
                topGraphic = def.building.mechGestatorTopGraphic.GraphicColoredFor(this);
            }
            Vector3 loc2 = new Vector3(drawLoc.x, AltitudeLayer.BuildingOnTop.AltitudeFor(), drawLoc.z);
            topGraphic.Draw(loc2, base.Rotation, this);
        }

        private bool TryGetMechFormingGraphic(out Graphic graphic)
        {
            graphic = null;
            if (ResurrectingMechCorpse != null)
            {
                graphic = ResurrectingMechCorpse.InnerPawn.ageTracker.CurKindLifeStage.bodyGraphicData.Graphic;
            }
            if (graphic != null && graphic.drawSize.x <= def.building.maxFormedMechDrawSize.x && graphic.drawSize.y <= def.building.maxFormedMechDrawSize.y)
            {
                return true;
            }
            graphic = null;
            return false;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            if (DebugSettings.ShowDevGizmos)
            {
                Command_Action command_Action = new Command_Action();
                command_Action.action = delegate
                {
                    //WasteProducer.ProduceWaste(5);
                };
                command_Action.defaultLabel = "DEV: Generate 5 waste";
                yield return command_Action;
                /*if (ActiveMechBill != null && ActiveMechBill.State != 0 && ActiveMechBill.State != FormingState.Formed)
                {
                    Command_Action command_Action2 = new Command_Action();
                    command_Action2.action = ActiveMechBill.ForceCompleteAllCycles;
                    command_Action2.defaultLabel = "DEV: Complete all cycles";
                    yield return command_Action2;
                }*/
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }
    }

    public class Bill_Improve : Bill_Autonomous
    {
        public int StartedTick => startedTick;

        private Building_UpgradesImprover WorkTable => (Building_UpgradesImprover)billStack.billGiver;

        public float WorkSpeedMultiplier
        {
            get
            {
                if (recipe.workSpeedStat != null)
                {
                    return WorkTable.GetStatValue(recipe.workSpeedStat);
                }
                return 1f;
            }
        }

        public override float GetWorkAmount(Thing thing = null)
        {
            return recipe.WorkAmountTotal(thing);
        }

        public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
        {
            base.Notify_IterationCompleted(billDoer, ingredients);
            if (!WorkTable.innerContainer.NullOrEmpty())
            {
                WorkTable.EjectContents();
            }
        }

        public override void Notify_BillWorkFinished(Pawn billDoer)
        {
            base.Notify_BillWorkFinished(billDoer);
            /*if(State == FormingState.Formed)
            {
                WorkTable.EjectContents();
                this.repeatCount--;
            }*/
        }

        /*protected override string StatusString
        {
            get
            {
                switch (base.State)
                {
                    case FormingState.Gathering:
                    case FormingState.Preparing:
                        if (BoundPawn != null)
                        {
                            return "Worker".Translate() + ": " + BoundPawn.LabelShortCap;
                        }
                        break;
                    case FormingState.Forming:
                        return "Gestating".Translate();
                    case FormingState.Formed:
                        if (BoundPawn != null)
                        {
                            return "WaitingFor".Translate() + ": " + BoundPawn.LabelShortCap;
                        }
                        break;
                }
                return null;
            }
        }*/

        protected override float StatusLineMinHeight => 20f;

        protected override Color BaseColor
        {
            get
            {
                if (suspended)
                {
                    return base.BaseColor;
                }
                return Color.white;
            }
        }

        public Bill_Improve()
        {
        }

        public Bill_Improve(RecipeDef recipe, Precept_ThingStyle precept = null)
            : base(recipe, precept)
        {
        }

        public override bool ShouldDoNow()
        {
            return base.ShouldDoNow();
        }

        public override bool PawnAllowedToStartAnew(Pawn p)
        {
            if (!base.PawnAllowedToStartAnew(p))
            {
                return false;
            }
            if (p.GetOverseer() == null && p.mechanitor == null)
            {
                JobFailReason.Is("NotEnoughBandwidth".Translate());
                return false;
            }
            return true;
        }

        public override void Notify_DoBillStarted(Pawn billDoer)
        {
            base.Notify_DoBillStarted(billDoer);
        }

        public override void Reset()
        {
            base.Reset();
        }

        public override void BillTick()
        {
            if (suspended || state != FormingState.Forming || !WorkTable.PoweredOn)
            {
                return;
            }
            formingTicks -= 1f * WorkSpeedMultiplier;
            if (formingTicks <= 0f)
            {
                state = FormingState.Formed;
                WorkTable.Notify_FormingCompleted();
            }
        }

        /*protected override Window GetBillDialog()
        {
            return new Dialog_ImproveBillConfig(this, ((Thing)billStack.billGiver).Position);
        }*/
    }

   /* public class Dialog_ImproveBillConfig : Dialog_BillConfig
    {
        private static float formingInfoHeight;

        private static List<SpecialThingFilterDef> cachedHiddenSpecialThingFilters;

        private static IEnumerable<SpecialThingFilterDef> HiddenSpecialThingFilters
        {
            get
            {
                if (cachedHiddenSpecialThingFilters != null)
                {
                    return cachedHiddenSpecialThingFilters;
                }
                cachedHiddenSpecialThingFilters = new List<SpecialThingFilterDef>();
                return cachedHiddenSpecialThingFilters;
            }
        }

        private List<ThingDef> cachedHiddenSpecialThings;

        private IEnumerable<ThingDef> HiddenSpecialThingDefs
        {
            get
            {
                if (cachedHiddenSpecialThings != null)
                {
                    return cachedHiddenSpecialThings;
                }
                //cachedHiddenSpecialThings = bill.ingredientFilter.AllowedThingDefs.ToList();
                //cachedHiddenSpecialThings.RemoveWhere(x => bill.recipe.fixedIngredientFilter.hiddenSpecialFilters);
                return cachedHiddenSpecialThings;
            }
        }


        [TweakValue("Interface", 0f, 400f)]
        private static int IngredientRadiusSubdialogHeight = 50;

        public Dialog_ImproveBillConfig(Bill_Improve bill, IntVec3 billGiverPos)
            : base(bill, billGiverPos)
        {
        }

        public override void PreOpen()
        {
            base.PreOpen();
            thingFilterState.quickSearch.Reset();
        }

        private ThingFilterUI.UIState thingFilterState = new ThingFilterUI.UIState();

        protected override void DoIngredientConfigPane(float x, ref float y, float width, float height)
        {


            bool flag = true;
            for (int i = 0; i < bill.recipe.ingredients.Count; i++)
            {
                if (!bill.recipe.ingredients[i].IsFixedIngredient)
                {
                    flag = false;
                    break;
                }
            }
            if (!flag)
            {
                Rect rect = new Rect(x, y, width, height - (float)IngredientRadiusSubdialogHeight);
                bool num = bill.GetSlotGroup() == null || bill.recipe.WorkerCounter.CanPossiblyStore(bill, bill.GetSlotGroup());
                ThingFilterUI.DoThingFilterConfigWindow(rect, thingFilterState, bill.ingredientFilter, bill.recipe.fixedIngredientFilter, 4, HiddenSpecialThingDefs, HiddenSpecialThingFilters.ConcatIfNotNull(bill.recipe.forceHiddenSpecialFilters), forceHideHitPointsConfig: false, forceHideQualityConfig: false, showMentalBreakChanceRange: false, bill.recipe.GetPremultipliedSmallIngredients(), bill.Map);
                y += rect.height;
                bool flag2 = bill.GetSlotGroup() == null || bill.recipe.WorkerCounter.CanPossiblyStore(bill, bill.GetSlotGroup());
                if (num && !flag2)
                {
                    Messages.Message("MessageBillValidationStoreZoneInsufficient".Translate(bill.LabelCap, bill.billStack.billGiver.LabelShort.CapitalizeFirst(), SlotGroup.GetGroupLabel(bill.GetSlotGroup())), bill.billStack.billGiver as Thing, MessageTypeDefOf.RejectInput, historical: false);
                }
            }
            Rect rect2 = new Rect(x, y, width, IngredientRadiusSubdialogHeight);
            Listing_Standard listing_Standard = new Listing_Standard();
            listing_Standard.Begin(rect2);
            string text = "IngredientSearchRadius".Translate().Truncate(rect2.width * 0.6f);
            string text2 = ((bill.ingredientSearchRadius == 999f) ? "Unlimited".TranslateSimple().Truncate(rect2.width * 0.3f) : bill.ingredientSearchRadius.ToString("F0"));
            listing_Standard.Label(text + ": " + text2);
            bill.ingredientSearchRadius = listing_Standard.Slider((bill.ingredientSearchRadius > 100f) ? 100f : bill.ingredientSearchRadius, 3f, 100f);
            if (bill.ingredientSearchRadius >= 100f)
            {
                bill.ingredientSearchRadius = 999f;
            }
            listing_Standard.End();
            y += IngredientRadiusSubdialogHeight;
        }
    }*/

    public class ImproveRecipeDef : RecipeDef
    {
        public ThingCategoryDef category;

        public string tier;

        public List<ThingDefCountClass> yieldThings = new List<ThingDefCountClass>();

        override 

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            DeepProfiler.Start("MU.fixedIngredientFilter.ResolveReferences()");
            try
            {
                if(fixedIngredientFilter == null)
                {
                    fixedIngredientFilter = new ThingFilter();
                }
                fixedIngredientFilter.SetDisallowAll();
                foreach (ThingDef def in category.childThingDefs)
                {
                    if (def.defName.EndsWith("_" + tier) && def.GetCompProperties<CompProperties_MechUpgrade>() != null)
                    {
                        fixedIngredientFilter.SetAllow(def, true);
                    }
                }
                fixedIngredientFilter.ResolveReferences();
            }
            finally
            {
                DeepProfiler.End();
            }
            DeepProfiler.Start("MU.defaultIngredientFilter setup");
            try
            {
                defaultIngredientFilter = new ThingFilter();
                defaultIngredientFilter.CopyAllowancesFrom(fixedIngredientFilter);
            }
            finally
            {
                DeepProfiler.End();
            }
            DeepProfiler.Start("MU.defaultIngredientFilter.ResolveReferences()");
            try
            {
                defaultIngredientFilter.ResolveReferences();
            }
            finally
            {
                DeepProfiler.End();
            }
            
        }
    }

    public class PlaceWorker_UpgradeImproverTop : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 loc, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            //GhostUtility.GhostGraphicFor(GraphicDatabase.Get<Graphic_Multi>(def.building.mechGestatorCylinderGraphic.texPath, ShaderDatabase.Cutout, def.building.mechGestatorCylinderGraphic.drawSize, Color.white), def, ghostCol).DrawFromDef(GenThing.TrueCenter(loc, rot, def.Size, AltitudeLayer.MetaOverlays.AltitudeFor()), rot, def);
            GhostUtility.GhostGraphicFor(GraphicDatabase.Get<Graphic_Multi>(def.building.mechGestatorTopGraphic.texPath, ShaderDatabase.Cutout, def.building.mechGestatorTopGraphic.drawSize, Color.white), def, ghostCol).DrawFromDef(GenThing.TrueCenter(loc, rot, def.Size, AltitudeLayer.MetaOverlays.AltitudeFor()), rot, def);
        }
    }
}