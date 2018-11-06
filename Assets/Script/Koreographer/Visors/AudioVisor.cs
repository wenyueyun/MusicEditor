//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;

namespace SonicBloom.Koreo.Players
{
	/// <summary>
	/// A struct to hold information that uniquely identifies an
	/// <c>AudioClip</c>.
	/// </summary>
	public struct AudioClipID
	{
		/// <summary>
		/// The Object.InstanceID associated with a specific AudioClip.
		/// </summary>
		public int instanceID;
		/// <summary>
		/// The cached name of a specific AudioClip.
		/// </summary>
		public string name;
	}

	/// <summary>
	/// The AudioVisor is built on top of VisorBase, specifically adding support for
	/// Unity's built-in audio system.  It is built around the <c>AudioSource</c>
	/// component and sends timing information based on its intricacies.
	/// </summary>
	public class AudioVisor : VisorBase
	{
		#region Fields

		/// <summary>
		/// The <c>AudioSource</c> component that this AudioVisor watches over.
		/// </summary>
		protected AudioSource audioCom;
		/// <summary>
		/// The ID of the <c>AudioClip</c> this AudioVisor is watching over.
		/// If a change is detected, this should get updated.
		/// </summary>
		protected AudioClipID clipID;
		/// <summary>
		/// If AudioSource Scheduling features are used, this field will contain
		/// the target start time according to the DSP clock.
		/// </summary>
		protected double dspPlayStartTime = 0d;

		#endregion
		#region Properties

		/// <summary>
		/// Gets or sets the scheduled play time of the associated AudioSource. This is required
		/// for properly handling scheduled playback because Unity does not have a way to inspect
		/// the scheduled state of an AudioClip in an AudioSource.
		/// </summary>
		/// <value>The DSP time at which audio playback is set to begin.</value>
		public double ScheduledPlayTime
		{
			get
			{
				return dspPlayStartTime;
			}
			set
			{
				dspPlayStartTime = value;
			}
		}

		#endregion
		#region Constructors

		/// <summary>
		/// Private default constructor means we require a different constructor.
		/// </summary>
		private AudioVisor(){}

		/// <summary>
		/// Initializes a new instance of the <see cref="AudioVisor"/> class.  This will 
		/// connect the AudioSource to a Koreographer.
		/// </summary>
		/// <param name="sourceCom">The AudioSource component to watch.</param>
		/// <param name="targetKoreographer">If specified, updates are sent to this
		/// Koreographer.  Otherwise they use the default global Koreographer.</param>
		public AudioVisor(AudioSource sourceCom, Koreographer targetKoreographer = null)
		{
			// Potentially store a specific Koreographer to report to.
			koreographerCom = targetKoreographer;
			if (koreographerCom == null)
			{
				koreographerCom = Koreographer.Instance;
			}

			audioCom = sourceCom;

			// This class shouldn't work without a valid AudioSource.
			if (audioCom == null)
			{
				System.NullReferenceException e = new System.NullReferenceException("AudioVisor does not work with a null AudioSource.");
				throw e;
			}

			// Initialize timings.
			sourceSampleTime = GetAudioSampleTime();
		}

		#endregion
		#region AudioVisor-Specific
		
		/// <summary>
		/// This is used when the <c>AudioSource</c> has a clip change or a jump within the audio. The
		/// <c>AudioVisor</c> attempts to catch "seeks" automatically, but can't guarantee perfect timing.
		/// This forces the <c>AudioVisor</c> to resync to the sample time reported by the
		/// <c>AudioSource</c> at time of call. Using this is far better than allowing the <c>AudioVisor</c>
		/// to estimate the seeked position.
		/// </summary>
		public void ResyncTimings()
		{
			// TODO: Optionally enable processing an Update first?
			ResyncTimings(GetAudioSampleTime());
		}
		
		/// <summary>
		/// This is used when the <c>AudioSource</c> has a clip change or a jump within the audio. The
		/// <c>AudioVisor</c> attempts to catch "seeks" automatically, but can't guarantee perfect timing.
		/// This forces the <c>AudioVisor</c> to resync to the sample position supplied in the
		/// <paramref name="targetSampleTime"/> parameter. Using this is far better than allowing the
		/// <c>AudioVisor</c> to estimate the seeked position.
		/// </summary>
		/// <param name="targetSampleTime">The sample position to which the <c>AudioSource</c> was seeked.</param>
		public void ResyncTimings(int targetSampleTime)
		{
			// TODO: Optionally enable processing an Update first?
			Mathf.Clamp(targetSampleTime, 0, GetAudioEndSampleExtent());
			
			sourceSampleTime = targetSampleTime;
			sampleTime = sourceSampleTime - 1;
		}
		
		/// <summary>
		/// Gets the current time in samples.
		/// </summary>
		/// <returns>The current time in samples.</returns>
		public int GetCurrentTimeInSamples()
		{
			return Mathf.Max(0, sampleTime);		// Use Max() because sampleTime can be -1 (during initialization/startup).
		}
		
		#endregion
		#region State Accessors

		override protected string GetAudioName()
		{
			int curClipInstID = audioCom.clip.GetInstanceID();
			if (curClipInstID != clipID.instanceID)
			{
				clipID.instanceID = curClipInstID;
				clipID.name = audioCom.clip.name;
			}

			return clipID.name;
		}

		override protected bool GetIsAudioPlaying()
		{
			return audioCom.isPlaying && (dspPlayStartTime <= AudioSettings.dspTime);
		}
		
		override protected bool GetIsAudioLooping()
		{
			return audioCom.loop;
		}

		override protected bool GetDidAudioLoop()
		{
			bool bLooped = false;

			if (GetIsAudioLooping())
			{
				// TODO: Possibly check for audio thread starvation? When the audio thread is starved, the amount of
				//  *game time* should exceed the amount of *audio time* advanced (and by a decent margin). Such a
				//  check will need to keep audio playback speed (pitch) in mind.

				// To determine if a loop occurred, we need to verify that the amount of time that passed since
				//  the previous read location was enough for the audio to go back around to the beginning.
				// Assume that we read from the previous location to the end of the clip and then from the
				//  beginning to the current sample position. The resultant value should be something we expect
				//  based on the delta time of the frame.
				int totalSampleDist = GetAudioSampleTime() + (GetAudioEndSampleExtent() - sourceSampleTime);

				//  Add 1 to include the positional sample as part of the magnitude.
				totalSampleDist += 1;

				// Compare the total samples between ([lastSamplePos, lastClipSample] and [0, curSamplePos]) with the
				//  delta game time. We double the delta time to account for "slop" between frames.
				// TODO: Handle this by storing the previous frame's delta time as well in case of a poorly [precisely]
				//  timed really heavy frame?
				// The other option here is to compare the totalSampleDist with the size of the full audio buffer,
				//  which would be (audioBufferLen * numBuffers). The problem with this is that a poorly timed heavy
				//  frame could break this as well by pushing us beyond the size of the ring.
				bLooped = (totalSampleDist < GetDeltaTimeInSamples() * 2);
			}

			return bLooped;
		}

		override protected int GetAudioSampleTime()
		{
			return audioCom.timeSamples;
		}

		// Returns the first *playable* sample position.
		override protected int GetAudioStartSampleExtent()
		{
			return 0;
		}

		// Returns the last *playable* sample position.
		override protected int GetAudioEndSampleExtent()
		{
			// Subtract one because AudioClip.samples is "total" whereas "playable" is 0-indexed.
			return audioCom.clip.samples - 1;
		}
	
		/// <summary>
		/// Convert this frame's delta time from solar time to sample time.  Note that this
		/// does not respect <c>Time.timeScale</c>: that kind of math comes in with the audio pitch
		/// settings.
		/// </summary>
		/// <returns>The delta time in samples.</returns>
		override protected int GetDeltaTimeInSamples()
		{
			return (int)(((double)audioCom.clip.frequency * (double)audioCom.pitch) * (double)GetRawFrameTime());
		}

		#endregion
	}
}
