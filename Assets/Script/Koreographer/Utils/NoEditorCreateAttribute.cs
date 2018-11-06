//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using System;

namespace SonicBloom.Koreo
{
	/// <summary>
	/// The NoEditorCreate Attribute allows you to mark a class such that it cannot
	/// be created using the standard tools in the Koreography Editor. Currently
	/// works for Payloads and KoreographyTracks.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = true, AllowMultiple = false)]
	public class NoEditorCreateAttribute : Attribute
	{
		// No content needed.  All the work is done in the Koreography Editor.
	}
}
