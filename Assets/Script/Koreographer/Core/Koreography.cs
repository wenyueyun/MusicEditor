//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SonicBloom.Koreo
{
	/// <summary>
	/// A group of n-Koreography Track objects associated with a single AudioClip object.
	/// Each track is uniquely tied to a single <c>AudioClip</c>.  Each Koreography
	/// Track is uniquely (for the purposes of this Koreography) tied to a single
	/// <c>string</c> Event ID.
	/// </summary>
	public class Koreography : ScriptableObject
	{
		#region Fields
		
		[SerializeField]
		[Tooltip("The AudioClip to which this Koreography refers.  Koreograhpy tracks should describe events matched to this AudioClip.")]
		AudioClip mSourceClip = null;

		[SerializeField]
		[Tooltip("The URI of the location of the Audio file to use for this Koreography.")]
		string mAudioFilePath = string.Empty;

		[SerializeField]
		[Tooltip("The Sample Rate of the audio data that Koreography Tracks were authored against.")]
		int mSampleRate = -1;

		[SerializeField]
		[Tooltip("Whether or not this Koreography should ignore any configured Latency/Delay Offset set on the Koreographer component.")]
		bool mIgnoreLatencyOffset = false;
		
		[SerializeField]
		[Tooltip("The Tempo Map for this Koreography that describes the tempo throughout the referenced AudioClip.")]
		List<TempoSectionDef> mTempoSections = new List<TempoSectionDef>(new TempoSectionDef[1]{new TempoSectionDef()});
		
		[SerializeField]
		[Tooltip("The Koreography Tracks associated with this Koreography.")]
		List<KoreographyTrackBase> mTracks = new List<KoreographyTrackBase>();

		// Avoid allocations by caching the name of the clip locally.
		string clipName;
		
		int lastUpdateStart = 0;
		int lastUpdateEnd = 0;
		
		#endregion
		#region Properties

		/// <summary>
		/// <para>Gets or sets the <c>AudioClip</c> associated with this Koreography.</para>
		/// 
		/// <para>Note: This will clear any previous SourceClipPath setting.</para>
		/// </summary>
		/// <value>The <c>AudioClip</c> object.</value>
		public AudioClip SourceClip
		{
			get
			{
				return mSourceClip;
			}
			set
			{
				mSourceClip = value;
				mAudioFilePath = string.Empty;

				// Update for editor or runtime.
				if (value == null)
				{
					clipName = string.Empty;
				}
				else
				{
					clipName = mSourceClip.name;
				}
			}
		}

		/// <summary>
		/// <para>Gets or sets the Path for the Audio File associated with this Koreography.</para>
		/// 
		/// <para>Note: This will clear any previous SourceClip setting.</para>
		/// </summary>
		/// <value>The Path to the Audio File associated with this Koreography.</value>
		public string SourceClipPath
		{
			get
			{
				return mAudioFilePath;
			}
			set
			{
				mAudioFilePath = value;
				mSourceClip = null;

				// Update for editor or runtime.
				clipName = Path.GetFileNameWithoutExtension(mAudioFilePath);
			}
		}

		/// <summary>
		/// Gets the name of the source clip or audio file associated with this Koreography.
		/// </summary>
		/// <value>The name of the source <c>AudioClip</c> or audio file.</value>
		public string SourceClipName
		{
			get
			{
				if (string.IsNullOrEmpty(clipName))
				{
					clipName = (SourceClip != null) ? SourceClip.name : Path.GetFileNameWithoutExtension(mAudioFilePath);
				}

				return clipName;
			}
		}

		/// <summary>
		/// Gets or sets the Sample Rate of the audio data that Koreography Tracks were authored against.  
		/// This may be different between Edit-time and Runtime.
		/// </summary>
		/// <value>The 'authored' sample rate.</value>
		public int SampleRate
		{
			get
			{
				// TODO: Remove this check in v2.  This was added as a fallback to make upgrading to 1.1.0+
				//  easier for early users.
				if (mSampleRate == -1 && mSourceClip != null)
				{
					mSampleRate = mSourceClip.frequency;
				}

				return mSampleRate;
			}
			set
			{
				if (mSampleRate != -1 && value != mSampleRate && mTracks.Count > 0)
				{
					Debug.LogWarning("Changing the Sample Rate may break KoreographyEvents in previously configured KoreographyTracks.  Please verify data in the Koreography Editor.");
				}

				mSampleRate = value;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether this Koreography should ignore any configured
		/// latency or delay offsets.
		/// </summary>
		/// <value><c>true</c> if processing Koreography Events in Tracks within this
		/// Koreography should ignore the configured latency/delay; otherwise, <c>false</c>.</value>
		public bool IgnoreLatencyOffset
		{
			get
			{
				return mIgnoreLatencyOffset;
			}
			set
			{
				mIgnoreLatencyOffset = value;
			}
		}
		
		/// <summary>
		/// Returns a COPY of the internal list of Koreography Tracks.  This grants access to
		/// configured tracks but does not allow for editing of the internal Track List itself.
		/// </summary>
		/// <value>A list of Koreography Tracks contained in this Koreography.</value>
		public List<KoreographyTrackBase> Tracks
		{
			get
			{
				return new List<KoreographyTrackBase>(mTracks);
			}
		}
		
		#endregion
		#region Maintenance Methods

		/// <summary>
		/// Checks the integrity of the internal Koreography Track list, removing any <c>null</c>
		/// entries.
		/// </summary>
		/// <returns><c>true</c>, if there was a change to the list during this operation,
		/// <c>false</c> otherwise.</returns>
		public bool CheckTrackListIntegrity()
		{
			int startLength = mTracks.Count;
			
			// Remove NULL entries.
			mTracks.RemoveAll(track => track == null);
			
			return (startLength != mTracks.Count);
		}

		/// <summary>
		/// Checks the integrity of the internal Tempo Section list, removing any <c>null</c>
		/// entries.
		/// </summary>
		/// <returns><c>true</c>, if there was a change to the list during this operation,
		/// <c>false</c> otherwise.</returns>
		public bool CheckTempoSectionListIntegrity()
		{
			int startLength = mTempoSections.Count;
			
			// Remove NULL entries.
			mTempoSections.RemoveAll(section => section == null);
			
			EnsureTempoSectionOrder();
			
			// TODO: determine if this should be done elsewhere.  This seems pretty safe, but...
			bool bDidAdjustFirst = false;
			if (mTempoSections[0].StartSample > 0)
			{
				mTempoSections[0].StartSample = 0;
				bDidAdjustFirst = true;
			}
			
			return bDidAdjustFirst || (startLength != mTracks.Count);
		}
		
		#endregion
		#region Tempo Section Management Methods

		/// <summary>
		/// Completely replaces the currently configured list of Tempo Sections with the
		/// one passed in.
		/// </summary>
		/// <param name="newSections">The list of sections to overwrite the current list
		/// with.</param>
		// TODO: Store a COPY of the passed in list, not a direct reference to it.
		public void OverwriteTempoSections(List<TempoSectionDef> newSections)
		{
			if (mTempoSections.Count > 0)
			{
				mTempoSections = newSections;
			}
			
			EnsureTempoSectionOrder();
			
			if (mTempoSections[0].StartSample != 0)
			{
				Debug.LogWarning("The new Tempo Sections don't start at 0.  This is required!  Overwriting.");
				
				mTempoSections[0].StartSample = 0;
			}
		}

		/// <summary>
		/// Gets the index of the given Tempo Section.
		/// </summary>
		/// <returns>The index of the Tempo Section, <c>-1</c> if not found.</returns>
		/// <param name="sectionDef">The Tempo Section to check.</param>
		public int GetIndexOfTempoSection(TempoSectionDef sectionDef)
		{
			return mTempoSections.IndexOf(sectionDef);
		}

		/// <summary>
		/// Creates a new Tempo Section with default values at the given index and returns it.
		/// </summary>
		/// <returns>The newly created and added Tempo Section.</returns>
		/// <param name="idxToInsert">The index location at which to insert.</param>
		public TempoSectionDef InsertTempoSectionAtIndex(int idxToInsert)
		{
			TempoSectionDef newSectionDef = null;
			
			if (idxToInsert >= 0 && idxToInsert <= mTempoSections.Count)
			{
				newSectionDef = new TempoSectionDef();
				if (idxToInsert == mTempoSections.Count)
				{
					mTempoSections.Add(newSectionDef);
				}
				else
				{
					mTempoSections.Insert(idxToInsert, newSectionDef);
				}
			}
			
			return newSectionDef;
		}

		/// <summary>
		/// Removes the TempoSection at the index provided.
		/// </summary>
		/// <returns><c>true</c>, if a Tempo Section was removed, <c>false</c> otherwise.</returns>
		/// <param name="idxToRemove">The index at which to remove a Tempo Section.</param>
		public bool RemoveTempoSectionAtIndex(int idxToRemove)
		{
			bool bDidRemove = false;
			
			// Only allow removal if we have more than one section.
			if (mTempoSections.Count > 1 &&
			    idxToRemove < mTempoSections.Count)
			{
				mTempoSections.RemoveAt(idxToRemove);
				bDidRemove = true;
				
				// Ensure that our first section is okay!
				if (idxToRemove == 0)
				{
					// Force the first sample to the beginning of the song.
					mTempoSections[0].StartSample = 0;
				}
			}
			
			return bDidRemove;
		}

		/// <summary>
		/// Removes the given Tempo Section from the Koreography.
		/// </summary>
		/// <returns><c>true</c>, if a Tempo Section was removed, <c>false</c> otherwise.</returns>
		/// <param name="sectionDef">The Tempo Section to remove.</param>
		public bool RemoveTempoSection(TempoSectionDef sectionDef)
		{
			return RemoveTempoSectionAtIndex(GetIndexOfTempoSection(sectionDef));
		}

		/// <summary>
		/// Get the name of all configured Tempo Sections.
		/// </summary>
		/// <returns>An array of strings containing the configured name, if any, of Tempo Sections
		/// in the Koreography.</returns>
		public string[] GetTempoSectionNames()
		{
			return mTempoSections.Select(section => section.SectionName).ToArray();
		}

		/// <summary>
		/// Get the Tempo Section at a given index.
		/// </summary>
		/// <returns>The Tempo Section found at the given index, if any.</returns>
		/// <param name="sectionIdx">The index of the Tempo Section to retrieve.</param>
		public TempoSectionDef GetTempoSectionAtIndex(int sectionIdx)
		{
			return (sectionIdx >= 0 && sectionIdx < mTempoSections.Count) ? mTempoSections[sectionIdx] : null;
		}

		/// <summary>
		/// Gets the number of Tempo Sections in the Koreography.
		/// </summary>
		/// <returns>The total number of Tempo Sections.</returns>
		public int GetNumTempoSections()
		{
			return mTempoSections.Count;
		}

		/// <summary>
		/// Sorts the Tempo Sections in the Koreography by configured <c>StartSample</c>,
		/// ensuring proper order.
		/// </summary>
		public void EnsureTempoSectionOrder()
		{
			mTempoSections.Sort(TempoSectionDef.CompareByStartSample);
		}
		
		#endregion
		#region Track Management Methods

		/// <summary>
		/// Determines whether the <paramref name="track"/> can be added to the Koreography.
		/// A Koreography Track cannot be added if another Koreography Track with the same
		/// Event ID already exists in the Koreography.
		/// </summary>
		/// <returns><c>true</c> if the <paramref name="track"/> can be added; otherwise, <c>false</c>.</returns>
		/// <param name="track">The Koreography Track to check with.</param>
		public bool CanAddTrack(KoreographyTrackBase track)
		{
			return !DoesTrackWithEventIDExist(track.EventID);
		}

		/// <summary>
		/// Adds the <paramref name="track"/> to the Koreography.
		/// </summary>
		/// <returns><c>true</c>, if <paramref name="track"/> was added, <c>false</c> otherwise.</returns>
		/// <param name="track">The Koreography Track to add.</param>
		public bool AddTrack(KoreographyTrackBase track)
		{
			bool bDidAdd = false;
			if (CanAddTrack(track))
			{
				mTracks.Add(track);
				
				bDidAdd = true;
			}
			return bDidAdd;
		}

		/// <summary>
		/// Removes the Koreography Track <paramref name="track"/> from the Koreography.
		/// </summary>
		/// <param name="track">The Koreography Track to remove.</param>
		public void RemoveTrack(KoreographyTrackBase track)
		{
			mTracks.Remove(track);
		}

		/// <summary>
		/// Removes the Koreography Track an Event ID matching <paramref name="eventID"/> from the
		/// Koreography.
		/// </summary>
		/// <param name="eventID">Event I.</param>
		public void RemoveTrack(string eventID)
		{
			mTracks.Remove(GetTrackByID(eventID));
		}

		/// <summary>
		/// Returns a <c>string Array</c> that contains the Event IDs for all Koreography
		/// Tracks in the Koreography.
		/// </summary>
		/// <returns>A <c> string Array</c> with Event IDs from configured Koreography
		/// Tracks.</returns>
		public string[] GetEventIDs()
		{
			return (mTracks.Count > 0) ? mTracks.Select(track => track.EventID).ToArray() : new string[]{""};
		}

		/// <summary>
		/// Returns the Koreography Track with Event ID matching <paramref name="eventID"/>.
		/// </summary>
		/// <returns>The Koreography Track with Event ID matching <paramref name="eventID"/>,
		/// <c>null</c> otherwise.</returns>
		/// <param name="eventID">The Event ID of the Koreography Track to retrieve.</param>
		public KoreographyTrackBase GetTrackByID(string eventID)
		{
			KoreographyTrackBase track = null;

			for (int i = 0; i < mTracks.Count; ++i)
			{
				if (mTracks[i].EventID == eventID)
				{
					track = mTracks[i];
					break;
				}
			}

			return track;
		}

		/// <summary>
		/// Gets the Koreography Track at <paramref name="trackIdx"/>.
		/// </summary>
		/// <returns>The Koreography Track at <paramref name="trackIdx"/> if one exists,
		/// <c>null</c> otherwise.</returns>
		/// <param name="trackIdx">The index of the Koreography Track within the internal
		/// Koreography Track list.</param>
		public KoreographyTrackBase GetTrackAtIndex(int trackIdx)
		{
			return (trackIdx >= 0 && trackIdx < mTracks.Count) ? mTracks[trackIdx] : null;
		}

		/// <summary>
		/// Gets the index of <paramref name="track"/> within the internal Koreography Track
		/// list.
		/// </summary>
		/// <returns>The index of <paramref name="track"/> within the internal Koreography
		/// Track list if it exists, <c>-1</c> otherwise.</returns>
		/// <param name="track">The Koreography Track to get the index of.</param>
		public int GetIndexOfTrack(KoreographyTrackBase track)
		{
			return mTracks.IndexOf(track);
		}

		/// <summary>
		/// Determines whether <paramref name="track"/> exists within the Koreography.
		/// </summary>
		/// <returns><c>true</c> if <paramref name="track"/> exists within the Koreography;
		/// otherwise, <c>false</c>.</returns>
		/// <param name="track">The Koreography Track to check.</param>
		public bool HasTrack(KoreographyTrackBase track)
		{
			return mTracks.Contains(track);
		}

		/// <summary>
		/// Gets the number of Koreography Tracks in the Koreography.
		/// </summary>
		/// <returns>The number of Koreography Tracks in the Koreography.</returns>
		public int GetNumTracks()
		{
			return mTracks.Count;
		}

		/// <summary>
		/// Checks whether a Koreography Track with the Event ID <paramref name="eventID"/> exists.
		/// </summary>
		/// <returns><c>true</c>, if a Koreography Track with Event ID <paramref name="eventID"/> exists,
		/// <c>false</c> otherwise.</returns>
		/// <param name="eventID">The Event ID to check.</param>
		public bool DoesTrackWithEventIDExist(string eventID)
		{
			return GetTrackByID(eventID) != null;
		}
		
		#endregion
		#region Music Timing

		/// <summary>
		/// Resets the internally tracked sample timings.
		/// </summary>
		public void ResetTimings()
		{
			lastUpdateStart = 0;
			lastUpdateEnd = 0;
		}
		
		// TODO: Add a function called GetCurrentBeatTime(int subdivisions) that returns the beat time based on
		//  the internal lastUpdateEnd field?

		/// <summary>
		/// Gets the amount of time in Beats the current update pass represents.
		/// </summary>
		/// <returns>The amount of time in Beats the current update pass represents.</returns>
		/// <param name="subdivisions">The number of subdivisions of a standard Beat that
		/// determine the Beat.</param>
		public float GetBeatTimeDelta(int subdivisions = 1)
		{
			int startSectionIdx = GetTempoSectionIndexForSample(lastUpdateStart);
			int endSectionIdx = GetTempoSectionIndexForSample(lastUpdateEnd);

			double delta = 0d;

			// Initialize the first "start" location and the index.
			int startPos = lastUpdateStart;
			int idx = startSectionIdx;
			for ( ; idx < endSectionIdx; ++idx)
			{
				TempoSectionDef nextSection = mTempoSections[idx + 1];
				TempoSectionDef curSection = mTempoSections[idx];

				// Add the delta.
				delta += (nextSection.StartSample - startPos) / (curSection.SamplesPerBeat / (double)subdivisions);
				// Iterate the checked location.
				startPos = nextSection.StartSample;
			}

			// Add the final section's delta.  The idx will either be the startSectionIdx (if the two indexes
			//  are identical) or it will be the final endSectionIdx.
			delta += (lastUpdateEnd - startPos) / (mTempoSections[idx].SamplesPerBeat / (float)subdivisions);

			return (float)delta;
		}

		/// <summary>
		/// Gets the time in samples of the current update pass.
		/// </summary>
		/// <returns>The time in samples for the current update pass.</returns>
		public int GetLatestSampleTime()
		{
			return lastUpdateEnd;
		}

		/// <summary>
		/// Gets the amount of time in samples the current update pass represents.
		/// </summary>
		/// <returns>The amount of time in samples the current update pass represents.</returns>
		public int GetLatestSampleTimeDelta()
		{
			// Add one because the sample bounds are INCLUSIVE.
			return 1 + (lastUpdateEnd - lastUpdateStart);
		}

		/// <summary>
		/// Gets the amount of time in samples the current update pass represents.
		/// </summary>
		/// <returns>The amount of time in samples the current update pass represents.</returns>
		[System.Obsolete("This method will be removed in an upcoming version.  Please update to use the more descriptive \"GetLatestSampleTimeDelta\" version.")]
		public int GetSampleTimeDelta()
		{
			return GetLatestSampleTimeDelta();
		}

		/// <summary>
		/// This method is obsolete and will be removed in an upcoming version.  Please use
		/// <c>GetBeatTimeFromSampleTime</c> instead.
		/// </summary>
		/// <returns>The Beat Time equivalent of <paramref name="sampleTime"/>.</returns>
		/// <param name="sampleTime">The time in samples.</param>
		/// <param name="subdivisions">The number of subdivisions of a standard Beat that
		/// determine the Beat.</param>
		[System.Obsolete("Internal refactoring has made this \"Lite\" version obsolete.  Please use the main GetBeatTimeFromSampleTime version instead.")]
		public double GetBeatTimeFromSampleTimeLite(int sampleTime, int subdivisions = 1)
		{
			return GetBeatTimeFromSampleTime(sampleTime, subdivisions);
		}

		/// <summary>
		/// Converts the <paramref name="sampleTime"/> from Sample Time into Beat Time with
		/// the subdivision value specified by <paramref name="subdivisions"/>.
		/// </summary>
		/// <returns>The Beat Time equivalent of <paramref name="sampleTime"/>.</returns>
		/// <param name="sampleTime">The time in samples.</param>
		/// <param name="subdivisions">The number of subdivisions of a standard Beat that
		/// determine the Beat.</param>
		public double GetBeatTimeFromSampleTime(int sampleTime, int subdivisions = 1)
		{
			double beatTime = 0d;
			
			int destTempoSectionIdx = GetTempoSectionIndexForSample(sampleTime);

			TempoSectionDef curSection = mTempoSections[0];
			
			for (int i = 1; i <= destTempoSectionIdx; ++i)
			{
				TempoSectionDef nextSection = mTempoSections[i];

				beatTime += curSection.GetBeatTimeFromSampleTime(nextSection.StartSample, subdivisions);

				if (nextSection.DoesStartNewMeasure)
				{
					// Next section restarts the measure (and thus the beat).  Special handling for this case!

					// If the section *does not* begin on a beat boundary we must reset it.  Skipping
					//  this increment stops us from having TWO beat numbers equate to a single sample
					//  position.
					double remainder = beatTime % 1d;
					double sampleDiff = 1d / curSection.SamplesPerBeat;
					if (remainder >= sampleDiff && (1d - remainder) >= sampleDiff)
					{
						beatTime = System.Math.Floor(beatTime) + 1d;
					}
				}

				curSection = nextSection;
			}
			
			return beatTime + curSection.GetBeatTimeFromSampleTime(sampleTime, subdivisions);
		}

		/// <summary>
		/// Converts the <paramref name="beatTime"/> from Beat Time into Sample Time with
		/// the subdivision value specified by <paramref name="subdivisions"/>.
		/// </summary>
		/// <returns>The Sample Time equivalent of <paramref name="beatTime"/>.</returns>
		/// <param name="beatTime">The time in beats.</param>
		/// <param name="subdivisions">The number of subdivisions of a standard Beat that
		/// determine the Beat.</param>
		public int GetSampleTimeFromBeatTime(double beatTime, int subdivisions = 1)
		{
			// Find the TempoSection that the beat is within.
			int nextSectionIdx = 1;
			int numSections = GetNumTempoSections();

			double beatsSoFar = 0d;

			TempoSectionDef curSection = mTempoSections[0];

			while (nextSectionIdx < numSections)
			{
				TempoSectionDef nextSection = mTempoSections[nextSectionIdx];

				double totalBeatsInSection = curSection.GetBeatTimeFromSampleTime(nextSection.StartSample, subdivisions);

				if (beatTime <= beatsSoFar + totalBeatsInSection)
				{
					// We are within the current section!
					break;
				}
				else
				{
					// Target beat is beyond the current section!
					beatsSoFar += totalBeatsInSection;

					if (nextSection.DoesStartNewMeasure)
					{
						// Next section restarts the measure (and thus the beat).  Special handling for this case!

						// Only do extra processing if this section *does not* fall on a beat boundary.
						//  This will stop us from treating the same location as equivalent for two
						//  separate beat numbers.
						double remainder = beatsSoFar % 1d;
						double sampleDiff = 1d / curSection.SamplesPerBeat;
						if (remainder >= sampleDiff && (1d - remainder) >= sampleDiff)
						{
							// Get the amount of time to the next beat as this section starts a new measure [and thus beat].
							double adjustTime = (System.Math.Floor(beatsSoFar) + 1d) - beatsSoFar;

							// This is a special early break!  This actually puts us at the *next* section!
							//  We can break here to save on some work, but we must be careful to iterate
							//  the sections!
							if (beatTime <= beatsSoFar + adjustTime)
							{
								// We're in the "void" space between the last beat of the previous section and the first
								//  of the next.
								beatsSoFar = beatTime;
								curSection = nextSection;
								break;
							}
							else
							{
								// Fast forward us to the next section beat start time.
								beatsSoFar += adjustTime;
							}
						}
					}
				}

				// Iterate section.
				curSection = nextSection;
				nextSectionIdx++;
			}

			// Subtract out the consumed time.
			beatTime -= beatsSoFar;

			// Maybe a way to do this by subtracting the beats so far meaning that we do it by beatTimeLeft?
			return curSection.StartSample + (int)(beatTime * curSection.GetSamplesPerBeatSection(subdivisions));
		}

		/// <summary>
		/// Converts the <paramref name="sampleTime"/> from Sample Time into Measure Time.
		/// </summary>
		/// <returns>The Measure Time equivalent of <paramref name="sampleTime"/>.</returns>
		/// <param name="sampleTime">The time in samples.</param>
		public double GetMeasureTimeFromSampleTime(int sampleTime)
		{
			double measureTime = 0d;
			
			int destTempoSectionIdx = GetTempoSectionIndexForSample(sampleTime);

			TempoSectionDef curSection = mTempoSections[0];
			
			for (int i = 1; i <= destTempoSectionIdx; ++i)
			{
				TempoSectionDef nextSection = mTempoSections[i];

				measureTime += curSection.GetMeasureTimeFromSampleTime(nextSection.StartSample);

				if (nextSection.DoesStartNewMeasure)
				{
					// Next section restarts the measure.  Special handling for that case!

					// If this section *does not* begin on a measure boundary we must reset it.  Skipping
					//  this increment stops us from having TWO separate measure numbers equate to a single
					//  sample position.
					double remainder = measureTime % 1d;
					double sampleDiff = 1d / curSection.SamplesPerMeasure;
					if (remainder >= sampleDiff && (1d - remainder) >= sampleDiff)
					{
						measureTime = System.Math.Floor(measureTime) + 1d;
					}
				}

				curSection = nextSection;
			}
			
			return measureTime + curSection.GetMeasureTimeFromSampleTime(sampleTime);
		}

		/// <summary>
		/// Converts the <paramref name="measureTime"/> from Measure Time into Sample Time.
		/// </summary>
		/// <returns>The Sample Time equivalent of <paramref name="measureTime"/>.</returns>
		/// <param name="measureTime">The time in measures.</param>
		public int GetSampleTimeFromMeasureTime(double measureTime)
		{
			int nextSectionIdx = 1;
			int numSections = GetNumTempoSections();

			double measuresSoFar = 0d;

			TempoSectionDef curSection = mTempoSections[0];

			while (nextSectionIdx < numSections)
			{
				TempoSectionDef nextSection = mTempoSections[nextSectionIdx];

				double totalMeasuresInSection = curSection.GetMeasureTimeFromSampleTime(nextSection.StartSample);

				if (measureTime <= measuresSoFar + totalMeasuresInSection)
				{
					// We are within the current section!
					break;
				}
				else
				{
					// Target measure is beyond the current section!
					measuresSoFar += totalMeasuresInSection;

					if (nextSection.DoesStartNewMeasure)
					{
						// Next section restarts the measure.  Special handling for that case!

						// Only do extra processing if this section *does not* fall on a measure boundary.
						//  This will stop us from treating the same location as equivalent for two
						//  separate measure numbers.
						double remainder = measuresSoFar % 1d;
						double sampleDiff = 1d / curSection.SamplesPerMeasure;
						if (remainder >= sampleDiff && (1d - remainder) >= sampleDiff)
						{
							// First we must fill up to the next measure.
							double adjustTime = (System.Math.Floor(measuresSoFar) + 1d) - measuresSoFar;

							// This is a special early break!  This actually puts us at the *next* section!
							//  We can break here to save on some work, but we must be careful to iterate
							//  the sections!
							if (measureTime <= measuresSoFar + adjustTime)
							{
								// We're in the "void" space between the last beat of the previous section and the first
								//  of the next.
								measuresSoFar = measureTime;
								curSection = nextSection;
								break;
							}
							else
							{
								// Fast forward us to the next section measure start time!
								measuresSoFar += adjustTime;
							}
						}
					}
				}

				// Iterate section.
				curSection = nextSection;
				nextSectionIdx++;
			}

			// Subtract out the consumed time.
			measureTime -= measuresSoFar;
			
			return curSection.StartSample + (int)(measureTime * (double)(curSection.BeatsPerMeasure * curSection.SamplesPerBeat));
		}

		/// <summary>
		/// Converts the <paramref name="measure"/> from Measure Time into Sample Time.
		/// </summary>
		/// <returns>The Sample Time equivalent of <paramref name="measure"/>.</returns>
		/// <param name="measure">The time in measures.</param>
		public int GetSampleTimeFromMeasureTime(int measure)
		{
			return GetSampleTimeFromMeasureTime((double)measure);
		}

		/// <summary>
		/// Gets the Beat Time within the Measure represented by <paramref name="sampleTime"/>.
		/// </summary>
		/// <returns>The Beat Time within the Measure represented by <paramref name="sampleTime"/>.</returns>
		/// <param name="sampleTime">The time in samples to convert.</param>
		public double GetBeatCountInMeasureFromSampleTime(int sampleTime)
		{
			// Get the measure time for the current sample position.
			double measureTime = GetMeasureTimeFromSampleTime(sampleTime);

			// Take the remainder of the measureTime and multiply that by the beats in the measure definition.
			return (measureTime % 1d) * GetTempoSectionForSample(sampleTime).BeatsPerMeasure;
		}

		/// <summary>
		/// Returns the number of samples within a beat at the location specified by
		/// <paramref name="checkSample"/>, with the beat value specified by <paramref name="subdivisions"/>.
		/// </summary>
		/// <returns>The number of samples within a 'beat' at the location specified by
		/// <paramref name="checkSample"/>.</returns>
		/// <param name="checkSample">The sample location within the audio to check.</param>
		/// <param name="subdivisions">The number of subdivisions of a standard Beat that
		/// determine the Beat.</param>
		public double GetSamplesPerBeat(int checkSample, int subdivisions = 1)
		{
			TempoSectionDef sectionForSample = GetTempoSectionForSample(checkSample);
			return sectionForSample.GetSamplesPerBeatSection(subdivisions);
		}

		/// <summary>
		/// Returns the Beats Per Minute at the location specified by <paramref name="checkSample"/>.
		/// </summary>
		/// <returns>The Beats Per Minute at the location specified by 
		/// <paramref name="checkSample"/>.</returns>
		/// <param name="checkSample">The sample location within the audio to check.</param>
		public double GetBPM(int checkSample)
		{
			TempoSectionDef sectionForSample = GetTempoSectionForSample(checkSample);
			return sectionForSample.GetBPM(mSampleRate);
		}

		/// <summary>
		/// Gets the sample location of the nearest beat to <paramref name="checkSample"/>.
		/// </summary>
		/// <returns>The sample of the nearest beat to <paramref name="checkSample"/>.</returns>
		/// <param name="checkSample">The sample location to check.</param>
		/// <param name="subdivisions">The number of subdivisions of a standard Beat that
		/// determine the Beat.</param>
		public int GetSampleOfNearestBeat(int checkSample, int subdivisions = 1)
		{
			double beatTime = GetBeatTimeFromSampleTime(checkSample, subdivisions);
			return GetSampleTimeFromBeatTime(System.Math.Round(beatTime), subdivisions);
		}

		/// <summary>
		/// Gets the Tempo Section that contains <paramref name="checkSample"/>.
		/// </summary>
		/// <returns>The Tempo Section that contains <paramref name="checkSample"/>,
		/// <c>null</c> otherwise.</returns>
		/// <param name="checkSample">The sample location to retrieve.</param>
		public TempoSectionDef GetTempoSectionForSample(int checkSample)
		{
			int sectionIdx = GetTempoSectionIndexForSample(checkSample);
			
			return (sectionIdx >= 0) ? mTempoSections[sectionIdx] : null;
		}

		/// <summary>
		/// Gets the index of the Tempo Section that contains <paramref name="checkSample"/>
		/// within the Tempo Section list.
		/// </summary>
		/// <returns>The index of the Tempo Section that contains <paramref name="checkSample"/>;
		/// <c>-1</c> otherwise.</returns>
		/// <param name="checkSample">The sample location to retrieve.</param>
		public int GetTempoSectionIndexForSample(int checkSample)
		{
			int sectionIdx = -1;
			
			if (checkSample >= 0)
			{
				if (mTempoSections.Count == 1)
				{
					sectionIdx = 0;
				}
				else
				{
					int i = 0;
					while (i < mTempoSections.Count &&
					       checkSample >= mTempoSections[i].StartSample)
					{
						++i;
					}
					
					// We're in the previous entry.
					sectionIdx = i - 1;
				}
			}
			
			return sectionIdx;
		}
		
		#endregion
		#region Event Registration/Triggering

		/// <summary>
		/// Part of the Event Triggering process.  This will trigger all Koreography
		/// Events that fall within the range specified by <paramref name="startTime"/>
		/// and <paramref name="endTime"/> across all configured Koreography Tracks,
		/// inclusive.
		/// </summary>
		/// <param name="startTime">The start time in samples of the range to trigger.</param>
		/// <param name="endTime">The end time in samples of the range to trigger.</param>
		/// <param name="deltaSlice">The update timing information to pass to events in
		/// callbacks with time.</param>
		public void UpdateTrackTime(int startTime, int endTime, DeltaSlice deltaSlice)
		{
			lastUpdateStart = startTime;
			lastUpdateEnd = endTime;
			
			for (int i = 0; i < mTracks.Count; ++i)
			{
				mTracks[i].CheckForEvents(startTime, endTime, deltaSlice);
			}
		}
		
		internal void RegisterForEvents(string eventDef, KoreographyEventCallback callback)
		{
			KoreographyTrackBase koreoTrack = GetTrackByID(eventDef);
			
			if (koreoTrack != null)
			{
				koreoTrack.RegisterForEvents(callback);
			}
			else
			{
				Debug.LogWarning("WARNING: no Koreography Track with event definition '" + eventDef + "' to register with.");
			}
		}
		
		internal void RegisterForEventsWithTime(string eventDef, KoreographyEventCallbackWithTime callback)
		{
			KoreographyTrackBase koreoTrack = GetTrackByID(eventDef);
			
			if (koreoTrack != null)
			{
				koreoTrack.RegisterForEventsWithTime(callback);
			}
			else
			{
				Debug.LogWarning("WARNING: no Koreography Track with event definition '" + eventDef + "' to register with.");
			}
		}
		
		internal void UnregisterForEvents(string eventDef, KoreographyEventCallback callback)
		{
			KoreographyTrackBase koreoTrack = GetTrackByID(eventDef);
			
			if (koreoTrack != null)
			{
				koreoTrack.UnregisterForEvents(callback);
			}
			else
			{
				Debug.LogWarning("WARNING: no Koreography Track with event definition '" + eventDef + "' to unregister from.");
			}
		}
		
		internal void UnregisterForEventsWithTime(string eventDef, KoreographyEventCallbackWithTime callback)
		{
			KoreographyTrackBase koreoTrack = GetTrackByID(eventDef);
			
			if (koreoTrack != null)
			{
				koreoTrack.UnregisterForEventsWithTime(callback);
			}
			else
			{
				Debug.LogWarning("WARNING: no Koreography Track with event definition '" + eventDef + "' to unregister from.");
			}
		}
		
		internal void ClearEventRegister()
		{
			for (int i = 0; i < mTracks.Count; ++i)
			{
				mTracks[i].ClearEventRegister();
			}
		}
		
		#endregion
	}

	/// <summary>
	/// An object that stores metadata necessary to properly define the tempo
	/// for a part of a song.
	/// </summary>
	[System.Serializable]
	public class TempoSectionDef
	{
		#region Fields

		[SerializeField]
		[Tooltip("The name of the Tempo Section.")]
		string sectionName = "New Section";		// Generally intended for Editor use only!

		[SerializeField]
		[Tooltip("The sample position at which this Tempo Section begins.")]
		int startSample = 0;

		[SerializeField]
		[Tooltip("The number of samples in a beat for this Tempo Section.")]
		double samplesPerBeat = 22050d;			// Defaults to 120bpm for songs at 44100 samples/second.

		[SerializeField]
		[Tooltip("The number of beats in a measure for this Tempo Section.")]
		int beatsPerMeasure = 4;

		[SerializeField]
		[Tooltip("Whether or not this section forces the beginning of a new measure.")]
		bool bStartNewMeasure = true;

		#endregion
		#region Properties

		/// <summary>
		/// Gets or sets the name of the Tempo Section.
		/// </summary>
		/// <value>The name of the Tempo Section.</value>
		public string SectionName
		{
			get
			{
				return sectionName;
			}
			set
			{
				sectionName = value;
			}
		}

		/// <summary>
		/// Gets or sets the Start Sample position.  This is guaranteed to be
		/// non-negative.
		/// </summary>
		/// <value>The start sample.</value>
		public int StartSample
		{
			get
			{
				return startSample;
			}
			set
			{
				// Disallow non-negative startSamples.
				if (value < 0)
				{
					startSample = 0;
				}
				else
				{
					startSample = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets the number of samples in a beat for this Tempo
		/// Section.  Guaranteed to be greater than zero.
		/// </summary>
		/// <value>The samples per beat.</value>
		public double SamplesPerBeat
		{
			get
			{
				return samplesPerBeat;
			}
			set
			{
				if (value <= 0d)
				{
					samplesPerBeat = 1d;
				}
				else
				{
					samplesPerBeat = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets the Beats Per Measure for this Tempo Section.
		/// Guaranteed to be greater than zero.
		/// </summary>
		/// <value>The beats per measure.</value>
		public int BeatsPerMeasure
		{
			get
			{
				return beatsPerMeasure;
			}
			set
			{
				if (value <= 0)
				{
					beatsPerMeasure = 1;
				}
				else
				{
					beatsPerMeasure = value;
				}
			}
		}

		/// <summary>
		/// Gets the Samples Per Measure for this Tempo Section.
		/// Guaranteed to be greater than zero.
		/// </summary>
		/// <value>The samples per measure.</value>
		public double SamplesPerMeasure
		{
			get
			{
				return samplesPerBeat * beatsPerMeasure;
			}
		}

		/// <summary>
		/// Gets or sets the flag indicating whether this Tempo Section forces the
		/// start of a new measure in the beat timeline.
		/// </summary>
		/// <value><c>true</c> if does reset beat count; otherwise, <c>false</c>.</value>
		public bool DoesStartNewMeasure
		{
			get
			{
				return bStartNewMeasure;
			}
			set
			{
				bStartNewMeasure = value;
			}
		}

		#endregion
		#region Static Methods
		
		/// <summary>
		/// Compares Tempo Sections by their Start Sample.  This is generally used by
		/// sorting functions.
		/// </summary>
		/// <returns><c>-1</c> if the Start Sample of <paramref name="first"/> is lower than
		/// that of <paramref name="second"/>, <c>0</c> if they are equal, and <c>1</c>
		/// otherwise.</returns>
		/// <param name="first">The first Tempo Section to compare.</param>
		/// <param name="second">The second Tempo Section to compare.</param>
		public static int CompareByStartSample(TempoSectionDef first, TempoSectionDef second)
		{
			if (first.StartSample < second.StartSample)
			{
				return -1;
			}
			else if (first.StartSample == second.StartSample)
			{
				return 0;
			}
			else
			{
				return 1;
			}
		}
	
		#endregion
		#region General Methods

		/// <summary>
		/// Gets the Samples Per Beat of the Tempo Section, given the beat value
		/// specified by <paramref name="subdivisions"/>.
		/// </summary>
		/// <returns>The Samples Per Beat of the Tempo Section, given the beat value
		/// specified by <paramref name="subdivisions"/>.</returns>
		/// <param name="subdivisions">The number of subdivisions of a standard Beat that
		/// determine the Beat.</param>
		public double GetSamplesPerBeatSection(int subdivisions = 1)
		{
			return SamplesPerBeat / (double)(subdivisions);
		}

		/// <summary>
		/// Gets the amount of beat time this section contains at the specified
		/// <paramref name="sampleTime"/> given the beat value specified by
		/// <paramref name="subdivisions"/>.
		/// </summary>
		/// <returns>The amount of beat time in this section at <paramref name="sampleTime"/>.</returns>
		/// <param name="sampleTime">The time in samples to convert to beat time.</param>
		/// <param name="subdivisions">The number of subdivisions of a standard Beat that
		/// determine the Beat.</param>
		public double GetBeatTimeFromSampleTime(int sampleTime, int subdivisions = 1)
		{
			double beatTime = 0d;

			if (sampleTime > startSample)
			{
				beatTime = (double)(sampleTime - startSample) / GetSamplesPerBeatSection(subdivisions);
			}
		
			return beatTime;
		}

		/// <summary>
		/// Gets the amount of measure time this section contains at the specified
		/// <paramref name="sampleTime"/>.
		/// </summary>
		/// <returns>The amount of measure time in this section at <paramref name="sampleTime"/>.  
		/// This value is 0-indexed!</returns>
		/// <param name="sampleTime">The time in samples to convert to measure time.</param>
		public double GetMeasureTimeFromSampleTime(int sampleTime)
		{
			return GetBeatTimeFromSampleTime(sampleTime) / (double)BeatsPerMeasure;
		}

		/// <summary>
		/// Gets the tempo of this section described in Beats Per Minute.
		/// </summary>
		/// <returns>The Beats Per Minute of this tempo section.</returns>
		/// <param name="sampleRate">The Sample Rate for the data.</param>
		public double GetBPM(int sampleRate)
		{
			return (double)sampleRate / samplesPerBeat * 60d;
		}

		#endregion
	}
}
