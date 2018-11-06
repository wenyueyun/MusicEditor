//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEditor;

namespace SonicBloom.Koreo.EditorUI
{
	internal class AssetCreators
	{
		[MenuItem("Assets/Create/Koreography")]
		public static void CreateKoreographyAsset()
		{
			CustomAssetUtility.CreateAsset<Koreography>();
		}

		[MenuItem("Assets/Create/Koreography Track")]
		public static void CreateKoreographyTrackAsset()
		{
			CustomAssetUtility.CreateAsset<KoreographyTrack>();
		}
	}
}
