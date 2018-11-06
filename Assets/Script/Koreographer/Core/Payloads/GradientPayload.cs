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
	/// <see cref="GradientPayload"/>-specific functionality.
	/// </summary>
	public static class GradientPayloadEventExtensions
	{
		#region KoreographyEvent Extension Methods

		/// <summary>
		/// Determines if the payload is of type <see cref="GradientPayload"/>.
		/// </summary>
		/// <returns><c>true</c> if the payload is of type <see cref="GradientPayload"/>;
		/// otherwise, <c>false</c>.</returns>
		public static bool HasGradientPayload(this KoreographyEvent koreoEvent)
		{
			return (koreoEvent.Payload as GradientPayload) != null;
		}

		/// <summary>
		/// Returns the gradient data associated with the GradientPayload.  If the
		/// Payload is not actually of type <see cref="GradientPayload"/>, this will return
		/// <c>null</c>.
		/// </summary>
		/// <returns>The <c>Gradient</c> data.</returns>
		public static Gradient GetGradientData(this KoreographyEvent koreoEvent)
		{
			Gradient retVal = null;
		
			GradientPayload pl = koreoEvent.Payload as GradientPayload;
			if (pl != null)
			{
				retVal = pl.GradientData;
			}
		
			return retVal;
		}

		/// <summary>
		/// Gets the color of the gradient at <paramref name="sampleTime"/>.  Returns the default
		/// color (white) if the payload is not a <see cref="GradientPayload"/>.
		/// </summary>
		/// <returns>The color of the gradient at <paramref name="sampleTime"/>.</returns>
		/// <param name="koreoEvent">The 'this' <c>KoreographyEvent</c> for the extension method.</param>
		/// <param name="sampleTime">The specified sample time.</param>
		public static Color GetColorOfGradientAtTime(this KoreographyEvent koreoEvent, int sampleTime)
		{
			Color retVal = Color.white;
		
			GradientPayload pl = koreoEvent.Payload as GradientPayload;
			if (pl != null)
			{
				retVal = pl.GradientData.Evaluate(koreoEvent.GetEventDeltaAtSampleTime(sampleTime));
			}
		
			return retVal;
		}

		#endregion
	}

	/// <summary>
	/// The GradientPayload class allows Koreorgraphy Events to contain a <c>Gradient</c>
	/// as a payload.
	/// </summary>
	[System.Serializable]
	public class GradientPayload : IPayload
	{
		#region Fields

		[SerializeField]
		[Tooltip("The gradient value that makes up the payload.")]
		Gradient mGradientData = new Gradient();

		#endregion
		#region Properties

		/// <summary>
		/// Gets or sets the <c>Gradient</c> data.
		/// </summary>
		/// <value>The <c>Gradient</c> data.</value>
		public Gradient GradientData
		{
			get
			{
				return mGradientData;
			}
			set
			{
				mGradientData = value;
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
			return "Gradient";
		}

		#endregion
		#region IPayload Interface

#if (!(UNITY_4_5 || UNITY_4_6 || UNITY_4_7) && UNITY_EDITOR)

		System.Reflection.MethodInfo GradientField;

		/// <summary>
		/// Used for drawing the GUI in the Editor Window (possibly scene overlay?).  Undo is
		/// supported.  Uses a special version of the <c>GradientField</c> drawing methods that
		/// isn't generally accessible due to an oversight at Unity.
		/// </summary>
		/// <returns><c>true</c>, if the Payload was edited in the GUI, <c>false</c>
		/// otherwise.</returns>
		/// <param name="displayRect">The <c>Rect</c> within which to perform GUI drawing.</param>
		/// <param name="track">The Koreography Track within which the Payload can be found.</param>
		/// <param name="isSelected">Whether or not the Payload (or the Koreography Event that
		/// contains the Payload) is selected in the GUI.</param>
		public bool DoGUI(Rect displayRect, KoreographyTrackBase track, bool isSelected)
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
			dispGradient.SetKeys(GradientData.colorKeys, GradientData.alphaKeys);
			
			EditorGUI.BeginChangeCheck();
			object[] parameters = {displayRect, dispGradient};
			dispGradient = (Gradient)GradientField.Invoke(null, parameters);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(track, "Modify Gradient Payload");
				GradientData = dispGradient;
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
			GradientPayload newPL = new GradientPayload();
			newPL.GradientData = GradientData;
			
			return newPL;
		}

		#endregion
	}
}
