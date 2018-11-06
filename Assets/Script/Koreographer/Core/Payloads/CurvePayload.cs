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
	/// <see cref="CurvePayload"/>-specific functionality.
	/// </summary>
	public static class CurvePayloadEventExtensions
	{
		#region KoreographyEvent Extension Methods

		/// <summary>
		/// Determines if the payload is of type <see cref="CurvePayload"/>.
		/// </summary>
		/// <returns><c>true</c> if the payload is of type <see cref="CurvePayload"/>;
		/// otherwise, <c>false</c>.</returns>
		public static bool HasCurvePayload(this KoreographyEvent koreoEvent)
		{
			return (koreoEvent.Payload as CurvePayload) != null;
		}

		/// <summary>
		/// Returns the curve data associated with the <c>CurvePayload</c>.  If the
		/// Payload is not actually of type <see cref="CurvePayload"/>, this will return
		/// <c>null</c>.
		/// </summary>
		/// <returns>The curve data.</returns>
		public static AnimationCurve GetCurveValue(this KoreographyEvent koreoEvent)
		{
			AnimationCurve retVal = null;

			CurvePayload pl = koreoEvent.Payload as CurvePayload;
			if (pl != null)
			{
				retVal = pl.CurveData;
			}

			return retVal;
		}

		/// <summary>
		/// Gets the value of the curve at <paramref name="sampleTime"/>.  Returns <c>0f</c> if the
		/// Payload is not a CurvePayload.
		/// </summary>
		/// <returns>The float value of the curve at <paramref name="sampleTime"/>.</returns>
		/// <param name="koreoEvent">The 'this' <c>KoreographyEvent</c> for the extension method.</param>
		/// <param name="sampleTime">The specified sample time.</param>
		public static float GetValueOfCurveAtTime(this KoreographyEvent koreoEvent, int sampleTime)
		{
			float retVal = 0f;

			CurvePayload pl = koreoEvent.Payload as CurvePayload;
			if (pl != null)
			{
				retVal = pl.GetValueAtDelta(koreoEvent.GetEventDeltaAtSampleTime(sampleTime));
			}

			return retVal;
		}

		#endregion
	}

	/// <summary>
	/// The CurvePayload class allows Koreorgraphy Events to contain an <c>AnimationCurve</c>
	/// as a payload.
	/// </summary>
	[System.Serializable]
	public class CurvePayload : IPayload
	{
		#region Fields

		[SerializeField]
		[Tooltip("The curve value that makes up the payload.")]
		AnimationCurve mCurveData;

		#endregion
		#region Properties

		/// <summary>
		/// Gets or sets the <c>AnimationCurve</c> data.
		/// </summary>
		/// <value>The <c>AnimationCurve</c> data.</value>
		public AnimationCurve CurveData
		{
			get
			{
				return mCurveData;
			}
			set
			{
				mCurveData = value;
			}
		}

		#endregion
		#region Constructor

		/// <summary>
		/// Initializes a new instance of the <see cref="SonicBloom.Koreo.CurvePayload"/> class.
		/// </summary>
		public CurvePayload()
		{
			mCurveData = GetNewCurve();
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
			return "Curve";
		}

		AnimationCurve GetNewCurve()
		{
			AnimationCurve newCurve = new AnimationCurve();
			newCurve.AddKey(0f, 0f);
			newCurve.AddKey(1f, 1f);
			return newCurve;
		}

		/// <summary>
		/// Gets the value of the curve at <paramref name="delta"/> (range <c>[0,1]</c>).
		/// </summary>
		/// <returns>The value at <paramref name="delta"/>.</returns>
		/// <param name="delta">A value in the range of <c>[0,1]</c>.</param>
		public float GetValueAtDelta(float delta)
		{
			float firstKeyTime = mCurveData[0].time;
			float lastKeyTime = mCurveData[mCurveData.length - 1].time;
			return mCurveData.Evaluate(Mathf.Lerp(firstKeyTime, lastKeyTime, delta));
		}

		#endregion
		#region IPayload Interface

#if (!(UNITY_4_5 || UNITY_4_6 || UNITY_4_7) && UNITY_EDITOR)
		
		static Color CurveBGColor = new Color(100f/255f, 100f/255f, 100f/255f);
		static Color SelectedCurveBGColor = new Color(0f, 100f/255f, 0f);

		/// <summary>
		/// Used for drawing the GUI in the Editor Window (possibly scene overlay?).  Due to an
		/// internal issue with Unity's handling of Curve serialization, Undo is NOT supported.
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
				AnimationCurve dispCurve = new AnimationCurve(CurveData.keys);
				dispCurve = EditorGUI.CurveField(displayRect, dispCurve);
				
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(track, "Modify Curve Payload");
					CurveData = dispCurve;
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
		
#endif

		/// <summary>
		/// Returns a copy of the current object, including the pertinent parts of
		/// the payload.
		/// </summary>
		/// <returns>A copy of the Payload object.</returns>
		public IPayload GetCopy()
		{
			CurvePayload newPL = new CurvePayload();
			newPL.CurveData = new AnimationCurve(CurveData.keys);

			return newPL;
		}

		#endregion
	}
}
