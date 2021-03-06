﻿using FlaUI.Core.AutomationElements.Infrastructure;
using FlaUI.Core;
using OpenRPA.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;
using FlaUI.Core.AutomationElements;

namespace OpenRPA
{
    public class UIElement : IElement
    {
        public UIElement(AutomationElement Element)
        {
            RawElement = Element;
            ProcessId = Element.Properties.ProcessId.ValueOrDefault;
            Id = Element.Properties.AutomationId.ValueOrDefault;
            Name = Element.Properties.Name.ValueOrDefault;
            ClassName = Element.Properties.ClassName.ValueOrDefault;
            Type = Element.Properties.ControlType.ValueOrDefault.ToString();
        }
        public void Refresh()
        {
            try
            {
                int pendingCounter = 0;
                while (!RawElement.Properties.BoundingRectangle.IsSupported && pendingCounter < 6)
                {
                    System.Windows.Forms.Application.DoEvents();
                    System.Threading.Thread.Sleep(50);
                    pendingCounter++;
                }
                ProcessId = RawElement.Properties.ProcessId.ValueOrDefault;
                Id = RawElement.Properties.AutomationId.ValueOrDefault;
                Name = RawElement.Properties.Name.ValueOrDefault;
                ClassName = RawElement.Properties.ClassName.ValueOrDefault;
                Type = RawElement.Properties.ControlType.ValueOrDefault.ToString();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "");
            }
        }
        public AutomationElement RawElement { get; private set; }
        object IElement.RawElement { get => RawElement; set => RawElement = value as AutomationElement; }
        public System.Drawing.Rectangle Rectangle
        {
            get
            {
                if (RawElement == null) return System.Drawing.Rectangle.Empty;
                if (!RawElement.Properties.BoundingRectangle.IsSupported) return System.Drawing.Rectangle.Empty;
                return new System.Drawing.Rectangle((int)RawElement.Properties.BoundingRectangle.Value.X,
                    (int)RawElement.Properties.BoundingRectangle.Value.Y, (int)RawElement.Properties.BoundingRectangle.Value.Width,
                    (int)RawElement.Properties.BoundingRectangle.Value.Height);
            }
        }
        public int ProcessId { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string ClassName { get; set; }
        public string Type { get; set; }
        public string Path => string.Format("{0}/{1}", Parent == null ? string.Empty : Parent.Path, Type);
        public string ControlType
        {
            get
            {
                try
                {
                    return RawElement.Properties.ControlType.ToString();
                }
                catch (Exception)
                {
                }
                return FlaUI.Core.Definitions.ControlType.Custom.ToString();
            }
        }
        public bool SupportInput
        {
            get
            {
                try
                {
                    //return rawElement.Patterns.TextEdit.IsSupported || rawElement.Patterns.Text.IsSupported || rawElement.Patterns.Text2.IsSupported
                    return RawElement.ControlType == FlaUI.Core.Definitions.ControlType.Edit
                        || RawElement.ControlType == FlaUI.Core.Definitions.ControlType.Document;
                }
                catch (Exception)
                {
                }
                return false;
            }
        }
        public UIElement Parent
        {
            get
            {
                //if (TreeWalker.RawViewWalker.GetParent(rawElement) is AutomationElement rawParent)
                //{
                //    return new UIElement(rawParent);
                //}
                return new UIElement(RawElement.Parent);
                //return null;
            }
        }
        public void Focus()
        {
            UntilResponsive();
            try
            {
                //rawElement.SetFocus();
                RawElement.Focus();
            }
            catch
            {
            }
        }
        private void UntilResponsive()
        {
            if (ProcessId <= 0) return;
            var process = System.Diagnostics.Process.GetProcessById(ProcessId);
            while (!process.Responding) { }
        }
        public void Click()
        {
            try
            {
                if (RawElement.Patterns.Invoke.IsSupported)
                {
                    var invokePattern = RawElement.Patterns.Invoke.Pattern;
                    invokePattern.Invoke();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public Task Highlight(bool Blocking, System.Drawing.Color Color, TimeSpan Duration)
        {
            if (!Blocking) {
                Task.Run(() => _Highlight(Color, Duration));
                return Task.CompletedTask;
            }
            return _Highlight(Color, Duration);
        }
        public Task _Highlight(System.Drawing.Color Color, TimeSpan Duration)
        {
            using (Interfaces.Overlay.OverlayWindow _overlayWindow = new Interfaces.Overlay.OverlayWindow())
            {
                _overlayWindow.Visible = true;
                _overlayWindow.SetTimeout(Duration);
                _overlayWindow.Bounds = Rectangle;
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                do
                {
                    System.Threading.Thread.Sleep(10);
                    _overlayWindow.TopMost = true;
                } while (_overlayWindow.Visible && sw.Elapsed < Duration);
                return Task.CompletedTask;
            }
        }
        public string Value
        {
            get
            {
                if (RawElement.Properties.IsPassword.TryGetValue(out var isPassword) && isPassword)
                {
                    throw new FlaUI.Core.Exceptions.MethodNotSupportedException($"Text from element '{ToString()}' cannot be retrieved because it is set as password.");
                }
                if (RawElement.Patterns.Value.TryGetPattern(out var valuePattern) &&
                    valuePattern.Value.TryGetValue(out var value))
                {
                    return value;
                }
                if (RawElement.Patterns.Text.TryGetPattern(out var textPattern))
                {
                    return textPattern.DocumentRange.GetText(Int32.MaxValue);
                }
                throw new FlaUI.Core.Exceptions.MethodNotSupportedException($"AutomationElement '{ToString()}' supports neither ValuePattern or TextPattern");
            }
            set
            {
                if (RawElement.Patterns.Value.TryGetPattern(out var valuePattern))
                {
                    valuePattern.SetValue(value);
                }
                else
                {
                    Enter(value);
                }
            }
        }
        public void Enter(string value)
        {
            RawElement.Focus();
            var valuePattern = RawElement.Patterns.Value.PatternOrDefault;
            valuePattern?.SetValue(String.Empty);
            if (String.IsNullOrEmpty(value)) return;

            var lines = value.Replace("\r\n", "\n").Split('\n');
            FlaUI.Core.Input.Keyboard.Type(lines[0]);
            foreach (var line in lines.Skip(1))
            {
                FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
                FlaUI.Core.Input.Keyboard.Type(line);
            }
            FlaUI.Core.Input.Wait.UntilInputIsProcessed();
        }
        public override string ToString()
        {
            return "id:" + Id + " Name:" + Name + " ClassName: " + ClassName;
        }
        public override bool Equals(object obj)
        {
            var e = obj as UIElement;
            if (e == null) return false;
            if (e.ProcessId != ProcessId) return false;
            if (e.Id != Id) return false;
            if (e.Name != Name) return false;
            if (e.ClassName != ClassName) return false;
            if (e.Type != Type) return false;
            return true;
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        public string ImageString()
        {
            var AddedWidth = 10;
            var AddedHeight = 10;
            var ScreenImageWidth = Rectangle.Width + AddedWidth;
            var ScreenImageHeight = Rectangle.Height + AddedHeight;
            var ScreenImagex = Rectangle.X - (AddedWidth / 2);
            var ScreenImagey = Rectangle.Y - (AddedHeight / 2);
            if (ScreenImagex < 0) ScreenImagex = 0; if (ScreenImagey < 0) ScreenImagey = 0;
            using (var image = Interfaces.Image.Util.Screenshot(ScreenImagex, ScreenImagey, ScreenImageWidth, ScreenImageHeight, Interfaces.Image.Util.ActivityPreviewImageWidth, Interfaces.Image.Util.ActivityPreviewImageHeight))
            {
                // Interfaces.Image.Util.SaveImageStamped(image, System.IO.Directory.GetCurrentDirectory(), "UIElement");
                return Interfaces.Image.Util.Bitmap2Base64(image);
            }
        }

    }
}
