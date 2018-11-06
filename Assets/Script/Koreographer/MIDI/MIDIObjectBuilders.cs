//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;
using System.IO;

namespace SonicBloom.MIDI.Objects
{
	/// <summary>
	/// <para>Used to create Song objects from passed in MIDI File references.
	/// As this class uses managed objects (Files) we need to be careful
	/// about disposing them properly.  To this end, it extends the BinaryReader
	/// class, which works with the using() statement to allow for runtime-supported
	/// cleanup in case of unhandled exception.</para>
	/// 
	/// <para>WARNING: Always be sure to call Dispose() as necessary or use the
	/// using() statement.</para>
	/// 
	/// <para>At present this shouldn't be a problem as the only accessor is
	/// a static builder function that handles this internally.</para>
	/// </summary>
	public class SongBuilder : BinaryReader
	{
#region Static Members

		static bool bPrintDebug = false;

		/// <summary>
		/// Builds the <see cref="SonicBloom.MIDI.Objects.Song"/> object representation
		/// of the MIDI file at location <paramref name="midiFileLoc"/>.
		/// </summary>
		/// <returns>A <see cref="SonicBloom.MIDI.Objects.Song"/> representation of the
		/// MIDIfile at location <paramref name="midiFileLoc"/>.</returns>
		/// <param name="midiFileLoc">The location of the MIDI file.</param>
		public static Song GetSong(string midiFileLoc)
		{
			Song retSong = null;

			using (FileStream file = File.OpenRead(midiFileLoc))
			{
				if (file != null)
				{
					using (SongBuilder builder = new SongBuilder(file))
					{
						retSong = builder.ParseSong();
					}
				}
			}

			return retSong;
		}

#endregion
#region Members

		// The raw parsed MIDI Chunks.
		MIDIHeader header;
		Song song;

#endregion
#region Constructors

		SongBuilder(Stream dataStream)
			: base(dataStream)
		{
			// We do nothing special.
		}

#endregion
#region Parsing Methods

		Song ParseSong()
		{
			if (ReadHeader())
			{
				song = new Song();
				song.SetTimeDiv(header.timeDivision);

				// Loop over the tracks.
				for (int i = 0; i < header.numTracks; ++i)
				{
					Track track = ReadTrack();

					if (track != null)
					{
						// In a certain format(s?) the first track is the TempoMap.
						if ((header.format == MIDIConstants.MIDIFileFormats.MultipleTrackAsync && i > 0) ||
						    track.Channels.Count > 0 ||
						    track.HasLyrics())
						{
							song.AddTrack(track);
						}
					}
				}
			}

			// We should now have the Header and Tracks all set to go.
			//  Use that data to build the Song.

			return song;
		}
		
		bool ReadHeader()
		{
			string ID = GetASCIIString(4);
			if (ID != MIDIConstants.HEADER_ID)		// Expect "MThd"
			{
				Debug.LogError("Malformed MIDI file!");
				return false;
			}
			
			int len = MIDIUtils.EndianSwap32(ReadInt32());
			long chunkPos = BaseStream.Position;
			if (len != 6)
			{
				Debug.LogWarning("Non-standard MIDI file header size!  Skipping non-standard parts.");
			}
			
			// Read out the format parts.
			header.format = (MIDIConstants.MIDIFileFormats)MIDIUtils.EndianSwap16(ReadInt16());
			header.numTracks = MIDIUtils.EndianSwap16(ReadInt16());
			
			// Unpack the time.
			short timeInfo = ReadInt16();
			if (System.BitConverter.IsLittleEndian)
			{
				timeInfo = MIDIUtils.EndianSwap16(timeInfo);
			}
			
			// If the Most Significant Byte is positive, it's ticks.  Otherwise SMPTE.
			header.timeDivision.bSMPTE = (timeInfo & 0x8000) != 0;
			
			if (header.timeDivision.bSMPTE)
			{
				header.timeDivision.fps = (float)(0 - (sbyte)(timeInfo >> 8));
				header.timeDivision.subFrames = (int)(timeInfo & 0xFF);

				if (header.timeDivision.fps == 29f)
				{
					// Fix up the 29 case.
					header.timeDivision.fps = 29.97f;
				}
			}
			else
			{
				// Tick count is the 15 bits.
				header.timeDivision.ticks = (int)(timeInfo & 0x7FFF);
			}
			
			BaseStream.Position = chunkPos + len;
			return true;
		}
		
		Track ReadTrack()
		{
			if (GetASCIIString(4) != MIDIConstants.TRACK_ID)
			{
				Debug.LogWarning("Malformed MIDI Track in file!");
				return null;
			}

			Track track = new Track();
			
			int trackLen = MIDIUtils.EndianSwap32(ReadInt32());
			long trackEnd = BaseStream.Position + trackLen;
			
			int eventTime = 0;
			
			MIDIEvent midiEvent;
			midiEvent.status = MIDIConstants.MIDIStatus.NoteOff;	// Error suppression.
			
			while (BaseStream.Position < trackEnd)
			{
				midiEvent.deltaTime = MIDIUtils.ReadVariableLengthQuantity(this);
				
				byte status = ReadByte();
				if ((status & 0x80) == 0x80)
				{
					midiEvent.status = (MIDIConstants.MIDIStatus)status;
				}
				else
				{
					// Leave midiEvent.status alone as it should save the one from before.
					// This handles "MIDI Running Status", wherein the "status" byte is not set.
					//  This can happen when two subsequent messages have the same status (only send the payload).
					//  In particular, this occurs with NoteOn messages.  A NoteOn message with Velocity = 0 is
					//  actually a NoteOff.
					//  See: http://www.recordingblogs.com/sa/tabid/88/Default.aspx?topic=MIDI+event
					
					BaseStream.Position -= 1;	// Back us out one.
				}
				
				eventTime += midiEvent.deltaTime;
				
				if ((MIDIConstants.MIDIStatus)((int)midiEvent.status & 0xF0) == MIDIConstants.MIDIStatus.NoteOn)
				{
					midiEvent.channel = MIDIUtils.LowNibble((byte)midiEvent.status);
					int note = ReadByte();
					int velocity = ReadByte();
					
					bool bNoteOff = false;
					
					// Velocity of 0 means NoteOff.
					if (velocity == 0 && track.GetChannel(midiEvent.channel).HasUnendedNoteWithKey(note))
					{
						//TODO:  Can't do this!  It will ruin the running status.  Luckily we don't actually store the midiEvent struct
						// anywhere yet.  BE CAREFUL!!!!!
						//midiEvent.status = (MIDIConstants.MIDIStatus)((byte)MIDIConstants.MIDIStatus.NoteOff | midiEvent.channel);
						bNoteOff = true;
						
						track.GetChannel(midiEvent.channel).EndNote(eventTime, note, velocity);
					}
					else
					{
						track.GetChannel(midiEvent.channel).BeginNote(eventTime, note, velocity);
					}
					
					if (bPrintDebug)
					{
						if (!bNoteOff)
							Debug.Log("---Note On---\nChannel: " + midiEvent.channel + ", Note: " + note + ", Velocity: " + velocity);
						else
							Debug.Log("---Note Off 2---\nChannel: " + midiEvent.channel + ", Note: " + note + ", Velocity: " + velocity);
					}
				}
				else if ((MIDIConstants.MIDIStatus)((int)midiEvent.status & 0xF0) == MIDIConstants.MIDIStatus.NoteOff)
				{
					midiEvent.channel = MIDIUtils.LowNibble((byte)midiEvent.status);
					int note = ReadByte();
					int velocity = ReadByte();
					
					if (bPrintDebug)
						Debug.Log("---Note Off---\nChannel: " + midiEvent.channel + ", Note: " + note + ", Velocity: " + velocity);
					
					track.GetChannel(midiEvent.channel).EndNote(eventTime, note, velocity);
				}
				else if ((MIDIConstants.MIDIStatus)((int)midiEvent.status & 0xF0) == MIDIConstants.MIDIStatus.NoteAftertouch)
				{
					midiEvent.channel = MIDIUtils.LowNibble((byte)midiEvent.status);
					
					byte note = ReadByte();
					byte pressure = ReadByte();
					
					if (bPrintDebug)
						Debug.Log("---Polyphonic Pressure---\nChannel: " + midiEvent.channel + ", Note: " + note + ", Pressure: " + pressure);
				}
				else if ((MIDIConstants.MIDIStatus)((int)midiEvent.status & 0xF0) == MIDIConstants.MIDIStatus.ControllerChange)
				{
					midiEvent.channel = MIDIUtils.LowNibble((byte)midiEvent.status);
					MIDIConstants.MIDIControllers controller = (MIDIConstants.MIDIControllers)ReadByte();
					byte value = ReadByte();
					
					if (bPrintDebug)
						Debug.Log("---Controller Change---\nChannel: " + midiEvent.channel + ", Controller Change: " + controller + " with value: " + value);
				}
				else if ((MIDIConstants.MIDIStatus)((int)midiEvent.status & 0xF0) == MIDIConstants.MIDIStatus.ProgramChange)
				{
					midiEvent.channel = MIDIUtils.LowNibble((byte)midiEvent.status);
					byte program = ReadByte();
					
					if (bPrintDebug)
						Debug.Log("---Program Change---\nChannel: " + midiEvent.channel + " to New Program: " + program);
				}
				else if ((MIDIConstants.MIDIStatus)((int)midiEvent.status & 0xF0) == MIDIConstants.MIDIStatus.ChannelPressure)
				{
					midiEvent.channel = MIDIUtils.LowNibble((byte)midiEvent.status);
					byte pressure = ReadByte();
					
					if (bPrintDebug)
						Debug.Log("---Channel Pressure---\nChannel: " + midiEvent.channel + ", Pressure: " + pressure);
				}
				else if ((MIDIConstants.MIDIStatus)((int)midiEvent.status & 0xF0) == MIDIConstants.MIDIStatus.PitchBend)
				{
					midiEvent.channel = MIDIUtils.LowNibble((byte)midiEvent.status);
					ushort pitch = (ushort)((ReadByte() << 7) | ReadByte());	// Default is 0x2000, middle of possible 14-bit range [0x0000,0x3FFF].
					
					if (bPrintDebug)
						Debug.Log("---Pitch Bend---\nChannel: " + midiEvent.channel + ", Pitch: " + pitch);
				}
				else if (midiEvent.status == MIDIConstants.MIDIStatus.MetaEventOrReset)	// These are MetaEvents in MIDI files.
				{
					MIDIConstants.MIDIMetaEvents meta = (MIDIConstants.MIDIMetaEvents)ReadByte();
					int length = MIDIUtils.ReadVariableLengthQuantity(this);
					
					if (meta == MIDIConstants.MIDIMetaEvents.SequenceOrTrackName)
					{
						string trackName = GetASCIIString(length);
						
						if (bPrintDebug)
							Debug.Log("-- Sequence or Track Name Event --\nName: " + trackName);
						
						track.name = trackName;
					}
					else if (meta == MIDIConstants.MIDIMetaEvents.InstrumentName)
					{
						string instName = GetASCIIString(length);
						
						if (bPrintDebug)
							Debug.Log("-- Instrument Name --\nName: " + instName);
						
						track.instrumentName = instName;
					}
					else if (meta == MIDIConstants.MIDIMetaEvents.EndOfTrack)
					{
						if (bPrintDebug)
							Debug.Log("-- End of Track Event -- ");
					}
					else if (meta == MIDIConstants.MIDIMetaEvents.SetTempo)
					{
						MIDITempo tempo = new MIDITempo();
						tempo.microPerQuarter = (ReadByte() << 16) | ReadByte() << 8 | ReadByte();

						song.AddTempo(eventTime, tempo);
						
						if (bPrintDebug)
							Debug.Log("-- Set Tempo Event (" + midiEvent.deltaTime + ") --\nMicroseconds Per Quarter: " + tempo.microPerQuarter + "\nBPM: " + tempo.GetBPM());
					}
					else if (meta == MIDIConstants.MIDIMetaEvents.SMPTEOffset)
					{
						// See http://en.wikipedia.org/wiki/MIDI_timecode for breakdown.
						byte hoursAndFrames = ReadByte();
						int hours = hoursAndFrames & 0x1F;	// Masking out the hours with bitmask: 0001 1111
						
						float frameRate = 30f;				// Frame rate is in bits: 0110 0000
						switch (hoursAndFrames >> 5)
						{
						case 0:
							frameRate = 24f;
							break;
						case 1:
							frameRate = 25f;
							break;
						case 2:
							frameRate = 29.97f;
							break;
						}
						
						int mins = ReadByte();
						int secs = ReadByte();
						int frames = ReadByte();
						int subFrames = ReadByte();	// Out of 100!  (Always 100 subframes per frame.)
						
						if (bPrintDebug)
						{
							Debug.Log("-- SMPTE Offset Event --\nTrack Offset from Sequence Start: " +
							          hours + ":" +
							          mins + ":" +
							          secs + ";" +
							          frames + "." +
							          subFrames + " at " + frameRate + "fps");
						}
					}
					else if (meta == MIDIConstants.MIDIMetaEvents.TimeSignature)
					{
						MIDITimeSignature timeSig = new MIDITimeSignature();
						timeSig.beatsPerBar = ReadByte();
						timeSig.noteValue = 1 << ReadByte();		// The base note value for a bar (4 = quarter notes).
						timeSig.midiClocks = ReadByte();			// Per metronome click.
						timeSig.num32NotesPerBeat = ReadByte();	// Defines a "beat".  E.g.: A quarter note 'beat' would be 8 (as there are 8 32-notes in a quarter note).

						song.AddTimeSignature(eventTime, timeSig);
						
						if (bPrintDebug)
							Debug.Log("-- Time Signature Event --\nTime Signature: " + timeSig.beatsPerBar + "/" + timeSig.noteValue + "\nMIDI Clocks: " + timeSig.midiClocks + "\n32nd notes per beat: " + timeSig.num32NotesPerBeat);
					}
					else if (meta == MIDIConstants.MIDIMetaEvents.KeySignature)
					{
						MIDIKeySignature keySig = new MIDIKeySignature();
						keySig.key = (int)ReadSByte();		// Key Signature [-7,7]: Negative is num flats, Positive is num sharps.
						keySig.bMinor = (ReadByte() == 1);	// Major [0] or Minor [1] scale?

						song.AddKeySignature(eventTime, keySig);
						
						if (bPrintDebug)
						{
							Debug.Log("-- Key Signature Event --\nKey signature: " + (keySig.key == 0 ? "C Major" : keySig.key < 0 ? -keySig.key + " flats" : keySig.key + " sharps") + ", in the " + (keySig.bMinor ? "minor" : "major") + " scale");
						}
					}
					else if (meta == MIDIConstants.MIDIMetaEvents.SequencerSpecific)
					{
						if (bPrintDebug)
							Debug.Log("-- Sequencer Specific Event --\nSequencer ID: " + ReadByte().ToString("X") + " with subType: " + ReadByte().ToString("X") + "" + ReadByte().ToString("X") + ", and ASCII: " + GetASCIIString(length - 3) + " || at time: " + midiEvent.deltaTime);
						else
							BaseStream.Position += length;
					}
					else if (meta == MIDIConstants.MIDIMetaEvents.Text)
					{
						string text = GetASCIIString(length);
						
						if (bPrintDebug)
							Debug.Log("-- Text Event --\nText: \'" + text + "\'.");
					}
					else if (meta == MIDIConstants.MIDIMetaEvents.MIDIChannelPrefix)
					{
						byte channel = ReadByte();
						
						if (bPrintDebug)
							Debug.Log("-- MIDI Channel Prefix Event --\nUntil the next MIDI event, all following Meta-events and Sysex-events are intended for channel " + channel + ".");
					}
					else if (meta == MIDIConstants.MIDIMetaEvents.Lyric)
					{
						string lyric = GetASCIIString(length);

						track.AddLyric(eventTime, lyric);

						if (bPrintDebug)
							Debug.Log("-- Lyric Event --\nLyric: \'" + lyric + "\'.");
					}
					else
					{
						Debug.LogWarning("Unhandled meta event: " + meta);
						BaseStream.Position += length;
					}
				}
				else
				{
					Debug.LogWarning("Unhandled status: " + (MIDIConstants.MIDIStatus)((int)midiEvent.status));
					
					// Read to next status.
					//  All status values have the most significant bit set to 1.
					//  Non status bytes have the most significant bit set to 0 (thus the 7-bits available to most data).
					byte val = ReadByte();
					while ((val & 0x80) == 0)
					{
						val = ReadByte();
					}
					
					// We have come across the next status byte.  Back up one if it's not the EOX message (0xF7)!
					if (val != 0xF7)
					{
						BaseStream.Position -= 1;
					}
				}
			}

			return track;
		}

#endregion
#region Helper Methods

		string GetASCIIString(int len)
		{
			byte[] str = ReadBytes(len);
			// UTF8 is a superset of ASCII.  ASCII encoding is not available on some platforms (WinRT).
			//return System.Text.Encoding.ASCII.GetString(str);
			
			// Must use the overloaded version that includes index and count because the base version
			//  doesn't exist on some platforms (XNA; Portable Class Library).
			return System.Text.Encoding.UTF8.GetString(str, 0, str.Length);
		}

#endregion
	}
}
