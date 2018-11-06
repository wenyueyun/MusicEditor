//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;

namespace SonicBloom.Koreo.Players
{
	/// <summary>
	/// This audio player is EXPERIMENTAL.  This audio player guarantees that all configured
	/// audio layers play back in a sample-synchronized fashion.  Currently this comes at a
	/// tradeoff in memory as all layers must be fully loaded in order to play back.
	/// </summary>
	// TODO: Convert the AudioBus *AND* this class into ones that support a queue of files to playback from.  This would make transitions
	//  far easier to handle.
	[RequireComponent(typeof(AudioSource))]
	[AddComponentMenu("Koreographer/Music Players/Experimental/Sample Sync Music Player")]
	public class SampleSyncMusicPlayer : MonoBehaviour, IKoreographedPlayer
	{
		#region Fields

		public delegate void MusicEndedHandler(AudioGroup group);

		/// <summary>
		/// Occurs when playback ends.
		/// </summary>
		public event MusicEndedHandler MusicEnded;

		[SerializeField]
		[Tooltip("The music/audio to play.")]
		AudioGroup playbackMusic = null;

		[SerializeField]
		[Tooltip("The number of channels in the configured audio.  This must match the configuration of all AudioClips across all layers!")]
		int musicChannels = 2;

		[SerializeField]
		[Tooltip("The sample rate of the configured audio.  This must match the configuration of all AudioClips across all layers!")]
		int musicFrequency = 44100;
		
		/// <summary>
		/// The AudioBus component through which this audio player should play samples.
		/// </summary>
		public AudioBus bus = null;

		[SerializeField]
		[Tooltip("[Optional] Specify a target Koreographer component to use for Koreography Event reporting and Music Time API support.  If no Koreographer is specified, the default global Koreographer component reference will be used.")]
		Koreographer targetKoreographer;

		AudioGroup curMusic = null;
		AudioGroup transMusic = null;

		int lastBusTime = -1;
		int curMusicTime = -1;
		int transMusicTime = 0;

		// This is necessary to store transition info in case we're told to do something like:
		//  SampleSyncMusicPlayer.PlayMusic();
		//  SampleSyncMusicPlayer.ScheduleMusic();
		// right in a row.  The underlying AudioBus needs time to move the "PlayMusic" contents out of its own
		// internal "next" audio reference.  So we store and keep trying until it goes through.  See: Update().
		struct TransitionInfo
		{
			public bool bValid;
			public AudioGroup group;
			public int curMusicTransLoc;
			public int startSampleOffset;
			public int lengthInSamples;
			public bool bReplaceIfExists;
		}
		TransitionInfo transInfo;

		AudioSource audioCom;

		#endregion
		#region Properties

		public bool IsPlaying
		{
			get
			{
				bool bPlaying = false;

				if (curMusic != null)
				{
					bPlaying = bus.IsAudioPlaying(curMusic);
				}
				else if (transMusic != null)
				{
					bPlaying = bus.IsAudioPlaying(transMusic);
				}

				return bPlaying;
			}
		}

		#endregion
		#region Methods

		void Awake()
		{
			audioCom = GetComponent<AudioSource>();

			// Fall back on the global Koreographer instance.
			if (targetKoreographer == null)
			{
				targetKoreographer = Koreographer.Instance;
			}

			targetKoreographer.musicPlaybackController = this;
			bus.AudioEnded += OnAudioEnded;
		}

		void Start()
		{
//			bus.Init(audioCom, playbackMusic.Channels, playbackMusic.Frequency);		// CAN'T DO THIS!  Need to Init the AudioGroup before the Properties will work.
			bus.Init(audioCom, musicChannels, musicFrequency);

			if (playbackMusic != null && !playbackMusic.IsEmpty())
			{
				PlayMusic(playbackMusic);
			}
		}

		void Update()
		{
			//  Use the times to update the Koreographer.  Be sure to also notify it of looping/audio transitions.
			if (curMusic != null && bus.IsAudioPlaying(curMusic) && !bus.IsPaused())
			{
				int prevMusicTime = curMusicTime;
				int curBusTime = bus.GetSampleTimeOfAudio(curMusic);

				// Set curMusicTime Do we need to estimate?  
				if (curBusTime == lastBusTime)
				{
					// We're playing but the Audio Bus didn't update the time.  Interpolate based on
					//  song time, system time, and playback speed.
					curMusicTime += (int)((float)curMusic.Frequency * GetPitch(string.Empty) * Time.unscaledDeltaTime);

					// Don't go beyond the edge of the group.
					curMusicTime = Mathf.Min(curMusicTime, curMusic.TotalSampleTime);
				}
				else
				{
					// New bus time!  Use it to update both current time and the bus time.
					curMusicTime = curBusTime;
					lastBusTime = curBusTime;
				}

				DeltaSlice deltaSlice = new DeltaSlice();
				deltaSlice.deltaLength = Time.unscaledDeltaTime;
				
				// Add one to startTime because the "prevMusicTime" sample was already checked in the previous update!
				PerformChoreographyForTimeSlice(curMusic, prevMusicTime + 1, curMusicTime, deltaSlice);
			}

			// Check to see if we need to try to schedule a transition.
			if (transInfo.bValid)
			{
				transInfo.bValid = false;
				ScheduleNextMusic(transInfo.group, transInfo.curMusicTransLoc, transInfo.startSampleOffset, transInfo.lengthInSamples, transInfo.bReplaceIfExists);

				// If scheduling failed, the transInfo should be regenerated from within ScheduleNextMusic(), including the bValid flag being set to true.
			}
		}

		#endregion
		#region Koreography Control

		void PerformChoreographyForTimeSlice(AudioGroup group, int startTime, int endTime, DeltaSlice deltaSlice)
		{
			for (int i = 0; i < curMusic.NumLayers; ++i)
			{
				AudioClip clip = curMusic.GetClipAtLayer(i);
			
				targetKoreographer.ProcessKoreography(clip.name, startTime, endTime, deltaSlice);
			}
		}

		#endregion
		#region Playback Control

		/// <summary>
		/// Plays the music in <paramref name="group"/> starting at sample time
		/// <paramref name="startSampleOffset"/> for <paramref name="lengthInSamples"/> time.
		/// If <paramref name="bReplaceIfExists"/> is <c>true</c>, the group will interrupt
		/// current audio playback as well as any scheduled audio.
		/// </summary>
		/// <param name="group">The <c>AudioGroup</c> to play.</param>
		/// <param name="startSampleOffset">The time in samples at which to begin playback
		/// of <paramref name="group"/>.</param>
		/// <param name="lengthInSamples">The amount of audio in samples to play.</param>
		/// <param name="bReplaceIfExists">If set to <c>true</c>, this operation will interrupt
		/// playing or scheduled audio; if false it will fail with a warning in the case that
		/// audio is already playing.</param>
		public void PlayMusic(AudioGroup group, int startSampleOffset = 0, int lengthInSamples = 0, bool bReplaceIfExists = false)
		{
			// TODO: Warn if channels/frequency not matching!
			// TODO: Validate AudioGroup (!group.IsEmpty())!

			if (!group.IsReady())
			{
				group.InitLayerData();
			}

			if (curMusic != null)
			{
				transMusic = group;
				transMusicTime = startSampleOffset;
			}
			else
			{
				curMusic = group;
				curMusicTime = startSampleOffset - 1;	// -1 to get the initial sample.
			}

			group.RegisterKoreography();

			if (!bus.PlayAudio(group, startSampleOffset, lengthInSamples, bReplaceIfExists))
			{
				Debug.LogWarning("PlayMusic() failed with group: " + group + ", likely something already in the AudioBus?");
			}
		}

		/// <summary>
		/// Schedules <paramref name="group"/> for playback, beginning at<paramref name="startSampleOffset"/>
		/// and set to continue for <paramref name="lengthInSamples"/>.  The <paramref name="curMusicTransLoc"/>
		/// parameter allows you to schedule where in the playback of any currently
		/// playing audio the transition should occur in sample time.  If the playhead
		/// is beyond the specified location, the scheduling will fail.  If <paramref name="bReplaceIfExists"/>
		/// is <c>true</c>, the scheduling will override any previously scheduled audio.
		/// </summary>
		/// <param name="group">The <c>AudioGroup</c> to schedule.</param>
		/// <param name="curMusicTransLoc">The sample location in the currently playing audio
		/// at which to transition.</param>
		/// <param name="startSampleOffset">The sample position in <paramref name="group"/> at which
		/// to begin playing audio.</param>
		/// <param name="lengthInSamples">The amount of <paramref name="group"/> to play back in
		/// samples.</param>
		/// <param name="bReplaceIfExists">If set to <c>true</c>, this operation will interrupt
		/// playing or scheduled audio; if false it will fail with a warning in the case that
		/// audio is already playing.</param>
		public void ScheduleNextMusic(AudioGroup group, int curMusicTransLoc = 0, int startSampleOffset = 0, int lengthInSamples = 0, bool bReplaceIfExists = false)
		{
			// TODO: Warn if channels/frequency not matching!
			// TODO: Validate AudioGroup (!group.IsEmpty())!

			if (!group.IsReady())
			{
				group.InitLayerData();		// Delay this?
			}

			if (curMusic == null)
			{
				// Just play now.
				curMusic = group;
				curMusicTime = startSampleOffset - 1;	// -1 to get the initial sample.

				bus.PlayAudio(group, startSampleOffset, lengthInSamples, bReplaceIfExists);
			}
			else if (transMusic == null || bReplaceIfExists)
			{
				if (bus.IsNextSongScheduled())
				{
					// Next song is already in, meaning we're likely waiting for curSong to transition in.  Save until buffers clear enough
					//  to allow scheduling.
					if (!transInfo.bValid || bReplaceIfExists)
					{
						// Override or just make sure there's nothing there.
						transMusic = null;
						transMusicTime = 0;

						// Set up info for another try later!
						transInfo.bValid = true;
						transInfo.group = group;
						transInfo.curMusicTransLoc = curMusicTransLoc;
						transInfo.startSampleOffset = startSampleOffset;
						transInfo.lengthInSamples = lengthInSamples;
						transInfo.bReplaceIfExists = bReplaceIfExists;
					}
					else
					{
						Debug.LogWarning("SampleSyncMusicPlayer::ScheduleNextMusic() - Transition music already scheduled!");
					}
				}
				else
				{
					// Set up the transition.
					transMusic = group;
					transMusicTime = startSampleOffset;

					// Koreography registration occurs later.  That way we don't double-up or trigger unwanted samples until later.
					bus.PlayAudioScheduled(group, curMusicTransLoc, startSampleOffset, lengthInSamples, bReplaceIfExists);
				}
			}
			// else - don't do anything - we already have music scheduled and we were told NOT to replace it.
		}

		/// <summary>
		/// Pause audio playback.
		/// </summary>
		public void Pause()
		{
			bus.Pause();
		}

		/// <summary>
		/// Unpauses audio playback.
		/// </summary>
		public void Continue()
		{
			bus.Continue();
		}

		/// <summary>
		/// Stops audio playback.
		/// </summary>
		public void Stop()
		{
			// Trigger the callback.  This will trigger OnAudioEnded() which will
			//  cause all the Koreographer and AudioGroup cleanup.
			bus.Stop(true);
		}

		#endregion
		#region AudioBus Callbacks

		void OnAudioEnded(AudioGroup group, int sampleTime, DeltaSlice deltaSlice)
		{
			if (curMusic == group)
			{
				if (sampleTime != curMusicTime)
				{
					PerformChoreographyForTimeSlice(curMusic, curMusicTime + 1, sampleTime, deltaSlice);
				}

				// Save some time if we're simply replaying the previous music.
				if (curMusic != transMusic)
				{
					group.UnregisterKoreography();	// Clean out Koreography linkages.
					group.ClearLayerData();			// Free up some space.
				}

				// Make sure we've loaded the new Koreography.
				if (transMusic != null && !transMusic.IsKoreographyRegistered())
				{
					transMusic.RegisterKoreography();
				}

				curMusic = transMusic;
				transMusic = null;

				curMusicTime = transMusicTime - 1;	// -1 to get the initial sample.
				transMusicTime = 0;

				// Trigger the callback!
				if (MusicEnded != null)
				{
					MusicEnded(group);
				}
			}
			else
			{
				Debug.LogWarning("Unexpected music has completed playback.");
			}
		}

		#endregion
		#region IKoreographedPlayer Methods

		/// <summary>
		/// Gets the current sample position of the audio with name <paramref name="clipName"/>.
		/// </summary>
		/// <returns>The current sample position of the audio with name <paramref name="clipName"/>.</returns>
		/// <param name="clipName">The name of the <c>AudioClip</c> to check.</param>
		public int GetSampleTimeForClip(string clipName)
		{
			int sampleTime = 0;
			if (curMusic != null && curMusic.ContainsClip(clipName))
			{
				sampleTime = Mathf.Max(0, curMusicTime);	// Initialized to -1 for startup.  Handle this, particularly for transitioning.
			}
			else if (transMusic != null && transMusic.ContainsClip(clipName))
			{
				sampleTime = transMusicTime;
			}
			return sampleTime;
		}

		/// <summary>
		/// Gets the total sample time of the audio with the name <paramref name="clipName"/>.
		/// </summary>
		/// <returns>The total sample time of the audio with the name <paramref name="clipName"/>.</returns>
		/// <param name="clipName">The name of the <c>AudioClip</c> to check.</param>
		public int GetTotalSampleTimeForClip(string clipName)
		{
			int totalSamples = 0;
			if (curMusic != null && curMusic.ContainsClip(clipName))
			{
				totalSamples = curMusic.TotalSampleTime;
			}
			else if (transMusic != null && transMusic.ContainsClip(clipName))
			{
				totalSamples = transMusic.TotalSampleTime;
			}
			return totalSamples;
		}

		/// <summary>
		/// Determines whether audio with name <paramref name="clipName"/> is playing.
		/// </summary>
		/// <returns><c>true</c>, if audio with name <paramref name="clipName"/> is
		/// playing,<c>false</c> otherwise.</returns>
		/// <param name="clipName">The name of the <c>AudioClip</c> to check.</param>
		public bool GetIsPlaying(string clipName)
		{
			bool bPlaying = false;
			if (curMusic != null && curMusic.ContainsClip(clipName))
			{
				bPlaying = bus.IsAudioPlaying(curMusic);
			}
			else if (transMusic != null && transMusic.ContainsClip(clipName))
			{
				bPlaying = bus.IsAudioPlaying(transMusic);
			}
			return bPlaying;
		}

		/// <summary>
		/// Gets the pitch of the audio with name <paramref name="clipName"/>.
		/// 
		/// In this case, <paramref name="clipName"/> is ignored as pitch is a
		/// global setting.
		/// </summary>
		/// <returns>The pitch of the audio.</returns>
		public float GetPitch(string clipName)
		{
			float pitch = 1f;
			if (bus != null)
			{
				pitch = bus.Pitch;
			}
			return pitch;
		}

		/// <summary>
		/// Gets the name of the current <c>AudioClip</c>.
		/// </summary>
		/// <returns>The name of the 'base' <c>AudioClip</c> of the currently playing
		/// <c>AudioGroup</c>, if any.</returns>
		public string GetCurrentClipName()
		{
			string clipName = string.Empty;
			if (curMusic != null)
			{
				clipName = curMusic.GetBaseClipName();
			}
			else if (transMusic != null)
			{
				clipName = transMusic.GetBaseClipName();
			}
			return clipName;
		}

		#endregion
	}
}
