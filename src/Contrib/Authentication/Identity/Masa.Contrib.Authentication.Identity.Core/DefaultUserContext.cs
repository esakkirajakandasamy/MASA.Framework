// Copyright (c) MASA Stack All rights reserved.
// Licensed under the MIT License. See LICENSE.txt in the project root for license information.

namespace Masa.Contrib.Authentication.Identity;

internal class DefaultUserContext : UserContext
{
    private readonly IOptions<IdentityClaimOptions> _options;
    private static readonly MemoryCache<Type, CustomizeModelRelation> ModelRelationCache = new();

    private ClaimsPrincipal? ClaimsPrincipal { get; set; }

    public DefaultUserContext(
        ITypeConvertProvider typeConvertProvider,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IOptions<IdentityClaimOptions> options)
        : base(typeConvertProvider)
    {
        _options = options;
        _options.Value.Initialize();
        ClaimsPrincipal = currentPrincipalAccessor.GetCurrentPrincipal();
    }

    protected override object? GetUser(Type userType)
    {
        var userClaimType = _options.Value.GetClaimType(nameof(_options.Value.UserId))!;
        var userId = ClaimsPrincipal?.FindClaimValue(userClaimType);
        if (userId == null)
            return null;

        var modelRelation = ModelRelationCache.GetOrAdd(userType, (type) =>
        {
            var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(c => c.GetParameters().Length == 0) ??
                throw new InvalidOperationException($"[{type.Name}] has a parameterless constructor");
            return new CustomizeModelRelation(
                InstanceBuilder.CreateInstanceDelegate(constructor),
                InstanceBuilder.GetPropertyAndMethodInfoRelations(type));
        });
        var userModel = modelRelation.Func.Invoke(Array.Empty<object>());
        foreach (var property in userType.GetProperties())
        {
            var claimType = _options.Value.GetClaimType(property.Name);
            if (claimType == null)
                continue;

            var claimValue = ClaimsPrincipal?.FindClaimValue(claimType);
            if (claimValue != null)
            {
                modelRelation.Setters[property]
                    .Invoke(userModel, new[] { TypeConvertProvider.ConvertTo(claimValue, property.PropertyType) });
            }
        }

        return userModel;
    }
}
