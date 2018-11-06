//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;
using System.Collections.Generic;

namespace SonicBloom.Koreo.Players
{
	/// <summary>
	/// <para>The <see cref="SonicBloom.Koreo.Players.MultiMusicPlayer"/> will attempt to play multiple synchronized layers
	/// of audio.  These layers can be configured using raw <c>AudioClip</c> references
	/// or with Koreography references.  The interface disallows setting both for
	/// a single layer.</para>
	/// 
	/// <para>Under certain conditions, the layers may not all start at the same time.
	/// This is usually due to heavy CPU load.  Use the SyncPlayDelay feature to
	/// schedule the playback of audio in a synchronized fashion.</para>
	/// 
	/// <para>Internally, each layer requires an <c>AudioSource</c> component for playback of
	/// the <c>AudioClip</c>.  It is possible to specify a specific <c>AudioSource</c> component
	/// for a layer to use.  If no <c>AudioSource</c> is specified, the system will add
	/// one to use automatically.</para>
	/// </summary>
	[AddComponentMenu("Koreographer/Music Players/Multi Music Player")]
	public class MultiMusicPlayer : MonoBehaviour, IKoreographedPlayer
	{
		#region Fields

		[SerializeField]
		[Tooltip("If synchronization fails, increase this value (in seconds) to ensure synchronization across layers.")]
		double syncPlayDelay = 0f;

		[SerializeField]
		[Tooltip("Initial pitch applied to all layers.")]
		float pitch = 1f;

		[SerializeField]
		[Tooltip("Whether or not the music player should start up looping.")]
		bool loop = false;

		[SerializeField]
		[Tooltip("When Music Layers are specified, this determines whether to play immediately on Awake() or not.")]
		bool autoPlayOnAwake = true;

		[SerializeField]
		[Tooltip("Music Layers.  Koreographed layers should come first.")]
		List<MusicLayer> musicLayers = new List<MusicLayer>();
		
		[SerializeField]
		[Tooltip("[Optional] Specify a target Koreographer component to use for Koreography Event reporting and Music Time API support.  If no Koreographer is specified, the default global Koreographer component reference will be used.")]
		Koreographer targetKoreographer;

		bool bWaitingToPlay = false;

		#endregion
		#region Properties

		/// <summary>
		/// Gets or sets the pitch.
		/// </summary>
		/// <value>The pitch.</value>
		public float Pitch
		{
			get
			{
				return pitch;
			}
			set
			{
				pitch = value;

				for (int i = 0; i < musicLayers.Count; ++i)
				{
					musicLayers[i].AudioSourceCom.pitch = pitch;
				}
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether the audio should loop.
		/// </summary>
		/// <value><c>true</c> if the audio should loop; otherwise, <c>false</c>.</value>
		public bool Loop
		{
			get
			{
				return loop;
			}
			set
			{
				loop = value;

				for (int i = 0; i < musicLayers.Count; ++i)
				{
					musicLayers[i].AudioSourceCom.loop = loop;
				}
			}
		}

		/// <summary>
		/// Sets the volume to all AudioSources at once.
		/// </summary>
		/// <value>The volume [0,1].</value>
		public float Volume
		{
			set
			{
				for (int i = 0; i < musicLayers.Count; ++i)
				{
					// The volume setter should handle clamping.
					musicLayers[i].AudioSourceCom.volume = value;
				}
			}
		}

		/// <summary>
		/// Gets a value indicating whether the audio is playing or not (paused/stopped).
		/// </summary>
		/// <value><c>true</c> if the audio is playing; otherwise, <c>false</c>.</value>
		public bool IsPlaying
		{
			get
			{
				bool bPaused = false;

				if (musicLayers.Count > 0)
				{
					bPaused = musicLayers[0].AudioSourceCom.isPlaying;
				}

				return bPaused;
			}
		}

		#endregion
		#region Methods

		void Awake()
		{
			// Fall back on the global Koreographer instance.
			if (targetKoreographer == null)
			{
				targetKoreographer = Koreographer.Instance;
			}

			// Set ourselves up as the Music Player.
			targetKoreographer.musicPlaybackController = this;

			InitializeLayers();

			if (musicLayers.Count > 0)
			{
				LoadKoreographyAndStart(0, autoPlayOnAwake);
			}
		}

		void Update()
		{
			if (bWaitingToPlay && IsReadyForPlayback())
			{
				PlayInternal();
			}

			for (int i = 0; i < musicLayers.Count; ++i)
			{
				musicLayers[i].Visor.Update();
			}
		}

		void OnDestroy()
		{
			if (targetKoreographer != null)
			{
				for (int i = 0; i < musicLayers.Count; ++i)
				{
					MusicLayer layer = musicLayers[i];

					if (layer.KoreoData != null)
					{
						targetKoreographer.UnloadKoreography(layer.KoreoData);
					}
				}
			}

			musicLayers.Clear();
		}

		#endregion
		#region Internal Maintenance

		void InitializeLayers()
		{
			// Remove empty layers.
			musicLayers.RemoveAll(x => x.Clip == null);

			if (musicLayers.Count > 0)
			{
				// Warn when AudioClips aren't all of identical length.
				int sampleCount = musicLayers[0].Clip.samples;
				for (int i = 1; i < musicLayers.Count; ++i)
				{
					AudioClip clip = musicLayers[i].Clip;
				
					if (clip.samples % sampleCount != 0 &&
						sampleCount % clip.samples != 0)
					{
						Debug.LogWarning("Music layer at index " + i + " using AudioClip '" + clip + "' has unexpected sample length!\n" +
										 "Expected " + sampleCount + ".\n" +
										 "Found " + clip.samples + ".\n" +
										 "This will cause synchronization problems if/when the audio loops!");
					}
				}

				// Initialize layers, providing AudioSources where needed and AudioVisors.
				for (int i = 0; i < musicLayers.Count; ++i)
				{
					MusicLayer layer = musicLayers[i];

					AudioSource audioCom = null;
					if (layer.AudioSourceCom == null)
					{
						audioCom = gameObject.AddComponent<AudioSource>();
					}
					
					// We might be passing nulls.  That's okay, though, as they're the default and expected.
					layer.Init(audioCom, targetKoreographer);
				
					// Initialize with expected settings.
					audioCom = layer.AudioSourceCom;
					audioCom.clip = layer.Clip;
					audioCom.pitch = pitch;
					audioCom.loop = loop;
					audioCom.playOnAwake = false;

#if !(UNITY_4_5 || UNITY_4_6 || UNITY_4_7)
					if (!layer.Clip.preloadAudioData && // Unnecessary?
						layer.Clip.loadState != AudioDataLoadState.Loading &&
						layer.Clip.loadState != AudioDataLoadState.Loaded)
					{
						layer.Clip.LoadAudioData();
					}
#endif
				}
			}
		}

		void LoadKoreographyAndStart(int startSampleTime = 0, bool autoPlay = true)
		{
			if (musicLayers.Count > 0)
			{
				for (int i = 0; i < musicLayers.Count; ++i)
				{
					MusicLayer layer = musicLayers[i];

					if (layer.KoreoData != null)
					{
						targetKoreographer.LoadKoreography(layer.KoreoData);
					}
				}
			
				SeekToSample(startSampleTime);
			
				if (autoPlay)
				{
					Play();
				}
			}
		}

		void PlayInternal()
		{
			double timeToStart = AudioSettings.dspTime + (double)syncPlayDelay;

			for (int i = 0; i < musicLayers.Count; ++i)
			{
				MusicLayer layer = musicLayers[i];
				layer.AudioSourceCom.PlayScheduled(timeToStart);
				layer.Visor.ScheduledPlayTime = timeToStart;
			}

			bWaitingToPlay = false;
		}
	
		bool IsReadyForPlayback()
		{
			bool isReady = true;
		
			for (int i = 0; i < musicLayers.Count; ++i)
			{
#if (UNITY_4_5 || UNITY_4_6 || UNITY_4_7)
				if (!musicLayers[i].Clip.isReadyToPlay)
#else
				if (musicLayers[i].Clip.loadState != AudioDataLoadState.Loaded)
#endif
				{
					isReady = false;
					break;
				}
			}
		
			return isReady;
		}

		int GetIndexOfLayerWithClipName(string clipName)
		{
			int idx = -1;

			for (int i = 0; i < musicLayers.Count; ++i)
			{
				if (musicLayers[i].ClipName == clipName)
				{
					idx = i;
					break;
				}
			}

			return idx;
		}

		#endregion
		#region Playback Control

		/// <summary>
		/// <para>Load a new song, as described by <paramref name="layers"/>.</para>
		/// 
		/// <para>This will stop the currently playing song.</para>
		/// </summary>
		/// <param name="layers">A <c>List</c> of <paramref name="MusicLayer"/>s that comprise the song.</param>
		/// <param name="startSampleTime">The time within the layers at which playback should begin.</param>
		/// <param name="autoPlay">If set to <c>true</c> auto play.</param>
		public void LoadSong(List<MusicLayer> layers, int startSampleTime = 0, bool autoPlay = true)
		{
			// Stop everything currently playing.
			Stop();

			// Unload all the Koreography from the Koreographer.
			for (int i = 0; i < musicLayers.Count; ++i)
			{
				MusicLayer layer = musicLayers[i];

				if (layer.KoreoData != null)
				{
					targetKoreographer.UnloadKoreography(layer.KoreoData);
				}
			}

			// Store the new layer structure.
			musicLayers.Clear();
			musicLayers.AddRange(layers);

			// Initialize internals.
			InitializeLayers();

			// Load the Koreography and begin playback (if necessary).
			LoadKoreographyAndStart(startSampleTime, autoPlay);
		}

		/// <summary>
		/// Plays the audio.
		/// </summary>
		public void Play()
		{
			if (IsReadyForPlayback())
			{
				PlayInternal();
			}
			else
			{
				bWaitingToPlay = true;
			}
		}

		/// <summary>
		/// Stops the audio.
		/// </summary>
		public void Stop()
		{
			// Should we use SetScheduledEndTime()?  Or does Stop() clear the scheduled playback?
			for (int i = 0; i < musicLayers.Count; ++i)
			{
				musicLayers[i].AudioSourceCom.Stop();
			}
		}

		/// <summary>
		/// Pauses the audio.
		/// </summary>
		public void Pause()
		{
			for (int i = 0; i < musicLayers.Count; ++i)
			{
				musicLayers[i].AudioSourceCom.Pause();
			}
		}

		/// <summary>
		/// Seeks the audio to the target sample location.
		/// </summary>
		/// <param name="targetSample">The location to seek the audio to.</param>
		public void SeekToSample(int targetSample)
		{
			for (int i = 0; i < musicLayers.Count; ++i)
			{
				musicLayers[i].AudioSourceCom.timeSamples = targetSample;
				musicLayers[i].Visor.ResyncTimings();
			}
		}

		#endregion
		#region Player Control

		/// <summary>
		/// Sets the volume for the given layer.
		/// </summary>
		/// <param name="layerNum">The number of the layer to set the volume for.</param>
		/// <param name="volume">The value to set the volume in the range of <c>[0,1]</c>.</param>
		public void SetVolumeForLayer(int layerNum, float volume)
		{
			// We "Guarantee" that there will be at least as many Audio Sources
			//  as there are Play Clips.
			if (layerNum >= 0 && layerNum < musicLayers.Count)
			{
				musicLayers[layerNum].AudioSourceCom.volume = volume;
			}
		}

		/// <summary>
		/// Sets the volume for the given layer.
		/// </summary>
		/// <param name="layerName">The name of the layer to set the volume for.</param>
		/// <param name="volume">The value to set the volume in the range of [0,1].</param>
		public void SetVolumeForLayer(string layerName, float volume)
		{
			// We "Guarantee" that there will be at least as many Audio Sources
			//  as there are Play Clips.
			SetVolumeForLayer(musicLayers.FindIndex(x => x.Name == layerName), volume);
		}

		#endregion
		#region IKoreographedPlayer Methods

		/// <summary>
		/// Gets the current sample position of the <c>AudioClip</c> with name <paramref name="clipName"/>.
		/// </summary>
		/// <returns>The current sample position of the <c>AudioClip</c> with name 
		/// <paramref name="clip"/>.</returns>
		/// <param name="clipName">The name of the <c>AudioClip</c> to check.</param>
		public int GetSampleTimeForClip(string clipName)
		{
			int sampleTime = 0;

			int idx = GetIndexOfLayerWithClipName(clipName);
			if (idx >= 0)
			{
				sampleTime = musicLayers[idx].Visor.GetCurrentTimeInSamples();
			}
			return sampleTime;
		}

		/// <summary>
		/// Gets the total sample time for <c>AudioClip</c> with name <paramref name="clipName"/>.  
		/// This total time is not necessarily the same at runtime as it was at edit time.
		/// </summary>
		/// <returns>The total sample time for <c>AudioClip</c> with name <paramref name="clipName"/>.</returns>
		/// <param name="clipName">The name of the <c>AudioClip</c> to check.</param>
		public int GetTotalSampleTimeForClip(string clipName)
		{
			int totalSamples = 0;

			int idx = GetIndexOfLayerWithClipName(clipName);
			if (idx >= 0)
			{
				totalSamples = musicLayers[idx].Clip.samples;
			}

			return totalSamples;
		}

		/// <summary>
		/// Determines whether the <c>AudioClip</c> with name <paramref name="clipName"/> is playing.
		/// </summary>
		/// <returns><c>true</c>, if <c>AudioClip</c> with name <paramref name="clipName"/> is
		/// playing,<c>false</c> otherwise.</returns>
		/// <param name="clipName">The name of the <c>AudioClip</c> to check.</param>
		public bool GetIsPlaying(string clipName)
		{
			int idx = GetIndexOfLayerWithClipName(clipName);

			MusicLayer layer = musicLayers[idx];

			return (idx >= 0) && layer.AudioSourceCom.isPlaying && (layer.Visor.ScheduledPlayTime <= AudioSettings.dspTime);
		}

		/// <summary>
		/// Gets the pitch.  The <paramref name="clipName"/> parameter is ignored.
		/// </summary>
		/// <returns>The pitch of the audio.</returns>
		public float GetPitch(string clipName)
		{
			return Pitch;
		}

		/// <summary>
		/// Gets the name of the current <c>AudioClip</c>.
		/// </summary>
		/// <returns>The name of the <c>AudioClip</c> in the currently playing 'base' 
		/// <c>MusicLayer</c>, if any.</returns>
		public string GetCurrentClipName()
		{
			return musicLayers[0].ClipName;
		}
		
		#endregion
	}

	/// <summary>
	/// A single layer of the music.  Layers are played back simultaneously by
	/// the MultiMusicPlayer.
	/// </summary>
	[System.Serializable]
	public class MusicLayer
	{
		#region Fields
		
		[SerializeField]
		[Tooltip("Adds a name to the layer.  This is used to rename the Music Layer entry in the list in the Inspector.")]
		string name = string.Empty;
		
		[SerializeField]
		[Tooltip("Koreography to load for this layer.  The AudioClip in the Koreography will be used for playback.")]
		Koreography koreography = null;
		
		[SerializeField]
		[Tooltip("Don’t load Koreography - simply play the AudioClip.")]
		AudioClip audioClip = null;
		
		[SerializeField]
		[Tooltip("A reference to an AudioSource component that this Music Layer should use for playback.  If not specified the system will autogenerate one.")]
		AudioSource audioSource = null;
		
		AudioVisor audioVisor = null;

		string clipName = null;
		
		#endregion
		#region Properties

		/// <summary>
		/// Gets the Koreography.
		/// </summary>
		/// <value>The Koreography.</value>
		public Koreography KoreoData
		{
			get
			{
				return koreography;
			}
		}

		/// <summary>
		/// Gets the <c>AudioClip</c> for this layer.
		/// </summary>
		/// <value>The <c>AudioClip</c>.</value>
		public AudioClip Clip
		{
			get
			{
				return (koreography == null) ? audioClip : koreography.SourceClip;
			}
		}

		/// <summary>
		/// Gets the name of the <c>AudioClip</c> used by this layer.
		/// </summary>
		/// <value>The name of the <c>AudioClip</c> used by this layer.</value>
		public string ClipName
		{
			get
			{
				if (string.IsNullOrEmpty(clipName))
				{
					clipName = Clip.name;
				}

				return clipName;
			}
		}

		/// <summary>
		/// Gets the <c>AudioSource</c> component this <c>MusicLayer</c> uses
		/// for playback.
		/// </summary>
		/// <value>The configured <c>AudioSource</c> component.</value>
		public AudioSource AudioSourceCom
		{
			get
			{
				return audioSource;
			}
		}

		/// <summary>
		/// Gets the <c>AudioVisor</c> linked to this <c>MusicLayer</c>.
		/// </summary>
		/// <value>The linked <c>AudioVisor</c>.</value>
		public AudioVisor Visor
		{
			get
			{
				return audioVisor;
			}
		}

		/// <summary>
		/// The string name configured for this <c>MusicLayer</c>.
		/// </summary>
		/// <value>The string name.</value>
		public string Name
		{
			get
			{
				return name;
			}
		}
		
		#endregion
		#region Constructors
		
		private MusicLayer(){}		// Disallow default constructor.

		/// <summary>
		/// Initializes a new instance of the <see cref="SonicBloom.Koreo.Players.MusicLayer"/> class.
		/// </summary>
		/// <param name="koreo">The Koreography to use.</param>
		/// <param name="source">The <c>AudioSource</c> to use.</param>
		/// <param name="layerName">The name of the layer.</param>
		public MusicLayer(Koreography koreo, AudioSource source, string layerName = "")
		{
			koreography = koreo;
			audioClip = koreo.SourceClip;
			clipName = audioClip.name;
			audioSource = source;
			name = layerName;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SonicBloom.Koreo.Players.MusicLayer"/> class.
		/// </summary>
		/// <param name="clip">The <c>AudioClip</c> to use.</param>
		/// <param name="source">The <c>AudioSource</c> to use.</param>
		/// <param name="layerName">The name of the layer.</param>
		public MusicLayer(AudioClip clip, AudioSource source, string layerName = "")
		{
			audioClip = clip;
			clipName = audioClip.name;
			audioSource = source;
			name = layerName;
		}
		
		#endregion
		#region Setup and Teardown

		/// <summary>
		/// Initializes the <c>MusicPlayer</c>
		/// </summary>
		/// <param name="source">An optional <c>AudioSource</c> that can be used to override a previously
		/// configured <c>AudioSource</c>.</param>
		/// <param name="targetKoreographer">An optional <c>Koreographer</c> to use for time reporting.  
		/// If not specified, the global Koreographer instance will be used.</param>
		public void Init(AudioSource source = null, Koreographer targetKoreographer = null)
		{
			if (source != null)
			{
				audioSource = source;
			}
			
			audioVisor = new AudioVisor(audioSource, targetKoreographer);
		}
		
		#endregion
	}
}
