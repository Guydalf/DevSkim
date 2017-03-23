﻿// Copyright(C) Microsoft.All rights reserved.
// Licensed under the MIT License. See LICENSE.txt in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Linq;

namespace Microsoft.DevSkim
{
    /// <summary>
    /// Helper class for language based commenting
    /// </summary>
    public class Language
    {
        private Language()
        {
            Assembly assembly = typeof(Microsoft.DevSkim.Language).GetTypeInfo().Assembly;

            // Load comments
            Stream resource = assembly.GetManifestResourceStream("Microsoft.DevSkim.Resources.comments.json");
            using (StreamReader file = new StreamReader(resource))
            {
                Comments = JsonConvert.DeserializeObject<List<Comment>>(file.ReadToEnd());
            }

            // Load languages
            resource = assembly.GetManifestResourceStream("Microsoft.DevSkim.Resources.languages.json");
            using (StreamReader file = new StreamReader(resource))
            {
                ContentTypes = JsonConvert.DeserializeObject<List<ContentType>>(file.ReadToEnd());
            }
        }

        /// <summary>
        /// Returns language for given file name
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <returns>Language</returns>
        public static string FromFileName(string fileName)
        {            
            string file = Path.GetFileName(fileName).ToLower(CultureInfo.CurrentCulture);
            string ext = Path.GetExtension(file);

            foreach (ContentType item in Instance.ContentTypes)
            {
                if (Array.Exists(item.Extensions, x => x.EndsWith(file)) ||
                    Array.Exists(item.Extensions, x => x.EndsWith(ext)))
                    return item.Name;
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets comment prefix for given language
        /// </summary>        
        /// <param name="language">Language</param>
        /// <returns>Commented string</returns>
        public static string GetCommentPrefix(string language)
        {
            string result = string.Empty;

            foreach (Comment comment in Instance.Comments)
            {
                if (comment.Languages.Contains(language))
                    return comment.Preffix;
            }

            return result;
        }

        /// <summary>
        /// Gets comment suffix for given language
        /// </summary>        
        /// <param name="language">Language</param>
        /// <returns>Commented string</returns>
        public static string GetCommentSuffix(string language)
        {
            string result = string.Empty;

            foreach (Comment comment in Instance.Comments)
            {
                if (comment.Languages.Contains(language))
                    return comment.Suffix;
            }

            return result;
        }

        private static Language _instance;
        private static Language Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new Language();

                return _instance;
            }        
        }

        private List<Comment> Comments;
        private List<ContentType> ContentTypes;      
    }
}

