﻿// Copyright (C) Microsoft. All rights reserved.
// Licensed under the MIT License. See LICENSE.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;


[assembly: CLSCompliant(true)]
namespace Microsoft.Security.DevSkim
{
    /// <summary>
    /// Heart of DevSkim. Parses code applies rules
    /// </summary>
    public class RuleProcessor
    {
        public RuleProcessor()
        {            
            _rulesCache = new Dictionary<string, IEnumerable<Rule>>();
            AllowSuppressions = false;

            SeverityLevel = Severity.Critical | Severity.Important | Severity.Moderate |
                            Severity.Low | Severity.Informational | Severity.DefenseInDepth;
        }

        /// <summary>
        /// Creates instance of RuleProcessor
        /// </summary>
        public RuleProcessor(Ruleset rules) : this()
        {
            this.Rules = rules;
        }       

        #region Public Methods

        /// <summary>
        /// Applies given fix on the provided source code line
        /// </summary>
        /// <param name="text">Source code line</param>
        /// <param name="fixRecord">Fix record to be applied</param>
        /// <returns>Fixed source code line</returns>
        public static string Fix(string text, CodeFix fixRecord)
        {
            string result = string.Empty;

            if (fixRecord.FixType == FixType.RegexSubstitute)
            {
                Regex regex = new Regex(fixRecord.Search);
                result = regex.Replace(text, fixRecord.Replace);
            }

            return result;
        }

        /// <summary>
        /// Analyzes given line of code
        /// </summary>
        /// <param name="text">Source code</param>
        /// <param name="language">Language</param>
        /// <returns>Array of matches</returns>
        public Match[] Analyze(string text, string language)
        {
            return Analyze(text, new string[] { language });
        }

        /// <summary>
        /// Analyzes given line of code
        /// </summary>
        /// <param name="text">Source code</param>
        /// <param name="languages">List of languages</param>
        /// <returns>Array of matches</returns>
        public Match[] Analyze(string text, string[] languages)
        {
            // Get rules for the given content type
            IEnumerable<Rule> rules = GetRulesForLanguages(languages);
            List<Match> matchList = new List<Match>();

            // Go through each rule
            foreach (Rule r in rules)
            {
                List<Match> resultList = new List<Match>();

                // Skip rules that don't apply based on settings
                if (r.Disabled || !SeverityLevel.HasFlag(r.Severity))
                    continue;

                // Go through each matching pattern of the rule
                foreach (SearchPattern p in r.Patterns)
                {
                    RegexOptions reopt = RegexOptions.None;
                    if (p.Modifiers != null && p.Modifiers.Length > 0)
                    {
                        reopt |= (p.Modifiers.Contains("IGNORECASE")) ? RegexOptions.IgnoreCase : RegexOptions.None;
                        reopt |= (p.Modifiers.Contains("MULTILINE")) ? RegexOptions.Multiline : RegexOptions.None;
                    }

                    Regex patRegx = new Regex(p.Pattern, reopt);
                    MatchCollection matches = patRegx.Matches(text);
                    if (matches.Count > 0)
                    {
                        foreach (System.Text.RegularExpressions.Match m in matches)
                        {
                            resultList.Add(new Match() { Index = m.Index, Length = m.Length, Rule = r });
                        }
                        break; // from pattern loop                 
                    }                    
                }

                // We got matching rule. Let's see if we have a supression on the line
                if (resultList.Count > 0)
                {
                    Suppressor supp = new Suppressor(text, languages[0]);

                    foreach (Match result in resultList)
                    {
                        // If rule is NOT being suppressed then useit
                        if (!(supp.IsRuleSuppressed(result.Rule.Id) && AllowSuppressions))
                        {
                            matchList.Add(result);
                        }
                    }
                }
            }
            
            // Deal with overrides 
            List<Match> removes = new List<Match>();
            foreach (Match m in matchList)
            {
                if (m.Rule.Overrides != null && m.Rule.Overrides.Length > 0)
                {
                    foreach(string ovrd in m.Rule.Overrides)
                    {                        
                        foreach(Match om in matchList.FindAll(x => x.Rule.Id == ovrd))
                        {
                            if (m.Index == om.Index)
                                removes.Add(om);
                        }
                    }
                }
            }

            // Remove overrided rules
            matchList.RemoveAll(x => removes.Contains(x));

            return matchList.ToArray();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Filters the rules for those matching the content type.
        /// Resolves all the overrides
        /// </summary>
        /// <param name="languages">Languages to filter rules for</param>
        /// <returns>List of rules</returns>
        private IEnumerable<Rule> GetRulesForLanguages(string[] languages)
        {
            string langid = string.Join(":", languages);

            // Do we have the ruleset alrady in cache? If so return it
            if (_rulesCache.ContainsKey(langid))
                return _rulesCache[langid];

            IEnumerable<Rule> filteredRules = _ruleset.ByLanguages(languages); 

            // Add the list to the cache so we save time on the next call
            _rulesCache.Add(langid, filteredRules);

            return filteredRules;
        }

        #endregion

        #region Properties

        public Ruleset Rules
        {
            get { return _ruleset; }
            set
            {
                _ruleset = value;
                _rulesCache = new Dictionary<string, IEnumerable<Rule>>();
            }
        }

        public bool AllowSuppressions { get; set; }

        public Severity SeverityLevel { get; set; }
        #endregion

        #region Fields 

        private Ruleset _ruleset;

        /// <summary>
        /// Cache for rules filtered by content type
        /// </summary>
        private Dictionary<string, IEnumerable<Rule>> _rulesCache;
        #endregion
    }
}