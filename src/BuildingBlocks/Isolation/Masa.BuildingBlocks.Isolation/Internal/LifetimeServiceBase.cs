﻿// Copyright (c) MASA Stack All rights reserved.
// Licensed under the MIT License. See LICENSE.txt in the project root for license information.

[assembly: InternalsVisibleTo("Masa.BuildingBlocks.Caching")]
[assembly: InternalsVisibleTo("Masa.BuildingBlocks.RulesEngine")]
[assembly: InternalsVisibleTo("Masa.BuildingBlocks.SearchEngine.AutoComplete")]
[assembly: InternalsVisibleTo("Masa.BuildingBlocks.Storage.ObjectStorage")]
[assembly: InternalsVisibleTo("Masa.Contrib.Configuration.ConfigurationApi.Dcc")]

// ReSharper disable once CheckNamespace

namespace Masa.BuildingBlocks.Isolation;

internal abstract class LifetimeServiceBase
{
    public abstract object GetService();
}

