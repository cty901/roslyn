﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.LanguageServer.UnitTests.RequestOrdering
{
    [Shared, ExportLspMethod(MethodName, mutatesSolutionState: true), PartNotDiscoverable]
    internal class FailingMutatingRequestHandler : IRequestHandler<TestRequest, TestResponse>
    {
        public const string MethodName = nameof(FailingMutatingRequestHandler);
        private const int Delay = 100;

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public FailingMutatingRequestHandler()
        {
        }

        public TextDocumentIdentifier GetTextDocumentIdentifier(TestRequest request) => null;

        public async Task<TestResponse> HandleRequestAsync(TestRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);

            // Mutate the solution
            var solution = context.Solution;
            solution = solution.WithNewWorkspace(solution.Workspace, solution.WorkspaceVersion + 1);
            context.UpdateSolution(solution);

            await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);

            throw new InvalidOperationException();
        }
    }
}
