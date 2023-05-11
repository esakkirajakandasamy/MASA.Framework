﻿// Copyright (c) MASA Stack All rights reserved.
// Licensed under the MIT License. See LICENSE.txt in the project root for license information.

// ReSharper disable once CheckNamespace

namespace Masa.Contrib.Configuration;

internal class MasaUnnamedOptionsCache<TOptions> : MemoryCache<IServiceProvider, TOptions>
    where TOptions : class
{

}
