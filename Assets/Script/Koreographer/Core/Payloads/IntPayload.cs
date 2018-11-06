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
	/// <see cref="IntPayload"/>-specific functionality.
	/// </summary>
	public static class IntPayloadEventExtensions
	{
		#region KoreographyEvent Extension Methods

		/// <summary>
		/// Determines if the payload is of type <see cref="IntPayload"/>.
		/// </summary>
		/// <returns><c>true</c> if the payload is of type <see cref="IntPayload"/>;
		/// otherwise, <c>false</c>.</returns>
		public static bool HasIntPayload(this KoreographyEvent koreoEvent)
		{
			return (koreoEvent.Payload as IntPayload) != null;
		}

		/// <summary>
		/// Returns the integer associated with the IntPayload.  If the
		/// Payload is not actually of type <see cref="IntPayload"/>, this will return
		/// <c>0</c>.
		/// </summary>
		/// <returns>The <c>int</c> value.</returns>
		public static int GetIntValue(this KoreographyEvent koreoEvent)
		{
			int retVal = 0;
		
			IntPayload pl = koreoEvent.Payload as IntPayload;
			if (pl != null)
			{
				retVal = pl.IntVal;
			}
		
			return retVal;
		}
	
		#endregion
	}

	/// <summary>
	/// The IntPayload class allows Koreorgraphy Events to contain an <c>int</c> value
	/// as a payload.
	/// </summary>
	[System.Serializable]
	public class IntPayload : IPayload
	{
		#region Fields
		
		[SerializeField]
		[Tooltip("The int value that makes up the payload.")]
		int mIntVal;
		
		#endregion
		#region Properties

		/// <summary>
		/// Gets or sets the <c>int</c> value.
		/// </summary>
		/// <value>The <c>int</c> value.</value>
		public int IntVal
		{
			get
			{
				return mIntVal;
			}
			set
			{
				mIntVal = value;
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
			return "Int";
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
			int newVal = EditorGUI.IntField(displayRect, IntVal);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(track, "Modify Int Payload");
				IntVal = newVal;
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
			IntPayload newPL = new IntPayload();
			newPL.IntVal = IntVal;
			
			return newPL;
		}
		
		#endregion
	}
}
