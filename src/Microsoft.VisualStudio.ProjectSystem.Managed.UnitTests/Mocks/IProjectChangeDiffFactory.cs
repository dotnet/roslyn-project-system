﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.VisualStudio.ProjectSystem;
using Moq;

namespace Microsoft.VisualStudio.Mocks
{
    internal static class IProjectChangeDiffFactory
    {
        public static IProjectChangeDiff Create()
        {
            return Mock.Of<IProjectChangeDiff>();
        }
    }
}
