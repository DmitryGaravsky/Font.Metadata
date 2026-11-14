using System.Collections.Generic;
using Spdx.Expressions;

namespace Fonts.Metadata {
    /// <summary>Independent source and processing outcomes. Neither implies permission to use a font.</summary>
    public sealed class FontSpdxAnalysis {
        internal FontSpdxAnalysis(bool sourceComplete, bool analysisComplete, SpdxRegistryIdentity identity,
            List<FontSpdxDeclaration> declarations, List<FontSpdxDiagnostic> diagnostics, bool truncated) {
            IsSourceComplete = sourceComplete;
            IsAnalysisComplete = analysisComplete;
            RegistryIdentity = identity;
            Declarations = declarations.AsReadOnly();
            Diagnostics = diagnostics.AsReadOnly();
            DiagnosticsTruncated = truncated;
        }
        /// <summary>True for a Complete or Missing name table, independently of recognition coverage.</summary>
        public bool IsSourceComplete { get; }
        /// <summary>True when the available source was processed without work or result exhaustion.</summary>
        public bool IsAnalysisComplete { get; }
        /// <summary>Whole-record recognition profile, independent of parser syntax.</summary>
        public string RecognitionProfile => "font-name13-v1";
        /// <summary>Fixed native expression syntax profile.</summary>
        public string SyntaxProfile => "SPDX-2.3.1";
        /// <summary>Identity of the explicit registry, including when no declarations were found.</summary>
        public SpdxRegistryIdentity RegistryIdentity { get; }
        /// <summary>Admitted attempts in source order, retaining duplicate records and failures.</summary>
        public IReadOnlyList<FontSpdxDeclaration> Declarations { get; }
        /// <summary>Adapter work-limit messages; native parse and validation messages remain in their results.</summary>
        public IReadOnlyList<FontSpdxDiagnostic> Diagnostics { get; }
        /// <summary>True if adapter messages were omitted; independent of validation message truncation.</summary>
        public bool DiagnosticsTruncated { get; }
    }
}
