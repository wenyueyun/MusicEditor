//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;
using System.Collections.Generic;

namespace SonicBloom.Koreo.Players
{
	/// <summary>
	/// This class collects several audio layers, or stems, into one logical "piece" of
	/// audio.  The properties revealed by the class are all based on the base layer; 
	/// the <c>AudioLayer</c> at position 0 in the Audio Layers list.
	/// </summary>
	[System.Serializable]
	public class AudioGroup
	{
		[SerializeField]
		[Tooltip("The AudioLayers that make up this group.")]
		List<AudioLayer> audioLayers = new List<AudioLayer>();

		// TODO: Single Group TimeLine - This to allow timed, layer segments, rather than "play until the end."
		//  Though this may not be necessary: "silence" can be added by simply skipping the "data fill" in the
		//  AddData function at the Layer level.

		/// <summary>
		/// Gets the total sample time of the <c>AudioGroup</c>.
		/// </summary>
		/// <value>The total sample time.</value>
		public int TotalSampleTime
		{
			get
			{
				int val = 0;
				if (audioLayers != null && audioLayers.Count > 0)
				{
					val = audioLayers[0].TotalSampleTime;
				}
				return val;
			}
		}

		/// <summary>
		/// Gets the number of audio channels used by the <c>AudioGroup</c>.
		/// </summary>
		/// <value>The number of audio channels.</value>
		public int Channels
		{
			get
			{
				int val = 0;
				if (audioLayers != null && audioLayers.Count > 0)
				{
					val = audioLayers[0].Channels;
				}
				return val;
			}
		}

		/// <summary>
		/// Gets the frequency of the audio data (sample rate).
		/// </summary>
		/// <value>The frequency of the audio data.</value>
		public int Frequency
		{
			get
			{
				int val = 0;
				if (audioLayers != null && audioLayers.Count > 0)
				{
					val = audioLayers[0].Frequency;
				}
				return val;
			}
		}

		/// <summary>
		/// Gets the number <c>AudioLayer</c> layers.
		/// </summary>
		/// <value>The number layers.</value>
		public int NumLayers
		{
			get
			{
				return (audioLayers == null) ? 0 : audioLayers.Count;
			}
		}

		/// <summary>
		/// Initializes the audio data of all <c>AudioLayer</c>s.
		/// </summary>
		public void InitLayerData()
		{
			for (int i = 0; i < audioLayers.Count; ++i)
			{
				audioLayers[i].InitData();
			}
		}

		/// <summary>
		/// Clears the audio data prepped by all <c>AudioLayer</c>s.
		/// </summary>
		public void ClearLayerData()
		{
			for (int i = 0; i < audioLayers.Count; ++i)
			{
				audioLayers[i].ClearData();
			}
		}

		/// <summary>
		/// Registers any Koreography configured within <c>AudioLayer</c>s.
		/// </summary>
		public void RegisterKoreography()
		{
			for (int i = 0; i < audioLayers.Count; ++i)
			{
				Koreographer.Instance.LoadKoreography(audioLayers[i].Koreo);
			}
		}

		/// <summary>
		/// Unregisters any Koreography configured within <c>AudioLayer</c>s.
		/// </summary>
		public void UnregisterKoreography()
		{
			for (int i = 0; i < audioLayers.Count; ++i)
			{
				Koreographer.Instance.UnloadKoreography(audioLayers[i].Koreo);
			}
		}

		/// <summary>
		/// Determines whether any Koreography configured within <c>AudioLayer</c>s is
		/// loaded.
		/// </summary>
		/// <returns><c>true</c> if Koreography is registered; otherwise, <c>false</c>.</returns>
		public bool IsKoreographyRegistered()
		{
			bool bIsLoaded = true;

			for (int i = 0; i < audioLayers.Count; ++i)
			{
				if (!Koreographer.Instance.IsKoreographyLoaded(audioLayers[i].Koreo))
				{
					bIsLoaded = false;
					break;
				}
			}

			return bIsLoaded;
		}

		/// <summary>
		/// Determines whether all layers are ready for playback.
		/// </summary>
		/// <returns><c>true</c> if all layers are ready for playback; otherwise, <c>false</c>.</returns>
		public bool IsReady()
		{
			bool bReady = true;
			for (int i = 0; i < audioLayers.Count; ++i)
			{
				if (!audioLayers[i].IsReady())
				{
					bReady = false;
					break;
				}
			}
			return bReady;
		}

		/// <summary>
		/// Determines whether or not this <c>AudioGroup</c> has any configured layers.
		/// </summary>
		/// <returns><c>true</c> if this <c>AudioGroup</c> contains at least one layer;
		/// otherwise, <c>false</c>.</returns>
		public bool IsEmpty()
		{
			return (audioLayers.Count == 0);
		}

		/// <summary>
		/// Checks to see whether the <c>AudioClip</c> with name <paramref name="clipName"/> 
		/// is used in any layer.
		/// </summary>
		/// <returns><c>true</c>, if <paramref name="clip"/> was used in a layer,
		/// <c>false</c> otherwise.</returns>
		/// <param name="clipName">The name of the <c>AudioClip</c> to check.</param>
		public bool ContainsClip(string clipName)
		{
			bool bContains = false;
			if (!string.IsNullOrEmpty(clipName))
			{
				for (int i = 0; i < audioLayers.Count; ++i)
				{
					if (audioLayers[i].ClipName == clipName)
					{
						bContains = true;
						break;
					}
				}
			}
			return bContains;
		}

		/// <summary>
		/// Gets the <c>AudioClip</c> at position <c>0</c> in the <c>AudioLayer</c> list.
		/// </summary>
		/// <returns>The base clip.</returns>
		public AudioClip GetBaseClip()
		{
			AudioClip baseClip = null;

			if (audioLayers != null && audioLayers.Count > 0)
			{
				baseClip = audioLayers[0].Clip;
			}

			return baseClip;
		}

		/// <summary>
		/// Gets the name of the <c>AudioClip</c> at position <c>0</c> in the <c>AudioLayer</c> list.
		/// </summary>
		/// <returns>The name of the base clip.</returns>
		public string GetBaseClipName()
		{
			string baseClipName = string.Empty;

			if (audioLayers != null && audioLayers.Count > 0)
			{
				baseClipName = audioLayers[0].ClipName;
			}

			return baseClipName;
		}

		/// <summary>
		/// Gets the the <c>AudioClip</c>, if any, of the <c>AudioLayer</c> at index
		/// <paramref name="layerIdx"/>.
		/// </summary>
		/// <returns>The <c>AudioClip</c> at layer <paramref name="layerIdx"/>.</returns>
		/// <param name="layerIdx">Layer index.</param>
		public AudioClip GetClipAtLayer(int layerIdx)
		{
			AudioClip clip = null;

			if (audioLayers != null && layerIdx < audioLayers.Count)
			{
				clip = audioLayers[layerIdx].Clip;
			}

			return clip;
		}

		/// <summary>
		/// Fills <paramref name="data"/> with audio data compiled across all <c>AudioLayer</c>s.
		/// </summary>
		/// <param name="sampleTime">The position in the audio from which to begin reading.</param>
		/// <param name="data">The audio data array to fill.</param>
		/// <param name="dataOffset">The position in <paramref name="data"/> to begin filling from.</param>
		/// <param name="amountToRead">The number of samples to read.</param>
		public void GetAudioData(int sampleTime, float[] data, int dataOffset, int amountToRead)
		{
			// We report the length of data of the group based on the base layer.  This should be okay.
			audioLayers[0].ReadLayerAudioData(sampleTime, data, dataOffset, amountToRead);

			for (int i = 1; i < audioLayers.Count; ++i)
			{
				int layerOffset = sampleTime * audioLayers[i].Channels;	// How far in data (samples * channels) we start reading.

				// Sub-layers can potentially be shorter than the base layer.  Protect against this.
				if (audioLayers[i].TotalDataLength > layerOffset + amountToRead)
				{
					audioLayers[i].ReadLayerAudioData(sampleTime, data, dataOffset, amountToRead, true);
				}
				else
				{
					// Calculate the amount we have left to feed into the data array.
					//  The "+1" is to compensate for 0-based indexing value vs 1-based magnitude.
					int amountLeft = audioLayers[i].TotalDataLength - (layerOffset + 1);

					// Verify if we're being asked for the final batch or for a position beyond our end.
					if (amountLeft > 0)
					{
						// Fill up to the end of the track.
						audioLayers[i].ReadLayerAudioData(sampleTime, data, dataOffset, amountLeft, true);
					}
				}
			}
		}
	}
}
