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
	/// <see cref="AssetPayload"/>-specific functionality.
	/// </summary>
	public static class AssetPayloadEventExtensions
	{
		#region KoreographyEvent Extension Methods
		
		/// <summary>
		/// Determines if the payload is of type <see cref="AssetPayload"/>.
		/// </summary>
		/// <returns><c>true</c> if the payload is of type <see cref="AssetPayload"/>;
		/// otherwise, <c>false</c>.</returns>
		public static bool HasAssetPayload(this KoreographyEvent koreoEvent)
		{
			return (koreoEvent.Payload as AssetPayload) != null;
		}
		
		/// <summary>
		/// Returns the asset reference associated with the AssetPayload.  If the
		/// Payload is not actually of type <see cref="AssetPayload"/>, this will return
		/// null.
		/// </summary>
		/// <returns>The <c>asset reference</c>.</returns>
		public static Object GetAssetValue(this KoreographyEvent koreoEvent)
		{
			Object retVal = null;
			
			AssetPayload pl = koreoEvent.Payload as AssetPayload;
			if (pl != null)
			{
				retVal = pl.AssetVal;
			}
			
			return retVal;
		}
		
		#endregion
	}
	
	/// <summary>
	/// The AssetPayload class allows Koreorgraphy Events to contain a reference to
	/// an asset as a payload.
	/// </summary>
	[System.Serializable]
	public class AssetPayload : IPayload
	{
		#region Fields
		
		[SerializeField]
		[Tooltip("The Asset reference stored in the payload.")]
		Object mAssetVal;
		
		#endregion
		#region Properties
		
		/// <summary>
		/// Gets or sets the asset reference value.
		/// </summary>
		/// <value>The asset reference value.</value>
		public Object AssetVal
		{
			get
			{
				return mAssetVal;
			}
			set
			{
				mAssetVal = value;
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
			return "Asset";
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

			float pickerWidth = 20f;
			Object newVal = null;
			
			if (displayRect.width >= pickerWidth + 2f)
			{
				// HACK to make the background of the picker icon readable.
				Rect pickRect = new Rect(displayRect);
				pickRect.xMin = pickRect.xMax - pickerWidth;
				GUI.Box(pickRect, string.Empty, EditorStyles.textField);
				
				// Draw the Object field.
				newVal = EditorGUI.ObjectField(displayRect, AssetVal, typeof(Object), false);
			}
			else
			{
				// Simply show a text field.
				string name = (AssetVal != null) ? AssetVal.name : "None (Object)";
				string tooltip = isSelected ? "Edit in the \"Selected Event Settings\" section below." : "Select this event and edit in the \"Selected Event Settings\" section below.";
				GUI.Box(displayRect, new GUIContent(name, tooltip), EditorStyles.textField);
			}

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(track, "Modify Asset Payload");
				AssetVal = newVal;
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
			AssetPayload newPL = new AssetPayload();
			newPL.AssetVal = AssetVal;
			
			return newPL;
		}
		
		#endregion
	}
}
