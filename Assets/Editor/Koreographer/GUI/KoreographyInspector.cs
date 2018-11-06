//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEditor;
using UnityEngine;

namespace SonicBloom.Koreo.EditorUI.UnityTools
{
	/// <summary>
	/// The custom Editor for Koreography.  This class customizes Koreography
	/// in the Inspector.
	/// </summary>
	[CustomEditor(typeof(Koreography))]
	[CanEditMultipleObjects()]
	public class KoreographyInspector : Editor
	{
		// Properties
		SerializedProperty clipProp = null;
		SerializedProperty audioPathProp = null;
		SerializedProperty sampleRateProp = null;
		SerializedProperty ignoreOffsetProp = null;
		SerializedProperty tempoProp = null;
		SerializedProperty tracksProp = null;

		void OnEnable()
		{
			clipProp = serializedObject.FindProperty("mSourceClip");
			audioPathProp = serializedObject.FindProperty("mAudioFilePath");
			sampleRateProp = serializedObject.FindProperty("mSampleRate");
			ignoreOffsetProp = serializedObject.FindProperty("mIgnoreLatencyOffset");
			tempoProp = serializedObject.FindProperty("mTempoSections");
			tracksProp = serializedObject.FindProperty("mTracks");
		}

		public override void OnInspectorGUI()
		{
			// Add the convenience button if only one exists.
			if (targets.Length == 1)
			{
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();

				if (GUILayout.Button("Open In Koreography Editor"))
				{
					KoreographyEditor.OpenKoreography(target as Koreography);
				}

				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				GUILayout.Space(5f);
			}

			EditorGUI.BeginChangeCheck();
			{
				Object clip = clipProp.objectReferenceValue;
				string file = audioPathProp.stringValue;

				if (clip != null || string.IsNullOrEmpty(file))
				{
					EditorGUILayout.PropertyField(clipProp);
				}
				if ((KoreographyEditor.ShowAudioFileImportOption && clip == null) || !string.IsNullOrEmpty(file))
				{
					EditorGUILayout.PropertyField(audioPathProp);
				}

				EditorGUILayout.PropertyField(sampleRateProp);
				EditorGUILayout.PropertyField(ignoreOffsetProp);
				EditorGUILayout.PropertyField(tempoProp, true);
				EditorGUILayout.PropertyField(tracksProp, true);
			}
			if (EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();
			}
		}
	}
}
