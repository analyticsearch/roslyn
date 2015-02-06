﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.InteractiveWindow
{
    internal sealed class InteractiveSmartIndenter : ISmartIndent
    {
        private readonly IContentType contentType;
        private readonly ITextView view;
        private readonly ISmartIndent indenter;

        internal static InteractiveSmartIndenter Create(
            IEnumerable<Lazy<ISmartIndentProvider, ContentTypeMetadata>> smartIndenterProviders,
            IContentType contentType,
            ITextView view)
        {
            var provider = GetProvider(smartIndenterProviders, contentType);
            return (provider == null) ? null : new InteractiveSmartIndenter(contentType, view, provider.Item2.Value);
        }

        private InteractiveSmartIndenter(IContentType contentType, ITextView view, ISmartIndentProvider provider)
        {
            this.contentType = contentType;
            this.view = view;
            this.indenter = provider.CreateSmartIndent(view);
        }

        public int? GetDesiredIndentation(ITextSnapshotLine line)
        {
            // get point at the subject buffer
            var mappingPoint = this.view.BufferGraph.CreateMappingPoint(line.Start, PointTrackingMode.Negative);
            var point = mappingPoint.GetInsertionPoint(b => b.ContentType.IsOfType(this.contentType.TypeName));
            if (!point.HasValue)
            {
                return null;
            }

            // Currently, interactive smart indenter returns indentation based
            // solely on subject buffer's information and doesn't consider spaces
            // in interactive window itself. Note: This means the ITextBuffer passed
            // to ISmartIndent.GetDesiredIndentation is not this.view.TextBuffer.
            return this.indenter.GetDesiredIndentation(point.Value.GetContainingLine());
        }

        public void Dispose()
        {
            this.indenter.Dispose();
        }

        // Returns the provider that supports the most derived content type.
        // If there are two providers that support the same content type, or
        // two providers that support different content types that do not have
        // inheritance relationship, we simply return the first we encounter.
        private static Tuple<IContentType, Lazy<ISmartIndentProvider, ContentTypeMetadata>> GetProvider(
            IEnumerable<Lazy<ISmartIndentProvider, ContentTypeMetadata>> smartIndenterProviders,
            IContentType contentType)
        {
            // If there are two providers that both support the
            // same content type, we simply choose the first.
            var provider = smartIndenterProviders.FirstOrDefault(p => p.Metadata.ContentTypes.Contains(contentType.TypeName));
            if (provider != null)
            {
                return Tuple.Create(contentType, provider);
            }

            Tuple<IContentType, Lazy<ISmartIndentProvider, ContentTypeMetadata>> bestPair = null;
            foreach (var baseType in contentType.BaseTypes)
            {
                var pair = GetProvider(smartIndenterProviders, baseType);
                if ((pair != null) && ((bestPair == null) || IsBaseContentType(pair.Item1, bestPair.Item1)))
                {
                    bestPair = pair;
                }
            }

            return bestPair;
        }

        // Returns true if the second content type is a base type of the first.
        private static bool IsBaseContentType(IContentType type, IContentType potentialBase)
        {
            return type.BaseTypes.Any(b => b.IsOfType(potentialBase.TypeName) || IsBaseContentType(b, potentialBase));
        }
    }
}
