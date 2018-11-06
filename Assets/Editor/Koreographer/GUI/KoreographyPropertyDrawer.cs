//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEditor;
using UnityEngine;

namespace SonicBloom.Koreo.EditorUI.UnityTools
{
	/// <summary>
	/// Custom Property Drawer for Koreography.  This class customizes
	/// the representation of Koreography when used as a property of
	/// other classes, behaviours, etc.
	/// </summary>
	[CustomPropertyDrawer(typeof(Koreography))]
	public class KoreographyPropertyDrawer : PropertyDrawer
	{
		GUIContent editContent = new GUIContent("E", "Edit this Koreography");
		GUIContent newContent = new GUIContent("N", "Create and edit new Koreography");

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			Rect fieldRect = new Rect(position);
			fieldRect.width -= 24f;
			Rect buttonRect = new Rect(position);
			buttonRect.xMin = fieldRect.xMax + 5;
			buttonRect.width = 19f;

			// Don't immediately set the result. That will break multi-object editing by replacing
			//  all selected objects' fields with a single value! Check for user-driven change first.
			Object selection = null;

			EditorGUI.BeginChangeCheck();
			{
				// Handle multiple values.
				EditorGUI.showMixedValue = property.hasMultipleDifferentValues;

				// Use ObjectField to turn off scene object lookup.
				selection = EditorGUI.ObjectField(fieldRect, label, property.objectReferenceValue, typeof(Koreography), false);
				
				// Reset handling of multiple values.
				EditorGUI.showMixedValue = false;
			}
			if (EditorGUI.EndChangeCheck())
			{
				property.objectReferenceValue = selection;
			}

			// If there is no Koreography in this field, offer to create a new one
			//  (loading is handled above).  Otherwise, offer to open the current 
			//  Koreography in the editor.
			// If there are multiple different values (multi-object-editing) then
			//  offer to create a New Koreography that would get set to both.
			if (property.objectReferenceValue == null || property.hasMultipleDifferentValues)
			{
				if (GUI.Button(buttonRect, newContent))
				{
					Koreography newKoreo = KoreographyEditor.CreateNewKoreography();

					if (newKoreo != null)
					{
						property.objectReferenceValue = newKoreo;

						KoreographyEditor.OpenKoreography(newKoreo);
					}
				}
			}
			else
			{
				if (GUI.Button(buttonRect, editContent))
				{
					KoreographyEditor.OpenKoreography(property.objectReferenceValue as Koreography);
				}
			}
		}
	}
}
