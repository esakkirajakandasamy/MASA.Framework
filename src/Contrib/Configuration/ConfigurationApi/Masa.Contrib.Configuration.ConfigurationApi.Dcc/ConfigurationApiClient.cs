// Copyright (c) MASA Stack All rights reserved.
// Licensed under the MIT License. See LICENSE.txt in the project root for license information.

using Google.Protobuf.WellKnownTypes;
using Masa.BuildingBlocks.Service.Caller;
using System.Diagnostics.Metrics;
using System;
using System.Collections.Generic;
using YamlDotNet.Core.Tokens;

namespace Masa.Contrib.Configuration.ConfigurationApi.Dcc;

public class ConfigurationApiClient : ConfigurationApiBase, IConfigurationApiClient
{
    private readonly ICaller _client;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly JsonSerializerOptions _dynamicJsonSerializerOptions;
    private readonly ILogger<ConfigurationApiClient>? _logger;
    private readonly DccOptions _dccOptions;

    private readonly Masa.BuildingBlocks.Data.ISerializer _yamlSerializer;
    private readonly Masa.BuildingBlocks.Data.IDeserializer _yamlDeserializer;

    public ConfigurationApiClient(
        IServiceProvider serviceProvider,
        JsonSerializerOptions jsonSerializerOptions,
        DccOptions dccOptions,
        DccSectionOptions defaultSectionOption,
        List<DccSectionOptions>? expandSectionOptions)
        : base(defaultSectionOption, expandSectionOptions)
    {
        var callerFactory = serviceProvider.GetRequiredService<ICallerFactory>();
        _client = callerFactory.Create(DEFAULT_CLIENT_NAME);
        _jsonSerializerOptions = jsonSerializerOptions;
        _dynamicJsonSerializerOptions = new JsonSerializerOptions(_jsonSerializerOptions);
        _dynamicJsonSerializerOptions.EnableDynamicTypes();
        _logger = serviceProvider.GetService<ILogger<ConfigurationApiClient>>();
        _yamlSerializer = new DefaultYamlSerializer(new SerializerBuilder().JsonCompatible().Build());
        _yamlDeserializer = new DefaultYamlDeserializer(new DeserializerBuilder().Build());
        _dccOptions = dccOptions;
    }

    public Task<(string Raw, ConfigurationTypes ConfigurationType)> GetRawAsync(string configObject, Action<string>? valueChanged)
    {
        return GetRawAsync(GetEnvironment(string.Empty), GetCluster(string.Empty), GetAppId(string.Empty), configObject, valueChanged);
    }

    public Task<(string Raw, ConfigurationTypes ConfigurationType)> GetRawAsync(string environment, string cluster, string appId,
        string configObject, Action<string>? valueChanged)
    {
        var key = FomartKey(environment, cluster, appId, configObject);
        return GetRawByKeyAsync(key, valueChanged);
    }

    public Task<T> GetAsync<T>(string configObject, Action<T>? valueChanged)
    {
        return GetAsync(GetEnvironment(string.Empty), GetCluster(string.Empty), GetAppId(string.Empty), configObject, valueChanged);
    }

    public async Task<T> GetAsync<T>(string environment, string cluster, string appId, string configObject, Action<T>? valueChanged = null)
    {
        var key = FomartKey(environment, cluster, appId, configObject);

        var result = await GetRawByKeyAsync(key, (value) =>
        {
            var result = JsonSerializer.Deserialize<T>(value, _dynamicJsonSerializerOptions);
            valueChanged?.Invoke(result!);
        }).ConfigureAwait(false);

        return JsonSerializer.Deserialize<T>(result.Raw, _dynamicJsonSerializerOptions) ??
            throw new MasaException($"The content of [{configObject}] is wrong");
    }

    public async Task<List<(string ConfigObject, string Raw, ConfigurationTypes ConfigurationType)>> GetRawsAsync(string environment, string cluster, string appId, Action<List<string>>? valueChanged = null, params string[] configObjects)
    {
        var result = await this.GetRawsByKeyAsync(environment, cluster, appId, configObjects).ConfigureAwait(false);
        valueChanged?.Invoke(result.Select(item => item.Raw).ToList());
        return result;
    }

    public async Task<List<(string ConfigObject, string Raw, ConfigurationTypes ConfigurationType)>> GetRawsAsync(string environment, string cluster, string appId, params string[] configObjects)
    {
        return await this.GetRawsByKeyAsync(environment, cluster, appId, configObjects).ConfigureAwait(false);
    }


    public Task<dynamic> GetDynamicAsync(string environment, string cluster, string appId, string configObject,
        Action<dynamic>? valueChanged = null)
    {
        var key = FomartKey(environment, cluster, appId, configObject);

        return GetDynamicAsync(key, (k, value, options) =>
        {
            var result = JsonSerializer.Deserialize<ExpandoObject>(value, options);
            valueChanged?.Invoke(result!);
        });
    }

    public Task<dynamic> GetDynamicAsync(string configObject)
    {
        var key = FomartKey(GetEnvironment(string.Empty), GetCluster(string.Empty), GetAppId(string.Empty), configObject);
        return GetDynamicAsync(key, null);
    }

    protected virtual Task<dynamic> GetDynamicAsync(string key, Action<string, dynamic, JsonSerializerOptions>? valueChanged)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        return GetDynamicInternalAsync(key, valueChanged);
    }

    private async Task<dynamic> GetDynamicInternalAsync(string key, Action<string, dynamic, JsonSerializerOptions>? valueChanged)
    {
        var raw = await GetRawByKeyAsync(key, value =>
        {
            valueChanged?.Invoke(key, value, _dynamicJsonSerializerOptions);
        }).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ExpandoObject>(raw.Raw, _dynamicJsonSerializerOptions) ?? throw new ArgumentException(key);

    }

    #region Request dcc interface to query data

    /// <summary>
    /// If no key is passed in, all will be queried by default.
    /// </summary>
    /// <param name="key">configuration name</param>
    /// <returns></returns>
    protected virtual async Task<Dictionary<string, PublishReleaseModel>> GetAsync(params string[] configObjects)
    {
        return await this.GetAsync(string.Empty, string.Empty, string.Empty, configObjects).ConfigureAwait(false);
    }

    /// <summary>
    /// If no key is passed in, all will be queried by default.
    /// </summary>
    /// <param name="key">configuration name</param>
    /// <returns></returns>
    protected virtual async Task<Dictionary<string, PublishReleaseModel>> GetAsync(string environment, string cluster, string appId, params string[] configObjects)
    {
        var requestUri = $"open-api/releasing/get/{GetEnvironment(environment)}/{GetCluster(cluster)}/{GetAppId(appId)}";
        var result = await _client.PostAsync<Dictionary<string, PublishReleaseModel>>(requestUri, configObjects, default).ConfigureAwait(false);
        return result!;
    }

    #endregion

    protected virtual async Task<List<(string ConfigObject, string Raw, ConfigurationTypes ConfigurationType)>> GetRawsByKeyAsync(string environment, string cluster, string appId, params string[] configObjects)
    {
        var result = new List<(string ConfigObject, string Raw, ConfigurationTypes ConfigurationType)>();
        var data = await this.GetAsync(environment, cluster, appId, configObjects).ConfigureAwait(false);
        foreach (var item in data)
        {
            var dickey = FomartKey(environment, cluster, appId, item.Key);
            var value = FormatRaws(data[item.Key], dickey);
            result.Add(value);
        }

        return result;
    }

    protected virtual async Task<(string Raw, ConfigurationTypes ConfigurationType)> GetRawByKeyAsync(string key,
        Action<string>? valueChanged)
    {
        var result = await this.GetAsync(key).ConfigureAwait(false);
        if (result is null)
        {
            return default;
        }

        var dickey = FomartKey(string.Empty, string.Empty, string.Empty, key);
        if (!result.ContainsKey(dickey))
        {
            return default;
        }

        var value = FormatRaw(result[key], key);
        valueChanged?.Invoke(value.Raw);
        return value;
    }

    protected virtual (string ConfigObject, string Raw, ConfigurationTypes ConfigurationType) FormatRaws(PublishReleaseModel? publishRelease, string key)
    {
        PublishReleaseModel result = FormatPublishRelease(publishRelease, key);

        switch (result.ConfigFormat)
        {
            case ConfigFormats.JSON:
                return (key, result.Content!, ConfigurationTypes.Json);

            case ConfigFormats.RAW:
                return (key, result.Content!, ConfigurationTypes.Text);

            case ConfigFormats.Properties:
                try
                {
                    var properties = PropertyConfigurationParser.Parse(result.Content!, _jsonSerializerOptions);
                    return (key, JsonSerializer.Serialize(properties, _jsonSerializerOptions), ConfigurationTypes.Properties);
                }
                catch (Exception exception)
                {
                    _logger?.LogWarning(exception,
                        "Dcc.ConfigurationApiClient: configObject invalid, {ParamName} is not a valid Properties type", key);
                    throw new ArgumentException("configObject invalid");
                }

            case ConfigFormats.XML:
                try
                {
                    var json = XmlConfigurationParser.XmlToJson(result.Content!);
                    return (key, json, ConfigurationTypes.Xml);
                }
                catch (Exception exception)
                {
                    _logger?.LogWarning(exception, "Dcc.ConfigurationApiClient: configObject invalid, {ParamName} is not a valid Xml type",
                        key);
                    throw new ArgumentException("configObject invalid");
                }

            case ConfigFormats.YAML:
                try
                {
                    var yamlObject = _yamlDeserializer.Deserialize<object>(result.Content!);

                    var json = _yamlSerializer.Serialize(yamlObject);
                    return (key, json, ConfigurationTypes.Yaml);
                }
                catch (Exception exception)
                {
                    _logger?.LogWarning(exception, "Dcc.ConfigurationApiClient: configObject invalid, {ParamName} is not a valid Yaml type",
                        key);
                    throw new ArgumentException("configObject invalid");
                }

            default:
                throw new NotSupportedException("Unsupported configuration type");
        }
    }

    protected virtual (string Raw, ConfigurationTypes ConfigurationType) FormatRaw(PublishReleaseModel? publishRelease, string key)
    {
        var value = this.FormatRaws(publishRelease, key);

        return (value.Raw, value.ConfigurationType);
    }

    private string FomartKey(string environment, string cluster, string appId, string configObject)
        => $"{GetEnvironment(environment)}-{GetCluster(cluster)}-{GetAppId(appId)}-{GetConfigObject(configObject)}".ToLower();

    private PublishReleaseModel FormatPublishRelease(PublishReleaseModel? publishRelease, string key)
    {
        if (publishRelease == null)
            throw new ArgumentException($"configObject invalid, {key} is not null");

        if (publishRelease.ConfigFormat == 0)
            throw new ArgumentException($"Dcc.ConfigurationApiClient: configObject invalid, {key} is an unsupported type");

        if (publishRelease.Encryption)
        {
            if (string.IsNullOrEmpty(_dccOptions.ConfigObjectSecret))
            {
                throw new ArgumentNullException(_dccOptions.ConfigObjectSecret, nameof(_dccOptions.ConfigObjectSecret));
            }
            publishRelease.Content = DecryptContent(_dccOptions.ConfigObjectSecret, publishRelease.Content);
        }

        return publishRelease;
    }

    private static string? DecryptContent(string secret, string? content)
    {
        if (!string.IsNullOrEmpty(content) && content != "{}" && content != "[]")
        {
            var encryptContent = AesUtils.Decrypt(content, secret, FillType.Left);
            return encryptContent;
        }
        else
        {
            return content;
        }
    }
}
