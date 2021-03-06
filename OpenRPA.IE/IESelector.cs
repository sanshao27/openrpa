﻿using FlaUI.Core;
using FlaUI.Core.AutomationElements.Infrastructure;
using OpenRPA.Interfaces;
using OpenRPA.Interfaces.Selector;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenRPA.IE
{
    class IESelector : Selector
    {
        public static readonly string[] frameTags = { "FRAME", "IFRAME" };
        IEElement element { get; set; }
        public IESelector(string json) : base(json) { }
        public IESelector(Browser browser, mshtml.IHTMLElement baseelement, IESelector anchor, bool doEnum, int X, int Y)
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            Log.Selector(string.Format("IEselector::AutomationElement::begin {0:mm\\:ss\\.fff}", sw.Elapsed));
            Log.Selector(string.Format("IEselector::GetControlViewWalker::end {0:mm\\:ss\\.fff}", sw.Elapsed));

            Clear();
            enumElements(browser, baseelement, anchor, doEnum, X, Y);

            Log.Selector(string.Format("IEselector::EnumNeededProperties::end {0:mm\\:ss\\.fff}", sw.Elapsed));
            OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new System.Collections.Specialized.NotifyCollectionChangedEventArgs(System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
        }
        private void enumElements(Browser browser, mshtml.IHTMLElement baseelement, IESelector anchor, bool doEnum, int X, int Y) {
            mshtml.IHTMLElement element = baseelement;
            mshtml.HTMLDocument document = browser.Document;
            var pathToRoot = new List<mshtml.IHTMLElement>();
            while (element != null)
            {
                if (pathToRoot.Contains(element)) { break; }
                try
                {
                    pathToRoot.Add(element);
                }
                catch (Exception)
                {
                }
                try
                {
                    element = element.parentElement;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "");
                    return;
                }
            }
            // Log.Selector(string.Format("IEselector::create pathToRoot::end {0:mm\\:ss\\.fff}", sw.Elapsed));
            pathToRoot.Reverse();
            if (anchor != null)
            {
                var anchorlist = anchor.Where(x => x.Enabled && x.Selector == null).ToList();
                for (var i = 0; i < anchorlist.Count(); i++)
                {
                    //if (((IESelectorItem)anchorlist[i]).Match(pathToRoot[0]))
                    if (IESelectorItem.Match(anchorlist[i], pathToRoot[0]))
                    {
                        pathToRoot.Remove(pathToRoot[0]);
                    }
                    else
                    {
                        Log.Selector("Element does not match the anchor path");
                        return;
                    }
                }
            }

            if (pathToRoot.Count == 0)
            {
                Log.Error("Element is same as annchor");
                return;
            }
            element = pathToRoot.Last();
            // 
            // Log.Selector(string.Format("IEselector::remove anchor if needed::end {0:mm\\:ss\\.fff}", sw.Elapsed));
            IESelectorItem item;
            if (anchor == null && Items.Count == 0)
            {
                item = new IESelectorItem(browser.Document);
                item.Enabled = true;
                //item.canDisable = false;
                Items.Add(item);
            }
            for (var i = 0; i < pathToRoot.Count(); i++)
            {
                var o = pathToRoot[i];
                item = new IESelectorItem(browser, o);
                if (i == 0 || i == (pathToRoot.Count() - 1)) item.canDisable = false;
                foreach (var p in item.Properties)
                {
                    int idx = p.Value.IndexOf(".");
                    if (p.Name == "className" && idx > -1)
                    {
                        int idx2 = p.Value.IndexOf(".", idx + 1);
                        if (idx2 > idx) p.Value = p.Value.Substring(0, idx2 + 1) + "*";
                    }
                }
                if (doEnum) item.EnumNeededProperties(o, o.parentElement);

                Items.Add(item);
            }
            if(frameTags.Contains(baseelement.tagName.ToUpper()))
            {
                //var ele2 = baseelement as mshtml.IHTMLElement2;
                //var col2 = ele2.getClientRects();
                //var rect2 = col2.item(0);
                //X -= rect2.left;
                //Y -= rect2.top;
                var frame = baseelement as mshtml.HTMLFrameElement;

                var fffff = frame.contentWindow;
                mshtml.IHTMLWindow2 window = frame.contentWindow;
                mshtml.IHTMLElement el2 = null;

                
                foreach (string frameTag in frameTags)
                {
                    mshtml.IHTMLElementCollection framesCollection = document.getElementsByTagName(frameTag);
                    foreach (mshtml.IHTMLElement _frame in framesCollection)
                    {
                        // var _f = _frame as mshtml.HTMLFrameElement;
                        el2 = browser.ElementFromPoint(_frame, X, Y);
                        //var _wb = _f as SHDocVw.IWebBrowser2;
                        //document = _wb.Document as mshtml.HTMLDocument;
                        //el2 = document.elementFromPoint(X, Y);
                        if (el2 != null)
                        {
                            var tag = el2.tagName;
                            // var html = el2.innerHTML;
                            Log.Selector("tag: " + tag);

                            //browser.elementx += _frame.offsetLeft;
                            //browser.elementy += _frame.offsetTop;
                            //browser.frameoffsetx += _frame.offsetLeft;
                            //browser.frameoffsety += _frame.offsetTop;


                            enumElements(browser, el2 , anchor, doEnum, X, Y);
                            return;
                        }
                    }
                }
            }
        }

        public override IElement[] GetElements(IElement fromElement = null, int maxresults = 1)
        {
            return IESelector.GetElementsWithuiSelector(this, fromElement, maxresults );
        }
        public static IEElement[] GetElementsWithuiSelector(IESelector selector, IElement fromElement = null, int maxresults = 1)
        {
            IEElement iefromElement = fromElement as IEElement;
            Browser browser;
            if (iefromElement != null)
            {
                browser = iefromElement.Browser;
            } else
            {
                browser = Browser.GetBrowser();
            }
            if (browser == null)
            {
                Log.Warning("Failed locating an Internet Explore instance");
                return new IEElement[] { };
            }

            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            IEElement _fromElement = fromElement as IEElement;
            var selectors = selector.Where(x => x.Enabled == true && x.Selector == null).ToList();

            var current = new List<IEElement>();

            IEElement[] result = null;

            int startIndex = 1;

            if(iefromElement != null)
            {
                startIndex = 0;
                current.Add(iefromElement);
            }
             else
            {
                mshtml.IHTMLElement startfrom = null;
                startfrom = browser.Document.documentElement;
                current.Add(new IEElement(browser, startfrom));
            }
            for (var i = startIndex; i < selectors.Count; i++)
            {
                var s = new IESelectorItem(selectors[i]);
                var elements = new List<IEElement>();
                elements.AddRange(current);
                current.Clear();
                int failcounter = 0;
                do
                {
                    foreach (var _element in elements)
                    {
                        mshtml.IHTMLElement[] matches;
                        if (frameTags.Contains(_element.tagName.ToUpper()))
                        {
                            if(s.tagName.ToUpper()=="HTML") { i++; s = new IESelectorItem(selectors[i]); }
                            var _f = _element.RawElement as mshtml.HTMLFrameElement;
                            mshtml.DispHTMLDocument doc = (mshtml.DispHTMLDocument)((SHDocVw.IWebBrowser2)_f).Document;
                            var _doc = doc.documentElement as mshtml.IHTMLElement;
                            matches = ((IESelectorItem)s).matches(_doc);

                            browser.elementx += _f.offsetLeft;
                            browser.elementy += _f.offsetTop;
                            browser.frameoffsetx += _f.offsetLeft;
                            browser.frameoffsety += _f.offsetTop;

                        }
                        else
                        {
                            matches = ((IESelectorItem)s).matches(_element.RawElement);
                        }
                        var uimatches = new List<IEElement>();
                        foreach (var m in matches)
                        {
                            var ui = new IEElement(browser, m);
                            uimatches.Add(ui);
                        }
                        current.AddRange(uimatches.ToArray());
                        Log.Selector("add " + uimatches.Count + " matches to current");
                    }
                    if (current.Count == 0)
                    {
                        ++failcounter;
                        string message = string.Format("Failer # " + failcounter + " finding any hits for selector # " + i + " {0:mm\\:ss\\.fff}", sw.Elapsed) + "\n";
                        message += "lookin for \n" + s.ToString() + "\n";
                        foreach (var _element in elements)
                        {
                            mshtml.IHTMLElementCollection children = _element.RawElement.children;
                            foreach (mshtml.IHTMLElement elementNode in children) {
                                var ui = new IEElement(browser, elementNode);
                                message += ui.ToString() + "\n";
                            }
                            var matches = ((IESelectorItem)s).matches(_element.RawElement);
                        }
                        Log.Selector(message);
                    }
                    else
                    {
                        Log.Selector(string.Format("Found " + current.Count + " hits for selector # " + i + " {0:mm\\:ss\\.fff}", sw.Elapsed));
                    }
                } while (failcounter < 2 && current.Count == 0);

                if (i == (selectors.Count - 1)) result = current.ToArray();
                if (current.Count == 0)
                {
                    var message = "needed to find " + Environment.NewLine + selectors[i].ToString() + Environment.NewLine + "but found only: " + Environment.NewLine;
                    foreach (var element in elements)
                    {
                        mshtml.IHTMLElementCollection children = element.RawElement.children;
                        foreach (mshtml.IHTMLElement c in children)
                        {
                            try
                            {
                                // message += automationutil.getSelector(c, (i == selectors.Count - 1)) + Environment.NewLine;
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                    Log.Selector(message);
                    return new IEElement[] { };
                }
            }
            if (result == null) return new IEElement[] { };
            Log.Selector(string.Format("GetElementsWithuiSelector::end {0:mm\\:ss\\.fff}", sw.Elapsed));
            return result;
        }


    }
}
