﻿using System;
using OpenTK.Graphics;
using System.Drawing;
using System.ComponentModel;
using OpenTK.Input;
using System.Diagnostics;

namespace OpenTK.Platform.MacOS
{
    class CocoaNativeWindow : INativeWindow
    {
        public event EventHandler<EventArgs> Move = delegate { };
        public event EventHandler<EventArgs> Resize = delegate { };
        public event EventHandler<System.ComponentModel.CancelEventArgs> Closing = delegate { };
        public event EventHandler<EventArgs> Closed = delegate { };
        public event EventHandler<EventArgs> Disposed = delegate { };
        public event EventHandler<EventArgs> IconChanged = delegate { };
        public event EventHandler<EventArgs> TitleChanged = delegate { };
        public event EventHandler<EventArgs> VisibleChanged = delegate { };
        public event EventHandler<EventArgs> FocusedChanged = delegate { };
        public event EventHandler<EventArgs> WindowBorderChanged = delegate { };
        public event EventHandler<EventArgs> WindowStateChanged = delegate { };
        public event EventHandler<OpenTK.Input.KeyboardKeyEventArgs> KeyDown = delegate { };
        public event EventHandler<KeyPressEventArgs> KeyPress = delegate { };
        public event EventHandler<OpenTK.Input.KeyboardKeyEventArgs> KeyUp = delegate { };
        public event EventHandler<EventArgs> MouseLeave = delegate { };
        public event EventHandler<EventArgs> MouseEnter = delegate { };

        static readonly IntPtr selNextEventMatchingMask = Selector.Get("nextEventMatchingMask:untilDate:inMode:dequeue:");
        static readonly IntPtr selSendEvent = Selector.Get("sendEvent:");
        static readonly IntPtr selUpdateWindows = Selector.Get("updateWindows");
        static readonly IntPtr selContentView = Selector.Get("contentView");
        static readonly IntPtr selConvertRectFromScreen = Selector.Get("convertRectFromScreen:");
        static readonly IntPtr selConvertRectToScreen = Selector.Get("convertRectToScreen:");
        //static readonly IntPtr selPerformClose = Selector.Get("performClose:");
        static readonly IntPtr selClose = Selector.Get("close");
        static readonly IntPtr selTitle = Selector.Get("title");
        static readonly IntPtr selSetTitle = Selector.Get("setTitle:");
        static readonly IntPtr selSetApplicationIconImage = Selector.Get("setApplicationIconImage:");
        static readonly IntPtr selIsKeyWindow = Selector.Get("isKeyWindow");
        static readonly IntPtr selIsVisible = Selector.Get("isVisible");
        static readonly IntPtr selSetIsVisible = Selector.Get("setIsVisible:");
        static readonly IntPtr selFrame = Selector.Get("frame");
        static readonly IntPtr selBounds = Selector.Get("bounds");
        static readonly IntPtr selScreen = Selector.Get("screen");
        static readonly IntPtr selSetFrame = Selector.Get("setFrame:display:");
        static readonly IntPtr selConvertRectToBacking = Selector.Get("convertRectToBacking:");
        static readonly IntPtr selConvertRectFromBacking = Selector.Get("convertRectFromBacking:");
        static readonly IntPtr selFrameRectForContentRect = Selector.Get("frameRectForContentRect:");
        static readonly IntPtr selType = Selector.Get("type");
        static readonly IntPtr selKeyCode = Selector.Get("keyCode");
        static readonly IntPtr selModifierFlags = Selector.Get("modifierFlags");
        static readonly IntPtr selIsARepeat = Selector.Get("isARepeat");
        static readonly IntPtr selCharactersIgnoringModifiers = Selector.Get("charactersIgnoringModifiers");
        static readonly IntPtr selAddTrackingArea = Selector.Get("addTrackingArea:");
        static readonly IntPtr selRemoveTrackingArea = Selector.Get("removeTrackingArea:");
        static readonly IntPtr selTrackingArea = Selector.Get("trackingArea");
        static readonly IntPtr selInitWithRect = Selector.Get("initWithRect:options:owner:userInfo:");
        static readonly IntPtr selOwner = Selector.Get("owner");
        static readonly IntPtr selLocationInWindowOwner = Selector.Get("locationInWindow");
        static readonly IntPtr selHide = Selector.Get("hide");
        static readonly IntPtr selUnhide = Selector.Get("unhide");
        static readonly IntPtr selScrollingDeltaY = Selector.Get("scrollingDeltaY");
        static readonly IntPtr selButtonNumber = Selector.Get("buttonNumber");
        static readonly IntPtr selSetStyleMask = Selector.Get("setStyleMask:");
        static readonly IntPtr selIsMiniaturized = Selector.Get("isMiniaturized");
        static readonly IntPtr selIsZoomed = Selector.Get("isZoomed");
        static readonly IntPtr selMiniaturize = Selector.Get("miniaturize:");
        static readonly IntPtr selDeminiaturize = Selector.Get("deminiaturize:");
        static readonly IntPtr selZoom = Selector.Get("zoom:");
        static readonly IntPtr selLevel = Selector.Get("level");
        static readonly IntPtr selSetLevel = Selector.Get("setLevel:");
        static readonly IntPtr selPresentationOptions = Selector.Get("presentationOptions");
        static readonly IntPtr selSetPresentationOptions = Selector.Get("setPresentationOptions:");
        //static readonly IntPtr selIsInFullScreenMode = Selector.Get("isInFullScreenMode");
        //static readonly IntPtr selExitFullScreenModeWithOptions = Selector.Get("exitFullScreenModeWithOptions:");
        //static readonly IntPtr selEnterFullScreenModeWithOptions = Selector.Get("enterFullScreenMode:withOptions:");

        static readonly IntPtr NSDefaultRunLoopMode;
        static readonly IntPtr NSCursor;

        static CocoaNativeWindow()
        {
            Cocoa.Initialize();
            NSDefaultRunLoopMode = Cocoa.GetStringConstant(Cocoa.FoundationLibrary, "NSDefaultRunLoopMode");
            NSCursor = Class.Get("NSCursor");
        }

        private CocoaWindowInfo windowInfo;
        private IntPtr windowClass;
        private IntPtr trackingArea;
        private bool disposed = false;
        private bool exists = true;
        private bool cursorVisible = true;
        private System.Drawing.Icon icon;
        private LegacyInputDriver inputDriver = new LegacyInputDriver();
        private WindowBorder windowBorder = WindowBorder.Resizable;
        private MacOSKeyMap keyMap = new MacOSKeyMap();
        private OpenTK.Input.KeyboardKeyEventArgs keyArgs = new OpenTK.Input.KeyboardKeyEventArgs();
        private KeyPressEventArgs keyPressArgs = new KeyPressEventArgs((char)0);

        bool exclusiveFullscreen = true;
        bool fullscreenMode;
        Rectangle preFullscreenSize;
        int preFullscreenLevel;
        string preFullscreenTitle;

        public CocoaNativeWindow(int x, int y, int width, int height, string title, GraphicsMode mode, GameWindowFlags options, DisplayDevice device)
        {
            // Create the window class
            windowClass = Class.AllocateClass("OpenTKWindow", "NSWindow");
            Class.RegisterMethod(windowClass, new WindowDidResizeDelegate(WindowDidResize), "windowDidResize:", "v@:@");
            Class.RegisterMethod(windowClass, new WindowDidMoveDelegate(WindowDidMove), "windowDidMove:", "v@:@");
            Class.RegisterMethod(windowClass, new WindowDidBecomeKeyDelegate(WindowDidBecomeKey), "windowDidBecomeKey:", "v@:@");
            Class.RegisterMethod(windowClass, new WindowDidResignKeyDelegate(WindowDidResignKey), "windowDidResignKey:", "v@:@");
            Class.RegisterMethod(windowClass, new WindowShouldCloseDelegate(WindowShouldClose), "windowShouldClose:", "b@:@");
            Class.RegisterMethod(windowClass, new AcceptsFirstResponderDelegate(AcceptsFirstResponder), "acceptsFirstResponder", "b@:");
            Class.RegisterMethod(windowClass, new CanBecomeKeyWindowDelegate(CanBecomeKeyWindow), "canBecomeKeyWindow", "b@:");
            Class.RegisterMethod(windowClass, new CanBecomeMainWindowDelegate(CanBecomeMainWindow), "canBecomeMainWindow", "b@:");

            Class.RegisterClass(windowClass);

            // Create window instance
            var contentRect = new System.Drawing.RectangleF(x, y, width, height);
            var style = GetStyleMask(windowBorder);
            var bufferingType = NSBackingStore.Buffered;

            IntPtr windowPtr;
            windowPtr = Cocoa.SendIntPtr(windowClass, Selector.Alloc);
            windowPtr = Cocoa.SendIntPtr(windowPtr, Selector.Get("initWithContentRect:styleMask:backing:defer:"), contentRect, (int)style, (int)bufferingType, false);
            windowInfo = new CocoaWindowInfo(windowPtr);

            // Set up behavior
            Cocoa.SendIntPtr(windowPtr, Selector.Get("setDelegate:"), windowPtr); // The window class acts as its own delegate 
            Cocoa.SendVoid(windowPtr, Selector.Get("cascadeTopLeftFromPoint:"), new System.Drawing.PointF(20, 20));
            Cocoa.SendVoid(windowPtr, Selector.Get("makeKeyAndOrderFront:"), IntPtr.Zero);
            SetTitle(title, false);

            ResetTrackingArea();
        }

        delegate void WindowDidResizeDelegate(IntPtr self, IntPtr cmd, IntPtr notification);
        delegate void WindowDidMoveDelegate(IntPtr self, IntPtr cmd, IntPtr notification);
        delegate void WindowDidBecomeKeyDelegate(IntPtr self, IntPtr cmd, IntPtr notification);
        delegate void WindowDidResignKeyDelegate(IntPtr self, IntPtr cmd, IntPtr notification);
        delegate bool WindowShouldCloseDelegate(IntPtr self, IntPtr cmd, IntPtr sender);
        delegate bool AcceptsFirstResponderDelegate(IntPtr self, IntPtr cmd);
        delegate bool CanBecomeKeyWindowDelegate(IntPtr self, IntPtr cmd);
        delegate bool CanBecomeMainWindowDelegate(IntPtr self, IntPtr cmd);

        private void WindowDidResize(IntPtr self, IntPtr cmd, IntPtr notification)
        {
            ResetTrackingArea();
            GraphicsContext.CurrentContext.Update(windowInfo);
            Resize(this, EventArgs.Empty);
        }

        private void WindowDidMove(IntPtr self, IntPtr cmd, IntPtr notification)
        {
            Move(this, EventArgs.Empty);
        }

        private void WindowDidBecomeKey(IntPtr self, IntPtr cmd, IntPtr notification)
        {
            FocusedChanged(this, EventArgs.Empty);
        }

        private void WindowDidResignKey(IntPtr self, IntPtr cmd, IntPtr notification)
        {
            FocusedChanged(this, EventArgs.Empty);
        }

        private bool WindowShouldClose(IntPtr self, IntPtr cmd, IntPtr sender)
        {
            var cancelArgs = new CancelEventArgs();
            Closing(this, cancelArgs);

            if (!cancelArgs.Cancel)
            {
                Closed(this, EventArgs.Empty);
                return true;
            }

            return false;
        }

        private bool AcceptsFirstResponder(IntPtr self, IntPtr cmd) 
        { 
            return true; 
        }

        private bool CanBecomeKeyWindow(IntPtr self, IntPtr cmd) 
        { 
            return true;
        }

        private bool CanBecomeMainWindow(IntPtr self, IntPtr cmd) 
        { 
            return true;
        }

        private void ResetTrackingArea()
        {
            var owner = windowInfo.ViewHandle;
            if (trackingArea != IntPtr.Zero)
            {
                Cocoa.SendVoid(owner, selRemoveTrackingArea, trackingArea);
            }

            var ownerBounds = Cocoa.SendRect(owner, selBounds);
            var options = (int)(NSTrackingAreaOptions.MouseEnteredAndExited | NSTrackingAreaOptions.ActiveInKeyWindow | NSTrackingAreaOptions.MouseMoved);

            trackingArea = Cocoa.SendIntPtr(Cocoa.SendIntPtr(Class.Get("NSTrackingArea"), Selector.Alloc),
                selInitWithRect, ownerBounds, options, owner, IntPtr.Zero);

            Cocoa.SendVoid(owner, selAddTrackingArea, trackingArea);
        }

        public void Close()
        {
            // PerformClose is equivalent to pressing the close-button, which
            // does not work in a borderless window. Handle this special case.
            //if (WindowBorder == WindowBorder.Hidden)
            {
                if (WindowShouldClose(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero))
                {
                    Cocoa.SendVoid(windowInfo.Handle, selClose);
                }
            }
//            else
//            {
//                Cocoa.SendVoid(windowInfo.Handle, selPerformClose, windowInfo.Handle);
//            }
        }

        private KeyModifiers GetModifiers(NSEventModifierMask mask)
        {
            OpenTK.Input.KeyModifiers modifiers = 0;
            if ((mask & NSEventModifierMask.ControlKeyMask) != 0) modifiers |= OpenTK.Input.KeyModifiers.Control;
            if ((mask & NSEventModifierMask.ShiftKeyMask) != 0) modifiers |= OpenTK.Input.KeyModifiers.Shift;
            if ((mask & NSEventModifierMask.AlternateKeyMask) != 0) modifiers |= OpenTK.Input.KeyModifiers.Alt;
            return modifiers;
        }

        private void GetKey(ushort keyCode, NSEventModifierMask modifierFlags, OpenTK.Input.KeyboardKeyEventArgs args)
        {
            OpenTK.Input.Key key;
            if (!keyMap.TryGetValue((OpenTK.Platform.MacOS.Carbon.MacOSKeyCode)keyCode, out key))
            {
                key = OpenTK.Input.Key.Unknown;
            }

            args.Key = key;
            args.Modifiers = GetModifiers(modifierFlags);
            args.ScanCode = (uint)keyCode;
        }

        private MouseButton GetMouseButton(int cocoaButtonIndex)
        {
            if (cocoaButtonIndex == 0) return MouseButton.Left;
            if (cocoaButtonIndex == 1) return MouseButton.Right;
            if (cocoaButtonIndex == 2) return MouseButton.Middle;
            if (cocoaButtonIndex >= (int)MouseButton.LastButton)
                return MouseButton.LastButton;

            return (MouseButton)cocoaButtonIndex;
        }

        public void ProcessEvents()
        {
            var e = Cocoa.SendIntPtr(NSApplication.Handle, selNextEventMatchingMask, uint.MaxValue, IntPtr.Zero, NSDefaultRunLoopMode, true);

            if (e == IntPtr.Zero)
                return;

            var type = (NSEventType)Cocoa.SendInt(e, selType);
            switch (type)
            {
                case NSEventType.KeyDown:
                    {
                        var keyCode = Cocoa.SendUshort(e, selKeyCode);
                        var modifierFlags = (NSEventModifierMask)Cocoa.SendUint(e, selModifierFlags);
                        var isARepeat = Cocoa.SendBool(e, selIsARepeat);
                        GetKey(keyCode, modifierFlags, keyArgs);
                        InputDriver.Keyboard[0].SetKey(keyArgs.Key, keyArgs.ScanCode, true);

                        if (!isARepeat || InputDriver.Keyboard[0].KeyRepeat)
                        {
                            KeyDown(this, keyArgs);
                        }

                        var s = Cocoa.FromNSString(Cocoa.SendIntPtr(e, selCharactersIgnoringModifiers));
                        foreach (var c in s)
                        {
                            int intVal = (int)c;
                            if (!Char.IsControl(c) && (intVal < 63232 || intVal > 63235))
                            {
                                // For some reason, arrow keys (mapped 63232-63235) are seen as non-control characters, so get rid of those.

                                keyPressArgs.KeyChar = c;
                                KeyPress(this, keyPressArgs);
                            }
                        }

                        // Steal all keydown events to avoid the annoying "bleep" sound.
                        return;
                    }

                case NSEventType.KeyUp:
                    {
                        var keyCode = Cocoa.SendUshort(e, selKeyCode);
                        var modifierFlags = (NSEventModifierMask)Cocoa.SendUint(e, selModifierFlags);

                        GetKey(keyCode, modifierFlags, keyArgs);
                        InputDriver.Keyboard[0].SetKey(keyArgs.Key, keyArgs.ScanCode, false);

                        KeyUp(this, keyArgs);
                    }
                    break;

                case NSEventType.MouseEntered:
                    {
                        var eventTrackingArea = Cocoa.SendIntPtr(e, selTrackingArea);
                        var trackingAreaOwner = Cocoa.SendIntPtr(eventTrackingArea, selOwner);
                        if (trackingAreaOwner == windowInfo.ViewHandle)
                        {
                            if (!cursorVisible)
                            {
                                SetCursorVisible(false);
                            }

                            MouseEnter(this, EventArgs.Empty);
                        }
                    }
                    break;

                case NSEventType.MouseExited:
                    {
                        var eventTrackingArea = Cocoa.SendIntPtr(e, selTrackingArea);
                        var trackingAreaOwner = Cocoa.SendIntPtr(eventTrackingArea, selOwner);
                        if (trackingAreaOwner == windowInfo.ViewHandle)
                        {
                            if (!cursorVisible)
                            {
                                SetCursorVisible(true);
                            }

                            MouseLeave(this, EventArgs.Empty);
                        }
                    }
                    break;

                case NSEventType.MouseMoved:
                    {
                        var pf = Cocoa.SendPoint(e, selLocationInWindowOwner);
                        var p = new Point((int)pf.X, (int)pf.Y);

                        var s = ClientSize;
                        if (p.X < 0) p.X = 0; 
                        if (p.Y < 0) p.Y = 0;
                        if (p.X > s.Width) p.X = s.Width;
                        if (p.Y > s.Height) p.Y = s.Height;
                        p.Y = s.Height - p.Y;

                        InputDriver.Mouse[0].Position = p;
                    }
                    break;

                case NSEventType.ScrollWheel:
                    {
                        var scrollingDelta = Cocoa.SendFloat(e, selScrollingDeltaY);
                        InputDriver.Mouse[0].WheelPrecise += scrollingDelta;
                    }
                    break;

                case NSEventType.LeftMouseDown:
                case NSEventType.RightMouseDown:
                case NSEventType.OtherMouseDown:
                    {
                        var buttonNumber = Cocoa.SendInt(e, selButtonNumber);
                        InputDriver.Mouse[0][GetMouseButton(buttonNumber)] = true;
                    }
                    break;

                case NSEventType.LeftMouseUp:
                case NSEventType.RightMouseUp:
                case NSEventType.OtherMouseUp:
                    {
                        var buttonNumber = Cocoa.SendInt(e, selButtonNumber);
                        InputDriver.Mouse[0][GetMouseButton(buttonNumber)] = false;
                    }
                    break;
            }

            Cocoa.SendVoid(NSApplication.Handle, selSendEvent, e);
            Cocoa.SendVoid(NSApplication.Handle, selUpdateWindows);
        }

        public System.Drawing.Point PointToClient(System.Drawing.Point point)
        {
            var r = Cocoa.SendRect(windowInfo.Handle, selConvertRectFromScreen, new RectangleF(point.X, point.Y, 0, 0));
            return new Point((int)r.X, (int)(GetContentViewFrame().Height - GetCurrentScreenFrame().Height - r.Y));
        }

        public System.Drawing.Point PointToScreen(System.Drawing.Point point)
        {
            var r = Cocoa.SendRect(windowInfo.Handle, selConvertRectToScreen, new RectangleF(point.X, point.Y, 0, 0));
            return new Point((int)r.X, (int)(-GetContentViewFrame().Height + GetCurrentScreenFrame().Height - r.Y));
        }

        public System.Drawing.Icon Icon
        {
            get { return icon; }
            set
            {
                icon = value;
                Cocoa.SendVoid(NSApplication.Handle, selSetApplicationIconImage, Cocoa.ToNSImage(icon.ToBitmap()));
                IconChanged(this, EventArgs.Empty);
            }
        }

        public string Title
        {
            get
            {
                return Cocoa.FromNSString(Cocoa.SendIntPtr(windowInfo.Handle, selTitle));
            }
            set
            {
                SetTitle(value, true);
            }
        }

        public bool Focused
        {
            get
            {
                return Cocoa.SendBool(windowInfo.Handle, selIsKeyWindow);
            }
        }

        public bool Visible
        {
            get
            {
                return Cocoa.SendBool(windowInfo.Handle, selIsVisible);
            }
            set
            {
                Cocoa.SendVoid(windowInfo.Handle, selSetIsVisible, value);
                VisibleChanged(this, EventArgs.Empty);
            }
        }

        public bool Exists
        {
            get
            {
                return exists;
            }
        }

        public IWindowInfo WindowInfo
        {
            get
            {
                return windowInfo;
            }
        }

        private void RestoreWindowState()
        {
            var ws = WindowState;
            if (ws == WindowState.Fullscreen)
            {
                //Cocoa.SendVoid(windowInfo.ViewHandle, selExitFullScreenModeWithOptions, IntPtr.Zero);

                SetMenuVisible(true);
                if (exclusiveFullscreen)
                {
                    OpenTK.Platform.MacOS.Carbon.CG.DisplayReleaseAll();
                    Cocoa.SendVoid(windowInfo.Handle, selSetLevel, preFullscreenLevel);
                }

                Cocoa.SendVoid(windowInfo.Handle, selSetStyleMask, (uint)GetStyleMask(windowBorder));
                Bounds = preFullscreenSize;
                SetTitle(preFullscreenTitle, false); // For some reason, the title is lost
                fullscreenMode = false;
            }
            else if (ws == WindowState.Maximized)
            {
                Cocoa.SendVoid(windowInfo.Handle, selZoom, windowInfo.Handle);
            }
            else if (ws == WindowState.Minimized)
            {
                Cocoa.SendVoid(windowInfo.Handle, selDeminiaturize, windowInfo.Handle);
            }
        }

        public WindowState WindowState
        {
            get
            {
                if (fullscreenMode)
                    return WindowState.Fullscreen;

                if (Cocoa.SendBool(windowInfo.Handle, selIsMiniaturized))
                    return WindowState.Minimized;

                if (Cocoa.SendBool(windowInfo.Handle, selIsZoomed))
                    return WindowState.Maximized;

                return WindowState.Normal;
            }
            set
            {
                var oldState = WindowState;
                if (oldState == value)
                    return;

                RestoreWindowState();

                if (value == WindowState.Fullscreen)
                {
//                    NSApplicationPresentationOptions options = 
//                        NSApplicationPresentationOptions.DisableAppleMenu |
//                        NSApplicationPresentationOptions.HideMenuBar |
//                        NSApplicationPresentationOptions.HideDock;
//
//                    // "Exclusive fullscreen"?
//                    //NSApplicationPresentationOptions.DisableProcessSwitching;
//
//                    var obj = Cocoa.SendIntPtr(Class.Get("NSNumber"), Selector.Get("numberWithUnsignedLong:"), (ulong)options);
//                    var key = Cocoa.ToNSString("NSFullScreenModeApplicationPresentationOptions");
//                    
//                    var nsDictionary = Cocoa.SendIntPtr(Class.Get("NSDictionary"), Selector.Alloc);
//                    nsDictionary = Cocoa.SendIntPtr(nsDictionary, Selector.Get("initWithObjectsAndKeys:"), obj, key, IntPtr.Zero);
//                    nsDictionary = Cocoa.SendIntPtr(nsDictionary, Selector.Autorelease);
//
//                    Cocoa.SendVoid(windowInfo.ViewHandle, selEnterFullScreenModeWithOptions, GetCurrentScreen(), nsDictionary);

                    fullscreenMode = true;
                    preFullscreenSize = Bounds;
                    preFullscreenTitle = Title;
                    var screenFrame = GetCurrentScreenFrame();

                    if (exclusiveFullscreen)
                    {
                        preFullscreenLevel = Cocoa.SendInt(windowInfo.Handle, selLevel);
                        var windowLevel = OpenTK.Platform.MacOS.Carbon.CG.ShieldingWindowLevel();

                        OpenTK.Platform.MacOS.Carbon.CG.CaptureAllDisplays();
                        Cocoa.SendVoid(windowInfo.Handle, selSetLevel, windowLevel);
                    }

                    Cocoa.SendVoid(windowInfo.Handle, selSetStyleMask, (uint)NSWindowStyle.Borderless);
                    Bounds = new Rectangle((int)screenFrame.X, (int)screenFrame.Y, (int)screenFrame.Width, (int)screenFrame.Height);
                    SetMenuVisible(false);
                }
                else if (value == WindowState.Maximized)
                {
                    Cocoa.SendVoid(windowInfo.Handle, selZoom, windowInfo.Handle);
                }
                else if (value == WindowState.Minimized)
                {
                    Cocoa.SendVoid(windowInfo.Handle, selMiniaturize, windowInfo.Handle);
                }

                WindowStateChanged(this, EventArgs.Empty);
                WindowDidResize(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            }
        }

        public WindowBorder WindowBorder
        {
            get 
            { 
                return windowBorder;
            }
            set
            {
                windowBorder = value;
                Cocoa.SendVoid(windowInfo.Handle, selSetStyleMask, (uint)GetStyleMask(windowBorder));
                WindowBorderChanged(this, EventArgs.Empty);
            }
        }

        private static NSWindowStyle GetStyleMask(WindowBorder windowBorder)
        {
            switch (windowBorder)
            {
                case WindowBorder.Resizable: return NSWindowStyle.Closable | NSWindowStyle.Miniaturizable | NSWindowStyle.Titled | NSWindowStyle.Resizable;
                case WindowBorder.Fixed: return NSWindowStyle.Closable | NSWindowStyle.Miniaturizable | NSWindowStyle.Titled;
                case WindowBorder.Hidden: return NSWindowStyle.Borderless;
            }

            return (NSWindowStyle)0;
        }

        public System.Drawing.Rectangle Bounds
        {
            get
            {
                var r = Cocoa.SendRect(windowInfo.Handle, selFrame);
                return new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
            }
            set
            {
                Cocoa.SendVoid(windowInfo.Handle, selSetFrame, new RectangleF(value.X, value.Y, value.Width, value.Height), true);
            }
        }

        public System.Drawing.Point Location
        {
            get 
            { 
                return Bounds.Location;
            }
            set
            {
                var b = Bounds;
                b.Location = value;
                Bounds = b;
            }
        }

        public System.Drawing.Size Size
        {
            get 
            { 
                return Bounds.Size;
            }
            set
            {
                var b = Bounds;
                b.Y -= Bounds.Height;
                b.Y += value.Height;
                b.Size = value;
                Bounds = b;
            }
        }

        public int X
        {
            get 
            {
                return Bounds.X;
            }
            set
            {
                var b = Bounds;
                b.X = value;
                Bounds = b;
            }
        }

        public int Y
        {
            get
            {
                return Bounds.Y;
            }
            set
            {
                var b = Bounds;
                b.Y = value;
                Bounds = b;
            }
        }

        public int Width
        {
            get { return ClientRectangle.Width; }
            set
            {
                var s = Size;
                s.Width = value;
                ClientSize = s;
            }
        }

        public int Height
        {
            get { return ClientRectangle.Height; }
            set
            {
                var s = Size;
                s.Height = value;
                ClientSize = s;
            }
        }

        public System.Drawing.Rectangle ClientRectangle
        {
            get
            {
                var contentViewBounds = Cocoa.SendRect(windowInfo.ViewHandle, selBounds);
                var bounds = Cocoa.SendRect(windowInfo.Handle, selConvertRectToBacking, contentViewBounds);
                return new Rectangle((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height); 
            }
            set 
            {
                ClientSize = value.Size; // Just set size, to be consistent with WinGLNative.
            }
        }

        public System.Drawing.Size ClientSize
        {
            get 
            {
                return ClientRectangle.Size;
            }
            set
            {
                var r_scaled = Cocoa.SendRect(windowInfo.Handle, selConvertRectFromBacking, new RectangleF(PointF.Empty, value));
                var r = Cocoa.SendRect(windowInfo.Handle, selFrameRectForContentRect, r_scaled);
                Size = new Size((int)r.Width, (int)r.Height);
            }
        }

        public OpenTK.Input.IInputDriver InputDriver
        {
            get
            {
                return inputDriver;
            }
        }

        public bool CursorVisible
        {
            get { return cursorVisible; }
            set
            {
                cursorVisible = value;
                if (value)
                {
                    SetCursorVisible(true);
                }
                else
                {
                    SetCursorVisible(false);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            Debug.Print("Disposing of CocoaNativeWindow.");

            CursorVisible = true;
            disposed = true;
            exists = false;

            if (disposing)
            {
                if (trackingArea != IntPtr.Zero)
                {
                    Cocoa.SendVoid(windowInfo.ViewHandle, selRemoveTrackingArea, trackingArea);
                    Cocoa.SendVoid(trackingArea, Selector.Release);
                    trackingArea = IntPtr.Zero;
                }

                Cocoa.SendVoid(windowInfo.Handle, Selector.Release);
            }

            Disposed(this, EventArgs.Empty);
        }

        ~CocoaNativeWindow()
        {
            Dispose(false);
        }

        public static IntPtr GetView(IntPtr windowHandle)
        {
            return Cocoa.SendIntPtr(windowHandle, selContentView);
        }

        private RectangleF GetContentViewFrame()
        {
            return Cocoa.SendRect(windowInfo.ViewHandle, selFrame);
        }
        
        private IntPtr GetCurrentScreen()
        {
            return Cocoa.SendIntPtr(windowInfo.Handle, selScreen);
        }

        private RectangleF GetCurrentScreenFrame()
        {
            return Cocoa.SendRect(GetCurrentScreen(), selFrame);
        }

        private void SetCursorVisible(bool visible)
        {
            Cocoa.SendVoid(NSCursor, visible ? selUnhide : selHide);
        }

        private void SetMenuVisible(bool visible)
        {
            var options = (NSApplicationPresentationOptions)Cocoa.SendInt(NSApplication.Handle, selPresentationOptions);
            var changedOptions = NSApplicationPresentationOptions.HideMenuBar | NSApplicationPresentationOptions.HideDock;

            if (!visible)
            {
                options |= changedOptions;
            }
            else
            {
                options &= ~changedOptions;
            }

            Cocoa.SendVoid(NSApplication.Handle, selSetPresentationOptions, (int)options);
        }

        private void SetTitle(string newTitle, bool callEvent)
        {
            Cocoa.SendIntPtr(windowInfo.Handle, selSetTitle, Cocoa.ToNSString(newTitle));
            if (callEvent)
            {
                TitleChanged(this, EventArgs.Empty);
            }
        }
    }
}
