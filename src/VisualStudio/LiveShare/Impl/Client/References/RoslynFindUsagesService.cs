﻿//
//  Copyright (c) Microsoft Corporation. All rights reserved.
//

using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editor.FindUsages;
using Microsoft.CodeAnalysis.FindUsages;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;
using LiveShareProtocol = Microsoft.VisualStudio.LiveShare.LanguageServices.Protocol;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServices.LiveShare.Client
{
    internal class RoslynFindUsagesService : IFindUsagesService
    {
        private readonly RoslynLSPClientServiceFactory roslynLSPClientServiceFactory;
        private readonly RemoteLanguageServiceWorkspace remoteLanguageServiceWorkspace;

        public RoslynFindUsagesService(RoslynLSPClientServiceFactory roslynLSPClientServiceFactory, RemoteLanguageServiceWorkspace remoteLanguageServiceWorkspace)
        {
            this.roslynLSPClientServiceFactory = roslynLSPClientServiceFactory ?? throw new ArgumentNullException(nameof(roslynLSPClientServiceFactory));
            this.remoteLanguageServiceWorkspace = remoteLanguageServiceWorkspace ?? throw new ArgumentNullException(nameof(remoteLanguageServiceWorkspace));
        }

        public async Task FindImplementationsAsync(Document document, int position, IFindUsagesContext context)
        {
            var text = await document.GetTextAsync().ConfigureAwait(false);

            var lspClient = this.roslynLSPClientServiceFactory.ActiveLanguageServerClient;
            if (lspClient == null)
            {
                return;
            }

            var documentPositionParams = document.GetTextDocumentPositionParams(text, position);

            object response = await lspClient.RequestAsync(LiveShareProtocol.Methods.TextDocumentImplementations, documentPositionParams, context.CancellationToken).ConfigureAwait(false);
            var locations = ((JToken)response)?.ToObject<LSP.Location[]>();
            if (locations == null)
            {
                return;
            }

            foreach (var location in locations)
            {
                var documentSpan = await location.ToDocumentSpanAsync(this.remoteLanguageServiceWorkspace, context.CancellationToken).ConfigureAwait(false);
                if (documentSpan == null)
                {
                    continue;
                }

                // Get the text for the line containing the definition to show in the UI.
                var docText = await documentSpan.Value.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
                var lineText = docText.GetSubText(docText.Lines[location.Range.Start.Line].Span).ToString();

                await context.OnDefinitionFoundAsync(DefinitionItem.Create(ImmutableArray<string>.Empty,
                    ImmutableArray.Create(new TaggedText(TextTags.Text, lineText)), documentSpan.Value)).ConfigureAwait(false);
            }
        }

        public async Task FindReferencesAsync(Document document, int position, IFindUsagesContext context)
        {
            var text = await document.GetTextAsync().ConfigureAwait(false);

            var lspClient = this.roslynLSPClientServiceFactory.ActiveLanguageServerClient;
            if (lspClient == null)
            {
                return;
            }

            var referenceParams = new ReferenceParams
            {
                Context = new ReferenceContext { IncludeDeclaration = false },
                TextDocument = document.ToTextDocumentIdentifier(),
                Position = position.ToPosition(text)
            };

            LSP.Location[] locations = await lspClient.RequestAsync(Methods.TextDocumentReferences, referenceParams, context.CancellationToken).ConfigureAwait(false);
            if (locations == null)
            {
                return;
            }

            // TODO: Need to get real definition data from the server.
            var dummyDef = DefinitionItem.CreateNonNavigableItem(ImmutableArray<string>.Empty, ImmutableArray<TaggedText>.Empty);
            await context.OnDefinitionFoundAsync(dummyDef).ConfigureAwait(false);

            foreach (var location in locations)
            {
                var documentSpan = await location.ToDocumentSpanAsync(this.remoteLanguageServiceWorkspace, context.CancellationToken).ConfigureAwait(false);
                if (documentSpan == null)
                {
                    continue;
                }

                await context.OnReferenceFoundAsync(new SourceReferenceItem(dummyDef, documentSpan.Value, isWrittenTo: false)).ConfigureAwait(false);
            }
        }
    }
}
