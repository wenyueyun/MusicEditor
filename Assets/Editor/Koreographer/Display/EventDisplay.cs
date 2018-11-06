//----------------------------------------------
//            	   Koreographer                 
//    Copyright © 2014-2017 Sonic Bloom, LLC    
//----------------------------------------------

using UnityEditor;
using UnityEngine;

namespace SonicBloom.Koreo.EditorUI
{
    internal enum EventEditMode
    {
        None,
        ResizeLeft,
        ResizeRight,
        Move,
    }

    internal static class EventDisplay
    {
        static int MinPixelWidth = 3;
        static float PeekUIMouseWidthBuffer = 2f;
        static float PeekUIWidth = EditorGUIUtility.fieldWidth / 2f;
        public static void ValidateDisplayRect(ref Rect displayRect)
        {
            // Fix up minimum width situation.
            if (displayRect.width < MinPixelWidth)
            {
                displayRect.x -= MinPixelWidth / 2;
                displayRect.width = MinPixelWidth;
            }
        }

        public static void Draw(Rect displayRect, KoreographyTrackBase track, KoreographyEvent drawEvent, bool isSelected = false)
        {
            if (drawEvent.IsOneOff())
            {
                DrawOneOff(displayRect, drawEvent, isSelected);

                // TODO: Ensure that the width of these is where we want it... this implies
                //  that the current Rect for OneOffs isn't centered.  If it's odd, this might
                //  actually be what we want, though.
                displayRect.width += PeekUIMouseWidthBuffer;
                if (displayRect.Contains(Event.current.mousePosition) || isSelected)
                {
                    DoPeekUI(displayRect, track, drawEvent);
                }
            }
            else
            {
                if (drawEvent.Payload != null)
                {
                    DrawPayload(displayRect, track, drawEvent, isSelected);
                }
                else
                {
                    DrawNoPayload(displayRect, drawEvent, isSelected);
                }
            }
        }

        public static void DrawOneOff(Rect displayRect, KoreographyEvent drawEvent, bool isSelected = false)
        {
            Color originalBG = GUI.backgroundColor;

            GUI.backgroundColor = isSelected ? Color.green : Color.magenta;

            GUI.Box(displayRect, "", KoreographyEditorSkin.box);

            GUI.backgroundColor = originalBG;
        }

        /// <summary>
        /// Draws the payload.  DOES NOT do a null-check on the Payload or the KoreographyEvent.
        ///  Assumes both are valid.
        /// </summary>
        /// <param name="displayRect">The Rect into which to draw the UI.</param>
        /// <param name="track">The Koreography Track this Payload is found within.</param>
        /// <param name="drawEvent">The KoreographyEvent containing the Payload to draw.</param>
        /// <param name="isSelected">Is selected if set to <c>true</c>.</param>
        public static void DrawPayload(Rect displayRect, KoreographyTrackBase track, KoreographyEvent drawEvent, bool isSelected = false)
        {
#if (UNITY_4_5 || UNITY_4_6 || UNITY_4_7)
			if (PayloadDisplay.DoGUI(drawEvent.Payload, displayRect, track, isSelected))
#else
            if (drawEvent.Payload.DoGUI(displayRect, track, isSelected))
#endif
            {
                GUI.changed = false;
                EditorUtility.SetDirty(track);
            }
        }

        public static void DrawNoPayload(Rect displayRect, KoreographyEvent drawEvent, bool isSelected = false)
        {
            Color originalBG = GUI.backgroundColor;
            GUI.backgroundColor = isSelected ? Color.green : Color.red;

            GUIStyle labelSkin = GUI.skin.GetStyle("Label");
            TextAnchor originalAlign = labelSkin.alignment;
            labelSkin.alignment = TextAnchor.MiddleCenter;

            GUI.Box(displayRect, "No Payload", KoreographyEditorSkin.box);

            labelSkin.alignment = originalAlign;

            GUI.backgroundColor = originalBG;
        }

        static void DoPeekUI(Rect displayRect, KoreographyTrackBase track, KoreographyEvent drawEvent)
        {
            // Resize and reposition the DisplayRect based on 
            displayRect.y -= displayRect.height * 1.5f;
            displayRect.width = PeekUIWidth;
            displayRect.x -= displayRect.width / 2f;

            // UI entries are left-aligned within their fields.  This checks to see if the
            //  position is such that it would show up "offscreen".
            if (displayRect.xMin < 0)
            {
                displayRect.x -= displayRect.xMin;
            }

            // Determine what content to draw.
            if (drawEvent.Payload == null)
            {
                DrawNoPayload(displayRect, drawEvent);
            }
            else
            {
                DrawPayload(displayRect, track, drawEvent);
            }

            // The KoreographyEditor won't always Repaint quickly.  If we're not currently Repainting, do so.
            //  This should only be done when we're hovering.  Click/Drag is already taken care of by the
            //  Koreography Editor itself.
            if (Event.current.type == EventType.MouseMove)
            {
                // This class is only used by an open Koreography Editor.  No need to null-check here.
                KoreographyEditor.TheEditor.Repaint();
            }
        }
    }
}
