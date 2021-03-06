﻿using Microsoft.VisualBasic.Activities;
using System;
using System.Activities;
using System.Activities.Expressions;
using System.Activities.Presentation.Model;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenRPA.Java
{
    public partial class GetElementDesigner : INotifyPropertyChanged
    {
        public GetElementDesigner()
        {
            InitializeComponent();
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private void Open_Selector(object sender, RoutedEventArgs e)
        {
            ModelItem loadFrom = ModelItem.Parent;
            string loadFromSelectorString = "";
            JavaSelector anchor = null;
            while (loadFrom.Parent != null)
            {
                var p = loadFrom.Properties.Where(x => x.Name == "Selector").FirstOrDefault();
                if (p != null)
                {
                    loadFromSelectorString = loadFrom.GetValue<string>("Selector");
                    anchor = new JavaSelector(loadFromSelectorString);
                    break;
                }
                loadFrom = loadFrom.Parent;
            }
            string SelectorString = ModelItem.GetValue<string>("Selector");
            int maxresults = ModelItem.GetValue<int>("MaxResults");
            Interfaces.Selector.SelectorWindow selectors;
            if (!string.IsNullOrEmpty(SelectorString))
            {
                var selector = new JavaSelector(SelectorString);
                selectors = new Interfaces.Selector.SelectorWindow("Java", selector, anchor, maxresults);
            }
            else
            {
                var selector = new JavaSelector("[{Selector: 'Java'}]");
                selectors = new Interfaces.Selector.SelectorWindow("Java", selector, anchor, maxresults);
            }
            if (selectors.ShowDialog() == true)
            {
                ModelItem.Properties["Selector"].SetValue(new InArgument<string>() { Expression = new Literal<string>(selectors.vm.json) });
                var l = selectors.vm.Selector.Last();
                if (l.Element != null)
                {
                    ModelItem.Properties["Image"].SetValue(l.Element.ImageString());
                    NotifyPropertyChanged("Image");
                }
                if (anchor != null)
                {
                    ModelItem.Properties["From"].SetValue(new InArgument<JavaElement>()
                    {
                        Expression = new Microsoft.VisualBasic.Activities.VisualBasicValue<JavaElement>("item")
                    });

                }
            }
        }
        private void Highlight_Click(object sender, RoutedEventArgs e)
        {
            string SelectorString = ModelItem.GetValue<string>("Selector");
            int maxresults = ModelItem.GetValue<int>("MaxResults");
            var selector = new JavaSelector(SelectorString);
            var elements = JavaSelector.GetElementsWithuiSelector(selector, null, maxresults);
            foreach (var ele in elements) ele.Highlight(false, System.Drawing.Color.Red, TimeSpan.FromSeconds(1));
        }
        public string ImageString
        {
            get
            {
                string result = string.Empty;
                result = ModelItem.GetValue<string>("Image");
                return result;
            }
        }
        public BitmapImage Image
        {
            get
            {
                var base64 = ImageString;
                if (string.IsNullOrEmpty(base64)) return null;
                //if (System.Text.RegularExpressions.Regex.Match(base64, "[a-f0-9]{24}").Success)
                //{
                //    return image.Screenutil.BitmapToImageSource(image.util.loadWorkflowImage(base64), image.Screenutil.ActivityPreviewImageWidth, image.Screenutil.ActivityPreviewImageHeight);
                //}

                // return OpenRPA.Interfaces.Image.Util.BitmapToImageSource
                using (var b = Interfaces.Image.Util.Base642Bitmap(base64))
                {
                    return Interfaces.Image.Util.BitmapToImageSource(b, Interfaces.Image.Util.ActivityPreviewImageWidth, Interfaces.Image.Util.ActivityPreviewImageHeight);
                }
            }
        }
    }
}