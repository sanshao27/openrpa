﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenRPA.Interfaces
{
    public static class Extensions
    {
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source) action(item);
        }
        public static string JParse(this string o)
        {
            if (o == null) return null;
            string result = Newtonsoft.Json.JsonConvert.ToString(o);
            result = result.Substring(1, result.Length - 2);
            return result;
        }
        public static void AddCacheArgument(System.Activities.NativeActivityMetadata metadata, string name, System.Activities.Argument argument)
        {
            try
            {
                if (argument == null) return;
                Type ttype = argument.GetType().GetGenericArguments()[0];
                System.Activities.ArgumentDirection direction = System.Activities.ArgumentDirection.In;
                if (argument is System.Activities.InArgument) direction = System.Activities.ArgumentDirection.In;
                if (argument is System.Activities.InOutArgument) direction = System.Activities.ArgumentDirection.InOut;
                if (argument is System.Activities.OutArgument) direction = System.Activities.ArgumentDirection.Out;
                var ra = new System.Activities.RuntimeArgument(name, ttype, direction);
                metadata.Bind(argument, ra);
                metadata.AddArgument(ra);
            }
            catch (Exception)
            {
                throw;
            }
        }
        public static string replaceEnvironmentVariable(this string filename)
        {
            var USERPROFILE = Environment.GetEnvironmentVariable("USERPROFILE");
            var windir = Environment.GetEnvironmentVariable("windir");
            var SystemRoot = Environment.GetEnvironmentVariable("SystemRoot");
            var PUBLIC = Environment.GetEnvironmentVariable("PUBLIC");

            if (!string.IsNullOrEmpty(USERPROFILE)) filename = filename.Replace(USERPROFILE, "%USERPROFILE%");
            if (!string.IsNullOrEmpty(windir)) filename = filename.Replace(windir, "%windir%");
            if (!string.IsNullOrEmpty(SystemRoot)) filename = filename.Replace(SystemRoot, "%SystemRoot%");
            if (!string.IsNullOrEmpty(PUBLIC)) filename = filename.Replace(PUBLIC, "%PUBLIC%");

            var ProgramData = Environment.GetEnvironmentVariable("ProgramData");
            if (!string.IsNullOrEmpty(ProgramData)) filename = filename.Replace(ProgramData, "%ProgramData%");
            var ProgramFilesx86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if (!string.IsNullOrEmpty(ProgramFilesx86)) filename = filename.Replace(ProgramFilesx86, "%ProgramFiles(x86)%");
            var ProgramFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            if (!string.IsNullOrEmpty(ProgramFiles)) filename = filename.Replace(ProgramFiles, "%ProgramFiles%");
            var LOCALAPPDATA = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrEmpty(LOCALAPPDATA)) filename = filename.Replace(LOCALAPPDATA, "%LOCALAPPDATA%");
            var APPDATA = Environment.GetEnvironmentVariable("APPDATA");
            if (!string.IsNullOrEmpty(APPDATA)) filename = filename.Replace(APPDATA, "%APPDATA%");


            //var = Environment.GetEnvironmentVariable("");
            //if (!string.IsNullOrEmpty()) filename = filename.Replace(, "%%");

            return filename;
        }

    }
}
