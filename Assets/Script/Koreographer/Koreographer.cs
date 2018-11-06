//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;
using System.Collections.Generic;

namespace SonicBloom.Koreo
{
	/// <summary>
	/// The Interface that Koreographer uses to get certain runtime information
	/// about the state of audio.
	/// </summary>
	public interface IKoreographedPlayer
	{
		int GetSampleTimeForClip(string clipName);
		int GetTotalSampleTimeForClip(string clipName);
		bool GetIsPlaying(string clipName);
		float GetPitch(string clipName);
		string GetCurrentClipName();
	}

	/// <summary>
	/// A slice of the unscaled delta time of a given frame.  This enables users to properly offset
	///  logic based on where the current timing is.
	/// </summary>
	public struct DeltaSlice
	{
		/// <summary>
		/// The starting point of the current unscaled delta of this slice.  Range: [0,1].
		/// </summary>
		public float deltaOffset;
		/// <summary>
		/// The length in seconds that this slice "consumes" of the unscaled delta.
		/// </summary>
		public float deltaLength;
	}
	
	// TODO: "Processed Frame Results" should be stored internally such that the Records can
	//  be shared between entries.  This would enable multiple Koreography to process the same
	//  time slice without having to do the math over again.
	/// <summary>
	/// A Timing Record for delayed processing of Koreography.
	/// </summary>
	struct TimingRecord
	{
		public double timeLeft;		// Amount of time until this TimingRecord is done processing.
		public double playTime;		// Amount of time spent between start/end sample.
	
		public int startSample;		// First audio read position.
		public int endSample;		// Second audio read position.
	}


	/// <summary>
	/// <para>The Koreographer is the object responsible for triggering events.  It takes the current music
	/// point, querries the Koreography for events at those times, and then sends cues to "actors" that
	/// are listening for directions.</para>
	/// <para>This setup *does not* stop an "actor" from looking directly at a specific Koreography Track.
	/// This is fully supported.  It just means that that actor will only get cues for the Tracks for
	/// which it has registered.</para>
	/// <para>The music is "reported" to the Koreographer.  In this sense, the Koreographer "Hears" the
	/// music.</para>
	/// 
	/// <para>The Koreographer manages a list of Koreography.  These are considered "loaded" and any
	/// time the associated music is played, their events are triggered.</para>
	/// 
	/// <para>When Koreography is loaded, the Koreographer finds-or-adds a string->EventObj mapping to a managed
	/// list.  This allows the Koreographer to get notified when Koreography Events occur directly from the
	/// triggered tracks.  The events are then automatically forwarded to those who registered for
	/// such events with the Koreographer.</para>
	/// </summary>
	[AddComponentMenu("Koreographer/Koreographer")]
	public class Koreographer : MonoBehaviour
	{
		#region Fields

		static Koreographer _instance = null;

		/// <summary>
		/// Returns the static Koreographer singleton instance.
		/// </summary>
		/// <value>The current singleton instance of Koreographer.</value>
		public static Koreographer Instance { get { return _instance; } }

		/// <summary>
		/// A reference to the currently configured Music Playback Controller.
		/// </summary>
		public IKoreographedPlayer musicPlaybackController = null;

		[SerializeField]
		[Tooltip("Koreography that should be auto-loaded by this Koreographer at start.")]
		List<Koreography> loadedKoreography = new List<Koreography>();
		
		[SerializeField]
		[Tooltip("The amount of time in seconds that event processing should be delayed.")]
		float eventDelayInSeconds = 0f;
		
		// Queued updates to handle intentional delay.
		List<KeyValuePair<Koreography, List<TimingRecord>>> delayQueue = new List<KeyValuePair<Koreography, List<TimingRecord>>>();

		// Like an operator's switch board.
		List<KeyValuePair<string,EventObj>> eventBoard = new List<KeyValuePair<string, EventObj>>();
		
		// Stored to reduce runtime memory allocations.  This is used to ensure consistent processing of
		//  Koreography in light of the ability to Add/Remove Koreography to/from the loadedKoreography
		//  list at any time, including during Koreography event processing callbacks.
		List<Koreography> koreographyToProcess = new List<Koreography>();

		// Stored to reduce runtime memory allocations.  This is used by certain APIs (e.g. GetAllEventsInRange)
		//  to track processed KoreographyTracks during event lookups, etc.  This is not currently instantiated
		//  by default as the APIs that use it are not part of the Koreographer class' core functionality.
		List<KoreographyTrackBase> trackProcessingHelper;

		#endregion
		#region Properties

		/// <summary>
		/// Gets or sets the Event Delay In Seconds.
		/// </summary>
		/// <value>Time in seconds by which Koreography Event triggering should be delayed.</value>
		public float EventDelayInSeconds
		{
			get
			{
				return eventDelayInSeconds;
			}
			set
			{
				eventDelayInSeconds = Mathf.Max(0f, value);
			}
		}

		#endregion
		#region Standard Methods

		void Awake()
		{
			_instance = this;
			
			eventDelayInSeconds = Mathf.Max(0f, eventDelayInSeconds);	// EventDelayInSeconds cannot be negative.

			// Ensure that we properly reset Koreography timings.
			for (int i = 0; i < loadedKoreography.Count; ++i)
			{
				loadedKoreography[i].ResetTimings();
			}
		}

		void OnDestroy()
		{
			// Clear out registrations.
			ClearEventRegister();

			// Remove the static reference to enable Garbage Collection properly.
			if (_instance == this)
			{
				_instance = null;
			}
		}

		void Update()
		{
			// Technically this should be done after the ProcessKoreography() pass.
			//  Currently it is actually done *before* that pass, based on the fact that
			//  the Koreographer script is set to update *before* the Player...
			// TODO: Look into this.  It's possible that it isn't necessary to process
			//  the MusicPlayer that early.
			if (delayQueue.Count > 0)
			{
				ProcessDelayQueue(Time.unscaledDeltaTime);
			}
		}
		
		#endregion
		#region Koreography Processing

		/// <summary>
		/// Process Koreography with <c>AudioClip</c> <paramref name="clip"/> given the
		/// specified timing information.
		/// </summary>
		/// <param name="clip">The <c>AudioClip</c> for which Koreography Events should
		/// be triggered.</param>
		/// <param name="startTime">The start time in samples of the range of time within
		/// <paramref name="clip"/>that should be processed.</param>
		/// <param name="endTime">The end time in samples of the range of time within
		/// <paramref name="clip"/> that should be processed.</param>
		/// <param name="deltaSlice">Extra timing information to be passed to events about
		/// the current processing pass.</param>
		[System.Obsolete("Please use Koreographer.ProcessKoreography instead.  This method will disappear in a future release.", false)]
		public void ProcessChoreography(AudioClip clip, int startTime, int endTime, DeltaSlice deltaSlice)
		{
			ProcessKoreography(clip.name, startTime, endTime, deltaSlice);
		}

		/// <summary>
		/// Process loaded Koreography associated with the audio data identified by <paramref name="clipName"/>
		/// using the specified timing information.
		/// </summary>
		/// <param name="clipName">The name of the audio data for which Koreography Events should
		/// be triggered.</param>
		/// <param name="startTime">The start time in samples of the range of time within the audio
		/// <paramref name="clipName"/> that should be processed.</param>
		/// <param name="endTime">The end time in samples of the range of time within the audio  
		/// <paramref name="clipName"/> that should be processed.</param>
		/// <param name="deltaSlice">Extra timing information to be passed to events about
		/// the current processing pass.</param>
		public void ProcessKoreography(string clipName, int startTime, int endTime, DeltaSlice deltaSlice)
		{
            Debug.Log(string.Format("startTime: {0}    endTime: {1}  deltaLength:{2}", startTime,endTime, deltaSlice.deltaLength));
			// Ensure that we're in a sane state: we shouldn't ever get in here with a full
			//  koreographyToProcess list as we do not currently support triggering
			//  ProcessKoreography from within an ongoing ProcessKoreography pass [on a
			//  single Koreographer instance].  The other way we could get this would be if
			//  there was an exception that halted the previous pass.
			if (koreographyToProcess.Count > 0)
			{
				Debug.LogWarning("Beginning to Process Koreography with a list of Koreography that should " +
				                 "have been processed. Please check that you are not calling ProcessKoreography during " +
				                 "another ProcessKoreography pass.  Alternatively, please verify that an Exception did " +
				                 "not occur during the previous attempt to Process Koreography.");
				
				// Clear the list so that we can proceed cleanly.
				koreographyToProcess.Clear();
			}
			
			// Collect all the Currently Loaded Koreography.  This is the set that we are
			//  going to process, regardless of whether they are loaded/unloaded.
			for (int i = 0; i < loadedKoreography.Count; ++i)
			{
				koreographyToProcess.Add(loadedKoreography[i]);
			}

			// Check if we have a sync delay set.
			if (eventDelayInSeconds > 0f)
			{
				for (int i = koreographyToProcess.Count - 1; i >= 0; --i)
				{
					Koreography koreo = koreographyToProcess[i];
					
					if (koreo.SourceClipName == clipName)
					{
						if (!koreo.IgnoreLatencyOffset)
						{
							AddRecordToDelayQueue(koreo, startTime, endTime, deltaSlice);
						}
						else
						{
							koreo.UpdateTrackTime(startTime, endTime, deltaSlice);
						}
					}
				}
			}
			else	// Process normally.
			{
				for (int i = koreographyToProcess.Count - 1; i >= 0; --i)
				{
					Koreography koreo = koreographyToProcess[i];
					
					if (koreo.SourceClipName == clipName)
					{
						koreo.UpdateTrackTime(startTime, endTime, deltaSlice);
					}
				}
			}
			
			// Clear out the KoreographyToProcess list.  This lets us check that nothing unexpected
			//  is going on (Uncaught Exception interrupted a pass or someone triggers another
			//  process pass while already within one).
			koreographyToProcess.Clear();
		}
		
		void AddRecordToDelayQueue(Koreography koreo, int startTime, int endTime, DeltaSlice deltaSlice)
		{
			int i = 0;
			for ( ; i < delayQueue.Count; ++i)
			{
				if (delayQueue[i].Key == koreo)
				{
					break;
				}
			}
			
			if (i == delayQueue.Count)
			{
				delayQueue.Add(new KeyValuePair<Koreography, List<TimingRecord>>(koreo, new List<TimingRecord>()));
			}
			
			TimingRecord newRecord = new TimingRecord()
			{
				// WARNING: UnscaledDeltaTime appears to break in the Editor when you pause the game (or leave and return).
				//  This may ALSO happen in builds when you leave the game running.
				// TODO: Verify that Time.unscaledDeltaTime doesn't cause any issues.  If it does, we may need to use
				//  Time.realtimeSinceStartup or something like it to calculate our own.  Note that this may also need to
				//  happen in the *player* rather than here...
				//  Basically, we need to figure out if Unity lost context or not (because that will stop the audio as well).
				playTime = deltaSlice.deltaLength,
				timeLeft = deltaSlice.deltaLength + eventDelayInSeconds + (Time.unscaledDeltaTime * deltaSlice.deltaOffset),	// All records get this.  Ensures paused/unpaused streams work.
				startSample = startTime,
				endSample = endTime,
			};
			
			delayQueue[i].Value.Add(newRecord);		// Add on the end; first in!
		}
		
		void ProcessDelayQueue(double timeToProcess)
		{
			// Only allocate this once.
			DeltaSlice deltaSlice = new DeltaSlice();

			// Process the list in reverse order so that we might remove the entry at the end
			//  if necessary.
			for (int i = delayQueue.Count - 1; i >= 0; --i)
			{
				KeyValuePair<Koreography, List<TimingRecord>> mapping = delayQueue[i];
				
				List<TimingRecord> curQueue = mapping.Value;

				// Per-Queue setup!
				int numToDequeue = 0;
				deltaSlice.deltaLength = (float)timeToProcess;
				deltaSlice.deltaOffset = 0f;

				// This variable helps us properly keep track of the DeltaSlice offset!
				double deltaTimeConsumed = 0d;
				
				for (int j = 0; j < curQueue.Count; ++j)
				{
					TimingRecord record = curQueue[j];

					// Subtract off the time.
					record.timeLeft = record.timeLeft - timeToProcess;
					
					// Ensure we're within bounds.
					if (record.timeLeft < 0d)
					{
						record.timeLeft = 0d;
					}
					
					// Process the item if we need to.
					// TODO: "ASSERT" or equivalent that the deltaSlice.deltaOffset < 1f.
					if (record.timeLeft < record.playTime)// && deltaSlice.deltaOffset < 1f)
					{
						// To the endSample that represents the time consumed.
						double timeConsumed = record.playTime - record.timeLeft;
						
						double totalSamples = (double)(record.endSample - record.startSample);
						int endSamp = record.startSample + (int)(totalSamples * (timeConsumed / record.playTime));
						
						// We can't commit an update with a sample range of 0. Make sure
						//  there's at least a gap of two.
						if (endSamp == record.startSample)
						{
							// Add one to space it out.
							endSamp++;
							
							// TODO: Adjust timeLeft by one sample's worth of time.  Skipping this for now
							//  because it probably isn't even necessary: in the common case, one sample
							//  is equivalent to ~22 microseconds...
							//record.timeLeft -= (timeConsumed / totalSamples);
						}
						
						// Not else-if because we adjust endSamp above and need to re-test.
						if (record.endSample - endSamp == 1)
						{
							// Consume all the time.
							endSamp++;
							record.timeLeft = 0f;
						}
						
						// Note how long much time this update represents.
						deltaSlice.deltaLength = (float)timeConsumed;
						
						// Update the events!
						mapping.Key.UpdateTrackTime(record.startSample, endSamp, deltaSlice);

						// Adjust the frame's total consumed time by adding the contribution for this slice. This
						//  is used to determine the deltaOffset of the next slice for the current frame.
						deltaTimeConsumed += timeConsumed;
						
						// Iterate the deltaOffset for the next go-round.
						deltaSlice.deltaOffset += Mathf.Clamp01((float)(deltaTimeConsumed / timeToProcess));
						
						// Prep for the next go-round.
						if (record.timeLeft > 0f)// ||
							//endSamp < record.endSample)			// This shouldn't happen; we're being cautious.
						{
							// Update the entry.
							record.playTime = record.timeLeft;
							record.startSample = endSamp + 1;	// Don't double-test a sample!
						}
						else
						{
							numToDequeue++;
						}
					}

					// The record is a struct!  All changes were local.  Set them back!
					curQueue[j] = record;
				}
				
				// Dequeue the amount we've been told to.
				curQueue.RemoveRange(0, numToDequeue);		// Remove from the front; first out!
				
				// Clear out the entry altogether if we're empty and no longer
				//  officially loaded.
				if (curQueue.Count == 0 && !IsKoreographyLoaded(mapping.Key))
				{
					delayQueue.RemoveAt(i);
				}
			}
		}

		/// <summary>
		/// Flushes timing information entries for a given <c>Koreography</c>.  This will stop any delayed events
		/// and timing updates from reaching the event system and effectively "reset" the Koreography.
		/// </summary>
		/// <returns><c>true</c>, if the delay queue for the <paramref name="targetKoreography"/> was flushed,
		/// <c>false</c> otherwise.</returns>
		/// <param name="targetKoreography">The <c>Koreography</c> to target for clearing out the delay
		/// queue.</param>
		public bool FlushDelayQueue(Koreography targetKoreography)
		{
			bool bDidFlush = false;

			for (int i = 0; i < delayQueue.Count; ++i)
			{
				KeyValuePair<Koreography, List<TimingRecord>> queue = delayQueue[i];

				if (queue.Key == targetKoreography)
				{
					delayQueue.Remove(queue);		// Remove the specific queue for this Koreography.
					bDidFlush = true;				// Set to true because we flushed.
					break;							// Break out as further checks are unnecessary.
				}
			}

			return bDidFlush;
		}

		#endregion
		#region Koreography Management

		/// <summary>
		/// Loads <paramref name="koreo"/> into the Koreographer, making it available
		/// for Koreography Event processing.  Koreography cannot be loaded more than
		/// once.
		/// </summary>
		/// <param name="koreo">The Koreography to load.</param>
		public void LoadKoreography(Koreography koreo)
		{
			if (koreo != null && !loadedKoreography.Contains(koreo))
			{
				for (int i = 0; i < eventBoard.Count; ++i)
				{
					KeyValuePair<string,EventObj> mapping = eventBoard[i];

					if (koreo.DoesTrackWithEventIDExist(mapping.Key))
					{
						// Tie the Koreography to pre-existing event requests.
						koreo.RegisterForEventsWithTime(mapping.Key, mapping.Value.Trigger);
					}
				}

				koreo.ResetTimings();			// Ensure that we properly reset Koreography timings.
				loadedKoreography.Add(koreo);
			}
		}

		/// <summary>
		/// Unloads <paramref name="koreo"/> from the Koreographer, removing it from
		/// Koreography Event processing.
		/// </summary>
		/// <param name="koreo">The Koreography to unload.</param>
		public void UnloadKoreography(Koreography koreo)
		{
			if (koreo != null && loadedKoreography.Contains(koreo))
			{
				for (int i = 0; i < eventBoard.Count; ++i)
				{
					KeyValuePair<string,EventObj> mapping = eventBoard[i];

					if (koreo.DoesTrackWithEventIDExist(mapping.Key))
					{
						// Untie the Koreography from existing event requests.
						koreo.UnregisterForEventsWithTime(mapping.Key, mapping.Value.Trigger);
					}
				}

				loadedKoreography.Remove(koreo);
			}
		}

		#endregion
		#region Koreography Inquiry
		
		/// <summary>
		/// Determines whether <paramref name="koreo"/> is loaded or not.
		/// </summary>
		/// <returns><c>true</c> if <paramref name="koreo"/> is loaded; otherwise,
		/// <c>false</c>.</returns>
		/// <param name="koreo">The Koreography to check.</param>
		public bool IsKoreographyLoaded(Koreography koreo)
		{
			return loadedKoreography.Contains(koreo);
		}

		/// <summary>
		/// Gets the number of loaded Koreography.
		/// </summary>
		/// <returns>The number of loaded Koreography.</returns>
		public int GetNumLoadedKoreography()
		{
			return loadedKoreography.Count;
		}

		/// <summary>
		/// Returns the Koreography currently loaded at the specified
		/// <paramref name="index"/>.
		/// </summary>
		/// <returns>The Koreography at the specified <paramref name="index"/>.</returns>
		/// <param name="index">The index of the Koreography to retrieve.</param>
		public Koreography GetKoreographyAtIndex(int index)
		{
			return (index >= 0 && index < loadedKoreography.Count) ? loadedKoreography[index] : null;
		}

		/// <summary>
		/// Adds all loaded Koreography into the passed in <paramref name="listToFill"/>.
		/// The internal list is not directly returned, although the returned order of
		/// Koreography should match that of the internal List.  It should be noted that
		/// <paramref name="listToFill"/> list is NOT cleared prior to adding Koreography.
		/// </summary>
		/// <param name="listToFill">The List container into which to add the loaded
		/// Koreography.</param>
		public void GetAllLoadedKoreography(List<Koreography> listToFill)
		{
			listToFill.AddRange(loadedKoreography);
		}

		#endregion
		#region Event Callback Registration

		KeyValuePair<string,EventObj> FindMappingWithEventID(string eventID)
		{
			KeyValuePair<string,EventObj> mapping = new KeyValuePair<string, EventObj>();
			
			for (int i = 0; i < eventBoard.Count; ++i)
			{
				if (eventBoard[i].Key == eventID)
				{
					mapping = eventBoard[i];
					break;
				}
			}

			return mapping;
		}

		/// <summary>
		/// Registers <paramref name="callback"/> with the internal event management
		/// system, activating it for Koreography Events that occur from Koreography
		/// Tracks identified by <paramref name="eventID"/>.
		/// </summary>
		/// <param name="eventID">The Event ID of Koreography Tracks for which this
		/// callback should be triggered.</param>
		/// <param name="callback">The callback to register.</param>
		public void RegisterForEvents(string eventID, KoreographyEventCallback callback)
		{
			if (string.IsNullOrEmpty(eventID))
			{
				Debug.LogError("Cannot register for events with an empty event identifier!");
			}
			else
			{
				KeyValuePair<string,EventObj> mapping = FindMappingWithEventID(eventID);

				// KeyValuePair generics treat the key as a property, which can return null.
				if (string.IsNullOrEmpty(mapping.Key))
				{
					mapping = new KeyValuePair<string, EventObj>(eventID, new EventObj());
					eventBoard.Add(mapping);
				
					// New Mapping (we haven't encountered this event ID before).  Register with previously
					//  loaded Koreography!
					// Adds the Koreographer->Koreography link.
					ConnectEventToLoadedKoreography(mapping);
				}
			
				// Add the Obj->Koreographer link.
				mapping.Value.Register(callback);
			}
		}

		/// <summary>
		/// Registers <paramref name="callback"/> with the internal event management
		/// system, activating it for Koreography Events that occur from Koreography
		/// Tracks identified by <paramref name="eventID"/>.
		/// </summary>
		/// <param name="eventID">The Event ID of Koreography Tracks for which this
		/// callback should be triggered.</param>
		/// <param name="callback">The callback to register.</param>
		public void RegisterForEventsWithTime(string eventID, KoreographyEventCallbackWithTime callback)
		{
			if (string.IsNullOrEmpty(eventID))
			{
				Debug.LogError("Cannot register for events with an empty event identifier!");
			}
			else
			{
				KeyValuePair<string,EventObj> mapping = FindMappingWithEventID(eventID);

				// KeyValuePair generics treat the key as a property, which can return null.
				if (string.IsNullOrEmpty(mapping.Key))
				{
					mapping = new KeyValuePair<string, EventObj>(eventID, new EventObj());
					eventBoard.Add(mapping);
				
					// New Mapping (we haven't encountered this event ID before).  Register with previously
					//  loaded Koreography!
					// Adds the Koreographer->Koreography link.
					ConnectEventToLoadedKoreography(mapping);
				}
			
				// Add the Obj->Koreographer link.
				mapping.Value.Register(callback);
			}
		}

		/// <summary>
		/// Unregisters <paramref name="callback"/> with the internal event management
		/// system, removing it from consideration for Koreography Event triggering for
		/// Koreography Tracks identified by <paramref name="eventID"/>.
		/// </summary>
		/// <param name="eventID">The Event ID of Koreography Tracks for which this
		/// callback should be unregistered.</param>
		/// <param name="callback">The callback to unregister.</param>
		public void UnregisterForEvents(string eventID, KoreographyEventCallback callback)
		{
			if (string.IsNullOrEmpty(eventID))
			{
				Debug.LogError("Cannot unregister for events with an empty event identifier!");
			}
			else
			{
				KeyValuePair<string,EventObj> mapping = FindMappingWithEventID(eventID);

				// KeyValuePair generics treat the key as a property, which can return null.
				if (!string.IsNullOrEmpty(mapping.Key))
				{
					// Remove the Obj->Koreographer link.
					mapping.Value.Unregister(callback);
				
					if (mapping.Value.IsClear())
					{
						// If there isn't a reason for this to exist anymore, clean it up!
					
						// Remove the Koreographer->Koreography link.
						DisconnectEventFromLoadedKoreography(mapping);
					
						eventBoard.Remove(mapping);
					}
				}
			}
		}

		/// <summary>
		/// Unregisters <paramref name="callback"/> with the internal event management
		/// system, removing it from consideration for Koreography Event triggering for
		/// Koreography Tracks identified by <paramref name="eventID"/>.
		/// </summary>
		/// <param name="eventID">The Event ID of Koreography Tracks for which this
		/// callback should be unregistered.</param>
		/// <param name="callback">The callback to unregister.</param>
		public void UnregisterForEvents(string eventID, KoreographyEventCallbackWithTime callback)
		{
			if (string.IsNullOrEmpty(eventID))
			{
				Debug.LogError("Cannot unregister for events with an empty event identifier!");
			}
			else
			{
				KeyValuePair<string,EventObj> mapping = FindMappingWithEventID(eventID);

				// KeyValuePair generics treat the key as a property, which can return null.
				if (!string.IsNullOrEmpty(mapping.Key))
				{
					// Remove the Obj->Koreographer link.
					mapping.Value.Unregister(callback);
				
					if (mapping.Value.IsClear())
					{
						// If there isn't a reason for this to exist anymore, clean it up!
					
						// Remove the Koreographer->Koreography link.
						DisconnectEventFromLoadedKoreography(mapping);

						eventBoard.Remove(mapping);
					}
				}
			}
		}

		/// <summary>
		/// Unregisters any callbacks of <paramref name="obj"/> from consideration
		/// for any and all Koreography Events, across all Event IDs.
		/// </summary>
		/// <param name="obj">The object whose callbacks will be unregistered.</param>
		public void UnregisterForAllEvents(System.Object obj)
		{
			// Go backwards as this loop may shrink the eventBoard list.
			for (int i = eventBoard.Count - 1; i >= 0; --i)
			{
				KeyValuePair<string,EventObj> mapping = eventBoard[i];

				mapping.Value.UnregisterByObject(obj);

				if (mapping.Value.IsClear())
				{
					// If there isn't a reason for this to exist anymore, clean it up!

					// Remove the Koreographer->Koreography link.
					DisconnectEventFromLoadedKoreography(mapping);

					eventBoard.Remove(mapping);
				}
			}
		}

		/// <summary>
		/// Clears the Event Register, a system that maintains mappings between callbacks,
		/// Event IDs, and Koreography Tracks.  This effectively unregisters ALL callbacks.
		/// </summary>
		public void ClearEventRegister()
		{
			for (int i = 0; i < eventBoard.Count; ++i)
			{
				KeyValuePair<string,EventObj> mapping = eventBoard[i];

				// Remove Obj->Koreographer links.
				mapping.Value.ClearRegistrations();

				// Remove the Koreographer->Koreography link.
				DisconnectEventFromLoadedKoreography(mapping);
			}
		
			// Releases all mappings.
			eventBoard.Clear();
		}

		void ConnectEventToLoadedKoreography(KeyValuePair<string,EventObj> mapping)
		{
			// Adds the Koreographer->Koreography link.
			for (int i = 0; i < loadedKoreography.Count; ++i)
			{
				Koreography koreo = loadedKoreography[i];

				if (koreo.DoesTrackWithEventIDExist(mapping.Key))
				{
					koreo.RegisterForEventsWithTime(mapping.Key, mapping.Value.Trigger);
				}
			}
		}

		void DisconnectEventFromLoadedKoreography(KeyValuePair<string,EventObj> mapping)
		{
			// Remove Koreographer->Koreography links.
			for (int i = 0; i < loadedKoreography.Count; ++i)
			{
				Koreography koreo = loadedKoreography[i];

				if (koreo.DoesTrackWithEventIDExist(mapping.Key))
				{
					koreo.UnregisterForEventsWithTime(mapping.Key, mapping.Value.Trigger);
				}
			}
		}
	
		#endregion
		#region Event Query
		
		/// <summary>
		/// <para>Searches loaded Koreography for instances configured with the audio clip specified by
		/// <paramref name="clipName"/>. It then filters by <paramref name="eventID"/>, returning
		/// all events encountered in the sample range defined by <paramref name="startPos"/> and
		/// <paramref name="endPos"/>.</para>
		/// 
		/// <para>This method is now implemented as a wrapper.</para>
		/// </summary>
		/// <returns>All of the events in the range found within any Koreography loaded
		/// that references <paramref name="clipName"/>.</returns>
		/// <param name="clipName">The name of the audio clip to look up timing info on.</param>
		/// <param name="eventID">The Event ID for events to look up.</param>
		/// <param name="startPos">Start of the search range.</param>
		/// <param name="endPos">End of the search range.</param>
		[System.Obsolete("This method will be removed in a future version.  Please use GetAllEventsInRange(string, string, int, int, List) instead.")]
		public List<KoreographyEvent> GetAllEventsInRange(string clipName, string eventID, int startPos, int endPos)
		{
			List<KoreographyEvent> evts = new List<KoreographyEvent>();

			GetAllEventsInRange(clipName, eventID, startPos, endPos, evts);

			return evts;
		}

		/// <summary>
		/// Searches loaded Koreography for instances configured with the audio clip specified by
		/// <paramref name="clipName"/>. It then filters by <paramref name="eventID"/> and adds
		/// all events encountered in the sample range defined by <paramref name="startPos"/> and
		/// <paramref name="endPos"/> to <paramref name="listToFill"/>.  It should be noted that
		/// <paramref name="listToFill"/> list is NOT cleared prior to adding Koreography Events.
		/// </summary>
		/// <param name="clipName">The name of the audio clip to look up timing info on.</param>
		/// <param name="eventID">The Event ID for events to look up.</param>
		/// <param name="startPos">Start of the search range.</param>
		/// <param name="endPos">End of the search range.</param>
		/// <param name="listToFill">The List container into which to add the located Koreography
		/// Events.</param>
		public void GetAllEventsInRange(string clipName, string eventID, int startPos, int endPos, List<KoreographyEvent> listToFill)
		{
			if (trackProcessingHelper == null)
			{
				trackProcessingHelper = new List<KoreographyTrackBase>();
			}
			/*else
			{
				// Currently assume that this is the only function using the list.  As it cleans
				//  up after itself at the end, everything should be fine.  The only way to
				//  cause an issue with this [currently] is if an exception is triggered prior
				//  to calling Clear.  Revisit if this causes problems for users.
				trackProcessingHelper.Clear();
			}*/
			
			for (int i = 0; i < loadedKoreography.Count; ++i)
			{
				Koreography koreo = loadedKoreography[i];
				
				if (koreo.SourceClipName == clipName)
				{
					KoreographyTrackBase track = koreo.GetTrackByID(eventID);
					if (track != null && !trackProcessingHelper.Contains(track))
					{
						track.GetEventsInRange(startPos, endPos, listToFill);
						
						// Make sure we don't end up searching the same Koreography Track again
						//  (asingle track can be loaded into multiple Koreography).
						trackProcessingHelper.Add(track);
					}
				}
			}

			// Clean up after ourselves.
			trackProcessingHelper.Clear();
		}

		#endregion
		#region Music Time API
		
		/// <summary>
		/// <para>Returns the sample rate of the music specified by <paramref name="clipName"/>.</para>
		/// 
		/// <para>[Note: This method is part of the Music Time API.]</para>
		/// </summary>
		/// <returns>The sample rate of the audio specified by <paramref name="clipName"/>
		/// or, if not specified, that of the currently playing audio.</returns>
		/// <param name="clipName">The name of the audio of which to get the sample rate.  If not
		/// specified, it will return a value for the currently playing music, if available.</param>
		public int GetMusicSampleRate(string clipName = null)
		{
			int sampleRate = 44100;	// Standard default.
			
			Koreography koreo = GetMusicKoreographyWithAudioName(clipName);
			
			if (koreo != null)
			{
				sampleRate = koreo.SampleRate;
			}
			
			return sampleRate;
		}
		
		/// <summary>
		/// <para>Gets the total time in samples of the music specified by
		/// <paramref name="clipName"/>.</para>
		/// 
		/// <para>[Note: This method is part of the Music Time API.]</para>
		/// </summary>
		/// <returns>The total time in samples of the audio specified by
		/// <paramref name="clipName"/> or, if not specified, that of the currently playing
		/// audio.</returns>
		/// <param name="clipName">The name of the audio from which to get the total sample time.
		/// If not specified, it will return a value for the currently playing music, if
		/// available.</param>
		public int GetMusicSampleLength(string clipName = null)
		{
			int totalSampleTime = 0;
			
			if (musicPlaybackController != null)
			{
				if (string.IsNullOrEmpty(clipName))
				{
					clipName = musicPlaybackController.GetCurrentClipName();
				}
				
				if (!string.IsNullOrEmpty(clipName))
				{
					totalSampleTime = musicPlaybackController.GetTotalSampleTimeForClip(clipName);
				}
			}
			
			return totalSampleTime;
		}
		
		/// <summary>
		/// <para>Gets the playback time in samples of the music specified by
		/// <paramref name="clipName"/>.
		/// </para>
		/// 
		/// <para>[Note: This method is part of the Music Time API.]</para>
		/// </summary>
		/// <returns>The playback time in samples of the audio specified by <paramref name="clipName"/>
		/// or, if not specified, that of the currently playing audio.</returns>
		/// <param name="clipName">The name of the audio of which to check the playback time.  If not
		/// specified, it will return a value for the currently playing music, if available.</param>
		public int GetMusicSampleTime(string clipName = null)
		{
			int sampleTime = -1;
			
			if (musicPlaybackController != null)
			{
				if (string.IsNullOrEmpty(clipName))
				{
					clipName = musicPlaybackController.GetCurrentClipName();
				}
				
				if (!string.IsNullOrEmpty(clipName))
				{
					sampleTime = musicPlaybackController.GetSampleTimeForClip(clipName);
				}
			}
			
			return sampleTime;
		}

		/// <summary>
		/// <para>Gets the delta time of the music in samples of the most recent Koreography processing
		/// pass.</para>
		/// 
		/// <para>[Note: This method is part of the Music Time API.]</para>
		/// </summary>
		/// <returns>The delta time in samples of the current Koreography processing pass.</returns>
		/// <param name="clipName">The name of the audio from which to get the delta time in samples.
		/// If not specified, it will return a value for the currently playing music, if
		/// available.</param>
		public int GetMusicSampleTimeDelta(string clipName = null)
		{
			int deltaTime = 0;

			Koreography koreo = GetMusicKoreographyWithAudioName(clipName);
			
			if (koreo != null)
			{
				deltaTime = koreo.GetLatestSampleTimeDelta();
			}

			return deltaTime;
		}

		/// <summary>
		/// <para>Gets the total time in seconds of the music specified by
		/// <paramref name="clipName"/>.</para>
		/// 
		/// <para>[Note: This method is part of the Music Time API.]</para>
		/// </summary>
		/// <returns>The total time in seconds of the audio specified by
		/// <paramref name="clipName"/> or, if not specified, that of the currently playing
		/// audio.</returns>
		/// <param name="clipName">The name of the audio from which to get the total time in seconds.
		/// If not specified, it will return a value for the currently playing music, if
		/// available.</param>
		public double GetMusicSecondsLength(string clipName = null)
		{
			double totalSecondsTime = 0d;

			Koreography koreo = GetMusicKoreographyWithAudioName(clipName);

			if (koreo != null)
			{
				totalSecondsTime = (double)GetMusicSampleLength(clipName) / (double)koreo.SampleRate;
			}

			return totalSecondsTime;
		}

		/// <summary>
		/// <para>Gets the playback time in seconds of the music specified by
		/// <paramref name="clipName"/>.
		/// </para>
		/// 
		/// <para>[Note: This method is part of the Music Time API.]</para>
		/// </summary>
		/// <returns>The playback time in seconds of the audio specified by <paramref name="clipName"/>
		/// or, if not specified, that of the currently playing audio.</returns>
		/// <param name="clipName">The name of the audio of which to check the playback time.  If not
		/// specified, it will return a value for the currently playing music, if available.</param>
		public double GetMusicSecondsTime(string clipName = null)
		{
			double secondsTime = -1d;
			
			Koreography koreo = GetMusicKoreographyWithAudioName(clipName);

			if (koreo != null)	// If this exists, so does the musicPlaybackController.
			{
				// Make sure the name is valid.
				if (string.IsNullOrEmpty(clipName))
				{
					clipName = musicPlaybackController.GetCurrentClipName();
				}
				
				secondsTime = (double)musicPlaybackController.GetSampleTimeForClip(clipName) / (double)koreo.SampleRate;
			}
			
			return secondsTime;
		}

		/// <summary>
		/// <para>Gets the delta time of the music in seconds of the most recent Koreography processing
		/// pass.</para>
		/// 
		/// <para>[Note: This method is part of the Music Time API.]</para>
		/// </summary>
		/// <returns>The delta time in seconds of the current Koreography processing pass.</returns>
		/// <param name="clipName">The name of the audio from which to get the delta time in seconds.
		/// If not specified, it will return a value for the currently playing music, if
		/// available.</param>
		public double GetMusicSecondsTimeDelta(string clipName = null)
		{
			double deltaTime = 0d;
			
			Koreography koreo = GetMusicKoreographyWithAudioName(clipName);
			
			if (koreo != null)
			{
				deltaTime = (double)koreo.GetLatestSampleTimeDelta() / (double)koreo.SampleRate;
			}
			
			return deltaTime;
		}
		
		/// <summary>
		/// <para>Gets the current Beats Per Minute (BPM) of the music.</para>
		/// 
		/// <para>[Note: This method is part of the Music Time API.]</para>
		/// </summary>
		/// <returns>The current Beats Per Minute of the audio with the name
		/// <paramref name="clipName"/>.</returns>
		/// <param name="clipName">The name of the audio from which to get the current BPM.
		/// If not specified, it will return a value for the currently playing music, if
		/// available.</param>
		public double GetMusicBPM(string clipName = null)
		{
			double bpm = 0d;
			
			Koreography koreo = GetMusicKoreographyWithAudioName(clipName);
			
			if (koreo != null)	// If this exists, so does the musicPlaybackController.
			{
				// Make sure the name is valid.
				if (string.IsNullOrEmpty(clipName))
				{
					clipName = musicPlaybackController.GetCurrentClipName();
				}
				
				int sampleTime = musicPlaybackController.GetSampleTimeForClip(clipName);
				bpm = koreo.GetBPM(sampleTime);
			}
			
			return bpm;
		}

		/// <summary>
		/// <para>Gets the total time in beats of the music specified by
		/// <paramref name="clipName"/>.</para>
		/// 
		/// <para>[Note: This method is part of the Music Time API.]</para>
		/// </summary>
		/// <returns>The total time in beats of the audio specified by
		/// <paramref name="clipName"/> or, if not specified, that of the currently playing
		/// audio.</returns>
		/// <param name="clipName">The name of the audio from which to get the total beat time.
		/// If not specified, it will return a value for the currently playing music, if
		/// available.</param>
		/// <param name="subdivisions">The number of subdivisions of a standard beat that
		/// determine the beat.</param>
		public double GetMusicBeatLength(string clipName = null, int subdivisions = 1)
		{
			double totalBeatTime = 0d;

			Koreography koreo = GetMusicKoreographyWithAudioName(clipName);

			if (koreo != null)
			{
				int totalSampleTime = GetTotalSampleTime(clipName);
				totalBeatTime = koreo.GetBeatTimeFromSampleTime(totalSampleTime, subdivisions);
			}

			return totalBeatTime;
		}

		/// <summary>
		/// <para>Gets the current time of the music in beats with the beat value specified by
		/// <paramref name="subdivisions"/>.</para>
		/// 
		/// <para>[Note: This method is part of the Music Time API.]</para>
		/// </summary>
		/// <returns>The current time in beats of audio with name <paramref name="clipName"/>.</returns>
		/// <param name="clipName">The name of the audio from which to get the current beat
		/// time.  If not specified, it will return a value for the currently playing
		/// music, if available.</param>
		/// <param name="subdivisions">The number of subdivisions of a standard beat that
		/// determine the beat.</param>
		public double GetMusicBeatTime(string clipName = null, int subdivisions = 1)
		{
			double beatTime = -1d;
			
			Koreography koreo = GetMusicKoreographyWithAudioName(clipName);
			
			if (koreo != null)	// If this exists, so does the musicPlaybackController.
			{
				// Make sure the name is valid.
				if (string.IsNullOrEmpty(clipName))
				{
					clipName = musicPlaybackController.GetCurrentClipName();
				}

				int sampleTime = musicPlaybackController.GetSampleTimeForClip(clipName);
				beatTime = koreo.GetBeatTimeFromSampleTime(sampleTime, subdivisions);
			}
			
			return beatTime;
		}

		/// <summary>
		/// <para>Gets the delta time of the music in beats of the most recent Koreography processing
		/// pass, with the beat value specified by <paramref name="subdivisions"/>.</para>
		/// 
		/// <para>[Note: This method is part of the Music Time API.]</para>
		/// </summary>
		/// <returns>The delta time in beats of the current Koreography processing pass.</returns>
		/// <param name="clipName">The name of the audio from which to get the beat time delta.  If
		/// not specified, it will return a value for the currently playing music, if available.</param>
		/// <param name="subdivisions">The number of subdivisions of a standard beat that
		/// determine the beat.</param>
		public double GetMusicBeatTimeDelta(string clipName = null, int subdivisions = 1)
		{
			double deltaTime = 0d;

			Koreography koreo = GetMusicKoreographyWithAudioName(clipName);
			
			if (koreo != null)
			{
				deltaTime = koreo.GetBeatTimeDelta(subdivisions);
			}

			return deltaTime;
		}

		/// <summary>
		/// <para>Gets the percentage of the way between beats of the current music beat time in
		/// the range [0,1), with the beat value specified by <paramref name="subdivisions"/>.</para>
		/// 
		/// <para>[Note: This method is part of the Music Time API.]</para>
		/// </summary>
		/// <returns>The percentage of the way between beats the current beat time is in the range
		/// [0,1).</returns>
		/// <param name="clipName">The name of the audio from which to get the time information.  If
		/// not specified, it will return a value for the currently playing music, if
		/// available.</param>
		/// <param name="subdivisions">The number of subdivisions of a standard beat that determine
		/// the beat.</param>
		public double GetMusicBeatTimeNormalized(string clipName = null, int subdivisions = 1)
		{
			return GetMusicBeatTime(clipName, subdivisions) % 1d;
		}
	
		#endregion
		#region Utility Methods

		protected Koreography GetMusicKoreographyWithAudioName(string clipName)
		{
			Koreography retKoreo = null;

			if (musicPlaybackController != null)
			{
				// Check if we need to look this up ourselves.
				if (string.IsNullOrEmpty(clipName))
				{
					clipName = musicPlaybackController.GetCurrentClipName();
				}
				
				// Do we have a valid clip name now?
				if (!string.IsNullOrEmpty(clipName))
				{
					retKoreo = GetLoadedKoreographyWithAudioName(clipName);
				}
			}

			return retKoreo;
		}

		protected Koreography GetLoadedKoreographyWithAudioName(string clipName)
		{
			Koreography retKoreo = null;

			for (int i = 0; i < loadedKoreography.Count; ++i)
			{
				Koreography koreo = loadedKoreography[i];
				
				if (koreo.SourceClipName == clipName)
				{
					retKoreo = koreo;
					break;
				}
			}

			return retKoreo;
		}

		#endregion
		#region Static Music Time Accessors

		/// <summary>
		/// <para>Returns the sample rate of the music specified by <paramref name="clipName"/>.
		/// This method is merely a wrapper that uses the singleton (global)
		/// <c>Koreographer.Instance</c>.</para>
		/// 
		/// <para>[Note: This method is part of the Music Time API.]</para>
		/// </summary>
		/// <returns>The sample rate of the audio specified by <paramref name="clipName"/>
		/// or, if not specified, that of the currently playing audio.</returns>
		/// <param name="clipName">The name of the audio of which to get the sample rate.  If not
		/// specified, it will return a value for the currently playing music, if available.</param>
		public static int GetSampleRate(string clipName = null)
		{
			Koreographer grapher = Koreographer.Instance;

			int sampleRate = 44100;	// Standard default.

			if (grapher != null)
			{
				sampleRate = grapher.GetMusicSampleRate(clipName);
			}

			return sampleRate;
		}

		/// <summary>
		/// <para>Gets the total time in samples of the music specified by
		/// <paramref name="clipName"/>.  This method is merely a wrapper that uses the singleton
		/// (global) <c>Koreographer.Instance</c>.</para>
		/// 
		/// <para>[Note: This method is part of the Music Time API.]</para>
		/// </summary>
		/// <returns>The total time in samples of the audio specified by
		/// <paramref name="clipName"/> or, if not specified, that of the currently playing
		/// audio.</returns>
		/// <param name="clipName">The name of the audio from which to get the total sample time.
		/// If not specified, it will return a value for the currently playing music, if
		/// available.</param>
		public static int GetTotalSampleTime(string clipName = null)
		{
			Koreographer grapher = Koreographer.Instance;

			int totalSampleTime = 0;

			if (grapher != null)
			{
				totalSampleTime = grapher.GetMusicSampleLength(clipName);
			}

			return totalSampleTime;
		}

		/// <summary>
		/// <para>Gets the playback time in samples of the music specified by
		/// <paramref name="clipName"/>.  This method is merely a wrapper that uses the singleton (global)
		/// <c>Koreographer.Instance</c>.
		/// </para>
		/// 
		/// <para>[Note: This method is part of the Music Time API.]</para>
		/// </summary>
		/// <returns>The playback time in samples of the audio specified by <paramref name="clipName"/>
		/// or, if not specified, that of the currently playing audio.</returns>
		/// <param name="clipName">The name of the audio of which to check the playback time.  If not
		/// specified, it will return a value for the currently playing music, if available.</param>
		public static int GetSampleTime(string clipName = null)
		{
			Koreographer grapher = Koreographer.Instance;

			int sampleTime = -1;

			if (grapher != null)
			{
				sampleTime = grapher.GetMusicSampleTime(clipName);
			}

			return sampleTime;
		}

		/// <summary>
		/// <para>Gets the current time of the music in beats with the beat value specified by
		/// <paramref name="subdivisions"/>.  This method is merely a wrapper that uses the singleton
		/// (global) <c>Koreographer.Instance</c>.  This version is less precise than using
		/// <see cref="GetMusicBeatTime"/> directly as it returns a <c>float</c> rather than the more
		/// precise <c>double</c>.</para>
		/// 
		/// <para>[Note: This method is part of the Music Time API.]</para>
		/// </summary>
		/// <returns>The current time in beats of audio with name <paramref name="clipName"/>.</returns>
		/// <param name="clipName">The name of the audio from which to get the current beat
		/// time.  If not specified, it will return a value for the currently playing
		/// music, if available.</param>
		/// <param name="subdivisions">The number of subdivisions of a standard beat that
		/// determine the beat.</param>
		public static float GetBeatTime(string clipName = null, int subdivisions = 1)
		{
			Koreographer grapher = Koreographer.Instance;

			float beatTime = -1f;
			
			if (grapher != null)
			{
				beatTime = (float)grapher.GetMusicBeatTime(clipName, subdivisions);
			}
			
			return beatTime;
		}

		/// <summary>
		/// <para>Gets the delta time of the music in beats of the most recent Koreography processing
		/// pass, with the beat value specified by <paramref name="subdivisions"/>.  This method is
		/// merely a wrapper that uses the singleton (global) <c>Koreographer.Instance</c>.  This
		/// version is less precise than using <see cref="GetMusicBeatTimeDelta"/> directly as it
		/// returns a <c>float</c> rather than the more precise <c>double</c>.</para>
		/// 
		/// <para>[Note: This method is part of the Music Time API.]</para>
		/// </summary>
		/// <returns>The delta time in beats of the current Koreography processing pass.</returns>
		/// <param name="clipName">The name of the audio from which to get the beat time delta.  If
		/// not specified, it will return a value for the currently playing music, if available.</param>
		/// <param name="subdivisions">The number of subdivisions of a standard beat that
		/// determine the beat.</param>
		public static float GetBeatTimeDelta(string clipName = null, int subdivisions = 1)
		{
			Koreographer grapher = Koreographer.Instance;

			float deltaTime = 0f;

			if (grapher != null)
			{
				deltaTime = (float)grapher.GetMusicBeatTimeDelta(clipName, subdivisions);
			}

			return deltaTime;
		}

		/// <summary>
		/// <para>Gets the percentage of the way between beats of the current music beat time in
		/// the range [0,1), with the beat value specified by <paramref name="subdivisions"/>.
		/// This method is merely a wrapper that uses the singleton (global)
		/// <c>Koreographer.Instance</c>.  This version is less precise than using
		/// <see cref="GetMusicBeatTimeNormalized"/> directly as it returns a <c>float</c> rather
		/// than the more precise <c>double</c>.</para>
		/// 
		/// <para>[Note: This method is part of the Music Time API.]</para>
		/// </summary>
		/// <returns>The percentage of the way between beats the current beat time is in the range
		/// [0,1).</returns>
		/// <param name="clipName">The name of the audio from which to get the time information.  If
		/// not specified, it will return a value for the currently playing music, if
		/// available.</param>
		/// <param name="subdivisions">The number of subdivisions of a standard beat that determine
		/// the beat.</param>
		public static float GetBeatTimeNormalized(string clipName = null, int subdivisions = 1)
		{
			Koreographer grapher = Koreographer.Instance;

			float beatTime = 0f;

			if (grapher != null)
			{
				beatTime = (float)grapher.GetMusicBeatTimeNormalized(clipName, subdivisions);
			}

			return beatTime;
		}

		#endregion
	}

	/// <summary>
	/// The glue object that maintains registration information between
	///  callbacks and registrants.
	/// </summary>
	class EventObj
	{
		event KoreographyEventCallback basicEvent;
		event KoreographyEventCallbackWithTime timedEvent;
		
		public void Register(KoreographyEventCallback callback)
		{
			basicEvent += callback;
		}
		
		public void Register(KoreographyEventCallbackWithTime callback)
		{
			timedEvent += callback;
		}
		
		public void Unregister(KoreographyEventCallback callback)
		{
			basicEvent -= callback;
		}
		
		public void Unregister(KoreographyEventCallbackWithTime callback)
		{
			timedEvent -= callback;
		}
		
		public void UnregisterByObject(System.Object obj)
		{
			System.Delegate[] delegates;
			
			if (basicEvent != null)
			{
				delegates = basicEvent.GetInvocationList();
				
				for (int i = 0; i < delegates.Length; ++i)
				{
					if (delegates[i].Target == obj)
					{
						basicEvent -= (KoreographyEventCallback)delegates[i];
						break;
					}
				}
			}
			
			if (timedEvent != null)
			{
				delegates = timedEvent.GetInvocationList();
				
				for (int i = 0; i < delegates.Length; ++i)
				{
					if (delegates[i].Target == obj)
					{
						timedEvent -= (KoreographyEventCallbackWithTime)delegates[i];
						break;
					}
				}
			}
		}
		
		public void ClearRegistrations()
		{
			basicEvent = null;
			timedEvent = null;
		}
		
		public bool IsClear()
		{
			return (basicEvent == null) && (timedEvent == null);
		}
		
		public void Trigger(KoreographyEvent evt, int sampleTime, int sampleDelta, DeltaSlice deltaSlice)
		{
			if (basicEvent != null)
			{
				basicEvent(evt);
			}
			
			if (timedEvent != null)
			{
				timedEvent(evt, sampleTime, sampleDelta, deltaSlice);
			}
		}
	}
}
