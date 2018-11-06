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
	/// <see cref="FloatPayload"/>-specific functionality.
	/// </summary>
	public static class FloatPayloadEventExtensions
	{
		#region KoreographyEvent Extension Methods

		/// <summary>
		/// Determines if the payload is of type <see cref="FloatPayload"/>.
		/// </summary>
		/// <returns><c>true</c> if the payload is of type <see cref="FloatPayload"/>;
		/// otherwise, <c>false</c>.</returns>
		public static bool HasFloatPayload(this KoreographyEvent koreoEvent)
		{
			return (koreoEvent.Payload as FloatPayload) != null;
		}

		/// <summary>
		/// Returns the <c>float</c> associated with the FloatPayload.  If the
		/// Payload is not actually of type <see cref="FloatPayload"/>, this will return
		/// <c>0f</c>.
		/// </summary>
		/// <returns>The <c>float</c> value.</returns>
		public static float GetFloatValue(this KoreographyEvent koreoEvent)
		{
			float retVal = 0f;
		
			FloatPayload pl = koreoEvent.Payload as FloatPayload;
			if (pl != null)
			{
				retVal = pl.FloatVal;
			}
		
			return retVal;
		}
	
		#endregion
	}

	/// <summary>
	/// The FloatPayload class allows Koreorgraphy Events to contain a <c>float</c> value
	/// as a payload.
	/// </summary>
	[System.Serializable]
	public class FloatPayload : IPayload
	{
		#region Fields
		
		[SerializeField]
		[Tooltip("The float value that makes up the payload.")]
		float mFloatVal;
		
		#endregion
		#region Properties
		
		/// <summary>
		/// Gets or sets the <c>float</c> value.
		/// </summary>
		/// <value>The <c>float</c> value.</value>
		public float FloatVal
		{
			get
			{
				return mFloatVal;
			}
			set
			{
				mFloatVal = value;
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
			return "Float";
		}

		#endregion
		#region IPayload Interface

#if (!(UNITY_4_5 || UNITY_4_6 || UNITY_4_7) && UNITY_EDITOR)

		/// <summary>
		/// Used for drawing the GUI in the Editor Window (possibly scene overlay?).  Undo is
		/// supported.
		/// </summary>
		/// <returns><c>true</c>, if the Payload was edited in the GUI, <c>false</c>
		/// otherwise.</returns>
		/// <param name="displayRect">The <c>Rect</c> within which to perform GUI drawing.</param>
		/// <param name="track">The Koreography Track within which the Payload can be found.</param>
		/// <param name="isSelected">Whether or not the Payload (or the Koreography Event that
		/// contains the Payload) is selected in the GUI.</param>
		public bool DoGUI(Rect displayRect, KoreographyTrackBase track, bool isSelected)
		{
			bool bDidEdit = false;
			Color originalBG = GUI.backgroundColor;
			GUI.backgroundColor = isSelected ? Color.green : originalBG;

			EditorGUI.BeginChangeCheck();
			float newVal = EditorGUI.FloatField(displayRect, FloatVal);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(track, "Modify Float Payload");
				FloatVal = newVal;
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
			FloatPayload newPL = new FloatPayload();
			newPL.FloatVal = FloatVal;

			return newPL;
		}
	
		#endregion
	}
}
