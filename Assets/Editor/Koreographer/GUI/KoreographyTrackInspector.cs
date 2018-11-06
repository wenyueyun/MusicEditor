//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace SonicBloom.Koreo.EditorUI.UnityTools
{
	/// <summary>
	/// The custom Editor for Koreography Tracks.  This class customizes
	/// Koreography Tracks in the Inspector.
	/// </summary>
	[CustomEditor(typeof(KoreographyTrackBase), true)]
	[CanEditMultipleObjects()]
	public class KoreographyTrackInspector : Editor
	{
		KoreographyTrackBase thisTrack = null;

		SerializedProperty eventIDProp = null;
		SerializedProperty eventsProp = null;
		
		List<Koreography> koreos = new List<Koreography>();

		int selIdx = 0;
		Vector2 scrollPos = Vector2.zero;
	
		void OnEnable()
		{
			thisTrack = target as KoreographyTrackBase;

			eventIDProp = serializedObject.FindProperty("mEventID");
			eventsProp = serializedObject.FindProperty("mEventList");
		}
	
		public override void OnInspectorGUI()
		{
			if (targets.Length == 1)
			{
				// Allow users to search for Koreography this track is connected to.
				//  The search is heavy so we make it optional.
				if(GUILayout.Button("Search for Connected Koreography"))
				{
					FindAssociatedKoreography();
				}

				// Grab the names of the Koreography with this track.
				string[] names = new string[koreos.Count];

				for (int i = 0; i < names.Length; ++i)
				{
					names[i] = koreos[i].name;
				}

				scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(50f));

				// Show the list of viable options.
				selIdx = GUILayout.SelectionGrid(selIdx, names, 1, EditorStyles.radioButton, GUILayout.MaxWidth(Screen.width - 20f));

				EditorGUILayout.EndScrollView();

				// Eventually do this with a GUIStyles in the skin.
				KoreographerGUIUtils.DrawOutlineAroundLastControl(Color.black);

				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();

				// Only show the Open button as active if we have a valid selection.
				GUI.enabled = names.Length > 0;
				if (GUILayout.Button("Open With Selected Koreography"))
				{
					KoreographyEditor.OpenKoreography(koreos[selIdx], target as KoreographyTrackBase);
				}
				GUI.enabled = true;

				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				GUILayout.Space(5f);

				// TODO: Add a button to create a new Keography object with this Koreography Track?
			}

			// Doing this gets rid of the stupid "Script" field.

			EditorGUI.BeginChangeCheck();
			{
				EditorGUILayout.PropertyField(eventIDProp);
				EditorGUILayout.PropertyField(eventsProp, true);
			}
			if (EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();
			}
		}

		void FindAssociatedKoreography()
		{
			if (targets.Length == 1)
			{
				// Find all Koreography in the Asset Database.
				string[] guids = AssetDatabase.FindAssets("t:Koreography");
				
				// Filter out the Koreography to only those that contain this Track.
				for (int i = 0; i < guids.Length; ++i)
				{
					string guid = guids[i];
					
					Koreography koreo = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), typeof(Koreography)) as Koreography;
					
					if (koreo != null && koreo.GetIndexOfTrack(thisTrack) >= 0)
					{
						koreos.Add(koreo);
					}
				}
			}
		}
	}
}
