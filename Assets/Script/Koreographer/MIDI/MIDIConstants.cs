//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

namespace SonicBloom.MIDI
{
	/// <summary>
	/// Values defined by the MIDI standard.
	/// </summary>
	public class MIDIConstants
	{
		/// <summary>
		/// MIDI File Format Constants.
		/// </summary>
		public enum MIDIFileFormats
		{
			SingleTrack			= 0,
			MultipleTrackSync	= 1,
			MultipleTrackAsync	= 2,
		}

		/// <summary>
		/// The standard MIDI Header identifier string.
		/// </summary>
		public const string HEADER_ID	= "MThd";
		/// <summary>
		/// The standard MIDI Track identifier string.
		/// </summary>
		public const string TRACK_ID	= "MTrk";

		/// <summary>
		/// MIDI Status Constants.
		/// </summary>
		public enum MIDIStatus
		{
			// Voice Messages

			NoteOff				= 0x80,
			NoteOn				= 0x90,
			NoteAftertouch		= 0xA0,		// Aftertouch / Key pressure; Polyphonic Pressure
			ControllerChange	= 0xB0,
			ProgramChange		= 0xC0,
			ChannelPressure		= 0xD0,
			PitchBend			= 0xE0,

			// System Common Messages

			SysExMessage		= 0xF0,
			TimeCode			= 0xF1,
			SongPositionPointer	= 0xF2,
			SongSelect			= 0xF3,
			TuneRequest			= 0xF6,
			EndSysExMessage		= 0xF7,

			// Realtime Messages

			TimingClock			= 0xF8,		// To be sent 6 times per beat (defined as a 16th note)
			Start				= 0xFA,
			Continue			= 0xFB,
			Stop				= 0xFC,
			ActiveSensing		= 0xFE,
			MetaEventOrReset	= 0xFF,		// MetaEvent for MIDI data; SystemReset for MIDI Device
		}

		/// <summary>
		/// MIDI Meta Event Constants
		/// </summary>
		public enum MIDIMetaEvents
		{
			SequenceNumber		= 0x00,
			Text				= 0x01,
			CopyrightNotice		= 0x02,
			SequenceOrTrackName	= 0x03,
			InstrumentName		= 0x04,
			Lyric				= 0x05,
			Marker				= 0x06,
			CuePoint			= 0x07,
			ProgramName			= 0x08,		// AKA Patch Name.
			DeviceName			= 0x09,		// AKA Port Name.
			MIDIChannelPrefix	= 0x20,		// A channel number (following meta events will apply to this channel)
			MIDIPort			= 0x21,		// Specifies which MIDI Port (i.e. bus) the MIDI events in the MTrk go to.
			EndOfTrack		 	= 0x2F,
			SetTempo			= 0x51,
			SMPTEOffset			= 0x54,
			TimeSignature		= 0x58,
			KeySignature		= 0x59,
			SequencerSpecific	= 0x7F,
		}

		/// <summary>
		/// MIDI Controller Constants.
		/// </summary>
		public enum MIDIControllers
		{
			// High resolution continuous controllers (MSB)

			BankSelectCoarse				= 0x00,
			ModulationWheelCoarse			= 0x01,
			BreathControllerCoarse			= 0x02,
			FootControllerCoarse			= 0x04,
			PortamentoTimeCoarse			= 0x05,
			DataEntryCoarse					= 0x06,
			ChannelVolumeCoarse				= 0x07,	// Formerly main volume
			BalanceCoarse					= 0x08,
			PanCoarse						= 0x0A,
			ExpressionCoarse				= 0x0B,
			EffectControl1Coarse			= 0x0C,
			EffectControl2Coarse			= 0x0D,
			GeneralPurposeController1Coarse	= 0x10,
			GeneralPurposeController2Coarse = 0x11,
			GeneralPurposeController3Coarse = 0x12,
			GeneralPurposeController4Coarse = 0x13,

			// High resolution continuous controllers (LSB)

			BankSelectFine					= 0x20,
			ModulationWheelFine				= 0x21,
			BreathControllerFine			= 0x22,
			FootControllerFine				= 0x24,
			PortamentoTimeFine				= 0x25,
			DataEntryFine					= 0x26,
			ChannelVolumeFine				= 0x27,	// Formerly main volume
			BalanceFine						= 0x28,
			PanFine							= 0x2A,
			ExpressionFine					= 0x2B,
			EffectControl1Fine				= 0x2C,
			EffectControl2Fine				= 0x2D,

			// Switches

			HoldPedal1						= 0x40,	// Damper, sustain; on/off
			PortamentoPedal					= 0x41,	// on/off
			SostenutoPedal					= 0x42,	// on/off
			SoftPedal						= 0x43,	// on/off
			LegatoPedal						= 0x44,	// on/off
			HoldPedal2						= 0x45,	// on/off

			// Low resolution continuous controllers

			SoundController1				= 0x46,	// Default is sound variation
			SoundController2				= 0x47,	// Default is timbre / harmonic intensity / filter resonance
			SoundController3				= 0x48,	// Default is release time
			SoundController4				= 0x49,	// Default is attack time
			SoundController5				= 0x4A,	// Default is brightness or cutoff frequency
			SoundController6				= 0x4B,	// Default is decay time
			SoundController7				= 0x4C,	// Default is vibrato rate
			SoundController8				= 0x4D,	// Default is vibrato depth
			SoundController9				= 0x4E,	// Default is vibrato delay
			Soundcontroller10				= 0x4F,	// Default is undefined
			GeneralPurposeController5		= 0x50,
			GeneralPurposeController6		= 0x51,
			GeneralPurposeController7		= 0x52,
			GeneralPurposeController8		= 0x53,
			PortamentoControl				= 0x54,
			HighResolutionVelocityPrefix	= 0x58,
			Effect1Depth					= 0x5B,	// Default is reverb send level, formerly external effect depth
			Effect2Depth					= 0x5C,	// Formerly tremolo depth
			Effect3Depth					= 0x5D,	// Default is chorus send level, formerly chorus depth
			Effect4Depth					= 0x5E,	// Formerly celeste depth
			Effect5Depth					= 0x5F,	// Formerly phaser level

			// RPNs / NRPNs

			DataButtonIncrement				= 0x60,
			DataButtonDecrement				= 0x61,
			NonRegisteredParameterCoarse	= 0x62,
			NonRegisteredParameterFine		= 0x63,
			RegisteredParameterCoarse		= 0x64,
			RegisteredParameterFine			= 0x65,

			// Channel Mode Messages

			AllSoundOff						= 0x78,
			AllControllersOff				= 0x79,
			LocalControl					= 0x7A,	// on/off
			AllNotesOff						= 0x7B,
			OmniModeOff						= 0x7C,
			OmniModeOn						= 0x7D,
			MonoOperation					= 0x7E,	// And all notes off
			PolyOperation					= 0x7F,	// And all notes off
		}
	}
}
