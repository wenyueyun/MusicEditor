//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace SonicBloom.Koreo.EditorUI
{
	internal enum WaveDisplayType
	{
		MinMax,
		RMS,
		Both
	}

	[System.Serializable]
	internal class WaveDisplay
	{
		#region Constants

		const int MAX_CHANNELS_TO_DRAW = 2; // WARNING! Do not edit this without reworking Draw()!

		#endregion
		#region Static Fields

		public static int pixelDistanceToPlayheadMarker = 200;

        static Color MainBGColor = new Color(0.19215f, 0.19215f, 0.19215f);
        static Color[] SectionBGColors = new Color[] { new Color(0.19215f, 0f, 0f), new Color(0f, 0.19215f, 0f), new Color(0f, 0f, 0.19215f) };

        #endregion
        #region Fields

        // Flag to tell whether this WaveDisplay is usable or not (helps with
        //  Default object showing up on Deserialize of null WaveDisplay
        //  references).
        public bool bValid = false;

		List<ChannelDisplay> channelDisplays = new List<ChannelDisplay>();
		//TrackDisplay trackDisplay = new TrackDisplay();

        List<TrackDisplay> trackDisplays = new List<TrackDisplay>();

        private int selectIdx;

        Rect waveContentRect;

		int maxSamples;

		#endregion
		#region Static Methods

		static Color GetColorForSectionAtIndex(int idx)
		{
			return WaveDisplay.SectionBGColors[idx % WaveDisplay.SectionBGColors.Length];
		}

		#endregion
		#region Methods

		public void SetAudioData(AudioClip clip)
		{
			// Clear out previous channel displays.
			channelDisplays.Clear();

			float[] rawData = new float[clip.samples * clip.channels];
			clip.GetData(rawData, 0);
		
			int numChannelsToDraw = Mathf.Min(clip.channels, MAX_CHANNELS_TO_DRAW);
			for (int i = 0; i < numChannelsToDraw; ++i)
			{
				float[] channelData = new float[clip.samples];
			
				for (int rawSampleIdx = i, channelSampleIdx = 0; rawSampleIdx < rawData.Length; rawSampleIdx += clip.channels, ++channelSampleIdx)
				{
					channelData[channelSampleIdx] = rawData[rawSampleIdx];
				}

				channelDisplays.Add(new ChannelDisplay(channelData));
			}

			maxSamples = clip.samples;
		}

		public void ClearAudioData()
		{
			channelDisplays.Clear();
		}

		public bool HasAudioData()
		{
			return (channelDisplays.Count > 0);
		}

        public void ShowAllTrack(List<KoreographyTrackBase> value,int selectIdx = 0)
        {
            this.selectIdx = selectIdx;
            for (int i = 0; i < trackDisplays.Count; i++)
            {
                trackDisplays[i].EventTrack = null;
            }
            trackDisplays.Clear();

            if (value != null)
            {
                for (int i = 0; i < value.Count; i++)
                {
                    KoreographyTrackBase eventTrack = value[i];
                    TrackDisplay trackDisplay = new TrackDisplay();
                    trackDisplay.EventTrack = eventTrack;
                    trackDisplays.Add(trackDisplay);
                }
            }
        }

		//public void SetEventTrack(KoreographyTrackBase newEventTrack)
		//{
		//	trackDisplay.EventTrack = newEventTrack;
		//}


		//public KoreographyTrackBase GetEventTrack()
		//{
		//	return trackDisplay.EventTrack;
		//}

		public void Draw(Rect displayRect, WaveDisplayState displayState, Koreography koreo, bool bShowPlayhead, List<KoreographyEvent> selectedEvents)
		{
			// Draw background.
			Color originalBG = GUI.backgroundColor;
			GUI.backgroundColor = WaveDisplay.MainBGColor;

			GUI.Box(displayRect, "", KoreographyEditorSkin.box);
		
			GUI.backgroundColor = originalBG;

			GUI.BeginGroup(displayRect);
			{
				// Calculate drawing metrics for channels.
				float left = GUI.skin.box.padding.left + 1f;
				float top = GUI.skin.box.padding.top;
				int fullWidth = GetChannelPixelWidthForWindow((int)displayRect.width);
				float height = (displayRect.height - GUI.skin.box.padding.vertical) / MAX_CHANNELS_TO_DRAW;

				// Width can be shorter than the full span of the view.  We handle the initial offset first and
				// then handle the case wherein there is less than the full view around.
				float viewOffset = (float)displayState.GetWaveformInset();
				float contentWidth = Mathf.Min(fullWidth, displayState.GetNumPacks(maxSamples) - displayState.GetFirstVisiblePack());

				Rect contentRect = new Rect(left + viewOffset, top, contentWidth - viewOffset, displayRect.height - GUI.skin.box.padding.vertical);
				// Adjust for start offset!
				Rect channelRect = new Rect(left + viewOffset, top, contentWidth - viewOffset, height);

				// Only process this drawing if we're repainting (DOES NOT USE GUI SYSTEM).
				if (Event.current.type.Equals(EventType.Repaint))
				{
					// Draw the beat markers before the actual audio content.
					DrawBeatLines(contentRect, displayState, koreo);

					// Draw Channels (waveforms)
					for (int i = 0; i < channelDisplays.Count; ++i)
					{
						channelRect.y += i * height;

						// Draw ZERO Line
						Handles.color = new Color(0f, 0f, 0f, KoreographerColors.HandleFullAlpha);
						Handles.DrawLine(new Vector3(channelRect.x, channelRect.center.y), new Vector3(channelRect.x + channelRect.width, channelRect.center.y));

						// Draw Channel Content
						channelDisplays[i].Draw(channelRect, displayState);
					}
				}

				if (channelDisplays.Count <= 0)
				{
					GUILayout.BeginArea(channelRect);
					{
						EditorGUILayout.BeginVertical();
						{
							GUILayout.FlexibleSpace();
							EditorGUILayout.BeginHorizontal();
							{
								GUILayout.FlexibleSpace();
								EditorGUILayout.HelpBox("Waveform cannot be displayed due to incompatible Load Type import setting for the selected AudioClip.", MessageType.Info);
								GUILayout.FlexibleSpace();
							}
							EditorGUILayout.EndHorizontal();
							GUILayout.FlexibleSpace();
						}
						EditorGUILayout.EndHorizontal();
					}
					GUILayout.EndArea();
				}

                // Draw Tracks (events)

                for (int i = 0; i < trackDisplays.Count; i++)
                {
                    TrackDisplay td = trackDisplays[i];
                    if(td.EventTrack != null)
                    {
                        Rect trackRect = new Rect(channelRect.x,
                                              contentRect.center.y + trackDisplays.Count * -20 + i*40,
                                              fullWidth - viewOffset,       // This is special because we actually DO want to draw off the edge.
                                              24f);

                        td.IsEdit = i == selectIdx;
                        td.Draw(trackRect, displayState, selectedEvents);
                    }
                }


				// Only process this drawing if we're repainting (DOES NOT USE GUI SYSTEM).
				if (Event.current.type.Equals(EventType.Repaint))
				{
					// Draw overlays
					int firstVisibleSample = displayState.GetFirstVisiblePackSample();
					int lastVisibleSample = firstVisibleSample + (fullWidth * displayState.samplesPerPack);

					int viewInset = displayState.GetWaveformInset();
					int bottom = (int)(top + (2f * height));

					// Playback Anchor - not a function (combined with Playhead) because we may specialize the rendering
					//  for the Anchor with some extra elements. For now, "inline" is fine.
					if (displayState.playbackAnchorSamplePosition >= firstVisibleSample &&
					    displayState.playbackAnchorSamplePosition <= lastVisibleSample)
					{
						int position = viewInset + ((displayState.playbackAnchorSamplePosition - firstVisibleSample) / displayState.samplesPerPack);

						float grayValue = 230f / 255f;
						Color anchorColor = new Color(grayValue, grayValue, grayValue, KoreographerColors.HandleFullAlpha);
						
						DrawOverlayLine((int)left + position, (int)top, bottom, anchorColor);
					}

					// Playhead.
					if (bShowPlayhead &&
					    displayState.playheadSamplePosition >= firstVisibleSample &&
					    displayState.playheadSamplePosition <= lastVisibleSample)
					{
						// Make the playhead position flexible to allow for scrolling (while maintaining playhead position).
						int position = viewInset + ((displayState.playheadSamplePosition - firstVisibleSample) / displayState.samplesPerPack);

						float grayValue = 180f / 255f;
						Color playheadColor = new Color(grayValue, grayValue, grayValue, KoreographerColors.HandleFullAlpha);

						DrawOverlayLine((int)left + position, (int)top, bottom, playheadColor);
					}
				}

				if (Event.current.type == EventType.Repaint)
				{
					// Store rect.  This must be done during Repaint as the values are not properly handled on Layout.
					waveContentRect = displayRect;
				}
			}
			GUI.EndGroup();
		}

		public int GetMaximumSamplesPerPack(int totalSampleTime, int windowWidth)
		{
			return Mathf.CeilToInt((float)totalSampleTime / (float)GetChannelPixelWidthForWindow(windowWidth));
		}

		void DrawOverlayLine(int x, int startY, int endY, Color lineColor)
		{
			Handles.color = lineColor;

			Vector3 startPoint = new Vector3(x, startY);
			Vector3 endPoint = new Vector3(x, endY);

#if UNITY_5_4_OR_NEWER
			float incVal = 1f / EditorGUIUtility.pixelsPerPoint;
#else
			float incVal = 1f;
#endif
			float curStep = 0f;

			// Outer loop allows us to duplicate lines such that we fill empty spaces with
			//  the GL.LINES rendering and Retina-or-equivalent contexts.
			while (curStep < 1f)
			{
				Handles.DrawLine(startPoint, endPoint);

				// Increment the vertex positions and counter.
				startPoint.x += incVal;
				endPoint.x += incVal;
				curStep += incVal;
			}
		}

		void DrawBeatLines(Rect contentRect, WaveDisplayState displayState, Koreography koreo)
		{
			int startSample = displayState.GetFirstVisiblePackSample();
			int endSample = startSample + displayState.samplesPerPack * (int)contentRect.width;

			int startSectionIdx = koreo.GetTempoSectionIndexForSample(startSample);
			int endSectionIdx = koreo.GetTempoSectionIndexForSample(endSample);

			TempoSectionDef drawSection = koreo.GetTempoSectionAtIndex(startSectionIdx);

			// The beat time at the beginning of the view.
			double beatTime = koreo.GetBeatTimeFromSampleTime(startSample, displayState.beatSubdivisions);

			// The distance into the current measure at the beginning of the view.
			double measureDist = koreo.GetMeasureTimeFromSampleTime(startSample) % 1d;

			// What beat division number are we at (used for coloring).
			int beatDivNum = (int)System.Math.Floor((measureDist * (double)(displayState.beatSubdivisions * drawSection.BeatsPerMeasure)));

			for (int i = startSectionIdx + 1; i <= endSectionIdx; ++i)
			{
				TempoSectionDef nextSection = koreo.GetTempoSectionAtIndex(i);

				DrawTempoSectionBG(contentRect, displayState, drawSection, startSample, nextSection.StartSample, WaveDisplay.GetColorForSectionAtIndex(i - 1));

				// This has the side effect of getting the beatTime value up to the "nextSection.StartSample" location.
				DrawBeatLinesForSection(contentRect, displayState, drawSection, startSample, nextSection.StartSample, ref beatTime, ref beatDivNum);

				// If the next section forces a new measure, we will consume the remaining time
				//  and fast-forward us to the next beat.
				if (nextSection.DoesStartNewMeasure)
				{
					beatTime = System.Math.Floor(beatTime) + 1d;
					beatDivNum = 0;
				}

				// Set up for the next section!
				startSample = nextSection.StartSample;
				drawSection = nextSection;
			}

			DrawTempoSectionBG(contentRect, displayState, drawSection, startSample, endSample, WaveDisplay.GetColorForSectionAtIndex(endSectionIdx));
			DrawBeatLinesForSection(contentRect, displayState, drawSection, startSample, endSample, ref beatTime, ref beatDivNum);
		}

		// TODO: All line drawing is done using repeated additions.  This can accumulate error over time.  If inconcistencies
		//  begin to show up with beat locations, redo this by calculating in terms of beats rather than samples.  Then do
		//  the inline conversion of beat number to sample position.
		void DrawBeatLinesForSection(Rect contentRect, WaveDisplayState displayState, TempoSectionDef tempoSection, int startSample, int endSample, ref double sectionBeatTime, ref int beatDivNum)
		{
			double sampsPerBeatDiv = tempoSection.GetSamplesPerBeatSection(displayState.beatSubdivisions);
			int divsPerMeasure = displayState.beatSubdivisions * tempoSection.BeatsPerMeasure;

			// The starting position to match the sectionBeatTime.
			double beatSample = startSample;

			// Store the initial beat number.
			double initialBeatNum = System.Math.Floor(sectionBeatTime);

			// Find the first beat line.  Note that this handles the 0 case (where the first beatline is right
			//  where we are).
			beatSample += ((System.Math.Ceiling(sectionBeatTime) - sectionBeatTime) * sampsPerBeatDiv);
			sectionBeatTime = System.Math.Ceiling(sectionBeatTime);

			// Adjust the beat division number to match with any beat timing adjustments.
			beatDivNum = (beatDivNum + (int)(sectionBeatTime - initialBeatNum)) % divsPerMeasure;

			float grayValue = 170f / 255f;
			Color firstBeatColor = new Color(grayValue, grayValue, grayValue);
			
			grayValue = 96f / 255f;
			Color normalBeatColor = new Color(grayValue, grayValue, grayValue);

			grayValue = 60f / 255f;
			Color subDivColor = new Color(grayValue, grayValue, grayValue);
			
			Vector3 topPoint = new Vector3(0f, contentRect.yMin);
			Vector3 botPoint = new Vector3(0f, contentRect.yMax);

			// Cache Rect Property values so that we don't constantly recalculate them.
			float startX = contentRect.x;
			
			// Handle situations where we need to draw beat lines (including subdivision lines) in a fashion that's more
			//  tightly packed than there is pixel-space for.  Currently this degrades across line-types equivalently,
			//  rather than by subdivision|beat|measure as we likely expect it to.
			int stepSize = 1;
			if (sampsPerBeatDiv < 2 * displayState.samplesPerPack)
			{
				stepSize = (int)System.Math.Ceiling(2d * ((double)displayState.samplesPerPack / sampsPerBeatDiv));
				sampsPerBeatDiv *= stepSize;
			}

#if UNITY_5_4_OR_NEWER
			float incVal = 1f / EditorGUIUtility.pixelsPerPoint;
#else
			float incVal = 1f;
#endif
			int firstVisibleSample = displayState.GetFirstVisiblePackSample();

			GLDrawing.BeginLineDrawing();
			{
				// Draw all the beat lines!
				while (beatSample < endSample)
				{
					// Color for Measure | Beat | Subdivision.
					GL.Color((beatDivNum % divsPerMeasure == 0) ? firstBeatColor : (beatDivNum % displayState.beatSubdivisions == 0) ? normalBeatColor : subDivColor);

					// Keeping this division in here is heavier but it is also more accurate.
					topPoint.x = startX + (int)((beatSample - firstVisibleSample) / displayState.samplesPerPack);
					botPoint.x = topPoint.x;

					// Inner loop allows us to duplicate lines such that we fill empty spaces with
					//  the GL.LINES rendering and Retina-or-equivalent contexts.  Most other similar
					//  rendering functions (e.g. those in ChannelDisplay) do this as an outter loop.
					//  In this case, we sidestep the need to set GL.Color an equivalent amount of
					//  times, including the nested ternary operators.  There are tradeoffs and this
					//  seems to be the most effective method for now.
					float curStep = 0f;
					while (curStep < 1f)
					{
						GL.Vertex(topPoint);
						GL.Vertex(botPoint);

						// Increment the vertex positions and counter.
						topPoint.x += incVal;
						botPoint.x += incVal;
						curStep += incVal;
					}

					// Iterate us forward!
					beatSample += sampsPerBeatDiv;
					sectionBeatTime += stepSize;
					beatDivNum = (beatDivNum + stepSize) % divsPerMeasure;
				}
			}
			GLDrawing.EndLineDrawing();

			// There is a single edge-case here.  If the beatSample is the same as the endSample then the following
			//  math will reduce the beatDivNum but would *NOT* reduce the sectionBeatTime.  This is a serious
			//  problem if the next TempoSection begins on exactly this sample.  In that case, the beatDivNum will
			//  be off by one (this is how we came across the edge case).
			if (beatSample != endSample)
			{
				// At this point, sectionBeatTime and beatDivNum are LARGER than they should be.  They point to
				//  the first beat location beyond this section.  As such, back them out to the end position.
				sectionBeatTime -= (beatSample - endSample) / sampsPerBeatDiv;
				beatDivNum = (beatDivNum - 1) % divsPerMeasure;
			}
		}

		void DrawTempoSectionBG(Rect contentRect, WaveDisplayState displayState, TempoSectionDef tempoSection, int startSample, int endSample, Color sectionColor)
		{
			int firstVisibleSample = displayState.GetFirstVisiblePackSample();
			// Only draw the section if our current zoom level is reasonable.
			if (endSample - startSample >= displayState.samplesPerPack * 2)
			{
				Color bgColor = GUI.backgroundColor;
				GUI.backgroundColor = sectionColor;
				Rect boxRect = new Rect(contentRect);
				boxRect.xMin += (float)(startSample - firstVisibleSample) / displayState.samplesPerPack;
				boxRect.xMax -= contentRect.width - (float)(endSample - firstVisibleSample) / displayState.samplesPerPack;
				KoreographyEditorSkin.box.Draw(boxRect, false, false, false, false);
				GUI.backgroundColor = bgColor;
			}
		}

		// This is mainly for debug.  Note that it uses the pretty heavy "GetSampleTimeFromMeasureTime" method
		//  which internally adds up time across all Tempo Sections call.  This gets heavy when there are a lot
		//  of tempo sections in the audio.
		void DrawMeasureLines(Rect contentRect, WaveDisplayState displayState, Koreography koreo)
		{
			int startSample = displayState.GetFirstVisiblePackSample();
			int endSample = startSample + displayState.samplesPerPack * (int)contentRect.width;

			// Measure lines only.
			int leftMeasureTime = (int)System.Math.Floor(koreo.GetMeasureTimeFromSampleTime(startSample));
			int rightMeasureTime = (int)System.Math.Floor(koreo.GetMeasureTimeFromSampleTime(endSample));
			
			Handles.color = Color.green;
			
			// This might cut off a measure line visible on the left-mose pixel position.
			if (leftMeasureTime < rightMeasureTime)
			{
				// We have at least one visible measure line in here.
				for (int i = leftMeasureTime; i <= rightMeasureTime; ++i)
				{
					int samplePos = (koreo.GetSampleTimeFromMeasureTime(i) - startSample) / displayState.samplesPerPack;
					
					Vector3 topPoint = new Vector3(0f, contentRect.yMin);
					Vector3 botPoint = new Vector3(0f, contentRect.yMax);
					
					topPoint.x = (int)(contentRect.x + samplePos);
					botPoint.x = topPoint.x;
					
					Handles.DrawLine(topPoint, botPoint);
				}
			}
		}
	
		public bool ContainsPoint(Vector2 loc)
		{
			return waveContentRect.Contains(loc);
		}

		public bool IsClickableAtLoc(Vector2 loc)
		{
			return waveContentRect.Contains(loc);
		}

		public int GetChannelPixelWidthForWindow(int containerWidth)
		{
			return containerWidth - GUI.skin.box.padding.horizontal;
		}

		public int GetPixelOffsetInChannelAtLoc(Vector2 loc)
		{
			return (int)GetOffsetLocFromRaw(loc).x;
		}

		Vector2 GetOffsetLocFromRaw(Vector2 rawLoc)
		{
			// Need to adjust the location for the internal stuff from here.  It's all stored in an offset GUI.Group!
			return new Vector2(rawLoc.x - (waveContentRect.xMin),
		                   rawLoc.y - (waveContentRect.yMin));
		}

		public bool IsTrackAtPoint(Vector2 loc,int idx)
		{
            if (trackDisplays.Count > idx)
            {
                return trackDisplays[idx].ContainsPoint(GetOffsetLocFromRaw(loc));
            }
            return false;
		}

		public KoreographyEvent GetEventAtLoc(Vector2 loc, int idx)
		{
			KoreographyEvent retEvent = null;
			if (waveContentRect.Contains(loc))
			{
                if (trackDisplays.Count > idx)
                {
                    retEvent = trackDisplays[idx].GetEventAtLoc(GetOffsetLocFromRaw(loc));
                }
			}
			return retEvent;
		}

		public EventEditMode GetEventEditModeAtLoc(Vector2 loc, int idx)
		{
			EventEditMode retMode = EventEditMode.None;
			if (waveContentRect.Contains(loc))
			{
                if (trackDisplays.Count > idx)
                {
                    retMode = trackDisplays[idx].GetEventEditModeAtLoc(GetOffsetLocFromRaw(loc));
                }
			}
			return retMode;
		}

		public List<KoreographyEvent> GetEventsTouchedByArea(Rect testArea, int idx)
		{
			testArea.center = GetOffsetLocFromRaw(testArea.center);
            if (trackDisplays.Count > idx)
            {
                return trackDisplays[idx].GetEventsTouchedByArea(testArea);
            }
            return null;
		}

		// Note that this is "local" in the sense that it does not take absolute positioning into
		//  account. As long as the waveform is the first horizontal element drawn in its "row", then
		//  the two are equivalent anyway.
		float GetDrawStart(WaveDisplayState displayState)
		{
			// TODO: Embed this info into the skin!  This was reconstructed from Draw().
			// TODO: Replace the GUI.skin calls with a call to GetFirstAbsoluteDrawableXPosition().
			return GUI.skin.box.padding.left + GUI.skin.box.margin.left + displayState.GetWaveformInset();
		}

		/// <summary>
		/// Gets the sample position of the specified point.  This is guaranteed positive and is
		/// NOT bound by the length of the song.  Please check that the value returned is within
		/// the range you need.
		/// </summary>
		/// <returns>The sample position of the requested point.</returns>
		/// <param name="loc">The location to test.</param>
		/// <param name="displayState">The current display state of the Wave Display.</param>
		public int GetSamplePositionOfPoint(Vector2 loc, WaveDisplayState displayState)
		{
			float drawStart = GetDrawStart(displayState);
			float distFromContentStart = loc.x - waveContentRect.x;

			int samplePos = displayState.GetFirstVisiblePackSample() + ((int)(distFromContentStart - drawStart) * displayState.samplesPerPack);

			// Disallow negative numbers!
			return Mathf.Max(samplePos, 0);
		}

		public float GetHorizontalLocOfSample(int samplePos, WaveDisplayState displayState)
		{
			float pixelsIn = (float)((samplePos / displayState.samplesPerPack) - displayState.GetFirstVisiblePack());
			return pixelsIn + GetDrawStart(displayState);
		}

		// Retrieves the first drawable horizontal position in terms of [non-scrolled!] absolute position.
		//  If the waveform is within a scrollable area, it is possible that this will return a value that
		//  that does not actually get drawn as it's offscreen even though it would still be positive as
		//  it's relative to that "view".
		public float GetFirstAbsoluteDrawableXPosition()
		{
			return GUI.skin.box.padding.left + waveContentRect.xMin;
		}

		#endregion
	}
}
