﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenRPA.Interfaces
{
    public interface IElement
    {
        Object RawElement { get; set; }
        string Value { get; set; }
        void Focus();
        void Click();
        Task Highlight(bool Blocking, System.Drawing.Color Color, TimeSpan Duration);
        string ImageString();
    }
}
