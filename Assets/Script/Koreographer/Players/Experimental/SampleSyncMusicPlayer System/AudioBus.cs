//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;
using System.Collections.Generic;

namespace SonicBloom.Koreo.Players
{
	/// <summary>
	/// The <see cref="AudioBus"/> manages the feeding of a specific buffer of audio
	/// to the Audio System.  Samples are fed into the audio buffer when the underlying
	/// audio system requests them.
	/// </summary>
	[AddComponentMenu("Koreographer/Music Players/Experimental/Sample Sync System/Audio Bus")]
	public class AudioBus : MonoBehaviour
	{
		// <--------- Callback stuffs --------->
		public delegate void AudioEndedHandler(AudioGroup group, int sampleTime, DeltaSlice deltaSlice);

		/// <summary>
		/// Occurs when Playback of an AudioGroup ends.
		/// </summary>
		public event AudioEndedHandler AudioEnded;

		// <--------- Source stuffs --------->
		AudioSource sourceCom = null;

		// <--------- Clip stuffs --------->
		const int BUS_SAMPLE_LENGTH = 44100;

		AudioClip busClip = null;
		int busChannels = 2;
		int busFrequency = 44100;
		float busLengthInSeconds = 1;	// Default (sample length / frequency)

		// <--------- Timing stuffs --------->
		int prevSourcePlayTime = 0;
		uint busPlaySampleTime = 0;
		uint busReadSampleTime = 0;		//TODO: Handle uint-wrapping!  This happens when busReadSampleTime goes over uint.maxValue.
		float timePlayStarted = 0f;

		// <--------- State-Sanity stuffs --------->
		bool bColdStart = true;
		bool bPaused = false;

		class AudioToBusAnchor
		{
			public AudioGroup audioGroup;

			public readonly int audioSampleLength;

			public readonly int audioSampleStartTime;
			public uint busSampleStartTime;

			public AudioToBusAnchor()
			{
				audioGroup = null;

				audioSampleLength = 0;

				audioSampleStartTime = 0;
				busSampleStartTime = 0;
			}

			public AudioToBusAnchor(AudioGroup _group, int _audioSampleStartTime, int _audioSampleLength)
			{
				audioGroup = _group;
				audioSampleStartTime = _audioSampleStartTime;
				audioSampleLength = _audioSampleLength;

				busSampleStartTime = 0;
			}

			public void ClearAnchor()
			{
				audioGroup = null;
			}

			public bool IsValid()
			{
				return (audioGroup != null);
			}

			public bool IsPlayingAtBusTime(uint busSampleTime)
			{
				return (busSampleTime >= busSampleStartTime && audioSampleLength >= (busSampleTime - busSampleStartTime));
			}

			public int SampleTimeAtBusTime(uint busSampleTime)
			{
				int sampleTime = (int)(busSampleTime - busSampleStartTime);
				sampleTime = Mathf.Clamp(sampleTime, audioSampleStartTime, audioSampleStartTime + audioSampleLength);

				return sampleTime;
			}
		}

		// <--------- Playback stuffs --------->
		//  Possibly handle this with lists?  One for "Read" and one for "Play"?
		readonly AudioToBusAnchor invalidSong = new AudioToBusAnchor();
		AudioToBusAnchor prevSong = new AudioToBusAnchor();
		AudioToBusAnchor curSong = new AudioToBusAnchor();
		AudioToBusAnchor nextSong = new AudioToBusAnchor();
		System.Object anchorLock = new System.Object();	// Obj. that is "locked" to make sure only one thread can touch nextSong.

		/// <summary>
		/// Gets or sets the pitch.
		/// </summary>
		/// <value>The pitch.</value>
		public float Pitch
		{
			get
			{
				float val = 1f;
				if (sourceCom != null)
				{
					val = sourceCom.pitch;
				}
				return val;
			}
			set
			{
				if (sourceCom != null)
				{
					sourceCom.pitch = value;
				}
				else
				{
					Debug.LogWarning("Attempting to set pitch on AudioBus that has no AudioSource!");
				}
			}
		}

		void Awake()
		{
			if (sourceCom == null)
			{
				enabled = false;
			}
		}
	
		void Update()
		{
			if (sourceCom != null)
			{
				int curSourceTime = sourceCom.timeSamples;
				int sourceTimeDiff = 0;

				// Simple read, or did we loop around the end.
				if (curSourceTime < prevSourcePlayTime)
				{
					sourceTimeDiff = curSourceTime + (busClip.samples - prevSourcePlayTime);
				}
				else
				{
					sourceTimeDiff = curSourceTime - prevSourcePlayTime;
				}

				// Adjust for extremely low framerates: this compensates for situations where the
				//  buffer loops while something like a load opperation blocks the Main thread for
				//  too long.
				float delta = Time.unscaledDeltaTime;

				// Play is special: it could have happened at Start() which doesn't properly report
				//  delta, especially for extremely large loading.
				if (timePlayStarted > 0f)
				{
					delta = Time.realtimeSinceStartup - timePlayStarted;
					timePlayStarted = 0f;
				}

				if (delta > busLengthInSeconds)
				{
					sourceTimeDiff += Mathf.FloorToInt(delta / busLengthInSeconds) * BUS_SAMPLE_LENGTH;
				}

				if (sourceTimeDiff > 0)
				{
					// Filled in if we transitioned; used to trigger callback after lock is released if so.
					AudioGroup prevGroup = null;
					int prevStartTime = 0;
					int prevTime = 0;
				
					lock (anchorLock)
					{
						uint lastBusPlaySampleTime = busPlaySampleTime;
						
						busPlaySampleTime += (uint)sourceTimeDiff;

						// Clear the prevSong information if necessary.
						if (prevSong.IsValid() &&
							!prevSong.IsPlayingAtBusTime(busPlaySampleTime) && // Make sure we're actually beyond the prevSong.
							(!curSong.IsValid() || // Silence.
							curSong.IsPlayingAtBusTime(busPlaySampleTime)))	// Next song is going.
						{
							prevGroup = prevSong.audioGroup;
							prevStartTime = prevSong.SampleTimeAtBusTime(lastBusPlaySampleTime);
							prevTime = prevSong.SampleTimeAtBusTime(busPlaySampleTime);

							prevSong.ClearAnchor();
						}
					}

					// Check for callback.
					if (prevGroup != null)
					{
						// NOTE: This math is untested!  Most likely has problems "prevStartTime" was added to the above to
						//  handle this case.  That may be the incorrect value.
						// TODO: When we decide to officially support this system, dig into this and verify that the
						//  DeltaSlice implementation works correctly.
						DeltaSlice deltaSlice = new DeltaSlice();
						deltaSlice.deltaLength = delta * ((float)(prevTime - prevStartTime) / (float)sourceTimeDiff);
						OnAudioEnded(prevGroup, prevTime, deltaSlice);
					}
				
					prevSourcePlayTime = curSourceTime;
				}
			}
		}

		/// <summary>
		/// Sets the internal <c>AudioSource</c> component with which to play audio.  Also initializes
		/// an internal <c>AudioClip</c> with the specified channel count and frequency.
		/// </summary>
		/// <param name="com">The <c>AudioSource</c> component that this <see cref="AudioBus"/> wraps.</param>
		/// <param name="numChannels">The number of channels in the audio that will be played through the bus.</param>
		/// <param name="frequency">The sample frequency of the audio that will be played through the bus.</param>
		public void Init(AudioSource com, int numChannels, int frequency)
		{
			sourceCom = com;
			busChannels = numChannels;
			busFrequency = frequency;
			busLengthInSeconds = (float)BUS_SAMPLE_LENGTH / (float)busFrequency;
		
			sourceCom.Stop();
			Object.DestroyImmediate(busClip);

#if (UNITY_4_5 || UNITY_4_6 || UNITY_4_7)
			busClip = AudioClip.Create("AUDIO_BUS", BUS_SAMPLE_LENGTH, busChannels, busFrequency, false, true, ReadBusAudioCallback, SetBusPositionCallback);
#else
			busClip = AudioClip.Create("AUDIO_BUS", BUS_SAMPLE_LENGTH, busChannels, busFrequency, true, ReadBusAudioCallback, SetBusPositionCallback);
#endif

			// Simply creating the AudioClip will cause both the ReadBusAudioCallback and SetBusPositionCallback to be called.
			//  Reset the bus values read/play positions after this initialization phase to get us back into order.
			busReadSampleTime = 0;
			busPlaySampleTime = 0;

			sourceCom.loop = true;
			sourceCom.clip = busClip;

			// Start us moving.
			Play();

			enabled = true;
		}

		void ReadBusAudioCallback(float[] data)
		{
			int sampleTimeToRead = data.Length / busChannels;

			//TODO: Clean this up!  Lots of maths are repeated; this can be condensed!

			// Doing a bunch of operations on the song anchors.  Lock them.
			lock (anchorLock)
			{
				if (!curSong.IsValid())
				{
					// Silence.  Possibly transitioning to actual song.
					if (!nextSong.IsValid())
					{
//						Debug.Log("Read: silence");
						System.Array.Clear(data, 0, data.Length);
					}
					else
					{
						// Transition
						if (nextSong.busSampleStartTime == 0)
						{
//							Debug.Log("Read: transition silence-song");
							// Set the current time!
							nextSong.busSampleStartTime = busReadSampleTime;

							// Read the audio data.
							nextSong.audioGroup.GetAudioData(nextSong.audioSampleStartTime, data, 0, data.Length);

							// Commit transition.
							curSong = nextSong;
							nextSong = invalidSong;
						}
						else if (nextSong.busSampleStartTime < busReadSampleTime + sampleTimeToRead)
						{
//							Debug.Log("Read: transition silence-song, timed");

							if (nextSong.busSampleStartTime > busReadSampleTime)
							{
								Debug.LogError("ERROR: We are somehow transitioning too late into the nextSong.  Handle this somehow!");
							}

							int dataSampleStride = (int)(nextSong.busSampleStartTime - busReadSampleTime) * busChannels;

							// Read in some silence.
							System.Array.Clear(data, 0, dataSampleStride);

							// Read the audio data.
							nextSong.audioGroup.GetAudioData(nextSong.audioSampleStartTime, data, dataSampleStride, data.Length - dataSampleStride);

							// Commit transition.
							curSong = nextSong;
							nextSong = invalidSong;
						}
						else
						{
//							Debug.Log("Read: transition but not away from silence");
							// Just read silence for now.
							System.Array.Clear(data, 0, data.Length);
						}
					}
				}
				else
				{
					// The current song time!
					int curSongReadPos = (curSong.audioSampleStartTime + (int)(busReadSampleTime - curSong.busSampleStartTime));

					// Options:
					//  Transition - nextSong is scheduled to go
					//  Transition - curSong is ending
					//  Continue - simply continue curSong.
					if (nextSong.busSampleStartTime > 0 && // A setting of 0 means "play this when the current song ends".
						nextSong.busSampleStartTime < (busReadSampleTime + sampleTimeToRead))
					{
//						Debug.Log("Read: transition song-song, timed");
						if (nextSong.busSampleStartTime > busReadSampleTime)
						{
							Debug.LogError("ERROR: We are somehow transitioning too late into the nextSong.  Handle this somehow!");
						}

						// Scheduled transition!
					
						// Read current song up to busSampleStartTime.
						int dataSampleStride = (int)(nextSong.busSampleStartTime - busReadSampleTime) * busChannels;

						curSong.audioGroup.GetAudioData(curSongReadPos, data, 0, dataSampleStride);

						// Start up next song!
						nextSong.audioGroup.GetAudioData(nextSong.audioSampleStartTime, data, dataSampleStride, data.Length - dataSampleStride);

						// Commit transition.
						prevSong = curSong;
						curSong = nextSong;
						nextSong = invalidSong;
					}
					else if (curSongReadPos + sampleTimeToRead > curSong.audioSampleLength)
					{
						// CurSong is ending!

						int numSamplesLeftToReadFromSong = (int)((curSong.busSampleStartTime + (uint)curSong.audioSampleLength) - busReadSampleTime);
						int dataSampleStride = numSamplesLeftToReadFromSong * busChannels;

						curSong.audioGroup.GetAudioData(curSongReadPos, data, 0, dataSampleStride);

						// Commit transition 1/3.
						prevSong = curSong;

						if (nextSong.IsValid())
						{
//							Debug.Log("Read: transition song-song, song1 ended");

							// Anchor this song to the bus!
							nextSong.busSampleStartTime = busReadSampleTime + (uint)numSamplesLeftToReadFromSong;

							// Start up next song!
							nextSong.audioGroup.GetAudioData(nextSong.audioSampleStartTime, data, dataSampleStride, data.Length - dataSampleStride);
						
							// Commit transition 2/3.
							curSong = nextSong;
						}
						else
						{
//							Debug.Log("Read: transition song-silence, song ended");
							// Silence.
							System.Array.Clear(data, dataSampleStride, data.Length - dataSampleStride);

							// Commit transition 2/3.
							curSong = invalidSong;
						}

						// Commit transition 3/3.
						nextSong = invalidSong;
					}
					else
					{
//						Debug.Log("Read: song");
						// Continue playing CurSong!
						curSong.audioGroup.GetAudioData(curSongReadPos, data, 0, data.Length);
					}
				}
			}

			busReadSampleTime += (uint)sampleTimeToRead;
		}

		void SetBusPositionCallback(int position)
		{
			// The Unity Audio system pre-caches some samples when the user calls "Play()" from a Stopped state.
			//  Reset the busReadSampleTime when this is called during the processing of the "Play()" call (on the main thread).
			if (bColdStart)
			{
				if (position != 0)
				{
					Debug.LogWarning("AudioBus is cold starting but was told to jump to a sample position other than 0.  WHAT?!");
				}

				busReadSampleTime = 0;
			}
		}

		void ScheduleNextSong(AudioGroup group, int startSampleOffset, int sampleLength, uint busStartTime)
		{
			lock (anchorLock)
			{
				// Adjust the sampleLength.
				sampleLength = (sampleLength > 0) ? sampleLength : group.TotalSampleTime - startSampleOffset;

				nextSong = new AudioToBusAnchor(group, startSampleOffset, sampleLength);
				nextSong.busSampleStartTime = busStartTime;
			}
		}

		/// <summary>
		/// Begin playback of <paramref name="group"/>.  If other audio is already scheduled to play, returns
		/// <c>false</c>.
		/// </summary>
		/// <returns><c>true</c>, if <paramref name="group"/> was played, <c>false</c> otherwise.</returns>
		/// <param name="group">The <see cref="AudioGroup"/> to play.</param>
		/// <param name="startSampleOffset">The number of samples to offset playback from.</param>
		/// <param name="sampleLength">The number of samples to play, starting at
		/// <paramref name="startSampleOffset"/>.</param>
		/// <param name="bReplaceIfExists">If set to <c>true</c>, this will override previously scheduled
		/// audio to playback.</param>
		public bool PlayAudio(AudioGroup group, int startSampleOffset = 0, int sampleLength = 0, bool bReplaceIfExists = false)
		{
			bool bQueued = false;
			lock (anchorLock)
			{
				// TODO: Validate Group!  Make sure that the length of the associated clip is greater than the buffer we have?  Also, that it's not null?
				//  If the length of the associated clip is LESS than the playback buffer, how do we handle the transition situation, etc.?  Right now it would
				//  simply go to silence.

				if (!nextSong.IsValid() ||
					bReplaceIfExists)
				{
					if (!sourceCom.isPlaying)
					{
						Play();
					}

					// Schedule the next song for "now".  Instead of busReadSampleTime, "0" should also work.
					ScheduleNextSong(group, startSampleOffset, sampleLength, busReadSampleTime);
					bQueued = true;
				}
			}
			return bQueued;
		}

		/// <summary>
		/// <para>Schedule's the provided Audio to be started at the sample location in the currently
		/// playing track indicated in the curSongSampleTransLoc parameter.</para>
		/// 
		/// <para>If no song is currently playing, the playback occurs immediately.</para>
		/// 
		/// <para>If the currently playing song has already played (or been read) beyond the transition
		/// point, this returns <c>false</c>.</para>
		/// </summary>
		/// <returns><c>true</c>, if audio was scheduled, <c>false</c> otherwise.</returns>
		/// <param name="group">The audio group to schedule.</param>
		/// <param name="curSongSampleTransLoc">The sample location in the currently playing song
		/// to transition at.  A value of <c>0</c> means "when the current song ends".</param>
		/// <param name="startSampleOffset">The start sample offset for the scheduled audio
		/// group.</param>
		/// <param name="sampleLength">The sample length of the audio to playback.  A value of
		/// <c>0</c> means "until the end".</param>
		/// <param name="bReplaceIfExists">If set to <c>true</c> any previously queued song will
		/// be overwritten.</param>
		public bool PlayAudioScheduled(AudioGroup group, int curSongSampleTransLoc = 0, int startSampleOffset = 0, int sampleLength = 0, bool bReplaceIfExists = false)
		{
			bool bQueued = false;
			lock (anchorLock)
			{
				if (!nextSong.IsValid() ||
					bReplaceIfExists)
				{
					if (!curSong.IsValid())
					{
						// Just play it.  Nothing already scheduled.
						bQueued = PlayAudio(group, startSampleOffset, sampleLength, bReplaceIfExists);
					}
					else if (curSongSampleTransLoc == 0)
					{
						// Play it after the current song completes.
						if (!sourceCom.isPlaying)
						{
							Play();
						}
					
						// Schedule it!
						ScheduleNextSong(group, startSampleOffset, sampleLength, 0);
						bQueued = true;
					}
					else if (curSongSampleTransLoc >= curSong.audioSampleStartTime + (int)(busReadSampleTime - curSong.busSampleStartTime))
					{
						// Play it at a specific location in the song.
						if (!sourceCom.isPlaying)
						{
							Play();
						}

						// Schedule it!
						uint busScheduledTime = curSong.busSampleStartTime + (uint)(curSongSampleTransLoc - curSong.audioSampleStartTime);
						ScheduleNextSong(group, startSampleOffset, sampleLength, busScheduledTime);
						bQueued = true;
					}
				}
			}
			return bQueued;
		}
	
		void Play()
		{
			if (bPaused)
			{
				Continue();
			}
			else if (!sourceCom.isPlaying)
			{
				lock (anchorLock)
				{
					bColdStart = true;	// Used to inform the set position callback that we're okay to restart.

					uint prevBusReadTime = busReadSampleTime;

					// Locking here *should* be okay because AudioSource.Play() triggers
					//  the Read Data Callback on the same thread.
					sourceCom.Play();

					// The bus read time has moved forward a bit from here.  Add that to the prevBusReadTime.
					//  During the Play() callback it is presumed that the audio position of the clip was reset to 0.
					//  This means that the read position is also the *amount* that the read head moved forward.
					//  Add the old location to the new one to get us back in sync.
					busReadSampleTime += prevBusReadTime;

					// Reset the other play/read times.
					prevSourcePlayTime = sourceCom.timeSamples;
					busPlaySampleTime = 0;

					// Stop the set position callback from resetting our bus location as we're now warmed up and running.
					bColdStart = false;
				}
			}

			timePlayStarted = Time.realtimeSinceStartup;
		}

		/// <summary>
		/// Resumes playback of a Paused <c>AudioBus</c>.
		/// </summary>
		public void Continue()
		{
			if (!bPaused)
			{
				Debug.LogWarning("AudioBus::Continue() called while AudioBus reports to NOT be paused.  Something weird going on?");
			}
			else if (sourceCom.isPlaying)
			{
				Debug.LogWarning("AudioBus::Continue() called when the bus is EXPECTED to be paused but its internal AudioSource is still playing.  What's going on?");
			}

			if (bPaused && !sourceCom.isPlaying)
			{
				lock (anchorLock)
				{
					// Unpause.
					sourceCom.Play();
					bPaused = false;
				}
			}
		}

		/// <summary>
		/// Pauses the <c>AudioBus</c>.
		/// </summary>
		public void Pause()
		{
			if (sourceCom.isPlaying)
			{
				lock (anchorLock)
				{
					sourceCom.Pause();
					bPaused = true;
				}
			}
		}

		/// <summary>
		/// Stops the <c>AudioBus</c>, optionally triggering the <see cref="AudioEnded"/> callback.
		/// </summary>
		/// <param name="bDoCallback">If set to <c>true</c>, triggers the <see cref="AudioEnded"/> callback.</param>
		public void Stop(bool bDoCallback = false)
		{
			AudioGroup stoppedGroup = null;
			int stoppedTime = 0;

			lock (anchorLock)
			{
				sourceCom.Stop();
				bPaused = false;
				bColdStart = true;

				if (bDoCallback)
				{
					// If there's a song to stop, store it.
					if (prevSong.IsValid())
					{
						stoppedGroup = prevSong.audioGroup;
						stoppedTime = prevSong.SampleTimeAtBusTime(busPlaySampleTime);
					}
					else if (curSong.IsValid())
					{
						stoppedGroup = curSong.audioGroup;
						stoppedTime = curSong.SampleTimeAtBusTime(busPlaySampleTime);
					}
				}

				// Clear the song Anchors.
				prevSong = invalidSong;
				curSong = invalidSong;
				nextSong = invalidSong;
			}

			if (stoppedGroup != null)
			{
				// For now, just throw in a default DeltaSlice.  The timing is a little rough
				//  here.
				// TODO: Actually attempt to implement this by using sample info if we get
				//  around to fully supporting it.
				DeltaSlice deltaSlice = new DeltaSlice();
				deltaSlice.deltaLength = Time.unscaledDeltaTime;
				
				OnAudioEnded(stoppedGroup, stoppedTime, deltaSlice);
			}
		}

		int GetSampleTimeOfAnchor(AudioToBusAnchor anchor)
		{
			int sampleTime = 0;
			lock (anchorLock)
			{
				if (busPlaySampleTime < anchor.busSampleStartTime)
				{
					sampleTime = anchor.audioSampleStartTime;
				}
				else
				{
					sampleTime = anchor.audioSampleStartTime + (int)(busPlaySampleTime - anchor.busSampleStartTime);
				}
			}
			return sampleTime;
		}

		/// <summary>
		/// Returns the time in samples of the currently playing <c>AudioGroup</c>.
		/// </summary>
		/// <returns>The time in samples of the currently playing audio.</returns>
		public int GetSampleTimeCurrentAudio()
		{
			lock (anchorLock)
			{
				return GetSampleTimeOfAnchor((prevSong.IsValid()) ? prevSong : curSong);
			}
		}

		/// <summary>
		/// Returns the playback position in samples of <paramref name="group"/>.
		/// </summary>
		/// <returns>The playback position in samples of <paramref name="group"/>.</returns>
		/// <param name="group">The <c>AudioGroup</c> to retrieve timing information about.</param>
		public int GetSampleTimeOfAudio(AudioGroup group)
		{
			int sampleTime = 0;
			lock (anchorLock)
			{
				if (prevSong.IsValid() && prevSong.audioGroup == group)
				{
					sampleTime = GetSampleTimeOfAnchor(prevSong);
				}
				else if (curSong.IsValid() && curSong.audioGroup == group)
				{
					sampleTime = GetSampleTimeOfAnchor(curSong);
				}
			}
			return sampleTime;
		}

		/// <summary>
		/// Determines whether <paramref name="group"/> currently playing.
		/// </summary>
		/// <returns><c>true</c> if audio data from <paramref name="group"/> is playing; otherwise,
		/// <c>false</c>.</returns>
		/// <param name="group">The <c>AudioGroup</c> to check.</param>
		public bool IsAudioPlaying(AudioGroup group)
		{
			bool bPlaying = false;
			lock (anchorLock)
			{
				if (prevSong.IsValid() && prevSong.audioGroup == group)
				{
					bPlaying = prevSong.IsPlayingAtBusTime(busPlaySampleTime);
				}
				else if (curSong.IsValid() && curSong.audioGroup == group)
				{
					bPlaying = curSong.IsPlayingAtBusTime(busPlaySampleTime);
				}
			}
			return bPlaying;
		}

		/// <summary>
		/// Determines whether this <c>AudioBus</c> is paused.
		/// </summary>
		/// <returns><c>true</c> if this <c>AudioBus</c> is paused; otherwise, <c>false</c>.</returns>
		public bool IsPaused()
		{
			return bPaused;
		}

		/// <summary>
		/// Determines whether an <c>AudioGroup</c> is scheduled for playback.
		/// </summary>
		/// <returns><c>true</c> if an <c>AudioGroup</c> is scheduled; otherwise, <c>false</c>.</returns>
		public bool IsNextSongScheduled()
		{
			return nextSong.IsValid();
		}

		#region Event Triggers

		void OnAudioEnded(AudioGroup songGroup, int sampleTime, DeltaSlice deltaSlice)
		{
			if (AudioEnded != null)
			{
				AudioEnded(songGroup, sampleTime, deltaSlice);
			}
		}
	
		#endregion
	}
}
