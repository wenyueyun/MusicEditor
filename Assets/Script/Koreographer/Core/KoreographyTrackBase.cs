//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SonicBloom.Koreo
{
	/// <summary>
	/// The basic Koreography Event callback format.  Use this format when no specific
	/// timing information is necessary.
	/// </summary>
	public delegate void KoreographyEventCallback(KoreographyEvent koreoEvent);
	
	/// <summary>
	/// The Koreography Event callback format that includes timing information.  Use
	/// this format to gain access to specific information about the timing of the
	/// event, both globally and within the current frame.
	/// </summary>
	public delegate void KoreographyEventCallbackWithTime(KoreographyEvent koreoEvent, int sampleTime, int sampleDelta, DeltaSlice deltaSlice);
	
	/// <summary>
	/// The base class for all Koreography Track classes. This allows more flexibility
	/// for the user. Each Koreography Track is a single list of Koreography Events.
	/// This class attempts to guarantee that all events in the track are stored in order
	/// by Start Sample position.
	/// </summary>
	[System.Serializable]
	public abstract partial class KoreographyTrackBase : ScriptableObject
	{
		#region Fields
		
		[SerializeField]
		[Tooltip("The Event ID for this Koreography Track.  Used for Event Registration.")]
		string mEventID = string.Empty;
		
		[SerializeField]
		[Tooltip("The complete, ordered list of Koreography Events within this Koreography Track.")]
		List<KoreographyEvent> mEventList = new List<KoreographyEvent>();
		
		event KoreographyEventCallback koreographyEventCallbacks;
		event KoreographyEventCallbackWithTime koreographyEventCallbacksWithTime;
		
		// Used internally as a container to collect KoreographyEvents within the GetEventsInRange and
		//  GetEventsInRangeTracked methods.  This is an optimization to reduce per-frame memory
		//  allocations.
		List<KoreographyEvent> eventsInRange = new List<KoreographyEvent>();
		
		// Used for runtime range checking optimization.  Using this dramatically cuts down on the 
		//  number of events that are scanned each frame, especially with lots of events in the event
		//  list.
		TrackingCrumbs internalTrackingCrumbs;
		
		#endregion
		#region Properties
		
		/// <summary>
		/// Gets or sets the Event ID of the Koreography Track.
		/// </summary>
		/// <value>The <c>string</c> Event ID.</value>
		public string EventID
		{
			get
			{
				return mEventID;
			}
			set
			{
				mEventID = value;
			}
		}
		
		#endregion
		#region Base Methods
		
		void OnEnable()
		{
			internalTrackingCrumbs.Reset();
		}
		
		#endregion
		#region Maintenance Methods
		
		/// <summary>
		/// Checks the integrity of the internal Koreography Event list, removing any <c>null</c>
		/// entries.
		/// </summary>
		/// <returns><c>true</c>, if there was a change to the list during this operation,
		/// <c>false</c> otherwise.</returns>
		public bool CheckEventListIntegrity()
		{
			int startLength = mEventList.Count;
			
			// Remove NULL entries.
			mEventList.RemoveAll(e => e == null);
			
			return (startLength != mEventList.Count);
		}
		
		#endregion
		#region General Methods
		
		/// <summary>
		/// Returns the ID for a given Koreography Event.  The ID of a Koreography Event
		/// is its current position within the Koreogrpahy Track list.
		/// </summary>
		/// <returns>The ID for the given Koreography Event.</returns>
		/// <param name="e">The Koreography Event to retrieve the ID of.</param>
		public int GetIDForEvent(KoreographyEvent e)
		{
			// Returns -1 if not in the list!
			return mEventList.IndexOf(e);
		}
		
		/// <summary>
		/// Ensures the order of Koreography Events in the event list (StartSample
		/// position).
		/// </summary>
		public void EnsureEventOrder()
		{
			mEventList.Sort(KoreographyEvent.CompareByStartSample);
		}
		
		/// <summary>
		/// Returns the first Koreography Event found at the passed in sample position.
		/// </summary>
		/// <returns>The event, if any, with a StartSample at the given sample position.</returns>
		/// <param name="sample">The sample position to check.</param>
		public KoreographyEvent GetEventAtStartSample(int sample)
		{
			KoreographyEvent evt = null;
			
			for (int i = 0; i < mEventList.Count; ++i)
			{
				if (mEventList[i].StartSample == sample)
				{
					evt = mEventList[i];
					break;
				}
			}
			
			return evt;
		}
		
		/// <summary>
		/// Adds a Koreography Event to the Koreography Track.  The event is inserted
		/// in order by StartSample position. The event is NOT added if it is a OneOff
		/// event and another OneOff event already exists with the same StartSample
		/// position.
		/// </summary>
		/// <returns><c>true</c>, if the event was added, <c>false</c> otherwise.</returns>
		/// <param name="addEvent">The Koreography Event to add.</param>
		// TODO: Remove the "no duplicate OneOffs" requirement(?).
		// TODO: Check/verify that the event fits within the song?
		public bool AddEvent(KoreographyEvent addEvent)
		{
			bool bAdd = true;
			
			if (addEvent.IsOneOff())
			{
				KoreographyEvent sameStartEvent = GetEventAtStartSample(addEvent.StartSample);
				if (sameStartEvent != null && sameStartEvent.IsOneOff())
				{
					// Disallow the add!
					bAdd = false;
				}
			}
			
			if (bAdd)
			{
				mEventList.Add(addEvent);
				EnsureEventOrder();
			}
			
			return bAdd;
		}
		
		/// <summary>
		/// Removes the given Koreography Event from the Koreography Track.
		/// </summary>
		/// <returns><c>true</c>, if the Koreography Event was found and removed,
		/// <c>false</c> otherwise.</returns>
		/// <param name="removeEvent">The Koreography Event to remove.</param>
		public bool RemoveEvent(KoreographyEvent removeEvent)
		{
			return mEventList.Remove(removeEvent);
		}
		
		/// <summary>
		/// Removes all Koreography Events from the Koreography Track.
		/// </summary>
		public void RemoveAllEvents()
		{
			mEventList.Clear();
		}
		
		/// <summary>
		/// <para>Returns a list of events in the provided range.  The check includes events
		/// that intersect the range and is inclusive of Start/End positions.</para>
		/// 
		/// <para>This method is now a wrapper.  Please use the other version of
		/// <c>GetEventsInRange</c> directly.</para>
		/// </summary>
		/// <returns>The events defined within the given range.</returns>
		/// <param name="startSample">First sample of the range to search.</param>
		/// <param name="endSample">Last sample of the range to search.</param>
		[System.Obsolete("This method will be removed in a future version.  Please use the new GetEventsInRange(int, int, List) instead.")]
		public List<KoreographyEvent> GetEventsInRange(int startSample, int endSample)
		{
			eventsInRange.Clear();
			GetEventsInRange(startSample, endSample, eventsInRange);
			
			return eventsInRange;
		}
		
		/// <summary>
		/// <para>Adds events in the provided range to the specified list.  The check includes
		/// events that intersect the range and is inclusive of Start/End positions.  The list
		/// is NOT cleared prior to adding events.</para>
		/// 
		/// <para>This method works in O(n) time and is built for random access.</para>
		/// </summary>
		/// <param name="startSample">First sample of the range to search.</param>
		/// <param name="endSample">Last sample of the range to search.</param>
		/// <param name="listToFill">The List into which to add <c>KoreographyEvent</c>s.</param>
		public void GetEventsInRange(int startSample, int endSample, List<KoreographyEvent> listToFill)
		{
			// TODO: Implement a binary search to find the range boundary indices.
			
			int eventListCount = mEventList.Count;
			
			// Collect all events that fit in the range.
			for (int i = 0; i < eventListCount; ++i)
			{
				KoreographyEvent evt = mEventList[i];
				
				// Make use of the fact that the list is sorted.
				if (evt.StartSample > endSample)
				{
					// We're beyond events we care about.  Quick-out!
					break;
				}
				else if (evt.EndSample >= startSample)
				{
					listToFill.Add(evt);
				}
			}
		}
		
		/// <summary>
		/// <para>Returns a list of events in the provided range.  This includes events that
		/// intersect the range and is inclusive of Start/End positions!</para>
		/// 
		/// <para>This method is now a wrapper.  Please use the other version of
		/// <c>GetEventsInRangeTracked</c> directly.  This will be removed in a future update.  
		/// It should be noted that use of this method may result in sub-optimal performance
		/// for complex situations.</para>
		/// </summary>
		/// <returns>The events defined within the given range.</returns>
		/// <param name="startSample">First sample of the range to search.</param>
		/// <param name="endSample">Last sample of the range to search.</param>
		[System.Obsolete("This method will be removed in a future version.  Please use the new GetEventsInRangeTracked(int, int, TrackingCrumbs, List) instead.")]
		public List<KoreographyEvent> GetEventsInRangeTracked(int startSample, int endSample)
		{
			eventsInRange.Clear();
			GetEventsInRangeTracked(startSample, endSample, ref internalTrackingCrumbs, eventsInRange);
			
			return eventsInRange;
		}
		
		/// <summary>
		/// <para>Adds any events found within the provided range to the specified list.  This
		/// includes events that intersect the range and is inclusive of Start/End
		/// positions!  The list is NOT cleared prior to adding events.</para>
		/// 
		/// <para>Please note that the <c>TrackingCrumbs</c> parameter is a struct.  Proper use of
		/// this method involves sending the SAME struct each time.  The contents of the struct are
		/// modified internally in preparation for the next range check.  This is the Tracking
		/// portion of the method.  If you only need a specific set of events and do not need to
		/// track subsequent checks, please use <c>GetEventsInRange</c> instead.</para>
		/// </summary>
		/// <param name="startSample">First sample of the range to search.</param>
		/// <param name="endSample">Last sample of the range to search.</param>
		/// <param name="crumbs">The tracking information about where to begin looking.</param>
		/// <param name="listToFill">The List into which to add <c>KoreographyEvent</c>s.</param>
		public void GetEventsInRangeTracked(int startSample, int endSample, ref TrackingCrumbs crumbs, List<KoreographyEvent> listToFill)
		{
			int eventListCount = mEventList.Count;
			int checkHeadIdx = crumbs.checkHeadIndex;
			
			// Find where to begin adding events if we've jumped around.
			if (startSample != crumbs.lastCheckedEndSample + 1)
			{
				// In this block we find the first event where the ending is greater than the
				//  current startSample, preparing it for the following loop.
				
				// If there is no event with an EndSample beyond the current startSample,
				//  then there isn't another event in the track.  Set the index to the number
				//  of items in the list, which will bypass the for-loop below entirely.
				checkHeadIdx = eventListCount;
				
				// Perform the search.
				for (int i = 0; i < eventListCount; ++i)
				{
					if (mEventList[i].EndSample >= startSample)
					{
						checkHeadIdx = i;
						break;
					}
				}
			}
			
			// Loop over the subsection of events within range.  At worst this should be
			//  an O(n) operation.
			for (int i = checkHeadIdx; i < eventListCount; ++i)
			{
				KoreographyEvent e = mEventList[i];
				
				// Events with an EndSample less than the StartSample are out of range.
				if (e.EndSample < startSample)
				{
					// If this event is no longer in range, update the stored reference to
					//  keep the events we walk to a minimum.  Prefer latter events - keep
					//  in mind that this algorithm assumes that mEventList is sorted by
					//  StartSample.
					// We also make sure that the EndSample of the event in question is
					//  farther out than index we're currently checking.  This helps in the
					//  case that we have multiple simultaneous, overlapping events,
					//  particularly with a span that has multiple OneOffs or shorter spans
					//  within the same time period.
					if (i != checkHeadIdx && e.EndSample >= mEventList[checkHeadIdx].EndSample)
					{
						checkHeadIdx = i;
					}
				}
				// Stop the search once we're starting afresh beyond the end of the range.
				else if (e.StartSample > endSample)
				{
					break;
				}
				// Events are otherwise within range!  Add them to the list!
				else
				{
					// We can assume that the checkHeadIdx is the first valid index, if it wasn't already.
					if (listToFill.Count == 0 && i != checkHeadIdx)
					{
						checkHeadIdx = i;
					}
					
					listToFill.Add(e);
				}
			}
			
			// Update the tracking crumbs!
			crumbs.checkHeadIndex = checkHeadIdx;
			crumbs.lastCheckedEndSample = endSample;
		}
		
		/// <summary>
		/// Get a List of all Koreography Events in this Koreography Track.
		/// </summary>
		/// <returns>Returns a new List with all Koreography Events in the track.
		/// Operations on this list do not affect the internally maintained list.</returns>
		public List<KoreographyEvent> GetAllEvents()
		{
			return new List<KoreographyEvent>(mEventList);
		}
		
		#endregion
		#region Event Registration/Triggering
		
		/// <summary>
		/// <para>Checks for Koreography Events within the current time range.  If events are found,
		/// they are triggered.</para>
		/// 
		/// <para>Note that this <c>startTime</c> should NOT be the same as the previous call's
		/// <c>endTime</c> as the check is inclusive of both <c>startTime</c> and <c>endTime</c>.
		/// This means that if a OneOff were to fall on a boundary and this frame's <c>endTime</c>
		/// became next frame's <c>startTime</c>, that event would be triggered twice.</para>
		/// </summary>
		/// <param name="startTime">Start time of the range in samples.</param>
		/// <param name="endTime">End time of the range in samples.</param>
		/// <param name="deltaSlice">The timing information to be passed to any triggered callbacks.</param>
		public void CheckForEvents(int startTime, int endTime, DeltaSlice deltaSlice)
		{
			eventsInRange.Clear();
			GetEventsInRangeTracked(startTime, endTime, ref internalTrackingCrumbs, eventsInRange);
			
			int numToUpdate = eventsInRange.Count;
			for (int i = 0; i < numToUpdate; ++i)
			{
				KoreographyEvent e = eventsInRange[i];
				
				if (koreographyEventCallbacks != null)
				{
					koreographyEventCallbacks(e);
				}
				if (koreographyEventCallbacksWithTime != null)
				{
					int delta = endTime - startTime;
					koreographyEventCallbacksWithTime(e, endTime, delta, deltaSlice);
				}
			}
		}
		
		internal void RegisterForEvents(KoreographyEventCallback callback)
		{
			koreographyEventCallbacks += callback;
		}
		
		internal void RegisterForEventsWithTime(KoreographyEventCallbackWithTime callback)
		{
			koreographyEventCallbacksWithTime += callback;
		}
		
		internal void UnregisterForEvents(KoreographyEventCallback callback)
		{
			koreographyEventCallbacks -= callback;
		}
		
		internal void UnregisterForEventsWithTime(KoreographyEventCallbackWithTime callback)
		{
			koreographyEventCallbacksWithTime -= callback;
		}
		
		internal void ClearEventRegister()
		{
			koreographyEventCallbacks = null;
			koreographyEventCallbacksWithTime = null;
		}
		
		#endregion
	}
	
	/*
	 * This section of the KoreographyTrackBase class contains internal class and struct
	 *  definitions. The organization is intentional for readability.
	 */
	public abstract partial class KoreographyTrackBase
	{
		/// <summary>
		/// An instance of this struct is required by the <c>GetEventsInRangeTracked</c> method.
		/// It contains state information that helps optimize consecutive calls to the method.  
		/// Please use the SAME INSTANCE of this struct in consecutive calls within the same
		/// logical process.
		/// </summary>
		public struct TrackingCrumbs
		{
			#region Fields
			
			// Marked internal so that external users cannot break things accidentally.
			//  These should only be accessed through special methods like <c>Reset</c>.
			internal int lastCheckedEndSample;
			internal int checkHeadIndex;
			
			#endregion
			#region Properties
			
			/// <summary>
			/// Returns the last end sample position checked in the most recent lookup.
			/// </summary>
			/// <value>The last checked end sample position.</value>
			public int LastCheckedEndSample
			{
				get
				{
					return lastCheckedEndSample;
				}
			}
			
			/// <summary>
			/// Gets the index of the first event to check against in the next lookup.
			/// </summary>
			/// <value>The index of the first event to check in the next lookup.</value>
			public int CheckHeadIndex
			{
				get
				{
					return checkHeadIndex;
				}
			}
			
			#endregion
			#region Methods
			
			/// <summary>
			/// Resets the state of the crumbs with the expectation that the tracking will
			/// start at the beginning of the internal KoreographyEvent List.
			/// </summary>
			public void Reset()
			{
				lastCheckedEndSample = -1;
				checkHeadIndex = 0;
			}
			
			#endregion
		}
	}
	
	/*
	 * ===Koreography Payload Serialization Implementation===
	 *  Each Payload class must implement the IPayload interface
	 *   and provide a partial KoreographyTrackBase class implementation. Each
	 *   such partial class must inlude two lists of the following formats:
	 * 
	 *     [SerializeField][HideInInspector]
	 *     List<[PayloadClass]> _[PayloadClass]s;
	 * 
	 *     [SerializeField][HideInInspector]
	 *     List<int>			_[PayloadClass]Idxs;
	 * 
	 *  The [SerializeField] attribute must exist, while the 
	 *   [HideInInspector] field remains optional but is recommended.
	 * 
	 *  These lists are used by the Serialization system through
	 *   reflection to actually store the KoreographyEvent Payloads across
	 *   session boundaries.
	 */
	public abstract partial class KoreographyTrackBase : ISerializationCallbackReceiver
	{
		#region Serialization Handling
		
		[HideInInspector][SerializeField]
		List<string> _SerializedPayloadTypes;
		
		#endregion
		#region Serialization Interface and Support Methods
		
		/// <summary>
		/// Koreography Tracks require special serialization marshalling.  This is for use
		/// by Unity's serialization system only!  (From: ISerializationCallbackReceiver
		/// interface.)
		/// </summary>
		public void OnBeforeSerialize()
		{
			// The type of this object. Enables KoreographyTrackBase subclasses to serialize correctly.
			System.Type thisType = GetType();
			
			// We must clear the payload lists.  This is to make sure that we don't have any data stored
			//  hidden away somewhere.  In theory this could happen when the user deletes the last few
			//  entries of a specific type from the payloads.  When "Save" was called, the lists might
			//  get generated and store the data.  Then, if there's no more references to that type in the
			//  KoreographyEvents list, we wouldn't check it here and we wouldn't know to check it in
			//  OnAfterDeserialize, either.  By resetting the payload lists here, we make sure we have a
			//  clean slate prior to reading out the actual payloads from the KoreographyEvents list.
			// Apparently the Unity Editor Serializes twice (with no Deserialize in between) when
			//  "File->Save Project" is selected.
			if (_SerializedPayloadTypes == null)
			{
				_SerializedPayloadTypes = new List<string>();
			}
			else
			{
				// Clear out the lists!
				for (int i = 0; i < _SerializedPayloadTypes.Count; ++i)
				{
					string typeStr = _SerializedPayloadTypes[i];
					string fieldNameStr = typeStr.Split('.').Last();
					
					// Find Data storage.
					FieldInfo plListInfo = GetFieldInfoOfListWithTypeString(thisType, typeStr, "_" + fieldNameStr + "s");
					if (plListInfo == null)
					{
						continue;       // Errors/warnings handled by method used to grab the field.
					}
					
					// Find Index storage.
					FieldInfo plListIdxsInfo = GetFieldInfoOfListWithTypeString(thisType, typeof(int).ToString(), "_" + fieldNameStr + "Idxs");
					if (plListIdxsInfo == null)
					{
						continue;       // Errors/warnings handled by method used to grab the field.
					}
					
					// Clear the lists.
					plListInfo.SetValue(this, null);
					plListIdxsInfo.SetValue(this, null);
				}
				
				_SerializedPayloadTypes.Clear();
			}
			
			// Maps for storing Type->Field links (means we only have to reflect twice per Type).
			Dictionary<System.Type, FieldInfo> plMaps = new Dictionary<System.Type, FieldInfo>();
			Dictionary<System.Type, FieldInfo> plIdxMaps = new Dictionary<System.Type, FieldInfo>();
			
			// Iterate over events, storing payloads!
			for (int i = 0; i < mEventList.Count; ++i)
			{
				IPayload pl = mEventList[i].Payload;
				
				// Only do the work if there's actually a payload to worry about.
				if (pl == null)
				{
					continue;
				}
				
				System.Type plType = pl.GetType();
				
				FieldInfo plListInfo = null;
				FieldInfo plListIdxsInfo = null;
				
				// Grab the cached FieldInfos.  If they aren't cached yet, search for them and add them.
				if (plMaps.ContainsKey(plType))
				{
					// If we have one, we have both!
					plListInfo = plMaps[plType];
					plListIdxsInfo = plIdxMaps[plType];
				}
				else
				{
					// We don't have the lists.  Find them.
					
					// Find Data storage.
					plListInfo = GetFieldInfoOfListWithType(thisType, plType, "_" + plType.Name + "s");
					if (plListInfo == null)
					{
						continue;		// Errors/warnings handled by method used to grab the field.
					}
					
					// Find Index storage.
					plListIdxsInfo = GetFieldInfoOfListWithType(thisType, typeof(int), "_" + plType.Name + "Idxs");
					if (plListIdxsInfo== null)
					{
						continue;       // Errors/warnings handled by method used to grab the field.
					}
					
					// Data storage.
					plListInfo.SetValue(this, System.Activator.CreateInstance(plListInfo.FieldType));
					plMaps.Add(plType, plListInfo);
					// Index storage.
					plListIdxsInfo.SetValue(this, new List<int>());
					plIdxMaps.Add(plType, plListIdxsInfo);
					
					// Make a note that we've serialized this type.
					_SerializedPayloadTypes.Add(plType.ToString());
				}
				
				// Grab the actual lists.
				System.Collections.IList plList = plListInfo.GetValue(this) as System.Collections.IList;
				List<int> plListIdxs = plListIdxsInfo.GetValue(this) as List<int>;
				
				// Actually store the payload into a list that will save!
				plList.Add(pl);
				plListIdxs.Add(i);
			}
		}
		
		/// <summary>
		/// Koreography Tracks require special serialization marshalling.  This is for use
		/// by Unity's serialization system only!  (From: ISerializationCallbackReceiver
		/// interface.)
		/// </summary>
		public void OnAfterDeserialize()
		{
			// Early out if nothing was serialized.
			if (_SerializedPayloadTypes == null)
			{
				return;
			}
			
			for (int i = 0; i < _SerializedPayloadTypes.Count; ++i)
			{
				// The type of this object. Enables KoreographyTrackBase subclasses to serialize correctly.
				System.Type thisType = GetType();
				
				// Full name from the payload types.
				string typeStr = _SerializedPayloadTypes[i];
				
				// The field name as just the "Type" (ignore potential namespace).
				string fieldNameStr = typeStr.Split('.').Last();
				
				// Verify that we even have a non-null (empty) list.
				FieldInfo plListInfo = GetFieldInfoOfListWithTypeString(thisType, typeStr, "_" + fieldNameStr + "s");
				if (plListInfo == null)
				{
					continue;       // Errors/warnings handled by method used to grab the field.
				}
				
				// Get the actual storage list as an IList.  This will allow indexing.  We can cast the 
				//  resultant generic object to IPayload to make sure the Payload connection happens.
				System.Collections.IList plList = plListInfo.GetValue(this) as System.Collections.IList;
				
				if (plList == null)
				{
					Debug.LogError("Error retreiving Payload storage list 'List<" + typeStr + "> _" + fieldNameStr +
					               "s'.  This should never happen: please check your implementation.");
					continue;
				}
				
				// Grab the index list field type.
				FieldInfo plListIdxsInfo = GetFieldInfoOfListWithTypeString(thisType, typeStr, "_" + fieldNameStr + "Idxs");
				if (plListIdxsInfo == null)
				{
					continue;       // Errors/warnings handled by method used to grab the field.
				}
				
				// Get the acrtual index list with Payload info stored in them.
				List<int> plListIdxs = plListIdxsInfo.GetValue(this) as List<int>;
				
				if (plListIdxs == null)
				{
					Debug.LogError("Payload storage indexing list '" + plListIdxsInfo.Name + "' probably has an incorrect type declaration." +
					               "\n\tExpected: 'List<int>'\n\tFound: '" + plListIdxsInfo.FieldType + "'");
					continue;
				}
				
				// Add the payloads back to the events.
				for (int j = 0; j < plList.Count; ++j)
				{
					mEventList[plListIdxs[j]].Payload = plList[j] as IPayload;
				}
				
				// All done with these lists.  Clear them!
				plListInfo.SetValue(this, null);
				plListIdxsInfo.SetValue(this, null);
			}
			
			// Clear out Serialized Payload Types to get next possible Serialization pass
			//  moving faster.  Also saves memory.
			_SerializedPayloadTypes = null;
		}
		
		/// <summary>
		/// Retrieves the <c>FieldInfo</c> object from a provided type that describes the field of a specific name.
		/// </summary>
		/// <param name="sourceType">The <c>System.Type</c> upon which to search for the target field.</param>
		/// <param name="lookupType">The <c>System.Type</c> to look for.</param>
		/// <param name="fieldName">The name of the list to look up.</param>
		/// <returns>The <c>FieldInfo</c> object that identifies the requested List&lt;type&gt; field.</returns>
		static FieldInfo GetFieldInfoOfListWithType(System.Type sourceType, System.Type lookupType, string fieldName)
		{
			FieldInfo listInfo = GetFieldInfoFromName(sourceType, fieldName);
			if (listInfo == null)
			{
				Debug.LogError("Serialization Error: No 'List<" + lookupType.ToString() + "> _" + lookupType.Name + "s' defined in " + sourceType.Name + " class!");
			}
			else if (!IsGenericList(listInfo.FieldType))
			{
				Debug.LogError("Serialization Error: Field called '" + listInfo.Name + "' is not a List<>!");
				listInfo = null;
			}
#if NETFX_CORE
			// New .NET moved GetGenericArguments.  Use this to bypass and enable more portable
			//  code.  This can still be compiled with older compilers and run on modern .NET runtimes.
			else if (!listInfo.FieldType.ToString().Contains(lookupType.ToString()))
#else
				else if (listInfo.FieldType.GetGenericArguments()[0] != lookupType)
#endif
			{
				Debug.LogError("Serialization Error: Field called '" + listInfo.Name + "' is not a list of the expected type '" +
				               lookupType.ToString() + "'. Actual full type of field is: " + listInfo.FieldType.Name);
				listInfo = null;
			}
			return listInfo;
		}
		
		/// <summary>
		/// Retrieves the <c>FieldInfo</c> object that describes the field of a specific name.
		/// </summary>
		/// <param name="sourceType">The <c>System.Type</c> upon which to find the specified field.</param>
		/// <param name="typeStr">The fully qualified (i.e. namespace included) name of the type as a string.</param>
		/// <param name="fieldName">The name of the list to look up.</param>
		/// <returns>The <c>FieldInfo</c> object that identifies the requested List&lt;type&gt; field.</returns>
		static FieldInfo GetFieldInfoOfListWithTypeString(System.Type sourceType, string typeStr, string fieldName)
		{
			// Verify that we even have a non-null (empty) list.
			FieldInfo listInfo = GetFieldInfoFromName(sourceType, fieldName);
			if (listInfo == null)
			{
				Debug.LogError("Payload storage list 'List<" + typeStr + "> " + fieldName +
				               "' could not be retreived. Old data or did someone change the name?");
			}
			else if (!IsGenericList(listInfo.FieldType))
			{
				Debug.LogError("Payload storage list 'List<" + typeStr + "> " + fieldName + 
				               "' is not actually of type List<>. Please check your implementation.");
				listInfo = null;
			}
			return listInfo;
		}
		
		/// <summary>
		/// Determines whether the passed in <c>System.Type</c> describes a generic List&lt;&gt; container or not.
		/// </summary>
		/// <param name="type">The <c>System.Type</c> to check.</param>
		/// <returns><c>True</c> if it is a generlic list, <c>false</c> otherwise.</returns>
		static bool IsGenericList(System.Type type)
		{
			return type.GetGenericTypeDefinition() == typeof(List<>);
		}
		
		/// <summary>
		/// Gets the <c>FieldInfo</c> for the field with name <paramref name="name"/> on the
		/// provided class type <paramref name="sourceType"/>.
		/// </summary>
		/// <returns>The <c>FieldInfo</c> object of the field with the given name on the provided type.</returns>
		/// <param name="sourceType">The type on which to look up the field name.</param>
		/// <param name="name">The name of the field to look up.</param>
		static FieldInfo GetFieldInfoFromName(System.Type sourceType, string name)
		{
#if NETFX_CORE
			// The 'foreach' is not scary for NETFX_CORE as it runs on Microsoft's runtime.  Even the code
			//  is compiled with the new Mono compiler which shouldn't have the boxing issues of Unity's
			//  outdated mono.
			FieldInfo retInfo = null;
			foreach (FieldInfo info in sourceType.GetTypeInfo().DeclaredFields)
			{
				if (info.Name == name)
				{
					retInfo = info;
					break;
				}
			}
			return retInfo;
#else
			return sourceType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
#endif
		}
		
		#endregion
	}
}
