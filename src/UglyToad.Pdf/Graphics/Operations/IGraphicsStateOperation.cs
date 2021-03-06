﻿namespace UglyToad.Pdf.Graphics.Operations
{
    using Content;

    internal interface IGraphicsStateOperation
    {
        string Operator { get; }

        void Run(IOperationContext operationContext, IResourceStore resourceStore);
    }
}