//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;
using System.Collections.Generic;

namespace SonicBloom.MIDI.Objects
{
	/// <summary>
	/// A representation of a Note.
	/// </summary>
	public class Note
	{
		/// <summary>
		/// The time in ticks.
		/// </summary>
		public int startTime				= -1;
		public int key						= 0x3C;		// Middle C, C3.
		public int velocity					= 0;		// Also a dictionary?
		public int endTime					= -1;
		public Dictionary<int, int> pitch	= new Dictionary<int, int>();

		/// <summary>
		/// Retrieves a human-readable <c>string</c> representation
		/// of <paramref name="key"/>.
		/// </summary>
		/// <returns>A human-readable <c>string</c> representation of
		/// <paramref name="key"/>.</returns>
		/// <param name="key">The <c>int</c> value of the key to convert
		/// to <paramref name="string"/>.</param>
		public static string GetMusicNote(int key)
		{
			string val = "C";
			switch (key % 12)
			{
			case 0:
				val = "C";
				break;
			case 1:
				val = "C#/Db";
				break;
			case 2:
				val = "D";
				break;
			case 3:
				val = "D#/Eb";
				break;
			case 4:
				val = "E";
				break;
			case 5:
				val = "F";
				break;
			case 6:
				val = "F#/Gb";
				break;
			case 7:
				val = "G";
				break;
			case 8:
				val = "G#/Ab";
				break;
			case 9:
				val = "A";
				break;
			case 10:
				val = "A#/Bb";
				break;
			case 11:
				val = "B";
				break;
			}
			return val;
		}
	}

	/// <summary>
	/// Representation of a Channel of notes.
	/// </summary>
	public class Channel
	{
		#region Fields

		List<Note>		notes			= new List<Note>();
		/// <summary>
		/// The name of the instrument.
		/// </summary>
		public string	instrumentName	= "";
//		public int program				= "";		// Virtual instrument, patch, or preset.  See: http://www.recordingblogs.com/sa/tabid/88/Default.aspx?topic=MIDI+Program+Change+message

		#endregion
		#region Properties

		/// <summary>
		/// Gets the notes.
		/// </summary>
		/// <value>The notes.</value>
		public List<Note> Notes
		{
			get
			{
				return notes;
			}
		}

		#endregion
		#region Methods

		/// <summary>
		/// Begins a note in the channel at <paramref name="time"/> with
		/// <paramref name="key"/> and <paramref name="velocity"/>.
		/// </summary>
		/// <param name="time">The time in ticks the note begins at.</param>
		/// <param name="key">The key of the note.</param>
		/// <param name="velocity">The velocity of the note.</param>
		public void BeginNote(int time, int key, int velocity)
		{
			Note note = notes.Find(x => (x.startTime <= time &&
			                             x.endTime > time &&
			                             x.key == key));
			
			if (note == null)
			{
				note = new Note();
				note.startTime = time;
				note.key = key;
				note.velocity = velocity;
				
				notes.Add(note);
			}
			else
			{
				Debug.LogWarning("Note with key '" + Note.GetMusicNote(key) + "' already exists at time " + time);
			}
		}

		/// <summary>
		/// Ends a note in the channel at <paramref name="time"/> with
		/// <paramref name="key"/> and <paramref name="velocity"/>.
		/// </summary>
		/// <param name="time">The time in ticks the note ends at.</param>
		/// <param name="key">The key of the note.</param>
		/// <param name="velocity">The velocity of the note.</param>
		public void EndNote(int time, int key, int velocity)
		{
			Note note = notes.Find(x => (x.key == key &&
			                             x.endTime < 0));
			
			if (note != null)
			{
				note.endTime = time;
			}
			else
			{
				Debug.LogWarning("No unended note with key: " + Note.GetMusicNote(key) + " (" + key + ")");
			}
		}

		/// <summary>
		/// Determines whether the <c>Channel</c> contains an unended note with
		/// key <paramref name="key"/>.
		/// </summary>
		/// <returns><c>true</c> if this <c>Channel</c> has an unended note with
		/// the specified key; otherwise, <c>false</c>.</returns>
		/// <param name="key">Key.</param>
		public bool HasUnendedNoteWithKey(int key)
		{
			// An unended note has an endTime less than 0.
			return notes.Find(x => (x.key == key && x.endTime < 0)) != null;
		}

		/// <summary>
		/// Gets the number of notes in the <c>Channel</c>.
		/// </summary>
		/// <returns>The number of notes in the <c>Channel</c>.</returns>
		public int NumNotes()
		{
			return notes.Count;
		}

		/// <summary>
		/// Gets the number of unnended notes in the <c>Channel</c>.
		/// </summary>
		/// <returns>The number of unnended notes in the <c>Channel</c>.</returns>
		public int NumUnendedNotes()
		{
			int amount = 0;

			for (int i = 0; i < notes.Count; ++i)
			{
				Note note = notes[i];

				if (note.endTime < 0)
				{
//					Debug.Log("Unended Note: " + Note.GetMusicNote(note.key) + " (" + note.key + ")");
					amount++;
				}
			}
			
			return amount;
		}

		#endregion
	}

	/// <summary>
	/// Representation of a Track object.
	/// </summary>
	public class Track
	{
		#region Fields

		public string name= "";
		Dictionary<int,Channel> channels	= new Dictionary<int, Channel>();
		List<MIDITimedMessage<string>> lyrics	= new List<MIDITimedMessage<string>>();
		//	List<Marker> markers				= new List<Markers>();		// TODO: Marker support.
		//	List<CuePoint> cuePoints			= new List<CuePoint>();		// TODO: Cue Point support.
		//	int bank							= 0;						// TODO: Bank support.
		public string instrumentName		= string.Empty;
		public string deviceName			= string.Empty;

		#endregion
		#region Properties

		public Dictionary<int, Channel> Channels
		{
			get
			{
				return channels;
			}
		}

		#endregion
		#region Methods

		/// <summary>
		/// Determines whether this <c>Track</c> contains any Lyrics.
		/// </summary>
		/// <returns><c>true</c> if this instance has lyrics; otherwise, <c>false</c>.</returns>
		public bool HasLyrics()
		{
			return lyrics.Count > 0;
		}

		/// <summary>
		/// Adds <paramref name="lyric"/> to the list of Lyrics at <paramref name="time"/>.
		/// </summary>
		/// <param name="time">The time at which to add the <paramref name="lyric"/>.</param>
		/// <param name="lyric">The <c>string</c> lyric to add to the Lyrics.</param>
		public void AddLyric(int time, string lyric)
		{
			if (lyrics.Exists(x => x.time == time))
			{
				Debug.LogWarning("A lyric at time '" + time + "' already exists!  Ignoring!  Lyric was: \"" + lyric + "\"");
			}
			else
			{
				MIDITimedMessage<string> lyricObj = new MIDITimedMessage<string>();
				lyricObj.msg = lyric;
				lyricObj.time = time;

				lyrics.Add(lyricObj);
			}
		}

		/// <summary>
		/// Gets the <c>List</c> of available lyrics from this Track as <c>MIDITimedMessage&lt;string&gt;</c> objects.
		/// </summary>
		/// <returns>The <c>List</c> of lyrics found in this Track.</returns>
		public List<MIDITimedMessage<string>> GetLyrics()
		{
			return lyrics;
		}

		/// <summary>
		/// Determines whether this <c>Track</c> has a <c>Channel</c> at
		/// <paramref name="channelNum"/>.
		/// </summary>
		/// <returns><c>true</c> if this <c>Track</c> has a <c>Channel</c> at
		/// <paramref name="channelNum"/>; otherwise, <c>false</c>.</returns>
		/// <param name="channelNum">Channel number.</param>
		public bool HasChannel(int channelNum)
		{
			return channels.ContainsKey(channelNum);
		}

		/// <summary>
		/// Gets the <c>Channel</c> at <paramref name="channelNum"/>.  If
		/// <paramref name="bCreateIfNull"/> is <c>true</c>, a <c>Channel</c>
		/// will be created if it doesn't already exist.
		/// </summary>
		/// <returns>A <c>Channel</c> at <paramref name="channelNum"/>.</returns>
		/// <param name="channelNum">The <c>Channel</c> to retrieve.</param>
		/// <param name="bCreateIfNull">If set to <c>true</c> creates the <c>Channel</c>
		/// if it doesn't already exist.</param>
		public Channel GetChannel(int channelNum, bool bCreateIfNull = true)
		{
			Channel chan = null;
			
			if (HasChannel(channelNum))
			{
				chan = channels[channelNum];
			}
			else if (bCreateIfNull)
			{
				chan = new Channel();
				channels.Add(channelNum, chan);
			}
			
			return chan;
		}

		/// <summary>
		/// Prints information about the Track.
		/// </summary>
		public void PrintInfo()
		{
			string output = "Track with Name: " + name;
			
			if (!string.IsNullOrEmpty(instrumentName))
			{
				output += ", and Instrument: " + instrumentName;
			}
			
			foreach (int chanNum in channels.Keys)
			{
				output += "\nChannel " + chanNum + " has " + channels[chanNum].NumNotes() + " notes." + " (" + channels[chanNum].NumUnendedNotes() + " unended)";
			}
			
			Debug.Log(output);
		}

		#endregion
	}

	/// <summary>
	/// Adds MIDI timing information to a MIDI Message.
	/// </summary>
	// NOTE: This could be removed if we put the Time in Ticks into the target messages
	//  (generally this means MIDITempo, MIDITimeSignature, MIDIKeySignature).
	public class MIDITimedMessage<T>
	{
		/// <summary>
		/// The time in ticks.
		/// </summary>
		public int time			= 0;					// Time in ticks.
		/// <summary>
		/// The time in seconds.
		/// </summary>
		public double timeInSec = double.MinValue;		// Time in seconds.
		/// <summary>
		/// The wrapped message.
		/// </summary>
		public T msg;
	}

	/// <summary>
	/// Representation of a MIDI File as a song.
	/// </summary>
	public class Song
	{
		#region Fields

		MIDITimeDivision							timeDivision;
//		SMPTETimecode								startOffset;							// TODO: Implement SMPTE timecode support.
		List<MIDITimedMessage<MIDITempo>>			tempoMap	= new List<MIDITimedMessage<MIDITempo>>();
		List<MIDITimedMessage<MIDITimeSignature>>	timeSigMap	= new List<MIDITimedMessage<MIDITimeSignature>>();
		List<MIDITimedMessage<MIDIKeySignature>>	keySigMap	= new List<MIDITimedMessage<MIDIKeySignature>>();
		List<Track>									tracks		= new List<Track>();

		#endregion
		#region Properties

		/// <summary>
		/// Gets the tempo map.
		/// </summary>
		/// <value>The tempo map.</value>
		public List<MIDITimedMessage<MIDITempo>> TempoMap
		{
			get
			{
				return tempoMap;
			}
		}

		/// <summary>
		/// Gets the time signature map.
		/// </summary>
		/// <value>The time signature map.</value>
		public List<MIDITimedMessage<MIDITimeSignature>> TimeSignatureMap
		{
			get
			{
				return timeSigMap;
			}
		}

		/// <summary>
		/// Gets the key signature map.
		/// </summary>
		/// <value>The key signature map.</value>
		public List<MIDITimedMessage<MIDIKeySignature>> KeySignatureMap
		{
			get
			{
				return keySigMap;
			}
		}

		/// <summary>
		/// Gets the tracks.
		/// </summary>
		/// <value>The tracks.</value>
		public List<Track> Tracks
		{
			get
			{
				return tracks;
			}
		}

		#endregion
		#region Static Methods

		// NOTE: Cache if these get used a lot.
		static MIDITimedMessage<MIDITempo> GetDefaultTempo()
		{
			MIDITimedMessage<MIDITempo> retTempo = new MIDITimedMessage<MIDITempo>();
			retTempo.msg = MIDITempo.DefaultTempo;
			retTempo.time = 0;
			retTempo.timeInSec = 0d;

			return retTempo;
		}

		// Cache if these get used a lot.
		static MIDITimedMessage<MIDITimeSignature> GetDefaultTimeSignature()
		{
			MIDITimedMessage<MIDITimeSignature> retTimeSig = new MIDITimedMessage<MIDITimeSignature>();
			retTimeSig.msg = MIDITimeSignature.DefaultTimeSignature;
			retTimeSig.time = 0;
			retTimeSig.timeInSec = 0d;
			
			return retTimeSig;
		}

		// Cache if these get used a lot.
		static MIDITimedMessage<MIDIKeySignature> GetDefaultKeySignature()
		{
			MIDITimedMessage<MIDIKeySignature> retKeySig = new MIDITimedMessage<MIDIKeySignature>();
			retKeySig.msg = MIDIKeySignature.DefaultKeySignature;
			retKeySig.time = 0;
			retKeySig.timeInSec = 0d;
			
			return retKeySig;
		}

		#endregion
		#region Methods

		/// <summary>
		/// Sets the MIDI Time Division for the song.
		/// </summary>
		/// <param name="div">The MIDI Time Division to which to set the song.</param>
		public void SetTimeDiv(MIDITimeDivision div)
		{
			timeDivision = div;
		}

		double GetTimeInSeconds(int messageTime, MIDITimedMessage<MIDITempo> targetTempo)
		{
			double retTime = 0d;

			if (targetTempo == null)
			{
				MIDITimedMessage<MIDITempo> defTempo = new MIDITimedMessage<MIDITempo>();
				defTempo.msg = MIDITempo.DefaultTempo;
				defTempo.time = 0;
				defTempo.timeInSec = 0d;
				
				targetTempo = defTempo;
			}
			
			if (timeDivision.bSMPTE)
			{
				// Get # of ticks; divide that by the number of ticks-per-second.
				retTime = targetTempo.timeInSec + (double)((decimal)(messageTime - targetTempo.time) / ((decimal)timeDivision.fps * (decimal)timeDivision.subFrames));
			}
			else
			{
				// Get Seconds-per-quarter; divide that by ticks-per-quarter; multiply by ticks to get # secs.
				retTime = targetTempo.timeInSec + (double)((((decimal)targetTempo.msg.microPerQuarter / 1000000M) / (decimal)timeDivision.ticks) * (decimal)(messageTime - targetTempo.time));
			}
			
			return retTime;
		}

		/// <summary>
		/// Converts <paramref name="messageTime"/> from MIDI Time to seconds.
		/// </summary>
		/// <returns><paramref name="messageTime"/> converted to seconds.</returns>
		/// <param name="messageTime">The MIDI Time of the message to convert.</param>
		public double GetTimeInSeconds(int messageTime)
		{
			return GetTimeInSeconds(messageTime, GetTempoAtTime(messageTime));
		}
		
		public void AddTrack(Track track)
		{
			tracks.Add(track);
		}

		public int NumTracks()
		{
			return tracks.Count;
		}

		/// <summary>
		/// Adds <paramref name="tempo"/> to the Tempo Map at <paramref name="time"/>.
		/// </summary>
		/// <param name="time">The time at which to add <paramref name="tempo"/>.</param>
		/// <param name="tempo">The <c>MIDITempo</c> to add to the Tempo Map.</param>
		public void AddTempo(int time, MIDITempo tempo)
		{
			if (tempoMap.Exists(x => x.time == time))
			{
				Debug.LogWarning("Ignored duplicate Tempo Entry! Received '" + tempo.microPerQuarter + "' for time '" + time + "', but already have '" + tempoMap.Find(x => x.time == time).msg.microPerQuarter + "'.");
			}
			else
			{
				MIDITimedMessage<MIDITempo> tempoMsg = new MIDITimedMessage<MIDITempo>();
				tempoMsg.msg = tempo;
				tempoMsg.time = time;

				// Pre-calculate the time-in-seconds
				if (tempoMap.Count <= 0)
				{
					if (tempoMsg.time != 0)
					{
						Debug.LogWarning("Strange MIDI format encountered. Inserting default Tempo for beginning of TempoMap.");

						// Add a default tempo.
						AddTempo(0, MIDITempo.DefaultTempo);

						// Get the time in seconds using the newly added default tempo.
						tempoMsg.timeInSec = GetTimeInSeconds(time, tempoMap[0]);
					}
					else
					{
						tempoMsg.timeInSec = 0d;
					}
				}
				else
				{
					// Simply get the time based on the previous Tempo section.
					tempoMsg.timeInSec = GetTimeInSeconds(time, tempoMap[tempoMap.Count - 1]);
				}

				tempoMap.Add(tempoMsg);
			}
		}

		/// <summary>
		/// Adds <paramref name="sig"/> to the Time Signature Map at
		/// <paramref name="tickTime"/>.
		/// </summary>
		/// <param name="tickTime">The time at which to add <paramref name="sig"/>.</param>
		/// <param name="sig">The <c>MIDITimeSignature</c> to add to
		/// the Time Signature Map.</param>
		public void AddTimeSignature(int tickTime, MIDITimeSignature sig)
		{
			if (timeSigMap.Exists(x => x.time == tickTime))
			{
				Debug.LogWarning("Ignored duplicate Time Signature Entry! Received '" + sig.GetTimeSig() + "' for time '" + tickTime + "', but already have '" + timeSigMap.Find(x => x.time == tickTime).msg.GetTimeSig() + "'.");
			}
			else
			{
				MIDITimedMessage<MIDITimeSignature> timeSigMsg = new MIDITimedMessage<MIDITimeSignature>();
				timeSigMsg.msg = sig;
				timeSigMsg.time = tickTime;
				timeSigMsg.timeInSec = GetTimeInSeconds(tickTime);

				timeSigMap.Add(timeSigMsg);
			}
		}

		/// <summary>
		/// Adds <paramref name="sig"/> to the Key Signature Map at
		/// <paramref name="tickTime"/>.
		/// </summary>
		/// <param name="tickTime">The time at which to add <paramref name="sig"/>.</param>
		/// <param name="sig">The <c>MIDIKeySignature</c> to add to
		/// the Key Signature Map.</param>
		public void AddKeySignature(int tickTime, MIDIKeySignature sig)
		{
			if (keySigMap.Exists(x => x.time == tickTime))
			{
				Debug.LogWarning("Ignored duplicate Key Signature Entry! Received '" + sig.GetMusicKey() + "' for time '" + tickTime + "', but already have '" + keySigMap.Find(x => x.time == tickTime).msg.GetMusicKey() + "'.");
			}
			else
			{
				MIDITimedMessage<MIDIKeySignature> keySigMsg = new MIDITimedMessage<MIDIKeySignature>();
				keySigMsg.msg = sig;
				keySigMsg.time = tickTime;
				keySigMsg.timeInSec = GetTimeInSeconds(tickTime);

				keySigMap.Add(keySigMsg);
			}
		}

		/// <summary>
		/// Determines whether this <c>Song</c> has a Tempo Map.
		/// </summary>
		/// <returns><c>true</c> if this <c>Song</c> has a Tempo Map;
		/// otherwise, <c>false</c>.</returns>
		public bool HasTempoMap()
		{
			return tempoMap.Count > 0;
		}

		/// <summary>
		/// Determines whether this <c>Song</c> has a Time Signature Map.
		/// </summary>
		/// <returns><c>true</c> if this <c>Song</c> has a Time Signature
		/// Map; otherwise, <c>false</c>.</returns>
		public bool HasTimeSignatureMap()
		{
			return timeSigMap.Count > 0;
		}

		/// <summary>
		/// Gets the Tempo Map entry at <paramref name="tickTime"/>.
		/// </summary>
		/// <returns>The Tempo Map entry at <paramref name="tickTime"/>.</returns>
		/// <param name="tickTime">The time to get the Tempo Map entry of.</param>
		public MIDITimedMessage<MIDITempo> GetTempoAtTime(int tickTime)
		{
			MIDITimedMessage<MIDITempo> retTempo = tempoMap.FindLast(x => x.time <= tickTime);
			if (retTempo == null)
			{
				retTempo = GetDefaultTempo();
			}
			return retTempo;
		}

		/// <summary>
		/// Gets the Time Signature Map entry at <paramref name="tickTime"/>.
		/// </summary>
		/// <returns>The Time Signature Map entry at <paramref name="tickTime"/>.</returns>
		/// <param name="tickTime">The time to get the Time Signature Map entry of.</param>
		public MIDITimedMessage<MIDITimeSignature> GetTimeSignatureAtTime(int tickTime)
		{
			MIDITimedMessage<MIDITimeSignature> retSig = timeSigMap.FindLast(x => x.time <= tickTime);
			if (retSig == null)
			{
				retSig = GetDefaultTimeSignature();
			}
			return retSig;
		}

		/// <summary>
		/// Gets the Key Signature Map entry at <paramref name="tickTime"/>.
		/// </summary>
		/// <returns>The Key Signature Map entry at <paramref name="tickTime"/>.</returns>
		/// <param name="tickTime">The time to get the Key Signature Map entry of.</param>
		public MIDITimedMessage<MIDIKeySignature> GetKeySignatureAtTime(int tickTime)
		{
			MIDITimedMessage<MIDIKeySignature> retSig = keySigMap.FindLast(x => x.time <= tickTime);
			if (retSig == null)
			{
				retSig = GetDefaultKeySignature();
			}
			return retSig;
		}

		#endregion
	}
}
