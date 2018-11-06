//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using System;
using UnityEngine;

namespace SonicBloom.Koreo
{
	/// <summary>
	/// The EventID Attribute allows you to mark a serializable or public <c>string</c>
	/// field as being an EventID.  When this happens, the field gets special
	/// consideration in the Inspector, showing a customizable field and a list of
	/// Event ID options configured across Koreography Tracks found within the current
	/// project.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
	public class EventIDAttribute : PropertyAttribute
	{
		// No content needed.  All the work is done in the Property Drawer.
	}
}
