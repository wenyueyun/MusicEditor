//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using LitJson;

namespace SonicBloom.Koreo.EditorUI
{
    [System.Serializable]
    internal class WaveDisplayState
    {
        #region Static Fields

        // TODO: Calculate this to take SampleRate of clip into account.
        public static int s_DefaultSamplesPerPack = 990;

        #endregion
        #region Fields

        // Core positioning info.
        public int samplesPerPack = s_DefaultSamplesPerPack;    // The number of samples represented by a single Pack.
        public int waveStartMax = 0;    // How far into the view the waveform beginning can be drawn.
        public int waveEndMin = 0;      // How far into the view the waveform end can be drawn.
        public int firstPackPos = 0;    // The position of the first waveform pack.  Range: [-(totalPacks - (waveEndMin - waveStartMax)), 0].

        public int playheadSamplePosition = 0;
        public int playbackAnchorSamplePosition = 0;
        public int beatSubdivisions = 0;
        public WaveDisplayType displayType = WaveDisplayType.Both;

        #endregion
        #region Static Methods

        /// <summary>
        /// Takes a sample count and a number of samples per pack and returns the
        /// total number of packs. This includes a potentially *partial* final pack.
        /// </summary>
        /// <returns>The number of packs available.</returns>
        /// <param name="sampleCount">The total sample count to use.</param>
        /// <param name="samplesPerPack">The number of samples per pack to use in the calculation.</param>
        public static int GetNumPacks(int sampleCount, int samplesPerPack)
        {
            // The total number of packs (in the sampleCount).  Integer division truncates.
            return (sampleCount / samplesPerPack) + ((sampleCount % samplesPerPack == 0) ? 0 : 1);
        }

        #endregion
        #region Methods

        /// <summary>
        /// Gets the number of the first visible pack.
        /// </summary>
        /// <returns>The number of the first visible pack of samples.</returns>
        public int GetFirstVisiblePack()
        {
            // FirstPackPos goes negative quickly.  Subtracting actually adds the value.  Make it zero if we're kicked
            //  in at all.
            return Mathf.Max(0, (-waveStartMax - firstPackPos));
        }

        /// <summary>
        /// Gets the sample index that represents the first visible pack of samples.
        /// </summary>
        /// <returns>The sample index of the first visible pack of samples.</returns>
        public int GetFirstVisiblePackSample()
        {
            return GetFirstVisiblePack() * samplesPerPack;
        }

        /// <summary>
        /// Gets the amount the waveform is kicked into the view.
        /// </summary>
        /// <returns>The waveform inset.</returns>
        public int GetWaveformInset()
        {
            return Mathf.Max(waveStartMax + firstPackPos, 0);
        }

        /// <summary>
        /// Given the sample count, this will return the total number of sample
        /// packs available.  It includes a potentially *partial* final pack.
        /// </summary>
        /// <returns>The number packs available.</returns>
        /// <param name="sampleCount">The total sample count to use.</param>
        public int GetNumPacks(int sampleCount)
        {
            return WaveDisplayState.GetNumPacks(sampleCount, samplesPerPack);
        }

        /// <summary>
        /// Returns the position of firstPackPos when fully scrolled.  Note that
        /// this value is probably very negative.
        /// </summary>
        /// <returns>The position of the firstPackPos when fully scrolled.</returns>
        /// <param name="totalPacks">The total number of packs in consideration.</param>
        public int GetMaxPackPosition(int totalPacks)
        {
            return -(totalPacks - (waveEndMin - waveStartMax));
        }

        #endregion
    }

    /// <summary>
    /// The Koreography Editor.  This is the main UI for editing Koreography
    /// and Koreography Tracks.
    /// </summary>
    public class KoreographyEditor : EditorWindow
    {
        #region Static Fields

        static KoreographyEditor TheKoreographyEditor = null;

        // Editor Preferences Keys.
        static string FirstRunPref = "KoreographyEditor_FirstRun";
        static string AssetPathPref = "KoreographyEditor_AssetPath";

        // System Settings.
        static SerializedProperty DisabledAudioSetting;

        // Koreography Editor Settings.
        public static bool ShowAudioFileImportOption = false;   // Enable this to show the Audio File loading option for AudioClips.

#if UNITY_5_0
		// Cached Reflection information.
		static MethodInfo GetBuildTargetGroup = typeof(BuildPipeline).GetMethod("GetBuildTargetGroup", BindingFlags.Static | BindingFlags.NonPublic);
#endif

        // General Content.
        static GUIContent helpContent = new GUIContent("", "Koreographer Help");
        static GUIContent exportContent = new GUIContent("Export", "Export  Json");
        // Audio Loading Content.
        static GUIContent audioClipContent = new GUIContent("Audio Clip", "The AudioClip asset associated with this Koreography.");
        static GUIContent audioFileContent = new GUIContent("Audio File", "The Audio File used by this Koreography.");
        static GUIContent loadAudioFileContent = new GUIContent("Load Audio File", "Load an audio file.");
        static GUIContent loadClipContent = new GUIContent("AudioClip Asset", "Load an AudioClip into the Koreography.  This is default and will store a reference to the AudioClip inside the Koreography.");
        static GUIContent loadFileContent = new GUIContent("Audio File", "Load an Audio File into the Koreography.  This will load an Audio File on your computer.  No AudioClip will be stored in the Koreography (just the name of the file).");
        // Tempo Content.
        static GUIContent tempoContent = new GUIContent("Tempo", "The tempo for this tempo section.");
        static GUIContent startSampContent = new GUIContent("Start Sample", "The sample position at which this Tempo Section begins.");
        static GUIContent beatsPerMinContent = new GUIContent("BPM", "Beats Per Minute");
        static GUIContent sampsPerBeatContent = new GUIContent("Samples Per Beat", "Number of samples that span a single beat");
        static GUIContent beatsPerBarContent = new GUIContent("Beats Per Measure", "How many beats appear in a single measure.");
        static GUIContent resetMeasureContent = new GUIContent("Starts New Measure", "Whether this Tempo Section begins a new measure or continues from the previous one.  Makes no difference for the first Tempo Section.");
        // Track Content.
        static GUIContent trackEventIDContent = new GUIContent("Track Event ID", "The Event ID of the currently selected Koreography Track.");
        static GUIContent trackNewContent = new GUIContent("New", "Create a new Koreography Track and add it to the current Koreography.");
        static GUIContent trackLoadContent = new GUIContent("Load", "Load an existing Koreography Track and add it to the current Koreography.");
        static GUIContent trackRemoveContent = new GUIContent("Remove", "Remove the current Koreography Track from the current Koreography.");
#if !KOREO_NON_PRO
        static GUIContent trackAnalysisContent = new GUIContent("Analyze", "Opens the Analysis Settings panel, which allows you to auto-generate Koreography Events!");
#else
		static GUIContent trackAnalysisContent = new GUIContent("Analyze", "Analysis is a Koreographer Pro feature.");
#endif
        // Button Content.
        static GUIContent visContent = new GUIContent("V", "(v) Open or close the Visualizer window.");
        static GUIContent prevBeatContent = new GUIContent("", "Snaps the value to the previous beat (or subdivision, if specified).");
        static GUIContent nextBeatContent = new GUIContent("", "Snaps the value to the next beat (or subdivision, if specified).");
        static GUIContent nearestBeatContent = new GUIContent("", "Snaps the value to the nearest beat (or subdivision, if specified).");
        // Menu Content.
        static GUIContent cutContent = new GUIContent("Cut");
        static GUIContent copyContent = new GUIContent("Copy");
        static GUIContent pasteContent = new GUIContent("Paste");
        static GUIContent pastePayloadContent = new GUIContent("Paste Payload Only");
        static GUIContent playFromHereContent = new GUIContent("Play From Here");
        static GUIContent playAnchorHereContent = new GUIContent("Set Playback Anchor Here");
        static GUIContent playAnchorClearContent = new GUIContent("Clear Custom Playback Anchor");
        // Warning content.
        static GUIContent fixClipContent = new GUIContent("Fix this?", "In order for the Koreography Editor to generate a waveform, the Audio Clip's import settings need to be changed (Load Type).");
        static GUIContent enableAudioContent = new GUIContent("Enable Audio System", "Re-enables Unity Audio for the current project.  You can disable it again in \"Edit->Project Settings->Audio\".");

        static GUIStyle playButtonStyle;
        static GUIStyle stopButtonStyle;
        static GUIStyle payloadFieldStyle;
        static GUIStyle radioButtonStyle;
        static GUIStyle waveScrollAreaStyle;

        // Layout related.
        static float MinWaveViewWidth = 783f;   // Used to handle the minimum width of the wave display.  NOTE: Should be updated as layout changes.
        static float HorizontalPadding = 4f;    // Used to offset the wave display (and adjust width).

        static float MaxWaveHeightBase = 622f;  // Used to detect whether the window's vertical scrollbar is visible or not.
        static float MaxWaveHeightMany = 680f;  // Same as above except for multi-event selection.
        static float MaxWaveHeightOne = 702f;   // Same as above except for single-event selection.

        // Zoom related.
        static int MaxLinearZoomPackSize = 35;  // Maximum sample count for "linear" zooming. Higher values will be handled on the logarithmic scale.

        // Track and Payload type handling. These values are not specific to a given Koreography Editor, but are currently
        //  only used within it.
        static Dictionary<System.Type, List<string>> TrackPayloadNames = new Dictionary<System.Type, List<string>>();

        #endregion
        #region Fields

        bool bIsWaveDisplayFocused = false;

        AudioSource audioSrc = null;            // Used for audio playback control.
        AudioSource scratchSrc = null;          // Used for scratching audio.
        int estimatedSampleTime = 0;

        long lastUpdateTicks = 0;               // Used to calculate a proper deltaTime value in Update().

        Koreography editKoreo = null;

        enum AudioLoadMethod
        {
            AudioClip,
            AudioFile,
        }

        AudioLoadMethod audioLoadMethod = AudioLoadMethod.AudioClip;

        AudioClip audioFileClip = null;

        int editTempoSectionIdx = -1;

        KoreographyTrackBase editTrack = null;
        private int selectedIdx = 0;

        WaveDisplayState displayState = new WaveDisplayState();

        Rect newTrackButtRect;          // Used for positioning a potential dropdown menu (if needed).

        WaveDisplay waveDisplay = new WaveDisplay();    // Serialized fields can't reliably be null (deserialize to default object!).
        Rect fullWaveContentRect;

        // Layout related.
        int maxSamplesPerPack = 1;
        float curWaveViewWidth = KoreographyEditor.MinWaveViewWidth;    // Used for dynamic resizing thanks to scroll views.
        float curMaxHeight = 0f;

        bool bShowBPM = true;           // Tempo display switch: [BPM, Samples Per Beat]
        bool bShowPlayhead = false;

        // Track and Payload type handling. These are "current state" values.
        List<System.Type> payloadTypes = new List<System.Type>();
        List<string> payloadTypeNames = new List<string>();
        int currentPayloadTypeIdx = -1;

        bool bCreateOneOff = true;

        Vector2 viewPosition = Vector2.zero;
        Vector2 scrollPosition = Vector2.zero;
        bool bScrollWithPlayhead = true;

        bool bSnapTimingToBeat = true;

        // Data!
        KoreographyEvent buildEvent = null;
        List<KoreographyEvent> selectedEvents = new List<KoreographyEvent>();
        List<KoreographyEvent> eventsToHighlight = new List<KoreographyEvent>();

        // Cut/Copy/Paste!
        List<KoreographyEvent> clippedEvents = new List<KoreographyEvent>();

        // Mouse dragging related.
        EventEditMode eventEditMode = EventEditMode.None;
        float eventEditClickX = 0f;
        Vector2 dragStartPos = Vector2.zero;
        Vector2 dragEndPos = Vector2.zero;
        List<KoreographyEvent> dragSelectedEvents = new List<KoreographyEvent>();

        List<Rect> lcdRects = new List<Rect>();

        enum LCDDisplayMode
        {
            SampleTime,
            MusicTime,
            SolarTime,
        }

        LCDDisplayMode lcdMode = LCDDisplayMode.MusicTime;

        // Use the string builder instead of raw strings to save a little garbage each pass!
        System.Text.StringBuilder lcdStringBuilder = new System.Text.StringBuilder(20);

        enum ControlMode
        {
            Select,
            Author,
            Clone,
        }

        ControlMode controlMode = ControlMode.Select;

        EventVisualizer visualizerWindow = null;
        KoreographyTrackBase.TrackingCrumbs trackingCrumbs;
        List<KoreographyEvent> eventsToVisualize = new List<KoreographyEvent>();

#if !KOREO_NON_PRO
        AnalysisPanel analysisWindow = null;
        Vector2 analysisRange = new Vector2(0f, 1f);
#endif

        #endregion
        #region Static Properties

        /// <summary>
        /// Gets a reference to the Koreography Editor window.  Null if not open.
        /// </summary>
        /// <value>The Koreography Editor window.  This is null if not open.</value>
        internal static KoreographyEditor TheEditor
        {
            get
            {
                return TheKoreographyEditor;
            }
        }

        /// <summary>
        /// Gets or sets the path to the most recently used asset directory in the current project.
        /// </summary>
        /// <value>The asset path directory.</value>
        private static string AssetPath
        {
            get
            {
                // Grab the path out of the EditorPrefs system.
                string path = EditorPrefs.GetString(KoreographyEditor.AssetPathPref, string.Empty);

                // If we have an actual value we need to verify it.
                if (!string.IsNullOrEmpty(path))
                {
                    // Check that the directory exists at the location specified.
                    if (!Directory.Exists(path))
                    {
                        // If the directory didn't exist, set to empty.
                        path = string.Empty;
                    }
                    else
                    {
                        if (!Path.GetFullPath(path).Contains(Application.dataPath))
                        {
                            path = string.Empty;
                        }
                    }
                }

                if (string.IsNullOrEmpty(path))
                {
                    path = Path.Combine(Application.dataPath, "Export/Koreography");
                }

                return path;
            }
            set
            {
                string path = value;
                if (File.Exists(path))
                {
                    path = Path.GetDirectoryName(path);
                }
                EditorPrefs.SetString(KoreographyEditor.AssetPathPref, path);
            }
        }

        /// <summary>
        /// Gets the offset in units for the edge of the WaveDisplay content from the
        ///  edge of the EditorWindow.
        /// </summary>
        /// <value>The number of units between the edge of the EditorWindow and the
        /// beginning of the Waveform display.</value>
        static float WaveStartPositionOffset
        {
            get
            {
                // Horizontal padding is used in two locations: one for the scroll view and one
                //  for the wave display offset within it.
                return HorizontalPadding * 2f;
            }
        }

        #endregion
        #region Properties

        internal AudioClip EditClip
        {
            get
            {
                AudioClip retClip = null;

                switch (audioLoadMethod)
                {
                    case AudioLoadMethod.AudioClip:
                        if (editKoreo != null)
                        {
                            retClip = editKoreo.SourceClip;
                        }
                        break;
                    case AudioLoadMethod.AudioFile:
                        retClip = audioFileClip;
                        break;
                    default:
                        break;
                }

                return retClip;
            }
        }

        internal KoreographyTrackBase EditTrack
        {
            get
            {
                return editTrack;
            }
        }

        internal WaveDisplayState DisplayState
        {
            get
            {
                return displayState;
            }
        }

        #endregion
        #region Static Methods

        // Use custom priority to reduce likelihood of collision with other assets (fun fact: 7/14 is Sonic Bloom's birthday!).
        [MenuItem("Window/Koreography Editor %#k", false, 714)]
        static KoreographyEditor ShowWindow()
        {
            KoreographyEditor win = EditorWindow.GetWindow<KoreographyEditor>("Koreography Editor");

            // First run?
            if (!EditorPrefs.HasKey(KoreographyEditor.FirstRunPref) ||
                EditorPrefs.GetBool(KoreographyEditor.FirstRunPref))
            {
                // Adjust the size of the window to show the entire view.
                win.position = new Rect(50f, 50f, 900f, 710f);

                // Store the fact that we've had a first-run!
                EditorPrefs.SetBool(KoreographyEditor.FirstRunPref, false);
            }

            return win;
        }

        /// <summary>
        /// Opens <paramref name="koreography"/> in the Koreography Editor,
        /// optionally selecting <paramref name="track"/>.
        /// </summary>
        /// <param name="koreography">The Koreography to open.</param>
        /// <param name="track">The Koreography Track to highlight.</param>
        public static void OpenKoreography(Koreography koreography, KoreographyTrackBase track = null)
        {
            KoreographyEditor editor = ShowWindow() as KoreographyEditor;

            if (editor != null)
            {
                // Set the Koreography only if it isn't the one already selected!
                if (editor.editKoreo != koreography)
                {
                    editor.SetNewEditKoreo(koreography);
                }

                // Set the Koreography Track if it's valid and if it isn't already selected!
                if (track != null && editor.editTrack != track)
                {
                    editor.SetNewEditTrack(track);
                }
            }
        }

        /// <summary>
        /// Opens an OS "File Save" dialog with which the user may select the
        /// location and name at which to create a Koreography asset.  If one
        /// is specified, this creates and returns a new Koreography asset.
        /// </summary>
        /// <returns>The newly created Koreography asset if one was created,
        /// <c>null</c> otherwise.</returns>
        public static Koreography CreateNewKoreography()
        {
            Koreography newKoreo = null;

            // Get the save location and file type.  Then massage it for the AssetDatabase functions.
            string targetPath = EditorUtility.SaveFilePanel("Save New Koreography Asset...", KoreographyEditor.AssetPath, "NewKoreography", "asset");

            // Only proceed if we have a valid path.
            if (!string.IsNullOrEmpty(targetPath))
            {
                // Verify that we have a valid path.
                if (targetPath.Contains(Application.dataPath))
                {
                    // The AssetDatabase uses relative paths, starting at the project root.
                    string dbPath = targetPath.Replace(Application.dataPath, "Assets");

                    // Final sanity checking.
                    if (Path.GetExtension(dbPath) == ".asset")
                    {
                        // Instantiate and init the new Koreography object.
                        newKoreo = ScriptableObject.CreateInstance<Koreography>();

                        // Create the new Koreography asset and save it!
                        AssetDatabase.CreateAsset(newKoreo, dbPath);
                        AssetDatabase.SaveAssets();

                        // Store the full path.
                        KoreographyEditor.AssetPath = targetPath;
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("Whoops!", "Cannot create Koreography at the specified location.  " +
                                                "Please choose a location in the current Unity project.", "Okay");
                }
            }

            return newKoreo;
        }

        /// <summary>
        /// Opens an OS "File Save" dialog with which the user may select the
        /// location and name at which to create a Koreography Track asset.
        /// If one is specified, this creates and returns a new Koreography
        /// Track asset.
        /// </summary>
        /// <returns>The newly created Koreography Track asset if one was
        /// created, <c>null</c> otherwise.</returns>
        public static KoreographyTrackBase CreateNewKoreographyTrack(System.Type trackType)
        {
            // NOTE: This is intentionally not generic. It is possible to support a generic version
            //  but it implies the use of Reflection when attempting to handle user-created types
            //  due to the nature of having to support callbacks from the menu. By utilizing the
            //  built-in ScriptableObject.CreateInstance(System.Type) function, we sidestep much of
            //  the reflection headaches caused.
            KoreographyTrackBase newTrack = null;

            string typeName = trackType.Name;

            // Get the save location and file type.  Then massage it for the AssetDatabase functions.
            string targetPath = EditorUtility.SaveFilePanel("Save New " + typeName + " Asset...", KoreographyEditor.AssetPath, "New" + typeName, "asset");

            // Only proceed if we have a valid path and a valid type.
            if (!string.IsNullOrEmpty(targetPath) && trackType.IsSubclassOf(typeof(KoreographyTrackBase)))
            {
                // Verify that we have a valid path.
                if (targetPath.Contains(Application.dataPath))
                {
                    // The AssetDatabase uses relative paths, starting at the project root.
                    string dbPath = targetPath.Replace(Application.dataPath, "Assets");

                    // Final sanity checking.
                    if (Path.GetExtension(dbPath) == ".asset")
                    {
                        // Instantiate and init the new Koreography Track object.
                        newTrack = ScriptableObject.CreateInstance(trackType) as KoreographyTrackBase;

                        // Create the new Koreography Track asset and save it!
                        AssetDatabase.CreateAsset(newTrack, dbPath);
                        AssetDatabase.SaveAssets();

                        // Store the full path.
                        KoreographyEditor.AssetPath = targetPath;
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("Whoops!", "Cannot create " + typeName + " asset at the specified location. " +
                                                "Please choose a location in the current Unity project.", "Okay");
                }
            }

            return newTrack;
        }

#if !(UNITY_4_5 || UNITY_4_6 || UNITY_4_7)
        // Used to grab the Active Platform (set in the Build Settings window).  Reflection is necessary
        //  due to private APIs.
        static BuildTargetGroup GetActivePlatformGroup()
        {
#if UNITY_5_0
			// Use the cached "GetBuildTargetGroup" method.
			return (BuildTargetGroup)GetBuildTargetGroup.Invoke(null, new object[]{EditorUserBuildSettings.activeBuildTarget});
#else
            return BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
#endif
        }
#endif

        /// <summary>
        /// Gets the Friendly Name of the [payload] type passed in by invoking the
        /// "GetFriendlyName" function of the type.
        /// </summary>
        /// <returns>The friendly name of payload type.</returns>
        /// <param name="payType">The type of the payload for which to retreive the name.</param>
        internal static string GetFriendlyNameOfPayloadType(System.Type payType)
        {
            MethodInfo methodInfo = payType.GetMethod("GetFriendlyName");
            return (methodInfo != null) ? (string)methodInfo.Invoke(null, null) : payType.Name;
        }

        /// <summary>
        /// Determines if <paramref name="clip"/> is okay for editing in the Koreography
        /// Editor (if <c>AudioClip.GetData()</c> will return usable data). By default,
        /// <c>AudioClip</c>s are *not* valid.
        /// </summary>
        /// <returns><c>true</c> if <paramref name="clip"/> will work with the Koreography
        /// Editor; otherwise, <c>false</c>.</returns>
        /// <param name="clip">The <c>AudioClip</c> to check.</param>
        public static bool IsAudioClipValid(AudioClip clip)
        {
            bool bValid = false;

            // We need to verify that we can call AudioClip.GetData().  According to the
            //  documentaiton (http://docs.unity3d.com/ScriptReference/AudioClip.GetData.html),
            //  only assets set to "DecompressOnLoad" should be viable.
            AudioImporter clipImporter = AudioImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as AudioImporter;

            if (clipImporter != null)
            {
#if UNITY_4_5
				// Internal testing shows that LoadType "StreamFromDisc" also works.  The Native 
				//  format (WAV) only supports those two options so we simply allow them.
				//  Otherwise, simply make sure we're not set to Compressed.
				bValid = clipImporter.format == AudioImporterFormat.Native ||
				         clipImporter.loadType != AudioImporterLoadType.CompressedInMemory;
#elif (UNITY_4_6 || UNITY_4_7)
				// Internal testing shows that only Native AudioClips or compressed AudioClips
				//  set to "DecompressOnLoad" work.
				bValid = clipImporter.format == AudioImporterFormat.Native ||
					   	 clipImporter.loadType == AudioImporterLoadType.DecompressOnLoad;
#else

                // AudioClip.GetData in the Unity Editor uses the Active Platform (set in the Build Settings window) to determine settings.
                AudioImporterSampleSettings sampleSettings = clipImporter.GetOverrideSampleSettings(GetActivePlatformGroup().ToString());

                // Internal testing shows that AudioClips with Format "PCM" and LoadType
                //  "CompressedInMemory" also works.  Allow this as well.
                bValid = sampleSettings.loadType == AudioClipLoadType.DecompressOnLoad ||
                          (sampleSettings.loadType == AudioClipLoadType.CompressedInMemory && sampleSettings.compressionFormat == AudioCompressionFormat.PCM);
#endif
            }
            else
            {
                // We are probably some kind of streaming asset.  Either from a MovieTexture's AudioClip (forced streaming?)
                //  or from a WWW class...
                bValid = clip != null;
            }

            return bValid;
        }

        /// <summary>
        /// Checks <paramref name="clip"/> for validity.  If <paramref name="clip"/> is invalid
        /// it will pop up a dialog asking if the user wishes to take action to correct
        /// the issue.  If so, this causes a quick reimport of the <paramref name="AudioClip"/>.
        /// </summary>
        /// <returns><c>true</c> if <paramref name="clip"/> will work with the Koreography
        /// Editor; otherwise, <c>false</c>.</returns>
        /// <param name="clip">The <c>AudioClip</c> to check.</param>
        public static bool CheckAudioClipValidity(AudioClip clip)
        {
            bool bValid = true;

            if (!IsAudioClipValid(clip))
            {
                string clipPath = AssetDatabase.GetAssetPath(clip);
                AudioImporter clipImporter = AudioImporter.GetAtPath(clipPath) as AudioImporter;

                string title = "Incompatible settings detected";
                string message = "In order for the Koreography Editor to generate a waveform, the Audio Clip's import settings need to be changed (Load Type).  Please select an option below.  Pressing 'Cancel' will result in no waveform generation.";
#if !(UNITY_4_5 || UNITY_4_6 || UNITY_4_7)
                string platform = GetActivePlatformGroup().ToString();
                message += "\n\nNOTE: \"" + (clipImporter.ContainsSampleSettingsOverride(platform) ? platform : "Default") + "\" import settings will be modified.";
#endif
                string okay = "Set to Decompress on load";
                string cancel = "Cancel";
#if UNITY_4_5
				string alt = "Set to Stream from disc";
#else
                string alt = string.Empty;
#endif
                int choice = EditorUtility.DisplayDialogComplex(title, message, okay, cancel, alt);

                switch (choice)
                {
                    case 0: // "okay": Decompress on load.
#if (UNITY_4_5 || UNITY_4_6 || UNITY_4_7)
					clipImporter.loadType = AudioImporterLoadType.DecompressOnLoad;
					AssetDatabase.ImportAsset(clipPath);
#else
                        if (clipImporter.ContainsSampleSettingsOverride(platform))
                        {
                            // Change the settings for the current build platform.  Settings are overridden so changing
                            //  the default will have no effect.
                            AudioImporterSampleSettings importSettings = clipImporter.GetOverrideSampleSettings(platform);
                            importSettings.loadType = AudioClipLoadType.DecompressOnLoad;
                            clipImporter.SetOverrideSampleSettings(platform, importSettings);
                        }
                        else
                        {
                            // Change the Default build settings.  The user did not override settings for the current
                            //  build platform.  Changing the default is the most obvious (as it is the default view
                            //  in the Inspector).
                            AudioImporterSampleSettings defaultImportSettings = clipImporter.defaultSampleSettings;
                            defaultImportSettings.loadType = AudioClipLoadType.DecompressOnLoad;
                            clipImporter.defaultSampleSettings = defaultImportSettings;
                        }
                        AssetDatabase.ImportAsset(clipPath);
#endif
                        break;
                    case 1: // "cancel": Cancel.
                        bValid = false;
                        break;
#if UNITY_4_5
				case 2: // "alt": Stream from disc. (4.5 only!  See above...)
					clipImporter.loadType = AudioImporterLoadType.StreamFromDisc;
					AssetDatabase.ImportAsset(clipPath);
					break;
#endif
                    default:
                        Debug.LogError("What?!  Did the DisplayDialogComplex API change??");
                        break;
                }

            }

            return bValid;
        }

        internal static AudioClip GetAudioClipFromPath(string path)
        {
            AudioClip retClip = null;

            string filePath = path;

            // Ensure an absolute path.  Relative paths received should be
            //  relative to the "Assets" directory.
            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.GetFullPath(Path.Combine(Application.dataPath, filePath));
            }

            if (File.Exists(filePath))
            {
                if (filePath.Contains(Application.dataPath))
                {
                    EditorUtility.DisplayDialog("Unity Asset Specified", "The selected file already exists in this Project's Asset folder.  Please load this AudioClip with the standard object selector.", "OK");
                }
                else
                {
                    WWW fileLoadedAudioClip = new WWW("file://" + filePath);

                    while (!fileLoadedAudioClip.isDone)
                    {
                        EditorUtility.DisplayProgressBar("Loading AudioClip", "Loading External AudioClip...", fileLoadedAudioClip.progress);
                    }
                    EditorUtility.ClearProgressBar();

#if UNITY_5_6_OR_NEWER
                    // NOTE: Unity 5.6 and above converted the WWW.GetAudioClip() call to an Extension Method.
                    //  Using the extension method is not strictly necessary as the existing code works without
                    //  issue in source code releases. Unity's API Updater also properly handles the API change
                    //  in Unity 5.6 and up (the IL produced is slightly different). Further, building against
                    //  Unity 5.6 will stop the Assembly Updater from appearing in Unity 5.6 projects [and up].
                    //  That said, we still explicitly make use of the Extension Method here to call attention
                    //  to the change. The API Updater currently does not generate a report about the specific
                    //  APIs that it adjusts, so this is effectively the "documentation" about what changed
                    //  between versions; about why we produce a separate build for Unity 5.6 and up.
                    retClip = WWWAudioExtensions.GetAudioClip(fileLoadedAudioClip, false, false);
#else
					retClip = fileLoadedAudioClip.GetAudioClip(false, false);
#endif
                    retClip.name = Path.GetFileName(filePath);

                    // Tell Unity that we don't have any plans to save this asset.
                    retClip.hideFlags = HideFlags.HideAndDontSave;

                    fileLoadedAudioClip.Dispose();
                }
            }
            else
            {
                Debug.LogError("Failed loading Audio File at path: \"" + path + "\".\nAttempted to load fully qualified path: \"" + filePath + "\".");
            }

            return retClip;
        }

        /// <summary>
        /// Caches and returns a reference to the SerializedProperty that wraps the
        /// AudioManager's DisabledAudio system property.
        /// </summary>
        /// <returns>The cached SerializedProperty wrapper for the DisableAudio setting.</returns>
        internal static SerializedProperty GetSystemDisabledAudioProperty()
        {
            if (DisabledAudioSetting == null)
            {
                // Using AllAssets does not require a type set.  Doing that seems to restrict the properties that
                //  get loaded (at least by name).  Loading all like this appears to sidestep that issue.
                Object[] loadedObject = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset");

                // Grab a reference to the necessary property.
                SerializedObject audioMgr = new SerializedObject(loadedObject[0]);
                DisabledAudioSetting = audioMgr.FindProperty("m_DisableAudio");
            }

            // Update the object in case things changed in the background.
            DisabledAudioSetting.serializedObject.Update();

            return DisabledAudioSetting;
        }

        /// <summary>
        /// Checks whether Unity's internal Audio System is disabled.  This value is typically set
        /// with the "Disable Audio" setting in Edit->Project Settings->Audio.
        /// </summary>
        /// <returns><c>true</c>, if the audio system is disabled, <c>false</c> otherwise.</returns>
        public static bool GetAudioSystemDisabled()
        {
            return GetSystemDisabledAudioProperty().boolValue;
        }

        /// <summary>
        /// Disables or enables Unity's Audio system for the current project.  This modifies the setting
        /// stored in "ProjectSettings/AudioManager.asset".
        /// </summary>
        /// <param name="bDisabled">Disables Unity Audio if set to <c>true</c>, otherwise enables it.</param>
        public static void SetAudioSystemDisabled(bool bDisabled)
        {
            SerializedProperty bDisableAudio = GetSystemDisabledAudioProperty();

            bDisableAudio.boolValue = bDisabled;

            bDisableAudio.serializedObject.ApplyModifiedProperties();
        }

        #endregion
        #region Utility Methods

        internal bool DoesCurrentTrackSupportPayloadType(System.Type testType)
        {
            bool bSupported = false;

            if (EditTrack != null)
            {
                // Check if the TrackPayloadTypes property returns null? If that happens then something
                //  has gone seriously wrong (also this will cause a null-reference error).
                List<System.Type> trTypes = KoreographyTrackTypeUtils.TrackPayloadTypes[EditTrack.GetType()];
                bSupported = trTypes.Contains(testType);
            }

            return bSupported;
        }

        #endregion
        #region Methods

        void OnEnable()
        {
            TheKoreographyEditor = this;

            // Initialize timing calculations.
            lastUpdateTicks = System.DateTime.Now.Ticks;

            wantsMouseMove = true;
            EditorApplication.modifierKeysChanged += Repaint;
            EditorApplication.playmodeStateChanged += OnPlayModeStateChange;

            // Initialize the AudioSource for playback and editing.
            if (audioSrc == null)
            {
                GameObject go = EditorUtility.CreateGameObjectWithHideFlags("__KOREOGRAPHER__", HideFlags.HideAndDontSave, new System.Type[] { typeof(AudioSource) });
                audioSrc = go.GetComponent<AudioSource>();
                audioSrc.volume = 0.75f;
                audioSrc.playOnAwake = false;
                scratchSrc = go.AddComponent<AudioSource>();
                scratchSrc.volume = 0.75f;
                scratchSrc.playOnAwake = false;

                // Disregard global stuff that devs may use in their games.
                audioSrc.ignoreListenerPause = true;
                audioSrc.ignoreListenerVolume = true;
                audioSrc.bypassEffects = true;
                scratchSrc.ignoreListenerPause = true;
                scratchSrc.ignoreListenerVolume = true;
                scratchSrc.bypassEffects = true;

                // Make it so that camera movement has zero effect on the audio balance for us.
#if (UNITY_4_5 || UNITY_4_6 || UNITY_4_7)
				audioSrc.panLevel = 0f;
				scratchSrc.panLevel = 0f;
#else
                audioSrc.spatialBlend = 0f;
                scratchSrc.spatialBlend = 0f;
#endif
            }

            // Ensure we have access to skins.
            KoreographyEditorSkin.InitSkin();

            // Update GUIContent based on skin settings.
            prevBeatContent.image = KoreographyEditorSkin.prevBeatTex;
            nextBeatContent.image = KoreographyEditorSkin.nextBeatTex;
            nearestBeatContent.image = KoreographyEditorSkin.beatTex;

            // Setup the Track Type Lists.
            if (KoreographyEditor.TrackPayloadNames.Count == 0)
            {
                // Grab and store all the Koreography Track types.
                List<System.Type> trackTypes = KoreographyTrackTypeUtils.EditableTrackTypes;
                Dictionary<System.Type, List<System.Type>> trackPayloadTypes = KoreographyTrackTypeUtils.EditableTrackPayloadTypes;

                // Get all Payload types and names.
                foreach (System.Type trTy in trackTypes)
                {
                    // Store types.
                    List<System.Type> plTypes = trackPayloadTypes[trTy];

                    // Create the list of class names for use in the GUI.
                    List<string> plNames = new List<string>();
                    plNames.Add("No Payload");
                    plNames.AddRange(plTypes.Select(x => GetFriendlyNameOfPayloadType(x)));
                    KoreographyEditor.TrackPayloadNames.Add(trTy, plNames);
                }
            }

            // Ensure that we have the correct Payload type options selected. This ensures that selections
            //  last beyond script compilation passes.
            UpdatePayloadTypesForTrack(EditTrack);

            if (buildEvent != null)
            {
                //Debug.LogWarning("The Build Event was not properly cleared during the last use.  Did you just recompile scripts?");
                buildEvent = null;
            }

            // Initialize the scroll boundaries for the Waveform to the playhead marker location.
            displayState.waveStartMax = WaveDisplay.pixelDistanceToPlayheadMarker;
            displayState.waveEndMin = WaveDisplay.pixelDistanceToPlayheadMarker;

            // Cleaning out selection-related lists isn't necessary.  Their validity is checked at the beginning of the OnGUI call.
            //  Further, we will want to handle them differently when we eventually give them consideration for the Undo stack.
        }

        void OnDisable()
        {
            EditorApplication.playmodeStateChanged -= OnPlayModeStateChange;
            EditorApplication.modifierKeysChanged -= Repaint;
        }

        void OnPlayModeStateChange()
        {
            // When we are no longer playing.
            //  isPlayingOrWillChangePlaymode is true when either in PlayMode or heading there.
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                //  isPLaying is true when we're about to leave PlayMode.
                if (!EditorApplication.isPlaying)
                {
                    RemoveAudioListenerIfNecessary();
                }
            }
            // When we're already playing or paused we will need to add a listener.
            else if (IsPlaying() || IsPaused())
            {
                AddAudioListenerIfNecessary();
            }
        }

        void AddAudioListenerIfNecessary()
        {
            // We only add an AudioListener when we're entering or in PlayMode.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // First try to find one in the scene.
                AudioListener listenCom = FindObjectOfType<AudioListener>();
                if (listenCom == null)
                {
                    // If there isn't one, make sure we don't already have one.
                    //  Note that FindObjectOfType does not locate hidden objects (so ours
                    //  won't show up in the list!).
                    listenCom = audioSrc.GetComponent<AudioListener>();
                    if (listenCom == null)
                    {
                        audioSrc.gameObject.AddComponent<AudioListener>();
                    }
                }
            }
        }

        void RemoveAudioListenerIfNecessary()
        {
            AudioListener listenCom = audioSrc.GetComponent<AudioListener>();
            if (listenCom != null)
            {
                DestroyImmediate(listenCom);
            }
        }

        void Update()
        {
            // Grab the current ticks for the update processing.
            long curTicks = System.DateTime.Now.Ticks;

            // Force a Repaint() while audio is playing to update the playhead.
            if (IsPlaying())
            {
                int lastEstimate = estimatedSampleTime;
                int rawSampleTime = GetCurrentRawMusicSample();

                if (rawSampleTime == estimatedSampleTime)
                {
                    // Calculate the deltaTime in milliseconds. The standard Time functions don't work in the Editor.
                    double deltaTime = (double)(curTicks - lastUpdateTicks) / 10000000d;

                    // No update in the audio buffer.  Time to extrapolate.
                    estimatedSampleTime += (int)((float)EditClip.frequency * audioSrc.pitch * deltaTime);
                }
                else if (rawSampleTime < estimatedSampleTime)
                {
                    // We have backtracked most likely!  Test for a single sample, udating with the next frame.
                    estimatedSampleTime = rawSampleTime;
                    lastEstimate = estimatedSampleTime + 1;
                }
                else
                {
                    // Audio buffer updated.  Make sure we're good.
                    estimatedSampleTime = rawSampleTime;
                }

                // Visualizer messaging.
                if (editTrack != null && visualizerWindow != null)
                {
                    // TODO: handle this similarly to how it's done in the AudioVisor... for now, just make sure we get the
                    //  first sample position.
                    if (lastEstimate == 0)
                    {
                        lastEstimate--;

                        // Also reset the tracking crumbs as this indicates the beginning of the audio file.
                        trackingCrumbs.Reset();
                    }
                    editTrack.GetEventsInRangeTracked(lastEstimate + 1, estimatedSampleTime, ref trackingCrumbs, eventsToVisualize);

                    visualizerWindow.EventsFired(eventsToVisualize, estimatedSampleTime);
                    eventsToVisualize.Clear();
                }

                Repaint();
            }
            else
            {
                estimatedSampleTime = GetCurrentRawMusicSample();
            }

            // Store the current Update()'s time for next Update()'s timing calculation.
            lastUpdateTicks = curTicks;
        }

        void OnDestroy()
        {
            // Stop and delete the AudioSource.
            StopAudio();
            scratchSrc.Stop();
            GameObject.DestroyImmediate(audioSrc.gameObject);

            // Close all utility windows.
            if (visualizerWindow != null)
            {
                visualizerWindow.Close();
            }
#if !KOREO_NON_PRO
            if (analysisWindow != null)
            {
                analysisWindow.Close();
            }
#endif

            TheKoreographyEditor = null;
        }

        void OnGUI()
        {
            // Sanity checking.
            {
                // Make sure that we don't have any empty entries or otherwise bad data to worry about.
                ValidateKoreographyAndTrackData();

                // Check that our editTrack is okay.  It can be deleted out from underneath us by direct deletion in the asset library or operating system.
                if (editTrack != null && (editKoreo == null || editKoreo.GetIndexOfTrack(editTrack) < 0))
                {
                    KoreographyTrackBase newEditTrack = null;
                    if (editKoreo != null)
                    {
                        newEditTrack = editKoreo.GetTrackAtIndex(0);
                    }
                    SetNewEditTrack(newEditTrack);
                }

                // Check that the editTempoSectionIdx is valid.
                if (editTempoSectionIdx < 0 || (editKoreo == null || editTempoSectionIdx >= editKoreo.GetNumTempoSections()))
                {
                    editTempoSectionIdx = (editKoreo != null) ? Mathf.Clamp(editTempoSectionIdx, 0, editKoreo.GetNumTempoSections() - 1) : -1;
                }

                // Use for-loops below: Cannot do destructive tasks (Remove()) within foreach loops.

                // Check that our selectedEvents are okay.  They can be deleted out from underneath us by inspecting the event in the editor.
                for (int i = selectedEvents.Count - 1; i >= 0; --i)
                {
                    KoreographyEvent evt = selectedEvents[i];
                    if (evt == null || (editTrack == null || editTrack.GetIDForEvent(evt) < 0))
                    {
                        selectedEvents.RemoveAt(i);
                    }
                }

                // Also check dragSelectedEvents.
                for (int i = dragSelectedEvents.Count - 1; i >= 0; --i)
                {
                    KoreographyEvent evt = dragSelectedEvents[i];
                    if (evt == null || (editTrack == null || editTrack.GetIDForEvent(evt) < 0))
                    {
                        dragSelectedEvents.RemoveAt(i);
                    }
                }

                // Also check clippedEvents(??).
                for (int i = clippedEvents.Count - 1; i >= 0; --i)
                {
                    if (clippedEvents[i] == null)
                    {
                        clippedEvents.RemoveAt(i);
                    }
                }

                // Initialize our own in-line radio button style, currently based on the built-in one.
                //  Apparently the getter fails during recompilation passes so we need to check for it
                //  here.
                // TODO: Move this into OnEnable()?
                if (playButtonStyle == null)
                {
                    playButtonStyle = new GUIStyle(EditorStyles.miniButtonLeft);
                    playButtonStyle.padding.top = 4;
                    playButtonStyle.padding.bottom = 3;
                }
                if (stopButtonStyle == null)
                {
                    stopButtonStyle = new GUIStyle(EditorStyles.miniButtonRight);
                    stopButtonStyle.padding.top = 4;
                    stopButtonStyle.padding.bottom = 3;
                }
                if (payloadFieldStyle == null)
                {
                    payloadFieldStyle = new GUIStyle(EditorStyles.popup);
                    payloadFieldStyle.margin.top = 4;
                }
                if (radioButtonStyle == null)
                {
                    radioButtonStyle = new GUIStyle(EditorStyles.radioButton);
                    radioButtonStyle.padding.top = 0;
                    radioButtonStyle.overflow.top = -1;
                }
                if (waveScrollAreaStyle == null)
                {
                    waveScrollAreaStyle = new GUIStyle(GUI.skin.scrollView);
                    waveScrollAreaStyle.margin = GUI.skin.box.margin;
                }
            }

            // Input checking - This usually changes State.  Do this work BEFORE or AFTER all controls are drawn so that we don't change the layout mid-call!
            //  Doing this "before" allows us to Use the input events before the controls get a chance at them.
            {
                if (Event.current.type == EventType.ValidateCommand)
                {

                    switch (Event.current.commandName)
                    {
                        case "Cut":
                            if (selectedEvents.Count > 0 && bIsWaveDisplayFocused)
                            {
                                Event.current.Use();
                            }
                            break;
                        case "Copy":
                            if (selectedEvents.Count > 0 && bIsWaveDisplayFocused)
                            {
                                Event.current.Use();
                            }
                            break;
                        case "Paste":
                            if (clippedEvents.Count > 0 && selectedEvents.Count > 0 && bIsWaveDisplayFocused && waveDisplay.bValid)
                            {
                                Event.current.Use();
                            }
                            break;
                        case "SelectAll":
                            if (editTrack != null && bIsWaveDisplayFocused)
                            {
                                Event.current.Use();
                            }
                            break;
                        case "UndoRedoPerformed":
                            Event.current.Use();    // For now, just Use() it so we don't have to go into the rest of the function unnecessarily.
                            break;
                        default:
                            //						Debug.Log("Unknown command name encountered for Event ValidateCommand: " + Event.current.commandName);
                            break;
                    }
                }

                if (Event.current.type == EventType.ExecuteCommand)
                {
                    switch (Event.current.commandName)
                    {
                        case "Cut":
                            if (selectedEvents.Count > 0 && bIsWaveDisplayFocused)
                            {
                                CutSelectedEvents();
                                Event.current.Use();
                            }
                            break;
                        case "Copy":
                            if (selectedEvents.Count > 0 && bIsWaveDisplayFocused)
                            {
                                CopySelectedEvents();
                                Event.current.Use();
                            }
                            break;
                        case "Paste":
                            if (clippedEvents.Count > 0 && selectedEvents.Count > 0 && bIsWaveDisplayFocused && waveDisplay.bValid)
                            {
                                PasteOverSelectedEvents();
                                Event.current.Use();
                            }
                            break;
                        case "SelectAll":
                            if (editTrack != null && bIsWaveDisplayFocused)
                            {
                                SelectAll();
                                Event.current.Use();
                            }
                            break;
                        case "UndoRedoPerformed":
                            Event.current.Use();
                            break;
                        default:
                            //						Debug.Log("Unhandled command name encountered for Event ExecuteCommand: " + Event.current.commandName);
                            break;
                    }
                }

                if (Event.current.isKey)
                {
                    HandleKeyInput();
                }

                if (Event.current.isMouse ||
                    Event.current.type == EventType.Ignore && Event.current.rawType == EventType.MouseUp)
                {
                    HandleMouseInput();
                }

                // This happens before the mouse-down button (at least on Mac(?)).
                if (Event.current.type == EventType.ContextClick)
                {
                    Vector2 mousePos = GetMousePosition();
                    if (IsWaveDisplayClickableAtLoc(mousePos))
                    {
                        KoreographyEvent evt = waveDisplay.GetEventAtLoc(mousePos,selectedIdx);
                        if (evt != null && !selectedEvents.Contains(evt))
                        {
                            // If an event is being hovered over and is not otherwise already in the
                            //  selection, replace the selection.
                            selectedEvents.Clear();
                            selectedEvents.Add(evt);
                        }
                        else if (evt == null)
                        {
                            // If the mouse isn't over an event, clear the selection to paste where
                            //  the mouse is, rather than simply replae the current selection.
                            selectedEvents.Clear();
                        }

                        GenericMenu menu = new GenericMenu();

                        // Cut/Copy
                        {
                            if (selectedEvents.Count > 0)
                            {
                                menu.AddItem(cutContent, false, CutSelectedEvents);
                                menu.AddItem(copyContent, false, CopySelectedEvents);
                            }
                            else
                            {
                                menu.AddDisabledItem(cutContent);
                                menu.AddDisabledItem(copyContent);
                            }
                        }

                        // Paste.
                        {
                            if (clippedEvents.Count > 0)
                            {
                                // Paste either over the selection or where the mouse is.
                                if (selectedEvents.Count > 0)
                                {
                                    menu.AddItem(pasteContent, false, PasteOverSelectedEvents);
                                    menu.AddItem(pastePayloadContent, false, PastePayloadToSelectedEvents);
                                }
                                else
                                {
                                    int samplePos = waveDisplay.GetSamplePositionOfPoint(mousePos, displayState);

                                    if (bSnapTimingToBeat)
                                    {
                                        samplePos = editKoreo.GetSampleOfNearestBeat(samplePos, displayState.beatSubdivisions);
                                    }

                                    menu.AddItem(pasteContent, false, PasteEventsAtLocation, samplePos);
                                    menu.AddDisabledItem(pastePayloadContent);
                                }
                            }
                            else
                            {
                                menu.AddDisabledItem(pasteContent);
                                menu.AddDisabledItem(pastePayloadContent);
                            }
                        }

                        menu.AddSeparator(string.Empty);

                        // Playhead Anchor
                        {
                            int samplePos = waveDisplay.GetSamplePositionOfPoint(mousePos, displayState);
                            menu.AddItem(playAnchorHereContent, false, SetPlaybackAnchorToLocation, samplePos);

                            if (displayState.playbackAnchorSamplePosition != 0)
                            {
                                menu.AddItem(playAnchorClearContent, false, ClearCustomPlaybackAnchorLocation);
                            }
                        }

                        menu.AddSeparator(string.Empty);

                        // Playback
                        {
                            if (!KoreographyEditor.GetAudioSystemDisabled())
                            {
                                int samplePos = waveDisplay.GetSamplePositionOfPoint(mousePos, displayState);
                                menu.AddItem(playFromHereContent, false, PlayFromSampleLocation, samplePos);
                            }
                            else
                            {
                                menu.AddDisabledItem(playFromHereContent);
                            }
                        }

                        menu.ShowAsContext();

                        Event.current.Use();
                    }
                }

                // Unlike other input, we test for scrolling before drawing anything.  This needs to occur prior to the ScrollView because otherwise the
                //  ScrollView will swallow the input.  This is safe because currently it only affects zoom.
                HandleScrollInput();

                // Early out!  No need to further process a Used event.
                if (Event.current.type == EventType.Used)
                {
                    return;
                }
            }

            viewPosition = EditorGUILayout.BeginScrollView(viewPosition);

            EditorGUILayout.BeginHorizontal();
            {
                // Edit Koreography.
                Koreography newKoreo = EditorGUILayout.ObjectField("Koreography", editKoreo, typeof(Koreography), false) as Koreography;

                if (newKoreo != editKoreo)
                {
                    SetNewEditKoreo(newKoreo);
                }

                if (GUILayout.Button("New Koreography", GUILayout.MaxWidth(115f)))
                {
                    newKoreo = CreateNewKoreography();
                    if (newKoreo != null)
                    {
                        // Handle all of the associated setup.
                        SetNewEditKoreo(newKoreo);
                    }
                }

                GUI.enabled = editKoreo != null;
                if (GUILayout.Button(exportContent, GUILayout.MaxWidth(115f)))
                {
                    if (editKoreo != null)
                    {
                        Exprot(editKoreo);
                    }
                }

                // Help Button (?).
                if (GUILayout.Button(KoreographyEditor.helpContent, KoreographyEditorSkin.helpIcon, GUILayout.MaxWidth(20f)))
                {
                    HelpPanel.OpenWindow();
                }
            }
            EditorGUILayout.EndHorizontal();

            GUI.enabled = editKoreo != null;
            EditorGUI.indentLevel++;

            // Audio Clip.
            EditorGUILayout.BeginHorizontal();
            {
                if (audioLoadMethod == AudioLoadMethod.AudioClip)
                {
                    AudioClip newClip = EditorGUILayout.ObjectField(audioClipContent, (editKoreo == null ? null : editKoreo.SourceClip), typeof(AudioClip), false) as AudioClip;

                    // Only need to proceed if the Koreography is loaded.
                    if (editKoreo != null)
                    {
                        if (newClip != editKoreo.SourceClip)    // If the clip changed, update the system.
                        {
                            if (newClip != null)
                            {
                                // Check that we have a valid clip for the tool, offer to fix if import settings aren't usable.
                                CheckAudioClipValidity(newClip);

                                Undo.RecordObject(editKoreo, "Set Audio Clip");

                                editKoreo.SourceClip = newClip;
                                editKoreo.SampleRate = newClip.frequency;

                                EditorUtility.SetDirty(editKoreo);

                                InitNewClip(newClip);
                            }
                            else
                            {
                                Undo.RecordObject(editKoreo, "Clear Audio Clip");

                                editKoreo.SourceClip = newClip;

                                EditorUtility.SetDirty(editKoreo);

                                ClearEditClip();
                                ClearWaveDisplay();
                            }
                        }
                        else if (newClip != null && !waveDisplay.bValid)    // If the clip is valid but the waveDisplay is not, fix it.
                        {
                            InitNewClip(newClip);
                        }

                        // Provide mechanism for user to be able to trigger Invalid AudioClip re-import.
                        if (EditClip != null && waveDisplay.bValid && !waveDisplay.HasAudioData() && !IsAudioClipValid(EditClip))
                        {
                            EditorGUILayout.LabelField("Cannot generate waveform!", EditorStyles.boldLabel, GUILayout.Width(185f));
                            if (GUILayout.Button(fixClipContent, GUILayout.MaxHeight(EditorGUIUtility.singleLineHeight), GUILayout.MaxWidth(60f)))
                            {
                                if (CheckAudioClipValidity(EditClip))
                                {
                                    // Trigger the regeneration of waveform data, etc.
                                    InitNewClip(EditClip);
                                }
                            }
                        }
                    }
                }
                else if (audioLoadMethod == AudioLoadMethod.AudioFile)
                {
                    string audioFilePath = (editKoreo == null) ? string.Empty : editKoreo.SourceClipPath;

                    // LoadString is used by both the text field and the Load Button.  There is no way for BOTH of these methods
                    //  to fill in a string at the same time.  Therefore, we can simply check that the audioFilePath is
                    //  different from this to see if an update occurred.
                    // This control can either be deleted to clear the path or pasted into to set it.
                    string loadString = EditorGUILayout.TextField(audioFileContent, audioFilePath);

                    Color prevColor = GUI.backgroundColor;
                    GUI.backgroundColor = string.IsNullOrEmpty(loadString) ? Color.yellow : prevColor;
                    if (GUILayout.Button(loadAudioFileContent, EditorStyles.miniButton, GUILayout.Width(95f)))
                    {
                        loadString = EditorUtility.OpenFilePanel("Load Audio File", string.IsNullOrEmpty(audioFilePath) ? Application.dataPath : Path.Combine(Application.dataPath, audioFilePath), string.Empty);

                        // Only returns an empty string if the user cancels.
                        if (string.IsNullOrEmpty(loadString))
                        {
                            loadString = audioFilePath;
                        }
                    }
                    GUI.backgroundColor = prevColor;

                    // Detect a change.
                    if (loadString != audioFilePath)
                    {
                        // If we're empty, clear out!
                        if (string.IsNullOrEmpty(loadString))
                        {
                            Undo.RecordObject(editKoreo, "Clear Audio File Path");

                            editKoreo.SourceClipPath = loadString;

                            EditorUtility.SetDirty(editKoreo);

                            ClearAudioFileClip();

                            ClearEditClip();
                            // TODO: Clear any pre-existing data from the waveDisplay?  Doing this with ClearWaveDisplay causes
                            //  the editTrack to remain changeable in the dropdown but this isn't reflected when the EditClip is
                            //  reset (shows same track contents from previously viewed track... strange).
                        }
                        else
                        {
                            // Transform the returned absolute path to a relative path.
                            loadString = KoreographerMiscUtils.AbsoluteToRelativePath(loadString, Application.dataPath);

                            // Can be null, especially if the path is invalid.
                            audioFileClip = KoreographyEditor.GetAudioClipFromPath(loadString);

                            if (audioFileClip != null)
                            {
                                Undo.RecordObject(editKoreo, "Set Audio File Path");

                                editKoreo.SourceClipPath = loadString;
                                editKoreo.SampleRate = audioFileClip.frequency;

                                EditorUtility.SetDirty(editKoreo);

                                InitNewClip(audioFileClip);
                            }
                        }
                    }
                }

                // Show the selector if the option to load by file is enabled.
                if (KoreographyEditor.ShowAudioFileImportOption)
                {
                    // Load type selector.
                    if (GUILayout.Toggle(audioLoadMethod == AudioLoadMethod.AudioClip, loadClipContent, radioButtonStyle, GUILayout.Width(105f))
                        && audioLoadMethod != AudioLoadMethod.AudioClip)
                    {
                        audioLoadMethod = AudioLoadMethod.AudioClip;

                        // Clear out any potential Audio File.
                        ClearAudioFileClip();

                        // Init the AudioClip if one existed.
                        if (editKoreo.SourceClip != null)
                        {
                            InitNewClip(editKoreo.SourceClip);
                        }
                    }
                    if (GUILayout.Toggle(audioLoadMethod == AudioLoadMethod.AudioFile, loadFileContent, radioButtonStyle, GUILayout.Width(75f))
                        && audioLoadMethod != AudioLoadMethod.AudioFile)
                    {
                        audioLoadMethod = AudioLoadMethod.AudioFile;

                        // Clear out any potential AudioClip.
                        ClearEditClip();

                        // Init the Audio File's AudioClip if one existed.
                        audioFileClip = KoreographyEditor.GetAudioClipFromPath(editKoreo.SourceClipPath);

                        if (audioFileClip != null)
                        {
                            InitNewClip(audioFileClip);
                        }
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField("Tempo Section Settings:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.BeginHorizontal();

            // Section list.
            {
                string[] sectionNames = new string[] { "" };

                int selectedIdx = 0;
                if (editKoreo != null)
                {
                    sectionNames = editKoreo.GetTempoSectionNames();

                    // Section names need to be unique to properly show up in the Popup.
                    for (int i = 0; i < sectionNames.Length; ++i)
                    {
                        sectionNames[i] = (i + 1) + ") " + sectionNames[i];
                    }

                    selectedIdx = editTempoSectionIdx;  // This index is valid if editKoreo is valid.
                }

                int newSelectedIdx = EditorGUILayout.Popup("Tempo Section to Edit", selectedIdx, sectionNames, GUILayout.MaxWidth(300f));

                // We guard this way as editTempoSectionIdx *can* be -1 in certain circumstances, marking it as invalid.
                if (selectedIdx != newSelectedIdx)
                {
                    editTempoSectionIdx = newSelectedIdx;
                }
            }

            GUI.enabled = (editTempoSectionIdx >= 0);

            // This takes advantage of the fact that editTempoSectionIdx is only a valid index when editKoreo exists.
            TempoSectionDef editTempoSection = (editTempoSectionIdx >= 0) ? editKoreo.GetTempoSectionAtIndex(editTempoSectionIdx) : null;

            // Allow editing of tempo section name.
            {
                EditorGUIUtility.labelWidth = 100f;
                string oldName = editTempoSection != null ? editTempoSection.SectionName : string.Empty;
                string newName = EditorGUILayout.TextField("Section Name", oldName, GUILayout.MaxWidth(200f));
                EditorGUIUtility.labelWidth = 0f;

                if (newName != oldName)
                {
                    Undo.RecordObject(editKoreo, "Set Tempo Section Name");
                    editTempoSection.SectionName = newName;
                    EditorUtility.SetDirty(editKoreo);
                }
            }

            // Add Section Before/After buttons.
            {
                GUILayout.FlexibleSpace();

                GUI.enabled = (editTempoSection != null) && (editKoreo != null) && (editKoreo.GetNumTempoSections() > 1);

                if (GUILayout.Button("Delete", GUILayout.MaxWidth(50f)))
                {
                    Undo.RecordObject(editKoreo, "Delete Tempo Section " + editTempoSection.SectionName);

                    editKoreo.RemoveTempoSectionAtIndex(editTempoSectionIdx);

                    // Ensure the new editTempoSectionIdx is still valid.
                    editTempoSectionIdx = Mathf.Clamp(editTempoSectionIdx, 0, editKoreo.GetNumTempoSections() - 1);

                    EditorUtility.SetDirty(editKoreo);
                    Repaint();
                }

                GUI.enabled = (editTempoSection != null);

                if (GUILayout.Button(new GUIContent("Insert New Before", "Inserts a new Tempo Section before the current one."), GUILayout.MaxWidth(115f)))
                {
                    Undo.RecordObject(editKoreo, "Insert New Tempo Section");

                    TempoSectionDef newSection = editKoreo.InsertTempoSectionAtIndex(editTempoSectionIdx);

                    newSection.StartSample = editTempoSection.StartSample;
                    newSection.SamplesPerBeat = editTempoSection.SamplesPerBeat;

                    editTempoSection = newSection;

                    EditorUtility.SetDirty(editKoreo);
                    Repaint();
                }

                if (GUILayout.Button(new GUIContent("Insert New After", "Inserts a new Tempo Section after the current one."), GUILayout.MaxWidth(115f)))
                {
                    Undo.RecordObject(editKoreo, "Insert New Tempo Section");

                    TempoSectionDef newSection = editKoreo.InsertTempoSectionAtIndex(++editTempoSectionIdx);

                    newSection.StartSample = editTempoSection.StartSample;
                    newSection.SamplesPerBeat = editTempoSection.SamplesPerBeat;

                    editTempoSection = newSection;

                    EditorUtility.SetDirty(editKoreo);
                    Repaint();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();

            // StartSample, SamplesPerBeat/BPM
            {
                // Disallow editing the first section's StartSample.
                GUI.enabled = (editKoreo != null && editTempoSectionIdx > 0);

                int newStartSample = EditorGUILayout.IntField(startSampContent, (editTempoSection != null) ? editTempoSection.StartSample : 0);
                if (editTempoSection != null && editTempoSection.StartSample != newStartSample)
                {
                    Undo.RecordObject(editKoreo, "Set Tempo Section Start Sample");

                    editTempoSection.StartSample = newStartSample;

                    // Verify that we have the correct ordering.
                    editKoreo.EnsureTempoSectionOrder();

                    EditorUtility.SetDirty(editKoreo);
                }

                // Add some space to separate the two float fields.
                GUILayout.Space(95f);

                EditorGUIUtility.labelWidth = 100f;
                EditorGUIUtility.fieldWidth = 80f;
                if (bShowBPM)
                {
                    GUI.enabled = (EditClip != null) && (editTempoSection != null);
                    // BPM Settings
                    {
                        // Default to 44100hz if no editClip exists.
                        int frequency = (EditClip != null) ? EditClip.frequency : 44100;
                        double bpm = (editTempoSection != null) ? editTempoSection.GetBPM(frequency) : 0d;
#if UNITY_4_5 || UNITY_4_6 || UNITY_4_7
						double newBPM = (double)EditorGUILayout.FloatField(tempoContent, (float)bpm);
#else
                        double newBPM = EditorGUILayout.DoubleField(tempoContent, bpm);
#endif
                        if (newBPM != bpm && newBPM > 0d && editTempoSection != null)
                        {
                            Undo.RecordObject(editKoreo, "Set Tempo Section BPM");
                            editTempoSection.SamplesPerBeat = (double)frequency / (newBPM / 60d);
                            EditorUtility.SetDirty(editKoreo);
                        }
                    }
                }
                else
                {
                    GUI.enabled = editTempoSection != null;
#if UNITY_4_5 || UNITY_4_6 || UNITY_4_7
					double newSamplesPerBeat = (double)EditorGUILayout.FloatField(tempoContent, (editTempoSection != null) ? (float)editTempoSection.SamplesPerBeat : 0f);
#else
                    double newSamplesPerBeat = EditorGUILayout.DoubleField(tempoContent, (editTempoSection != null) ? editTempoSection.SamplesPerBeat : 0d);
#endif
                    if (editTempoSection != null && editTempoSection.SamplesPerBeat != newSamplesPerBeat)
                    {
                        Undo.RecordObject(editKoreo, "Set Tempo Section Samples Per Beat");
                        editTempoSection.SamplesPerBeat = newSamplesPerBeat;
                        EditorUtility.SetDirty(editKoreo);
                    }
                }
                EditorGUIUtility.labelWidth = 0f;
                EditorGUIUtility.fieldWidth = 0f;

                if (GUILayout.Toggle(bShowBPM, beatsPerMinContent, radioButtonStyle, GUILayout.Width(40f)))
                {
                    bShowBPM = true;
                }
                if (GUILayout.Toggle(!bShowBPM, sampsPerBeatContent, radioButtonStyle, GUILayout.Width(118f)))
                {
                    bShowBPM = false;
                }
            }
            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();

            // Beats Per Measure, Reset Measure
            {
                GUI.enabled = editTempoSection != null;

                // Beats Per Measure
                int newBeatsPerMeasure = EditorGUILayout.IntField(beatsPerBarContent, (editTempoSection != null) ? editTempoSection.BeatsPerMeasure : 0);
                if (editTempoSection != null && editTempoSection.BeatsPerMeasure != newBeatsPerMeasure)
                {
                    Undo.RecordObject(editKoreo, "Set Tempo Section Beats Per Measure");
                    editTempoSection.BeatsPerMeasure = newBeatsPerMeasure;
                    EditorUtility.SetDirty(editKoreo);
                }

                GUILayout.Space(95f);

                // Reset Measure
                bool newResetMeasure = EditorGUILayout.Toggle(resetMeasureContent, (editTempoSection != null) ? editTempoSection.DoesStartNewMeasure : true);
                if (editTempoSection != null && editTempoSection.DoesStartNewMeasure != newResetMeasure)
                {
                    Undo.RecordObject(editKoreo, "Toggle Tempo Section Starts New Measure");
                    editTempoSection.DoesStartNewMeasure = newResetMeasure;
                    EditorUtility.SetDirty(editKoreo);
                }

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.EndHorizontal();

            GUI.enabled = editKoreo != null && editKoreo.GetNumTracks() > 0;

            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField("Track Settings:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.BeginHorizontal();

            // Track list.
            {
                string[] trackNames = new string[] { "" };

                selectedIdx = 0;

                if (editKoreo != null)
                {
                    trackNames = editKoreo.GetEventIDs();
                    if (editTrack != null)
                    {
                        // TODO: Check that the selectedIdx isn't -1?
                        selectedIdx = editKoreo.GetIndexOfTrack(editTrack);
                    }

                }

                int newSelectedIdx = EditorGUILayout.Popup("Track to Edit", selectedIdx, trackNames);


                if (selectedIdx != newSelectedIdx)
                {
                    selectedIdx = newSelectedIdx;
                    Debug.Log("selectIdx:---------->" + selectedIdx);
                    // For these to be different we MUST have an editKoreo.
                    SetNewEditTrack(editKoreo.GetTrackAtIndex(newSelectedIdx));
                }
            }

            GUI.enabled = editTrack != null;

            // Make the track name adjustable.
            {
                EditorGUIUtility.labelWidth = 106f;
                string oldID = editTrack != null ? editTrack.EventID : string.Empty;
                string newID = EditorGUILayout.TextField(KoreographyEditor.trackEventIDContent, oldID, GUILayout.MaxWidth(300f));
                EditorGUIUtility.labelWidth = 0f;

                if (newID != oldID)
                {
                    if (!editKoreo.DoesTrackWithEventIDExist(newID))
                    {
                        Undo.RecordObject(editTrack, "Set Event ID");
                        // Won't get here unless editTrack is valid (GUI is disabled otherwise).
                        editTrack.EventID = newID;
                        EditorUtility.SetDirty(editTrack);
                    }
                }
            }

            GUI.enabled = editKoreo != null;

            // Load an existing track into the Koreography.
            if (GUILayout.Button(KoreographyEditor.trackLoadContent, GUILayout.MaxWidth(60f)))
            {
                // Get an asset path and massage it to work with the loading mechanisms.
                string targetPath = EditorUtility.OpenFilePanel("Select a KoreographyTrack Asset...", KoreographyEditor.AssetPath, "asset");

                // The path is empty if the user cancels.
                if (!string.IsNullOrEmpty(targetPath))
                {
                    // Verify that we have a valid path.
                    if (targetPath.Contains(Application.dataPath))
                    {
                        // The AssetDatabase uses relative paths, starting at the project root.
                        string dbPath = targetPath.Replace(Application.dataPath, "Assets");

                        // Load the Koreography Track from the database.
                        KoreographyTrack loadTrack = AssetDatabase.LoadAssetAtPath(dbPath, typeof(KoreographyTrack)) as KoreographyTrack;

                        if (loadTrack != null)
                        {
                            // Won't get here unless the editKoreo is around.  The GUI button won't work otherwise.
                            if (editKoreo.HasTrack(loadTrack))
                            {
                                // This doesn't change any links because it's already loaded.  Simply switch to it.
                                SetNewEditTrack(loadTrack);
                            }
                            else if (!editKoreo.CanAddTrack(loadTrack))
                            {
                                EditorUtility.DisplayDialog("Load Error Occurred!", "The selected Track could not be loaded.  Is the track already in the Koreography?  " +
                                    "Or is there another Track with the same Event ID (" + loadTrack.EventID + ") already in the Koreography?", "Okay");
                            }
                            else
                            {
                                // Record the object and then commit!
                                Undo.RecordObject(editKoreo, "Load Track");
                                editKoreo.AddTrack(loadTrack);
                                EditorUtility.SetDirty(editKoreo);
                                SetNewEditTrack(loadTrack);
                            }

                            // Store the full path.
                            KoreographyEditor.AssetPath = targetPath;
                        }
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Whoops!", "Cannot load KoreographyTracks from outside the current project.  " +
                                                    "Please choose an asset in the current Unity project.", "Okay");
                    }
                }
            }

            // Create a new track and add to the Koreography.
            if (GUILayout.Button(KoreographyEditor.trackNewContent, GUILayout.MaxWidth(60f)))
            {
                KoreographerGUIUtils.ShowTypeSelectorMenu(newTrackButtRect, KoreographyTrackTypeUtils.EditableTrackTypes, OnNewTrackOptionSelected);
            }

            // Store the location of the above button. We can only get into its block with a
            //  mouse or keyboard event.
            if (Event.current.type == EventType.Repaint)
            {
                newTrackButtRect = GUILayoutUtility.GetLastRect();
            }

            EditorGUI.BeginDisabledGroup(editKoreo == null || editTrack == null);
            {
                // Remove the currently selected track from the Koreography.
                if (GUILayout.Button(KoreographyEditor.trackRemoveContent, GUILayout.Width(65f)))
                {
                    int idx = editKoreo.GetIndexOfTrack(editTrack);

                    // Remove the track if it's in the Koreography.
                    if (idx >= 0)
                    {
                        editKoreo.RemoveTrack(editTrack);
                    }

                    // Set a new track.  Null things out if we no longer have a valid track.
                    int numTracks = editKoreo.GetNumTracks();
                    if (numTracks > 0)
                    {
                        SetNewEditTrack(editKoreo.GetTrackAtIndex(Mathf.Clamp(idx, 0, numTracks - 1)));
                    }
                    else
                    {
                        SetNewEditTrack(null);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;

            GUI.enabled = EditClip != null;

            // Handle markup generation.
            {
                // Start by checking if an event exists and, if so, continue it!
                if (EditClip != null && buildEvent != null && IsPlaying())
                {
                    ContinueNewEvent(GetCurrentEstimatedMusicSample());
                }
            }

            // Buttons.
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUI.BeginDisabledGroup(KoreographyEditor.GetAudioSystemDisabled());
                {
                    // Playback controls.
                    if (IsPlaying())
                    {
                        if (GUILayout.Button(KoreographyEditorSkin.pauseTex, playButtonStyle))
                        {
                            PauseAudio();

                            // Without this the Play/Pause Button itself takes focus.
                            //  Which is meaningless.
                            FocusWaveDisplayWindow();
                        }
                    }
                    else
                    {
                        if (GUILayout.Button(KoreographyEditorSkin.playTex, playButtonStyle))
                        {
                            // Playback always causes window focus.
                            PlayAudio();
                        }
                    }

                    if (GUILayout.Button(KoreographyEditorSkin.stopTex, stopButtonStyle))
                    {
                        StopAudio();

                        // Without this the Stop Button itself takes focus.  Which is meaningless.
                        FocusWaveDisplayWindow();
                    }
                }
                EditorGUI.EndDisabledGroup();

                // Used for display below here.
                EditorGUI.BeginChangeCheck();
                {
                    // Visualizer Button.
                    bool bVizOpen = visualizerWindow != null;
                    if (GUILayout.Toggle(bVizOpen, visContent, EditorStyles.miniButton, GUILayout.Height(20f)) != bVizOpen)
                    {
                        ToggleVisualizer();
                    }

                    GUILayout.FlexibleSpace();

                    // Interaction Mode.
                    if (GUILayout.Toggle(controlMode == ControlMode.Select, new GUIContent("Select", "(a) Put the editor in Selection mode"), EditorStyles.miniButtonLeft, GUILayout.Width(45f), GUILayout.Height(20f)))
                    {
                        controlMode = ControlMode.Select;
                    }
                    if (GUILayout.Toggle(controlMode == ControlMode.Author, new GUIContent("Draw", "(s) Put the editor in Draw mode"), EditorStyles.miniButtonMid, GUILayout.Width(45f), GUILayout.Height(20f)))
                    {
                        controlMode = ControlMode.Author;
                    }
                    if (GUILayout.Toggle(controlMode == ControlMode.Clone, new GUIContent("Clone", "(d) Put the editor in Clone mode"), EditorStyles.miniButtonRight, GUILayout.Width(45f), GUILayout.Height(20f)))
                    {
                        controlMode = ControlMode.Clone;
                    }

                    GUILayout.FlexibleSpace();

                    // Duration type.
                    if (GUILayout.Toggle(bCreateOneOff, new GUIContent("OneOff", "(z) Created events are mapped to a single sample"), EditorStyles.miniButtonLeft, GUILayout.Width(50f), GUILayout.Height(20f)))
                    {
                        bCreateOneOff = true;
                    }
                    if (GUILayout.Toggle(!bCreateOneOff, new GUIContent("Span", "(x) Created events span multiple samples"), EditorStyles.miniButtonRight, GUILayout.Width(50f), GUILayout.Height(20f)))
                    {
                        bCreateOneOff = false;
                    }

                    GUILayout.FlexibleSpace();

                    // Payload selection.
                    EditorGUIUtility.labelWidth = 50f;
                    EditorGUIUtility.fieldWidth = 100f;

                    // +1 to string index input and -1 from string index output.  This keeps the index properly
                    //  in the range of the actual Type list which DOESN'T have the "No Payload" option.
                    currentPayloadTypeIdx = EditorGUILayout.Popup("Payload", currentPayloadTypeIdx + 1, payloadTypeNames.ToArray(), payloadFieldStyle) - 1;

                    GUILayout.FlexibleSpace();

#if !KOREO_NON_PRO
                    // Opens and focuses the Analysis Settings panel.
                    if (GUILayout.Button(trackAnalysisContent, EditorStyles.miniButton, GUILayout.Width(55f), GUILayout.Height(20f)))
                    {
                        analysisWindow = AnalysisPanel.OpenWindow(this);
                    }
#else
					EditorGUI.BeginDisabledGroup(true);
					{
						GUILayout.Button(trackAnalysisContent, EditorStyles.miniButton, GUILayout.Width(55f), GUILayout.Height(20f));
					}
					EditorGUI.EndDisabledGroup();
#endif

                    GUILayout.FlexibleSpace();

                    // Zoom.
                    {
                        //------------WARNING------------//------------WARNING------------
                        // Modifications here should also be made in the mouse scroll 
                        //  handling code! If you change something here, ensure that it is
                        //  mirrored in that area!
                        //------------WARNING------------//------------WARNING------------

                        EditorGUILayout.LabelField("Zoom", GUILayout.MaxWidth(40f));

                        // Upper bound of the logarithmic scale.
                        float maxLog = (float)System.Math.Log((double)maxSamplesPerPack);

                        // The percent [0,1] value at which we switch from linear to logarithmic scales.
                        float linSwitchPct = Mathf.InverseLerp(0f, maxLog, (float)System.Math.Log((double)KoreographyEditor.MaxLinearZoomPackSize));

                        // Calculate the current delta setting based on the present zoom state.
                        float delta = 0f;
                        if (displayState.samplesPerPack < KoreographyEditor.MaxLinearZoomPackSize)
                        {
                            // Linear scale.
                            delta = Mathf.InverseLerp(1f, KoreographyEditor.MaxLinearZoomPackSize, displayState.samplesPerPack) * linSwitchPct;
                        }
                        else
                        {
                            // Logarithmic scale.
                            float curLog = (float)System.Math.Log((double)displayState.samplesPerPack);
                            delta = Mathf.InverseLerp(0f, maxLog, curLog);
                        }

                        float newDelta = (float)GUILayout.HorizontalSlider(delta, 0f, 1f, GUILayout.MinWidth(250f));

                        // Update the scroll position if the samplesPerPack (zoom factor) changes.
                        if (newDelta != delta)
                        {
                            // Zoom around the center of the display.
                            //	NOTE: centering currently ignores visuals when the window must be horizontally scrolled.
                            int zoomOffsetInPixels = waveDisplay.GetPixelOffsetInChannelAtLoc(fullWaveContentRect.center);

                            // Calculate the new zoom amount based on the new delta value.
                            int newSamps = 0;
                            if (newDelta < linSwitchPct)
                            {
                                newSamps = (int)Mathf.Lerp(1f, KoreographyEditor.MaxLinearZoomPackSize, newDelta / linSwitchPct);
                            }
                            else
                            {
                                newSamps = (int)Mathf.Exp(Mathf.Lerp(0f, maxLog, newDelta));
                            }

                            // Commit the newly calculated zoom state to the view.
                            SetNewSamplesPerPack(newSamps, zoomOffsetInPixels);
                        }
                    }
                }
                if (EditorGUI.EndChangeCheck())
                {
                    FocusWaveDisplayWindow();
                    GUI.changed = false;
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            {
                if (KoreographyEditor.GetAudioSystemDisabled())
                {
                    EditorGUILayout.HelpBox("Koreographer cannot playback AudioClip because Unity's Audio system has been disabled in Project Settings.", MessageType.Warning);

                    EditorGUILayout.BeginVertical();
                    {
                        GUILayout.Space(EditorGUIUtility.singleLineHeight - 2f);

                        GUI.backgroundColor = new Color(.0f, .85f, .0f);
                        if (GUILayout.Button(enableAudioContent))
                        {
                            bool bEnable = EditorUtility.DisplayDialog("Enable Unity's Internal Audio System?",
                                                                       "Disabling the Audio System is common practice when using third-party Audio Integrations.  If you are using such a system, be " +
                                                                       "sure to reset this in \"Edit->Project Settings->Audio\" once you have finished using Koreographer.\n\n" +
                                                                       "Enabling Unity's Audio System will allow Koreographer to play back audio.",
                                                                       "OK",
                                                                       "Cancel");
                            if (bEnable)
                            {
                                KoreographyEditor.SetAudioSystemDisabled(false);
                            }
                        }
                        GUI.backgroundColor = Color.white;
                    }
                    EditorGUILayout.EndVertical();
                }

                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();

#if !KOREO_NON_PRO
            // Analysis Range Selector
            if (analysisWindow != null && analysisWindow.IsEditorRangeSelection)
            {
                float start = analysisRange.x;
                float end = analysisRange.y;

                EditorGUI.BeginChangeCheck();
                {
                    GUI.color = Color.green;
                    EditorGUILayout.MinMaxSlider(ref start, ref end, 0f, 1f);
                    GUI.color = Color.white;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    analysisRange.x = start;
                    analysisRange.y = end;
                }

                // Repaint is triggered by AnalysisPanel to get the slider updated.
                //  This stops us from resending the same message back to the analysisWindow.
                //  Updates from the AnalysisPanel should only come from UI interactions, which
                //  would imply focus.  In those cases, the signal to "repaint" came from there.
                if (focusedWindow == this)
                {
                    UpdateAnalysisRange();
                }
            }
#endif

            {
                // Wave Display Area.
                if (waveDisplay.bValid)
                {
                    displayState.playheadSamplePosition = GetCurrentEstimatedMusicSample();

                    // Handle display.
                    {
                        // Add some space for the LCD readouts.
                        GUILayout.Space(14f);
                        Rect lcdRect = GUILayoutUtility.GetLastRect();

                        float waveHeight = 400f;
                        float viewHeight = waveHeight + GUI.skin.horizontalScrollbar.fixedHeight;

                        Rect posRect = GUILayoutUtility.GetRect(KoreographyEditor.MinWaveViewWidth, float.MaxValue, viewHeight, viewHeight);

                        // Adjust the position rect for the waveView inlay.
                        posRect.width = Mathf.Max(KoreographyEditor.MinWaveViewWidth, posRect.width) - (2f * KoreographyEditor.HorizontalPadding);
                        posRect.height = viewHeight;
                        posRect.x += KoreographyEditor.HorizontalPadding;

                        // Store the width for calculation in case we get a new clip set (or somesuch).
                        if (Event.current.type == EventType.Repaint)
                        {
                            if (curWaveViewWidth != posRect.width)
                            {
                                curWaveViewWidth = posRect.width;
                                UpdateMaxSamplesPerPack();
                            }
                        }
                        else
                        {
                            // Restore original width so we're not at the EventType.Layout's "1" (which breaks stuff).
                            posRect.width = curWaveViewWidth;
                        }

                        // Handle playback state.  Ensure that this is done AFTER we update the Max Samples Per Pixel as it
                        //  can affect how this math works.
                        if (IsPlaying() && bScrollWithPlayhead)
                        {
                            // Number of packs to the playhead.
                            int packDistanceToPlayhead = WaveDisplay.pixelDistanceToPlayheadMarker;
                            // The sample pack the playhead is currently over.
                            int playheadPack = displayState.playheadSamplePosition / displayState.samplesPerPack;

                            // Set the firstPackPos to the playheadPack with an offset based on the packDistanceToPlayhead.
                            displayState.firstPackPos = -(playheadPack - (packDistanceToPlayhead - displayState.waveStartMax));

                            // Note that with weird settings for displayState.waveStartMax and packDistanceToPlayhead
                            //  (specifically when packDistanceToPlayhead > waveStartMax) it is possible to get values
                            //  for the scroll position that go beyond the bounds of the scroll.  The scroll bar is
                            //  clamped in this case.  However, the position is not.  We render the waveform and
                            //  content separately.  While the values and visuals work, the scrollbar remains fixed
                            //  in this case.
                            SetScrollPosition((float)-displayState.firstPackPos);
                        }
                        else
                        {
                            displayState.firstPackPos = -(int)scrollPosition.x;
                        }

                        float viewWidth = posRect.width;
                        float fullScrollExtents = viewWidth;

                        if (EditClip != null)
                        {
                            // The total number of packs (also the width of the waveform).
                            int totalPacks = displayState.GetNumPacks(EditClip.samples);

                            // Add the full extents at beginning to the full extents at the end.
                            fullScrollExtents = (displayState.waveStartMax + totalPacks) + (viewWidth - displayState.waveEndMin);
                        }

                        // Create an area that defines length of the entire wave.
                        Rect contentRect = new Rect(0f, 0f, fullScrollExtents, waveHeight);

                        // Scroll area.  This is only used to draw the scroll bar and reserve the requisite space internally.
                        Vector2 newScrollPos = GUI.BeginScrollView(posRect, scrollPosition, contentRect, true, false);
                        GUI.EndScrollView();

                        // Update scroll position.
                        if (newScrollPos.x != scrollPosition.x)
                        {
                            SetScrollPosition(newScrollPos.x);

                            // Handle seeking while playing back!
                            if (IsPlaying() && bScrollWithPlayhead &&
                                //							    scrollPosition.x < fullScrollExtents - viewWidth && 	// If we get to full don't continuously reset the timeSamples.
                                //  The above line is currently useless: never true (with current settings)!
                                scrollPosition.x > 256 / displayState.samplesPerPack)   // If we get to the beginning, don't continuously reset the timeSamples.
                            {
                                ScanToSample((int)scrollPosition.x * displayState.samplesPerPack);
                            }
                        }

                        // This is used to detect when the scrollbar is clicked.  The WaveContentRect is adjusted to fit only the wave contents.
                        //  The difference between these two rects is, essentially, the scrollbar.  The movement of 6 pixels means that there's likely
                        //  a clickable area near the top but this should be fine for now.
                        if (Event.current.type == EventType.Repaint)
                        {
                            fullWaveContentRect = posRect;
                        }

                        // TODO: Fix this.  It should be posRect with some adjustments.  Current adjustments are customized to work and are partially
                        //  handled in the WaveDisplay class (maybe - needs verification).
                        Rect waveBoxRect = GUILayoutUtility.GetLastRect();
                        waveBoxRect.width = viewWidth;
                        waveBoxRect.height = waveHeight;
                        waveBoxRect.x += KoreographyEditor.HorizontalPadding;

#if !(UNITY_4_5 || UNITY_4_6 || UNITY_4_7)
                        // Make sure we fill up with AudioData if it's finally loaded.
                        if (Event.current.type == EventType.Repaint &&
                            !waveDisplay.HasAudioData() && // The WaveDisplay doesn't have any data yet and
                            EditClip != null && // We have a valid AudioClip reference set
                            EditClip.loadState == AudioDataLoadState.Loaded && // And the background loading is finished!
                            IsAudioClipValid(EditClip))                         // And the AudioClip can be used to access sample data
                        {
                            waveDisplay.SetAudioData(EditClip);
                        }
#endif
                        // Send a COPY of the displayState so other folks can't change it?!
                        waveDisplay.Draw(waveBoxRect, displayState, editKoreo, bShowPlayhead, (dragStartPos == dragEndPos) ? selectedEvents : eventsToHighlight);

                        // LCD Readouts.
                        {
                            if (Event.current.type == EventType.Repaint)
                            {
                                lcdRects.Clear();
                            }

                            lcdRect.yMin += waveScrollAreaStyle.margin.top;
                            lcdRect.height += 2f;
                            lcdRect.width = viewWidth + waveScrollAreaStyle.margin.horizontal;
                            lcdRect.xMin = 1f;

                            GUI.BeginGroup(lcdRect);
                            {
                                Rect lcd = new Rect();

                                // Left-most-sample LCD.
                                lcd.width = 150f;
                                lcd.height = lcdRect.height;
                                lcd.xMin = KoreographyEditorSkin.lcdStyleLeft.margin.left - 1f;

                                //string solarTimeFmt = @"{0:d\.hh\:mm\:ss\.fff}";	// For TimeSpan.ToString(fmt) - not supported by Unity!
                                string solarTimeFmt = "{0:00':'}{1:00':'}{2:00'.'}{3:000}";
                                System.TimeSpan solarTime;
                                string output = string.Empty;

                                int startSample = displayState.GetFirstVisiblePackSample();

                                switch (lcdMode)
                                {
                                    case LCDDisplayMode.SampleTime:
                                        output = string.Format("sample {0:0}", startSample);
                                        break;
                                    case LCDDisplayMode.MusicTime:
                                        output = GetMusicTimeForDisplayFromSample(startSample);
                                        break;
                                    case LCDDisplayMode.SolarTime:
                                        solarTime = System.TimeSpan.FromSeconds((double)startSample / (double)EditClip.frequency);
                                        output = string.Format(solarTimeFmt, solarTime.Hours, solarTime.Minutes, solarTime.Seconds, solarTime.Milliseconds);
                                        break;
                                    default:
                                        break;
                                }

                                GUI.Label(lcd, output, KoreographyEditorSkin.lcdStyleLeft);
                                if (Event.current.type == EventType.Repaint)
                                {
                                    Rect lcdScreenRect = lcd;
                                    lcdScreenRect.y = lcdRect.y;
                                    lcdRects.Add(lcdScreenRect);
                                }

                                // Currently playing sample.
                                if (bShowPlayhead)
                                {
                                    lcd.x = (lcdRect.width - lcd.width) * 0.5f;

                                    switch (lcdMode)
                                    {
                                        case LCDDisplayMode.SampleTime:
                                            output = string.Format("sample {0:0}", displayState.playheadSamplePosition);
                                            break;
                                        case LCDDisplayMode.MusicTime:
                                            output = GetMusicTimeForDisplayFromSample(displayState.playheadSamplePosition);
                                            break;
                                        case LCDDisplayMode.SolarTime:
                                            solarTime = System.TimeSpan.FromSeconds((double)displayState.playheadSamplePosition / (double)EditClip.frequency);
                                            output = string.Format(solarTimeFmt, solarTime.Hours, solarTime.Minutes, solarTime.Seconds, solarTime.Milliseconds);
                                            break;
                                        default:
                                            break;
                                    }

                                    GUI.Label(lcd, output, KoreographyEditorSkin.lcdStyleCenter);
                                    if (Event.current.type == EventType.Repaint)
                                    {
                                        Rect lcdScreenRect = lcd;
                                        lcdScreenRect.y = lcdRect.y;
                                        lcdRects.Add(lcdScreenRect);
                                    }
                                }

                                // Right-most-sample LCD.
                                lcd.x = lcdRect.width - (lcd.width + KoreographyEditorSkin.lcdStyleRight.margin.right);

                                int totalPackWidth = waveDisplay.GetChannelPixelWidthForWindow((int)waveBoxRect.width);
                                // The total possible number of packs in view minus an offset that will go very negative.
                                int endSample = (totalPackWidth - (displayState.waveStartMax + displayState.firstPackPos)) * displayState.samplesPerPack;
                                // Then effectively clamped.
                                endSample = (EditClip != null && endSample > EditClip.samples) ? EditClip.samples : endSample;

                                switch (lcdMode)
                                {
                                    case LCDDisplayMode.SampleTime:
                                        output = string.Format("sample {0:0}", endSample);
                                        break;
                                    case LCDDisplayMode.MusicTime:
                                        output = GetMusicTimeForDisplayFromSample(endSample);
                                        break;
                                    case LCDDisplayMode.SolarTime:
                                        solarTime = System.TimeSpan.FromSeconds((double)endSample / (double)EditClip.frequency);
                                        output = string.Format(solarTimeFmt, solarTime.Hours, solarTime.Minutes, solarTime.Seconds, solarTime.Milliseconds);
                                        break;
                                    default:
                                        break;
                                }

                                GUI.Label(lcd, output, KoreographyEditorSkin.lcdStyleRight);
                                if (Event.current.type == EventType.Repaint)
                                {
                                    Rect lcdScreenRect = lcd;
                                    lcdScreenRect.y = lcdRect.y;
                                    lcdRects.Add(lcdScreenRect);
                                }
                            }
                            GUI.EndGroup();
                        }
                    }

                    // Draw the selection box.
                    if (dragStartPos != dragEndPos)
                    {
                        Color bgColor = GUI.backgroundColor;
                        Color newBGColor = Color.gray;
                        newBGColor.a = 0.25f;
                        GUI.backgroundColor = newBGColor;

                        GUI.Box(GetDragAreaRect(), "", KoreographyEditorSkin.box);

                        GUI.backgroundColor = bgColor;
                    }
                }

                GUILayout.BeginHorizontal();

                GUI.changed = false;
                bSnapTimingToBeat = EditorGUILayout.ToggleLeft("Snap To Beat", bSnapTimingToBeat, GUILayout.Width(94f));
                if (!bSnapTimingToBeat && buildEvent != null && buildEvent.IsOneOff() && IsPlaying())
                {
                    // Clear the one off guy!  This will allow us to not do the silly every-frame repeating!
                    buildEvent = null;
                }
                if (GUI.changed)
                {
                    FocusWaveDisplayWindow();
                    GUI.changed = false;
                }

                EditorGUIUtility.labelWidth = 86f;
                EditorGUIUtility.fieldWidth = 20f;
                displayState.beatSubdivisions = EditorGUILayout.IntField(new GUIContent("Divide beat by", "Values greater than 1 will create snap points between beats."), displayState.beatSubdivisions);
                displayState.beatSubdivisions = (displayState.beatSubdivisions < 1) ? 1 : displayState.beatSubdivisions;
                EditorGUIUtility.labelWidth = 0f;
                EditorGUIUtility.fieldWidth = 0f;

                // This will push the toggles to the right!
                GUILayout.FlexibleSpace();

                GUI.changed = false;
                EditorGUIUtility.labelWidth = 68f;
                bScrollWithPlayhead = EditorGUILayout.Toggle(new GUIContent("Auto Scroll", "Whether or not the view automatically scrolls with audio playback."), bScrollWithPlayhead);
                if (GUI.changed)
                {
                    FocusWaveDisplayWindow();
                    GUI.changed = false;
                }

                // Some separation between the elements when applicable.
                GUILayout.FlexibleSpace();

                EditorGUIUtility.labelWidth = 50f;
                audioSrc.volume = EditorGUILayout.Slider(new GUIContent("Volume", "Volume of the Audio Clip during playback."), audioSrc.volume, 0.0f, 1f);
                EditorGUIUtility.labelWidth = 0f;
                EditorGUIUtility.labelWidth = 40f;
                audioSrc.pitch = EditorGUILayout.Slider(new GUIContent("Speed", "How fast the Audio Clip should play. Changes pitch."), audioSrc.pitch, 0.05f, 5f);
                EditorGUIUtility.labelWidth = 0f;

                GUI.enabled = displayState.samplesPerPack != 1;

                EditorGUI.BeginChangeCheck();
                {
                    // Display toggles (this could alternatively be done with a SelectionGrid).
                    if (GUILayout.Toggle(displayState.displayType == WaveDisplayType.MinMax,
                                          new GUIContent("MinMax", "Vertical lines depict the minimum and maximum values of the sample range of a given pixel location."),
                                          radioButtonStyle, GUILayout.Width(60)))
                    {
                        displayState.displayType = WaveDisplayType.MinMax;
                    }
                    if (GUILayout.Toggle(displayState.displayType == WaveDisplayType.RMS,
                                          new GUIContent("RMS", "Vertical lines depict the result of taking the Root Mean Square of the sample range of a given pixel location.  Vertically symmetrical."),
                                          radioButtonStyle, GUILayout.Width(40)))
                    {
                        displayState.displayType = WaveDisplayType.RMS;
                    }
                    if (GUILayout.Toggle(displayState.displayType == WaveDisplayType.Both,
                                          new GUIContent("Both", "Overlays RMS over MinMax."),
                                          radioButtonStyle, GUILayout.Width(50)))
                    {
                        displayState.displayType = WaveDisplayType.Both;
                    }
                }
                if (EditorGUI.EndChangeCheck())
                {
                    FocusWaveDisplayWindow();
                    GUI.changed = false;
                }

                GUI.enabled = true;

                GUILayout.EndHorizontal();

                // Fine tuning of currently selected event settings.
                if (waveDisplay.bValid && editTrack != null)
                {
                    bool bEventDeletionScheduled = false;

                    if (selectedEvents.Count == 1)      // When only a single event is selected!
                    {
                        KoreographyEvent selectedEvent = null;

                        // Only runs once (see if-condition).  This is the only way I've found to pull an element out of the
                        //  HashSet without dumping the contents into a list first (though this may be doing that internally.
                        foreach (KoreographyEvent e in selectedEvents)
                        {
                            selectedEvent = e;
                        }

                        EditorGUILayout.BeginHorizontal(GUILayout.Width(385f));

                        EditorGUILayout.LabelField("Selected Event Settings: (" + editTrack.GetIDForEvent(selectedEvent) + ")", EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();

                        // Handle the deletion of the event AFTER we have finished drawing everything related to it.  This is necessary for
                        //  the Layout and Repaint passes of OnGUI to work.
                        bEventDeletionScheduled = GUILayout.Button("Delete Event", GUILayout.MaxWidth(90), GUILayout.MaxHeight(15));

                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();

                        // Used at the bottom of this block to determine whether or not we need to set the dirty bit for saving.
                        GUI.changed = false;

                        EditorGUIUtility.labelWidth = 132f;
                        EditorGUIUtility.fieldWidth = 100f;

                        int evtPayloadTypeIdx = (selectedEvent.Payload == null) ? 0 : payloadTypes.IndexOf(selectedEvent.Payload.GetType()) + 1;
                        evtPayloadTypeIdx = EditorGUILayout.Popup("Payload", evtPayloadTypeIdx, payloadTypeNames.ToArray());
                        if (GUI.changed)
                        {
                            Undo.RecordObject(editTrack, "Change Payload Type");
                            AttachPayloadToEvent(selectedEvent, (evtPayloadTypeIdx == 0) ? null : payloadTypes[evtPayloadTypeIdx - 1]);

                            // SetDirty (based on GUI.changed) called below.
                        }

                        if (selectedEvent.Payload != null)
                        {
                            // Undo operations handled within DoGUI.
#if (UNITY_4_5 || UNITY_4_6 || UNITY_4_7)
							PayloadDisplay.DoGUI(selectedEvent.Payload, EditorGUILayout.GetControlRect(false, GUILayout.Width(144f)), editTrack, false);
#else
                            selectedEvent.Payload.DoGUI(EditorGUILayout.GetControlRect(false, GUILayout.Width(144f)), editTrack, false);
#endif
                            // SetDirty (based on GUI.changed) called below.
                        }

                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();

                        {
                            // START SAMPLE
                            EditorGUILayout.BeginHorizontal();

                            // Disallow negative sample positions.
                            int startSample = Mathf.Max(0, EditorGUILayout.IntField("Start Sample Location", selectedEvent.StartSample));
                            //int startTime = Mathf.Max(0, EditorGUILayout.IntField("Start Sample Location", selectedEvent.StartTime));
                            //int startSample = (int)(((float)startTime / (float)1000) * EditClip.frequency);
                            if (selectedEvent.StartSample != startSample && startSample < EditClip.samples)
                            {
                                Undo.RecordObject(editTrack, "Adjust Start Sample Location");
                                selectedEvent.StartSample = startSample;
                                //selectedEvent.StartTime = (int)System.TimeSpan.FromSeconds((double)startSample / (double)EditClip.frequency).TotalMilliseconds;
                            }

                            // Snapping.
                            EditorGUILayout.LabelField("Snap to:", GUILayout.MaxWidth(52f));
                            // Disallow negative sample positions.
                            GUI.enabled = selectedEvent.StartSample > 0;
                            if (GUILayout.Button(prevBeatContent))
                            {
                                Undo.RecordObject(editTrack, "Snap Start to Previous Beat");

                                double beatTime = editKoreo.GetBeatTimeFromSampleTime(selectedEvent.StartSample, displayState.beatSubdivisions);
                                beatTime = System.Math.Ceiling(beatTime - 1d);

                                // Check that we didn't end up at the same sample position.  This can happen thanks to floating point/integer conversion.
                                int adjustedSampleLoc = editKoreo.GetSampleTimeFromBeatTime(beatTime, displayState.beatSubdivisions);
                                if (adjustedSampleLoc == selectedEvent.StartSample)
                                {
                                    adjustedSampleLoc = editKoreo.GetSampleTimeFromBeatTime(beatTime - 1d, displayState.beatSubdivisions);
                                }

                                // Auto clamps to 0 at min.
                                selectedEvent.StartSample = adjustedSampleLoc;
                            }
                            GUI.enabled = true;
                            if (GUILayout.Button(nearestBeatContent, GUILayout.MinWidth(30f)))
                            {
                                Undo.RecordObject(editTrack, "Snap Start to Nearest Beat");

                                int adjustedSampleLoc = editKoreo.GetSampleOfNearestBeat(selectedEvent.StartSample, displayState.beatSubdivisions);
                                if (adjustedSampleLoc < EditClip.samples)
                                {
                                    selectedEvent.StartSample = adjustedSampleLoc;
                                }
                                else
                                {
                                    // We're beyond the end of the song.  Just get the last one.
                                    selectedEvent.StartSample = GetSampleOfLastBeat();
                                }
                            }
                            if (GUILayout.Button(nextBeatContent))
                            {
                                double beatTime = editKoreo.GetBeatTimeFromSampleTime(selectedEvent.StartSample, displayState.beatSubdivisions);
                                beatTime = System.Math.Floor(beatTime + 1d);

                                // Check that we didn't end up at the same sample position.  This can happen thanks to floating point/integer conversion.
                                int adjustedSampleLoc = editKoreo.GetSampleTimeFromBeatTime(beatTime, displayState.beatSubdivisions);
                                if (adjustedSampleLoc == selectedEvent.StartSample)
                                {
                                    adjustedSampleLoc = editKoreo.GetSampleTimeFromBeatTime(beatTime + 1d, displayState.beatSubdivisions);
                                }

                                // Don't allow the StartSample to go beyond the bounds of the audio file.
                                if (adjustedSampleLoc < EditClip.samples)
                                {
                                    Undo.RecordObject(editTrack, "Snap Start to Next Beat");
                                    selectedEvent.StartSample = adjustedSampleLoc;
                                }
                            }
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.EndHorizontal();

                            // END SAMPLE
                            EditorGUILayout.BeginHorizontal();
                            //int endSample = EditorGUILayout.IntField("End Sample Location", selectedEvent.EndTime);
                            //int endTime = EditorGUILayout.IntField("End Sample Location", selectedEvent.EndTime);
                            //int endSample = (int)(((float)endTime / (float)1000) * EditClip.frequency);
                            int endSample = EditorGUILayout.IntField("End Sample Location", selectedEvent.EndSample);
                            if (selectedEvent.EndSample != endSample && endSample < EditClip.samples)
                            {
                                Undo.RecordObject(editTrack, "Adjust End Sample Location");
                                selectedEvent.EndSample = endSample;
                                //selectedEvent.EndTime = (int)System.TimeSpan.FromSeconds((double)endSample / (double)EditClip.frequency).TotalMilliseconds;
                            }

                            // Snapping.
                            EditorGUILayout.LabelField("Snap to:", GUILayout.MaxWidth(52f));
                            if (GUILayout.Button(prevBeatContent))
                            {
                                Undo.RecordObject(editTrack, "Snap End to Previous Beat");

                                double beatTime = editKoreo.GetBeatTimeFromSampleTime(selectedEvent.EndSample, displayState.beatSubdivisions);
                                beatTime = System.Math.Ceiling(beatTime - 1d);

                                // Check that we didn't end up at the same sample position.  This can happen thanks to floating point/integer conversion.
                                int adjustedSampleLoc = editKoreo.GetSampleTimeFromBeatTime(beatTime, displayState.beatSubdivisions);
                                if (adjustedSampleLoc == selectedEvent.EndSample)
                                {
                                    adjustedSampleLoc = editKoreo.GetSampleTimeFromBeatTime(beatTime - 1d, displayState.beatSubdivisions);
                                }

                                // Auto clamps to 0 at min.
                                selectedEvent.EndSample = adjustedSampleLoc;
                            }
                            if (GUILayout.Button(nearestBeatContent, GUILayout.MinWidth(30f)))
                            {
                                Undo.RecordObject(editTrack, "Snap End to Nearest Beat");

                                int adjustedSampleLoc = editKoreo.GetSampleOfNearestBeat(selectedEvent.EndSample, displayState.beatSubdivisions);
                                if (adjustedSampleLoc < EditClip.samples)
                                {
                                    selectedEvent.EndSample = adjustedSampleLoc;
                                }
                                else
                                {
                                    // We're beyond the end of the song.  Just get the last one.
                                    selectedEvent.EndSample = GetSampleOfLastBeat();
                                }
                            }
                            if (GUILayout.Button(nextBeatContent))
                            {
                                double beatTime = editKoreo.GetBeatTimeFromSampleTime(selectedEvent.EndSample, displayState.beatSubdivisions);
                                beatTime = System.Math.Floor(beatTime + 1d);

                                // Check that we didn't end up at the same sample position.  This can happen thanks to floating point/integer conversion.
                                int adjustedSampleLoc = editKoreo.GetSampleTimeFromBeatTime(beatTime, displayState.beatSubdivisions);
                                if (adjustedSampleLoc == selectedEvent.EndSample)
                                {
                                    adjustedSampleLoc = editKoreo.GetSampleTimeFromBeatTime(beatTime + 1d, displayState.beatSubdivisions);
                                }

                                // Don't allow the StartSample to go beyond the bounds of the audio file.
                                if (adjustedSampleLoc < EditClip.samples)
                                {
                                    Undo.RecordObject(editTrack, "Snap End to Next Beat");
                                    selectedEvent.EndSample = adjustedSampleLoc;
                                }
                            }
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                    else if (selectedEvents.Count > 1)      // When a group of events is selected!
                    {
                        EditorGUILayout.BeginHorizontal(GUILayout.Width(385f));

                        EditorGUILayout.LabelField("Selected Group Options:", EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();

                        // Handle the deletion of the event AFTER we have finished drawing everything related to it.  This is necessary for
                        //  the Layout and Repaint passes of OnGUI to work.
                        bEventDeletionScheduled = GUILayout.Button("Delete Events", GUILayout.MaxWidth(90f), GUILayout.MaxHeight(15f));

                        EditorGUILayout.EndHorizontal();

                        // Used for the next two lines.
                        EditorGUIUtility.labelWidth = 132f;
                        EditorGUIUtility.fieldWidth = 100f;

                        EditorGUILayout.BeginHorizontal();

                        // Group payload
                        {
                            List<System.Type> groupTypes = new List<System.Type>();

                            foreach (KoreographyEvent evt in selectedEvents)
                            {
                                if (evt.Payload == null)
                                {
                                    if (!groupTypes.Contains(null))
                                    {
                                        groupTypes.Add(null);
                                    }
                                }
                                else if (!groupTypes.Contains(evt.Payload.GetType()))
                                {
                                    groupTypes.Add(evt.Payload.GetType());
                                }

                                if (groupTypes.Count > 1)
                                {
                                    break;
                                }
                            }

                            List<string> tempPayloadNames = new List<string>(payloadTypeNames);

                            int evtPayloadTypeIdx = 0;
                            if (groupTypes.Count > 1)
                            {
                                tempPayloadNames.Insert(0, "[Mixed]");
                            }
                            else
                            {
                                System.Type groupType = groupTypes.First();
                                evtPayloadTypeIdx = (groupType == null) ? 0 : payloadTypes.IndexOf(groupType) + 1;
                            }

                            // Group Payload Type Edit.
                            {
                                int newTypeIdx = EditorGUILayout.Popup("Payload", evtPayloadTypeIdx, tempPayloadNames.ToArray());
                                if (newTypeIdx != evtPayloadTypeIdx)
                                {
                                    Undo.RecordObject(editTrack, "Change Payload Type");

                                    newTypeIdx -= (groupTypes.Count > 1) ? 1 : 0;

                                    System.Type newType = (newTypeIdx == 0) ? null : payloadTypes[newTypeIdx - 1];

                                    foreach (KoreographyEvent evt in selectedEvents)
                                    {
                                        AttachPayloadToEvent(evt, newType);
                                    }

                                    // SetDirty (based on GUI.changed) called below.
                                }
                            }

                            selectedEvents.Sort(KoreographyEvent.CompareByStartSample);
                            KoreographyEvent firstEvt = selectedEvents.First();

                            // Group Payload Edit.
                            {
                                if (firstEvt.Payload != null && groupTypes.Count < 2)
                                {
                                    // Undo operations handled within DoGUI.
#if (UNITY_4_5 || UNITY_4_6 || UNITY_4_7)
									if (PayloadDisplay.DoGUI(firstEvt.Payload, EditorGUILayout.GetControlRect(false, GUILayout.Width(144f)), editTrack, false))
#else
                                    if (firstEvt.Payload.DoGUI(EditorGUILayout.GetControlRect(false, GUILayout.Width(144f)), editTrack, false))
#endif
                                    {
                                        Undo.RecordObject(editTrack, "Group Adjust Payload");

                                        foreach (KoreographyEvent evt in selectedEvents)
                                        {
                                            if (evt != firstEvt)
                                            {
                                                // Figure out why this doesn't work with Curves.  Is it possible that
                                                //  Curves don't copy correctly with Layout(?) events?
                                                evt.Payload = firstEvt.Payload.GetCopy();
                                            }
                                        }

                                        // SetDirty (based on GUI.changed) called below.
                                    }
                                }
                            }
                        }

                        GUILayout.FlexibleSpace();

                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();

                        // Group position.
                        {
                            // Only need to convert the selectedEvents set.  Doesn't matter otherwise.
                            List<KoreographyEvent> selEventGroup = new List<KoreographyEvent>(selectedEvents);
                            selEventGroup.Sort(KoreographyEvent.CompareByStartSample);

                            // Disallow negative sample positions.
                            int startOffset = selEventGroup.First().StartSample;
                            int newPos = Mathf.Max(0, EditorGUILayout.IntField("Event Location", startOffset));

                            // Find the maximum position that the first sample can move.
                            int maxPos = (EditClip.samples - 1) - (selEventGroup[selEventGroup.Count - 1].EndSample - startOffset);
                            newPos = Mathf.Min(newPos, maxPos);

                            if (newPos != startOffset)
                            {
                                int posOffset = newPos - startOffset;

                                Undo.RecordObject(editTrack, "Move Events");

                                foreach (KoreographyEvent movEvt in selectedEvents)
                                {
                                    movEvt.MoveTo(movEvt.StartSample + posOffset);
                                }

                                // SetDirty (based on GUI.changed) called below.
                            }
                        }

                        GUILayout.FlexibleSpace();

                        EditorGUILayout.EndHorizontal();

                        // FUTURE: snapping?  How would this work?  Move by first event?
                        //  "Snap All Start" / "Snap All End" ?
                    }

                    // Change detected.  Mark dirty.
                    if (GUI.changed)
                    {
                        EditorUtility.SetDirty(editTrack);
                    }

                    // We're done doing selectedEvent-based tasks.  Safe to remove it!
                    if (bEventDeletionScheduled)
                    {
                        DeleteSelectedEvents();
                    }

                    // Ensure that no changes screw up the order of the events.
                    editTrack.EnsureEventOrder();
                }
            }

            EditorGUILayout.EndScrollView();

            if (Event.current.type == EventType.Repaint)
            {
                float curHeight = GUILayoutUtility.GetLastRect().yMax;

                if (curHeight != curMaxHeight)
                {
                    curMaxHeight = curHeight;
                }
            }

            // Wrap up.  All other controls have had their chance.  Left-over keys can be used here.
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                // Use the escape key to push focus to the WaveDisplay or clear event selection.
                if (!bIsWaveDisplayFocused)
                {
                    FocusWaveDisplayWindow();
                }
                else
                {
                    selectedEvents.Clear();
                }

                Event.current.Use();
            }
        }

        void SetNewEditKoreo(Koreography newKoreo)
        {
#if (UNITY_4_5 || UNITY_4_6 || UNITY_4_7)
			// This ensures that we don't force lots of errors to be thrown by
			//  loading up the Curve Editor.  Fixed in Unity 5!
			if (Selection.activeObject == null || Selection.activeObject == editKoreo)
			{
				Selection.activeObject = newKoreo;
			}
#endif
            editKoreo = newKoreo;

            if (editKoreo == null)
            {
                // Clear out related objects.
                editTempoSectionIdx = -1;
                SetNewEditTrack(null);
                ClearWaveDisplay();

                // Clear out the Audio Data.
                switch (audioLoadMethod)
                {
                    case AudioLoadMethod.AudioClip:
                        ClearEditClip();
                        break;
                    case AudioLoadMethod.AudioFile:
                        ClearAudioFileClip();
                        break;
                    default:
                        Debug.LogWarning("Unhandled AudioLoadMethod encountered!");
                        break;
                }
            }
            else        // Load in associated metadata.
            {
                // Default to AudioClip loading: only prefer SourceClipPath if it exists.
                audioLoadMethod = !string.IsNullOrEmpty(editKoreo.SourceClipPath) ? AudioLoadMethod.AudioFile : AudioLoadMethod.AudioClip;

                switch (audioLoadMethod)
                {
                    case AudioLoadMethod.AudioClip:
                        // TODO: Assert that the SourceClip is valid (correct Format/Load Type)?
                        if (editKoreo.SourceClip != null)
                        {
                            InitNewClip(editKoreo.SourceClip);
                        }
                        else
                        {
                            ClearWaveDisplay();
                        }
                        break;
                    case AudioLoadMethod.AudioFile:
                        audioFileClip = KoreographyEditor.GetAudioClipFromPath(editKoreo.SourceClipPath);
                        if (audioFileClip != null)
                        {
                            InitNewClip(audioFileClip);
                        }
                        else
                        {
                            ClearWaveDisplay();
                        }
                        break;
                    default:
                        Debug.LogWarning("Unhandled AudioLoadMethod encountered!");
                        break;
                }

                // Load in an Edit Track if one exists!
                KoreographyTrackBase firstTrack = editKoreo.GetTrackAtIndex(0);

                // This safely handles the null case as well (no tracks in loaded Koreography).
                SetNewEditTrack(firstTrack);

                // All Koreography objects have an editTempoSection.
                editTempoSectionIdx = 0;
            }
        }

        void SetNewEditTrack(KoreographyTrackBase newTrack)
        {
            editTrack = newTrack;

            // Clears out the TrackDisplay's reference if newTrack is null.
            //waveDisplay.SetEventTrack(editTrack);

            //显示所有track
            waveDisplay.ShowAllTrack(editKoreo.Tracks,selectedIdx);

            // Clear the event selections.
            selectedEvents.Clear();
            dragSelectedEvents.Clear();

            UpdatePayloadTypesForTrack(newTrack);
        }

        void UpdatePayloadTypesForTrack(KoreographyTrackBase track)
        {
            // Update Payload types.
            if (track != null)
            {
                Dictionary<System.Type, List<System.Type>> trackPayloadTypes = KoreographyTrackTypeUtils.EditableTrackPayloadTypes;

                System.Type trackType = track.GetType();
                payloadTypes = trackPayloadTypes[trackType];
                payloadTypeNames = KoreographyEditor.TrackPayloadNames[trackType];
            }
            //			else
            //			{
            //				// Set payload lists null?
            //			}
        }

        void InitNewClip(AudioClip newClip)
        {
            StopAudio();

            // New clip.  Reinitialize the WaveDisplay.
            waveDisplay.bValid = true;
            waveDisplay.ClearAudioData();

            if (IsAudioClipValid(newClip))
            {
#if (UNITY_4_5 || UNITY_4_6 || UNITY_4_7)
				waveDisplay.SetAudioData(newClip);
#else
                if (!newClip.preloadAudioData)
                {
                    newClip.LoadAudioData();
                }

                if (newClip.loadState == AudioDataLoadState.Loaded)
                {
                    waveDisplay.SetAudioData(newClip);
                }
#endif
            }

            UpdateMaxSamplesPerPack();
        }

        void ClearWaveDisplay()
        {
            waveDisplay.bValid = false;
            fullWaveContentRect = new Rect();
        }

        void ClearEditClip()
        {
            StopAudio();
        }

        void ClearAudioFileClip()
        {
            if (audioFileClip != null)
            {
                ClearEditClip();

                // TODO: Determine if this is the proper way to clear out WWW-loaded unreferenced assets.
                DestroyImmediate(audioFileClip);
                audioFileClip = null;
                Resources.UnloadUnusedAssets();
            }
        }

        void PlayAudio(int sampleTime = -1)
        {
            if (audioSrc.clip != EditClip)
            {
                audioSrc.clip = EditClip;
            }

            if (sampleTime >= 0)
            {
                audioSrc.timeSamples = sampleTime;
            }

            // This saves us from spamming the console if someone tries playing audio while the scene
            //  is in PlayMode and there's no configured AudioListener.
            AddAudioListenerIfNecessary();

            audioSrc.Play();

            bShowPlayhead = true;

            FocusWaveDisplayWindow();
        }

        void PauseAudio()
        {
            audioSrc.Pause();
        }

        void StopAudio()
        {
            audioSrc.Stop();

            audioSrc.timeSamples = displayState.playbackAnchorSamplePosition;

            bShowPlayhead = false;

            // This will reduce the likelihood that we'll cause "multiple AudioListeners" warning.
            RemoveAudioListenerIfNecessary();
        }

        void ScanToSample(int sample)
        {
            if (EditClip != null)
            {
                sample = Mathf.Clamp(sample, 0, EditClip.samples - 1);
                audioSrc.timeSamples = sample;
            }
        }

        void ScanAheadOneBeat()
        {
            if (editKoreo != null)
            {
                int curSample = GetCurrentEstimatedMusicSample();

                double beatTime = editKoreo.GetBeatTimeFromSampleTime(curSample);
                double beatNum = System.Math.Floor(beatTime);

                if (beatTime < beatNum + 0.5d)
                {
                    // Get the sample time from the very next beat.
                    ScanToSample(editKoreo.GetSampleTimeFromBeatTime(beatNum + 1d));
                }
                else
                {
                    // Skip beyond the next beat to the one following.
                    ScanToSample(editKoreo.GetSampleTimeFromBeatTime(beatNum + 2d));
                }

                bShowPlayhead = true;   // Potentially converts from stopped to paused.
            }
        }

        void ScanBackOneBeat()
        {
            if (editKoreo != null)
            {
                int curSample = GetCurrentEstimatedMusicSample();

                double beatTime = editKoreo.GetBeatTimeFromSampleTime(curSample);
                double beatNum = System.Math.Floor(beatTime);

                if (beatTime > beatNum + 0.5d)
                {
                    // Get the sample time from the current beat.
                    ScanToSample(editKoreo.GetSampleTimeFromBeatTime(beatNum));
                }
                else
                {
                    // Skip completely to the previous beat.
                    ScanToSample(editKoreo.GetSampleTimeFromBeatTime(beatNum - 1));
                }

                bShowPlayhead = true;   // Potentially converts from stopped to paused.
            }
        }

        void ScanAheadOneMeasure()
        {
            if (editKoreo != null)
            {
                int curSample = GetCurrentEstimatedMusicSample();

                int measureNum = (int)System.Math.Floor(editKoreo.GetMeasureTimeFromSampleTime(curSample));

                double beatInMeasure = editKoreo.GetBeatCountInMeasureFromSampleTime(curSample);
                if (beatInMeasure <= editKoreo.GetTempoSectionForSample(curSample).BeatsPerMeasure - 1)
                {
                    // Get the sample time from the measure of +1.
                    ScanToSample(editKoreo.GetSampleTimeFromMeasureTime(measureNum + 1));
                }
                else
                {
                    // Skip beyond the next measure to the one following.
                    ScanToSample(editKoreo.GetSampleTimeFromMeasureTime(measureNum + 2));
                }

                bShowPlayhead = true;   // Potentially converts from stopped to paused.
            }
        }

        void ScanBackOneMeasure()
        {
            if (editKoreo != null)
            {
                int curSample = GetCurrentEstimatedMusicSample();

                int measureNum = (int)System.Math.Floor(editKoreo.GetMeasureTimeFromSampleTime(curSample));

                double beatInMeasure = editKoreo.GetBeatCountInMeasureFromSampleTime(curSample);
                if (beatInMeasure >= 1)
                {
                    // Get the sample time from the current measure.
                    ScanToSample(editKoreo.GetSampleTimeFromMeasureTime(measureNum));
                }
                else
                {
                    // Skip completely to the previous measure.
                    ScanToSample(editKoreo.GetSampleTimeFromMeasureTime(measureNum - 1));
                }

                bShowPlayhead = true;   // Potentially converts from stopped to paused.
            }
        }

        bool IsPlaying()
        {
            return audioSrc.isPlaying;
        }

        bool IsStopped()
        {
            return !IsPlaying() &&
                   ((displayState.playbackAnchorSamplePosition != 0) ?
                         (audioSrc.timeSamples == displayState.playbackAnchorSamplePosition) :
                         (audioSrc.timeSamples == 0)) &&
                   !bShowPlayhead;
        }

        bool IsPaused()
        {
            return !IsPlaying() && bShowPlayhead;
        }

        bool IsSelecting()
        {
            return dragStartPos != dragEndPos;
        }

        void FocusWaveDisplayWindow()
        {
            bIsWaveDisplayFocused = true;
            GUI.FocusControl("");
        }

        Vector2 GetMousePosition()
        {
            return viewPosition + Event.current.mousePosition;
        }

        // Music is consumed in discreet chunks by the audio driver.  If updates occur so quickly that
        //  multiple frames report the same audio position but the music is still "playing" then we will
        //  do our best to extrapolate how many samples have been read between "now" and the last
        //  reported change (based on bitrate of music and CPU clock time differences).
        // The actual calculation for this is done in Update().  This ensures that we don't get 
        //  recalculated multiple times within a single frame (which could have consequences for drawing
        //  a concistent view of the WaveForm.
        int GetCurrentEstimatedMusicSample()
        {
            return estimatedSampleTime;
        }

        // Gets the data from the AudioSource itself.
        int GetCurrentRawMusicSample()
        {
            return audioSrc.timeSamples;
        }

        void UpdateMaxSamplesPerPack()
        {
            if (waveDisplay.bValid && EditClip != null)
            {
                // Adjust the max samples per pixel (zoom all the way out such that the waveform just fits in the display)
                //  and ensure that we're within the maximum settings.
                maxSamplesPerPack = waveDisplay.GetMaximumSamplesPerPack(EditClip.samples, (int)curWaveViewWidth);
                displayState.samplesPerPack = Mathf.Min(displayState.samplesPerPack, maxSamplesPerPack);
            }
        }

        // This updates the "Zoom" level.
        void SetNewSamplesPerPack(int samplesPerPack, int offsetInPixels = 0)
        {
            // Only make a change if the offset is beyond the first sample pack position.
            if (offsetInPixels >= (displayState.firstPackPos + displayState.waveStartMax))
            {
                int totalPacks = displayState.GetNumPacks(EditClip.samples);
                int newTotalPacks = WaveDisplayState.GetNumPacks(EditClip.samples, samplesPerPack);

                // Check if we're beyond the end.  The waveEndMin stuff is handled here already.
                int lastPackOffset = displayState.firstPackPos + displayState.waveStartMax + totalPacks;
                if (offsetInPixels > lastPackOffset)
                {
                    displayState.firstPackPos = lastPackOffset - displayState.waveStartMax - newTotalPacks;
                }
                else
                {
                    // We're over the waveform.  Do standard adjustment.

                    // Get sample centered on the offset position.  Note that this is clamped to [0, totalSamples] if
                    //  the mouse is not actually over a sample.
                    int originalCenterPack = (offsetInPixels - displayState.waveStartMax) - displayState.firstPackPos;
                    int centerSample = (int)(((double)originalCenterPack + 0.5d) * (double)displayState.samplesPerPack);
                    int newCenterPack = centerSample / samplesPerPack;

                    // Adjust the position by how far we're offset.
                    displayState.firstPackPos -= (newCenterPack - originalCenterPack);

                    // Ensure we have valid values for the position.  This matters when zooming out and the edges pull
                    //  into the waveform boundaries.  Be sure to take the waveEndMin stuff into account.
                    displayState.firstPackPos = Mathf.Clamp(displayState.firstPackPos, displayState.GetMaxPackPosition(newTotalPacks), 0);
                }
            }

            // Update the settings.
            displayState.samplesPerPack = samplesPerPack;

            // Then ensure that our scroll position remains sane!
            SetScrollPosition(-displayState.firstPackPos);

            // Do anything that needs to happen when zoom is updated.
            OnZoomUpdated();
        }

        void OnZoomUpdated()
        {
#if !KOREO_NON_PRO
            if (analysisWindow != null)
            {
                analysisWindow.Repaint();
            }
#endif
        }

        void SetScrollPosition(float xPos)
        {
            scrollPosition.x = xPos;

            Repaint();

            // Do anything that needs to happen when scroll position is updated.
            OnScrollPositionUpdated();
        }

        void OnScrollPositionUpdated()
        {
#if !KOREO_NON_PRO
            // Steal focus from the AnalysisPanel if it has it.  This is done
            //  to ensure that the updates happen correctly (without focus we don't
            //  send updates).
            if (analysisWindow != null && focusedWindow == analysisWindow)
            {
                Focus();
            }
#endif
        }

        KoreographyEvent GetNewEvent(int startSampleLoc)
        {
            KoreographyEvent newEvt = new KoreographyEvent();
            AttachPayloadToEvent(newEvt);
            newEvt.StartSample = startSampleLoc;
            //newEvt.StartTime = (int)System.TimeSpan.FromSeconds((double)startSampleLoc / (double)EditClip.frequency).TotalMilliseconds;

            if (!bCreateOneOff)
            {
                newEvt.EndSample += 1;          // Spans have a span of at least 1.
                //newEvt.EndTime = (int)System.TimeSpan.FromSeconds((double)(startSampleLoc + 1) / (double)EditClip.frequency).TotalMilliseconds;
            }

            return newEvt;
        }

        void BeginNewEvent(int samplePos)
        {
            if (bSnapTimingToBeat)
            {
                samplePos = editKoreo.GetSampleOfNearestBeat(samplePos, displayState.beatSubdivisions);
            }

            buildEvent = GetNewEvent(samplePos);

            Undo.RecordObject(editTrack, "Add New Event");

            // Might not actually add it.  Don't worry for now.
            if (editTrack.AddEvent(buildEvent))
            {
                // This only needs to happen here for OneOff events.
                EditorUtility.SetDirty(editTrack);
            }

            if (bCreateOneOff && !bSnapTimingToBeat)
            {
                buildEvent = null;
            }
        }

        void ContinueNewEvent(int samplePos)
        {
            // TODO: Check for beat overlap?

            // EndSample is set with StartSample.  If StartSample > curSampleTime, so is EndSample.
            //  Update EndSample if we're beyond StartSample and *NOT* in OneOff mode.
            if (buildEvent.StartSample < samplePos)
            {
                // In the OneOff case, we should add events we may have missed since the last event was added!
                if (bCreateOneOff)
                {
                    AddBeatAlignedOneOffEventsToRange(buildEvent.StartSample, samplePos, displayState.beatSubdivisions);
                }
                else
                {
                    buildEvent.EndSample = samplePos;
                }
            }
            else if (buildEvent.StartSample > samplePos)
            {
                if (bCreateOneOff)
                {
                    AddBeatAlignedOneOffEventsToRange(samplePos, buildEvent.StartSample, displayState.beatSubdivisions);
                }
                else
                {
                    // Don't do this in playmode.  It will break the startSample beat snapping.
                    if (Event.current.isMouse)
                    {
                        buildEvent.StartSample = samplePos;
                    }
                }
            }
        }

        void EndNewEvent(int rawSamplePos)
        {
            // End and commit the current event!
            if (bSnapTimingToBeat)
            {
                int beatSample = editKoreo.GetSampleOfNearestBeat(rawSamplePos, displayState.beatSubdivisions);

                if (bCreateOneOff)
                {
                    // Add intermediary OneOff events!
                    AddBeatAlignedOneOffEventsToRange(buildEvent.StartSample, rawSamplePos, displayState.beatSubdivisions);
                }
                else
                {
                    // Do things a bit differently if we're using the mouse to draw (keyboard-based input during playback).
                    if (!Event.current.isMouse)
                    {
                        if (buildEvent.StartSample > rawSamplePos || buildEvent.StartSample == beatSample)
                        {
                            double startBeat = editKoreo.GetBeatTimeFromSampleTime(buildEvent.StartSample, displayState.beatSubdivisions);
                            buildEvent.EndSample = editKoreo.GetSampleTimeFromBeatTime(System.Math.Round(startBeat + 1d), displayState.beatSubdivisions);
                        }
                        else
                        {
                            buildEvent.EndSample = beatSample;
                        }
                    }
                    else
                    {
                        // Set the StartSample and then ensure that we've quantized the EndSample.
                        //  Inverse the operation based on how the *raw* sample pos compares to
                        //  the StartSample.
                        if (rawSamplePos <= buildEvent.StartSample)
                        {
                            buildEvent.StartSample = beatSample;
                            buildEvent.EndSample = editKoreo.GetSampleOfNearestBeat(buildEvent.EndSample, displayState.beatSubdivisions);
                        }
                        else
                        {
                            buildEvent.StartSample = editKoreo.GetSampleOfNearestBeat(buildEvent.StartSample, displayState.beatSubdivisions);
                            buildEvent.EndSample = beatSample;
                        }

                        // We may have snapped ourselves into OneOff.  Detect and adjust for this.
                        if (buildEvent.IsOneOff())
                        {
                            double startBeat = editKoreo.GetBeatTimeFromSampleTime(buildEvent.StartSample, displayState.beatSubdivisions);
                            buildEvent.EndSample = editKoreo.GetSampleTimeFromBeatTime(System.Math.Round(startBeat + 1d), displayState.beatSubdivisions);
                        }
                    }
                }
            }
            else
            {
                // This should only happen with mouse input.
                if (rawSamplePos <= buildEvent.StartSample)
                {
                    buildEvent.StartSample = rawSamplePos;
                }
                else
                {
                    buildEvent.EndSample = rawSamplePos;
                }
            }

            // All above code flows end in actually updating the event's EndSample.  Mark this as dirty.
            EditorUtility.SetDirty(editTrack);

            buildEvent = null;
        }

        void AttachPayloadToEvent(KoreographyEvent koreoEvent)
        {
            AttachPayloadToEvent(koreoEvent, currentPayloadTypeIdx < 0 ? null : payloadTypes[currentPayloadTypeIdx]);
        }

        void AttachPayloadToEvent(KoreographyEvent koreoEvent, System.Type payloadType)
        {
            if (payloadType == null)
            {
                // No payload for this sucker!
                koreoEvent.Payload = null;
            }
            else if (koreoEvent.Payload == null || koreoEvent.Payload.GetType() != payloadType)
            {
                // GameObjects or Components can only be properly created with Object.Instantiate and require
                //  a base object to clone anyway.
                // This isn't actually true.  We *could* use EditorUtility.CreateGameObjectWithHideFlags.  Not
                //  sure of the implications of this, however.

                if (payloadType.IsSubclassOf(typeof(ScriptableObject)))
                {
                    koreoEvent.Payload = ScriptableObject.CreateInstance(payloadType) as IPayload;
                }
                else
                {
                    koreoEvent.Payload = System.Activator.CreateInstance(payloadType) as IPayload;
                }
            }
        }

        // Adds a new beat-aligned OneOff event to the current track in the following manner: (startSample, endSample].
        void AddBeatAlignedOneOffEventsToRange(int startSample, int endSample, int subdivisions = 1)
        {
            bool bDidAddAnEvent = false;

            endSample = Mathf.Min(endSample, EditClip.samples);

            int startBeat = (int)System.Math.Round(editKoreo.GetBeatTimeFromSampleTime(startSample, subdivisions));
            int endBeat = (int)System.Math.Round(editKoreo.GetBeatTimeFromSampleTime(endSample, subdivisions));

            for (int i = startBeat; i <= endBeat; ++i)
            {
                buildEvent = GetNewEvent(editKoreo.GetSampleTimeFromBeatTime(i, subdivisions));

                if (editTrack.AddEvent(buildEvent))
                {
                    bDidAddAnEvent = true;
                }
            }

            if (bDidAddAnEvent)
            {
                EditorUtility.SetDirty(editTrack);
            }
        }

        void DeleteSelectedEvents()
        {
            Undo.RecordObject(editTrack, selectedEvents.Count > 1 ? "Delete Events" : "Delete Event");

            // editTrack is valid when there are selected events.
            foreach (KoreographyEvent evt in selectedEvents)
            {
                if (evt != null)
                {
                    // Delete selected event.
                    editTrack.RemoveEvent(evt);

                    EditorUtility.SetDirty(editTrack);
                    Repaint();
                }
            }

            selectedEvents.Clear();
        }

        void HandleKeyInput()
        {
            // Only valid keys when the waveform is focused, on KeyUp or KeyDown.
            if ((Event.current.type == EventType.KeyDown || Event.current.type == EventType.KeyUp) &&
                Event.current.keyCode != KeyCode.None &&
                bIsWaveDisplayFocused)
            {
#if UNITY_4_5
				bool bModified = (Event.current.modifiers != (EventModifiers)0);
#else
                bool bModified = (Event.current.modifiers != EventModifiers.None);
#endif

                // Keys that care about both up AND down events go first.  Then we will
                //  process KeyDown, etc.

                // Event creation during playback.  Down to start, Up to stop.
                //  Check unmodified to reserve those for other commands.
                if ((Event.current.keyCode == KeyCode.KeypadEnter ||
                    Event.current.keyCode == KeyCode.Return ||
                    Event.current.keyCode == KeyCode.E) &&
                    !bModified)
                {
                    if (editTrack != null && IsPlaying())
                    {
                        if (Event.current.type == EventType.KeyDown)
                        {
                            if (buildEvent == null)
                            {
                                BeginNewEvent(GetCurrentEstimatedMusicSample());
                            }

                            Event.current.Use();
                        }
                        else if (Event.current.type == EventType.KeyUp)
                        {
                            if (buildEvent != null)
                            {
                                EndNewEvent(GetCurrentEstimatedMusicSample());
                                Event.current.Use();
                            }
                        }
                    }
                }
                else if (Event.current.type == EventType.KeyDown)
                {
                    if (!bModified)
                    {
                        if (Event.current.keyCode == KeyCode.Space)
                        {
                            if (!KoreographyEditor.GetAudioSystemDisabled())
                            {
                                if (IsPlaying())
                                {
                                    PauseAudio();
                                }
                                else
                                {
                                    PlayAudio();
                                }
                                Event.current.Use();
                            }
                        }
                        else if (Event.current.keyCode == KeyCode.A)
                        {
                            controlMode = ControlMode.Select;
                            Event.current.Use();
                        }
                        else if (Event.current.keyCode == KeyCode.S)
                        {
                            controlMode = ControlMode.Author;
                            Event.current.Use();
                        }
                        else if (Event.current.keyCode == KeyCode.D)
                        {
                            controlMode = ControlMode.Clone;
                            Event.current.Use();
                        }
                        else if (Event.current.keyCode == KeyCode.Z)
                        {
                            bCreateOneOff = true;
                            Event.current.Use();
                        }
                        else if (Event.current.keyCode == KeyCode.X)
                        {
                            bCreateOneOff = false;
                            Event.current.Use();
                        }
                        else if (Event.current.keyCode == KeyCode.V)
                        {
                            ToggleVisualizer();
                            Event.current.Use();
                        }
                    }
                    else
                    {
                        if (Event.current.keyCode == KeyCode.V && Event.current.shift &&
                            ((Application.platform == RuntimePlatform.OSXEditor && Event.current.command) ||
                             (Application.platform == RuntimePlatform.WindowsEditor && Event.current.control)))
                        {
                            // No need to check 'bIsWaveDisplayFocused' as it is checked before even getting here.
                            if (clippedEvents.Count > 0 && selectedEvents.Count > 0 && waveDisplay.bValid)
                            {
                                PastePayloadToSelectedEvents();
                                Event.current.Use();
                            }
                        }
                        else if (Event.current.keyCode == KeyCode.Space && Event.current.shift)
                        {
                            if (!KoreographyEditor.GetAudioSystemDisabled())
                            {
                                StopAudio();
                                Event.current.Use();
                            }
                        }
                        // NOTE: Arrow Keys appear to always have the FunctionKey modifier on.
                        else if (Event.current.keyCode == KeyCode.LeftArrow)
                        {
                            if (Event.current.modifiers == EventModifiers.FunctionKey)
                            {
                                ScanBackOneMeasure();
                                Event.current.Use();
                            }
                            else if (Event.current.modifiers == (EventModifiers.FunctionKey | EventModifiers.Shift))
                            {
                                ScanBackOneBeat();
                                Event.current.Use();
                            }
                        }
                        else if (Event.current.keyCode == KeyCode.RightArrow)
                        {
                            if (Event.current.modifiers == EventModifiers.FunctionKey)
                            {
                                ScanAheadOneMeasure();
                                Event.current.Use();
                            }
                            else if (Event.current.modifiers == (EventModifiers.FunctionKey | EventModifiers.Shift))
                            {
                                ScanAheadOneBeat();
                                Event.current.Use();
                            }
                        }
                        else if (Event.current.keyCode == KeyCode.DownArrow)
                        {
                            if (Event.current.modifiers == EventModifiers.FunctionKey)
                            {
                                audioSrc.pitch = Mathf.Max(0.1f, Mathf.Round((audioSrc.pitch - 0.1f) * 10f) / 10f);
                                Event.current.Use();
                            }
                            else if (Event.current.modifiers == (EventModifiers.FunctionKey | EventModifiers.Shift))
                            {
                                audioSrc.pitch = Mathf.Min(5f, Mathf.Round((audioSrc.pitch - 0.01f) * 100f) / 100f);
                                Event.current.Use();
                            }
                        }
                        else if (Event.current.keyCode == KeyCode.UpArrow)
                        {
                            if (Event.current.modifiers == EventModifiers.FunctionKey)
                            {
                                audioSrc.pitch = Mathf.Min(5f, Mathf.Round((audioSrc.pitch + 0.1f) * 10f) / 10f);
                                Event.current.Use();
                            }
                            else if (Event.current.modifiers == (EventModifiers.FunctionKey | EventModifiers.Shift))
                            {
                                audioSrc.pitch = Mathf.Min(5f, Mathf.Round((audioSrc.pitch + 0.01f) * 100f) / 100f);
                                Event.current.Use();
                            }
                        }
                        // NOTE: Delete/Backspace Keys appear to always have the FunctionKey modifier on.
                        else if ((Event.current.keyCode == KeyCode.Delete ||
                                  Event.current.keyCode == KeyCode.Backspace) &&
                                 Event.current.modifiers == EventModifiers.FunctionKey)
                        {
                            if (selectedEvents.Count > 0)
                            {
                                DeleteSelectedEvents();
                                Event.current.Use();
                            }
                        }
                    }
                }
            }
        }

        void HandleMouseInput()
        {
            Vector2 mousePos = GetMousePosition();

            if (Event.current.rawType == EventType.MouseMove)
            {
                if (IsSelecting())
                {
                    // MouseDrag occurs while selecting.  If we are now getting
                    //  MouseMove, a mouse up has occurred offscreen.  Clear the
                    //  selection.
                    ResetDrag();

                    // Calling Use() triggers a Repaint() internally (learned this from a Unity Blog post
                    //  before it was posted [as an RFC document]).
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseMove && Event.current.alt &&
                         waveDisplay.bValid && waveDisplay.IsClickableAtLoc(mousePos) &&
                         // Determine the mouse is actually over the waveform itself.
                         waveDisplay.GetFirstAbsoluteDrawableXPosition() + displayState.firstPackPos + displayState.waveStartMax <= mousePos.x &&
                         waveDisplay.GetFirstAbsoluteDrawableXPosition() + displayState.firstPackPos + displayState.waveEndMin + displayState.GetNumPacks(EditClip.samples) >= mousePos.x)
                {
                    // Do not allow scratching while the main audio is playing.
                    if (!IsPlaying())
                    {
                        if (scratchSrc.clip != EditClip)
                        {
                            scratchSrc.clip = EditClip;
                        }

                        // Set the time location of the samples to the mouse position and play.
                        scratchSrc.timeSamples = waveDisplay.GetSamplePositionOfPoint(mousePos, displayState);
                        scratchSrc.Play();

                        // Set a scheduled end time to leave enough playtime for something to be audible.
                        scratchSrc.SetScheduledEndTime(AudioSettings.dspTime + 0.05d);  // Currently 50ms.

                        // Adjust the pitch based on mouse move. This enables scrubbing back slightly more smoothly.
                        scratchSrc.pitch = (Event.current.delta.x < 0) ? -1f : 1f;
                    }
                }
            }
            else if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                // Make sure we handle the wave display focusing.
                bIsWaveDisplayFocused = false;

                // Waveform mouse-down checking:
                //  1) LCD overlays the waveform slightly.  Let it try to go first.
                //  2) Then verify that we're not just in the scroll bar.
                //  3) Finally check that we're over the wave view.

                if (IsPointInLCD(mousePos))
                {
                    // SampleTime->MusicTime->SolarTime->SampleTime...
                    lcdMode = (lcdMode == LCDDisplayMode.SampleTime) ? LCDDisplayMode.MusicTime : (lcdMode == LCDDisplayMode.MusicTime) ? LCDDisplayMode.SolarTime : LCDDisplayMode.SampleTime;
                    Event.current.Use();
                }
                else if (IsPointInWaveScrollBar(mousePos))
                {
                    // Don't lose focus when the scroll bar is clicked.
                    FocusWaveDisplayWindow();
                }
                else if (IsWaveDisplayClickableAtLoc(mousePos))
                {
                    FocusWaveDisplayWindow();

                    KoreographyEvent clickEvt = waveDisplay.GetEventAtLoc(mousePos,selectedIdx);

                    // Ensure a clean starting point.
                    eventEditMode = EventEditMode.None;
                    eventEditClickX = 0;

                    // If we are clicking on an event, prefer to select/resize/move it.
                    //  Otherwise, check that we're in the correct mode.
                    if (clickEvt != null || controlMode == ControlMode.Select)
                    {
                        MouseDownEditMode();

                        if (selectedEvents.Contains(clickEvt))
                        {
                            eventEditMode = waveDisplay.GetEventEditModeAtLoc(mousePos,selectedIdx);

                            if (eventEditMode == EventEditMode.Move)
                            {
                                selectedEvents.Sort(KoreographyEvent.CompareByStartSample);
                                eventEditClickX = mousePos.x - waveDisplay.GetHorizontalLocOfSample(selectedEvents.First().StartSample, displayState);
                            }
                            else if (eventEditMode != EventEditMode.None)
                            {
                                // Resizing.  Make sure we're the only one selected.
                                selectedEvents.Clear();
                                selectedEvents.Add(clickEvt);
                            }
                        }
                    }
                    else if (controlMode == ControlMode.Author)
                    {
                        MouseDownDrawMode();
                    }
                    else if (controlMode == ControlMode.Clone)
                    {
                        // Do the clone only if we have a selection.
                        //  If no selection, do selection.
                        if (selectedEvents.Count > 0)
                        {
                            MouseDownCloneMode();
                        }
                        else
                        {
                            MouseDownEditMode();
                        }
                    }
                }
            }
            else if ((Event.current.type == EventType.MouseUp || Event.current.rawType == EventType.MouseUp) && Event.current.button == 0)
            {
                // Clear the edit mode, no matter where we are.
                eventEditMode = EventEditMode.None;
                eventEditClickX = 0;

                if (controlMode == ControlMode.Select)
                {
                    MouseUpEditMode();
                }
                else if (controlMode == ControlMode.Author) // For this to be the case we must have valid Koreography and at least one Koreography Track.
                {
                    MouseUpDrawMode();
                }
                else if (controlMode == ControlMode.Clone)
                {
                    if (selectedEvents.Count > 0)
                    {
                        MouseUpCloneMode();
                    }
                    else
                    {
                        MouseUpEditMode();
                    }
                }

                // We've released the mouse.  No more dragging.
                ResetDrag();
            }
            else if (Event.current.type == EventType.MouseDrag && Event.current.button == 0)
            {
                if (waveDisplay.bValid && waveDisplay.ContainsPoint(mousePos))
                {
                    if (eventEditMode != EventEditMode.None)
                    {
                        MouseEditEvent();
                    }
                    else if (controlMode == ControlMode.Select)
                    {
                        MouseDragEditMode();
                    }
                    else if (controlMode == ControlMode.Author)
                    {
                        MouseDragDrawMode();
                    }
                    else if (controlMode == ControlMode.Clone)
                    {
                        if (selectedEvents.Count > 0)
                        {
                            MouseDragCloneMode();
                        }
                        else
                        {
                            MouseDragEditMode();
                        }
                    }
                }
            }
            else if (Event.current.type == EventType.MouseDrag && Event.current.button == 2)
            {
                if (waveDisplay.bValid &&
                    waveDisplay.ContainsPoint(GetMousePosition()))
                {
                    // Get the new position into the firstPackPos form.
                    float newPos = -(scrollPosition.x - Event.current.delta.x);
                    // Clamp it to the max positions.
                    newPos = Mathf.Clamp(newPos, (float)displayState.GetMaxPackPosition(displayState.GetNumPacks(EditClip.samples)), 0f);

                    // Set the position.
                    SetScrollPosition(-newPos);

                    // Update playhead to "scratch" if we're playing.
                    if (IsPlaying() && bScrollWithPlayhead)
                    {
                        ScanToSample((int)scrollPosition.x * displayState.samplesPerPack);
                    }

                    Event.current.Use();
                }
            }
        }

        void HandleScrollInput()
        {
            if (Event.current.type == EventType.ScrollWheel && !Event.current.shift)
            {
                if (waveDisplay.bValid &&
                    waveDisplay.ContainsPoint(GetMousePosition()) &&
                    Mathf.Abs(Event.current.delta.y) > Mathf.Abs(Event.current.delta.x))
                {
                    //------------WARNING------------//------------WARNING------------
                    // Modifications here should also be made in the zoom bar handling
                    //  code! If you change something here, ensure that it is mirrored
                    //  in that area!
                    //------------WARNING------------//------------WARNING------------

                    // Upper bound of the logarithmic scale.
                    float maxLog = (float)System.Math.Log((double)maxSamplesPerPack);

                    // The percent [0,1] value at which we switch from linear to logarithmic scales.
                    float linSwitchPct = Mathf.InverseLerp(0f, maxLog, (float)System.Math.Log((double)KoreographyEditor.MaxLinearZoomPackSize));

                    // Calculate the current delta setting based on the present zoom state.
                    float zoomPercent = 0f;
                    if (displayState.samplesPerPack < KoreographyEditor.MaxLinearZoomPackSize)
                    {
                        // Linear scale.
                        zoomPercent = Mathf.InverseLerp(1f, KoreographyEditor.MaxLinearZoomPackSize, displayState.samplesPerPack) * linSwitchPct;
                    }
                    else
                    {
                        // Logarithmic scale.
                        float curLog = (float)System.Math.Log((double)displayState.samplesPerPack);
                        zoomPercent = Mathf.InverseLerp(0f, maxLog, curLog);
                    }

                    // Controls how fast the scale moves.
                    float scale = 0.01f;

                    // Find the new zoom percent.
                    zoomPercent = zoomPercent + (Event.current.delta.y * scale);

                    // Zoom around the center of the display.
                    //	NOTE: centering currently ignores visuals when the window must be horizontally scrolled.
                    int zoomOffsetInPixels = waveDisplay.GetPixelOffsetInChannelAtLoc(GetMousePosition());

                    // Calculate the new zoom amount based on the new delta value.
                    int newSamps = 0;
                    if (zoomPercent < linSwitchPct)
                    {
                        newSamps = (int)Mathf.Lerp(1f, KoreographyEditor.MaxLinearZoomPackSize, zoomPercent / linSwitchPct);
                    }
                    else
                    {
                        newSamps = (int)Mathf.Exp(Mathf.Lerp(0f, maxLog, zoomPercent));
                    }

                    // Commit the newly calculated zoom state to the view.
                    SetNewSamplesPerPack(newSamps, zoomOffsetInPixels);

                    Event.current.Use();
                }
            }
        }

        bool IsPointInLCD(Vector2 point)
        {
            bool bInLCD = false;
            foreach (Rect lcdRect in lcdRects)
            {
                if (lcdRect.Contains(point))
                {
                    bInLCD = true;
                    break;
                }
            }
            return bInLCD;
        }

        bool IsPointInWaveScrollBar(Vector2 point)
        {
            // The wave scrollbar is the difference between the wave content and the full wave display rect
            //  while also not being over a Window Scrollbar (the one that appears if the window is shrunk).
            return (fullWaveContentRect.Contains(point) &&
                !(waveDisplay.bValid && waveDisplay.IsClickableAtLoc(point)) &&
                !IsPointInWindowScrollbar(point));
        }

        bool IsPointInWindowScrollbar(Vector2 point)
        {
            bool bOver = false;

            // If the vertical scrollbar is visible...
            if ((selectedEvents.Count == 0 && curMaxHeight < KoreographyEditor.MaxWaveHeightBase) ||
                (selectedEvents.Count == 1 && curMaxHeight < KoreographyEditor.MaxWaveHeightOne) ||
                (selectedEvents.Count > 1 && curMaxHeight < KoreographyEditor.MaxWaveHeightMany))
            {
                // And if the point is in the scrollbar's space...
                if (point.x - viewPosition.x >= position.width - GUI.skin.verticalScrollbar.fixedWidth)
                {
                    bOver = true;
                }
            }

            // If the horizontal scrollbar is visible... 
            if (position.width < KoreographyEditor.MinWaveViewWidth)
            {
                // And if the point is in the scrollbar's space...
                if (point.y - viewPosition.y >= position.height - GUI.skin.horizontalScrollbar.fixedHeight)
                {
                    bOver = true;
                }
            }

            return bOver;
        }

        // Verifies that the tested point is both clickable within the WaveDisplay AND not in potential screen scrollbars.
        bool IsWaveDisplayClickableAtLoc(Vector2 point)
        {
            return (waveDisplay.bValid && waveDisplay.IsClickableAtLoc(point)) && !IsPointInWindowScrollbar(point);
        }

        bool ShouldEventEditSnapToBeat()
        {
            bool bSnap = bSnapTimingToBeat;

            if (Event.current != null)
            {
                if (bSnap)
                {
                    bSnap = Event.current.shift ? false : true;
                }
                else
                {
                    bSnap = Event.current.shift ? true : false;
                }
            }

            return bSnap;
        }

        string GetMusicTimeForDisplayFromSample(int sample)
        {
            string output = string.Empty;

            if (Event.current.type == EventType.Repaint)
            {
                // Get the measure time.
                double measureTime = editKoreo.GetMeasureTimeFromSampleTime(sample);
                double measure = System.Math.Floor(measureTime);

                TempoSectionDef section = editKoreo.GetTempoSectionForSample(sample);

                // Calculate the beat within the measure.
                double beat = (measureTime - measure) * section.BeatsPerMeasure;

                // Determine whether or not we're actually at the next measure (but simply too "accurate"
                //  with the double precision for the given sample).  Currently, if we're less than one
                //  sample distance from the next beat, adjust the measure time.
                double sampleDiff = 1d / section.SamplesPerBeat;

                if ((beat - System.Math.Floor(beat)) + sampleDiff > 1d)
                {
                    // Iterate the beat!
                    beat = (double)(((int)beat + 1) % section.BeatsPerMeasure);

                    if (beat == 0d)
                    {
                        measure++;
                    }
                }

                // Preserve three digits.  Do not round as this can go beyond where
                //  we want it to.
                beat = System.Math.Floor(beat * 1000d) / 1000d;

                // Build the string using the StringBuilder to save a bunch of garbage generation.
                //  There is still a TON of it generated here, just less this way.
                lcdStringBuilder.Length = 0;
                lcdStringBuilder.AppendFormat("{0:0'm | '}{1:0.000'b'}", measure + 1d, beat + 1d);
                output = lcdStringBuilder.ToString();
            }

            return output;
        }

        Rect GetDragAreaRect()
        {
            float minX = Mathf.Min(dragStartPos.x, dragEndPos.x);
            float minY = Mathf.Min(dragStartPos.y, dragEndPos.y);
            float maxX = Mathf.Max(dragStartPos.x, dragEndPos.x);
            float maxY = Mathf.Max(dragStartPos.y, dragEndPos.y);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        void ValidateKoreographyAndTrackData()
        {
            if (editKoreo != null)
            {
                // This is done to avoid short-circuiting as cleanup logic could happen in both in the same frame!
                bool bNeedsTrackListSave = editKoreo.CheckTrackListIntegrity();
                bool bNeedsTempoListSave = editKoreo.CheckTempoSectionListIntegrity();

                if (bNeedsTrackListSave || bNeedsTempoListSave)
                {
                    EditorUtility.SetDirty(editKoreo);
                }
            }

            if (editTrack != null && editTrack.CheckEventListIntegrity())
            {
                EditorUtility.SetDirty(editTrack);
            }
        }

        void ToggleVisualizer()
        {
            if (visualizerWindow == null)
            {
                visualizerWindow = EventVisualizer.OpenWindow(this);
            }
            else
            {
                visualizerWindow.Close();
                visualizerWindow = null;
            }
        }

        #endregion
        #region Helper Methods

        int GetSampleOfLastBeat()
        {
            int samplePos = 0;
            if (EditClip != null)
            {
                double lastBeatTime = editKoreo.GetBeatTimeFromSampleTime(EditClip.samples, displayState.beatSubdivisions);
                samplePos = editKoreo.GetSampleTimeFromBeatTime(System.Math.Floor(lastBeatTime), displayState.beatSubdivisions);
            }
            return samplePos;
        }

        #endregion
        #region Callback Handlers

        void OnNewTrackOptionSelected(object type)
        {
            KoreographyTrackBase newTrack = CreateNewKoreographyTrack((System.Type)type);

            if (newTrack != null)
            {
                newTrack.EventID = newTrack.name;

                // File a bug?  Does not work as expected: files are not properly uncreated.
                // See: http://answers.unity3d.com/questions/674429/calling-registercreatedobjectundo-on-an-asset-is-b.html
                //Undo.RegisterCreatedObjectUndo(newTrack, "New Track");

                if (editKoreo.CanAddTrack(newTrack))
                {
                    Undo.RecordObject(editKoreo, "New Track");

                    editKoreo.AddTrack(newTrack);

                    EditorUtility.SetDirty(editKoreo);
                }

                SetNewEditTrack(newTrack);
            }
        }

        #endregion
        #region Mouse Methods

        void ResetDrag()
        {
            dragStartPos = Vector2.zero;
            dragEndPos = Vector2.zero;
        }

        void MouseEditEvent()
        {
            Vector2 mouseLoc = GetMousePosition();
            if (waveDisplay.bValid && waveDisplay.ContainsPoint(mouseLoc) && editKoreo != null)
            {
                int samplePos = 0;
                if (eventEditMode == EventEditMode.Move)
                {
                    Vector2 edgeLoc = mouseLoc;
                    edgeLoc.x -= eventEditClickX;

                    // This also clamps the sample position to zero.
                    samplePos = waveDisplay.GetSamplePositionOfPoint(edgeLoc, displayState);

                    if (ShouldEventEditSnapToBeat())
                    {
                        samplePos = editKoreo.GetSampleOfNearestBeat(samplePos, displayState.beatSubdivisions);
                    }

                    selectedEvents.Sort(KoreographyEvent.CompareByStartSample);

                    // Used for offsetting.  We are going to move all events by the amount
                    //  done on the first event.  This could be weird.  Wait for
                    //  suggestions/experience/complaints to adjust further.
                    int startOffset = selectedEvents.First().StartSample;

                    // Gets the destination of the StartSample position of the final event.
                    int finalEventStartSample = samplePos + (selectedEvents[selectedEvents.Count - 1].StartSample - startOffset);

                    // Only adjust/serialize if we actually moved AND if the move won't push an
                    //  event off the end of the waveform.
                    if (samplePos != selectedEvents.First().StartSample &&
                        finalEventStartSample < EditClip.samples)
                    {
                        Undo.RecordObject(editTrack, (selectedEvents.Count == 1) ? "Move Event" : "Move Events");

                        foreach (KoreographyEvent movEvt in selectedEvents)
                        {
                            movEvt.MoveTo(samplePos + (movEvt.StartSample - startOffset));
                        }

                        EditorUtility.SetDirty(editTrack);
                    }
                }
                else
                {
                    samplePos = waveDisplay.GetSamplePositionOfPoint(mouseLoc, displayState);

                    if (ShouldEventEditSnapToBeat())
                    {
                        samplePos = editKoreo.GetSampleOfNearestBeat(samplePos, displayState.beatSubdivisions);
                    }

                    if (eventEditMode == EventEditMode.ResizeLeft)
                    {
                        KoreographyEvent evt = selectedEvents.First();

                        // Only adjust/serialize if we actually changed.
                        if (samplePos != evt.StartSample && samplePos < EditClip.samples)
                        {
                            Undo.RecordObject(editTrack, "Adjust Event Start");

                            evt.StartSample = samplePos;

                            EditorUtility.SetDirty(editTrack);
                        }
                    }
                    else if (eventEditMode == EventEditMode.ResizeRight)
                    {
                        KoreographyEvent evt = selectedEvents.First();

                        // Only adjust/serialize if we actually changed.
                        if (samplePos != evt.EndSample)
                        {
                            Undo.RecordObject(editTrack, "Adjust Event End");

                            evt.EndSample = samplePos;

                            EditorUtility.SetDirty(editTrack);
                        }
                    }
                }

                Event.current.Use();
            }
        }

        void MouseDownEditMode()
        {
            Vector2 mousePos = GetMousePosition();

            KoreographyEvent selectedEvent = waveDisplay.GetEventAtLoc(mousePos,selectedIdx);

            if (selectedEvent != null)
            {
                if (Event.current.clickCount < 2)   // Double clicks should fall through to the system to enable editing!
                {
                    if (Event.current.shift)    // Shift is add [up to].
                    {
                        if (!selectedEvents.Contains(selectedEvent))
                        {
                            selectedEvents.Add(selectedEvent);
                        }
                    }
                    else if ((Application.platform == RuntimePlatform.OSXEditor && Event.current.command) ||    // Command (Mac) is add/remove unique.
                             (Application.platform == RuntimePlatform.WindowsEditor && Event.current.control))  // Control (other) is add/remove unique.
                    {
                        if (selectedEvents.Contains(selectedEvent))
                        {
                            selectedEvents.Remove(selectedEvent);
                        }
                        else
                        {
                            selectedEvents.Add(selectedEvent);
                        }
                    }
                    else if (selectedEvents.Count == 0 || !selectedEvents.Contains(selectedEvent))      // Replace selection.
                    {
                        selectedEvents.Clear();
                        selectedEvents.Add(selectedEvent);
                    }

                    Event.current.Use();
                }
                else
                {
                    // Drop focus (as the edit field should be focused now).
                    bIsWaveDisplayFocused = false;

                    // Clear out the selection to enable [Delete/Backspace] keys to work without
                    //  deleting the events first.
                    selectedEvents.Clear();
                }
            }
            else
            {
                if (Event.current.clickCount < 2)
                {
                    // Remove the selection - no Shift or [CMD/CTRL].
                    if (!(Event.current.shift ||
                         (Application.platform == RuntimePlatform.OSXEditor && Event.current.command) ||
                         (Application.platform == RuntimePlatform.WindowsEditor && Event.current.control)))
                    {
                        selectedEvents.Clear();
                    }

                    // Start dragging!
                    dragStartPos = mousePos;
                    dragEndPos = dragStartPos;

                    Event.current.Use();
                }
                else if (Event.current.clickCount == 2)
                {
                    // Double click to add a new event, even in Edit Mode.
                    if (editKoreo != null && editTrack != null)
                    {
                        int samplePos = waveDisplay.GetSamplePositionOfPoint(mousePos, displayState);

                        // Only add if we're within bounds.
                        if (samplePos < EditClip.samples)
                        {
                            KoreographyEvent newEvt = GetNewEvent(samplePos);

                            // Only create OneOff events on Double click.
                            newEvt.EndSample = newEvt.StartSample;

                            if (ShouldEventEditSnapToBeat())
                            {
                                newEvt.MoveTo(editKoreo.GetSampleOfNearestBeat(newEvt.StartSample, displayState.beatSubdivisions));
                            }

                            Undo.RecordObject(editTrack, "Add New Event");

                            if (editTrack.AddEvent(newEvt))
                            {
                                // This only needs to happen here for OneOff events.
                                EditorUtility.SetDirty(editTrack);
                            }
                        }
                    }

                    // Clear selected events?

                    Event.current.Use();
                }
            }
        }

        void MouseDragEditMode()
        {
            if (dragStartPos != Vector2.zero)
            {
                dragEndPos = GetMousePosition();

                // Replace the currently selected area.
                dragSelectedEvents.Clear();
                dragSelectedEvents.AddRange(waveDisplay.GetEventsTouchedByArea(GetDragAreaRect(),selectedIdx));

                // Set up the highlight set.
                eventsToHighlight.Clear();
                eventsToHighlight.AddRange(selectedEvents);

                // And adjust the highlight set based on user control.
                if (Event.current.shift)
                {
                    eventsToHighlight = eventsToHighlight.Union(dragSelectedEvents).ToList();
                }
                else if ((Application.platform == RuntimePlatform.OSXEditor && Event.current.command) ||    // Command (Mac) is add/remove unique.
                         (Application.platform == RuntimePlatform.WindowsEditor && Event.current.control))  // Control (other) is add/remove unique.
                {
                    eventsToHighlight = eventsToHighlight.Except(dragSelectedEvents).ToList();
                }
                else
                {
                    eventsToHighlight.Clear();
                    eventsToHighlight.AddRange(dragSelectedEvents);
                }

                Event.current.Use();
            }
        }

        void MouseUpEditMode()
        {
            if (dragStartPos != dragEndPos)
            {
                if (dragSelectedEvents.Count > 0)
                {
                    // Commit the changes.
                    if (Event.current.shift)
                    {
                        selectedEvents = selectedEvents.Union(dragSelectedEvents).ToList();
                    }
                    else if ((Application.platform == RuntimePlatform.OSXEditor && Event.current.command) ||    // Command (Mac) is add/remove unique.
                             (Application.platform == RuntimePlatform.WindowsEditor && Event.current.control))  // Control (other) is add/remove unique.
                    {
                        selectedEvents = selectedEvents.Except(dragSelectedEvents).ToList();
                    }
                    else
                    {
                        // Replace the events.
                        selectedEvents.Clear();
                        selectedEvents.AddRange(dragSelectedEvents);
                    }

                    dragSelectedEvents.Clear();
                }

                Event.current.Use();
            }
        }

        void MouseDownDrawMode()
        {
            if (editKoreo != null && editTrack != null && buildEvent == null)
            {
                // Clear out the selected events in draw mode before drawing.
                if (selectedEvents.Count > 0)
                {
                    selectedEvents.Clear();
                }
                else
                {
                    int samplePos = waveDisplay.GetSamplePositionOfPoint(GetMousePosition(), displayState);
                    // Only add if we're within bounds.
                    if (samplePos < EditClip.samples)
                    {
                        BeginNewEvent(samplePos);
                    }
                }

                Event.current.Use();
            }
        }

        void MouseDragDrawMode()
        {
            if (buildEvent != null) // For this to be the case we must have valid Koreography and at least one KoreographyTrack.
            {
                // Sample position clamping for OneOffs is handled internally.
                ContinueNewEvent(waveDisplay.GetSamplePositionOfPoint(GetMousePosition(), displayState));

                Event.current.Use();
            }
        }

        void MouseUpDrawMode()
        {
            if (buildEvent != null)
            {
                int samplePos = buildEvent.EndSample;

                if (waveDisplay.ContainsPoint(GetMousePosition()))
                {
                    samplePos = waveDisplay.GetSamplePositionOfPoint(GetMousePosition(), displayState);
                }

                // Sample position clamping for OneOffs is handled internally.
                EndNewEvent(samplePos);

                Event.current.Use();
            }
        }

        void MouseDownCloneMode()
        {
            if (editKoreo != null && editTrack != null && buildEvent == null)
            {
                if (selectedEvents.Count > 0)
                {
                    int samplePos = waveDisplay.GetSamplePositionOfPoint(GetMousePosition(), displayState);
                    // Only clone if we're within bounds.
                    if (samplePos < EditClip.samples)
                    {
                        // Snap these to the beat if that's the setting (or override)
                        if (ShouldEventEditSnapToBeat())
                        {
                            samplePos = editKoreo.GetSampleOfNearestBeat(samplePos, displayState.beatSubdivisions);
                        }

                        DuplicateEventsAtLocation(selectedEvents, samplePos, "Clone Event");
                    }
                }

                Event.current.Use();
            }
        }

        void MouseDragCloneMode()
        {
            // Don't do anything yet.
        }

        void MouseUpCloneMode()
        {
            // Don't do anything yet.
        }

        #endregion
        #region Commands

        void SelectAll()
        {
            if (editTrack != null)
            {
                selectedEvents.Clear();
                selectedEvents = editTrack.GetAllEvents();
            }
        }

        void CutSelectedEvents()
        {
            if (selectedEvents.Count > 0)
            {
                CopySelectedEvents();

                Undo.RecordObject(editTrack, selectedEvents.Count > 1 ? "Cut Events" : "Cut Event");
                DeleteSelectedEvents();
                EditorUtility.SetDirty(editTrack);
            }
        }

        void CopySelectedEvents()
        {
            // Clear previously copied events.
            clippedEvents.Clear();

            // TODO: Sort the selected list (once we change from a Set to a List).

            foreach (KoreographyEvent evt in selectedEvents)
            {
                clippedEvents.Add(evt.GetCopy());
            }
        }

        void PasteOverSelectedEvents()
        {
            clippedEvents.Sort(KoreographyEvent.CompareByStartSample);

            KoreographyEvent evtToOverwrite = (selectedEvents.Count == 1) ? selectedEvents.First() : null;

            if (selectedEvents.Count > 1)
            {
                // Store off references to the events before they're deleted.
                selectedEvents.Sort(KoreographyEvent.CompareByStartSample);
                evtToOverwrite = selectedEvents.First();
            }

            // Delete the selected events.  We have to do this because we're replacing exact events and
            //  sample location collisions may not be allowed.
            Undo.RecordObject(editTrack, clippedEvents.Count > 1 ? "Paste Events" : "Paste Event");
            DeleteSelectedEvents();
            EditorUtility.SetDirty(editTrack);

            // Used for offsetting.
            int startOffset = clippedEvents.First().StartSample;

            if (evtToOverwrite != null)
            {
                foreach (KoreographyEvent addEvt in clippedEvents)
                {
                    KoreographyEvent newEvt = addEvt.GetCopy();

                    int samplePos = evtToOverwrite.StartSample + (addEvt.StartSample - startOffset);

                    if (samplePos < EditClip.samples)
                    {
                        newEvt.MoveTo(samplePos);
                        editTrack.AddEvent(newEvt);
                    }
                    else
                    {
                        // Further events are also out of range.
                        break;
                    }
                }
            }
        }

        void PastePayloadToSelectedEvents()
        {
            Undo.RecordObject(editTrack, "Paste Payload");
            EditorUtility.SetDirty(editTrack);

            clippedEvents.Sort(KoreographyEvent.CompareByStartSample);

            IPayload payload = clippedEvents.First().Payload;

            if (payload == null)
            {
                foreach (KoreographyEvent evt in selectedEvents)
                {
                    evt.Payload = null;
                }
            }
            else
            {
                foreach (KoreographyEvent evt in selectedEvents)
                {
                    evt.Payload = payload.GetCopy();
                }
            }
        }

        void PasteEventsAtLocation(System.Object samplePosAsObj)
        {
            int samplePos = (int)samplePosAsObj;

            // The samplePos is already clamped to 0-or-greater.
            //  we now need to handle it if it's beyond the edge of the song.
            if (samplePos >= EditClip.samples)
            {
                if (bSnapTimingToBeat)
                {
                    samplePos = GetSampleOfLastBeat();
                }
                else
                {
                    samplePos = EditClip.samples - 1;
                }
            }

            DuplicateEventsAtLocation(clippedEvents, samplePos, "Paste Event");
        }

        /// <summary>
        /// Duplicates the events in the source list beginning at the specified location, recording
        /// them as the operation for the Undo system.
        /// </summary>
        /// <param name="srcEvents">Events to duplicate.</param>
        /// <param name="samplePos">The position to seed the duplication.</param>
        /// <param name="operationSingle">The Operation to record this as, in single (made multiple internally).</param>
        void DuplicateEventsAtLocation(List<KoreographyEvent> srcEvents, int samplePos, string operationSingle)
        {
            if (srcEvents.Count > 0)
            {
                srcEvents.Sort(KoreographyEvent.CompareByStartSample);

                Undo.RecordObject(editTrack, srcEvents.Count == 1 ? operationSingle : operationSingle + "s");
                EditorUtility.SetDirty(editTrack);

                // Used for offsetting.
                int startOffset = srcEvents.First().StartSample;

                // Currently runs in sorted order.  Rely upon this.
                foreach (KoreographyEvent addEvt in srcEvents)
                {
                    KoreographyEvent newEvt = addEvt.GetCopy();

                    int newPos = samplePos + (addEvt.StartSample - startOffset);
                    if (newPos < EditClip.samples)
                    {
                        newEvt.MoveTo(newPos);
                        editTrack.AddEvent(newEvt);
                    }
                    else
                    {
                        // Further events are also out of bounds.
                        break;
                    }
                }
            }
        }

        void PlayFromSampleLocation(System.Object samplePosAsObj)
        {
            PlayAudio((int)samplePosAsObj);
        }

        void SetPlaybackAnchorToLocation(System.Object samplePosAsObj)
        {
            int newPos = (int)samplePosAsObj;

            if (IsStopped())
            {
                ScanToSample(newPos);
            }

            displayState.playbackAnchorSamplePosition = newPos;
        }

        void ClearCustomPlaybackAnchorLocation()
        {
            displayState.playbackAnchorSamplePosition = 0;

            // Also, make sure the playhead is visible if the playhead isn't at 0.
            if (displayState.playheadSamplePosition != 0)
            {
                bShowPlayhead = true;
            }
        }

        #endregion
        #region Analysis Related

#if !KOREO_NON_PRO
        void UpdateAnalysisRange()
        {
            // ASSUMPTION: In order to get here, the EditClip *must* be valid.

            Vector2 pos = Vector2.zero;
            pos.x = WaveStartPositionOffset + analysisRange.x * curWaveViewWidth;
            int startSample = waveDisplay.GetSamplePositionOfPoint(pos, displayState);
            startSample = Mathf.Clamp(startSample, 0, EditClip.samples);

            pos.x = WaveStartPositionOffset + analysisRange.y * curWaveViewWidth;
            int endSample = waveDisplay.GetSamplePositionOfPoint(pos, displayState);
            endSample = Mathf.Clamp(endSample, 0, EditClip.samples);

            analysisWindow.SetRangeExtents(startSample, endSample);
            analysisWindow.Repaint();
        }
#endif

        internal int GetMinVisibleSamplePos()
        {
            // Automatically clamps to 0 on the lower-bound.
            return waveDisplay.GetSamplePositionOfPoint(Vector2.zero, displayState);
        }

        internal int GetMaxVisibleSamplePos()
        {
            int samplePos = 0;

            if (EditClip != null)
            {
                // Does not clamp to the song length.  We need to do that here.
                samplePos = waveDisplay.GetSamplePositionOfPoint(new Vector2(curWaveViewWidth, 0f), displayState);
                samplePos = Mathf.Clamp(samplePos, 0, EditClip.samples);
            }

            return samplePos;
        }

#if !KOREO_NON_PRO
        // For use by external sources.  KoreographyEditor logic should use "UpdateAnalysisRange".
        internal void SetSelectedRange(Vector2 range)
        {
            if (analysisRange != range)
            {
                analysisRange = range;
            }
        }
#endif

        #endregion

        /// <summary>
        /// 导出json文件
        /// </summary>
        void Exprot(Koreography editKoreo)
        {
            Debug.Log("Exprot ----------> " + editKoreo.name);

            JsonData json = new JsonData();
            json["Name"] = editKoreo.name;
            json["SourceClipName"] = editKoreo.SourceClipName;
            json["SampleRate"] = editKoreo.SampleRate;

            int frequency = (EditClip != null) ? EditClip.frequency : 44100;
            json["BPM"] = (int)editKoreo.GetTempoSectionAtIndex(editTempoSectionIdx).GetBPM(frequency);

            if (editKoreo.Tracks.Count > 0)
            {
                JsonData tracksData = new JsonData();


                bool trackAdd = false;

                for (int i = 0; i < editKoreo.Tracks.Count; i++)
                {
                    JsonData trackData = new JsonData();


                    KoreographyTrackBase track = editKoreo.Tracks[i];
                    trackData["Name"] = track.name;

                    JsonData evtsData = new JsonData();
              

                    bool evtAdd = false;
                    for (int j = 0; j < track.GetAllEvents().Count; j++)
                    {
                        KoreographyEvent evt = track.GetAllEvents()[j];
                        if (evt.Payload != null)
                        {
                            JsonData evtData = new JsonData();
                            if (evt.HasTextPayload())
                            {
                                evtData["Type"] = "TextPayload";
                                evtData["Val"] = evt.GetTextValue();
                            }
                            else if (evt.HasFloatPayload())
                            {
                                evtData["Type"] = "FloatPayload";
                                evtData["Val"] = evt.GetFloatValue();
                            }
                            else if (evt.HasIntPayload())
                            {
                                evtData["Type"] = "IntPayload";
                                evtData["Val"] = evt.GetIntValue();
                            }
                            else if (evt.HasColorPayload())
                            {
                                evtData["Type"] = "ColorPayload";
                                evtData["Val"] = evt.GetColorValue().ToString();
                            }
                            evtData["StartSample"] = evt.StartSample;
                            evtData["EndSample"] = evt.EndSample;
                            evtData["StartTime"] = (int)System.TimeSpan.FromSeconds((double)evt.StartSample / (double)EditClip.frequency).TotalMilliseconds; //evt.StartTime;
                            evtData["EndTime"] = (int)System.TimeSpan.FromSeconds((double)evt.EndSample / (double)EditClip.frequency).TotalMilliseconds; //evt.EndTime;
                            evtsData.Add(evtData);
                            evtAdd = true;
                        }
                    }

                    if (evtAdd)
                    {
                        trackData["Notes"] = evtsData;
                        tracksData.Add(trackData);
                        trackAdd = true;
                    }
                }

                if (trackAdd)
                {
                    json["Tracks"] = tracksData;
                }
            }

            File.WriteAllText(Path.Combine(Application.dataPath + "/Export/Audio", editKoreo.name + ".json"), JsonFormat(json.ToJson()));

            AssetDatabase.Refresh();

            Debug.Log("Exprot ----------> Succ ");
        }

        //将json数据进行格式化
        public static string JsonFormat(string str)
        {
            JsonSerializer serializer = new JsonSerializer();
            StringReader sReader = new StringReader(str);
            JsonTextReader jReader = new JsonTextReader(sReader);
            object readerObj = serializer.Deserialize(jReader);
            if (readerObj != null)
            {
                StringWriter sWriter = new StringWriter();
                JsonTextWriter jWriter = new JsonTextWriter(sWriter)
                {
                    Formatting = Newtonsoft.Json.Formatting.Indented,
                    Indentation = 2,
                    IndentChar = ' '
                };
                serializer.Serialize(jWriter, readerObj);
                return sWriter.ToString();
            }
            else
            {
                return str;
            }
        }
    }
}
