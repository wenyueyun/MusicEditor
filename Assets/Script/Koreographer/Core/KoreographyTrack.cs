//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SonicBloom.Koreo
{
	/// <summary>
	/// <para>The built-in KoreographyTrack class, supporting KoreographyEvents that use
	/// the default set of Payload types.</para>
	/// <para>A single list of Koreography Events. This class attempts to guarantee that
	/// all events in the track are stored in order by Start Sample position.</para>
	/// </summary>
	[System.Serializable]
	public partial class KoreographyTrack : KoreographyTrackBase
	{
		// This class is "partial" for legacy reasons (basically, to make it easier for
		//  source code users to add their own Payload types).

		#region Serialization Handling

		[HideInInspector][SerializeField]
		protected List<AssetPayload>	_AssetPayloads;
		[HideInInspector][SerializeField]
		protected List<int>				_AssetPayloadIdxs;
		[HideInInspector][SerializeField]
		protected List<ColorPayload>	_ColorPayloads;
		[HideInInspector][SerializeField]
		protected List<int>				_ColorPayloadIdxs;
		[HideInInspector][SerializeField]
		protected List<CurvePayload>	_CurvePayloads;
		[HideInInspector][SerializeField]
		protected List<int>				_CurvePayloadIdxs;
		[HideInInspector][SerializeField]
		protected List<FloatPayload>	_FloatPayloads;
		[HideInInspector][SerializeField]
		protected List<int>				_FloatPayloadIdxs;
		[HideInInspector][SerializeField]
		protected List<GradientPayload>	_GradientPayloads;
		[HideInInspector][SerializeField]
		protected List<int>				_GradientPayloadIdxs;
		[HideInInspector][SerializeField]
		protected List<IntPayload>		_IntPayloads;
		[HideInInspector][SerializeField]
		protected List<int>				_IntPayloadIdxs;
		[HideInInspector][SerializeField]
		protected List<SpectrumPayload>	_SpectrumPayloads;
		[HideInInspector][SerializeField]
		protected List<int>				_SpectrumPayloadIdxs;
		[HideInInspector][SerializeField]
		protected List<TextPayload>		_TextPayloads;
		[HideInInspector][SerializeField]
		protected List<int>				_TextPayloadIdxs;
		
		#endregion
	}
}
