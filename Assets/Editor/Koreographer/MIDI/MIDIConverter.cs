//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

using SonicBloom.MIDI;
using SonicBloom.MIDI.Objects;

namespace SonicBloom.Koreo.EditorUI
{
	static class MIDIImporterSkin
	{
		static GUISkin theSkin = null;

		static bool bDarkMode = false;

		// Styles.
		public static GUIStyle rowDarkBGStyle	= null;
		public static GUIStyle rowLightBGStyle	= null;
		public static GUIStyle titleStyle		= null;
		public static GUIStyle indentAreaStyle	= null;
		public static GUIStyle borderAreaStyle	= null;

		// Must be called!
		public static void InitSkin()
		{
			// Load in the GUIStyles.
			if (theSkin == null)
			{
				theSkin = EditorGUIUtility.Load("Koreographer/GUI/MIDIImporterSkin.guiskin") as GUISkin;

				titleStyle =		theSkin.GetStyle("SectionTitle");
				indentAreaStyle =	theSkin.GetStyle("IndentArea");
			
				// Default to light skin.
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
			rowDarkBGStyle =	theSkin.GetStyle("LS_RowElementDark");
			rowLightBGStyle =	theSkin.GetStyle("LS_RowElementLight");
			borderAreaStyle =	theSkin.GetStyle("LS_BorderArea");

			bDarkMode = false;
		}

		static void InitDarkSkinStyles()
		{
			rowDarkBGStyle =	theSkin.GetStyle("DS_RowElementDark");
			rowLightBGStyle =	theSkin.GetStyle("DS_RowElementLight");
			borderAreaStyle =	theSkin.GetStyle("DS_BorderArea");
		
			bDarkMode = true;
		}
	}

	internal class MIDIConverter : EditorWindow
	{
		enum ContentTab
		{
			KoreographyExport,
			KoreographyTrackExport,
		}

		#region Static Fields

		static string FirstRunPref	= "MIDIConverter_FirstRun";
		static string MIDIPathPref	= "MIDIConverter_AssetPath";

		static string wrongTrackTypeContent		= "A Koreography Track with the specified Event ID exists but it is a type that the MIDI Converter cannot use. Only Koreography Tracks that support the Int, Float, and Text Payload types can be used with the MIDI Converter.";

		static GUIContent helpContent			= new GUIContent("", "Koreographer Help");
		static GUIContent openMIDIContent		= new GUIContent("Open MIDI File", "Select a MIDI file to load for importation.");
		static GUIContent fileContent			= new GUIContent("Current File", "The name of the currently loaded MIDI file.");
		static GUIContent fileNameContent		= new GUIContent("", "");	// Filled in at runtime.
		static GUIContent clipContent			= new GUIContent("Audio Clip", "The AudioClip that will be assinged to the exported Koreography Data.");
		static GUIContent exportKoreoLabel		= new GUIContent("Export Koreography", "Export the main Koreography data with the selected AudioClip and tempo Settings.");
		static GUIContent koreoLabel			= new GUIContent("Koreography", "The target Koreography into which to export new Koreoraphy Track data.");
		static GUIContent eventIDLabel			= new GUIContent("Event ID", "The Event ID of the Koreography Track asset to target for Payload creation. If a new Koreography Track is created, it will receive this Event ID.");
		static GUIContent startOffsetLabel		= new GUIContent("Start Offset", "The authored start offset of the music in seconds (e.g. the non-MIDI audio file was exported with 0.5s of silence at the beginning).");
		static GUIContent exportTrackLabel		= new GUIContent("Export New Track", "Create a new Koreography Track, add it to the selected Koreography, and export to it with the current selections and settings.");
		static GUIContent overwriteTrackLabel	= new GUIContent("Overwrite Events in Track", "Overwrite all Koreography Events in the Koreography Track indicated by the Event ID with the current selections and settings.");
		static GUIContent appendTrackLabel		= new GUIContent("Append to Track", "Append to the Koreography Track indicated by the Event ID with the current selections and settings.");

		static List<System.Type> SupportedTrackTypes = new List<System.Type>();

		static float FixedWinWidth	= 600f;
		static float MinWinHeight	= 267f;

		#endregion
		#region Fields

		string curMIDIPath = string.Empty;	// The MIDI Path of the asset currently loaded.

		Song parsedSong = null;
		List<TrackContainer> tracks = null;
		List<TempoEntry> tempos = null;

		Vector2 tempoScrollPos = Vector2.zero;
		Vector2 trackScrollPos = Vector2.zero;

		// Fields for Koreography export.
		AudioClip authoredClip = null;
		Koreography targetKoreo = null;

		// Fields for Koreography Track export.
		[EventID]
		[SerializeField]
		string eventID = string.Empty;		// Event ID for track to export.
		float startOffset = 0f;				// Time to offset tracks.

		// Property versions of fields used for UI.  Set these in ShowWindow().
		SerializedObject thisObj = null;
		SerializedProperty eventIDProp = null;

		ContentTab selectedTab = ContentTab.KoreographyExport;

		// Layout stuff.
		float scrollViewStartY = 0f;		// Used to store the start y position of the scroll views (has to be calculated on Repaint, not Layout OnGUI pass).
		float preScrollSpacer = 6f;			// Space used to separate the tabs from the beginning of the scroll view.
		Rect newTrackButtRect;				// Used for positioning a potential dropdown menu (if needed).

		#endregion
		#region Static Properties

		/// <summary>
		/// Gets or sets a valid version of the last path used from the EditorPrefs system.  This
		/// is a path to a directory, not a file.
		/// </summary>
		/// <value>The stored MIDI path.</value>
		private static string MIDIPath
		{
			get
			{
				// Grab the path out of the EditorPrefs system.
				string path = EditorPrefs.GetString(MIDIConverter.MIDIPathPref, string.Empty);
			
				// If we have an actual value we need to verify it.
				if (!string.IsNullOrEmpty(path))
				{
					// Check that the directory exists at the location specified.
					if (!Directory.Exists(path))
					{
						// If the directory didn't exist, set to empty.
						path = string.Empty;
					}
				}
			
				return path;
			}
			set
			{
				string path = value;
				
				// Remove the file name.  We only want to store the directory.
				if (File.Exists(path))
				{
					path = Path.GetDirectoryName(path);
				}

				// Store the path in the EditorPrefs system.
				EditorPrefs.SetString(MIDIConverter.MIDIPathPref, path);
			}
		}
	
		#endregion
		#region Properties

		bool IsContentSelected
		{
			get
			{
				bool bSelected = false;
				if (tracks != null)
				{
					foreach (TrackContainer cont in tracks)
					{
						if (cont.HasSelectedContent)
						{
							bSelected = true;
							break;
						}
					}
				}
				return bSelected;
			}
		}

		#endregion
		#region Static Methods

		// Use custom priority to reduce likelihood of collision with other assets.
		[MenuItem("Window/Koreographer MIDI Converter &#k", false, 715)]
		static void ShowWindow()
		{
			MIDIConverter win = EditorWindow.GetWindow<MIDIConverter>(false, "MIDI Converter");

			// First run?
			if (!EditorPrefs.HasKey(MIDIConverter.FirstRunPref) ||
				EditorPrefs.GetBool(MIDIConverter.FirstRunPref))
			{
				// Adjust the size of the window to show the entire view.
				win.position = new Rect(50f, 50f, MIDIConverter.FixedWinWidth, MIDIConverter.MinWinHeight);
			
				// Store the fact that we've had a first-run!
				EditorPrefs.SetBool(MIDIConverter.FirstRunPref, false);
			}

			// Restrict window sizing to fit with window design constraints.
			win.minSize = new Vector2(MIDIConverter.FixedWinWidth, MIDIConverter.MinWinHeight);
			win.maxSize = new Vector2(MIDIConverter.FixedWinWidth, win.maxSize.y);
		}

		#endregion
		#region Methods

		void OnEnable()
		{
			MIDIImporterSkin.InitSkin();
			KoreographyEditorSkin.InitSkin();	// This is for the Help Button.  TODO: Move Help Button into "shared" skin.

			if (!string.IsNullOrEmpty(curMIDIPath))
			{
				// We have a path to a file.  If the TrackContainer list is
				//  valid, then restore.  Otherwise build from scratch.
				// This was likely a compile process or Play in Editor run.
				if (tracks != null)
				{
					RestoreSongFromFileAtPath(curMIDIPath);
				}
				else
				{
					InitializeSongFromFileAtPath(curMIDIPath);
				}
			}
			
			// Grab the properties if they don't exist yet!
			if (thisObj == null)
			{
				thisObj = new SerializedObject(this);
				eventIDProp = thisObj.FindProperty("eventID");
			}

			// Ensure that the SupportedTrackTypes list is valid.
			if (MIDIConverter.SupportedTrackTypes.Count == 0)
			{
				List<System.Type> trackTypes = KoreographyTrackTypeUtils.EditableTrackTypes;
				Dictionary<System.Type, List<System.Type>> trPlMap = KoreographyTrackTypeUtils.EditableTrackPayloadTypes;

				foreach (System.Type trType in trackTypes)
				{
					List<System.Type> plTypes = trPlMap[trType];

					// Check if Track type supports the Payload types the MIDI Converter requires. 
					if (plTypes.Contains(typeof(FloatPayload)) &&
					    plTypes.Contains(typeof(IntPayload)) &&
					    plTypes.Contains(typeof(TextPayload)))
					{
						MIDIConverter.SupportedTrackTypes.Add(trType);
					}
				}
			}
		}

		void RestoreSongFromFileAtPath(string path)
		{
			parsedSong = SongBuilder.GetSong(path);
		
			if (!RestoreTracks())
			{
				// If restoration doesn't work, fallback
				//  on full reinitialization!
				InitializeSongFromFileAtPath(path);
			}
		}

		bool RestoreTracks()
		{
			bool bRestored = true;

			List<Track> midiTracks = parsedSong.Tracks;

			if (tracks == null)
			{
				Debug.LogError("MIDIConverter::RestoreTracks() - No valid TrackContainer list.  Something's very wrong!");
				bRestored = false;
			}
			else if (midiTracks.Count != tracks.Count)
			{
				Debug.LogError("MIDIConverter::RestoreTracks() - TrackContainer count doesn't match Track count.  Something's very wrong!");
				bRestored = false;
			}
			else
			{
				for (int i = 0; i < tracks.Count; ++i)
				{
					if (!tracks[i].RestoreTrack(midiTracks[i]))
					{
						bRestored = false;
						break;
					}
				}
			}

			return bRestored;
		}

		void InitializeSongFromFileAtPath(string path)
		{
			parsedSong = SongBuilder.GetSong(path);
		
			PrepareTracks();
			PrepareTempoSections();
		}

		void PrepareTracks()
		{
			tracks = new List<TrackContainer>();

			List<Track> midiTracks = parsedSong.Tracks;

			// Don't use a foreach loop to ensure that this matches with RestoreTracks.
			for (int i = 0; i < midiTracks.Count; ++i)
			{
				tracks.Add(new TrackContainer(midiTracks[i]));
			}
		}

		void PrepareTempoSections()
		{
			tempos = new List<TempoEntry>();
		
			// Tempo Map!
			if (parsedSong.HasTempoMap())
			{
				List<MIDITimedMessage<MIDITempo>> tempoMap = parsedSong.TempoMap;
			
				for (int i = 0; i < tempoMap.Count; ++i)
				{
					MIDITimedMessage<MIDITempo> tempo = tempoMap[i];
				
					TempoEntry def = new TempoEntry();

					// Set information.
					def.SetInfoFromTempoAndTimeSig(tempo.msg, parsedSong.GetTimeSignatureAtTime(tempo.time).msg);
					def.startTimeInSec = tempo.timeInSec;
				
					tempos.Add(def);
				}
			}
		
			// TimeSignature Map!
			if (parsedSong.HasTimeSignatureMap())
			{
				List<MIDITimedMessage<MIDITimeSignature>> timeSigMap = parsedSong.TimeSignatureMap;
			
				// Time Signature changes may have been taken care of in the TempoMap
				//  processing above.  Only add new sections if a tempo section with
				//  the same time as the TimeSignature change doesn't already exist.
				for (int i = 0; i < timeSigMap.Count; ++i)
				{
					MIDITimedMessage<MIDITimeSignature> timeSig = timeSigMap[i];

					if (!tempos.Exists(x => x.startTimeInSec == timeSig.timeInSec))
					{
						// Tempo didn't previously exist.  Build from default.
						TempoEntry def = new TempoEntry();

						// Set information
						def.SetInfoFromTempoAndTimeSig(parsedSong.GetTempoAtTime(timeSig.time).msg, timeSig.msg);
						def.startTimeInSec = timeSig.timeInSec;

						// We may be *inserting* into the list, not necessarily adding to.
						int locOfPrior = tempos.FindLastIndex(x => x.startTimeInSec < def.startTimeInSec);
						tempos.Insert(locOfPrior + 1, def);
					}
				}
			}
		}
	
		void OnGUI()
		{
			// MIDI File
			{
				EditorGUILayout.BeginHorizontal();
				{
					EditorGUILayout.LabelField("MIDI File Settings", MIDIImporterSkin.titleStyle, GUILayout.Width(200f));
					GUILayout.FlexibleSpace();

					// Help Button (?).
					if (GUILayout.Button(MIDIConverter.helpContent, KoreographyEditorSkin.helpIcon, GUILayout.MaxWidth(20f)))
					{
						HelpPanel.OpenWindow();
					}
				}
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.BeginVertical(MIDIImporterSkin.indentAreaStyle);
				{
					// Open MIDI File.
					EditorGUILayout.BeginHorizontal();
					{
						if (GUILayout.Button(openMIDIContent, GUILayout.MaxWidth(100f)))
						{
							// Get an asset path and massage it to work with the loading mechanisms.
							string path = EditorUtility.OpenFilePanel("Select a MIDI file...", MIDIConverter.MIDIPath, "mid");
							if (!string.IsNullOrEmpty(path))
							{
								MIDIConverter.MIDIPath = path;	// Store valid path.
								curMIDIPath = path;				// Set the current MIDI Path.

								InitializeSongFromFileAtPath(curMIDIPath);
							}
						}

						GUILayout.Space(15f);

						EditorGUILayout.LabelField(fileContent, EditorStyles.boldLabel, GUILayout.Width(80f));
						if (string.IsNullOrEmpty(curMIDIPath))
						{
							fileNameContent.text = "[none]";
							fileNameContent.tooltip = string.Empty;
						}
						else
						{
							fileNameContent.text = Path.GetFileName(curMIDIPath);
							fileNameContent.tooltip = curMIDIPath;
						}
						EditorGUILayout.LabelField(fileNameContent);
					}
					EditorGUILayout.EndHorizontal();

					// File Start Offset.
					{
						EditorGUIUtility.labelWidth = 120f;
						startOffset = EditorGUILayout.FloatField(startOffsetLabel, startOffset, GUILayout.Width(220f));
						EditorGUIUtility.labelWidth = 0f;
					}
				}
				EditorGUILayout.EndVertical();
			}

			GUIContent koreographyTabContent = new GUIContent("Koreography Export", "Export Koreography in this view.");
			GUIContent koreographyTrackTabContent = new GUIContent("Koreography Track Export", "Export Koreography Tracks in this view.");

			EditorGUILayout.BeginHorizontal();
			{
				GUILayout.FlexibleSpace();

				if (GUILayout.Toggle(selectedTab == ContentTab.KoreographyExport, koreographyTabContent, EditorStyles.miniButtonLeft, GUILayout.Width(160f), GUILayout.Height(20f)))
				{
					selectedTab = ContentTab.KoreographyExport;
				}
				if (GUILayout.Toggle(selectedTab == ContentTab.KoreographyTrackExport, koreographyTrackTabContent, EditorStyles.miniButtonRight, GUILayout.Width(160f), GUILayout.Height(20f)))
				{
					selectedTab = ContentTab.KoreographyTrackExport;
				}

				GUILayout.FlexibleSpace();
			}
			EditorGUILayout.EndHorizontal();

			// Find the height of where we are now (must be specially handled).
			CheckUpdateScrollStartPos();

			// Add a little spacing before the following content.
			GUILayout.Space(preScrollSpacer);

			if (selectedTab == ContentTab.KoreographyExport)
			{
				// Koreography Export
				DoKoreographyGUI();
			}
			else //if (selectedTab == ContentTab.KoreographyTrackExport)
			{
				// Koreography Track Export
				DoKoreographyTrackGUI();
			}
		}

		void DoKoreographyGUI()
		{
			EditorGUI.BeginDisabledGroup(parsedSong == null);
			{
				EditorGUILayout.BeginHorizontal(MIDIImporterSkin.indentAreaStyle);
				{
					float height = GetScrollViewHeight();

					// Tempo Map contents.
					tempoScrollPos = EditorGUILayout.BeginScrollView(tempoScrollPos, MIDIImporterSkin.borderAreaStyle, GUILayout.Width(275f), GUILayout.Height(height));
					{
						if (tempos != null)
						{
							TempoEntry.bDrawLight = true;	// Ensure first entry is always light.
							foreach (TempoEntry entry in tempos)
							{
								entry.OnGUI();
								TempoEntry.bDrawLight = !TempoEntry.bDrawLight;
							}
						}
					}
					EditorGUILayout.EndScrollView();
				
					// This ensures that Koreography Settings below are lined up with Koreography Track Settings.
					GUILayout.Space(65f);
				
					// Koreography Settings.
					EditorGUILayout.BeginVertical();
					{
						// Audio Clip.
						float originalLabelW = EditorGUIUtility.labelWidth;
						EditorGUIUtility.labelWidth = 70f;
						AudioClip newClip = EditorGUILayout.ObjectField(clipContent, authoredClip, typeof(AudioClip), false, GUILayout.Width(220f)) as AudioClip;
						EditorGUIUtility.labelWidth = originalLabelW;
						if (newClip != null && KoreographyEditor.CheckAudioClipValidity(newClip)) // Check that we have a valid clip for the KoreographyEditor.
						{
							authoredClip = newClip;
						}
					
						// Export Koreography button.
						EditorGUILayout.BeginHorizontal(GUILayout.Width(220f));
						{
							GUILayout.FlexibleSpace();
						
							EditorGUI.BeginDisabledGroup(parsedSong == null || authoredClip == null);
							{
								if (GUILayout.Button(exportKoreoLabel, GUILayout.Width(150f)))
								{
									// Grab the new Koreography Track!
									Koreography newKoreo = KoreographyEditor.CreateNewKoreography();
									if (newKoreo != null)
									{
										targetKoreo = newKoreo;
										targetKoreo.SourceClip = authoredClip;
										targetKoreo.SampleRate = authoredClip.frequency;

										List<TempoSectionDef> sectionMap = new List<TempoSectionDef>();
									
										// Convert all tempo entries into TempoSectionDefs and then add them!
										for (int i = 0; i < tempos.Count; ++i)
										{
											sectionMap.Add(tempos[i].GetDefForEntry(authoredClip.frequency));
										}
									
										// Handle offsetting of the tempo entries.
										if (startOffset > 0f)
										{
											int startOffsetInSamples = (int)(startOffset * (float)authoredClip.frequency);
										
											// There may be no tempo sections but we still need to offset.
											//  Create one at the zero point to offset in the following foreach.
											if (sectionMap.Count <= 0)
											{
												// This ensures setting consistency with default MIDI tempo settings,
												//  including section "name".
												sectionMap.Add(new TempoEntry().GetDefForEntry(authoredClip.frequency));
											}
										
											// Offset sections.
											foreach (TempoSectionDef def in sectionMap)
											{
												def.StartSample += startOffsetInSamples;
											}
										
											// Add new start offset definition. It will be sorted in OverwriteTempoSections() below.
											TempoSectionDef startDef = new TempoSectionDef();
											startDef.SectionName = "Spacer";
										
											sectionMap.Add(startDef);
										}
									
										if (sectionMap.Count > 0)
										{
											// Handles order verification.
											targetKoreo.OverwriteTempoSections(sectionMap);

											// If there's a start offset, make sure that the first non-spacer Tempo Section
											//  resets the measure count.  We do this here because the Tempo Sections have
											//  been properly sorted by their start sample positions.
											if (startOffset > 0 && targetKoreo.GetNumTempoSections() > 1)
											{
												// Grab the first non-spacer Tempo Section and set it to reset the measure
												//  count.
												targetKoreo.GetTempoSectionAtIndex(1).DoesStartNewMeasure = true;
											}
										}
									
										EditorUtility.SetDirty(targetKoreo);
									}
								}
							}
							EditorGUI.EndDisabledGroup();
						
							GUILayout.FlexibleSpace();
						}
						EditorGUILayout.EndHorizontal();
					}
					EditorGUILayout.EndVertical();
				}
				EditorGUILayout.EndHorizontal();
			}
			EditorGUI.EndDisabledGroup();
		}

		void DoKoreographyTrackGUI()
		{
			EditorGUI.BeginDisabledGroup(parsedSong == null);
			{
				// Wrap a scrollview in a vertical.  Seems to work better?
				EditorGUILayout.BeginHorizontal(MIDIImporterSkin.indentAreaStyle);
				{
					float height = GetScrollViewHeight();

					// Tempo Map contents.
					trackScrollPos = EditorGUILayout.BeginScrollView(trackScrollPos, MIDIImporterSkin.borderAreaStyle, GUILayout.MinWidth(340f), GUILayout.Height(height));
					{
						if (tracks != null)
						{
							TrackContainer.bDrawLight = true;
							foreach (TrackContainer cont in tracks)
							{
								cont.OnGUI();
								TrackContainer.bDrawLight = !TrackContainer.bDrawLight;
							}
						}
					}
					EditorGUILayout.EndScrollView();
				
					// Koreography Track Settings.
					EditorGUILayout.BeginVertical();
					{
						// Export settings.
						EditorGUIUtility.labelWidth = 80f;
						targetKoreo = EditorGUILayout.ObjectField(koreoLabel, targetKoreo, typeof(Koreography), false, GUILayout.Width(220f)) as Koreography;

						EditorGUI.BeginChangeCheck();
						EditorGUILayout.PropertyField(eventIDProp, eventIDLabel, GUILayout.Width(220f));
						if(EditorGUI.EndChangeCheck())
						{
							// Skip worrying about ApplyModifiedProperties.  Simply detect if the SerializedProperty
							//  version changed and update the local object version.  ApplyModifiedProperties
							//  automatically adds an Undo field.  There is a version that doesn't do this in Unity
							//  but it is marked as internal until Unity 5.2.
							eventID = eventIDProp.stringValue;
						}
					
						EditorGUIUtility.labelWidth = 0f;

						EditorGUI.BeginDisabledGroup(!IsContentSelected || string.IsNullOrEmpty(eventID) || targetKoreo == null);
						{
							KoreographyTrackBase targetTrack = (targetKoreo == null) ? null : targetKoreo.GetTrackByID(eventID) as KoreographyTrackBase;
							
							// Verify that the track is of a valid type.
							bool bTrackIsValidType = (targetTrack != null) && MIDIConverter.SupportedTrackTypes.Contains(targetTrack.GetType());
							
							// Show a warning about the track if it exists but isn't a valid type...
							if (targetTrack != null && !bTrackIsValidType)
							{
								// The Horizontal Group helps bound the width of the Help Box.
								EditorGUILayout.BeginHorizontal(GUILayout.Width(220f));
								{
									EditorGUILayout.HelpBox(wrongTrackTypeContent, MessageType.Warning);
								}
								EditorGUILayout.EndHorizontal();
							}

							EditorGUILayout.BeginHorizontal(GUILayout.Width(220f));
							{
								GUILayout.FlexibleSpace();

								EditorGUILayout.BeginVertical();
								{
									// A valid type must exist.
									if (bTrackIsValidType)
									{
										// Overwrite Events in Track option.
										if (GUILayout.Button(overwriteTrackLabel, GUILayout.Width(170f)))
										{
											if (targetTrack != null)
											{
												Undo.RecordObject(targetTrack, "Replace MIDI Events");

												// Clear out old ones
												targetTrack.RemoveAllEvents();

												// Replace with new ones.
												foreach (TrackContainer cont in tracks)
												{
													cont.AddEventsToKoreographyTrack(targetTrack, parsedSong, targetKoreo.SampleRate, startOffset);
												}
												
												EditorUtility.SetDirty(targetTrack);
											}
										}
									}
									else
									{
										// Export New Track option.
										if (GUILayout.Button(exportTrackLabel, GUILayout.Width(170f)))
										{
											KoreographerGUIUtils.ShowTypeSelectorMenu(newTrackButtRect, MIDIConverter.SupportedTrackTypes, OnNewTrackOptionSelected);
										}

										// Store the location of the above button. We can only get into its block with a
										//  mouse or keyboard event.
										if (Event.current.type == EventType.Repaint)
										{
											newTrackButtRect = GUILayoutUtility.GetLastRect();
										}
									}
									EditorGUI.BeginDisabledGroup(!bTrackIsValidType);
									{
										if (GUILayout.Button(appendTrackLabel, GUILayout.Width(170f)))
										{
											KoreographyTrackBase oldKoreoTrack = targetKoreo.GetTrackByID(eventID) as KoreographyTrackBase;

											if (oldKoreoTrack != null)
											{
												Undo.RecordObject(oldKoreoTrack, "Append MIDI Events");

												foreach (TrackContainer cont in tracks)
												{
													cont.AddEventsToKoreographyTrack(oldKoreoTrack, parsedSong, targetKoreo.SampleRate, startOffset);
												}

												EditorUtility.SetDirty(oldKoreoTrack);
											}
										}
									}
									EditorGUI.EndDisabledGroup();
								}
								EditorGUILayout.EndVertical();
						
								GUILayout.FlexibleSpace();
							}
							EditorGUILayout.EndHorizontal();
						}
						EditorGUI.EndDisabledGroup();
					}
					EditorGUILayout.EndVertical();
				}
				EditorGUILayout.EndHorizontal();
			}
			EditorGUI.EndDisabledGroup();
		}

		/// <summary>
		/// Calculates the size of the scroll view based on current window size, start position, and
		///  some spacing rules.  This is currently shared across multiple views.  Break it up if UI
		///  layouts change!
		/// </summary>
		/// <returns>The height of flexible scroll views.</returns>
		float GetScrollViewHeight()
		{
			// Height of the scroll view.  For whatever reason, 152 is the number that allows for four rows without a scroll-bar.
			return Mathf.Max(position.height - (scrollViewStartY + preScrollSpacer + MIDIImporterSkin.indentAreaStyle.margin.left), 152f);
		}

		/// <summary>
		/// Sets, if necessary, the start y position of the scroll views.  This must
		///  be calculated in the Repaint OnGUI flow because GetLastRect doesn't
		///  return usable values in Layout OnGUI.  Annoyingly, the sizing values
		///  appear to only be cared about on the Layout OnGUI pass.
		/// </summary>
		void CheckUpdateScrollStartPos()
		{
			if (scrollViewStartY == 0f && Event.current.type == EventType.Repaint)
			{
				scrollViewStartY = GUILayoutUtility.GetLastRect().yMax;
				Repaint();	// Paint to the screen with the current value.
			}
		}

		#endregion
		#region Callback Handlers

		void OnNewTrackOptionSelected(object type)
		{
			// Grab the new Koreography Track!
			KoreographyTrackBase newKoreoTrack = KoreographyEditor.CreateNewKoreographyTrack((System.Type)type);

			if (newKoreoTrack != null)
			{
				newKoreoTrack.EventID = eventID;
				
				foreach (TrackContainer cont in tracks)
				{
					cont.AddEventsToKoreographyTrack(newKoreoTrack, parsedSong, targetKoreo.SampleRate, startOffset);
				}
				
				// Replace any tracks with the same ID.
				targetKoreo.RemoveTrack(eventID);
				targetKoreo.AddTrack(newKoreoTrack);
			}
		}

		#endregion
	}

	[System.Serializable]
	class TrackContainer
	{
		#region Static Fields

		public static bool bDrawLight = true;

		static GUIContent trackContent			= new GUIContent("Track", "The name of the MIDI Track.");
		static GUIContent trackNameContent		= new GUIContent("", "");
		static GUIContent instrumentContent		= new GUIContent("Instrument", "The name of the instrument assigned to this MIDI Track.");
		static GUIContent instrumentNameContent	= new GUIContent("", "");
		static GUIContent selectedChanContent	= new GUIContent("", "Checked if one or more channels are selected for export.");
		static GUIContent lyricContent			= new GUIContent("Lyrics", "The lyrics detected in this MIDI Track.");
		static GUIContent selectedLyricContent	= new GUIContent("Selected", "Whether or not the Lyric content found in this MIDI Track will be included in the exported Koreography Track.");
		static GUIContent channelContent		= new GUIContent("Channels", "Show/hide channels found in this MIDI Track.");

		#endregion
		#region Fields

		public Track track;

		[SerializeField]
		bool bLyricsSelected = false;

		List<ChannelContainer> channels;
		bool bShowChannels = false;

		#endregion
		#region Properties

		public bool HasSelectedLyrics
		{
			get
			{
				return bLyricsSelected;
			}
		}

		public bool HasSelectedChannel
		{
			get
			{
				bool bSelected = false;
				foreach (ChannelContainer cont in channels)
				{
					if (cont.bSelected)
					{
						bSelected = true;
						break;
					}
				}
				return bSelected;
			}
		}
		
		public bool HasSelectedContent
		{
			get
			{
				return HasSelectedLyrics || HasSelectedChannel;
			}
		}

		#endregion
		#region Methods

		public TrackContainer(Track tr)
		{
			track = tr;

			PrepareChannels();
		}

		/// <summary>
		/// Restores the track reference, reconnecting all internal Channel
		///  references.
		/// </summary>
		/// <returns><c>true</c>, if the track was restored, <c>false</c> otherwise.</returns>
		/// <param name="tr">The track to restore.</param>
		public bool RestoreTrack(Track tr)
		{
			bool bRestored = true;

			track = tr;

			Dictionary<int, Channel> midiChannels = track.Channels;

			if (channels == null)
			{
				Debug.LogError("TrackContainer::RestoreTrack() - No valid ChannelContainer list.  Something's very wrong!");
				bRestored = false;
			}
			else if (midiChannels.Count != channels.Count)
			{
				Debug.LogError("TrackContainer::RestoreTrack() - ChannelContainer count doesn't match Channel count.  Something's very wrong!");
				bRestored = false;
			}
			else
			{
				foreach (ChannelContainer cont in channels)
				{
					// Subtract one from the Channel Number because we don't store the index, but the "name".
					Channel midiChannel = midiChannels[cont.ChannelNum - 1];

					if (midiChannel != null)
					{
						cont.channel = midiChannel;
					}
					else
					{
						bRestored = false;
						break;
					}
				}
			}

			return bRestored;
		}

		void PrepareChannels()
		{
			channels = new List<ChannelContainer>();
		
			foreach (KeyValuePair<int, Channel> entry in track.Channels)
			{
				// Add one to the Channel Number because we don't store the index, but the "name".
				ChannelContainer cont = new ChannelContainer(entry.Key + 1, entry.Value);
				channels.Add(cont);
			}
		}

		public void OnGUI()
		{
			EditorGUILayout.BeginVertical(bDrawLight ? MIDIImporterSkin.rowLightBGStyle : MIDIImporterSkin.rowDarkBGStyle);
			{
				EditorGUILayout.BeginHorizontal();
				{
					// Label width is 100 by default.  Field is larger.  Set directly with GUILayout.Width().
					EditorGUILayout.LabelField(trackContent, EditorStyles.boldLabel, GUILayout.Width(45f));
					trackNameContent.text = track.name;
					trackNameContent.tooltip = track.name;
					EditorGUILayout.LabelField(trackNameContent, GUILayout.Width(70f));
					EditorGUILayout.LabelField(instrumentContent, EditorStyles.boldLabel, GUILayout.Width(65f));
					instrumentNameContent.text = track.instrumentName;
					instrumentNameContent.tooltip = track.instrumentName;
					EditorGUILayout.LabelField(instrumentNameContent, GUILayout.Width(70f));
					GUILayout.FlexibleSpace();
					{
						// This control is always disabled (read-only).
						EditorGUI.BeginDisabledGroup(true);
						{
							GUILayout.Toggle(HasSelectedContent, selectedChanContent);
						}
						EditorGUI.EndDisabledGroup();
					}
				}
				EditorGUILayout.EndHorizontal();

				if (track.HasLyrics())
				{
					{
						// Lyrics.
						EditorGUILayout.BeginHorizontal();
						{
							GUILayout.Space(18f);
							EditorGUILayout.LabelField(lyricContent, GUILayout.Width(60f));

							GUILayout.FlexibleSpace();

							EditorGUILayout.LabelField(selectedLyricContent, GUILayout.Width(52f));
							bLyricsSelected = EditorGUILayout.Toggle(bLyricsSelected, GUILayout.Width(20f));
						}
						EditorGUILayout.EndHorizontal();
					}
				}

				if (track.Channels.Count > 0)
				{
					// Channels.
					Rect rect = KoreographerGUIUtils.GetLayoutRectForFoldout();
					bShowChannels = EditorGUI.Foldout(rect, bShowChannels, channelContent, true);
					{
						if (bShowChannels)
						{
							EditorGUILayout.BeginVertical(MIDIImporterSkin.indentAreaStyle);
							{
								// Contents of channels.
								ChannelContainer.bDrawLight = true;			// Always start with light.
								foreach (ChannelContainer cont in channels)
								{
									cont.OnGUI();
									ChannelContainer.bDrawLight = !ChannelContainer.bDrawLight;
								}
							}
							EditorGUILayout.EndVertical();
						}
					}
				}
			}
			EditorGUILayout.EndVertical();
		}

		public void AddEventsToKoreographyTrack(KoreographyTrackBase target, Song songDef, int sampleRate, float timeOffset)
		{
			if (track.HasLyrics() && bLyricsSelected)
			{
				AddLyricsToKoreographyTrack(target, songDef, sampleRate, timeOffset);
			}

			foreach (ChannelContainer cont in channels)
			{
				if (cont.bSelected)
				{
					cont.AddEventsToKoreographyTrack(target, songDef, sampleRate, timeOffset);
				}
			}
		}

		void AddLyricsToKoreographyTrack(KoreographyTrackBase target, Song songDef, int sampleRate, float timeOffset)
		{
			List<MIDITimedMessage<string>> lyrics = track.GetLyrics();
			
			foreach(MIDITimedMessage<string> lyric in lyrics)
			{
				KoreographyEvent evt = new KoreographyEvent();

				// Timing information.  lyric.timeInSec isn't calculated yet.  We need
				//  to further use the songDef to get the timing right because the ticks
				//  have variable timing weight based on tempo changes.
				double startTime = songDef.GetTimeInSeconds(lyric.time) + timeOffset;
				evt.StartSample = (int)(startTime * (double)sampleRate);

				TextPayload pl = new TextPayload();
				pl.TextVal = lyric.msg;
				evt.Payload = pl;

				target.AddEvent(evt);
			}

			EditorUtility.SetDirty(target);
		}

		#endregion
	}

	enum PayloadOptions
	{
		None,
		Velocity,
		Note
	}

	enum TypeOptions
	{
		OneOff,
		Span
	}

	[System.Serializable]
	class ChannelContainer
	{
		#region Static Fields

		public static bool bDrawLight = true;
		public static float rowWidth = 300f;	// Do something with this?

		static GUIContent channelContent	= new GUIContent("Override Me!", "MIDI event information and export options for this channel.");
		static GUIContent noteCountContent	= new GUIContent("Note Count", "The number of notes in this MIDI channel. NOTE: This may be different from exported Koreography Event total as Koreography Events can't exist at the same time.");
		static GUIContent selectedContent	= new GUIContent("Selected", "Whether or not the contents of this channel will be included in the exported Koreography Track.");

		static GUIContent typeContent		= new GUIContent("Output Type", "The type of Koreography Event MIDI events in this channel will be exported as.");
		static GUIContent oneOffContent		= new GUIContent("OneOff", "Exported events will occur at MIDI KeyOn timings.");
		static GUIContent spanContent		= new GUIContent("Span", "Exported events will span note duration.");

		static GUIContent payloadContent	= new GUIContent("Output Payload", "The payload, if any, to export from MIDI events.");
		static GUIContent noPayloadContent	= new GUIContent("None", "No payload will be attached to the event.");
		static GUIContent velocityContent	= new GUIContent("Velocity", "Velocity (intensity/volume of the note) will be attached to the event.");
		static GUIContent noteContent		= new GUIContent("Note", "The musical note will be attached to the event.");

		static GUIContent optionsContent	= new GUIContent("Payload Options", "Settings to specify how the MIDI event data should be exported.");
		static GUIContent velNormalContent	= new GUIContent("Normalized", "The velocity data normalized as a float in the range of [0,1].");
		static GUIContent velRawContent		= new GUIContent("Raw Data", "The integer value contained in the MIDI data [0,127].");
		static GUIContent noteTextContent	= new GUIContent("As Text", "The musical note converted to text, e.g. \"C\" or \"D#/Eb\".");
		static GUIContent noteRawContent	= new GUIContent("Raw Data", "The integer value contained in the MIDI data [0,127].  Middle C is 60.");

		#endregion
		#region Fields

		public Channel	channel;
		public bool 	bSelected = false;

		int				channelNum;	// The MIDI channel number [1,16].

		// Defaults.
		TypeOptions typeOpt = TypeOptions.OneOff;
		PayloadOptions payloadOpt = PayloadOptions.None;

		// UI related.
		bool bRawVelocity = false;
		bool bRawNote = false;
		bool bShowContent = false;
	
		#endregion
		#region Properties

		/// <summary>
		/// Returns the ChannelNumber assigned to the MIDI Channel in
		///  this Container.  This is the "name" of the channel, not
		///  the index [1, 16].
		/// </summary>
		/// <value>The channel number.</value>
		public int ChannelNum
		{
			get
			{
				return channelNum;
			}
		}

		#endregion
		#region Methods

		public ChannelContainer(int chanNum, Channel chan)
		{
			channelNum = chanNum;
			channel = chan;
		}

		public void OnGUI()
		{
			EditorGUILayout.BeginHorizontal();
			{
				channelContent.text = channelNum.ToString();
				EditorGUIUtility.fieldWidth = 35f;
				Rect rect = KoreographerGUIUtils.GetLayoutRectForFoldout();
				bShowContent = EditorGUI.Foldout(rect, bShowContent, channelContent, true);
				EditorGUIUtility.fieldWidth = 0f;
				GUILayout.Space(10f);
				EditorGUILayout.LabelField(noteCountContent, EditorStyles.boldLabel, GUILayout.Width(75f));
				EditorGUILayout.LabelField(channel.NumNotes().ToString(), GUILayout.Width(40f));
				GUILayout.FlexibleSpace();
				EditorGUILayout.LabelField(selectedContent, GUILayout.Width(52f));
				bSelected = EditorGUILayout.Toggle(bSelected, GUILayout.Width(20f));
			}
			EditorGUILayout.EndHorizontal();

			if (bShowContent)
			{
				EditorGUI.BeginDisabledGroup(!bSelected);
				{
					EditorGUILayout.BeginVertical(MIDIImporterSkin.indentAreaStyle);
					{
						// Output Type
						{
							EditorGUILayout.BeginHorizontal();
							{
								EditorGUILayout.LabelField(typeContent, GUILayout.Width(100f));

								if (GUILayout.Toggle(typeOpt == TypeOptions.OneOff, oneOffContent, EditorStyles.miniButtonLeft, GUILayout.Width(50f), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
								{
									typeOpt = TypeOptions.OneOff;
								}
								if (GUILayout.Toggle(typeOpt == TypeOptions.Span, spanContent, EditorStyles.miniButtonRight, GUILayout.Width(50f), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
								{
									typeOpt = TypeOptions.Span;
								}
							}
							EditorGUILayout.EndHorizontal();
						}

						// Output Payload
						{
							EditorGUILayout.BeginHorizontal();
							{
								EditorGUILayout.LabelField(payloadContent, GUILayout.Width(100f));

								if (GUILayout.Toggle(payloadOpt == PayloadOptions.None, noPayloadContent, EditorStyles.miniButtonLeft, GUILayout.Width(50f), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
								{
									payloadOpt = PayloadOptions.None;
								}
								if (GUILayout.Toggle(payloadOpt == PayloadOptions.Velocity, velocityContent, EditorStyles.miniButtonMid, GUILayout.Width(70f), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
								{
									payloadOpt = PayloadOptions.Velocity;
								}
								if (GUILayout.Toggle(payloadOpt == PayloadOptions.Note, noteContent, EditorStyles.miniButtonRight, GUILayout.Width(50f), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
								{
									payloadOpt = PayloadOptions.Note;
								}
							}
							EditorGUILayout.EndHorizontal();
						}

						// Payload Options
						{
							EditorGUILayout.BeginHorizontal();
							{
								EditorGUILayout.LabelField(optionsContent, GUILayout.Width(100f));

								if (payloadOpt == PayloadOptions.None)
								{
									// Empty.
								}
								else if (payloadOpt == PayloadOptions.Velocity)
								{
									if (GUILayout.Toggle(!bRawVelocity, velNormalContent, EditorStyles.miniButtonLeft, GUILayout.Width(85f), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
									{
										bRawVelocity = false;
									}
									if (GUILayout.Toggle(bRawVelocity, velRawContent, EditorStyles.miniButtonRight, GUILayout.Width(70f), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
									{
										bRawVelocity = true;
									}
								}
								else if (payloadOpt == PayloadOptions.Note)
								{
									if (GUILayout.Toggle(!bRawNote, noteTextContent, EditorStyles.miniButtonLeft, GUILayout.Width(65f), GUILayout.MaxHeight(EditorGUIUtility.singleLineHeight)))
									{
										bRawNote = false;
									}
									if (GUILayout.Toggle(bRawNote, noteRawContent, EditorStyles.miniButtonRight, GUILayout.Width(70f), GUILayout.MaxHeight(EditorGUIUtility.singleLineHeight)))
									{
										bRawNote = true;
									}
								}
							}
							EditorGUILayout.EndHorizontal();
						}
					}
					EditorGUILayout.EndVertical();
				}
				EditorGUI.EndDisabledGroup();
			}
		}

		public void AddEventsToKoreographyTrack(KoreographyTrackBase target, Song songDef, int sampleRate, float timeOffset)
		{
			List<Note> notes = channel.Notes;

			foreach (Note note in notes)
			{
				KoreographyEvent evt = new KoreographyEvent();

				// Timing information.
				double startTime = songDef.GetTimeInSeconds(note.startTime) + timeOffset;
				evt.StartSample = (int)(startTime * (double)sampleRate);

				if (typeOpt == TypeOptions.Span)
				{
					double endTime = songDef.GetTimeInSeconds(note.endTime) + timeOffset;
					evt.EndSample = (int)(endTime * (double)sampleRate);
				}
				else
				{
					evt.EndSample = evt.StartSample;
				}

				// Payload Settings.
				if (payloadOpt == PayloadOptions.Note)
				{
					if (bRawNote)
					{
						IntPayload pl = new IntPayload();
						pl.IntVal = note.key;
						evt.Payload = pl;
					}
					else
					{
						TextPayload pl = new TextPayload();
						pl.TextVal = Note.GetMusicNote(note.key);
						evt.Payload = pl;
					}
				}
				else if (payloadOpt == PayloadOptions.Velocity)
				{
					if (bRawVelocity)
					{
						IntPayload pl = new IntPayload();
						pl.IntVal = note.velocity;
						evt.Payload = pl;
					}
					else
					{
						FloatPayload pl = new FloatPayload();
						pl.FloatVal = (float)note.velocity / 127f;
						evt.Payload = pl;
					}
				}

				target.AddEvent(evt);
			}

			EditorUtility.SetDirty(target);
		}

		#endregion
	}

	[System.Serializable]
	class TempoEntry
	{
		#region Static Fields

		public static bool bDrawLight = true;

		static GUIContent nameField	= new GUIContent("Section Name", "The name to give to this tempo section.");
		static GUIContent timeField	= new GUIContent("Time", "The time at which this tempo section begins.");
		static GUIContent bpmField	= new GUIContent("BPM", "The Beats Per Minute of this tempo section.");
		static GUIContent bpbField	= new GUIContent("BPB", "The Beats Per Bar of this tempo section.");

		#endregion
		#region Fields

		public string name = "Section";
		public int beatsPerBar = 4;
		public double startTimeInSec = 0f;
		public double secsPerBeat = 0.5f;

		#endregion
		#region Properties

		public float BPM
		{
			get
			{
				return (float)(60d / secsPerBeat);
			}
		}

		#endregion
		#region Methods

		public void OnGUI()
		{
			EditorGUILayout.BeginVertical(bDrawLight ? MIDIImporterSkin.rowLightBGStyle : MIDIImporterSkin.rowDarkBGStyle);
			{
				float originalPrefix = EditorGUIUtility.labelWidth;
				EditorGUIUtility.labelWidth = 85f;
				name = EditorGUILayout.TextField(nameField, name, GUILayout.Width(100f + EditorGUIUtility.labelWidth));
				EditorGUIUtility.labelWidth = originalPrefix;

				EditorGUILayout.BeginHorizontal();
				{
					EditorGUILayout.LabelField(timeField, EditorStyles.boldLabel, GUILayout.Width(40f));
					EditorGUILayout.LabelField(startTimeInSec.ToString(), GUILayout.Width(50f));
					GUILayout.FlexibleSpace();
					EditorGUILayout.LabelField(bpmField, EditorStyles.boldLabel, GUILayout.Width(30f));
					EditorGUILayout.LabelField(BPM.ToString(), GUILayout.Width(50f));
					GUILayout.FlexibleSpace();
					EditorGUILayout.LabelField(bpbField, EditorStyles.boldLabel, GUILayout.Width(30f));
					EditorGUILayout.LabelField(beatsPerBar.ToString(), GUILayout.Width(15f));
				}
				EditorGUILayout.EndHorizontal();
			}
			EditorGUILayout.EndVertical();
		}

		public TempoSectionDef GetDefForEntry(int sampleFrequency)
		{
			TempoSectionDef def = new TempoSectionDef();
			def.SectionName = name;
			def.BeatsPerMeasure = beatsPerBar;
			def.StartSample = (int)(startTimeInSec * (double)sampleFrequency);
			def.SamplesPerBeat = secsPerBeat * (double)sampleFrequency;
			def.DoesStartNewMeasure = false;

			return def;
		}

		public void SetInfoFromTempoAndTimeSig(MIDITempo tempo, MIDITimeSignature timeSig)
		{
			beatsPerBar = timeSig.beatsPerBar;
			secsPerBeat = ((double)tempo.microPerQuarter / 1000000d);
		
			// MIDI stores Tempo values based on the quarter note.  Adjust if
			//  the TimeSignature message contained a non-quarter note beat value.
			if (timeSig.noteValue != 4)
			{
				secsPerBeat = (4d * secsPerBeat) / (double)timeSig.noteValue;
			}
		}

		#endregion
	}
}
