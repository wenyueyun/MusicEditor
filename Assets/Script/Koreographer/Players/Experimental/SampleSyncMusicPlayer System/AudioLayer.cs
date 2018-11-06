//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;

namespace SonicBloom.Koreo.Players
{
	/// <summary>
	/// <para>This class is a data supplier: it logically combines Koreography with an
	/// <c>AudioClip</c> for playback purposes.</para>
	/// 
	/// <para>Note that EITHER the Koreography or the clip is sufficient to work.  This
	/// is designed to allow non-choreographed layers in the audio system.  In the
	/// case that both exist, the <c>AudioClip</c> specified by the user is
	/// preferred.  WARNING: this will cause issues with the specified Koreography
	/// if the clip is different.</para>
	/// </summary>
	// TODO: Simplify this.  No need to have an AudioClip alone.  Just create empty Koreography to wrap it?
	[System.Serializable]
	public class AudioLayer
	{
		const int MAX_ARRAY_LENGTH = 4000000;

		[SerializeField]
		[Tooltip("The Koreography that defines this layer.")]
		Koreography koreo = null;

		float[][] audioDatas = null;

		[SerializeField]
		[Tooltip("The AudioClip that defines this layer.  This will override Koreography.  If you set this, please reset the Koreography setting!")]
		AudioClip clip = null;
		string clipName = string.Empty;

		int totalSampleTime = 0;
		int channelCount = 0;
		int frequency = 0;

		[Tooltip("The volume at which this layer should be played into the mix.")]
		[Range(0f, 1f)]
		public float volume = 1f;

		/// <summary>
		/// Gets the layer's <c>AudioClip</c>.
		/// </summary>
		/// <value>The <c>AudioClip</c>.</value>
		public AudioClip Clip
		{
			get
			{
				return clip;
			}
		}

		/// <summary>
		/// Gets the layer's <c>AudioClip</c>'s name.
		/// </summary>
		/// <value>The name of the <c>AudioClip</c>.</value>
		public string ClipName
		{
			get
			{
				return clipName;
			}
		}

		/// <summary>
		/// Gets the layer's Koreography.
		/// </summary>
		/// <value>The Koreography.</value>
		public Koreography Koreo
		{
			get
			{
				return koreo;
			}
		}

		/// <summary>
		/// Gets the total sample time of the layer.
		/// </summary>
		/// <value>The total sample time.</value>
		public int TotalSampleTime
		{
			get
			{
				return totalSampleTime;
			}
		}

		/// <summary>
		/// Gets the number of audio channels.
		/// </summary>
		/// <value>The number of audio channels.</value>
		public int Channels
		{
			get
			{
				return channelCount;
			}
		}

		/// <summary>
		/// Gets the audio data sample frequency (sample rate).
		/// </summary>
		/// <value>The audio data sample frequency.</value>
		public int Frequency
		{
			get
			{
				return frequency;
			}
		}

		/// <summary>
		/// Gets or sets the volume of the layer.
		/// </summary>
		/// <value>The volume of the layer.</value>
		public float Volume
		{
			get
			{
				return volume;
			}
			set
			{
				volume = Mathf.Clamp01(value);
			}
		}
		
		/// <summary>
		/// Gets the total number of samples across all channels of audio data.
		/// </summary>
		/// <value>The total number of samples across all channels of audio data.</value>
		public int TotalDataLength
		{
			get
			{
				return totalSampleTime * channelCount;
			}
		}

		/// <summary>
		/// Initializes the audio data, reading all samples into a set
		/// of buffers.  This cannot be a single buffer as Mono's Garbage
		/// Collection fails for very large arrays, never properly freeing
		/// the memory.
		/// </summary>
		public void InitData()
		{
			if (clip == null)
			{
				clip = koreo.SourceClip;
			}
			
			// Cache the name.  This is because accessing this allocates memory.
			clipName = clip.name;

			// Store all of this off.  This is because we can ONLY access
			//  properties of the AudioClip from the Main thread.
			totalSampleTime = clip.samples;
			channelCount = clip.channels;
			frequency = clip.frequency;

			int totalDataLength = Clip.samples * Clip.channels;
		
			// Get the total number of arrays used.
			int arrayCount = (totalDataLength / MAX_ARRAY_LENGTH);
			if (totalDataLength % MAX_ARRAY_LENGTH != 0)
			{
				arrayCount++;
			}
		
			audioDatas = new float[arrayCount][];
		
			for (int i = 0; i < audioDatas.Length; ++i)
			{
				int offset = i * MAX_ARRAY_LENGTH;
				int length = MAX_ARRAY_LENGTH;
			
				if (offset + MAX_ARRAY_LENGTH > totalDataLength)
				{
					length = totalDataLength - offset;
				}
			
				// Capture data, too.
				audioDatas[i] = new float[length];
				Clip.GetData(audioDatas[i], (offset / Clip.channels));	// Offset is in sample TIME.
			}
		}

		/// <summary>
		/// Clears the audio buffers.
		/// </summary>
		public void ClearData()
		{
			audioDatas = null;
		}

		/// <summary>
		/// Determines whether this <c>AudioLayer</c> is ready for playback.
		/// </summary>
		/// <returns><c>true</c> if this <c>AudioLayer</c> is ready for playback;
		/// otherwise, <c>false</c>.</returns>
		public bool IsReady()
		{
			return (audioDatas != null);
		}

		/// <summary>
		/// Reads <paramref name="amount"/> audio data from the buffers beginning at
		/// <paramref name="sampleTime"/> into <paramref name="data"/> beginning at <paramref name="dataOffset"/>.
		/// If <paramref name="bAdditive"/> is <c>true</c>, the audio data is multiplied
		/// into <paramref name="data"/> rather than overwritten.
		/// </summary>
		/// <param name="sampleTimePos">Time in samples to begin reading from.</param>
		/// <param name="data">The array to fill with audio data.</param>
		/// <param name="dataOffset">The offset into <paramref name="data"/> at which to 
		/// begin filling.</param>
		/// <param name="amount">The amount of audio data to read into <paramref name="data"/>.</param>
		/// <param name="bAdditive">If set to <c>true</c>, samples will be multiplied (mixed)
		/// into <paramref name="data"/>; if set to <c>false</c> the samples will replace
		/// anything in <paramref name="data"/>.</param>
		public void ReadLayerAudioData(int sampleTimePos, float[] data, int dataOffset, int amount, bool bAdditive = false)
		{
			// Total combined position.
			int dataPos = sampleTimePos * channelCount;

			// Find first/last array.
			int arrayStartIdx = dataPos / MAX_ARRAY_LENGTH;
			int arrayEndIdx = (dataPos + amount) / MAX_ARRAY_LENGTH;

			// Normalize the position in the array.
			int posInSourceArray = dataPos % MAX_ARRAY_LENGTH;
			int amountLeft = amount;
		
			for (int i = arrayStartIdx; i <= arrayEndIdx; ++i)
			{
				float[] sourceArray = audioDatas[i];

				// Calculate how much to read from this array.
				int readAmount = amountLeft;
				if ((sourceArray.Length - posInSourceArray) < amountLeft)
				{
					// Read to the end of this array.
					readAmount = sourceArray.Length - posInSourceArray;
				}

				// Read that amount.
				for (int j = 0; j < readAmount; ++j)
				{
					if (bAdditive)
					{
						data[dataOffset + j] += sourceArray[posInSourceArray + j] * volume;
					}
					else
					{
						data[dataOffset + j] = sourceArray[posInSourceArray + j] * volume;
					}
				}

				// Update for the next loop.
				posInSourceArray = 0;		// Start at beginning.
				amountLeft -= readAmount;	// Reduce the amount left to read.
				dataOffset += readAmount;	// Update where we'll be reading into by the amount we've read.
			}
		}
	}
}
