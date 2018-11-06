//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEditor;
using UnityEngine;

namespace SonicBloom.Koreo.EditorUI
{
	internal static class KoreographyEditorSkin
	{
		static GUISkin theSkin = null;

		static bool bDarkMode = false;

		#region Styles

		public static GUIStyle lcdStyleLeft = null;
		public static GUIStyle lcdStyleRight = null;
		public static GUIStyle lcdStyleCenter = null;

		public static GUIStyle visualizerImage = null;
		public static GUIStyle visualizerImageBG = null;

		public static GUIStyle helpIcon = null;

		public static GUIStyle box = null;

		#endregion
		#region Textures

		// Button textures.
		public static Texture playTex = null;
		public static Texture stopTex = null;
		public static Texture pauseTex = null;
		public static Texture prevBeatTex = null;
		public static Texture nextBeatTex = null;
		public static Texture beatTex = null;

		#endregion
		#region Static Methods

		// This must be called!
		public static void InitSkin()
		{
			// Load in the GUIStyles.
			if (theSkin == null)
			{
				theSkin = EditorGUIUtility.Load("GUI/KoreographyEditorSkin.guiskin") as GUISkin;
				lcdStyleLeft = theSkin.GetStyle("LCDLeft");
				lcdStyleRight = theSkin.GetStyle("LCDRight");
				lcdStyleCenter = theSkin.GetStyle("LCDCenter");
				visualizerImage = theSkin.GetStyle("VisualizerImage");
				visualizerImageBG = theSkin.GetStyle("VisualizerImageBG");
				box = theSkin.box;

				// Default to Light Skin.
				InitLightSkinStyles();
			}
		
			// Skin-specific.
			if (EditorGUIUtility.isProSkin && !bDarkMode)
			{
				InitDarkSkinStyles();
			}
			else if (!EditorGUIUtility.isProSkin && bDarkMode)
			{
				InitLightSkinStyles();
			}
		}
	
		static void InitLightSkinStyles()
		{
			helpIcon = theSkin.GetStyle("LS_HelpIcon");

			playTex = EditorGUIUtility.Load("Textures/LS_Play.png") as Texture;
			stopTex = EditorGUIUtility.Load("Textures/LS_Stop.png") as Texture;
			pauseTex = EditorGUIUtility.Load("Textures/LS_Pause.png") as Texture;
			prevBeatTex = EditorGUIUtility.Load("Textures/LS_PrevBeat.png") as Texture;
			nextBeatTex = EditorGUIUtility.Load("Textures/LS_NextBeat.png") as Texture;
			beatTex = EditorGUIUtility.Load("Textures/LS_Beat.png") as Texture;
		
			bDarkMode = false;
		}
	
		static void InitDarkSkinStyles()
		{
			helpIcon = theSkin.GetStyle("DS_HelpIcon");

			playTex = EditorGUIUtility.Load("Textures/DS_Play.png") as Texture;
			stopTex = EditorGUIUtility.Load("Textures/DS_Stop.png") as Texture;
			pauseTex = EditorGUIUtility.Load("Textures/DS_Pause.png") as Texture;
			prevBeatTex = EditorGUIUtility.Load("Textures/DS_PrevBeat.png") as Texture;
			nextBeatTex = EditorGUIUtility.Load("Textures/DS_NextBeat.png") as Texture;
			beatTex = EditorGUIUtility.Load("Textures/DS_Beat.png") as Texture;
		
			bDarkMode = true;
		}

		#endregion
	}
}
