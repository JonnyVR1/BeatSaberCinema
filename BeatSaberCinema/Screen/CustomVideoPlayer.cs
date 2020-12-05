﻿using System;
using System.Reflection;
using BS_Utils.Utilities;
using UnityEngine;
using UnityEngine.Video;

namespace BeatSaberCinema
{
	public class CustomVideoPlayer: MonoBehaviour
	{
		public VideoPlayer Player { get; }
		private readonly AudioSource _videoPlayerAudioSource;
		private readonly Screen _screen;
		private readonly Renderer _screenRenderer;
		private Shader _glowShader = null!;

		private readonly Vector3 _gameplayPosition = new Vector3(0, 12.40f, 68);
		private readonly Vector3 _menuPosition = new Vector3(0, 12.40f, 56);
		private readonly Vector3 _gameplayRotation = new Vector3(352, 0, 0);
		private readonly Vector3 _gameplayScale = new Vector3(1, 25, 0.1f);

		private const string MAIN_TEXTURE_NAME = "_MainTex";

		private const float SCREEN_BRIGHTNESS = 0.92f;
		private readonly Color _screenColorOn = Color.white.ColorWithAlpha(0f) * SCREEN_BRIGHTNESS;
		private readonly Color _screenColorOff = Color.clear;
		private static readonly int MainTex = Shader.PropertyToID(MAIN_TEXTURE_NAME);
		private bool _waitForFirstFrame;

		private float _correctPlaybackSpeed = 1.0f;

		public float PlaybackSpeed
		{
			get => Player.playbackSpeed;
			set
			{
				if (!IsSyncing)
				{
					_correctPlaybackSpeed = value;
				}

				Player.playbackSpeed = value;
			}
		}

		public float FrameDuration => Player.frameRate / 1000f;
		public float Volume
		{
			set => _videoPlayerAudioSource.volume = value;
		}

		public float PanStereo
		{
			set => _videoPlayerAudioSource.panStereo = value;
		}

		public string Url
		{
			get => Player.url;
			set => Player.url = value;
		}

		public bool IsPlaying => Player.isPlaying;
		public bool IsPrepared => Player.isPrepared;
		[NonSerialized] public bool IsSyncing;
		[NonSerialized] public int OutOfSyncFrames;

		public CustomVideoPlayer()
		{
			_screen = CreateScreen();
			_screenRenderer = _screen.GetRenderer();
			_screenRenderer.material = new Material(GetShader()) {color = _screenColorOff};
			if (_glowShader == null)
			{
				Plugin.Logger.Error("SHADER WAS NULL");
			}

			Player = gameObject.AddComponent<VideoPlayer>();
			Player.source = VideoSource.Url;
			Player.isLooping = false;
			Player.renderMode = VideoRenderMode.MaterialOverride;
			Player.targetMaterialProperty = MAIN_TEXTURE_NAME;
			Player.targetMaterialRenderer = _screenRenderer;

			Player.playOnAwake = false;
			Player.waitForFirstFrame = true;
			Player.errorReceived += VideoPlayerErrorReceived;
			Player.prepareCompleted += VideoPlayerPrepareComplete;
			Player.started += VideoPlayerStarted;

			//TODO PanStereo does not work as expected with this AudioSource. Panning fully to one side is still slightly audible in the other.
			_videoPlayerAudioSource = gameObject.AddComponent<AudioSource>();
			_videoPlayerAudioSource.reverbZoneMix = 0;
			_videoPlayerAudioSource.spatialize = false;
			_videoPlayerAudioSource.spatialBlend = 0.0f;
			_videoPlayerAudioSource.playOnAwake = false;
			Player.audioOutputMode = VideoAudioOutputMode.AudioSource;
			Player.SetTargetAudioSource(0, _videoPlayerAudioSource);

			BSEvents.menuSceneLoaded += OnMenuSceneLoaded;
			BSEvents.gameSceneLoaded += OnGameSceneLoaded;
		}

		private Screen CreateScreen()
		{
			var screen =  gameObject.AddComponent<Screen>();
			screen.SetTransform(transform);
			screen.SetPlacement(_menuPosition, _gameplayRotation, _gameplayScale);
			screen.Show();

			return screen;
		}

		private void OnMenuSceneLoaded()
		{
			SetPlacement(_menuPosition, _gameplayRotation, _gameplayScale.y);
		}

		private void OnGameSceneLoaded()
		{
			SetPlacement(_gameplayPosition, _gameplayRotation, _gameplayScale.y);
		}

		private Shader GetShader()
		{
			if (_glowShader != null)
			{
				return _glowShader;
			}

			var bundle = UIUtilities.GetResource(Assembly.GetExecutingAssembly(), "BeatSaberCinema.Resources.bscinema.bundle");
			if (bundle == null || bundle.Length == 0)
			{
				Plugin.Logger.Error("GetResource failed");
				return Shader.Find("Hidden/BlitAdd");
			}
			var myLoadedAssetBundle = AssetBundle.LoadFromMemory(bundle);
			if (myLoadedAssetBundle == null)
			{
				Plugin.Logger.Error("LoadFromMemory failed");
				return Shader.Find("Hidden/BlitAdd");
			}

			Shader shader = myLoadedAssetBundle.LoadAsset<Shader>("ScreenShader");

			myLoadedAssetBundle.Unload(false);

			_glowShader = shader;
			return shader;
		}

		public void ResetPlaybackSpeed()
		{
			Player.playbackSpeed = _correctPlaybackSpeed;
			IsSyncing = false;
		}

		public void SetPlacement(SerializableVector3? position, SerializableVector3? rotation, float? height)
		{
			//Scale doesnt need to be a vector. Width is calculated based on height and aspect ratio. Depth is a constant value.
			var scale = _gameplayScale;
			scale.y = height ?? _gameplayScale.y;
			_screen.SetPlacement(position ?? _gameplayPosition, rotation ?? _gameplayRotation, scale);
		}

		private void FrameReady(VideoPlayer player, long frame)
		{
			//This is done because the video screen material needs to be set to white, otherwise no video would be visible.
			//When no video is playing, we want it to be black though to not blind the user.
			//If we set the white color when calling Play(), a few frames of white screen are still visible.
			//So, we wait before the player renders its first frame and then set the color, making the switch invisible.
			if (_waitForFirstFrame)
			{
				_screenRenderer.material.color = _screenColorOn;
				_screen.SetAspectRatio(GetAspectRatio());
				_waitForFirstFrame = false;
				Player.frameReady -= FrameReady;

			}
		}

		public void Show()
		{
			_screen.Show();
		}

		public void Hide()
		{
			_screen.Hide();
		}

		public void Play()
		{
			_waitForFirstFrame = true;
			Player.frameReady += FrameReady;
			Player.Play();
		}

		public void Pause()
		{
			Player.Pause();
		}

		public void Stop()
		{
			Player.Stop();
			_screen.SetAspectRatio(GetAspectRatio());
			_screenRenderer.material.color = _screenColorOff;
		}

		public void Prepare()
		{
			Player.Prepare();
		}

		public void Update()
		{
			//This is required for the CustomBloomPrePass
			_screen.GetRenderer().material.SetTexture(MainTex, Player.texture);
		}

		private static void VideoPlayerPrepareComplete(VideoPlayer source)
		{
			Plugin.Logger.Debug("Video player prepare complete");
			var texture = source.texture;
			Plugin.Logger.Debug($"Video resolution: {texture.width}x{texture.height}");
		}

		private static void VideoPlayerStarted(VideoPlayer source)
		{
			Plugin.Logger.Debug("Video player started event");
		}

		private static void VideoPlayerErrorReceived(VideoPlayer source, string message)
		{
			if (message == "Can't play movie []")
			{
				//Expected when preparing null source
				return;
			}
			Plugin.Logger.Error("Video player error: " + message);
		}

		private float GetAspectRatio()
		{
			var texture = Player.texture;
			if (texture == null || texture.width == 0 || texture.height == 0)
			{
				return 16f / 9f;
			}
			return (float) texture.width / texture.height;
		}

		public void Mute()
		{
			Volume = 0;
		}

		public void Unmute()
		{
			Volume = 0.8f;
		}
	}
}