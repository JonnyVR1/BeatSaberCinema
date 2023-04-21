using System.IO;
using BS_Utils.Utilities;
using IPA;
using IPA.Config.Stores;
using IPA.Logging;
using IPA.Utilities;
using JetBrains.Annotations;
using SongCore;
using Config = IPA.Config.Config;

namespace BeatSaberCinema
{
	[Plugin(RuntimeOptions.DynamicInit)]
	[UsedImplicitly]
	public class Plugin
	{
		internal const string CAPABILITY = "Cinema";
		private HarmonyPatchController? _harmonyPatchController;
		private static bool _enabled;

		public static bool Enabled
		{
			get => _enabled && SettingsStore.Instance.PluginEnabled;
			private set => _enabled = value;
		}

		[Init]
		[UsedImplicitly]
		public void Init(Logger ipaLogger, Config config)
		{
			Log.IpaLogger = ipaLogger;
			SettingsStore.Instance = config.Generated<SettingsStore>();
			VideoMenu.instance.AddTab();
			Log.Debug("Plugin initialized");
		}

		[OnStart]
		[UsedImplicitly]
		public void OnApplicationStart()
		{
			Log.Debug("Hardware info:\n"+Util.GetHardwareInfo(), true);
			BSEvents.OnLoad();
			VideoLoader.Init();
		}

		private static void OnMenuSceneLoadedFresh(ScenesTransitionSetupDataSO scenesTransition)
		{
			VideoMenu.instance.Init();
			SongPreviewPlayerController.Init();
		}

		[OnEnable]
		[UsedImplicitly]
		public void OnEnable()
		{
			Enabled = true;
			PlaybackController.Create();
			VideoMenu.instance.Init();
			BSEvents.lateMenuSceneLoadedFresh += OnMenuSceneLoadedFresh;
			_harmonyPatchController = new HarmonyPatchController();
			ApplyHarmonyPatches();
			SettingsUI.CreateMenu();
			VideoMenu.instance.AddTab();
			EnvironmentController.Init();
			Collections.RegisterCapability(CAPABILITY);
			if (File.Exists(Path.Combine(UnityGame.InstallPath, "dxgi.dll")))
			{
				Log.Warn("dxgi.dll is present, video may fail to play. To fix this, delete the file dxgi.dll from your main Beat Saber folder (not in Plugins).");
			}

			//No need to index maps if the filter isn't going to be applied anyway
			if (Util.IsModInstalled("BetterSongList", "0.3.2"))
			{
				Loader.SongsLoadedEvent += VideoLoader.IndexMaps;
			}
		}

		[OnDisable]
		[UsedImplicitly]
		public void OnDisable()
		{
			Enabled = false;
			BSEvents.lateMenuSceneLoadedFresh -= OnMenuSceneLoadedFresh;
			Loader.SongsLoadedEvent -= VideoLoader.IndexMaps;
			RemoveHarmonyPatches();
			_harmonyPatchController = null;
			SettingsUI.RemoveMenu();

			//TODO Destroying and re-creating the PlaybackController messes up the VideoMenu without any exceptions in the log. Investigate.
			//PlaybackController.Destroy();

			VideoMenu.instance.RemoveTab();
			EnvironmentController.Disable();
			VideoLoader.StopFileSystemWatcher();
			Collections.DeregisterizeCapability(CAPABILITY);
		}

		private void ApplyHarmonyPatches()
		{
			_harmonyPatchController?.PatchAll();
		}

		private void RemoveHarmonyPatches()
		{
			_harmonyPatchController?.UnpatchAll();
		}
	}
}