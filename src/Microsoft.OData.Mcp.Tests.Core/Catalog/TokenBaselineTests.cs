// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpToken;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Guards the per-call tool results against costing more tokens than they did before the optimization.
    /// </summary>
    /// <remarks>
    /// This is the build-time guard only. The reports (<c>TOKENS.md</c>, <c>FORMATS.md</c>) are produced by
    /// <c>Microsoft.OData.Mcp.Benchmarks</c> with <c>dotnet run -c Release -- --tokens</c>, which links the same
    /// <c>Before/</c> and <c>Current/</c> folders this class reads.
    /// </remarks>
    [TestClass]
    public class TokenBaselineTests
    {

        #region Fields

        internal const string EncodingName = "cl100k_base";

        internal static readonly GptEncoding Encoding = GptEncoding.GetEncoding(EncodingName);

        private const string projectPath = "..//..//..//";

        #endregion

        #region Public Methods

        /// <summary>
        /// Every paired tool result costs no more tokens now than it did before the optimization.
        /// </summary>
        /// <remarks>
        /// Input schemas are excluded because typed schemas are larger than the old <c>{ key, body }</c> by design,
        /// <c>tools.list</c> is excluded because the Before snapshot never captured descriptions or instructions, and
        /// the <c>describe_type</c> text files are excluded because the old text form was the bare type name.
        /// </remarks>
        [TestMethod]
        public void PairedToolResults_DoNotCostMoreThanBefore()
        {
            var counts = CountPairs(projectPath);
            var results = counts.Where(pair => pair.Value.Before is not null && pair.Value.Current is not null && IsToolResult(pair.Key)).ToList();

            results.Should().NotBeEmpty();
            foreach (var pair in results)
            {
                pair.Value.Current.Should().BeLessThanOrEqualTo(pair.Value.Before!.Value, pair.Key);
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Counts tokens for every file name that exists in <c>Before/</c>, <c>Current/</c>, or both.
        /// </summary>
        /// <param name="projectPath">The path to the test project directory.</param>
        /// <returns>
        /// File name to <c>(Before, Current)</c> counts, ordinal order; a side that lacks the file is <c>null</c>.
        /// </returns>
        internal static SortedDictionary<string, (int? Before, int? Current)> CountPairs(string projectPath)
        {
            var before = Path.Combine(projectPath, TypeShapeBaselineTests.BeforeFolder);
            var current = Path.Combine(projectPath, TypeShapeBaselineTests.CurrentFolder);
            var names = Directory.EnumerateFiles(before).Concat(Directory.EnumerateFiles(current))
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(name => !name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.Ordinal);

            var result = new SortedDictionary<string, (int? Before, int? Current)>(StringComparer.Ordinal);
            foreach (var name in names)
            {
                var beforePath = Path.Combine(before, name);
                var currentPath = Path.Combine(current, name);
                result[name] = (
                    File.Exists(beforePath) ? CountTokens(File.ReadAllText(beforePath)) : null,
                    File.Exists(currentPath) ? CountTokens(File.ReadAllText(currentPath)) : null);
            }

            return result;
        }

        /// <summary>
        /// Counts the tokens in a string with <see cref="Encoding"/>.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>
        /// The token count; zero for an empty string.
        /// </returns>
        internal static int CountTokens(string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            return text.Length == 0 ? 0 : Encoding.CountTokens(text);
        }

        /// <summary>
        /// Decides whether a baseline is a per-call tool result that must not grow.
        /// </summary>
        /// <param name="fileName">The baseline file name.</param>
        /// <returns>
        /// <c>false</c> for input schemas, the <c>tools.list</c> aggregate, and <c>describe_type</c> text files;
        /// <c>true</c> otherwise.
        /// </returns>
        internal static bool IsToolResult(string fileName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

            if (fileName.EndsWith(".inputschema.json", StringComparison.Ordinal) || fileName == TypeShapeBaselineTests.NorthwindToolsList)
            {
                return false;
            }

            return !(fileName.Contains(".describe_type.", StringComparison.Ordinal) && fileName.EndsWith(".txt", StringComparison.Ordinal));
        }

        #endregion

    }

}
