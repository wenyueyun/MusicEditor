//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEditor;
using UnityEngine;

using SonicBloom.Koreo.Players;

namespace SonicBloom.Koreo.EditorUI.UnityTools
{
	/// <summary>
	/// Custom Property Drawer for the <c>MusicLayer</c>.  This class
	/// customizes the representation of <c>MusicLayer</c>s when used
	/// as a property of other classes, behaviours, etc.
	/// </summary>
	[CustomPropertyDrawer(typeof(MusicLayer))]
	public class MusicLayerPropertyDrawer : PropertyDrawer
	{
		static GUIContent koreoLabel	= new GUIContent("Koreography", "The Koreography for this layer.");
		static GUIContent clipLabel		= new GUIContent("Audio Clip", "The AudioClip for this layer.");
		static GUIContent sourceLabel	= new GUIContent("Audio Source", "The AudioSource to use to play back this layer.");
		static GUIContent nameLabel		= new GUIContent("Name", "The name of this layer.");
		static GUIContent optionLabel	= new GUIContent("Optional:");
		static GUIContent musicLabel	= new GUIContent("Music Source:");
		static GUIContent buttonLabel	= new GUIContent("Clear", "Clear the current Music Source settings.");

		static GUIStyle boldFoldoutStyle = null;
	
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (boldFoldoutStyle == null)
			{
				boldFoldoutStyle = new GUIStyle(EditorStyles.foldout);
				boldFoldoutStyle.fontStyle = FontStyle.Bold;
			}

			EditorGUI.BeginProperty(position, label, property);

			float verticalSpacing = GetVerticalSpacing();

			position.height = EditorGUIUtility.singleLineHeight;

			EditorGUI.PropertyField(position, property, label);

			if (property.isExpanded)
			{
				SerializedProperty koreoProp = property.FindPropertyRelative("koreography");
				SerializedProperty clipProp = property.FindPropertyRelative("audioClip");
				SerializedProperty sourceProp = property.FindPropertyRelative("audioSource");
				SerializedProperty nameProp = property.FindPropertyRelative("name");

				position.y += verticalSpacing;

				{
					float buttonWidth = 45f;
					Rect posInRow = new Rect(position);
					posInRow.width -= buttonWidth;

					EditorGUI.LabelField(posInRow, musicLabel, EditorStyles.boldLabel);

					posInRow.xMin = posInRow.xMax;
					posInRow.width = buttonWidth;

					GUI.enabled = (koreoProp.objectReferenceValue != null || clipProp.objectReferenceValue != null);
					if (GUI.Button(posInRow, buttonLabel))
					{
						koreoProp.objectReferenceValue = null;
						clipProp.objectReferenceValue = null;
					}
					GUI.enabled = true;
				}

				position.y += verticalSpacing;

				EditorGUI.indentLevel++;
				{
					if (clipProp.objectReferenceValue == null)
					{
						EditorGUI.PropertyField(position, koreoProp, koreoLabel);
						position.y += verticalSpacing;
					}

					if (koreoProp.objectReferenceValue == null)
					{
						EditorGUI.PropertyField(position, clipProp, clipLabel);
						position.y += verticalSpacing;
					}
				
					sourceProp.isExpanded = EditorGUI.Foldout(position, sourceProp.isExpanded, optionLabel, boldFoldoutStyle);

					if (sourceProp.isExpanded)
					{
						position.y += verticalSpacing;

						EditorGUI.indentLevel++;
						{
							// Indent a bit more for readability.
							float extraIndent = 10f;
							EditorGUIUtility.labelWidth += extraIndent;

							EditorGUI.PropertyField(position, nameProp, nameLabel);
							position.y += verticalSpacing;

							EditorGUI.PropertyField(position, sourceProp, sourceLabel);

							EditorGUIUtility.labelWidth -= extraIndent;
						}
						EditorGUI.indentLevel--;
					}
				}
				EditorGUI.indentLevel--;
			}

			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			// Subtract standardVerticalSpacing from the end as we don't space between ourselves and the next property.
			return ((property.isExpanded ? GetNumRowsExpanded(property) : 1) * GetVerticalSpacing()) - EditorGUIUtility.standardVerticalSpacing;
		}

		float GetVerticalSpacing()
		{
			return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
		}

		float GetNumRowsExpanded(SerializedProperty property)
		{
			bool hasKoreo = property.FindPropertyRelative("koreography").objectReferenceValue != null;
			bool hasClip = property.FindPropertyRelative("audioClip").objectReferenceValue != null;

			float rows = 3f; 			// Base + 2 labels

			if (hasKoreo || hasClip)	// Music Source Options
			{
				rows++;
			}
			else
			{
				rows += 2f;
			}

			if (property.FindPropertyRelative("audioSource").isExpanded)			// Whether options are open or not.
			{
				rows += 2f;
			}

			return rows;
		}
	}
}
