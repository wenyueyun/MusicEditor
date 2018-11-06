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
	/// <see cref="TextPayload"/>-specific functionality.
	/// </summary>
	public static class TextPayloadEventExtensions
	{
		#region KoreographyEvent Extension Methods
	
		/// <summary>
		/// Determines if the payload is of type <see cref="TextPayload"/>.
		/// </summary>
		/// <returns><c>true</c> if the payload is of type <see cref="TextPayload"/>;
		/// otherwise, <c>false</c>.</returns>
		public static bool HasTextPayload(this KoreographyEvent koreoEvent)
		{
			return (koreoEvent.Payload as TextPayload) != null;
		}
	
		/// <summary>
		/// Returns the string associated with the TextPayload.  If the
		/// Payload is not actually of type <see cref="TextPayload"/>, this will return
		/// the empty string.
		/// </summary>
		/// <returns>The <c>string</c>.</returns>
		public static string GetTextValue(this KoreographyEvent koreoEvent)
		{
			string retVal = string.Empty;
		
			TextPayload pl = koreoEvent.Payload as TextPayload;
			if (pl != null)
			{
				retVal = pl.TextVal;
			}
		
			return retVal;
		}
	
		#endregion
	}

	/// <summary>
	/// The TextPayload class allows Koreorgraphy Events to contain a <c>string</c>
	/// as a payload.
	/// </summary>
	[System.Serializable]
	public class TextPayload : IPayload
	{
		#region Fields
		
		[SerializeField]
		[Tooltip("The string value that makes up the payload.")]
		string mTextVal;
		
		#endregion
		#region Properties
		
		/// <summary>
		/// Gets or sets the <c>string</c> value.
		/// </summary>
		/// <value>The <c>string</c> value.</value>
		public string TextVal
		{
			get
			{
				return mTextVal;
			}
			set
			{
				mTextVal = value;
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
			return "Text";
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
			string newVal = EditorGUI.TextField(displayRect, TextVal);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(track, "Modify Text Payload");
				TextVal = newVal;
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
			TextPayload newPL = new TextPayload();
			newPL.TextVal = TextVal;

			return newPL;
		}
	
		#endregion
	}
}
