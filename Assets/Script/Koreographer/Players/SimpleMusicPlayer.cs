//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;

namespace SonicBloom.Koreo.Players
{
	/// <summary>
	/// The SimpleMusicPlayer component plays audio using Koreography.  Instead of
	/// taking an <c>AudioClip</c> reference, it takes a single Koreography reference.
	/// At runtime, the Koreography is loaded into the static Koreographer Instance.
	/// Adding this component will also add an <c>AudioSource</c> component, which is
	/// used internally to control audio.  The SimpleMusicPlayer uses the <c>AudioClip</c>
	/// reference in the referenced Koreography for audio playback.
	/// </summary>
	[RequireComponent(typeof(AudioSource))]
	[AddComponentMenu("Koreographer/Music Players/Simple Music Player")]
	public class SimpleMusicPlayer : MonoBehaviour, IKoreographedPlayer
	{
		#region Fields

		[SerializeField]
		[Tooltip("When an initial Koreography is specified, determines whether to play immediately on Awake() or not.")]
		bool autoPlayOnAwake = true;

		[SerializeField]
		[Tooltip("The Koreography to play back.")]
		Koreography koreography = null;
		
		[SerializeField]
		[Tooltip("[Optional] Specify a target Koreographer component to use for Koreography Event reporting and Music Time API support.  If no Koreographer is specified, the default global Koreographer component reference will be used.")]
		Koreographer targetKoreographer;

		AudioVisor visor = null;

		AudioSource audioCom = null;

		#endregion
		#region Properties

		/// <summary>
		/// Gets a value indicating whether the audio is playing or not (paused/stopped).
		/// </summary>
		/// <value><c>true</c> if the audio is playing; otherwise, <c>false</c>.</value>
		public bool IsPlaying
		{
			get
			{
				return audioCom.isPlaying;
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

			audioCom = GetComponent<AudioSource>();
			visor = new AudioVisor(audioCom, targetKoreographer);

			if (koreography != null)
			{
				// Get the Koreography loaded, cue up the sample position, and start playback if requested.
				LoadSong(koreography, 0, autoPlayOnAwake);
			}
		}

		void Update()
		{
			// Ensure that the visor gets the update call.
			visor.Update();
		}

		#endregion
		#region Playback Control

		/// <summary>
		/// <para>Loads the <paramref name="koreo"/> and begins playing its configured
		/// <paramref name="AudioClip"/> at time <paramref name="startSampleTime"/>.
		/// If <paramref name="autoPlay"/> is <c>true</c>, playback will begin
		/// immediately.</para>
		/// 
		/// <para>This immediately unloads the Koreography for any previously playing
		/// audio.</para>
		/// </summary>
		/// <param name="koreo">Koreo.</param>
		/// <param name="startSampleTime">Start sample time.</param>
		/// <param name="autoPlay">If set to <c>true</c> auto play.</param>
		public void LoadSong(Koreography koreo, int startSampleTime = 0, bool autoPlay = true)
		{
			targetKoreographer.UnloadKoreography(koreography);
			koreography = koreo;

			if (koreography != null)
			{
				targetKoreographer.LoadKoreography(koreography);
			
				audioCom.clip = koreography.SourceClip;

				SeekToSample(startSampleTime);

				if (autoPlay)
				{
					audioCom.Play();
				}
			}
		}

		/// <summary>
		/// Plays the loaded audio.
		/// </summary>
		public void Play()
		{
			if (!audioCom.isPlaying)
			{
				audioCom.Play();
			}
		}

		/// <summary>
		/// Stop playback.
		/// </summary>
		public void Stop()
		{
			audioCom.Stop();
		}

		/// <summary>
		/// Pauses playback.
		/// </summary>
		public void Pause()
		{
			audioCom.Pause();
		}

		/// <summary>
		/// Seeks the audio playback to the target sample location.
		/// </summary>
		/// <param name="targetSample">The location to seek to.</param>
		public void SeekToSample(int targetSample)
		{
			audioCom.timeSamples = targetSample;

			visor.ResyncTimings();
		}

		#endregion
		#region IKoreographedPlayer Methods

		/// <summary>
		/// Gets the current sample position of the <c>AudioClip</c> with name 
		/// <paramref name="clipName"/>.
		/// </summary>
		/// <returns>The current sample position of the <c>AudioClip</c> with name
		/// <paramref name="clipName"/>.</returns>
		/// <param name="clipName">The name of the <c>AudioClip</c> to check.</param>
		public int GetSampleTimeForClip(string clipName)
		{
			int sampleTime = 0;
			if (koreography != null && koreography.SourceClipName == clipName)
			{
				sampleTime = visor.GetCurrentTimeInSamples();
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
			int totalTime = 0;
			if (koreography != null && koreography.SourceClipName == clipName)
			{
				totalTime = audioCom.clip.samples;
			}
			return totalTime;
		}

		/// <summary>
		/// Determines whether the <c>AudioClip</c> with name <paramref name="clipName"/> 
		/// is playing.
		/// </summary>
		/// <returns><c>true</c>, if <c>AudioClip</c> with name <paramref name="clipName"/>
		/// is playing,<c>false</c> otherwise.</returns>
		/// <param name="clipName">The name of the <c>AudioClip</c> to check.</param>
		public bool GetIsPlaying(string clipName)
		{
			return (koreography != null && koreography.SourceClipName == clipName) && audioCom.isPlaying;
		}

		/// <summary>
		/// Gets the pitch.  The <paramref name="clipName"/> parameter is ignored.
		/// </summary>
		/// <returns>The pitch of the audio.</returns>
		/// <param name="clipName">The name of the <c>AudioClip</c> to check.  Currently ignored.</param>
		public float GetPitch(string clipName)
		{
			return audioCom.pitch;
		}

		/// <summary>
		/// Gets the name of the current <c>AudioClip</c>.
		/// </summary>
		/// <returns>The name of the currently playing <c>AudioClip</c>.</returns>
		public string GetCurrentClipName()
		{
			return (koreography == null) ? string.Empty : koreography.SourceClipName;
		}

		#endregion
	}
}
