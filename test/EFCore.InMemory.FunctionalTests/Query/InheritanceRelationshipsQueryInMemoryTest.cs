﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.EntityFrameworkCore.Query
{
    // TODO: Issue #16963
    internal class InheritanceRelationshipsQueryInMemoryTest : InheritanceRelationshipsQueryTestBase<
        InheritanceRelationshipsQueryInMemoryFixture>
    {
        public InheritanceRelationshipsQueryInMemoryTest(InheritanceRelationshipsQueryInMemoryFixture fixture)
            : base(fixture)
        {
        }
    }
}
