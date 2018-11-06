//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;

#if (!(UNITY_4_5 || UNITY_4_6 || UNITY_4_7) && UNITY_EDITOR)
using UnityEditor;
#endif

namespace SonicBloom.Koreo
{
	/// <summary>
	/// Extension Methods for the <see cref="KoreographyEvent"/> class that add
	/// <see cref="ColorPayload"/>-specific functionality.
	/// </summary>
	public static class ColorPayloadEventExtensions
	{
		#region KoreographyEvent Extension Methods

		/// <summary>
		/// Determines if the payload is of type <see cref="ColorPayload"/>.
		/// </summary>
		/// <returns><c>true</c> if the payload is of type <see cref="ColorPayload"/>;
		/// otherwise, <c>false</c>.</returns>
		public static bool HasColorPayload(this KoreographyEvent koreoEvent)
		{
			return (koreoEvent.Payload as ColorPayload) != null;
		}

		/// <summary>
		/// Returns the <c>Color</c> associated with the ColorPayload.  If the
		/// Payload is not actually of type <see cref="ColorPayload"/>, this
		/// will return the default color, white.
		/// </summary>
		/// <returns>The <c>Color</c> value.</returns>
		public static Color GetColorValue(this KoreographyEvent koreoEvent)
		{
			Color retVal = Color.white;
		
			ColorPayload pl = koreoEvent.Payload as ColorPayload;
			if (pl != null)
			{
				retVal = pl.ColorVal;
			}
		
			return retVal;
		}

		#endregion
	}

	/// <summary>
	/// The ColorPayload class allows Koreorgraphy Events to contain a <c>Color</c> value
	/// as a payload.
	/// </summary>
	[System.Serializable]
	public class ColorPayload : IPayload
	{
		#region Fields

		[SerializeField]
		[Tooltip("The color value that makes up the payload.")]
		Color mColorVal = Color.white;

		#endregion
		#region Properties

		/// <summary>
		/// Gets or sets the <c>Color</c> value.
		/// </summary>
		/// <value>The <c>Color</c> value.</value>
		public Color ColorVal
		{
			get
			{
				return mColorVal;
			}
			set
			{
				mColorVal = value;
			}
		}

		#endregion
		#region Standard Methods

		/// <summary>
		/// This is used by the Koreography Editor to create the Payload type entry
		/// in the UI dropdown.
		/// </summary>
		/// <returns>The friendly name of the class.</returns>
		public static string GetFriendlyName()
		{
			return "Color";
		}

		#endregion
		#region IPayload Interface

#if (!(UNITY_4_5 || UNITY_4_6 || UNITY_4_7) && UNITY_EDITOR)

		// Replace this with a call to KoreographerGUIUtils.ColorField once we properly divorce UnityEditor stuff
		//  from the game stuff for packaging.
		System.Reflection.MethodInfo ColorFieldSpecial;

		/// <summary>
		/// Used for drawing the GUI in the Editor Window (possibly scene overlay?).  Undo is
		/// supported.  Uses a version of the <c>ColorField</c> drawing methods that doesn't show
		/// the color picker.
		/// </summary>
		/// <returns><c>true</c>, if the Payload was edited in the GUI, <c>false</c>
		/// otherwise.</returns>
		/// <param name="displayRect">The <c>Rect</c> within which to perform GUI drawing.</param>
		/// <param name="track">The Koreography Track within which the Payload can be found.</param>
		/// <param name="isSelected">Whether or not the Payload (or the Koreography Event that
		/// contains the Payload) is selected in the GUI.</param>
		public bool DoGUI(Rect displayRect, KoreographyTrackBase track, bool isSelected)
		{
			if (ColorFieldSpecial == null)
			{
				System.Type[] types = {typeof(Rect), typeof(Color), typeof(bool), typeof(bool)};
				ColorFieldSpecial = typeof(EditorGUI).GetMethod("ColorField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null, types, null);
			}

			bool bDidEdit = false;
			Color originalBG = GUI.backgroundColor;
			GUI.backgroundColor = isSelected ? Color.green : originalBG;
			
			EditorGUI.BeginChangeCheck();
			object[] parameters = {displayRect, ColorVal, false, true};
			Color newVal = (Color)ColorFieldSpecial.Invoke(null, parameters);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(track, "Modify Color Payload");
				ColorVal = newVal;
				bDidEdit = true;
			}
			
			GUI.backgroundColor = originalBG;
			return bDidEdit;
		}
		
#endif
		/// <summary>
		/// Returns a copy of the current object, including the pertinent parts of
		/// the payload.
		/// </summary>
		/// <returns>A copy of the Payload object.</returns>
		public IPayload GetCopy()
		{
			ColorPayload newPL = new ColorPayload();
			newPL.ColorVal = ColorVal;
			
			return newPL;
		}

		#endregion
	}
}
