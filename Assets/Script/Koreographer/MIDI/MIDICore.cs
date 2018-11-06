//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using System.Collections.Generic;
using System.IO;

namespace SonicBloom.MIDI
{
	/// <summary>
	/// Basic MIDI Time representation.
	/// </summary>
	public struct MIDITime
	{
		public int bar;
		public int beat;
		public int tick;
	}

	/// <summary>
	/// SMPTE time representation.
	/// </summary>
	public struct SMPTETime
	{
		public int hours;
		public int minutes;
		public int seconds;
		public int frames;
		public int ticks;					// Sub-Frames
	}

	/// <summary>
	/// Representation of MIDI Time Division, or resolution
	/// </summary>
	public struct MIDITimeDivision
	{
		/// <summary>
		/// Whether time uses SMPTE representation.
		/// </summary>
		public bool		bSMPTE;				// Is or isn't SMPTE time divisions.

		// SMPTE

		/// <summary>
		/// The fps (for SMPTE).
		/// </summary>
		public float	fps;				// Frames per second.
		/// <summary>
		/// The sub frames, or updates per 'frame' (for SMPTE).
		/// </summary>
		public int		subFrames;			// Updates per frame.

		// PPQN

		/// <summary>
		/// The ticks or PPQN (Pulses Per Quarter Note)
		/// </summary>
		public int		ticks;				// Ticks, aka PPQN (Pulses Per Quarter Note).
	}

	/// <summary>
	/// Representation of MIDI Tempo.
	/// </summary>
	public class MIDITempo
	{
		/// <summary>
		/// A default MIDI Tempo definition.
		/// </summary>
		public static MIDITempo DefaultTempo = new MIDITempo();

		/// <summary>
		/// Thenumber of Microseconds Per Quarter Note that define the tempo.
		/// </summary>
		public int microPerQuarter = 500000;	// Microseconds Per Quarter Note.

		/// <summary>
		/// Gets the BPM.
		/// </summary>
		/// <returns>The BPM.</returns>
		public float GetBPM()
		{
			return (60000000f / (float)microPerQuarter);
		}
	}

	/// <summary>
	/// Representation of MIDI time signature.
	/// </summary>
	public class MIDITimeSignature
	{
		/// <summary>
		/// A default MIDI Time Signature definition.
		/// </summary>
		public static MIDITimeSignature DefaultTimeSignature = new MIDITimeSignature();

		/// <summary>
		/// The beats per bar (or measure).
		/// </summary>
		public int beatsPerBar			= 4;
		/// <summary>
		/// The note value.  This is the denominator of the time signature. A
		/// value of 4 means quarter notes.  Also known as the Beat Unit.
		/// </summary>
		public int noteValue			= 4;
		/// <summary>
		/// How many MIDI clocks per metronome click.
		/// </summary>
		public int midiClocks			= 24;	// Assuming default of 24 midi clocks per quarter note.
		/// <summary>
		/// Defines a "beat".
		/// <example>A quarter note 'beat' would be 8 (as there are 8 32-notes in a quarter note).</example>
		/// </summary>
		public int num32NotesPerBeat	= 8;	// Defines a "beat".  E.g.: A quarter note 'beat' would be 8 (as there are 8 32-notes in a quarter note).

		/// <summary>
		/// Returns a human-readable time signature.
		/// </summary>
		/// <returns>The time signature as a <c>string</c>.</returns>
		public string GetTimeSig()
		{
			return beatsPerBar + "/" + noteValue;
		}
	}

	/// <summary>
	/// Representation of MIDI key signature.
	/// </summary>
	public class MIDIKeySignature
	{
		/// <summary>
		/// A default key signature definition.
		/// </summary>
		public static MIDIKeySignature DefaultKeySignature = new MIDIKeySignature();

		static string[] majorKeys = {"Cb", "Gb", "Db", "Ab", "Eb", "Bb", "F", "C", "G", "D", "A", "E", "B", "F#", "C#"};
		static string[] minorKeys = {"Ab", "Eb", "Bb", "F", "C", "G", "D", "A", "E", "B", "F#", "C#", "G#", "D#", "A#"};

		/// <summary>
		/// The key.  This is a value of <c>[-7,7]</c>. Negative is flat;
		/// positive is sharp.
		/// </summary>
		public int key		= 0;
		public bool bMinor	= false;

		/// <summary>
		/// Gets the human-readable key signature.  Flat keys are followed by a
		/// lowercase 'b', Sharp keys are followed by a '#'.
		/// </summary>
		/// <returns>The key as a <c>string</c>.</returns>
		public string GetMusicKey()
		{
			// Add 7 to normlaize the key range into an array index.
			return bMinor ? minorKeys[key + 7] + " minor" : majorKeys[key + 7] + " major";
		}
	}

	/// <summary>
	/// Representation of a raw MIDI event.
	/// </summary>
	public struct MIDIEvent
	{
		public MIDITime	eventTime;
		public int		deltaTime;			// In ticks (of either MIDITime.tick or SMPTETime.tick)
		public MIDIConstants.MIDIStatus	status;
		public byte		channel;
		public byte		dataOne;			// Event ParamOne
		public byte		dataTwo;			// Event ParamTwo
	}

	/// <summary>
	/// Representation of a raw MIDI track.
	/// </summary>
	public struct MIDITrack
	{
		public int trackNumber;
		public int trackEventCount;			// Not necessary because we're using a linked list with an associated "Count"?
		public List<MIDIEvent> midiEvents;
	}

	/// <summary>
	/// Representation of a raw MIDI header.
	/// </summary>
	public struct MIDIHeader
	{
		/// <summary>
		/// Format of the MIDI file.
		/// 
		/// [NOTE] The first track of a Format 1 file is special, and is also known as the 'Tempo Map'. It should contain all
		/// meta-events of the types Time Signature, and Set Tempo. The meta-events Sequence/Track Name, Sequence Number,
		/// Marker, and SMTPE Offset. should also be on the first track of a Format 1 file.
		///   - http://cs.fit.edu/~ryan/cse4051/projects/midi/midi.html
		/// </summary>
		public MIDIConstants.MIDIFileFormats	format;

		public int								numTracks;

		public MIDITimeDivision 				timeDivision;
	}

	/// <summary>
	/// Utility methods for MIDI operations and types.
	/// </summary>
	public class MIDIUtils
	{
		/// <summary>
		/// Endian swap for a 16-bit value.
		/// </summary>
		/// <returns>An endian-swapped version of <paramref name="input"/></returns>
		/// <param name="input">The value to swap endianness of.</param>
		public static short EndianSwap16(short input)
		{
			return (short)(((input & 0xFF00) >> 8) | ((input & 0xFF) << 8));
		}

		/// <summary>
		/// Endian swap for a 32-bit value.
		/// </summary>
		/// <returns>An endian-swapped version of <paramref name="input"/>.</returns>
		/// <param name="input">The value to swap endianness of.</param>
		public static int EndianSwap32(int input)
		{
			return (int)(((uint)(input & 0xFF000000) >> 24) |	// Byte 4->1
			             ((uint)(input & 0xFF0000) >> 8) | 		// Byte 3->2
			             ((uint)(input & 0xFF00) << 8) |		// Byte 2->3
			             ((uint)(input & 0xFF) << 24));			// Byte 1->4
		}

		/// <summary>
		/// Gets the lower 4-bits of <paramref name="input"/>.
		/// </summary>
		/// <returns>The lower 4-bits of <paramref name="input"/>.</returns>
		/// <param name="input">The value to retrieve the lower nibble from.</param>
		public static byte LowNibble(byte input)
		{
			return (byte)(input & 0xF);
		}

		/// <summary>
		/// Gets the upper 4-bits of <paramref name="input"/>.
		/// </summary>
		/// <returns>The upper 4-bits of <paramref name="input"/>.</returns>
		/// <param name="input">The value to retrieve the upper nibble from.</param>
		public static byte HighNibble(byte input)
		{
			return (byte)((input & 0xF0) >> 4);
		}

		/// <summary>
		/// Reads a variable length quantity value out of <paramref name="reader"/>.
		/// (see: https://en.wikipedia.org/wiki/Variable-length_quantity)
		/// </summary>
		/// <returns>The variable length quantity, packed into an <c>int</c>.</returns>
		/// <param name="reader">The <c>BinaryReader</c> to read the variable length
		/// quantity from.</param>
		public static int ReadVariableLengthQuantity(BinaryReader reader)
		{
			int retValue = 0;
			byte val = reader.ReadByte();

			while ((val & 0x80) != 0)
			{
				// Shuffle in the bits.
				retValue = (retValue << 7) | (val & 0x7F);

				// Read the next value.
				val = reader.ReadByte();
			}

			retValue = (retValue << 7) | (val & 0x7F);

			return retValue;
		}
	}
}
