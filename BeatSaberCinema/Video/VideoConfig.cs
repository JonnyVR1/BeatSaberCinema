﻿using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace BeatSaberCinema
{
	public enum DownloadState { NotDownloaded, Downloading, Downloaded, Cancelled }

	[Serializable]
	[JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
	public class VideoConfig
	{
		private const int CURREN_FORMAT_VERSION = 1;

		[JsonRequired] public string? videoID;
		public string? title;
		public string? author;
		public string? videoFile;
		public int duration; //s
		public int offset; //in ms
		public int formatVersion = CURREN_FORMAT_VERSION;
		public bool loop;
		public bool? configByMapper;

		public SerializableVector3? screenPosition;
		public SerializableVector3? screenRotation;
		public float? screenHeight;
		public bool? disableBigMirrorOverride;

		public EnvironmentModification[]? environment;

		[JsonIgnore, NonSerialized] public DownloadState DownloadState;
		[JsonIgnore, NonSerialized] public bool BackCompat;
		[JsonIgnore, NonSerialized] public bool NeedsToSave;
		[JsonIgnore, NonSerialized] public float DownloadProgress;
		[JsonIgnore] public string? VideoPath
		{
			get
			{
				if (videoFile != null && IsLocal)
				{
					return Path.Combine(LevelDir, videoFile);
				}

				if (videoFile != null && !IsLocal)
				{
					return videoFile;
				}

				return null;
			}
		}

		[JsonIgnore] public string LevelDir => VideoLoader.GetLevelPath(Level);
		[JsonIgnore] public IPreviewBeatmapLevel Level = null!;
		[JsonIgnore] public bool IsLocal => videoFile != null && !videoFile.StartsWith("http");
		[JsonIgnore] public bool IsPlayable => DownloadState == DownloadState.Downloaded;


		private static Regex _regexParseID = new Regex(@"\/watch\?v=([a-z0-9_-]*)",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		public VideoConfig()
		{
			//Intentionally empty. Used as ctor for JSON deserializer
		}

		public VideoConfig(VideoConfigListBackCompat configListBackCompat)
		{
			var configBackCompat = configListBackCompat.videos?[configListBackCompat.activeVideo];
			if (configBackCompat == null)
			{
				throw new ArgumentException("Json file was not a video config list");
			}

			title = configBackCompat.title;
			author = configBackCompat.author;
			loop = configBackCompat.loop;
			offset = configBackCompat.offset;
			videoFile = configBackCompat.videoPath;

			//MVP duration implementation for reference:
			/*
			 * duration = duration.Hours > 0
                    ? $"{duration.Hours}:{duration.Minutes}:{duration.Seconds}"
                    : $"{duration.Minutes}:{duration.Seconds}";
             * TimeSpan.Parse assumes HH:MM instead of MM:SS if only one colon is present, so divide result by 60
			 */
			configBackCompat.duration ??= "0:00";
			duration = (int) TimeSpan.Parse(configBackCompat.duration).TotalSeconds;
			var colons = Regex.Matches(configBackCompat.duration, ":").Count;
			if (colons == 1)
			{
				duration /= 60;
			}

			var match = _regexParseID.Match(configBackCompat.URL ?? "");
			if (!match.Success)
			{
				throw new ArgumentException("Video config is missing the video URL");
			}
			videoID = match.Groups[1].Value;
		}

		public VideoConfig(DownloadController.YTResult searchResult, IPreviewBeatmapLevel level)
		{
			videoID = searchResult.ID;
			title = searchResult.Title;
			author = searchResult.Author;
			duration = searchResult.Duration;

			Level = level;
		}

		public new string ToString()
		{
			return $"[{videoID}] {title} by {author} ({duration})";
		}

		public float GetOffsetInSec()
		{
			return offset / 1000f;
		}

		public DownloadState UpdateDownloadState()
		{
			return (DownloadState = ((IsLocal && File.Exists(VideoPath)) ? DownloadState.Downloaded : DownloadState.NotDownloaded));
		}

		[JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
		public class EnvironmentModification
		{
			[JsonRequired] public string name = null!;

			public bool? active;
			public SerializableVector3? position;
			public SerializableVector3? rotation;
			public SerializableVector3? scale;
		}
	}
}