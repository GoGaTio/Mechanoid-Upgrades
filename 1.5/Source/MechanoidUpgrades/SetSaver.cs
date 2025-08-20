using System;
using System.IO;
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

	public class Dialog_SaveUpgradeSet : Window
	{
		protected readonly UpgradeSet set;

		private string curName;

		protected virtual int MaxNameLength => 28;

		public override Vector2 InitialSize => new Vector2(280f, 175f);

		public Dialog_SaveUpgradeSet(UpgradeSet set)
		{
			this.set = set;
			curName = set.name ?? "";
			doCloseX = false;
			forcePause = true;
			closeOnClickedOutside = true;
			absorbInputAroundWindow = true;
			closeOnAccept = false;
			closeOnCancel = false;
		}

		protected AcceptanceReport NameIsValid(string name)
		{
			return name.Length != 0;
		}

		public override void DoWindowContents(Rect inRect)
		{
			Text.Font = GameFont.Small;
			bool flag = false;
			if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
			{
				flag = true;
				Event.current.Use();
			}
			Rect rect = new Rect(inRect);
			Text.Font = GameFont.Medium;
			rect.height = Text.LineHeight + 10f;
			Widgets.Label(rect, "SaveAs".Translate());
			Text.Font = GameFont.Small;
			GUI.SetNextControlName("RenameField");
			string text = Widgets.TextField(new Rect(0f, rect.height, inRect.width, 35f), curName);
			if (text.Length < MaxNameLength)
			{
				curName = text;
			}
			if (!(Widgets.ButtonText(new Rect(15f, inRect.height - 35f - 10f, inRect.width - 15f - 15f, 35f), "Save".Translate()) || flag))
			{
				return;
			}
			AcceptanceReport acceptanceReport = NameIsValid(curName);
			if (!acceptanceReport.Accepted)
			{
				if (acceptanceReport.Reason.NullOrEmpty())
				{
					Messages.Message("NameIsInvalid".Translate(), MessageTypeDefOf.RejectInput, historical: false);
				}
				else
				{
					Messages.Message(acceptanceReport.Reason, MessageTypeDefOf.RejectInput, historical: false);
				}
				return;
			}
			set.name = curName;
			set.fileName = GenFile.SanitizedFileName(curName);
			string absPath = UpgradeFilePaths.AbsFilePathForUpgradeSet(set.fileName);
			LongEventHandler.QueueLongEvent(delegate
			{
				UpgradeFilePaths.SaveUpgradeSet(set, absPath);
			}, "SavingLongEvent", doAsynchronously: false, null);
			Messages.Message("SavedAs".Translate(set.fileName), MessageTypeDefOf.SilentInput, historical: false);
			Find.WindowStack.TryRemove(this);
		}
	}


	public abstract class Dialog_UpgradeSetList : Dialog_FileList
	{
		protected override void ReloadFiles()
		{
			files.Clear();
			foreach (FileInfo allCustomUpgradeSetFile in UpgradeFilePaths.AllCustomUpgradeSetFiles)
			{
				try
				{
					SaveFileInfo saveFileInfo = new SaveFileInfo(allCustomUpgradeSetFile);
					saveFileInfo.LoadData();
					files.Add(saveFileInfo);
				}
				catch (Exception ex)
				{
					Log.Error("Exception loading " + allCustomUpgradeSetFile.Name + ": " + ex.ToString());
				}
			}
		}
	}
	public class Dialog_UpgradeSetList_Load : Dialog_UpgradeSetList
	{
		private Action<UpgradeSet> setReturner;

		public Dialog_UpgradeSetList_Load(Action<UpgradeSet> setReturner)
		{
			this.setReturner = setReturner;
			interactButLabel = "LoadGameButton".Translate();
			deleteTipKey = "DeleteThisUpgradeSet";
		}

		protected override void DoFileInteraction(string fileName)
		{
			string filePath = UpgradeFilePaths.AbsFilePathForUpgradeSet(fileName);
			PreLoadUtility.CheckVersionAndLoad(filePath, ScribeMetaHeaderUtility.ScribeHeaderMode.Xenotype, delegate
			{
				if (UpgradeFilePaths.TryLoadUpgradeSet(filePath, out var set))
				{
					setReturner(set);
				}
				Close();
			});
		}
	}
	public class UpgradeSet : IExposable
	{
		public string fileName;

		public string name = "";

		public List<MechUpgradeDef> upgrades = new List<MechUpgradeDef>();

		public void ExposeData()
		{
			Scribe_Values.Look(ref name, "name");
			Scribe_Collections.Look(ref upgrades, "upgrades", LookMode.Def);
			if (Scribe.mode == LoadSaveMode.PostLoadInit && upgrades.RemoveAll((MechUpgradeDef x) => x == null) > 0)
			{
				Log.Error("Removed null upgrades from GoGaTio.MechanoidUpgrades mod");
			}
		}
	}

	public static class UpgradeFilePaths
	{

		private static string UpgradeSetFolderPath => FolderUnderSaveData("MechanoidUpgradesSets");

		public static IEnumerable<FileInfo> AllCustomUpgradeSetFiles
		{
			get
			{
				DirectoryInfo directoryInfo = new DirectoryInfo(UpgradeSetFolderPath);
				if (!directoryInfo.Exists)
				{
					directoryInfo.Create();
				}
				return from f in directoryInfo.GetFiles()
					   where f.Extension == ".mus"
					   orderby f.LastWriteTime descending
					   select f;
			}
		}

		private static string FolderUnderSaveData(string folderName)
		{
			string text = Path.Combine(GenFilePaths.SaveDataFolderPath, folderName);
			DirectoryInfo directoryInfo = new DirectoryInfo(text);
			if (!directoryInfo.Exists)
			{
				directoryInfo.Create();
			}
			return text;
		}

		public static string AbsFilePathForUpgradeSet(string upgradeSetName)
		{
			return Path.Combine(UpgradeSetFolderPath, upgradeSetName + ".mus");
		}

		public static void SaveUpgradeSet(UpgradeSet upgradeSet, string absFilePath)
		{
			try
			{
				upgradeSet.fileName = Path.GetFileNameWithoutExtension(absFilePath);
				SafeSaver.Save(absFilePath, "savedUpgradeSet", delegate
				{
					ScribeMetaHeaderUtility.WriteMetaHeader();
					Scribe_Deep.Look(ref upgradeSet, "upgradeSet");
				});
			}
			catch (Exception ex)
			{
				Log.Error("Exception while saving upgradeSet from GoGaTio.MechanoidUpgrades mod: " + ex.ToString());
			}
		}

		public static bool TryLoadUpgradeSet(string absPath, out UpgradeSet upgradeSet)
		{
			upgradeSet = null;
			try
			{
				Scribe.loader.InitLoading(absPath);
				try
				{
					ScribeMetaHeaderUtility.LoadGameDataHeader(ScribeMetaHeaderUtility.ScribeHeaderMode.Xenotype, logVersionConflictWarning: true);
					Scribe_Deep.Look(ref upgradeSet, "upgradeSet");
					Scribe.loader.FinalizeLoading();
				}
				catch
				{
					Scribe.ForceStop();
					throw;
				}
				upgradeSet.fileName = Path.GetFileNameWithoutExtension(new FileInfo(absPath).Name);
			}
			catch (Exception ex)
			{
				Log.Error("Exception loading upgradeSet from GoGaTio.MechanoidUpgrades mod: " + ex.ToString());
				upgradeSet = null;
				Scribe.ForceStop();
			}
			return upgradeSet != null;
		}
	}
}