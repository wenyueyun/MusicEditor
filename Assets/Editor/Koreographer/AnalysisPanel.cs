//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

#if !KOREO_NON_PRO

using UnityEditor;
using UnityEngine;

namespace SonicBloom.Koreo.EditorUI
{
	internal partial class AnalysisPanel : EditorWindow
	{
		#region Static Fields

		static AnalysisPanel thePanel = null;

		static GUIContent winTitleContent = new GUIContent("Analysis Settings");

		static string warnNoClipContent		= "No Audio Clip set in the Koreography Editor.  Please assign one to access settings.";
		static string warnNoTrackContent	= "No Koreography Track set in the Koreography Editor.  Please assign one to access settings.";

		static GUIContent[] tabOptions = {	new GUIContent("RMS", "Runs a Root Mean Square algorithm over the audio.  Matches waveform RMS amplitudes."),
											new GUIContent("FFT", "Runs a Fast Fourier Transform algorithm over the audio. Produces a list of frequency spectrum intencities."), };

		static GUIContent commonSettingsContent	= new GUIContent("Common", "These settings are common across all analysis options.");

		// Shared!
		static GUIContent channelIndexContent		= new GUIContent("Audio Clip Channel", "Which channel of the Audio Clip should be sourced for calculations.");
		static GUIContent sampleRangeModeContent	= new GUIContent("Sample Range Mode", "Provides the following modes:\nFull Clip - Uses the entire Audio Clip.\nEditor Range - Adds a range specifier to the Koreography Editor and uses it.\nCustom Range - Uses the controls below to set a sample range for configuration.");
		static GUIContent processRangeContent		= new GUIContent("Sample Range", "Sets the start and end samples of the range of Audio data to process.");
		static GUIContent rangeSectionStartContent	= new GUIContent("Start", "Precise setting for the start sample of the analysis range.");
		static GUIContent rangeSectionEndContent	= new GUIContent("End", "Precise setting for the end sample of the analysis range.");
		static GUIContent fullClipSliderContent		= new GUIContent("", "The entire Audio Clip is selected by default.");
		static GUIContent editorRangeSliderContent	= new GUIContent("", "Please use the green range specifier in the Koreography Editor to set the sample range for RMS calculation.");
		static GUIContent customRangeSliderContent	= new GUIContent("", "Use this slider to perform broad-strokes selection of a sample region to analyze.");
		static GUIContent outputSettingsContent		= new GUIContent("Output Settings", "Settings to adjust the output Koreography Events.");
		static GUIContent overwriteTrackLabel		= new GUIContent("Overwrite Events in Track", "Overwrite all Koreography Events in the currently selected Koreography Track in the Koreography Editor with the analysis output.");
		static GUIContent appendTrackLabel			= new GUIContent("Append to Track", "Append the analysis output to the currently selected Koreography Track in the Koreography Editor.");

		// Audio channel related.
		static string[] channelOptionsMono		= { "Mono" };
		static string[] channelOptionsStereo	= { "Left", "Right" };
		static string[] channelOptionsQuad		= { "Front Left", "Front Right", "Rear Left", "Rear Right" };
		static string[] channelOptionsSurround	= { "Front Left", "Front Right", "Center", "Rear Left", "Rear Right" };
		static string[] channelOptions5Point1	= { "Front Left", "Front Right", "Center", "Rear Left", "Rear Right", "Subwoofer" };
		static string[] channelOptions7Point1	= { "Front Left", "Front Right", "Center", "Rear Left", "Rear Right", "Side Left", "Side Right", "Subwoofer" };
		static string[][] channelMappings = { null, channelOptionsMono, channelOptionsStereo, null, channelOptionsQuad, channelOptionsSurround, channelOptions5Point1, null, channelOptions7Point1 };

		#endregion
		#region Fields
		
		KoreographyEditor editorWin = null;		// The window to interface with.

		int mainTabIdx = 0;						// The index of the currently selected tab.

		string[] currentChannelOptions;			// The list of channel options associated with the number of channels in the Audio.
		int channelCount = 0;
		int channelIdx = 0;

		// Sample Range stuff.
		int sampleStartOffset = 0;
		int sampleEndOffset = int.MaxValue;

		enum SampleRangeModes
		{
			FullClip,
			EditorRange,
			CustomRange,
		}
		SampleRangeModes sampleRangeMode = SampleRangeModes.FullClip;
		
		#endregion
		#region Static Methods

		public static AnalysisPanel OpenWindow(KoreographyEditor editorWindow)
		{
			if (thePanel == null)
			{
				AnalysisPanel win = GetWindow<AnalysisPanel>(true, winTitleContent.text);
				
				Vector2 winSize = new Vector2(400f, 400f);
				win.maxSize = winSize;
				win.minSize = winSize;

				win.editorWin = editorWindow;

				thePanel = win;
			}
			else
			{
				thePanel.Focus();
			}

			return thePanel;
		}
		
		#endregion
		#region Properties

		int StartSample
		{
			get
			{
				int retVal = sampleStartOffset;
				if (sampleRangeMode == SampleRangeModes.FullClip)
				{
					retVal = 0;
				}
				return retVal;
			}
		}

		int EndSample
		{
			get
			{
				int retVal = sampleEndOffset;
				if (sampleRangeMode == SampleRangeModes.FullClip)
				{
					retVal = editorWin.EditClip.samples;
				}
				return retVal;
			}
		}

		public bool IsEditorRangeSelection
		{
			get
			{
				return sampleRangeMode == SampleRangeModes.EditorRange;
			}
		}

		#endregion
		#region Methods
		
		void OnEnable()
		{
			wantsMouseMove = true;

			// Script compilation resets static fields.  Make sure we're the only one.
			if (thePanel == null)
			{
				thePanel = this;
			}

			// Ensure access to GUIStyles.
//			AnalysisSkin.InitSkin();
		}

		void UpdateWindowSize()
		{
			Vector2 winSize = new Vector2(400f, 325f);

			switch(mainTabIdx)
			{
			case 0:	// RMS
				winSize.Set(400f, 325f);
				break;
			case 1:	// FFT
				winSize.Set(400f, 548f);
				break;
			default:
				break;
			}

			// Update EditorWindow settings.
			maxSize = winSize;
			minSize = winSize;
		}
		
		void OnGUI()
		{
			// Disable the content if the EditClip isn't valid.
			if (editorWin.EditClip == null)
			{
				EditorGUILayout.HelpBox(warnNoClipContent, MessageType.Error);
			}
			else if (editorWin.EditTrack == null)
			{
				EditorGUILayout.HelpBox(warnNoTrackContent, MessageType.Error);
			}
			else
			{
				DoCommonGUI();

				EditorGUI.BeginChangeCheck();
				{
					mainTabIdx = GUILayout.Toolbar(mainTabIdx, tabOptions);
				}
				if (EditorGUI.EndChangeCheck())
				{
					UpdateWindowSize();
				}

				switch(mainTabIdx)
				{
				case 0:	// RMS
					DoRMSGUI();
					break;
				case 1:	// FFT
					DoFFTGUI();
					break;
				default:
					Debug.LogError("How did we get an unknown tab index??");
					break;
				}
			}
		}

		void DoCommonGUI()
		{
			GUILayout.Toggle(true, commonSettingsContent, GUI.skin.button);
			
			DoChannelSelectGUI();
			
			DoRangeSelectGUI();
		}

		void DoChannelSelectGUI()
		{
			// Detect a potential change to the channel options.
			if (channelCount == 0 || channelCount != editorWin.EditClip.channels || currentChannelOptions == null)
			{
				channelCount = editorWin.EditClip.channels;

				// Catch situations wherein a channel mapping isn't defined, or we have more channels than we have mappings for.
				if (channelCount > channelMappings.Length || channelMappings[channelCount] == null)
				{
					currentChannelOptions = new string[channelCount];

					for (int i = 0; i < channelCount; ++i)
					{
						currentChannelOptions[i] = "Channel " + i;
					}
				}
				else
				{
					currentChannelOptions = channelMappings[channelCount];
				}

				channelIdx = Mathf.Clamp(channelIdx, 0, channelCount - 1);
			}

			EditorGUILayout.BeginHorizontal();
			{
				EditorGUILayout.PrefixLabel(channelIndexContent);
				
				EditorGUI.BeginChangeCheck();
				{
					channelIdx = EditorGUILayout.Popup(channelIdx, currentChannelOptions);
				}
				if (EditorGUI.EndChangeCheck())
				{
					// Update the FFT panel when the channel changes!
					if (mainTabIdx == 1)
					{
						UpdateFFTPreviewTexture();
					}
				}
			}
			EditorGUILayout.EndHorizontal();
		}

		void DoRangeSelectGUI()
		{
			int totalSamples = editorWin.EditClip.samples;

			SampleRangeModes newMode = (SampleRangeModes)EditorGUILayout.EnumPopup(sampleRangeModeContent, sampleRangeMode);

			// Adjust bounds if necessary.
			if (newMode != sampleRangeMode)
			{
				// Store previous mode.
				SampleRangeModes prevMode = sampleRangeMode;

				// Set current option (necessary for internal usage within methods).
				sampleRangeMode = newMode;

				if (sampleRangeMode == SampleRangeModes.EditorRange)
				{
					// Make sure that the range is in view.  If it *ISN'T* in view,
					//  shift it so that it is.
					int startSample = sampleStartOffset;
					int endSample = sampleEndOffset;
					int minSample = editorWin.GetMinVisibleSamplePos();
					int maxSample = editorWin.GetMaxVisibleSamplePos();

					int targetSampleDiff = Mathf.Min(endSample - startSample, maxSample - minSample);

					if (startSample > maxSample)
					{
						// The entire range is greater than the current view range.
						endSample = maxSample;
						startSample = endSample - targetSampleDiff;
					}
					else if (endSample < minSample)
					{
						// The entire range is less than the current view range.
						startSample = minSample;
						endSample = startSample + targetSampleDiff;
					}
					else
					{
						// Clamp the range to the bounds.
						startSample = Mathf.Clamp(startSample, minSample, maxSample);
						endSample = Mathf.Clamp(endSample, minSample, maxSample);
					}

					SetRangeExtents(startSample, endSample);
					UpdateEditorRange();
				}

				// If we should no longer show the slider in the Koreography Editor, refresh it to force the
				//  visual update.
				if (prevMode == SampleRangeModes.EditorRange)
				{
					editorWin.Repaint();
				}
			}

			EditorGUILayout.BeginHorizontal();
			{
				EditorGUI.indentLevel++;
				{
					EditorGUILayout.LabelField(processRangeContent, GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
				}
				EditorGUI.indentLevel--;

				EditorGUI.BeginDisabledGroup(sampleRangeMode == SampleRangeModes.FullClip);
				{
					int start = 0;
					int end = 0;
					EditorGUI.BeginChangeCheck();
					{
						EditorGUIUtility.labelWidth = 35f;
						start = EditorGUILayout.IntField(rangeSectionStartContent, StartSample);
						GUILayout.Space(5f);
						end = EditorGUILayout.IntField(rangeSectionEndContent, EndSample);
						EditorGUIUtility.labelWidth = 0f;	// Restore default.
					}
					if(EditorGUI.EndChangeCheck())
					{
						SetRangeExtents(start, end);
						UpdateEditorRange();
					}
				}
				EditorGUI.EndDisabledGroup();
			}
			EditorGUILayout.EndHorizontal();

			// Range selection slider.
			EditorGUI.indentLevel++;
			{
				float totalSampsFloat = (float)totalSamples;
				float start = (float)sampleStartOffset / totalSampsFloat;
				float end = (float)Mathf.Min(sampleEndOffset, totalSampsFloat) / totalSampsFloat;		// Ensure that we're in range.  Fixes first-run issues.

				GUIContent tooltip = fullClipSliderContent;

				if (sampleRangeMode == SampleRangeModes.FullClip)
				{
					EditorGUI.BeginDisabledGroup(true);
					{
						start = 0f;
						end = 1f;
						EditorGUILayout.MinMaxSlider(ref start, ref end, 0f, 1f);
					}
					EditorGUI.EndDisabledGroup();
				}
				else if (sampleRangeMode == SampleRangeModes.CustomRange)
				{
					EditorGUI.BeginChangeCheck();
					{
						EditorGUILayout.MinMaxSlider(ref start, ref end, 0f, 1f);
					}
					if (EditorGUI.EndChangeCheck())
					{
						int startSample = (int)(start * totalSampsFloat);
						int endSample = (int)(end * totalSampsFloat);
						
						SetRangeExtents(startSample, endSample);
					}

					tooltip = customRangeSliderContent;
				}
				else
				{
					EditorGUI.BeginDisabledGroup(true);
					{
						EditorGUILayout.MinMaxSlider(ref start, ref end, 0f, 1f);
					}
					EditorGUI.EndDisabledGroup();

					tooltip = editorRangeSliderContent;
				}

				// Tooltip for the slider.
				KoreographerGUIUtils.AddTooltipToRect(tooltip, GUILayoutUtility.GetLastRect());
			}
			EditorGUI.indentLevel--;
		}

		void UpdateEditorRange()
		{
			int minSample = editorWin.GetMinVisibleSamplePos();
			int maxSample = editorWin.GetMaxVisibleSamplePos();

			float min = (float)(sampleStartOffset - minSample) / (float)(maxSample - minSample);
			float max = (float)(sampleEndOffset - minSample) / (float)(maxSample - minSample);

			editorWin.SetSelectedRange(new Vector2(min, max));
			editorWin.Repaint();
		}
		
		#endregion
		#region Control Methods

		public void SetRangeExtents(int startSample, int endSample)
		{
			if (startSample >= endSample)
			{
				int samplesPerPack = editorWin.DisplayState.samplesPerPack;
				int totalSamples = editorWin.EditClip.samples;

				// Ensure that we have at least one "pack" of pixels to read.
				if (endSample - startSample < samplesPerPack)
				{
					endSample = startSample + samplesPerPack;
					if (endSample > totalSamples)
					{
						endSample = totalSamples;
						startSample = totalSamples - samplesPerPack;
					}
				}
			}

			// Adjustments for the special mode.
			if (sampleRangeMode == SampleRangeModes.EditorRange)
			{
				int minSample = editorWin.GetMinVisibleSamplePos();
				int maxSample = editorWin.GetMaxVisibleSamplePos();

				// Make sure we're clamped correctly.
				startSample = Mathf.Clamp(startSample, minSample, maxSample);
				endSample = Mathf.Clamp(endSample, minSample, maxSample);
			}
			
			// Update the values.
			sampleStartOffset = startSample;
			sampleEndOffset = endSample;
		}

		#endregion
	}
}

#endif	// KOREO_NON_PRO
