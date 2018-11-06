//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;

namespace SonicBloom.Koreo.Players
{
	/// <summary>
	/// The Visor is the glue that binds the Audio Player and the Koreographer component
	/// together.  The Visor's sole responsibility is to provide the Koreographer
	/// component with information with which it can process Koreography Events.  This
	/// base class provides a common interface and set of core processing that handles
	/// most potential audio update scenarios.  Override and fill in the blanks!
	/// </summary>
	public abstract class VisorBase
	{
		#region Core Fields
		
		/// <summary>
		/// The Koreographer to report audio time updates to.
		/// </summary>
		protected Koreographer koreographerCom;
		/// <summary>
		/// The current sample time, possibly estimated.
		/// </summary>
		protected int sampleTime = -1;
		/// <summary>
		/// The most recently read sample time from the audio hardware (or equivalent).
		/// </summary>
		protected int sourceSampleTime = -1;
		
		#endregion
		#region State Accessors
		
		/// <summary>
		/// Gets the name of the audio currently playing back.  This is used to identify Koreography for
		/// event triggering.
		/// </summary>
		/// <returns>The name of the currently playing audio.</returns>
		protected abstract string GetAudioName();
		
		/// <summary>
		/// Gets whether or not the audio is currently playing back.
		/// </summary>
		/// <returns><c>true</c> if the audio is playing, <c>false</c> otherwise.</returns>
		protected abstract bool GetIsAudioPlaying();
		
		/// <summary>
		/// Gets whether the audio is set to loop or not.
		/// </summary>
		/// <returns><c>true</c> if audio should be looping, <c>false</c> otherwise.</returns>
		protected abstract bool GetIsAudioLooping();
		
		/// <summary>
		/// Gets whether the audio looped this frame or not.
		/// </summary>
		/// <returns><c>true</c> if the audio looped, <c>false</c> otherwise.</returns>
		protected abstract bool GetDidAudioLoop();
		
		/// <summary>
		/// Get the current time in samples of the current audio (the playhead position in samples).
		/// </summary>
		/// <returns>The current sample time.</returns>
		protected abstract int GetAudioSampleTime();
		
		/// <summary>
		/// Gets the first *playable* sample position of the current audio.
		/// </summary>
		/// <returns>The first playable sample position.</returns>
		protected abstract int GetAudioStartSampleExtent();
		
		/// <summary>
		/// Get the last *playable* sample position of the current audio.
		/// </summary>
		/// <returns>The last playable sample position.</returns>
		protected abstract int GetAudioEndSampleExtent();
		
		/// <summary>
		/// Get the number of samples that were played in the last frame.  Be sure to consider current settings 
		/// and playback state.
		/// </summary>
		/// <returns>The delta time in samples.</returns>
		protected abstract int GetDeltaTimeInSamples();
		
		#endregion
		#region Core Logic Methods
		
		/// <summary>
		/// Looks at the Audio System and determines how far the audio has moved.  It then
		/// reports those results, if necessary, to the specified  Koreographer to trigger
		/// any encountered Koreography Events.
		/// 
		/// This implementation is fairly robust in that it recognizes that some audio systems
		/// will not report a change in sample position until more samples are requested (no
		/// estimation).  In such cases it attempts to estimate the time spent.
		/// </summary>
		public virtual void Update()
		{
			if (GetIsAudioPlaying())
			{
				// Current time update!
				int prevSampleTime = sampleTime;					// Store last frame's value.
				int curSourceSampleTime = GetAudioSampleTime();     // Grab current reported sample time from source.
                Debug.Log("timeSamples---------->"+ curSourceSampleTime);

				float rawFrameTime = GetRawFrameTime();
				
				// Delta info.  Default of length zero is fine.
				DeltaSlice deltaSlice = new DeltaSlice();
				deltaSlice.deltaLength = rawFrameTime;				// Set the default for the fall-through case.
				
				// Check status of Audio System this frame vs last.
				if (sourceSampleTime == curSourceSampleTime)
				{
					// We're playing but the Audio System didn't update the time.  Interpolate based on
					//  tracked time, system time, and playback speed.
					int dtInSamples = GetDeltaTimeInSamples();
					int estSampleTime = prevSampleTime + dtInSamples;
					
					// Handle looping edge case.  Check if we're at or over the number of samples.  This is doable because 
					//  the reported sample time maxes out at the 0-indexed value.
					int finalAudioSample = GetAudioEndSampleExtent();
					if (estSampleTime > finalAudioSample)
					{
						if (GetIsAudioLooping())
						{
							// Process to the end of the song.
							sampleTime = finalAudioSample;
							prevSampleTime++;
							
							// Calculate the length of delta we're using.  This is the first processing slice.
							double endOffset = ((double)(sampleTime - prevSampleTime) / (double)dtInSamples);
							deltaSlice.deltaLength = (float)(endOffset * (double)rawFrameTime);
							
							koreographerCom.ProcessKoreography(GetAudioName(), prevSampleTime, sampleTime, deltaSlice);
							
							// Prep for fallthrough below.
							prevSampleTime = -1;
							sampleTime = estSampleTime - sampleTime;
							
							// Adjust the timing info for the next processing slice.
							deltaSlice.deltaOffset = (float)endOffset;
							deltaSlice.deltaLength = rawFrameTime - deltaSlice.deltaLength;
						}
						else
						{
							sampleTime = finalAudioSample;
						}
					}
					else
					{
						// We're within range so simply use the estimated sample time!
						sampleTime = estSampleTime;
					}
				}
				else if (curSourceSampleTime < sourceSampleTime)
				{
					// Looped?  Or position was set...
					if (GetDidAudioLoop())
					{
						// Check that we didn't already process the loop.
						if (sourceSampleTime <= prevSampleTime)
						{
							// Didn't process the loop yet.
							
							// Store the sampleTime.  We must do this prior to calling ProcessKoreography because callbacks may be
							//  triggered that want to know the music time.
							sampleTime = GetAudioEndSampleExtent();
							int dtInSamplesTotal = (sampleTime - prevSampleTime) + curSourceSampleTime;

							prevSampleTime++;
							
							// Calculate the length of delta we're using.  This is the first processing slice.
							double endOffset = ((double)(sampleTime - prevSampleTime) / (double)dtInSamplesTotal);
							deltaSlice.deltaLength = (float)(endOffset * (double)rawFrameTime);
							
							// Play to the end.
							koreographerCom.ProcessKoreography(GetAudioName(), prevSampleTime, sampleTime, deltaSlice);
							
							// Prep for beginning to curStartTime
							prevSampleTime = -1;
							
							// Adjust the timing info for the next processing slice.
							deltaSlice.deltaOffset = (float)endOffset;
							deltaSlice.deltaLength = rawFrameTime - deltaSlice.deltaLength;
						}
						// else - // We've already processed the loop.  Simply fall through, using the sampleTime update below.
					}
					else
					{
						// Assume the user changed the time directly.  Also, we don't know the time they set the AudioSource to.
						//  Therefore, simply back out with a guess by how much.
						
						// Calculate the amount of samples that should have played in the time since.
						int dtInSamples = GetDeltaTimeInSamples();
						
						// Back out the prevSampleTime.  The -1 is to offset the +1 that comes later (which ensures in most cases
						//  that we don't process a single sample twice.
						prevSampleTime = Mathf.Max(0, curSourceSampleTime - dtInSamples) - 1;
					}
					
					// Make sure we're properly set up for the fall-through handling below.  This works for both cases above.
					sampleTime = curSourceSampleTime;
				}
				else
				{
					sampleTime = curSourceSampleTime;
				}
				
				// Add one to startTime because "prevSampleTime" was already checked in the previous update!
				koreographerCom.ProcessKoreography(GetAudioName(), prevSampleTime + 1, sampleTime, deltaSlice);
				
				// Ensure we're up to date with the reported source sample time.
				sourceSampleTime = curSourceSampleTime;
			}
		}
		
		/// <summary>
		/// Gets the total number of samples that will be played back.
		/// </summary>
		/// <returns>The total number of samples in the audio that will be played back.</returns>
		protected int GetTotalPlaybackSamples()
		{
			// Add one because even if start and stop were "0", we'd still "Play sample at position 0."
			//  Zero should be handled by "GetIsAudioPlaying()".
			return 1 + (GetAudioEndSampleExtent() - GetAudioStartSampleExtent());
		}
		
		/// <summary>
		/// Gets the deltaTime used to process the frame *without* modification from Time.timeScale.
		/// </summary>
		/// <returns>The raw frame time.</returns>
		protected float GetRawFrameTime()
		{
			float rawFrameTime = Time.unscaledDeltaTime;

			// There is one known edge case that we check for here:
			//   1) When the Editor is paused (moved to background or the user presses the Pause button).
			//  Most of the time we want to use unscaled time for calculations. This is because the audio
			//  timeline moves at a fixed rate (sample rate); it is unaffected by the game simulation rate.
			//  That said, the unscaled delta time does not stop accumulating when the Editor pauses in the
			//  manners outlined in #1 above (at least). In these cases, the first frame re-processed when
			//  returning from the paused state is typically very short, falling underneath the maximum
			//  delta time. If we detect that the unscaled time is above the maximum delta but that the
			//  standard deltaTime is less than the maximum delta, then we prefer the standard deltaTime.
			// TODO: Determine if this effect is Editor-only or if it happens at game time as well. If
			//  Editor-only, we could add Preprocessor directives around it. The best possible solution
			//  for this, though, would be to determine a way to tell if the game had resumed from
			//  pause or not...
			// In the future, we may want to use EditorApplication.playmodeStateChanged to detect "play
			//  restarting" and use that to set (and then clear?) a flag. For now, this appears to work
			//  well enough.
			if (Time.unscaledDeltaTime >= Time.maximumDeltaTime && Time.deltaTime < Time.maximumDeltaTime)
			{
				rawFrameTime = Time.deltaTime / Time.timeScale;
			}

            Debug.Log(string.Format("deltaTime: {0}  unscaledDeltaTime: {1}",Time.deltaTime,Time.unscaledDeltaTime));
			return rawFrameTime;
		}
		
		#endregion
	}
}
