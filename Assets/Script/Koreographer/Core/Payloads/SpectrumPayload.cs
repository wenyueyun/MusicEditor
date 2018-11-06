//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;
using System.Collections.Generic;

#if (!(UNITY_4_5 || UNITY_4_6 || UNITY_4_7) && UNITY_EDITOR)
using UnityEditor;
#endif

namespace SonicBloom.Koreo
{
	/// <summary>
	/// Extension Methods for the <see cref="KoreographyEvent"/> class that add
	/// <see cref="SpectrumPayload"/>-specific functionality.
	/// </summary>
	public static class SpectrumPayloadEventExtensions
	{
		#region KoreographyEvent Extension Methods
		
		/// <summary>
		/// Determines if the payload is of type <see cref="SpectrumPayload"/>.
		/// </summary>
		/// <returns><c>true</c> if the payload is of type <see cref="SpectrumPayload"/>;
		/// otherwise, <c>false</c>.</returns>
		public static bool HasSpectrumPayload(this KoreographyEvent koreoEvent)
		{
			return (koreoEvent.Payload as SpectrumPayload) != null;
		}

		/// <summary>
		/// <para>Fills the passed in <paramref name="spectrum"/> array with spectrum values.  The returned
		/// values are interpolated between recorded spectrum entries.  If the <paramref name="spectrum"/>
		/// array is null or not the correct length, it will be overwritten with a newly created array of
		/// the correct length.  The <paramref name="spectrum"/> array is not touched if the payload is
		/// not a <see cref="SpectrumPayload"/>.</para>
		/// <para>If <paramref name="maxBinCount"/> is greater than zero, the number of bins return will be
		/// clamped and the values averaged.  The averaging is equal: each bin will only affect a single returned
		/// bin.  When this clamping occurs, the number of bins in the spectrum after the process completes will
		/// likely be less than <paramref name="maxBinCount"/>.</para>
		/// </summary>
		/// <param name="koreoEvent">The 'this' <c>KoreographyEvent</c> for the extension method.</param>
		/// <param name="sampleTime">The specified sample time.</param>
		/// <param name="spectrum">An array of <c>float</c> values into which spectrum data will be added.</param>
		/// <param name="maxBinCount">The maximum number of bins to fit data into.  If this number is lower than
		/// the number of bins available, bin-averaging will occur.</param>
		public static void GetSpectrumAtTime(this KoreographyEvent koreoEvent, int sampleTime, ref float[] spectrum, int maxBinCount = 0)
		{
			SpectrumPayload pl = koreoEvent.Payload as SpectrumPayload;
			if (pl != null)
			{
				pl.GetSpectrumAtDelta(ref spectrum, koreoEvent.GetEventDeltaAtSampleTime(sampleTime), maxBinCount);
			}
		}
		
		#endregion
	}
	
	/// <summary>
	/// The SpectrumPayload class allows Koreorgraphy Events to contain a <c>Spectrum</c>
	/// series as a payload.
	/// </summary>
	[System.Serializable]
	[NoEditorCreate]
	public class SpectrumPayload : IPayload
	{
		#region Inner Classes/Structs

		[System.Serializable]
		public struct SpectrumInfo
		{
			[Tooltip("The frequency difference between consecutive bins.")]
			public float binFrequencyWidth;

			[Tooltip("The frequency of the bin at index zero.")]
			public float minBinFrequency;

			[Tooltip("The sample position of the first spectrum.")]
			public int startSample;

			[Tooltip("The sample position of the last spectrum.")]
			public int endSample;
			
			public float GetFrequencyForBin(int binIndex)
			{
				float retVal = 0f;
				
				if (binIndex > 0)
				{
					retVal = minBinFrequency + (binFrequencyWidth * binIndex);
				}
				
				return retVal;
			}
		}

		// Silly class required for serialization because Unity doesn't support serializing
		//  Lists of Lists presently.
		[System.Serializable]
		public class Spectrum
		{
			public List<float> data = new List<float>();
		}

		#endregion
		#region Fields
		
		[SerializeField]
		[Tooltip("Metadata describing the spectra.")]
		SpectrumInfo mSpectrumInfo;
		
		[SerializeField]
		[Tooltip("The spectrum values that make up the payload.")]
		List<Spectrum> mSpectrumData = new List<Spectrum>();
		
		#endregion
		#region Properties
		
		/// <summary>
		/// Gets or sets the <c>Spectrum</c> data.
		/// </summary>
		/// <value>The <c>Spectrum</c> data.</value>
		public List<Spectrum> SpectrumData
		{
			get
			{
				return mSpectrumData;
			}
			set
			{
				mSpectrumData = value;
			}
		}

		/// <summary>
		/// Gets or sets the <c>SpectrumInfo</c> struct.
		/// </summary>
		/// <value>The <c>SpectrumInfo</c> data that describes the spectra.</value>
		public SpectrumInfo SpectrumDataInfo
		{
			get
			{
				return mSpectrumInfo;
			}
			set
			{
				mSpectrumInfo = value;
			}
		}

		/// <summary>
		/// Gets the number of entries (bins) for each Spectrum.
		/// </summary>
		/// <value>The number of spectrum entries per spectrum.</value>
		public int SpectrumEntryCount
		{
			get
			{
				int count = 0;

				if (mSpectrumData != null && mSpectrumData[0] != null)
				{
					count = mSpectrumData[0].data.Count;
				}

				return count;
			}
		}
		
		#endregion
		#region Standard Methods
		
		/// <summary>
		/// This is used by the Koreography Editor to create the Payload type entry
		/// in the UI dropdown.
		/// </summary>
		/// <returns>The friendly name of the class.</returns>
		public static string GetFriendlyName()
		{
			return "Spectrum";
		}
		
		#endregion
		#region Methods

		/// <summary>
		/// <para>Fills the passed in <paramref name="spectrum"/> array with spectrum values.  The returned
		/// values are interpolated between spectrum entries based on <paramref name="delta"/>.  If the
		/// <paramref name="spectrum"/> array is null or not the correct length, it will be overwritten
		/// with a newly created array of the correct length.</para>
		/// <para>If <paramref name="maxBinCount"/> is greater than zero, the number of bins return will be
		/// clamped and the values averaged.  The averaging is equal: each bin will only affect a single returned
		/// bin.  When this clamping occurs, the number of bins in the spectrum after the process completes will
		/// likely be less than <paramref name="maxBinCount"/>.</para>
		/// </summary>
		/// <param name="spectrum">An array of <c>float</c> values into which spectrum data will be added.</param>
		/// <param name="delta">The time at which to look up in the range [0,1].</param>
		/// <param name="maxBinCount">The maximum number of bins to fit data into.  If this number is lower than
		/// the number of bins available, bin-averaging will occur.</param>
		public void GetSpectrumAtDelta(ref float[] spectrum, float delta, int maxBinCount = 0)
		{
			// COMMON PROCESSING SETUP.
			// Ensure we're in the range [0, maxIndex].
			float spectrumLoc = delta * (mSpectrumData.Count - 1);
			
			// Figure out the indices of the spectrum to interpolate between.
			int firstSpectrumIdx = Mathf.FloorToInt(spectrumLoc);
			int secondSpectrumIdx = Mathf.CeilToInt(spectrumLoc);
			
			// Grab the spectra to interpolate between.
			List<float> earlySpectrum = mSpectrumData[firstSpectrumIdx].data;
			List<float> lateSpectrum = mSpectrumData[secondSpectrumIdx].data;
			
			float innerDelta = spectrumLoc - firstSpectrumIdx;

			int rawSpectrumLength = SpectrumEntryCount;

			// SPLIT BASED ON BINS TO RETURN.
			// Make sure that the number of entries is averaged to fit within the maximum bin count.
			int numEntries = Mathf.Min(rawSpectrumLength, maxBinCount);

			if (numEntries <= 0 || numEntries == rawSpectrumLength)
			{
				// STRAIGHT COPY!
				int spectrumLength = rawSpectrumLength;
				if (spectrum == null || spectrum.Length != spectrumLength)
				{
					spectrum = new float[spectrumLength];
				}

				for (int i = 0; i < spectrumLength; ++i)
				{
					spectrum[i] = Mathf.Lerp(earlySpectrum[i], lateSpectrum[i], innerDelta);
				}
			}
			else
			{
				// AVERAGING!
				int valsPerBin = Mathf.CeilToInt((float)rawSpectrumLength / numEntries);
				float numAvgBins = (float)rawSpectrumLength / valsPerBin;
				int numTotalAverages = Mathf.CeilToInt(numAvgBins);
				int numFullAverages = Mathf.FloorToInt(numAvgBins);
				
				// Resize processing if necessary.
				if (spectrum == null || spectrum.Length != numTotalAverages)
				{
					spectrum = new float[numTotalAverages];
				}
				
				int entryPos = 0;
				int idx = 0;
				
				// Grab all the full entries.
				for (; idx < numFullAverages; ++idx, entryPos += valsPerBin)
				{
					float total = 0f;
					for (int j = 0; j < valsPerBin; ++j)
					{
						total += Mathf.Lerp(earlySpectrum[entryPos + j], lateSpectrum[entryPos + j], innerDelta);
					}
					
					spectrum[idx] = total / valsPerBin;
				}
				
				// Fill in the last non-full entry, if necessary.
				if (entryPos < rawSpectrumLength)
				{
					float total = 0f;
					int numInTotal = 0;
					for (; entryPos < rawSpectrumLength; ++entryPos, ++numInTotal)
					{
						total += Mathf.Lerp(earlySpectrum[entryPos], lateSpectrum[entryPos], innerDelta);
					}
					
					// At this point, idx should be (processedSpec.Count - 1).
					spectrum[idx] = total / numInTotal;
				}
			}
		}

		/// <summary>
		/// <para>Fills the passed in <paramref name="spectrum"/> array with spectrum values.  The returned
		/// values are interpolated between spectrum entries based on the <paramref name="sample"/> time.
		/// If the <paramref name="spectrum"/> array is null or not the correct length, it will be
		/// overwritten with a newly created array of the correct length.</para>
		/// <para>If <paramref name="maxBinCount"/> is greater than zero, the number of bins return will be
		/// clamped and the values averaged.  The averaging is equal: each bin will only affect a single returned
		/// bin.  When this clamping occurs, the number of bins in the spectrum after the process completes will
		/// likely be less than <paramref name="maxBinCount"/>.</para>
		/// </summary>
		/// <param name="spectrum">An array of <c>float</c> values into which spectrum data will be added.</param>
		/// <param name="sample">The sample position to check.</param>
		/// <param name="maxBinCount">The maximum number of bins to fit data into.  If this number is lower than
		/// the number of bins available, bin-averaging will occur.</param>
		public void GetSpectrumAtSample(ref float[] spectrum, int sample, int maxBinCount = 0)
		{
			// InverseLerp is not a problem here as we don't care about the "OneOff" case (see:
			//  KoreographyEvent.GetEventDeltaAtSampleTime).  Results are the same either way.
			GetSpectrumAtDelta(ref spectrum, Mathf.InverseLerp(mSpectrumInfo.startSample, mSpectrumInfo.endSample, sample), maxBinCount);
		}

		#endregion
		#region IPayload Interface
		
#if (!(UNITY_4_5 || UNITY_4_6 || UNITY_4_7) && UNITY_EDITOR)

		static GUIContent SpectrumContent = new GUIContent();

		/// <summary>
		/// Used for drawing the GUI in the Editor Window (possibly scene overlay?).
		/// </summary>
		/// <returns>This always returns <c>false</c> as editing SpectrumPayloads is not currently
		/// supported.</returns>
		/// <param name="displayRect">The <c>Rect</c> within which to perform GUI drawing.</param>
		/// <param name="track">The Koreography Track within which the Payload can be found.</param>
		/// <param name="isSelected">Whether or not the Payload (or the Koreography Event that
		/// contains the Payload) is selected in the GUI.</param>
		public bool DoGUI(Rect displayRect, KoreographyTrackBase track, bool isSelected)
		{
			Color originalBG = GUI.backgroundColor;
			GUI.backgroundColor = isSelected ? Color.green : Color.yellow;
			{
				GUIStyle labelSkin = GUI.skin.GetStyle("Label");
				TextAnchor originalAlign = labelSkin.alignment;
				labelSkin.alignment = TextAnchor.MiddleCenter;
				{
					SpectrumContent.text = "[Spectrum - Start: " + mSpectrumInfo.startSample + ", End: " + mSpectrumInfo.endSample + "]";
					SpectrumContent.tooltip = SpectrumContent.text;
					GUI.Box(displayRect, SpectrumContent);
				}
				labelSkin.alignment = originalAlign;
			}
			GUI.backgroundColor = originalBG;
			
			return false;
		}
		
#endif
		
		/// <summary>
		/// Returns a copy of the current object, including the pertinent parts of
		/// the payload.
		/// </summary>
		/// <returns>A copy of the Payload object.</returns>
		public IPayload GetCopy()
		{
			SpectrumPayload newPL = new SpectrumPayload();

			newPL.SpectrumDataInfo = mSpectrumInfo;
			newPL.SpectrumData = new List<Spectrum>();

			for (int i = 0; i < mSpectrumData.Count; ++i)
			{
				Spectrum spectrum = new Spectrum();
				spectrum.data.AddRange(mSpectrumData[i].data);
				newPL.SpectrumData.Add(spectrum);
			}
			
			return newPL;
		}
		
		#endregion
	}
}
