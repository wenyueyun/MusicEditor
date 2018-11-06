//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEditor;
using UnityEngine;

namespace SonicBloom.Koreo.EditorUI
{
	static class HelpSkin
	{
		static GUISkin theSkin = null;

		static bool bDarkMode = false;
		
		#region Styles

		public static GUIStyle titleTextStyle	= null;
		public static GUIStyle sbLogoTextStyle	= null;
		public static GUIStyle linkTextStyle	= null;
		public static GUIStyle basicTextStyle	= null;

		public static GUIStyle tableBGStyle		= null;
		public static GUIStyle keyFieldStyle	= null;
		public static GUIStyle descFieldStyle	= null;

		#endregion
		#region Textures

		public static Texture sbLogoTex = null;

		#endregion
		#region Static Methods

		// This must be called!
		public static void InitSkin()
		{
			// Load in the GUIStyles.
			if (theSkin == null)
			{
				theSkin			= EditorGUIUtility.Load("GUI/HelpSkin.guiskin") as GUISkin;
				linkTextStyle	= theSkin.GetStyle("LinkText");

				// Default to Light Skin.
				InitLightSkinStyles();
			}

			InitBuiltInBasedStyles();

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

		static void InitBuiltInBasedStyles()
		{
			// Basic Text is the same as the built-in label, except that wordWrap is turned on!
			basicTextStyle	= new GUIStyle(EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).label);
			basicTextStyle.wordWrap = true;
			basicTextStyle.richText = true;
		}
			
		static void InitLightSkinStyles()
		{
			sbLogoTex = EditorGUIUtility.Load("Textures/LS_SonicBloomLogo.png") as Texture;
			titleTextStyle	= theSkin.GetStyle("LS_TitleText");
			sbLogoTextStyle	= theSkin.GetStyle("LS_SBLogoText");
			tableBGStyle	= theSkin.GetStyle("LS_TableBG");
			keyFieldStyle	= theSkin.GetStyle("LS_KeyField");
			descFieldStyle	= theSkin.GetStyle("LS_DescField");
			
			bDarkMode = false;
		}
		
		static void InitDarkSkinStyles()
		{
			sbLogoTex = EditorGUIUtility.Load("Textures/DS_SonicBloomLogo.png") as Texture;
			titleTextStyle	= theSkin.GetStyle("DS_TitleText");
			sbLogoTextStyle	= theSkin.GetStyle("DS_SBLogoText");
			tableBGStyle	= theSkin.GetStyle("DS_TableBG");
			keyFieldStyle	= theSkin.GetStyle("DS_KeyField");
			descFieldStyle	= theSkin.GetStyle("DS_DescField");
			
			bDarkMode = true;
		}

		#endregion
	}

	internal class HelpPanel : EditorWindow
	{
		#region Static Fields

		static HelpPanel thePanel = null;

		static PlatformString[]	hotkeys	= {
			new PlatformString("A", "Toggles <b>Select</b> mode for mouse interactions with Koreography Events"),
			new PlatformString("S", "Toggles <b>Draw</b> mode for mouse interactions with Koreography Events"),
			new PlatformString("D", "Toggles <b>Clone</b> mode for mouse interactions with Koreography Events"),
			new PlatformString("Z", "Toggles <b>OneOff</b> Koreography Event generation"),
			new PlatformString("C", "Toggles <b>Span</b> Koreography Event generation"),
			new PlatformString("E or Enter or Return", "E or ↵ or ↩", "<b>Insert</b> new Koreography Event at playhead [during playback]"),
			new PlatformString("V", "Toggles the <b>Visualizer</b> window visibility"),
			new PlatformString("Esc", "⎋", "<b>Focus</b> Waveform Display if not already focused; if focused, <b>clear</b> the active event selection"),
			new PlatformString("Shift", "⇧", "Inverts the <b>Snap to Beat</b> setting for some operations when held"),
			new PlatformString("Alt", "⌥", "Enables <b>Audio Scrubbing</b> when moving the mouse over the Waveform Display"),
			new PlatformString("Space", "<b>Play/Pause</b> audio"),
			new PlatformString("Shift+\nSpace", "⇧Space", "<b>Stop</b> audio"),
			new PlatformString("Left Arrow or Right Arrow", "← or →", "<b>Move the playhead</b> back/forward one measure"),
			new PlatformString("Shift+\nLeft Arrow or Shift+Right Arrow", "⇧← or ⇧→", "<b>Move the playhead</b> back/forward one beat"),
			new PlatformString("Down Arrow or Up Arrow", "↓ or ↑", "Decrease/Increase <b>playback speed</b> by 0.1x"),
			new PlatformString("Shift+\nDown Arrow or Shift+Up Arrow", "⇧↓ or ⇧↑", "Decrease/Increase <b>playback speed</b> by 0.01x"),
			new PlatformString("Backspace or Del", "⌫ or ⌦", "<b>Delete</b> selected Event(s)"),
			new PlatformString("Ctrl+A", "⌘A", "<b>Select All</b> Events"),
			new PlatformString("Ctrl+X", "⌘X", "<b>Cut</b> selected Event(s) to clipboard"),
			new PlatformString("Ctrl+C", "⌘C", "<b>Copy</b> selected Event(s) to clipboard"),
			new PlatformString("Ctrl+V", "⌘V", "<b>Paste</b> Event(s) from clipboard"),
			new PlatformString("Ctrl+\nShift+V", "⇧⌘V", "<b>Paste earliest Payload</b> from clipboard into selected Event(s)"),
			new PlatformString("Ctrl+Z", "⌘Z", "<b>Undo</b>"),
			new PlatformString("Ctrl+Y", "⇧⌘Z", "<b>Redo</b>")
		};

		static GUIContent docLinkContent	= new GUIContent("Online Documentation");
		static GUIContent forumLinkContent	= new GUIContent("Community Forums");
		static GUIContent bugReportContent	= new GUIContent("Bug Report");
		static GUIContent featReqContent	= new GUIContent("Feature Request");
		static GUIContent patentContent		= new GUIContent("Koreographer is manufactured under U.S. Patent No. 9,286,383B1.");

		static float indentSpace = 20f;
		static float keyFieldWidth = 70f;
		
		#endregion
		#region Fields

		bool bMouseDown = false;
		Vector2 hotkeyScrollPos = Vector2.zero;
		
		#endregion
		#region Static Methods

		[MenuItem("Help/Koreographer Help", false, 714)]
		public static HelpPanel OpenWindow()
		{
			if (thePanel == null)
			{
				// Equivalent of ShowUtility and then "title" or "titleContent" (version-dependent).
				HelpPanel win = GetWindow<HelpPanel>(true, "Koreographer Help");
				
				Vector2 winSize = new Vector2(300f, 444f);
				win.maxSize = winSize;
				win.minSize = winSize;

				thePanel = win;
			}
			else
			{
				thePanel.Focus();
			}

			return thePanel;
		}
		
		#endregion
		#region Methods
		
		void OnEnable()
		{
			wantsMouseMove = true;

			// Script compilation resets static fields.  Make sure we're the only one.
			if (thePanel == null)
			{
				thePanel = this;
			}

			// Platform-specific settings.
			if (Application.platform == RuntimePlatform.OSXEditor)
			{
				HelpPanel.keyFieldWidth = 60f;		// Platform-appropriate width.
			}

			// Ensure access to GUIStyles.
			HelpSkin.InitSkin();
		}
		
		void OnGUI()
		{
			// Store mouse state for use across event types.
			if (!bMouseDown && Event.current.type == EventType.MouseDown)
			{
				bMouseDown = true;
			}
			else if (bMouseDown && Event.current.type == EventType.MouseUp)
			{
				bMouseDown = false;
			}

			EditorGUILayout.BeginHorizontal();
			{
				GUILayout.FlexibleSpace();
				GUILayout.Label("Thanks for using");
				GUILayout.FlexibleSpace();
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			{
				GUILayout.FlexibleSpace();
#if KOREO_NON_PRO
				GUILayout.Label("Koreographer", HelpSkin.titleTextStyle);
#else
				GUILayout.Label("Koreographer Pro", HelpSkin.titleTextStyle);
#endif
				GUILayout.FlexibleSpace();
			}
			EditorGUILayout.EndHorizontal();
#if KOREO_EDUCATIONAL || KOREO_TRIAL
			EditorGUILayout.BeginHorizontal();
			{
				GUILayout.FlexibleSpace();
#if KOREO_EDUCATIONAL
				GUILayout.Label("Student License", EditorStyles.boldLabel);
#elif KOREO_TRIAL
				GUILayout.Label("Trial License", EditorStyles.boldLabel);
#endif
				GUILayout.FlexibleSpace();
			}
			EditorGUILayout.EndHorizontal();
#endif
			EditorGUILayout.BeginHorizontal();
			{
//				Drawing.DrawLine(new Vector2(position.width/2f, 0f), new Vector2(position.width/2f, position.height), Color.red, 1f, false);
				GUILayout.FlexibleSpace();
				GUILayout.Space(16f);
				GUILayout.Label("by <b>SONIC</b>", HelpSkin.sbLogoTextStyle);
				GUI.DrawTexture(GUILayoutUtility.GetRect(27f, 32f), HelpSkin.sbLogoTex, ScaleMode.ScaleToFit);
				GUILayout.Label("<b>BLOOM</b>, LLC", HelpSkin.sbLogoTextStyle);
				GUILayout.FlexibleSpace();
			}
			EditorGUILayout.EndHorizontal();

			GUILayout.Space(12f);

			GUILayout.Label("<b>Koreographer Help:</b>", HelpSkin.basicTextStyle);

			EditorGUILayout.BeginHorizontal();
			{
				GUILayout.Space(HelpPanel.indentSpace);
				EditorGUILayout.BeginVertical();
				{
					GUILayout.Label("Need a hand with something?  Chek out our:");

					// Documentation.
					EditorGUILayout.BeginHorizontal();
					{
						GUILayout.Label("\t*");
						if (LinkTextField(HelpPanel.docLinkContent, 1f, 0f))
						{
							Application.OpenURL("http://www.koreographer.com/help/");
						}

						GUILayout.FlexibleSpace();
					}
					EditorGUILayout.EndHorizontal();

					// Forums.
					EditorGUILayout.BeginHorizontal();
					{
						GUILayout.Label("\t*");
						if (LinkTextField(HelpPanel.forumLinkContent, 1f, 0f))
						{
							Application.OpenURL("http://forum.koreographer.com/");
						}
						
						GUILayout.FlexibleSpace();
					}
					EditorGUILayout.EndHorizontal();

					GUILayout.Label("Encounter a problem?  Got a great idea for a workflow enhancement?  Send us a:", HelpSkin.basicTextStyle);

					// Bug.
					EditorGUILayout.BeginHorizontal();
					{
						GUILayout.Label("\t*");
						if (LinkTextField(HelpPanel.bugReportContent, 1f, 0f))
						{
							Application.OpenURL("mailto:koreographerhelpdesk@gmail.com?Subject=%5BBUG%5D%20brief_description_here&Body=Summary%3A%0AThis%20is%20where%20you%20would%20explain%20the%20problem%20you%20are%20encountering%20in%20our%20product.%20%20Ex.%20When%20you%20click%20the%20button%2C%20nothing%20happens.%0A%0AVersion%20Info%3A%0ALet%20us%20know%20which%20version%20of%20Unity%2C%20and%20which%20version%20of%20Koreographer%20you%20are%20using.%20%20Ex.%20Unity%2010.0%2C%20Koreographer%20V10.10.%0A%0AExpected%20Result%3A%0AThis%20is%20where%20you%20tell%20us%20what%20you%20think%20is%20supposed%20to%20happen.%20%20Ex.%20When%20the%20button%20is%20clicked%2C%20it%20should%20go%20to%20the%20help%20page%20instead%20of%20doing%20nothing.%0A%0ASteps%20to%20Reproduce%3A%0AThis%20is%20where%20you%20give%20us%20a%20step%20by%20step%20break%20down%20of%20how%20to%20reproduce%20what%20you%20encountered.%20%20Ex.%201.%20Open%20The%20Koreography%20Editor.%202.%20When%20it%20loads%2C%20left-click%20the%20%22question%20mark%22%20button%20in%20the%20upper%20left%20hand%20corner.%203.%20Observe%20result.%0A%0AOther%20Details%3A%0AFeel%20free%20to%20add%20any%20additional%20information%20you%20think%20will%20help%20to%20identify%20the%20issue.%20Include%20details%20on%20your%20computer%20%28OS%20and%20pertinent%20details%29%2C%20any%20special%20cases%20concerning%20your%20Unity%20build%20or%20project%2C%20and%20if%20you%20are%20using%20any%20custom%20scripts.");
						}
						
						GUILayout.FlexibleSpace();
					}
					EditorGUILayout.EndHorizontal();

					// Feature Request.
					EditorGUILayout.BeginHorizontal();
					{
						GUILayout.Label("\t*");
						if (LinkTextField(HelpPanel.featReqContent, 1f, 0f))
						{
							Application.OpenURL("mailto:koreographerhelpdesk@gmail.com?Subject=%5BFEATURE%5D%20brief_description_here&Body=Summary%3A%0ALet%20us%20know%20what%20kind%20of%20feature%20you%20are%20looking%20for%21%20%20Ex.%20It%20would%20be%20great%20if%2C%20when%20I%20pressed%20the%20%22P%22%20button%2C%20I%20would%20be%20told%20where%20the%20nearest%20public%20restroom%20is.%0A%0AUse%20Case%28s%29%3A%0AThis%20is%20how%20you%20can%20tell%20us%20what%20you%20want%20your%20feature%20request%20to%20do.%20To%20do%20this%2C%20we%20ask%20that%20you%20follow%20a%20very%20specific%20%28i.e.%20scrumtastic%29%20method%20of%20writing%20it.%20It%20follows%20something%20like%20this%3A%20As%20a%20%3Cuser%20type%3E%2C%20I%20would%20like%20%3Cproblem%3E%2C%20so%20%3Cresult%3E.%20An%20example%20would%20be%3A%20As%20an%20exhausted%20engineer%2C%20I%20would%20like%20to%20know%20where%20the%20nearest%20public%20rest%20room%20is%2C%20so%20that%20I%20know%20where%20to%20wash%20my%20face%20when%20I%27m%20not%20near%20my%20computer.%0A%0AYou%20can%20add%20to%20this%20to%20make%20it%20more%20granular.%20%20Ex.%20As%20an%20exhausted%20engineer%2C%20I%20would%20like%20male%2C%20female%2C%20and%20unisex%20bathrooms%20to%20be%20identified%2C%20so%20I%20don%27t%20end%20up%20going%20to%20the%20wrong%20bathroom.%0A%0AOther%20Details%3A%0AInclude%20any%20additional%20information%20that%20might%20help%20us%20understand%20the%20feature%20you%20want.%20Feel%20free%20to%20include%20diagrams%2C%20details%20about%20how%20you%20use%20Koreographer%2C%20or%20napkin%20sketches%20to%20illustrate%20your%20problem.");
						}
						
						GUILayout.FlexibleSpace();
					}
					EditorGUILayout.EndHorizontal();
				}
				EditorGUILayout.EndHorizontal();
			}
			EditorGUILayout.EndHorizontal();

			GUILayout.Label("<b>Legal</b>", HelpSkin.basicTextStyle);
			EditorGUILayout.BeginHorizontal();
			{
				GUILayout.Space(HelpPanel.indentSpace);
				GUILayout.Label(patentContent, HelpSkin.basicTextStyle);
			}
			EditorGUILayout.EndHorizontal();

			GUILayout.Label("<b>Koreography Editor Hotkeys</b>", HelpSkin.basicTextStyle);
			hotkeyScrollPos = EditorGUILayout.BeginScrollView(hotkeyScrollPos, false, true, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, HelpSkin.tableBGStyle);
			{
				float winWidth = position.width - (
					(HelpSkin.keyFieldStyle.padding.horizontal + HelpSkin.keyFieldStyle.margin.horizontal) + 
					(HelpSkin.descFieldStyle.padding.horizontal + HelpSkin.descFieldStyle.margin.horizontal) +
					 HelpSkin.tableBGStyle.margin.horizontal);
				float usageFieldWidth = winWidth - HelpPanel.keyFieldWidth;

				foreach (PlatformString hotkey in hotkeys)
				{
					EditorGUILayout.BeginHorizontal();
					{
						GUILayout.Label(hotkey.Command, HelpSkin.keyFieldStyle, GUILayout.MaxWidth(keyFieldWidth));
						GUILayout.Label(hotkey.Description, HelpSkin.descFieldStyle, GUILayout.Width(usageFieldWidth));
					}
					EditorGUILayout.EndHorizontal();
				}
			}
			EditorGUILayout.EndScrollView();
		}

		bool LinkTextField(GUIContent content, float spacesBefore, float spacesAfter)
		{
			float spaceWidth = 3f;

			GUILayout.Space(spacesBefore * spaceWidth);

			Rect buttonRect = GUILayoutUtility.GetRect(content, HelpSkin.linkTextStyle);
			EditorGUIUtility.AddCursorRect(buttonRect, MouseCursor.Link);
			
			if ((Event.current.type == EventType.Repaint || Event.current.isMouse) && buttonRect.Contains(Event.current.mousePosition))
			{
				Color textColor = bMouseDown ? HelpSkin.linkTextStyle.active.textColor : HelpSkin.linkTextStyle.normal.textColor;

				float paddingOffset = -HelpSkin.linkTextStyle.padding.left;	// Same for right and left.  Negative to switch the sign.

				Drawing.DrawLine(new Vector2(buttonRect.xMin - paddingOffset, buttonRect.yMax - 2f),
				                 new Vector2(buttonRect.xMax + paddingOffset, buttonRect.yMax - 2f), textColor, 1f, false);
				Repaint();
			}

			bool bDidClick = GUI.Button(buttonRect, content, HelpSkin.linkTextStyle);

			GUILayout.Space(spacesAfter * spaceWidth);

			return bDidClick;
		}
		
		#endregion
	}

	internal class PlatformString
	{
		#region Fields

		GUIContent keyCommand = new GUIContent();
		GUIContent usage = new GUIContent();

		#endregion
		#region Constructors

		// Private default constructor.
		PlatformString(){}

		public PlatformString(string command, string desc)
		{
			keyCommand.text = command;
			usage.text = desc;
		}

		public PlatformString(string winCommand, string macCommand, string desc)
		{
			if (Application.platform == RuntimePlatform.OSXEditor)
			{
				keyCommand.text = macCommand;
			}
			else
			{
				keyCommand.text = winCommand;
			}

			usage.text = desc;
		}

		#endregion
		#region Properties

		public GUIContent Command
		{
			get
			{
				return keyCommand;
			}
		}

		public GUIContent Description
		{
			get
			{
				return usage;
			}
		}

		#endregion
	}
}
