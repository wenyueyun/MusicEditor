//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace SonicBloom.Koreo.EditorUI
{
	internal class EventVisualizer : EditorWindow
	{
		#region Static Fields

		static System.Type NormalCurveRendererType = typeof(EditorWindow).Assembly.GetType("UnityEditor.NormalCurveRenderer");
		static System.Reflection.MethodInfo GetCurveBounds;

		static float LabelWidth = 62f;

		static Color proSkinBGColor = new Color(0.16078f, 0.16078f, 0.16078f);

		static GUIContent numEvtContent		= new GUIContent("Num Hit", "The number of events hit this update.");
		static GUIContent payloadContent	= new GUIContent("Payloads", "Information gathered from payloads.");
		static GUIContent textContent		= new GUIContent("Text", "The text from a Text Payload, if any.");
		static GUIContent intContent		= new GUIContent("Int", "The integer value from an Int Payload, if any.");
		static GUIContent floatContent		= new GUIContent("Float", "The float value from a Float Payload, if any.");
		static GUIContent curveContent		= new GUIContent("Curve", "The evaluated curve value from a Curve Payload, if any.");
		static GUIContent colorContent		= new GUIContent("Color", "The color value from a Color Payload, if any.");
		static GUIContent gradientContent	= new GUIContent("Gradient", "The evaluated color from a Gradient Payload, if any.");
		static GUIContent spectrumContent	= new GUIContent("Spectrum", "The evaluated frequency spectrum from a Spectrum Payload, if any.");
		static GUIContent resetContent		= new GUIContent("Reset", "Resets the view to the default state.");
	
		#endregion
		#region Fields

		// The eventTriggerMask is used to show when events occurred.  By bit (as decimal):
		//  1 - No Payload.
		//  2 - Text Payload.
		//	4 - Int Payload.
		//  8 - Float Payload.
		//  16 - Curve Payload.
		//  32 - Color Payload.
		//	64 - Gradient Payload.
		//	128 - Spectrum Payload.
		int eventTriggerMask = 0;
		int numEventsThisUpdate = 0;

		// Most recent event values.
		string textPayload	= string.Empty;
		int intPayload		= 0;
		float floatPayload	= 0f;
		float curvePayload	= 0f;
		float curvePercent	= 1f;
		Color colorPayload	= Color.black;
		Color gradPayload	= Color.black;
		float[] specPayload	= null;
		

		// Overwritten by Color/Gradient events.
		Color sphereColor = Color.white;

		// Used for reapplying Focus to the source window.  Can be null.
		EditorWindow sourceWindow = null;

		// Used for tracking latest event so that we don't keep thrashing
		//  memory with allocation for curve bounds checking.
		KoreographyEvent lastCurveEvent;
		Bounds curveBounds;


		#endregion
		#region Properties

		float SpectrumFieldWidth
		{
			get
			{
				return position.width - (LabelWidth + 8f);		// 8 for edge spacing (4 on each side).
			}
		}

		#endregion
		#region Static Methods

		public static EventVisualizer OpenWindow(EditorWindow sourceWin)
		{
			// Equivalent of ShowUtility and then "title" or "titleContent" (version-dependent).
			EventVisualizer win = GetWindow<EventVisualizer>(true, "Visualizer");

			Vector2 winSize = new Vector2(264f, 444f);
			win.maxSize = winSize;
			win.minSize = winSize;

			win.sourceWindow = sourceWin;

			return win;
		}

		// Uses reflection to get the AnimationCurve Bounds using the Unity-provided [but not really] method.
		static Bounds GetBoundsOfCurve(AnimationCurve curve)
		{
			return (Bounds)GetCurveBounds.Invoke(System.Activator.CreateInstance(NormalCurveRendererType, new object[] {curve}), null);
		}

		#endregion
		#region Methods

		void ResetUI()
		{
			eventTriggerMask = 0;
			numEventsThisUpdate = 0;
			textPayload = string.Empty;
			intPayload = 0;
			floatPayload = 0f;
			curvePayload = 0f;
			curvePercent = 1f;
			colorPayload = Color.black;
			gradPayload = Color.black;
			if (specPayload != null)
			{
				System.Array.Clear(specPayload, 0, specPayload.Length);
			}
			sphereColor = Color.white;
		}

		public void EventsFired(List<KoreographyEvent> events, int curTime)
		{
			eventTriggerMask = 0;
			numEventsThisUpdate = events.Count;

			foreach (KoreographyEvent evt in events)
			{
				if (evt.Payload == null)
				{
					eventTriggerMask |= 1;
				}
				else
				{
					string typeStr = evt.Payload.GetType().Name;

					switch (typeStr)
					{
					case "TextPayload":
						textPayload = evt.GetTextValue();
						eventTriggerMask |= 2;
						break;
					case "IntPayload":
						intPayload = evt.GetIntValue();
						eventTriggerMask |= 4;
						break;
					case "FloatPayload":
						floatPayload = evt.GetFloatValue();
						eventTriggerMask |= 8;
						break;
					case "CurvePayload":
						curvePayload = evt.GetValueOfCurveAtTime(curTime);
						eventTriggerMask |= 16;

						if (lastCurveEvent == null || lastCurveEvent != evt)
						{
							lastCurveEvent = evt;
							curveBounds = GetBoundsOfCurve(evt.GetCurveValue());
						}

						Vector2 minMax = new Vector2(curveBounds.min.y, curveBounds.max.y);
						curvePercent = Mathf.InverseLerp(minMax.x, minMax.y, curvePayload);
						curvePercent = (minMax.x == minMax.y) ? 1f : (curvePayload - minMax.x) / (minMax.y - minMax.x);

						break;
					case "ColorPayload":
						colorPayload = evt.GetColorValue();
						sphereColor = colorPayload;
						eventTriggerMask |= 32;
						break;
					case "GradientPayload":
						gradPayload = evt.GetColorOfGradientAtTime(curTime);
						sphereColor = gradPayload;
						eventTriggerMask |= 64;
						break;
					case "SpectrumPayload":
						float minPointWidthPerBar = 3f;
						int maxBins = Mathf.FloorToInt(SpectrumFieldWidth / minPointWidthPerBar);
						evt.GetSpectrumAtTime(curTime, ref specPayload, maxBins);	// Get the spectrum data.
						eventTriggerMask |= 128;
						break;
					default:
						Debug.LogWarning("Unrecognized Payload type \'" + typeStr + "\' encountered. Ignoring.");
						break;
					}
				}
			}

			Repaint();
		}

		// Deprecated.  Use the Reflection-based GetCurveBounds feature to get more accurate values.
		Vector2 GetCurveMinMax(AnimationCurve curve)
		{
			Vector2 minMax = new Vector2(float.MaxValue, float.MinValue);

			foreach (Keyframe frame in curve.keys)
			{
				float val = frame.value;

				// Min
				if (val < minMax.x)
				{
					minMax.x = val;
				}

				// Max
				if (val > minMax.y)
				{
					minMax.y = val;
				}
			}

			return minMax;
		}

		void OnEnable()
		{
			// Ensure access to GUIStyles.
			KoreographyEditorSkin.InitSkin();

			if (GetCurveBounds == null)
			{
				GetCurveBounds = NormalCurveRendererType.GetMethod("GetBounds", System.Type.EmptyTypes);
			}
		}

		void OnFocus()
		{
			// OnFocus appears to come twice when the window starts:
			//  1) During initialization.
			//  2) Post initialization.
			// The first time, sourceWindow hasn't been set yet. Which
			//  is convenient: we only want to tell the system to focus
			//  the source the *last* time through the init phase. This
			//  seems to work fine.
			if (sourceWindow != null)
			{
				sourceWindow.Focus();
				sourceWindow = null;
			}
		}

		void OnGUI()
		{
			if (Event.current.type == EventType.KeyDown)
			{
				if (Event.current.keyCode == KeyCode.V ||
					Event.current.keyCode == KeyCode.Escape)
				{
					Close();
					return;
				}
			}

			EditorGUIUtility.labelWidth = LabelWidth;

			// Number of triggered events
			GUI.color = (eventTriggerMask != 0) ? Color.green : Color.white;
			EditorGUILayout.TextField(numEvtContent, numEventsThisUpdate.ToString());

			EditorGUILayout.BeginHorizontal();
			{
				GUILayout.FlexibleSpace();

				GUILayout.Label(payloadContent);

				GUILayout.FlexibleSpace();
			}
			EditorGUILayout.EndHorizontal();

			// Text events
			GUI.color = ((eventTriggerMask & 2) != 0) ? Color.green : Color.white;
			EditorGUILayout.TextField(textContent, textPayload);

			// Int events
			GUI.color = ((eventTriggerMask & 4) != 0) ? Color.green : Color.white;
			EditorGUILayout.IntField(intContent, intPayload);

			// Float events
			GUI.color = ((eventTriggerMask & 8) != 0) ? Color.green : Color.white;
			EditorGUILayout.FloatField(floatContent, floatPayload);

			// Curve events
			GUI.color = ((eventTriggerMask & 16) != 0) ? Color.green : Color.white;
			EditorGUILayout.FloatField(curveContent, curvePayload);

			GUI.color = Color.white;

			// Color events
			EditorGUILayout.LabelField(colorContent);
			Rect posRect = GUILayoutUtility.GetLastRect();
			posRect.xMin += EditorGUIUtility.labelWidth;
			EditorGUI.BeginDisabledGroup(true);
			{
				KoreographerGUIUtils.ColorField(posRect, colorPayload, false, true);
			}
			EditorGUI.EndDisabledGroup();
			if ((eventTriggerMask & 32) != 0)
			{
				KoreographerGUIUtils.DrawOutlineAroundRect(posRect, Color.green, 2f);
			}

			// Gradient events
			EditorGUILayout.LabelField(gradientContent);
			posRect = GUILayoutUtility.GetLastRect();
			posRect.xMin += EditorGUIUtility.labelWidth;
			EditorGUI.BeginDisabledGroup(true);
			{
				KoreographerGUIUtils.ColorField(posRect, gradPayload, false, true);
			}
			EditorGUI.EndDisabledGroup();
			if ((eventTriggerMask & 64) != 0)
			{
				KoreographerGUIUtils.DrawOutlineAroundRect(posRect, Color.green, 2f);
			}

			// Spectrum events
			EditorGUILayout.LabelField(spectrumContent);
			posRect = GUILayoutUtility.GetLastRect();
			posRect.xMin += EditorGUIUtility.labelWidth;
			EditorGUI.BeginDisabledGroup(true);
			{
				KoreographerGUIUtils.DrawSpectrumGUI(posRect, specPayload);
			}
			EditorGUI.EndDisabledGroup();
			if ((eventTriggerMask & 128) != 0)
			{
				KoreographerGUIUtils.DrawOutlineAroundRect(posRect, Color.green, 2f);
			}

			// Event Triggered.
			EditorGUILayout.BeginVertical();
			{
				GUILayout.FlexibleSpace();
			
				EditorGUILayout.BeginHorizontal();
				{
					GUILayout.FlexibleSpace();

					DoCurveBallGUI();

					GUILayout.FlexibleSpace();
				}
				EditorGUILayout.EndHorizontal();
			
				GUILayout.FlexibleSpace();

				EditorGUILayout.BeginHorizontal();
				{
					GUILayout.FlexibleSpace();

					if (GUILayout.Button(resetContent))
					{
						ResetUI();
					}
				}
				EditorGUILayout.EndHorizontal();
			}
			EditorGUILayout.EndVertical();

			EditorGUIUtility.labelWidth = 0f;

			GUI.FocusControl("");
		}

		void DoCurveBallGUI()
		{
			// Ball size.
			float size = 256f * curvePercent;
			float viewSizeMax = 256f;

			// Ensure a reasonble view if ever the min/max evaluation points go
			//  outside the expected range of [0,1].
			Vector2 viewOffset = Vector2.zero;
			float absSize = Mathf.Abs(size);
			if (absSize > viewSizeMax)
			{
				float offset = (absSize - viewSizeMax) * 0.5f;
				viewOffset = new Vector2(offset, offset);
			}

			GUI.color = EditorGUIUtility.isProSkin ? proSkinBGColor : Color.gray;
			GUILayout.BeginScrollView(viewOffset, false, false, GUIStyle.none, GUIStyle.none, KoreographyEditorSkin.visualizerImageBG, GUILayout.MaxHeight(viewSizeMax), GUILayout.MaxWidth(viewSizeMax));
			GUI.color = Color.white;
			{
				EditorGUILayout.BeginVertical(GUILayout.MaxHeight(viewSizeMax));
				{
					GUILayout.FlexibleSpace();
				
					EditorGUILayout.BeginHorizontal(GUILayout.MaxWidth(viewSizeMax));
					{
						GUILayout.FlexibleSpace();

						GUI.color = (size >= 0) ? sphereColor : Color.red;
						GUILayout.Box("", KoreographyEditorSkin.visualizerImage, GUILayout.Width(absSize), GUILayout.Height(absSize));
						GUI.color = Color.white;
					
						GUILayout.FlexibleSpace();
					}
					EditorGUILayout.EndHorizontal();
				
					GUILayout.FlexibleSpace();
				}
				EditorGUILayout.EndVertical();
			}
			GUILayout.EndScrollView();
		}

		#endregion
	}
}
