//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEngine;

namespace SonicBloom.Koreo.EditorUI
{
	/// <summary>
	/// Extra KoreographyEvent methods for editor purposes!
	/// </summary>
	public static class KoreographyEventExtensions
	{
		internal static KoreographyEvent GetCopy(this KoreographyEvent evt)
		{
			KoreographyEvent newEvt = new KoreographyEvent();
			newEvt.StartSample = evt.StartSample;
			newEvt.EndSample = evt.EndSample;
			newEvt.Payload = (evt.Payload != null) ? evt.Payload.GetCopy() : null;
			return newEvt;
		}
		
		internal static void MoveTo(this KoreographyEvent evt, int newSampleLoc)
		{
			newSampleLoc = Mathf.Max(0, newSampleLoc);
			int span = evt.EndSample - evt.StartSample;
			
			evt.StartSample = newSampleLoc;
			evt.EndSample = evt.StartSample + span;
		}
	}
}
