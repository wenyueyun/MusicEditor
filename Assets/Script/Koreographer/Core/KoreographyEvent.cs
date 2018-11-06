//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;
using System.Linq;

namespace SonicBloom.Koreo
{
	/// <summary>
	/// The Interface used to define Koreography Payloads.  This is currently used
	/// mainly as a classifier but does provide functionality in Editor contexts.
	/// </summary>
	public interface IPayload
	{
#if (!(UNITY_4_5 || UNITY_4_6 || UNITY_4_7) && UNITY_EDITOR)
		/// <summary>
		/// Used for drawing the GUI in the Editor Window (possibly scene overlay?). Undo
		/// support is available but must be implemented in this method.
		/// </summary>
		/// <returns><c>true</c>, if the Payload was edited in the GUI, <c>false</c>
		/// otherwise.</returns>
		/// <param name="displayRect">The <c>Rect</c> within which to perform GUI drawing.</param>
		/// <param name="track">The Koreography Track within which the Payload can be found.</param>
		/// <param name="isSelected">Whether or not the Payload (or the Koreography Event that
		/// contains the Payload) is selected in the GUI.</param>
		bool DoGUI(Rect displayRect, KoreographyTrackBase track, bool isSelected);
#endif

		/// <summary>
		/// Returns a copy of the current object, including the pertinent parts of
		/// the payload.
		/// </summary>
		/// <returns>A copy of the Payload object.</returns>
		IPayload GetCopy();
	}

	/// <summary>
	/// The base Koreography Event definition.  Each event instance can carry
	/// a single Payload.  Events can span a range of samples or can be tied
	/// to a single one.
	/// 
	/// Sample values (Start/End) are in "Sample Time" range, *NOT* absolute
	/// sample position.  Be sure that querries/comparisons occur in TIME and
	/// not DATA space.
	/// </summary>
	[System.Serializable]
	public class KoreographyEvent
	{
		#region Fields

		[SerializeField]
		[Tooltip("The sample position at which this event starts.")]
		int mStartSample = 0;

		[SerializeField]
		[Tooltip("The sample position at which this event ends.")]
		int mEndSample = 0;

        [SerializeField]
        [Tooltip("The sample position at which this event starts.")]
        int mStartTime = 0;

        [SerializeField]
        [Tooltip("The sample position at which this event ends.")]
        int mEndTime = 0;
        // The data is serialized by the Koreography Track in the
        //  ISerializationCallbackReceiver method implementations.
        IPayload mPayload = null;

        #endregion
        #region Properties

        public int StartTime
        {
            get
            {
                return mStartTime;
            }
            set
            {
                // Start Sample should never fall below 0.
                mStartTime = Mathf.Max(0, value);

                // Move these together.
                if (mStartTime > mEndTime)
                {
                    mEndTime = mStartTime;
                }
            }
        }

        public int EndTime
        {
            get
            {
                return mEndTime;
            }
            set
            {
                mEndTime = Mathf.Max(0, value);

                if (mEndTime < mStartTime)
                {
                    mStartTime = mEndTime;
                }

            }
        }

        /// <summary>
        /// Gets or sets the start sample.
        /// </summary>
        /// <value>The start sample of the Koreography Event.</value>
        public int StartSample
		{
			get
			{
				return mStartSample;
			}
			set
			{
				// Start Sample should never fall below 0.
				mStartSample = Mathf.Max(0, value);

				// Move these together.
				if (mStartSample > mEndSample)
				{
					mEndSample = mStartSample;
				}
			}
		}
		
		/// <summary>
		/// Gets or sets the end sample.
		/// </summary>
		/// <value>The end sample of the Koreography Event.</value>
		public int EndSample
		{
			get
			{
				return mEndSample;
			}
			set
			{
				mEndSample = Mathf.Max(0, value);

				if (mEndSample < mStartSample)
				{
					mStartSample = mEndSample;
				}

			}
		}

		/// <summary>
		/// Gets or sets the payload.
		/// </summary>
		/// <value>The payload of the Koreography Event.  Can be <c>null</c>.</value>
		public IPayload Payload
		{
			get
			{
				return mPayload;
			}
			set
			{
				mPayload = value;
			}
		}

		#endregion
		#region Static Methods

		/// <summary>
		/// Compares two Koreography Events by their start sample.
		/// </summary>
		/// <returns><c>-1</c> if the <c>StartSample</c> of <paramref name="first"/> is earlier
		/// than the <c>StartSample</c> of <paramref name="second"/>, <c>0</c> if they are equal;
		/// <c>-1</c> otherwise.</returns>
		/// <param name="first">A first Koreography Event to compare.</param>
		/// <param name="second">A second Koreography Event to compare.</param>
		public static int CompareByStartSample(KoreographyEvent first, KoreographyEvent second)
		{
			if (first.StartSample < second.StartSample)
			{
				return -1;
			}
			else if (first.StartSample == second.StartSample)
			{
				return 0;
			}
			else
			{
				return 1;
			}
		}
	
		#endregion
		#region Methods

		/// <summary>
		/// Determines whether this Koreography Event is a OneOff or not.  OneOff events have a
		/// range/span of 0 (their <c>EndSample</c> is the same as their <c>StartSample</c>).
		/// </summary>
		/// <returns><c>true</c> if this Koreography Event is a OneOff; otherwise, <c>false</c>.</returns>
		public bool IsOneOff()
		{
			return StartSample == EndSample;
		}

		/// <summary>
		/// Gets the event delta at <paramref name="sampleTime"/>.  The delta is clamped
		/// to a range of [0,0, 1.0].  If <paramref name="sampleTime"/> is not within this
		/// event's range, it returns <c>0</c> or <c>1</c> depending of if it comes
		/// before or after, respectively.
		/// </summary>
		/// <returns>The delta within the range of the event represented by <paramref name="sampleTime"/>.</returns>
		/// <param name="sampleTime">The sample time to get the delta of.</param>
		public float GetEventDeltaAtSampleTime(int sampleTime)
		{
			float retVal = -1f;

			// TODO: Add an OutOfRange value?  Error?
			if (sampleTime < StartSample)
			{
				retVal = 0f;
			}
			else if (sampleTime > EndSample ||	// Check that we're beyond the end.
					 IsOneOff())				// Logic order is important here(?), enabling this check!
			{
				retVal = 1f;
			}
			else
			{
				// We don't use Mathf.InverseLerp here because we want to handle the OneOff case as above.
				//  When 'to' and 'from' in InverseLerp are equal, it always returns 0.
				retVal = (float)((double)(sampleTime - StartSample) / (double)(EndSample - StartSample));
			}

			return retVal;
		}

		#endregion
	}
}
