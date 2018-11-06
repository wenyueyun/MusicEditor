//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SonicBloom.Koreo.EditorUI
{
	// This class originally provided by Jacob Pennock at the following location:
	//  http://www.jacobpennock.com/Blog/?p=670
	//  http://jacobpennock.com/Files/CustomAssetUtility.cs
	/// <summary>
	/// Utility class to help with generating Assets in earlier versions of Unity.
	/// </summary>
	public static class CustomAssetUtility
	{
		/// <summary>
		/// Creates an asset at the location of the current "active (selected) object". If
		/// none exists, it defaults to "Assets". In Unity 5.1 and above, use the built-in
		/// <c>[CreateAssetMenuAttribute]</c> instead.
		/// </summary>
		/// <param name="fileExtension">File extension to use.</param>
		/// <typeparam name="T">The specific type of asset to create.</typeparam>
		public static void CreateAsset<T>(string fileExtension = "asset") where T : ScriptableObject
		{
			T asset = ScriptableObject.CreateInstance<T>();
		
			string path = AssetDatabase.GetAssetPath(Selection.activeObject);
			if (string.IsNullOrEmpty(path))
			{
				path = "Assets";
			}
			else if (!string.IsNullOrEmpty(Path.GetExtension(path)))
			{
				path = path.Replace(Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), "");
			}

			string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + "/New " + typeof(T).Name + "." + fileExtension);
		
			AssetDatabase.CreateAsset(asset, assetPathAndName);
		
			AssetDatabase.SaveAssets();
			EditorUtility.FocusProjectWindow();
			Selection.activeObject = asset;
		}
	}

	// This class contains colors and related values that are used throughout
	//  the Koreorapher Editor system.
	internal static class KoreographerColors
	{
		// This is necessary when using the UnityEditor.Handles class because the internal
		//  processing on the lines automatically multiplies the alpha of the color stored
		//  in the static "Handles.color" field by 0.75.  We prepare for this by setting
		//  alpha to a number that when multiplied by 0.75 yields 1, namely 1.333333.
		public static float HandleFullAlpha = 1.333333f;

		public static Color WaveformBG = new Color(1f, 104f / 255f, 0f,0.5f);
		public static Color WaveformFG = new Color(1f, 149f / 255f, 0f,0.5f);
	}

	// This class contains functionality related to Koreography Track and Payload
	//  type handling.
	internal static class KoreographyTrackTypeUtils
	{
		// Collections containing ALL types.
		static List<Type> trackTypes;
		static Dictionary<Type, List<Type>> trackPayloadTypes;
		// Collections containing only Editor-usable types. (Those that don't have the [NoEditorCreate] attribute.
		static List<Type> editableTrackTypes;
		static Dictionary<Type, List<Type>> editableTrackPayloadTypes;

		public static List<Type> TrackTypes
		{
			get
			{
				if (trackTypes == null)
				{
					trackTypes = GetTrackTypes(false);
				}

				return trackTypes;
			}
		}

		public static Dictionary<Type, List<Type>> TrackPayloadTypes
		{
			get
			{
				if (trackPayloadTypes == null)
				{
					trackPayloadTypes = GetTrackPayloadTypes(false, false);
				}

				return trackPayloadTypes;
			}
		}

		public static List<Type> EditableTrackTypes
		{
			get
			{
				if (editableTrackTypes == null)
				{
					editableTrackTypes = GetTrackTypes(true);
				}

				return editableTrackTypes;
			}
		}
		
		public static Dictionary<Type, List<Type>> EditableTrackPayloadTypes
		{
			get
			{
				if (editableTrackPayloadTypes == null)
				{
					editableTrackPayloadTypes = GetTrackPayloadTypes(true, true);
				}

				return editableTrackPayloadTypes;
			}
		}

		/// <summary>
		/// Retrieves all Koreography Track types by searching all Assemblies loaded into the
		/// current App Domain for any <c>System.Type</c> that subclasses the
		/// <c>KoreographyTrackBase</c> class.
		/// </summary>
		/// <returns>A <c>System.Type</c> <c>List</c> containing all types that subclass the
		/// <c>KoreographyTrackBase</c> class in the current <c>AppDomain</c>.</returns>
		/// <param name="bExcludeNoEditorCreate">Whether or not to exclude Track Types with the
		/// <c>NoEditorCreate</c> Attribute set.</param>
		static List<Type> GetTrackTypes(bool bExcludeNoEditorCreate)
		{
			// Initialize the internal private cached version if necessary.
			List<Type> trTypes = new List<Type>();

#if !KOREO_NON_PRO
			Type trackBase = typeof(KoreographyTrackBase);
			Type noEditorAttrib = typeof(NoEditorCreateAttribute);
			
			Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();		// Get all assemblies.
			trTypes.AddRange(
				assemblies.SelectMany(ass => ass.GetTypes())							// Get all types.
				.Where(ty => ty.IsClass &&												// Filter out classes.
				       ty.IsSubclassOf(trackBase) &&									// Find Track Base subclasses.
			       	   (!bExcludeNoEditorCreate ||
			 			ty.GetCustomAttributes(noEditorAttrib, false).Length == 0)));	// Filter out classes with the No Editor Create attribute.
#else
			trTypes.Add(typeof(KoreographyTrack));
#endif
			return trTypes;
		}

		/// <summary>
		/// Retrieves a Dictionary of [Koreography Track Types] to [supported Payload Types].
		/// </summary>
		/// <returns>A dictionary that maps Koreography Track types to the Payload types they support.</returns>
		/// <param name="bExcludeNoEditorCreateTracks">If set to <c>true</c> Track types with the
		/// <c>[NoEditorCreate]</c> Attribute will be excluded.</param>
		/// <param name="bExcludeNoEditorCreatePayloads">If set to <c>true</c> Payload types with the
		/// <c>[NoEditorCreate]</c> Attribute will be excluded.</param>
		static Dictionary<Type, List<Type>> GetTrackPayloadTypes(bool bExcludeNoEditorCreateTracks, bool bExcludeNoEditorCreatePayloads)
		{
			Dictionary<Type, List<Type>> trPlTypes = new Dictionary<Type, List<Type>>();

#if !KOREO_NON_PRO
			List<Type> trTypes = bExcludeNoEditorCreateTracks ? EditableTrackTypes : TrackTypes;

			foreach (Type trTy in trTypes)
			{
				// Store types.
				List<Type> plTypes = GetPayloadTypesForTrackType(trTy, bExcludeNoEditorCreatePayloads);
				trPlTypes.Add(trTy, plTypes);
			}
#else
			// Store types - GetAllPayloadTypes() will return expected types for KoreographyTrack in the
			//  editor.
			List<Type> plTypes = GetAllPayloadTypes(true);
			trPlTypes.Add(typeof(KoreographyTrack), plTypes);
#endif
			return trPlTypes;
		}

		/// <summary>
		/// Retrieves all Payload Types used in the specified Track type.
		/// </summary>
		/// <returns>A List of Payload Types used in the Koreography Track type.</returns>
		/// <param name="trackType">The type of track to search.</param>
		/// <param name="bExcludeNoEditorCreate">Whether or not to exclude Payload Types with the
		/// <c>NoEditorCreate</c> Attribute set.</param>
		static List<Type> GetPayloadTypesForTrackType(Type trackType, bool bExcludeNoEditorCreate)
		{
			List<Type> fieldTypes = new List<Type>();
			
			if (trackType.IsSubclassOf(typeof(KoreographyTrackBase)))
			{
				FieldInfo[] fields = trackType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				Type noEditorAttrib = typeof(NoEditorCreateAttribute);
				
				foreach (FieldInfo field in fields)
				{
					Type ty = field.FieldType;
					
					if (ty.IsGenericType && ty.GetGenericTypeDefinition() == typeof(List<>))
					{
						Type plType = ty.GetGenericArguments()[0];
						
						// Payload types must implement the IPayload interface.
						if (plType.GetInterfaces().Contains(typeof(IPayload)) &&
						    field.Name == "_" + plType.Name + "s" &&
						    // Optionally exclude payloads with the [NoEditorCreate] attribute set.
						    (!bExcludeNoEditorCreate || plType.GetCustomAttributes(noEditorAttrib, false).Length == 0))
						{
							fieldTypes.Add(plType);
						}
					}
				}
			}
			else
			{
				Debug.LogWarning("Attempted to find Payload types for non-Koreography Track type.");
			}
			
			return fieldTypes;
		}

		/// <summary>
		/// Retrieves all Payload types by searching all Assemblies loaded into the current
		/// App Domain for any <c>System.Type</c> that implements the <c>IPayload</c>
		/// interface.
		/// </summary>
		/// <returns>A <c>System.Type</c> <c>List</c> containing all types that implement
		/// the <c>IPayload</c> interface in the current AppDomain.</returns>
		/// <param name="bExcludeNoEditorCreate">Whether or not to exclude Payload Types with the
		/// <c>NoEditorCreate</c> Attribute set.</param>
		static List<Type> GetAllPayloadTypes(bool bExcludeNoEditorCreate)
		{
#if !KOREO_NON_PRO
			// Get references to all Payload types.
			Type iface = typeof(IPayload);
			Type noEditorAttrib = typeof(NoEditorCreateAttribute);
			
			// Adapted from http://stackoverflow.com/questions/26733/getting-all-types-that-implement-an-interface/12602220
			Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();   	// Get all Assemblies.
			return assemblies.SelectMany(ass => ass.GetTypes())                         // Get all Types.
				.Where(ty => ty.IsClass &&												// Filter out Classes.
				       ty.GetInterfaces().Contains(iface) &&							// Find only those that implement IPayload.
				       (!bExcludeNoEditorCreate ||
				       ty.GetCustomAttributes(noEditorAttrib, false).Length == 0))		// Filter out Payloads with the No Editor Create attribute.
					.ToList();
#else
			Type[] plTypes = { typeof(CurvePayload), typeof(FloatPayload), typeof(IntPayload), typeof(TextPayload) };
			return new List<Type>(plTypes);
#endif
			
			// TODO: Check for any that Type.IsSubclassOf(MonoBehaviour) and issue a warning.
			//  They [probably?] won't work as Payload types (no way to instantiate them without)
			//  cloning.
		}

		/// <summary>
		/// Determines if the type has the [NoEditorCreate] attribute applied or not.
		/// </summary>
		/// <returns><c>true</c> if the type has the [NoEditorCreate] attribute, otherwise, <c>false</c>.</returns>
		/// <param name="testType">The <c>System.Type</c> to test.</param>
		public static bool IsTypeNoEditorCreate(System.Type testType)
		{
			return testType.GetCustomAttributes(typeof(NoEditorCreateAttribute), false).Length > 0;
		}
	}

	// This class contains various functions and values that don't really fit
	//  in a classification by themselves.  If enough of a given type appear,
	//  a separate class will be broken out.
	internal static class KoreographerMiscUtils
	{
		/// <summary>
		/// Takes two absolute paths and returns a string representing the version of the first
		/// relative to the second.
		/// </summary>
		/// <returns>A representation of <paramref name="startPath"/> that is relative to <paramref name="relativeToPath"/>.</returns>
		/// <param name="startPath">The absolute path to make relative.</param>
		/// <param name="relativeToPath">The absolute path to which to be made relative to.</param>
		public static string AbsoluteToRelativePath(string startPath, string relativeToPath)
		{
			string retPath = string.Empty;

			if (!string.IsNullOrEmpty(startPath) && !string.IsNullOrEmpty(relativeToPath) &&
			    Path.IsPathRooted(startPath) && Path.IsPathRooted(relativeToPath))
			{
				// Uri wants directories to end in a trailing slash.  Ensure that this is the case by
				//  adding a trailing slash (double slashes are fine).
				if (Directory.Exists(startPath))
				{
					startPath += Path.DirectorySeparatorChar;
				}
				
				if (Directory.Exists(relativeToPath))
				{
					// If the relative path is a directory, add the "current directory" symbol (".") to make sure that
					//  the relative paths source *that* location, rather than the directory CONTAINING the one that
					//  ends this string.
					relativeToPath += Path.DirectorySeparatorChar + "." + Path.DirectorySeparatorChar;
				}

				Uri startURI = new Uri(startPath, UriKind.Absolute);
				Uri relativeToURI = new Uri(relativeToPath, UriKind.Absolute);

				retPath = relativeToURI.MakeRelativeUri(startURI).ToString();
			}

			return retPath;
		}
	}

	// This class contains GUI functions and related values that are used by
	//  the Koreographer Editor and related system.
	internal static class KoreographerGUIUtils
	{
		static Type[] colorFieldTypes = {typeof(Rect), typeof(Color), typeof(bool), typeof(bool)};
		static MethodInfo EditorGUIColorField = typeof(EditorGUI).GetMethod("ColorField", BindingFlags.NonPublic | BindingFlags.Static, null, colorFieldTypes, null);
		static GUIContent tooltipContent = new GUIContent("", "");

		// The EditorGUI class abstracts this value away.  The value 15f was obtained by snooping in the Assembly Browser.
		//  See: the internal EditorGUI.indent property (derived from the constant EditorGUI.kIndentPerLevel).
		public static float EditorIndent
		{
			get
			{
				return EditorGUI.indentLevel * 15f;
			}
		}

		public static Color ColorField(Rect position, Color value, bool showEyeDropper, bool showAlpha)
		{
			object[] parameters = {position, value, showEyeDropper, showAlpha};
			return (Color)EditorGUIColorField.Invoke(null, parameters);
		}

		public static void DrawOutlineAroundLastControl(Color color, float width = 1f)
		{
			DrawOutlineAroundRect(GUILayoutUtility.GetLastRect(), color, width);
		}

		public static void DrawOutlineAroundRect(Rect rect, Color color, float width = 1f)
		{
			Vector3 topLeft, topRight, botLeft, botRight;
			topLeft = topRight = botLeft = botRight = Vector3.zero;
		
			topLeft.x = rect.xMin - 1f;
			topLeft.y = rect.yMin - 1f;
			topRight.x = rect.xMax + 1f;
			topRight.y = rect.yMin - 1f;
			botLeft.x = rect.xMin - 1f;
			botLeft.y = rect.yMax + 1f;
			botRight.x = rect.xMax + 1f;
			botRight.y = rect.yMax + 1f;

			Drawing.DrawLine(topLeft, topRight, color, width, false);
			Drawing.DrawLine(topRight, botRight, color, width, false);
			Drawing.DrawLine(botRight, botLeft, color, width, false);
			Drawing.DrawLine(botLeft, topLeft, color, width, false);
		}

		public static Rect GetLayoutRectForFoldout()
		{
			// The following sizes taken from the Disassembled EditorGUILayout.Foldout() definition.
			return GUILayoutUtility.GetRect(EditorGUIUtility.fieldWidth, EditorGUIUtility.fieldWidth, 16f, 16f, EditorStyles.foldout);
		}

		public static void AddTooltipToRect(string tooltip, Rect area)
		{
			tooltipContent.tooltip = tooltip;
			EditorGUI.HandlePrefixLabel(area, area, tooltipContent);
		}

		public static void AddTooltipToRect(GUIContent tooltip, Rect area)
		{
			EditorGUI.HandlePrefixLabel(area, area, tooltip);
		}

		/// <summary>
		/// Draws the <paramref name="spectrum"/> into the <paramref name="spectrumRect"/>.
		/// This method does not validate width.  You should ensure the <c>Rect</c> has enough
		/// with for the number of entries in the spectrum.  Bars are drawn as GL QUADs.
		/// </summary>
		/// <param name="spectrumRect">The Rect within which to draw the <paramref name="spectrum"/>.</param>
		/// <param name="spectrum">The spectrum data to render into the <paramref name="spectrumRect"/> .</param>
		public static void DrawSpectrumGUI(Rect spectrumRect, float[] spectrum)
		{
			if (Event.current.type == EventType.Repaint)
			{
				GLDrawing.BeginQuadDrawing();
				{
					// Draw a black quad as background.
					GL.Color(Color.black);
					GL.Vertex3(spectrumRect.xMin, spectrumRect.yMax, 0f);	// Bottom left.
					GL.Vertex3(spectrumRect.xMin, spectrumRect.yMin, 0f);	// Top left.
					GL.Vertex3(spectrumRect.xMax, spectrumRect.yMin, 0f);	// Top right.
					GL.Vertex3(spectrumRect.xMax, spectrumRect.yMax, 0f);	// Bottom right.
					
					if (spectrum != null && spectrum.Length > 0)
					{
						float pointsPerBar = spectrumRect.width / spectrum.Length;
						
						float xPos = spectrumRect.xMin;
						float yPos = spectrumRect.yMax;
						
						Color color = Color.red;
						float colorStep = 1f / spectrum.Length;
						
						for (int i = 0; i < spectrum.Length; ++i)
						{
							GL.Color(color);
							
							float topY = yPos - (spectrum[i] * spectrumRect.height);
							GL.Vertex3(xPos, yPos, 0f);		// Bottom left.
							GL.Vertex3(xPos, topY, 0f);		// Top left.
							
							xPos += pointsPerBar;			// Move along X.
							
							GL.Vertex3(xPos, topY, 0f);		// Top right.
							GL.Vertex3(xPos, yPos, 0f);		// Bottom right.
							
							color.r -= colorStep;
							color.g += colorStep;
						}
					}
				}
				GLDrawing.EndQuadDrawing();
			}
		}

		/// <summary>
		/// <para>Draws an Editor Field for a given <c>UnityEngine.Object</c>. This version does
		/// not include the little selector icon towards the back of the field.</para>
		/// <para>Left-clicking the field will open the Object Picker. Right-clicking will select
		/// the object (if one exists) in the Project View.</para>
		/// </summary>
		/// <returns>A reference to a <c>UnityEngine.Object</c> instance or <c>null</c>.</returns>
		/// <param name="controlRect">The <c>Rect</c> within which to draw the control.</param>
		/// <param name="obj">The <c>UnityEngine.Object</c> reference to show with this control.</param>
		internal static UnityEngine.Object CustomObjectField(Rect controlRect, UnityEngine.Object obj)
		{
			int controlID = EditorGUIUtility.GetControlID(FocusType.Passive);
			
			switch (Event.current.GetTypeForControl(controlID))
			{
			case EventType.Repaint:
				GUI.Box(controlRect, (obj == null) ? "None" : obj.name, EditorStyles.textField);
				break;
			case EventType.MouseDown:
				if (controlRect.Contains(Event.current.mousePosition))
				{
					if (Event.current.button == 0)
					{
						// Show picker!
						EditorGUIUtility.ShowObjectPicker<UnityEngine.Object>(obj, false, string.Empty, controlID);
						Event.current.Use();
					}
					else if (Event.current.button == 1)
					{
						// Show object!
						if (obj != null)
						{
							Selection.activeObject = obj;
						}
						Event.current.Use();
					}
				}
				break;
			case EventType.ExecuteCommand:
				// We only care about the Object Picker and if it's targeting us.
				if (EditorGUIUtility.GetObjectPickerControlID() == controlID)
				{
					if (Event.current.commandName == "ObjectSelectorUpdated")
					{
						UnityEngine.Object newObj = EditorGUIUtility.GetObjectPickerObject();
						
						GUI.changed = (obj != newObj);
						obj = newObj;
						
						Event.current.Use();
					}
				}
				break;
			default:
				break;
			}
			
			return obj;
		}

		/// <summary>
		/// Shows a dropdown menu (via GenericMenu) with options specified in the <paramref name="typeOptions"/>
		/// parameter. If a selection occurs, the <paramref name="OnOptionSelected"/> function will be called and
		/// passed the <c>System.Type</c> as the function's <c>data</c> parameter. If there is only one
		/// option, the function will be called immediately and no menu will be shown.
		/// </summary>
		/// <param name="position">The position of the element over which to draw the menu.</param>
		/// <param name="typeOptions">The types to show in the list.</param>
		/// <param name="OnOptionSelected">On new track option selected.</param>
		internal static void ShowTypeSelectorMenu(Rect position, List<System.Type> typeOptions, GenericMenu.MenuFunction2 OnOptionSelected)
		{
			if (typeOptions.Count == 1)
			{
				// If there is only one type available, use it by default.
				OnOptionSelected(typeOptions[0]);
			}
			else
			{
				// Else, prepare a list to create any we have access to.
				GenericMenu menu = new GenericMenu();
				
				for (int i = 0; i < typeOptions.Count; ++i)
				{
					menu.AddItem(new GUIContent(typeOptions[i].Name), false, OnOptionSelected, typeOptions[i]);
				}

				menu.DropDown(position);
			}
		}
	}

	internal static class GLDrawing
	{
		static Material LineMat = null;		// Should be set to the same material used by Handles for line drawing.

		static void Initialize()
		{
			// Standard approach - taken from Unity Assembly.
			if (LineMat == null)
			{
				LineMat = EditorGUIUtility.LoadRequired("SceneView/2DHandleLines.mat") as Material;
			}
			
			// Fallback is to use reflection.
			if (LineMat == null)
			{
				System.Reflection.FieldInfo lineMatInfo = typeof(HandleUtility).GetField("s_HandleWireMaterial2D", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
				
				if (lineMatInfo != null)
				{
					LineMat = lineMatInfo.GetValue(null) as Material;
				}
			}
		}

		public static void BeginLineDrawing()
		{
			if (LineMat == null)
			{
				GLDrawing.Initialize();
			}

			LineMat.SetPass(0);
			GL.PushMatrix();
			GL.Begin(GL.LINES);
		}

		public static void EndLineDrawing()
		{
			GL.End();
			GL.PopMatrix();
		}

		public static void BeginQuadDrawing()
		{
			if (LineMat == null)
			{
				GLDrawing.Initialize();
			}
			
			LineMat.SetPass(0);
			GL.PushMatrix();
			GL.Begin(GL.QUADS);
		}
		
		public static void EndQuadDrawing()
		{
			GL.End();
			GL.PopMatrix();
		}
	}

	// Line drawing routine originally courtesy of Linusmartensson:
	// http://forum.unity3d.com/threads/71979-Drawing-lines-in-the-editor
	//
	// Rewritten to improve performance by Yossarian King / August 2013.
	//
	// This version produces virtually identical results to the original (tested by drawing
	// one over the other and observing errors of one pixel or less), but for large numbers
	// of lines this version is more than four times faster than the original, and comes
	// within about 70% of the raw performance of Graphics.DrawTexture.
	//
	// Peak performance on my laptop is around 200,000 lines per second. The laptop is
	// Windows 7 64-bit, Intel Core2 Duo CPU 2.53GHz, 4G RAM, NVIDIA GeForce GT 220M.
	// Line width and anti-aliasing had negligible impact on performance.
	//
	// For a graph of benchmark results in a standalone Windows build, see this image:
	// https://app.box.com/s/hyuhi565dtolqdm97e00
	//
	// For a Google spreadsheet with full benchmark results, see:
	// https://docs.google.com/spreadsheet/ccc?key=0AvJlJlbRO26VdHhzeHNRMVF2UHZHMXFCTVFZN011V1E&usp=sharing

	internal static class Drawing
	{
		private static Texture2D aaLineTex = null;
		private static Texture2D lineTex = null;
		private static Material blitMaterial = null;
		private static Material blendMaterial = null;
		private static Rect lineRect = new Rect(0, 0, 1, 1);

		// Draw a line in screen space, suitable for use from OnGUI calls from either
		// MonoBehaviour or EditorWindow. Note that this should only be called during repaint
		// events, when (Event.current.type == EventType.Repaint).
		//
		// Works by computing a matrix that transforms a unit square -- Rect(0,0,1,1) -- into
		// a scaled, rotated, and offset rectangle that corresponds to the line and its width.
		// A DrawTexture call used to draw a line texture into the transformed rectangle.
		//
		// More specifically:
		//      scale x by line length, y by line width
		//      rotate around z by the angle of the line
		//      offset by the position of the upper left corner of the target rectangle
		//
		// By working out the matrices and applying some trigonometry, the matrix calculation comes
		// out pretty simple. See https://app.box.com/s/xi08ow8o8ujymazg100j for a picture of my
		// notebook with the calculations.
		public static void DrawLine(Vector2 pointA, Vector2 pointB, Color color, float width, bool antiAlias)
		{
			// Normally the static initializer does this, but to handle texture reinitialization
			// after editor play mode stops we need this check in the Editor.
#if UNITY_EDITOR
			if (!lineTex)	
			{
				Initialize();
			}
#endif

			// Note that theta = atan2(dy, dx) is the angle we want to rotate by, but instead
			// of calculating the angle we just use the sine (dy/len) and cosine (dx/len).
			float dx = pointB.x - pointA.x;
			float dy = pointB.y - pointA.y;
			float len = Mathf.Sqrt(dx * dx + dy * dy);
		
			// Early out on tiny lines to avoid divide by zero.
			// Plus what's the point of drawing a line 1/1000th of a pixel long??
			if (len < 0.001f)
			{
				return;
			}

			// Pick texture and material (and tweak width) based on anti-alias setting.
			Texture2D tex;
			Material mat;
		
			if (antiAlias)
			{
				// Multiplying by three is fine for anti-aliasing width-1 lines, but make a wide "fringe"
				// for thicker lines, which may or may not be desirable.
				width = width * 3.0f;
				tex = aaLineTex;
				mat = blendMaterial;
			}
			else
			{
				tex = lineTex;
				mat = blitMaterial;
			}

			float wdx = width * dy / len;
			float wdy = width * dx / len;

			Matrix4x4 matrix = Matrix4x4.identity;
			matrix.m00 = dx;
			matrix.m01 = -wdx;
			matrix.m03 = pointA.x + 0.5f * wdx;
			matrix.m10 = dy;
			matrix.m11 = wdy;
			matrix.m13 = pointA.y - 0.5f * wdy;

			// Use GL matrix and Graphics.DrawTexture rather than GUI.matrix and GUI.DrawTexture,
			// for better performance. (Setting GUI.matrix is slow, and GUI.DrawTexture is just a
			// wrapper on Graphics.DrawTexture.)
			GL.PushMatrix();
			GL.MultMatrix(matrix);
			Graphics.DrawTexture(lineRect, tex, lineRect, 0, 0, 0, 0, color, mat);
			GL.PopMatrix();
		}
	
	
	
		// Other than method name, DrawBezierLine is unchanged from Linusmartensson's original implementation.
		public static void DrawBezierLine(Vector2 start, Vector2 startTangent, Vector2 end, Vector2 endTangent, Color color, float width, bool antiAlias, int segments)
		{
			Vector2 lastV = CubeBezier(start, startTangent, end, endTangent, 0);
				
			for (int i = 1; i < segments; ++i)
			{
				Vector2 v = CubeBezier(start, startTangent, end, endTangent, i / (float)segments);
				Drawing.DrawLine(lastV, v, color, width, antiAlias);
				lastV = v;
			}
		}
	
		private static Vector2 CubeBezier(Vector2 s, Vector2 st, Vector2 e, Vector2 et, float t)
		{
		
			float rt = 1 - t;
			return rt * rt * rt * s + 3 * rt * rt * t * st + 3 * rt * t * t * et + t * t * t * e;		
		}
	
		// This static initializer works for runtime, but apparently isn't called when
		// Editor play mode stops, so DrawLine will re-initialize if needed.
		static Drawing()
		{
			Initialize();
		}

		private static void Initialize()
		{
			if (lineTex == null)
			{
				lineTex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
				lineTex.SetPixel(0, 1, Color.white);
				lineTex.Apply();
			}

			if (aaLineTex == null)
			{
				// TODO: better anti-aliasing of wide lines with a larger texture? or use Graphics.DrawTexture with border settings
				aaLineTex = new Texture2D(1, 3, TextureFormat.ARGB32, false);
				aaLineTex.SetPixel(0, 0, new Color(1, 1, 1, 0));
				aaLineTex.SetPixel(0, 1, Color.white);
				aaLineTex.SetPixel(0, 2, new Color(1, 1, 1, 0));
				aaLineTex.Apply();
			}

			// GUI.blitMaterial and GUI.blendMaterial are used internally by GUI.DrawTexture,
			// depending on the alphaBlend parameter. Use reflection to "borrow" these references.
			blitMaterial = (Material)typeof(GUI).GetMethod("get_blitMaterial", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
			blendMaterial = (Material)typeof(GUI).GetMethod("get_blendMaterial", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
		}	
	}
}
