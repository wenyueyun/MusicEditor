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
	/// Custom Property Drawer for the <c>EventIDAttribute</c>.  This
	/// class customizes the representation of fields marked with
	/// either [EventID] or [EventIDAttribute].
	/// </summary>
	[CustomPropertyDrawer(typeof(EventIDAttribute))]
	public class EventIDPropertyDrawer : PropertyDrawer
	{
		#region Static Fields

		static string TooltipText = "An Event ID that corresponds to a Koreography Track. The dropdown field contains a list of options found in the project. Press the 'R' button to the right to refresh this list if you don't see an ID you expect or simply write in a custom ID.";
		static GUIContent RefreshButtonContent	= new GUIContent("R", "Refresh the list of available Event IDs from KoreographyTracks in the project.");
		static GUIContent WrongTypeContent		= new GUIContent("Wrong Field Type for the EventIDAttribute!", "The EventIDAttribute only works on string fields.  Please fix the script!");

		static List<string> AvailableEventIDs = null;

		#endregion
		#region Static Methods

		static List<string> GetAllEventIDsInProject()
		{
			// Find all Koreography in the Asset Database.
			string[] guids = AssetDatabase.FindAssets("t:KoreographyTrackBase");

			List<string> ids = new List<string>();

			for (int i = 0; i < guids.Length; ++i)
			{
				KoreographyTrackBase track = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guids[i]), typeof(KoreographyTrackBase)) as KoreographyTrackBase;

				string id = track.EventID;

				if (track != null &&
					!string.IsNullOrEmpty(id) &&
					!ids.Contains(id))
				{
					ids.Add(track.EventID);
				}
			}

			return ids;
		}

		#endregion
		#region Methods

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (property.propertyType == SerializedPropertyType.String)
			{
				// Take a look at the current value.
				string curValue = property.stringValue;

				// Collect all the possible EventIDs from the KoreographyTracks in the project.
				//  Note: This is a really heavy operation.  Only do this when the user requests it (or first-pass).
				if (AvailableEventIDs == null)
				{
					AvailableEventIDs = GetAllEventIDsInProject();
				}

				List<string> eventIDs = AvailableEventIDs;

				// Sort the list alphabetically.
				eventIDs.Sort();

				// Find the index of the current value in the set of available EventIDs.
				int idx = eventIDs.IndexOf(curValue);

				// Init the Rects for the three pieces we'll be showing:
				//  - Standard Property Field (string).
				//  - Dropdown for event selection.
				//  - Button to refresh the list of available 
				float buttonWidth = 19f;
				float fieldWidth = ((position.width - EditorGUIUtility.labelWidth) - buttonWidth) / 2f;


				Rect propPos = new Rect(position);
				propPos.width = EditorGUIUtility.labelWidth + fieldWidth;

				Rect popupPos = new Rect(propPos);
				popupPos.width = fieldWidth;
				popupPos.x += propPos.width;

				Rect buttonPos = new Rect(popupPos);
				buttonPos.width = buttonWidth;
				buttonPos.x += popupPos.width;

				// The label for this property.
				{
					if (string.IsNullOrEmpty(label.tooltip))
					{
						label.tooltip = EventIDPropertyDrawer.TooltipText;
					}

					EditorGUI.PropertyField(propPos, property, label);

					// Reset the tooltip text so that subsequent fields don't end up with it.
					label.tooltip = string.Empty;
				}

				// Dropdown.
				{
					// Greater than one because we always insert a "Custom" option to the beginning.
					EditorGUI.BeginDisabledGroup(eventIDs.Count <= 0);
					{
						EditorGUI.BeginChangeCheck();
						int newIdx = EditorGUI.Popup(popupPos, idx, eventIDs.ToArray());
						if (EditorGUI.EndChangeCheck())
						{
							property.stringValue = eventIDs[newIdx];
						}
					}
					EditorGUI.EndDisabledGroup();
				}

				// Refresh button.
				{
					// The GUI for this button is enabled based on the parent settings.

					if (GUI.Button(buttonPos, RefreshButtonContent))
					{
						EventIDPropertyDrawer.AvailableEventIDs = GetAllEventIDsInProject();
					}
				}
			}
			else
			{
				EditorGUI.LabelField(position, label, EventIDPropertyDrawer.WrongTypeContent);
			}
		}

		#endregion
	}
}
