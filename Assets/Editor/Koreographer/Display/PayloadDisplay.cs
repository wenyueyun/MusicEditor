//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEditor;
using UnityEngine;

namespace SonicBloom.Koreo.EditorUI
{
	/// <summary>
	/// Handles drawing the Editor GUI for payloads.  In early Koreographer builds,
	///  this functionality was implemented with inheritance of the Payload itself.
	///  Due to packaging requirements in Unity 4.5/4.6, this method is not viable.
	///  As such we use the dispatch method that "simulates" the vtable.  This is
	///  ever-so-slightly slower (testing on Mid-2012 MBP with Retina reveals ~1.6%
	///  slower).
	/// </summary>
	internal static class PayloadDisplay
	{
		public static bool DoGUI(IPayload payload, Rect displayRect, KoreographyTrackBase track, bool isSelected)
		{
			AssetPayload assetPayload = payload as AssetPayload;
			if (assetPayload != null)
			{
				return DoGUI(assetPayload, displayRect, track, isSelected);
			}

			ColorPayload colorPayload = payload as ColorPayload;
			if (colorPayload != null)
			{
				return DoGUI(colorPayload, displayRect, track, isSelected);
			}

			CurvePayload curvePayload = payload as CurvePayload;
			if (curvePayload != null)
			{
				return DoGUI(curvePayload, displayRect, track, isSelected);
			}

			FloatPayload floatPayload = payload as FloatPayload;
			if (floatPayload != null)
			{
				return DoGUI(floatPayload, displayRect, track, isSelected);
			}

			GradientPayload gradientPayload = payload as GradientPayload;
			if (gradientPayload != null)
			{
				return DoGUI(gradientPayload, displayRect, track, isSelected);
			}

			IntPayload intPayload = payload as IntPayload;
			if (intPayload != null)
			{
				return DoGUI(intPayload, displayRect, track, isSelected);
			}

			SpectrumPayload spectrumPayload = payload as SpectrumPayload;
			if (spectrumPayload != null)
			{
				return DoGUI(spectrumPayload, displayRect, track, isSelected);
			}

			// Fall through.
			return DoGUI(payload as TextPayload, displayRect, track, isSelected);
		}

		public static bool DoGUI(AssetPayload payload, Rect displayRect, KoreographyTrackBase track, bool isSelected)
		{
			bool bDidEdit = false;
			Color originalBG = GUI.backgroundColor;
			GUI.backgroundColor = isSelected ? Color.green : originalBG;
			
			EditorGUI.BeginChangeCheck();

			float pickerWidth = 20f;
			Object newVal = null;

			// Handle short fields.
			if (displayRect.width >= pickerWidth + 2f)
			{
				// HACK to make the background of the picker icon readable.
				Rect pickRect = new Rect(displayRect);
				pickRect.xMin = pickRect.xMax - pickerWidth;
				GUI.Box(pickRect, string.Empty, EditorStyles.textField);

				// Draw the Object field.
				newVal = EditorGUI.ObjectField(displayRect, payload.AssetVal, typeof(Object), false);
			}
			else
			{
				// Simply show a text field.
				string name = (payload.AssetVal != null) ? payload.AssetVal.name : "None (Object)";
				string tooltip = isSelected ? "Edit in the \"Selected Event Settings\" section below." : "Select this event and edit in the \"Selected Event Settings\" section below.";
				GUI.Box(displayRect, new GUIContent(name, tooltip), EditorStyles.textField);
			}
			
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(track, "Modify Asset Payload");
				payload.AssetVal = newVal;
				bDidEdit = true;
			}
			
			GUI.backgroundColor = originalBG;
			return bDidEdit;
		}

		public static bool DoGUI(ColorPayload payload, Rect displayRect, KoreographyTrackBase track, bool isSelected)
		{
			bool bDidEdit = false;
			Color originalBG = GUI.backgroundColor;
			GUI.backgroundColor = isSelected ? Color.green : originalBG;
			
			EditorGUI.BeginChangeCheck();
			Color newVal = KoreographerGUIUtils.ColorField(displayRect, payload.ColorVal, false, true);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(track, "Modify Color Payload");
				payload.ColorVal = newVal;
				bDidEdit = true;
			}
			
			GUI.backgroundColor = originalBG;
			return bDidEdit;
		}
		
		static Color CurveBGColor = new Color(100f/255f, 100f/255f, 100f/255f);
		static Color SelectedCurveBGColor = new Color(0f, 100f/255f, 0f);
		
		public static bool DoGUI(CurvePayload payload, Rect displayRect, KoreographyTrackBase track, bool isSelected)
		{
			bool bDidEdit = false;
			Color originalBG = GUI.backgroundColor;
			
			// 10,000 is the MAXIMUM width that a CurveField works at.  Try larger and it will crash Unity.
			if (displayRect.width <= 10000f)
			{
				GUI.backgroundColor = isSelected ? Color.green : originalBG;
				
				EditorGUI.BeginChangeCheck();

				// Store off a new copy of the curve.  Internally keys are an array of
				//  structs and Unity's internal system really sucks at properly handling
				//  these.  By completely duplicating the Curve and then setting it *if*
				//  a change occurs we can sidestep the bugs, which include, but are not
				//  limited to:
				//  - Addition/Deletion of keys to/from curve not recorded in Undo stack.
				//  - Potentially [based on key addition/deletion interactions] stomp
				//		all over the Undo stack (at least that's what it looks like).
				AnimationCurve dispCurve = new AnimationCurve(payload.CurveData.keys);
				dispCurve = EditorGUI.CurveField(displayRect, dispCurve);

				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(track, "Modify Curve Payload");
					payload.CurveData = dispCurve;
					bDidEdit = true;
				}
			}
			else
			{
				GUI.backgroundColor = isSelected ? SelectedCurveBGColor : CurveBGColor;
				
				GUI.Box(displayRect, string.Empty);
			}
			
			GUI.backgroundColor = originalBG;
			return bDidEdit;
		}
		
		public static bool DoGUI(FloatPayload payload, Rect displayRect, KoreographyTrackBase track, bool isSelected)
		{
			bool bDidEdit = false;
			Color originalBG = GUI.backgroundColor;
			GUI.backgroundColor = isSelected ? Color.green : originalBG;
			
			EditorGUI.BeginChangeCheck();
			float newVal = EditorGUI.FloatField(displayRect, payload.FloatVal);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(track, "Modify Float Payload");
				payload.FloatVal = newVal;
				bDidEdit = true;
			}
			
			GUI.backgroundColor = originalBG;
			return bDidEdit;
		}

		static System.Reflection.MethodInfo GradientField;
		
		public static bool DoGUI(GradientPayload payload, Rect displayRect, KoreographyTrackBase track, bool isSelected)
		{
			if (GradientField == null)
			{
				System.Type[] types = {typeof(Rect), typeof(Gradient)};
				GradientField = typeof(EditorGUI).GetMethod("GradientField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null, types, null);
			}
			
			bool bDidEdit = false;
			Color originalBG = GUI.backgroundColor;
			GUI.backgroundColor = isSelected ? Color.green : originalBG;

			// Gradient keys aren't copied correctly for Undo...
			//  Create a new object and copy the keys to get it to work properly.
			//  This is simillar to the issue seen with AnimationCurves.
			Gradient dispGradient = new Gradient();
			dispGradient.SetKeys(payload.GradientData.colorKeys, payload.GradientData.alphaKeys);
			
			EditorGUI.BeginChangeCheck();
			object[] parameters = {displayRect, dispGradient};
			dispGradient = (Gradient)GradientField.Invoke(null, parameters);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(track, "Modify Gradient Payload");
				payload.GradientData = dispGradient;
				bDidEdit = true;
			}
			
			GUI.backgroundColor = originalBG;
			return bDidEdit;
		}

		public static bool DoGUI(IntPayload payload, Rect displayRect, KoreographyTrackBase track, bool isSelected)
		{
			bool bDidEdit = false;
			Color originalBG = GUI.backgroundColor;
			GUI.backgroundColor = isSelected ? Color.green : originalBG;
			
			EditorGUI.BeginChangeCheck();
			int newVal = EditorGUI.IntField(displayRect, payload.IntVal);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(track, "Modify Int Payload");
				payload.IntVal = newVal;
				bDidEdit = true;
			}
			
			GUI.backgroundColor = originalBG;
			return bDidEdit;
		}

		static GUIContent SpectrumContent = new GUIContent();

		public static bool DoGUI(SpectrumPayload payload, Rect displayRect, KoreographyTrackBase track, bool isSelected)
		{
			Color originalBG = GUI.backgroundColor;
			GUI.backgroundColor = isSelected ? Color.green : Color.yellow;
			{
				GUIStyle labelSkin = GUI.skin.GetStyle("Label");
				TextAnchor originalAlign = labelSkin.alignment;
				labelSkin.alignment = TextAnchor.MiddleCenter;
				{
					SpectrumPayload.SpectrumInfo info = payload.SpectrumDataInfo;
					SpectrumContent.text = "[Spectrum - Start: " + info.startSample + ", End: " + info.endSample + "]";
					SpectrumContent.tooltip = SpectrumContent.text;
					GUI.Box(displayRect, SpectrumContent);
				}
				labelSkin.alignment = originalAlign;
			}
			GUI.backgroundColor = originalBG;

			return false;
		}

		public static bool DoGUI(TextPayload payload, Rect displayRect, KoreographyTrackBase track, bool isSelected)
		{
			bool bDidEdit = false;
			Color originalBG = GUI.backgroundColor;
			GUI.backgroundColor = isSelected ? Color.green : originalBG;
			
			EditorGUI.BeginChangeCheck();
			string newVal = EditorGUI.TextField(displayRect, payload.TextVal);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(track, "Modify Text Payload");
				payload.TextVal = newVal;
				bDidEdit = true;
			}
			
			GUI.backgroundColor = originalBG;
			return bDidEdit;
		}
	}
}
