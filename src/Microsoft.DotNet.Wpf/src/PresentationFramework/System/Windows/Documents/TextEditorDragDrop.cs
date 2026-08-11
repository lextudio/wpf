// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using MS.Internal;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;  // WindowInteropHelper
using System.Windows.Controls; // ScrollChangedEventArgs
using System.Windows.Controls.Primitives;  // CharacterCasing, TextBoxBase
using System.Windows.Data; // BindingExpression
using System.Windows.Media;
using MS.Win32;

// 
// Description: A Component of TextEditor class supposrtinng Drag-and-drop 
//              functionality
//

namespace System.Windows.Documents
{
    /// <summary>
    /// Text editing service for controls.
    /// </summary>
    internal static partial class TextEditorDragDrop
    {
        //------------------------------------------------------
        //
        //  Class Internal Methods
        //
        //------------------------------------------------------

        #region Class Internal Methods

        // Registers all text editing command handlers for a given control type.
        // The upstream body registered the OLE DragDrop event handlers, which
        // were deleted with _DragDropProcess (every build defines HAS_UNO); the
        // Uno drag path is wired on the renderer instead (TextEditorDragDropUno).
        internal static void _RegisterClassHandlers(Type controlType, bool readOnly, bool registerEventListeners)
        {
        }

        #endregion Class Internal Methods        

        //------------------------------------------------------
        //
        //  Class Internal Types
        //
        //------------------------------------------------------

        #region Class Internal Types

        /// <summary>
        /// Shared contract between the WPF OLE drag-drop process and the Uno no-op stub,
        /// allowing all call sites in TextEditor/TextEditorMouse/TextBoxBase to be unmodified.
        /// </summary>
        internal interface IDragDropProcess
        {
            bool SourceOnMouseLeftButtonDown(Point mouseDownPoint);
            void DoMouseLeftButtonUp(MouseButtonEventArgs e);
            bool SourceOnMouseMove(Point mouseMovePoint);
            void TargetEnsureDropCaret();
            void TargetOnDragEnter(DragEventArgs e);
            void TargetOnDragOver(DragEventArgs e);
            void TargetOnDrop(DragEventArgs e);
            void DeleteCaret();
        }

        // The OLE _DragDropProcess implementation (upstream WPF's DoDragDrop
        // loop over Ole32) was deleted: every build of this repo defines
        // HAS_UNO, so it never compiled. The Uno stub (_DragDropProcessUno,
        // TextEditorDragDrop.uno.cs) implements the IDragDropProcess contract
        // below; the deleted code remains in git history.


        /// <summary>
        /// An event reporting that the query continue drag during drag-and-drop operation.
        /// </summary>
        internal static void OnQueryContinueDrag(object sender, QueryContinueDragEventArgs e)
        {
            TextEditor This = TextEditor._GetTextEditor(sender);

            if (This == null)
            {
                return;
            }

            // Ignore the event if the editor has been detached from its scope
            if (!This._IsEnabled)
            {
                return;
            }

            // Consider event handled
            e.Handled = true;

            e.Action = DragAction.Continue;
            bool mouseUp = (((int)e.KeyStates & (int)DragDropKeyStates.LeftMouseButton) == 0);
            if (e.EscapePressed)
            {
                e.Action = DragAction.Cancel;
            }
            else if (mouseUp)
            {
                e.Action = DragAction.Drop;
            }
        }

        /// <summary>
        /// An event reporting that the give feedback during drag-and-drop operation.
        /// </summary>
        internal static void OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            TextEditor This = TextEditor._GetTextEditor(sender);

            if (This == null)
            {
                return;
            }

            // Ignore the event if the editor has been detached from its scope
            if (!This._IsEnabled)
            {
                return;
            }

            // Show the default DragDrop cursor.
            e.UseDefaultCursors = true;

            // Consider event handled
            e.Handled = true;
        }

        /// <summary>
        /// An event reporting that the drag enter during drag-and-drop operation.
        /// </summary>
        internal static void OnDragEnter(object sender, DragEventArgs e)
        {
            // Consider event handled
            e.Handled = true;

            TextEditor This = TextEditor._GetTextEditor(sender);

            if (This == null)
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            // Ignore the event if the editor has been detached from its scope
            if (!This._IsEnabled || This.TextView == null || This.TextView.RenderScope == null)
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            // If there's no supported data available, don't allow the drag-and-drop.
            if (e.Data == null)
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            // Ignore the event if there isn't the dropable(pasteable) data format
            if (TextEditorCopyPaste.GetPasteApplyFormat(This, e.Data) == string.Empty)
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            TextEditorTyping._FlushPendingInputItems(This);

            if (!This.TextView.Validate(e.GetPosition(This.TextView.RenderScope)))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            This._dragDropProcess.TargetOnDragEnter(e);
        }

        /// <summary>
        /// An event reporting that the drag over during drag-and-drop operation.
        /// </summary>
        internal static void OnDragOver(object sender, DragEventArgs e)
        {
            // Consider event handled
            e.Handled = true;

            TextEditor This = TextEditor._GetTextEditor(sender);

            if (This == null)
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            // Ignore the event if the editor has been detached from its scope
            if (!This._IsEnabled || This.TextView == null || This.TextView.RenderScope == null)
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            // If there's no supported data available, don't allow the drag-and-drop.
            if (e.Data == null)
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            // Ignore the event if there isn't the dropable(pasteable) data format
            if (TextEditorCopyPaste.GetPasteApplyFormat(This, e.Data) == string.Empty)
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            TextEditorTyping._FlushPendingInputItems(This);
            if (!This.TextView.Validate(e.GetPosition(This.TextView.RenderScope)))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            This._dragDropProcess.TargetOnDragOver(e);
        }

        /// <summary>
        /// An event reporting that the drag leave during drag-and-drop operation.
        /// </summary>
        internal static void OnDragLeave(object sender, DragEventArgs e)
        {
            // Consider event handled
            e.Handled = true;

            TextEditor This = TextEditor._GetTextEditor(sender);

            if (This == null)
            {
                return;
            }

            //
            // Remove UI feedback here if UI is specified on DragEnter.
            //
            // Ignore the event if the editor has been detached from its scope
            if (!This._IsEnabled)
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            TextEditorTyping._FlushPendingInputItems(This);
            if (!This.TextView.Validate(e.GetPosition(This.TextView.RenderScope)))
            {
                return;
            }
        }
       
        /// <summary>
        /// An event reporting that the drop happened.
        /// </summary>
        internal static void OnDrop(object sender, DragEventArgs e)
        {
            TextEditor This = TextEditor._GetTextEditor(sender);

            if (This == null)
            {
                return;
            }

            // Ignore the event if the editor has been detached from its scope
            if (!This._IsEnabled)
            {
                return;
            }

            TextEditorTyping._FlushPendingInputItems(This);
            if (!This.TextView.Validate(e.GetPosition(This.TextView.RenderScope)))
            {
                return;
            }

            This._dragDropProcess.TargetOnDrop(e);
        }

        /// <summary>
        /// An event for clearing the state of the Text Editor after events like drop or drag leave,
        /// Currently, It's clearing the caret which is drawn during dragOver and have never been deleted.
        /// </summary>
        internal static void OnClearState(object sender, DragEventArgs e)
        {
            TextEditor This = TextEditor._GetTextEditor(sender);

            if (This == null)
            {
                return;
            }

           This._dragDropProcess.DeleteCaret();
        }

        #endregion Class Internal Types
    }
}
