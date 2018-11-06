//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

#if !KOREO_NON_PRO

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace SonicBloom.Koreo.EditorUI
{
	internal partial class AnalysisPanel
	{
		#region Const Fields
		
		// FFT Window Size.
		const int fftWindowPowerMin	= 5;			// 2^5 = 32.
		const int fftWindowPowerMax	= 14;			// 2^14 = 16384.

		// FFT Sampling Rate.
		const float fftRateMin		= 1 / 60f;		// Equals 1 BPM.
		const float fftRateMax		= 3600 / 60f;	// Equals 900 BPM. (60fps)
		
		// FFT Decibel Gain.
		const float fftGainMin		= 0f;
		const float fftGainMax		= 200f;
		
		// FFT Decibel Normalization Range.
		const float fftRangeMin		= 1f;
		const float fftRangeMax		= 400f;

		const float fftPreviewHeight	= 128f;

		#endregion
		#region Static Fields

		static string warnFFTNoPayloadSupport = "The currently selected Koreography Track does not support the necessary types for this feature.  Please select a Koreography Track that supports the Spectrum Payload.";
		
		static string FFTExplanation = "FFT (Fast Fourier Transform) provides a frequency spectrum of a certain set of audio samples.  Use the settings below to configure FFT calculations to generate a Span that contain frequency intensities of the selected range of the audio clip.";
		static string FFTInvalidSettings = "Not enough samples in the range provided.  Please adjust either lower the Start Sample value, the Window Size, or both.";

		static GUIContent fftWindowSizeContent		= new GUIContent("Window Size", "The number of samples to send to the FFT algorithm.  The larger the window, the higher the frequency resolution of the output.  However, larger windows generate more data and aren't as localized in time.");
		static GUIContent fftWindowFuncContent		= new GUIContent("Window Function", "The windowing function to use during FFT processing.  Different windows produce slightly different results.  The Rectangular window is equivalent to no window at all and typically results in the largest amount of noise.");
		static GUIContent fftEvalsPerSecContent		= new GUIContent("Evaluations Per Second", "The number of times the FFT should be applied for every second selected in the configured Sample Range.  The calculations are evenly spread and may overlap.");
		static GUIContent fftGainFieldContent		= new GUIContent("Gain", "The number of decibels by which to raise all FFT results.  May bring out quieter peaks.");
		static GUIContent fftRangeFieldContent		= new GUIContent("Range", "The decibel range to consider to showcase results.  A wider range may result in more visual noise, while a lower range may highlight dramatic changes.");
		static GUIContent fftFreqMaskLabelContent	= new GUIContent("Frequency Mask", "The range of frequencies to select for export.  Frequency resolution is dependent upon Window Size.");
		static GUIContent fftPrevSpectroContent		= new GUIContent("Preview Spectrogram", "A spectrogram showing a sampling of frequencies evaluated over time given the current FFT settings.  Each column of pixels is a representation of the linear frequency spectrum.\n\nNOTE: The configured Sample Range is currently ignored.  The preview does not necessarily reflect output Koreography.");
		static GUIContent fftPrevTexFitContent		= new GUIContent("Fit", "Whether or not the preview texture should be vertically stretched or squashed to fit the preview area.");
		static GUIContent fftNumBinsLabelContent	= new GUIContent("<b>Bins:</b>", "The number of frequency bins that will be exported with the current settings.");
		static GUIContent fftNumBinsValueContent	= new GUIContent("", "The number of frequency bins that will be exported with the current settings.");
		static GUIContent fftsPerMinLabelContent	= new GUIContent("<b>FFTs Per Min:</b>", "The number of FFTs analyzed per minute.  Similar to BPM!");
		static GUIContent fftsPerMinValueContent	= new GUIContent("", "The number of FFTs analyzed per minute.  Similar to BPM!");
		static GUIContent fftOutputSizeLabelContent	= new GUIContent("<b>Output Size:</b>", "The estimated size of the resulting data.");
		static GUIContent fftOutputSizeValueContent	= new GUIContent("", "The estimated size of the resulting data.");
		static GUIContent fftPrevSpectrumContent	= new GUIContent("Preview Spectrum", "Mouse over the Preview Spectrogram to see a preview of the spectrum at that location.  Note that the number of bars represented may be smaller than the total number of available bins.");

		static GUIContent fftProcessContent			= new GUIContent("Process FFT", "Uses the settings above to process the audio and generate Payloads.");
		
		static GUIStyle prevTexInfoStyle;

		#endregion
		#region Fields

		// Algorithm Settings.

		// FFT Window Size.
		int fftWindowPower		= 8;		// 2^8 = 256.
		int fftWindowSize		= 256;		// Default.
		int fftWindowHalfSize	= 128;		// Default.

		// Window Functions.
		enum FFTWindowFunc
		{
			Rectangular,
			Hann,
			Hamming,
		}
		FFTWindowFunc fftWindowFunc = FFTWindowFunc.Hann;

		// FFT Sampling Rate.
		float fftsPerSecond	= 2f;

		// FFT Decibel Gain.
		float fftGain		= 20f;		// The amount to pump the FFT results by.
		
		// FFT Decibel Normalization Range.
		float fftRange		= 80f;		// The range of volume to normalize.
		
		// FFT Frequency Mask.
		float fftFreqMaskMin	= 0f;	// In a range of [0,1].
		float fftFreqMaskMax	= 1f;	// In a range of [0,1].

		// Preview Settings.

		Texture2D fftPreviewTex;
		Texture2D fftPreviewBGTex;
		Vector2 fftPreviewScrollPos = Vector2.zero;
		bool bFFTPrevTexFit = false;
		float[] fftPrevSpectrum;

		// Support Fields.
		
		// FFT Bin Width.
		float fftBinWidth	= 1f;

		// FFT Calculation related.
		FFT2 fftObj = new FFT2();

		[System.NonSerialized]
		double[] fftWindowCoefficients;

		[System.NonSerialized]
		double[] fftRealValues;

		[System.NonSerialized]
		double[] fftImagValues;

		[System.NonSerialized]
		double[] fftResultSpectrum;

		// Audio Reading.
		[System.NonSerialized]
		float[] fftAudioSamples;

		#endregion
		#region Properties

		float FieldWidth
		{
			get
			{
				return minSize.x - (EditorGUIUtility.labelWidth + 8f);	// The +8 is for the sides.
			}
		}

		#endregion
		#region Methods

		void InitFFT()
		{
			if (prevTexInfoStyle == null)
			{
				prevTexInfoStyle = new GUIStyle(EditorStyles.miniLabel);
				prevTexInfoStyle.richText = true;
			}

			if (fftPreviewTex == null)
			{
				fftPreviewTex = new Texture2D((int)position.width, fftWindowHalfSize, TextureFormat.Alpha8, false);
				fftPreviewTex.hideFlags = HideFlags.HideAndDontSave;
			}

			if (fftPreviewBGTex == null)
			{
				fftPreviewBGTex = new Texture2D(4, 4);
				fftPreviewBGTex.hideFlags = HideFlags.HideAndDontSave;

				Color[] bgColors = fftPreviewBGTex.GetPixels();
				for (int i = 0; i < bgColors.Length; ++i)
				{
					bgColors[i] = Color.gray;
				}
				fftPreviewBGTex.SetPixels(bgColors);
				fftPreviewBGTex.Apply();
			}

			UpdateFFTWindowSize();

			UpdateFFTPreviewTexture();
		}

		void DoFFTGUI()
		{
			if (!editorWin.DoesCurrentTrackSupportPayloadType(typeof(SpectrumPayload)))
			{
				// Show warning.
				EditorGUILayout.HelpBox(warnFFTNoPayloadSupport, MessageType.Error);
			}
			else
			{
				// All good, dive in.
				DoFFTMainGUI();
			}
		}

		void DoFFTMainGUI()
		{
			// Check if initialization is required.  Can't do this in Enable() because we need access to a few
			//  things that aren't ready yet at that time (GUI.skin access; editorWin availability).
			if (fftRealValues == null)
			{
				InitFFT();
			}

			EditorGUILayout.HelpBox(FFTExplanation, MessageType.Info);

			EditorGUI.BeginChangeCheck();
			{
				EditorGUILayout.BeginHorizontal();
				{
					EditorGUI.BeginChangeCheck();
					{
						// Range of 2^6 to 2^14 [64 to 16384].
						EditorGUILayout.PrefixLabel(fftWindowSizeContent);

						Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUI.skin.horizontalSlider);
						float result = GUI.HorizontalSlider(rect, Mathf.InverseLerp(fftWindowPowerMin, fftWindowPowerMax, fftWindowPower), 0f, 1f);
						fftWindowPower = (int)Mathf.Lerp(fftWindowPowerMin, fftWindowPowerMax, result);

						EditorGUILayout.IntField(fftWindowSize, GUILayout.Width(50f));
					}
					if (EditorGUI.EndChangeCheck())
					{
						UpdateFFTWindowSize();
					}
				}
				EditorGUILayout.EndHorizontal();

				// Window Functions!
				EditorGUI.BeginChangeCheck();
				{
					fftWindowFunc = (FFTWindowFunc)EditorGUILayout.EnumPopup(fftWindowFuncContent, fftWindowFunc);
				}
				if (EditorGUI.EndChangeCheck())
				{
					UpdateFFTWindowFunction();
				}

				// How frequently to evaluate output!
				fftsPerSecond = EditorGUILayout.Slider(fftEvalsPerSecContent, fftsPerSecond, fftRateMin, fftRateMax);

				// Output settings!
				GUILayout.Label(outputSettingsContent, EditorStyles.boldLabel);

				// Decibel output adjustment controls!
				EditorGUI.indentLevel++;
				{
					fftGain = EditorGUILayout.Slider(fftGainFieldContent, fftGain, fftGainMin, fftGainMax);
					fftRange = EditorGUILayout.Slider(fftRangeFieldContent, fftRange, fftRangeMin, fftRangeMax);
				}
				EditorGUI.indentLevel--;

				// Frequency Mask!
				EditorGUILayout.BeginHorizontal();
				{
					EditorGUI.indentLevel++;
					{
						EditorGUILayout.PrefixLabel(fftFreqMaskLabelContent);
					}
					EditorGUI.indentLevel--;

					EditorGUILayout.FloatField(GetFFTBinFromPercent(fftFreqMaskMin) * fftBinWidth, GUILayout.Width(50f));
					EditorGUILayout.MinMaxSlider(ref fftFreqMaskMin, ref fftFreqMaskMax, 0f, 1f);
					EditorGUILayout.FloatField(GetFFTBinFromPercent(fftFreqMaskMax) * fftBinWidth, GUILayout.Width(50f));
				}
				EditorGUILayout.EndHorizontal();
			}
			if (EditorGUI.EndChangeCheck())
			{
				UpdateFFTPreviewTexture();
			}

			EditorGUILayout.BeginHorizontal();
			{
				GUILayout.Label(fftPrevSpectroContent, EditorStyles.boldLabel);

				GUILayout.FlexibleSpace();

				EditorGUI.BeginDisabledGroup(fftPreviewTex.height == fftPreviewHeight);
				{
					bFFTPrevTexFit = GUILayout.Toggle(bFFTPrevTexFit, fftPrevTexFitContent, EditorStyles.miniButton);
				}
				EditorGUI.EndDisabledGroup();
			}
			EditorGUILayout.EndHorizontal();

			Rect prevTexRect;

			// Draw the Preview Texture Area.
			fftPreviewScrollPos = GUILayout.BeginScrollView(fftPreviewScrollPos, false, true, GUILayout.Height(fftPreviewHeight));
			{
				// Add the first BG texture if necessary.
				if (fftPreviewTex.height < fftPreviewHeight && !bFFTPrevTexFit)
				{
					Rect bgRect = GUILayoutUtility.GetRect(fftPreviewTex.width, (fftPreviewHeight - fftPreviewTex.height) / 2f, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
					EditorGUI.DrawPreviewTexture(bgRect, fftPreviewBGTex);
				}

				// Draw the preview texture itself.
				float drawHeight = bFFTPrevTexFit ? fftPreviewHeight : fftPreviewTex.height;
				prevTexRect = GUILayoutUtility.GetRect(fftPreviewTex.width, drawHeight, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
				EditorGUI.DrawPreviewTexture(prevTexRect, fftPreviewTex);

				// Add the second BG texture if necessary.
				if (fftPreviewTex.height < fftPreviewHeight && !bFFTPrevTexFit)
				{
					Rect bgRect = GUILayoutUtility.GetRect(fftPreviewTex.width, (fftPreviewHeight - fftPreviewTex.height) / 2f, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
					EditorGUI.DrawPreviewTexture(bgRect, fftPreviewBGTex);
				}
			}
			GUILayout.EndScrollView();

			// Determine what to do about the preview spectrum.
			if (Event.current.type == EventType.MouseMove)
			{
				Rect previewRect = GUILayoutUtility.GetLastRect();					// Grab the scroll view Rect.

				// Adjust the previewRect based on the prevTexRect.  Do bottom value first as it's dependent upon the original settings for the top value.
				previewRect.xMax = prevTexRect.xMax;													// Fix the width.
				previewRect.yMax = Mathf.Min(previewRect.yMax, previewRect.yMin + prevTexRect.yMax);	// Fix the bot-Y value.  (Offset because the tex positions are relative.)
				previewRect.yMin = Mathf.Max(previewRect.yMin, previewRect.yMin + prevTexRect.yMin);	// Fix the top-Y value.  (Offset because the tex positions are relative.)

				if (previewRect.Contains(Event.current.mousePosition))
				{
					// Recalculate the spectrum.
					int mousePosX = (int)Event.current.mousePosition.x;

					int maxNumBars = (int)(position.width - FieldWidth / 2f);

					int freqBinMin = (int)GetFFTBinFromPercent(fftFreqMaskMin);
					int freqBinMax = (int)GetFFTBinFromPercent(fftFreqMaskMax);

					int rawSpecLength = 1 + freqBinMax - freqBinMin;	// Add one because even if they're the same, there should be a single value.

					if (fftPreviewTex.height <= maxNumBars)
					{
						// Resize the spectrum array if necessary.
						if (fftPrevSpectrum == null || fftPrevSpectrum.Length != rawSpecLength)
						{
							fftPrevSpectrum = new float[rawSpecLength];
						}

						// Copy data over.
						for (int texIdx = freqBinMin, specIdx = 0; texIdx <= freqBinMax; ++texIdx, ++specIdx)
						{
							fftPrevSpectrum[specIdx] = fftPreviewTex.GetPixel(mousePosX, texIdx).a;
						}
					}
					else
					{
						// Averaging required.
						int valsPerBin = Mathf.CeilToInt((float)rawSpecLength / maxNumBars);
						float numAvgBins = (float)rawSpecLength / valsPerBin;
						int numTotalAverages = Mathf.CeilToInt(numAvgBins);
						int numFullAverages = Mathf.FloorToInt(numAvgBins);
						
						// Resize processing if necessary.
						if (fftPrevSpectrum == null || fftPrevSpectrum.Length != numTotalAverages)
						{
							fftPrevSpectrum = new float[numTotalAverages];
						}
						
						int entryPos = freqBinMin;
						int idx = 0;
						
						// Grab all the full entries.
						for (; idx < numFullAverages; ++idx, entryPos += valsPerBin)
						{
							float total = 0f;
							for (int j = 0; j < valsPerBin; ++j)
							{
								total += fftPreviewTex.GetPixel(mousePosX, entryPos + j).a;
							}
							
							fftPrevSpectrum[idx] = total / valsPerBin;
						}
						
						// Fill in the last non-full entry, if necessary.
						if (entryPos <= freqBinMax)
						{
							float total = 0f;
							int numInTotal = 0;
							for (; entryPos <= freqBinMax; ++entryPos, ++numInTotal)
							{
								total += fftPreviewTex.GetPixel(mousePosX, entryPos).a;
							}
							
							// At this point, idx should be (processedSpec.Count - 1).
							fftPrevSpectrum[idx] = total / numInTotal;
						}
					}

					Repaint();
				}
				else if (fftPrevSpectrum != null)
				{
					fftPrevSpectrum = null;
					Repaint();
				}
			}

			EditorGUI.BeginDisabledGroup(true);
			{
				// Spectrogram Preview Infos.
				EditorGUILayout.BeginHorizontal();
				{
					// NUM BUCKETS.
					GUILayout.Label(fftNumBinsLabelContent, prevTexInfoStyle);
					int numBuckets = 1 + (int)GetFFTBinFromPercent(fftFreqMaskMax) - (int)GetFFTBinFromPercent(fftFreqMaskMin);	// +1 because we're inclusive of ends.
					fftNumBinsValueContent.text = numBuckets.ToString();
					GUILayout.Label(fftNumBinsValueContent, EditorStyles.miniLabel);

					GUILayout.FlexibleSpace();

					// FFTS PER MIN.
					GUILayout.Label(fftsPerMinLabelContent, prevTexInfoStyle);
					fftsPerMinValueContent.text = (fftsPerSecond * 60f).ToString();
					GUILayout.Label(fftsPerMinValueContent, prevTexInfoStyle);

					GUILayout.FlexibleSpace();

					// ESTIMATED SIZE.
					GUILayout.Label(fftOutputSizeLabelContent, prevTexInfoStyle);
					// For MiB calculation, 1024 * 1024 = 1048576
					// Always guarantee at least one bucket for sizing.  The Min test forces enough samples for a final pass with the configured window size.
					long numPasses = (long)(((double)(Mathf.Min(EndSample, editorWin.EditClip.samples - fftWindowSize) - StartSample) / (double)editorWin.EditClip.frequency) * fftsPerSecond) + 1;
					long bytes = numBuckets * sizeof(float) * numPasses;
					fftOutputSizeValueContent.text = "<b>~</b>" + ((bytes > 1048576) ? ((bytes / 1048576) + "MiB") : (bytes > 1024) ? ((bytes / 1024) + "KiB") : (bytes + "B"));
					GUILayout.Label(fftOutputSizeValueContent, prevTexInfoStyle);
				}
				EditorGUILayout.EndHorizontal();
			}
			EditorGUI.EndDisabledGroup();

			// Spectrum Preview.
			{
				EditorGUILayout.LabelField(fftPrevSpectrumContent, EditorStyles.boldLabel);
				Rect spectrumRect = GUILayoutUtility.GetLastRect();
				spectrumRect.xMin += EditorGUIUtility.labelWidth;

				KoreographerGUIUtils.DrawSpectrumGUI(spectrumRect, fftPrevSpectrum);
			}

			GUILayout.FlexibleSpace();

			EditorGUILayout.BeginHorizontal();
			{
				GUILayout.FlexibleSpace();
				GUILayout.Label(fftProcessContent, EditorStyles.boldLabel);
				GUILayout.FlexibleSpace();
			}
			EditorGUILayout.EndHorizontal();

			if (editorWin.EditClip.samples - StartSample < fftWindowSize)
			{
				EditorGUILayout.HelpBox(FFTInvalidSettings, MessageType.Warning);
			}
			else
			{
				EditorGUILayout.BeginHorizontal();
				{
					if (GUILayout.Button(overwriteTrackLabel))
					{
						DoFFT(StartSample, EndSample, true);
					}
					if (GUILayout.Button(appendTrackLabel))
					{
						DoFFT(StartSample, EndSample, false);
					}
				}
				EditorGUILayout.EndHorizontal();
			}
		}

		void UpdateFFTWindowSize()
		{
			// Fix up the window size.
			fftWindowSize = (int)System.Math.Pow(2, fftWindowPower);
			fftWindowHalfSize = fftWindowSize / 2;

			// Adjust bin frequency width.
			fftBinWidth = (float)editorWin.EditClip.frequency / (float)fftWindowSize;

			// Init FFT bins.
			if (fftRealValues == null || fftRealValues.Length != fftWindowSize)
			{
				fftRealValues = new double[fftWindowSize];
				fftImagValues = new double[fftWindowSize];
				fftResultSpectrum = new double[fftWindowHalfSize];
			}

			// Update the FFT Window if necessary.
			UpdateFFTWindowFunction();

			// Prewarm the FFT Object.
			fftObj.init((uint)fftWindowPower);

			// Resize the Preview Texture.
			fftPreviewTex.Resize((int)(position.width - GUI.skin.verticalScrollbar.fixedWidth), fftWindowHalfSize);
		}

		void UpdateFFTWindowFunction()
		{
			if (fftWindowCoefficients == null || fftWindowCoefficients.Length != fftWindowSize)
			{
				fftWindowCoefficients = new double[fftWindowSize];
			}

			switch (fftWindowFunc)
			{
			case FFTWindowFunc.Rectangular:
				AudioAnalysis.BuildRectangularWindow(fftWindowCoefficients);
				break;
			case FFTWindowFunc.Hann:
				AudioAnalysis.BuildHannWindow(fftWindowCoefficients);
				break;
			case FFTWindowFunc.Hamming:
				AudioAnalysis.BuildHammingWindow(fftWindowCoefficients);
				break;
			default:
				break;
			}
		}

		void UpdateFFTPreviewTexture()
		{
			AudioClip clip = editorWin.EditClip;

			// Init audio buffer.
			if (fftAudioSamples == null || fftAudioSamples.Length != fftWindowSize * clip.channels)
			{
				fftAudioSamples = new float[fftWindowSize * clip.channels];
			}

			int freqMin = (int)GetFFTBinFromPercent(fftFreqMaskMin);
			int freqMax = (int)GetFFTBinFromPercent(fftFreqMaskMax);

			Color[] pixels = fftPreviewTex.GetPixels();

			int sampleStride = Mathf.Min(Mathf.FloorToInt((float)clip.frequency / fftsPerSecond), clip.samples);

			int numIterations = Mathf.Min(fftPreviewTex.width, Mathf.FloorToInt((float)clip.samples / (float)sampleStride));

			for (int i = 0, offset = 0; i < numIterations; i++, offset += sampleStride)
			{
				// Get the samples.
				clip.GetData(fftAudioSamples, offset); //sampleStart + (i * windowSize));
				System.Array.Clear(fftImagValues, 0, fftImagValues.Length);
				
				// Get sample data into FFT buffer.
				for (int rIdx = 0, sIdx = channelIdx; sIdx < fftAudioSamples.Length; rIdx++, sIdx += clip.channels)
				{
					fftRealValues[rIdx] = fftAudioSamples[sIdx] * fftWindowCoefficients[rIdx];
				}
				
				// Run the second FFT!
				fftObj.run(fftRealValues, fftImagValues);
				
				// Magnitudes are overwritten.
				AudioAnalysis.CalculateFFTDecibels(fftRealValues, fftImagValues, fftResultSpectrum);

				// TODO: Push the empty row of pixels to the top (this is for the ignored DC0 bin).
				//  For now, set the empty row to filled when there is a column, and empty when there
				//  isn't one.
				pixels[i].a = 0f;

				// Start at 1 as this skips the 0hz value.
				int j = 1;
				for (; j < (int)freqMin; ++j)
				{
					pixels[i + j * fftPreviewTex.width].a = 1f;
				}
				
				for (; j <= (int)freqMax; ++j)
				{
					float mag = (fftGain + fftRange + (float)fftResultSpectrum[j]) / fftRange;
					pixels[i + j * fftPreviewTex.width].a = mag;
				}

				// Fill for (freqMax, fftWindowHalfSize).
				for (; j < fftWindowHalfSize; ++j)
				{
					pixels[i + j * fftPreviewTex.width].a = 1f;
				}
			}

			// Fill in any leftover pixels with 0s.
			if (numIterations < fftPreviewTex.width)
			{
				for (int i = numIterations; i < fftPreviewTex.width; ++i)
				{
					for (int j = 0; j < fftPreviewTex.height; ++j)
					{
						// Get this right......
						pixels[i + j * fftPreviewTex.width].a = 0f;
					}
				}
			}

			fftPreviewTex.SetPixels(0, 0, fftPreviewTex.width, fftPreviewTex.height, pixels);
			fftPreviewTex.Apply();
		}

		float GetFFTBinFromPercent(float percent)
		{
			return Mathf.Round(Mathf.Lerp(1f, (float)fftWindowHalfSize - 1, percent));
		}

		void DoFFT(int startSample, int endSample, bool bReplace)
		{
			Undo.RecordObject(editorWin.EditTrack, "Run FFT Analysis");
			
			if (bReplace)
			{
				// Clear old events.
				editorWin.EditTrack.RemoveAllEvents();
			}

			KoreographyEvent evt = new KoreographyEvent();

			SpectrumPayload pl = new SpectrumPayload();
			List<SpectrumPayload.Spectrum> spectra = pl.SpectrumData;

			AudioClip clip = editorWin.EditClip;

			// Init audio buffer.
			float[] sampleBuffer = new float[fftWindowSize * clip.channels];
			
			int freqBinMin = (int)GetFFTBinFromPercent(fftFreqMaskMin);
			int freqBinMax = (int)GetFFTBinFromPercent(fftFreqMaskMax);

			int sampleStride = Mathf.Min((int)System.Math.Floor((double)clip.frequency / fftsPerSecond), clip.samples);
			int numIterations = 1 + (int)System.Math.Floor((double)(endSample - startSample) / (double)sampleStride);	// Add one to ensure that there's at least one done.

			// Don't center the window.  Centering would get some before/after a beat hit, for instance, muddying the data.
			//  perhaps we don't care about that for now...  Don't center, go from startSample forward.  At the end, if
			//  there is only a single FFT, make the start/end sample positions both equal the start.
			// The buttons are guarded against the range being too small.  This means that we are guaranteed to safely
			//  run through this and process at least one FFT.

			int offset = startSample;
			for (int i = 0; i < numIterations; i++, offset += sampleStride)
			{
				// Get the samples.
				clip.GetData(sampleBuffer, offset);
				System.Array.Clear(fftImagValues, 0, fftImagValues.Length);
				
				// Get sample data into FFT buffer.
				for (int rIdx = 0, sIdx = channelIdx; sIdx < sampleBuffer.Length; rIdx++, sIdx += clip.channels)
				{
					fftRealValues[rIdx] = sampleBuffer[sIdx] * fftWindowCoefficients[rIdx];
				}
				
				// Run the second FFT!
				fftObj.run(fftRealValues, fftImagValues);
				
				// Magnitudes are overwritten.
				AudioAnalysis.CalculateFFTDecibels(fftRealValues, fftImagValues, fftResultSpectrum);

				// Create locally for faster indexing.
				SpectrumPayload.Spectrum spectrumVals = new SpectrumPayload.Spectrum();

				// Store the results properly.  Be sure to include the max bin!  This allows us to store the one row
				//  if the two bins are the same number.
				for (int j = (int)freqBinMin; j <= (int)freqBinMax; ++j)
				{
					float mag = (fftGain + fftRange + (float)fftResultSpectrum[j]) / fftRange;
					spectrumVals.data.Add(Mathf.Clamp01(mag));
				}

				// Add the new Spectrum to the Spectra.
				spectra.Add(spectrumVals);
			}

			// Update the endSample to the actual location.
			endSample = offset - sampleStride;		// Back out one because of for-loop bounds.

			// Fill in the data.
			SpectrumPayload.SpectrumInfo spectrumInfo;
			spectrumInfo.startSample = startSample;
			spectrumInfo.endSample = endSample;
			spectrumInfo.binFrequencyWidth = fftBinWidth;
			spectrumInfo.minBinFrequency = fftBinWidth * freqBinMin;
			pl.SpectrumDataInfo = spectrumInfo;

			// Update and then add the Koreography Event.
			evt.EndSample = endSample;
			evt.StartSample = startSample;
			evt.Payload = pl;

			editorWin.EditTrack.AddEvent(evt);
			
			// Make sure to mark the track dirty so that the changes will be saved properly!
			EditorUtility.SetDirty(editorWin.EditTrack);
			editorWin.Repaint();
		}
		
		#endregion
	}
}

#endif	// KOREO_NON_PRO
