//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;

namespace SonicBloom.Koreo.Players
{
	/// <summary>
	/// The Audio Source Visor adds Koreographer event triggering support
	/// to a single, targeted <c>AudioSource</c>.  Any audio played back
	/// through the <c>AudioSource</c> will be reported to either the
	/// default Koreographer or the <paramref name="targetKoreographer"/>,
	/// if specified.
	/// </summary>
	[AddComponentMenu("Koreographer/Visors/Audio Source Visor")]
	public class AudioSourceVisor : MonoBehaviour
	{
		#region Fields

		[SerializeField]
		[Tooltip("[Optional] Specify a target AudioSource component for this AudioSourceVisor to watch over. " +
				 "If no AudioSource is specified, this component will attempt to find and use an AudioSource " +
				 "component located on the same GameObject. If none is found, it will log a warning and " +
				 "disable itself.")]
		AudioSource targetAudioSource;

		/// <summary>
		/// An optional <c>Koreographer</c> component.  Set this to force targeting a specific Koreographer for
		/// event driving.
		/// </summary>
		[SerializeField]
		[Tooltip("[Optional] Specify a target Koreographer component to use for Koreography Event reporting. " +
				 "If no Koreographer is specified, the default global Koreographer component reference will " +
				 "be used.")]
		Koreographer targetKoreographer;
		
		AudioVisor visor;

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
				return visor.ScheduledPlayTime;
			}
			set
			{
				visor.ScheduledPlayTime = value;
			}
		}

		#endregion
		#region Methods
		
		void Awake()
		{
			if (targetAudioSource == null)
			{
				targetAudioSource = GetComponent<AudioSource>();
			}

			if (targetAudioSource == null)
			{
				Debug.LogWarning("No Target Audio Source specified and no AudioSource found on Game Object '" +
				                 gameObject.name + "'. Disabling this AudioSourceVisor.");
				enabled = false;
			}
			else
			{
				visor = new AudioVisor(targetAudioSource, targetKoreographer);
			}

		}
		
		void Update()
		{
			// This should never be null.  If it is, then there's a serious problem.
			visor.Update();
		}

		/// <summary>
		/// <para>This method should be called when the <c>AudioSource</c> being watched is seeked to ensure
		/// proper timing updates. Specify the seek position in samples with the
		/// <paramref name="targetSampleTime"/> parameter.</para>
		/// <para>This should also be used if the <c>AudioClip</c> loaded into the <c>AudioSource</c>
		/// changes.</para>
		/// </summary>
		/// <param name="targetSampleTime">The sample position to which the <c>AudioSource</c> was seeked.
		/// The value will be internally clamped between [0, totalSamplesInClip - 1).</param>
		public void ResyncTimings(int targetSampleTime)
		{
			visor.ResyncTimings(targetSampleTime);
		}

		#endregion
	}
}
