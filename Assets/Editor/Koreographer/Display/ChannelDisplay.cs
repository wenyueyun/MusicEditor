//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;

namespace SonicBloom.Koreo.EditorUI
{
	[System.Serializable]
	internal class ChannelDisplay
	{
		#region Inner Classes
		
		struct WaveformCacheEntry
		{
			public float rms;
			public float min;
			public float max;
		}
		
		#endregion
		#region Constants
		
		const float channelAmplitudePercent = 0.9f;
		
		#endregion
		#region Fields
		
		float[] sampleData = new float[0];
		
		int cachedSamplesPerPack = 0;
		
		[System.NonSerialized]	// This array is far too big to cache quickly.  Just don't.
		WaveformCacheEntry[] cachedData = new WaveformCacheEntry[0];

		[System.NonSerialized]
		int cacheRangeMin = -1;	// Don't save cache info.
		[System.NonSerialized]
		int cacheRangeMax = -1;	// Don't save cache info.
		
		#endregion
		#region Methods
		
		public ChannelDisplay(float[] inSamples)
		{
			sampleData = inSamples;
			
			InitCachedData(sampleData.Length);
		}
		
		void InitCachedData(float totalSampleCount)
		{
			// Set up the cache.  Reserve the maximum required size [ceil(sampleCount/2)].
			cachedData = new WaveformCacheEntry[Mathf.CeilToInt(totalSampleCount / 2f)];
		}

		void ResetCache(int newSamplesPerPack)
		{
			// No need to reset values.  The cacheRangeMin/Max values define the valid range.
			//  As such, we just ignore old values left over in the array.
			cachedSamplesPerPack = newSamplesPerPack;

			cacheRangeMin = -1;
			cacheRangeMax = -1;
		}
		
		public void Draw(Rect displayRect, WaveDisplayState displayState)
		{
			if (cachedData == null)
			{
				InitCachedData(sampleData.Length);
			}
			
			if (displayState.samplesPerPack > 1 &&
			    displayState.samplesPerPack != cachedSamplesPerPack)
			{
				ResetCache(displayState.samplesPerPack);
			}
			
			// Draw lines!
			GLDrawing.BeginLineDrawing();
			{
				if (displayState.samplesPerPack == 1)
				{
					DrawWaves(displayRect, displayState);
				}
				else
				{
					EnsureEntriesCached(displayRect, displayState);

					switch (displayState.displayType)
					{
					case WaveDisplayType.MinMax:
						DrawMinMax(displayRect, displayState);
						break;
					case WaveDisplayType.RMS:
						DrawRMS(displayRect, displayState);
						break;
					case WaveDisplayType.Both:
						DrawMinMax(displayRect, displayState);
						DrawRMS(displayRect, displayState);
						break;
					}
				}
			}
			GLDrawing.EndLineDrawing();
		}

		void EnsureEntriesCached(Rect displayRect, WaveDisplayState displayState)
		{
			int samplesPerPack = displayState.samplesPerPack;	// Copy to the stack.

			// Check if the range is within our cached range.
			int drawRangeMin = displayState.GetFirstVisiblePack();

			// These are actually indices (thus the - 1).
			int maxRequested = drawRangeMin + (int)displayRect.width;
			int maxAvailable = Mathf.FloorToInt(sampleData.Length / samplesPerPack) - 1;
			
			int drawRangeMax = Mathf.Min(maxRequested, maxAvailable);

			// Determine what ranges need to be recalculated.
			// Before, after, all.

			int calcMin = drawRangeMin;
			int calcMax = drawRangeMax;

			// Check if recalculation is needed at all.
			if (cacheRangeMin > drawRangeMin ||
			    cacheRangeMax < drawRangeMax)
			{
				if (cacheRangeMax < 0)
				{
					// Recalc all. [drawMin,drawMax]
					cacheRangeMin = drawRangeMin;
					cacheRangeMax = drawRangeMax;

					CalculateEntryRange(calcMin, calcMax, samplesPerPack, maxAvailable);
				}
				else
				{
					if (drawRangeMax < cacheRangeMin ||
				        drawRangeMin < cacheRangeMin)
					{
						// Recalc earlier. [drawMin,cacheRangeMin - 1]
						calcMin = drawRangeMin;
						calcMax = cacheRangeMin - 1;

						cacheRangeMin = drawRangeMin;

						CalculateEntryRange(calcMin, calcMax, samplesPerPack, maxAvailable);
					}

					if (drawRangeMin > cacheRangeMax ||
					    drawRangeMax > cacheRangeMax)
					{
						// Recalc later. [cacheRangeMax + 1, drawRangeMax]
						calcMin = cacheRangeMax + 1;
						calcMax = drawRangeMax;

						cacheRangeMax = drawRangeMax;

						CalculateEntryRange(calcMin, calcMax, samplesPerPack, maxAvailable);
					}
				}
			}
		}

		// Runs through the specified range and calculates cache entries.
		void CalculateEntryRange(int startIdx, int endIdx, int samplesPerPack, int totalFullPacks)
		{
			int samplePackPos = startIdx * samplesPerPack;

			// Calculate the vast majority of these in a tight loop.
			for (int i = startIdx; i <= endIdx; ++i)
			{
				CalculateEntry(i, samplePackPos, samplesPerPack);
				
				samplePackPos += samplesPerPack;
			}

			// Support the final pack.  Only attempt this if we're calculating the previously
			//  "final" pack.
			int finalPackSamps = sampleData.Length % samplesPerPack;
			if (endIdx == totalFullPacks &&
			    finalPackSamps != 0)
			{
				// Recalculate the final pack.
				//  samplePackPos is at the right spot now!
				CalculateEntry(endIdx + 1, samplePackPos, finalPackSamps);
				
				cacheRangeMax += 1;
			}
		}

		// Calculates a single cache entry.  Inline tests showed little to no improvements in performance.
		void CalculateEntry(int entryIdx, int sampleStartIdx, int count)
		{
			float peak = 0;

			float minSample = 1f;
			float maxSample = -1f;

			int sampleEndIdx = sampleStartIdx + count;	// Non-inclusive.

			for (int curSampleIdx = sampleStartIdx; curSampleIdx < sampleEndIdx; ++curSampleIdx)
			{
				float curSample = sampleData[curSampleIdx];

				// RMS
				peak += curSample * curSample;

				// MinMax
				minSample = Mathf.Min(minSample, curSample);
				maxSample = Mathf.Max(maxSample, curSample);
			}

			cachedData[entryIdx].rms = Mathf.Sqrt(peak / count);
			cachedData[entryIdx].min = minSample;
			cachedData[entryIdx].max = maxSample;
		}
		
		void DrawWaves(Rect waveArea, WaveDisplayState displayState)
		{
			GL.Color(KoreographerColors.WaveformFG);

			// In this case, we don't have to incur the extra multiplication by using GetFirstVisiblePackSample
			//  because we are already at 1-sample-per-pack!
			int startSample = displayState.GetFirstVisiblePack();
			int amplitude = (int)(channelAmplitudePercent * (waveArea.height / 2f));
			
			Vector3 startPoint = Vector3.zero;
			Vector3 endPoint = Vector3.zero;

			// Cache Rect Property values so that we don't constantly recalculate them.
			float centerY = waveArea.center.y;
			float startX = waveArea.x;
			float width = waveArea.width;

#if UNITY_EDITOR && UNITY_5_4_OR_NEWER
			float incVal = 1f / UnityEditor.EditorGUIUtility.pixelsPerPoint;
#else
			float incVal = 1f;
#endif
			float curStep = 0f;

			// Outer loop allows us to duplicate lines such that we fill empty spaces with
			//  the GL.LINES rendering and Retina-or-equivalent contexts.
			while (curStep < 1f)
			{
				float lastY = centerY + (sampleData[startSample] * amplitude);

				for (int i = 1; i < width && i + startSample < sampleData.Length; ++i)
				{
					endPoint.x = startX + i;
					startPoint.x = endPoint.x - 1;	// Back us up by one!
					
					// Get y's for left channel.
					startPoint.y = lastY;
					// Subtract because positive is down!
					endPoint.y = centerY - (sampleData[startSample + i] * amplitude);

					GL.Vertex(startPoint);
					GL.Vertex(endPoint);
					
					// Store previous y for next time!
					lastY = endPoint.y;
				}

				// Increment the draw offset and counter.
				startX += incVal;
				curStep += incVal;
			}
		}

		void DrawMinMax(Rect waveArea, WaveDisplayState displayState)
		{
			GL.Color(KoreographerColors.WaveformBG);

			int amplitude = (int)(channelAmplitudePercent * (waveArea.height / 2f));
			
			Vector3 startPoint = Vector3.zero;
			Vector3 endPoint = Vector3.zero;

			// Cache Rect Property values so that we don't constantly recalculate them.
			float centerY = waveArea.center.y;
			float startX = waveArea.x;
			float width = waveArea.width;

			int firstDrawEntry = displayState.GetFirstVisiblePack();
			int lastDrawEntry = Mathf.Min(firstDrawEntry + (int)width, cacheRangeMax);

#if UNITY_EDITOR && UNITY_5_4_OR_NEWER
			float incVal = 1f / UnityEditor.EditorGUIUtility.pixelsPerPoint;
#else
			float incVal = 1f;
#endif
			float curStep = 0f;

			// Outer loop allows us to duplicate lines such that we fill empty spaces with
			//  the GL.LINES rendering and Retina-or-equivalent contexts.
			while (curStep < 1f)
			{
				// Set up for the drawing loop.
				int xPos = 0;
				int drawEntry = firstDrawEntry;

				while (drawEntry <= lastDrawEntry)
				{
					WaveformCacheEntry entry = cachedData[drawEntry];
					// Draw a vertical line.
					startPoint.x = startX + xPos;
					endPoint.x = startPoint.x;

					// Subtract because positive is down!
					startPoint.y = centerY - (entry.max * amplitude);
					endPoint.y = centerY - (entry.min * amplitude);

					GL.Vertex(startPoint);
					GL.Vertex(endPoint);

					xPos += 1;
					drawEntry += 1;
				}

				// Increment the draw offset and counter.
				startX += incVal;
				curStep += incVal;
			}
		}

		void DrawRMS(Rect waveArea, WaveDisplayState displayState)
		{
			GL.Color(KoreographerColors.WaveformFG);
			
			int amplitude = (int)(channelAmplitudePercent * (waveArea.height / 2f));
			
			Vector3 startPoint = Vector3.zero;
			Vector3 endPoint = Vector3.zero;

			// Cache Rect Property values so that we don't constantly recalculate them.
			float centerY = waveArea.center.y;
			float startX = waveArea.x;
			float width = waveArea.width;

			int firstDrawEntry = displayState.GetFirstVisiblePack();
			int lastDrawEntry = Mathf.Min(firstDrawEntry + (int)width, cacheRangeMax);

#if UNITY_EDITOR && UNITY_5_4_OR_NEWER
			float incVal = 1f / UnityEditor.EditorGUIUtility.pixelsPerPoint;
#else
			float incVal = 1f;
#endif
			float curStep = 0f;

			// Outer loop allows us to duplicate lines such that we fill empty spaces with
			//  the GL.LINES rendering and Retina-or-equivalent contexts.
			while (curStep < 1f)
			{
				// Set up for the drawing loop.
				int xPos = 0;
				int drawEntry = firstDrawEntry;

				while (drawEntry <= lastDrawEntry)
				{
					WaveformCacheEntry entry = cachedData[drawEntry];
					// Draw a vertical line.
					startPoint.x = startX + xPos;
					endPoint.x = startPoint.x;

					float rmsVal = entry.rms * amplitude;
					
					// Subtract because positive is down!
					startPoint.y = centerY - rmsVal;
					endPoint.y = centerY + rmsVal;
					
					GL.Vertex(startPoint);
					GL.Vertex(endPoint);

					xPos += 1;
					drawEntry += 1;
				}

				// Increment the draw offset and counter.
				startX += incVal;
				curStep += incVal;
			}
		}
		
		#endregion
	}
}
