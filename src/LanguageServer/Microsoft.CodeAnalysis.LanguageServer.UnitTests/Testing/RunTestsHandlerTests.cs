// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.LanguageServer.Testing;

namespace Microsoft.CodeAnalysis.LanguageServer.UnitTests.Testing;

public sealed class RunTestsHandlerTests
{
    [Theory]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    [InlineData("<RunSettings/>", true, false)]
    [InlineData("<RunSettings/>", false, false)]
    public void ShouldUseMtp(string? runSettings, bool isMtpProject, bool expected)
    {
        Assert.Equal(expected, RunTestsHandler.ShouldUseMtp(runSettings, isMtpProject));
    }
}
