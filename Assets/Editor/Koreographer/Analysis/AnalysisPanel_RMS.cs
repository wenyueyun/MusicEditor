//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

#if !KOREO_NON_PRO

using UnityEditor;
using UnityEngine;

namespace SonicBloom.Koreo.EditorUI
{
	internal partial class AnalysisPanel
	{
		#region Static Fields

		static string warnRMSNoPayloadSupport = "The currently selected Koreography Track does not support the necessary types for this feature.  Please select a Koreography Track that supports the Curve and Float Payload types.";
		
		static string RMSExplanation = "RMS (Root Mean Square) provides an average of \"loudness\" of a certain set of audio samples.  Use the settings below to configure an RMS calculation to generate a curve (or series of OneOffs) that follows the relative loudness of the audio clip.";

		static GUIContent evalFreqContent			= new GUIContent("Evaluation Frequency", "The number of data points evaluated in a given sample range.  Increasing this value will reduce the number of data points to be calculated in the selected range (one of every 'n' available).  The current zoom level of the Koreography Editor determines the number of samples to calculate per point (see Samples Per Point below).");
		static GUIContent samplePackContent			= new GUIContent("Samples Per Point", "The number of audio samples to run RMS over for a single datapoint.  This is the same number of audio samples used to create a single peak in the RMS waveform.  The value can be changed by zooming in or out of the waveform in the Koreography Editor.");
		static GUIContent samplePackValContent		= new GUIContent("");
		
		static GUIContent outputTypeContent			= new GUIContent("Payload Type", "Set the Payload type for the output of the algorithm.");
		static GUIContent rmsOutputRangeContent		= new GUIContent("Value Range", "The range of values to output.  Default is [0,1].");
		static GUIContent rmsOutputRangeMinContent	= new GUIContent("Min", "The lower bound value to use for RMS output.  Must be less than Max.");
		static GUIContent rmsOutputRangeMaxContent	= new GUIContent("Max", "The upper bound value to use for RMS output.  Must be greater than Min");
		
		static GUIContent rmsProcessContent			= new GUIContent("Process RMS", "Uses the settings above to process the audio and generate Payloads.");

		#endregion
		#region Fields

		int rmsEvalFrequency = 4;

		// Output stuff.
		enum RMSOutputType
		{
			Curve,
			OneOffs,
		}
		RMSOutputType rmsPayloadSetting = RMSOutputType.Curve;
		
		Vector2 rmsOutputRange = new Vector2(0f, 1f);

		#endregion
		#region Methods

		void DoRMSGUI()
		{
			if (!editorWin.DoesCurrentTrackSupportPayloadType(typeof(CurvePayload)) ||
			    !editorWin.DoesCurrentTrackSupportPayloadType(typeof(FloatPayload)))
			{
				// Show warning.
				EditorGUILayout.HelpBox(warnRMSNoPayloadSupport, MessageType.Error);
			}
			else
			{
				// All good, dive in.
				DoRMSMainGUI();
			}
		}

		void DoRMSMainGUI()
		{
			EditorGUILayout.HelpBox(RMSExplanation, MessageType.Info);
			
			rmsEvalFrequency = EditorGUILayout.IntField(evalFreqContent, rmsEvalFrequency);
			rmsEvalFrequency = Mathf.Max(1, rmsEvalFrequency);	// Ensure a minimum value of 1.
			EditorGUI.indentLevel++;
			{
				EditorGUI.BeginDisabledGroup(true);
				{
					samplePackValContent.text = editorWin.DisplayState.samplesPerPack.ToString();
					EditorGUILayout.LabelField(samplePackContent, samplePackValContent);
				}
				EditorGUI.EndDisabledGroup();
			}
			EditorGUI.indentLevel--;
			
			GUILayout.Label(outputSettingsContent, EditorStyles.boldLabel);
			
			EditorGUI.indentLevel++;
			{
				rmsPayloadSetting = (RMSOutputType)EditorGUILayout.EnumPopup(outputTypeContent, (System.Enum)rmsPayloadSetting);
			}
			EditorGUI.indentLevel--;
			
			// Output range Min & Max setting.
			EditorGUILayout.BeginHorizontal();
			{
				EditorGUI.indentLevel++;
				{
					EditorGUILayout.LabelField(rmsOutputRangeContent, GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
				}
				EditorGUI.indentLevel--;
				
				EditorGUIUtility.labelWidth = 30f;
				
				float floatVal = EditorGUILayout.FloatField(rmsOutputRangeMinContent, rmsOutputRange.x);
				if (floatVal < rmsOutputRange.y)
				{
					rmsOutputRange.x = floatVal;
				}
				
				GUILayout.Space(5f);
				
				floatVal = EditorGUILayout.FloatField(rmsOutputRangeMaxContent, rmsOutputRange.y);
				if (floatVal > rmsOutputRange.x)
				{
					rmsOutputRange.y = floatVal;
				}
				
				EditorGUIUtility.labelWidth = 0f;	// Restore default.
			}
			EditorGUILayout.EndHorizontal();
			
			GUILayout.FlexibleSpace();
			
			EditorGUILayout.BeginHorizontal();
			{
				GUILayout.FlexibleSpace();
				GUILayout.Label(rmsProcessContent, EditorStyles.boldLabel);
				GUILayout.FlexibleSpace();
			}
			EditorGUILayout.EndHorizontal();
			
			EditorGUILayout.BeginHorizontal();
			{
				if (GUILayout.Button(overwriteTrackLabel))
				{
					DoRMS(StartSample, EndSample, true);
				}
				if (GUILayout.Button(appendTrackLabel))
				{
					DoRMS(StartSample, EndSample, false);
				}
			}
			EditorGUILayout.EndHorizontal();
		}
		
		void DoRMS(int startSample, int endSample, bool bReplace)
		{
			int samplesPerPack = editorWin.DisplayState.samplesPerPack;
			int channelCount = editorWin.EditClip.channels;
			
			int samplesToRead = endSample - startSample;
			float[] rawSamples = new float[samplesToRead * channelCount];
			editorWin.EditClip.GetData(rawSamples, startSample);
			
			// Deinterleave samples.  This gets the first channel.
			float[] chanSamples = new float[samplesToRead];
			for (int i = 0, j = channelIdx; i < chanSamples.Length; ++i, j += channelCount)
			{
				chanSamples[i] = rawSamples[j];
			}
			
			// TODO: Handle the end!  This truncates...
			int numPayloads = chanSamples.Length / samplesPerPack;
			float[] sampleWindow = new float[samplesPerPack];
			
			Undo.RecordObject(editorWin.EditTrack, "Run RMS Analysis");
			
			if (bReplace)
			{
				// Clear old events.
				editorWin.EditTrack.RemoveAllEvents();
			}
			
			if (rmsPayloadSetting == RMSOutputType.OneOffs)
			{
				// Fill current Koreography Track with OneOffs.
				
				for (int payloadIdx = 0, sampleIdx = 0;
				     payloadIdx < numPayloads && sampleIdx + sampleWindow.Length <= chanSamples.Length;
				     payloadIdx += rmsEvalFrequency, sampleIdx += (samplesPerPack * rmsEvalFrequency))
				{
					System.Array.Copy(chanSamples, sampleIdx, sampleWindow, 0, sampleWindow.Length);
					
					float rms = AudioAnalysis.ComputeRMS(sampleWindow, samplesPerPack);
					
					FloatPayload pl = new FloatPayload();
					pl.FloatVal = Mathf.Lerp(rmsOutputRange.x, rmsOutputRange.y, rms);
					
					KoreographyEvent evt = new KoreographyEvent();
					evt.StartSample = startSample + sampleIdx;
					evt.Payload = pl;
					
					editorWin.EditTrack.AddEvent(evt);
				}
			}
			else
			{
				// Fill current Koreography Track with a single AnimationCurve.
				
				KoreographyEvent evt = new KoreographyEvent();
				evt.StartSample = startSample;
				
				CurvePayload pl = new CurvePayload();
				pl.CurveData = new AnimationCurve();
				
				int lastSample = 0;
				for (int payloadIdx = 0, sampleIdx = 0;
				     payloadIdx < numPayloads && sampleIdx + sampleWindow.Length <= chanSamples.Length;
				     payloadIdx += rmsEvalFrequency, sampleIdx += (samplesPerPack * rmsEvalFrequency))
				{
					System.Array.Copy(chanSamples, sampleIdx, sampleWindow, 0, sampleWindow.Length);
					
					float rms = AudioAnalysis.ComputeRMS(sampleWindow, samplesPerPack);
					
					Keyframe newKey = new Keyframe();
					newKey.time = (float)payloadIdx / (float)numPayloads;
					newKey.value = Mathf.Lerp(rmsOutputRange.x, rmsOutputRange.y, rms);
					pl.CurveData.AddKey(newKey);
					lastSample = startSample + sampleIdx + samplesPerPack - 1;
				}
				
				evt.EndSample = lastSample;
				evt.Payload = pl;
				
				editorWin.EditTrack.AddEvent(evt);
			}
			
			// Make sure to mark the track dirty so that the changes will be saved properly!
			EditorUtility.SetDirty(editorWin.EditTrack);
			editorWin.Repaint();
		}

		#endregion
	}
}

#endif	// KOREO_NON_PRO
