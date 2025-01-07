﻿using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lip;

public record PackageManifest
{
    public record AssetType
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum TypeEnum
        {
            [JsonStringEnumMemberName("self")]
            Self,
            [JsonStringEnumMemberName("tar")]
            Tar,
            [JsonStringEnumMemberName("tgz")]
            Tgz,
            [JsonStringEnumMemberName("uncompressed")]
            Uncompressed,
            [JsonStringEnumMemberName("zip")]
            Zip,
        }

        [JsonPropertyName("type")]
        public required TypeEnum Type { get; init; }

        [JsonPropertyName("urls")]
        public List<string>? Urls { get; init; }

        [JsonPropertyName("place")]
        public List<PlaceType>? Place { get; init; }

        [JsonPropertyName("preserve")]
        public List<string>? Preserve { get; init; }

        [JsonPropertyName("remove")]
        public List<string>? Remove { get; init; }
    }

    public partial record InfoType
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("author")]
        public string? Author { get; init; }

        [JsonPropertyName("tags")]
        public List<string>? Tags
        {
            get => _tags;
            init
            {
                if (value is not null)
                {
                    foreach (string tag in value)
                    {
                        if (!StringValidator.IsTagValid(tag))
                        {
                            throw new ArgumentException($"Tag {tag} is invalid.", nameof(value));
                        }
                    }
                }
                _tags = value;
            }
        }

        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; init; }

        private List<string>? _tags;
    }

    public record PlaceType
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum TypeEnum
        {
            [JsonStringEnumMemberName("file")]
            File,
            [JsonStringEnumMemberName("dir")]
            Dir,
        }

        [JsonPropertyName("type")]
        public required TypeEnum Type { get; init; }

        [JsonPropertyName("src")]
        public required string Src { get; init; }

        [JsonPropertyName("dest")]
        public required string Dest { get; init; }
    }

    public partial record ScriptsType
    {
        [JsonPropertyName("pre_install")]
        public List<string>? PreInstall { get; init; }

        [JsonPropertyName("install")]
        public List<string>? Install { get; init; }

        [JsonPropertyName("post_install")]
        public List<string>? PostInstall { get; init; }

        [JsonPropertyName("pre_pack")]
        public List<string>? PrePack { get; init; }

        [JsonPropertyName("post_pack")]
        public List<string>? PostPack { get; init; }

        [JsonPropertyName("pre_uninstall")]
        public List<string>? PreUninstall { get; init; }

        [JsonPropertyName("uninstall")]
        public List<string>? Uninstall { get; init; }

        [JsonPropertyName("post_uninstall")]
        public List<string>? PostUninstall { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }

        [JsonIgnore]
        public Dictionary<string, List<string>> AdditionalScripts
        {
            get
            {
                var additionalScripts = new Dictionary<string, List<string>>();
                foreach (KeyValuePair<string, JsonElement> kvp in AdditionalProperties ?? [])
                {
                    string key = kvp.Key;
                    JsonElement value = kvp.Value;

                    // Ignore all properties that don't match the script name and value pattern.

                    if (!StringValidator.IsScriptNameValid(key))
                    {
                        continue;
                    }

                    if (value.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    bool allStrings = true;
                    foreach (JsonElement element in value.EnumerateArray())
                    {
                        if (element.ValueKind != JsonValueKind.String)
                        {
                            allStrings = false;
                            break;
                        }
                    }
                    if (!allStrings)
                    {
                        continue;
                    }

                    // The value will always be an array of strings, since we've checked that above.
                    List<string> scripts = value.Deserialize<List<string>>()!;

                    additionalScripts[kvp.Key] = scripts;
                }
                return additionalScripts;
            }
            init
            {
                AdditionalProperties ??= [];

                foreach (KeyValuePair<string, List<string>> kvp in value)
                {
                    AdditionalProperties[kvp.Key] = JsonSerializer.SerializeToElement(kvp.Value);
                }
            }
        }
    }

    public record VariantType
    {
        [JsonPropertyName("label")]
        public string? Label { get; init; }

        [JsonPropertyName("platform")]
        public string? Platform { get; init; }

        [JsonPropertyName("dependencies")]
        public Dictionary<string, string>? Dependencies { get; init; }

        [JsonPropertyName("assets")]
        public List<AssetType>? Assets { get; init; }

        [JsonPropertyName("scripts")]
        public ScriptsType? Scripts { get; init; }
    }

    public const int DefaultFormatVersion = 3;
    public const string DefaultFormatUuid = "289f771f-2c9a-4d73-9f3f-8492495a924d";

    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
    {
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IndentSize = 4,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
    };

    [JsonPropertyName("format_version")]
    public required int FormatVersion
    {
        get => DefaultFormatVersion;
        init => _ = value == DefaultFormatVersion ? 0
            : throw new ArgumentException($"Format version is not equal to {DefaultFormatVersion}.", nameof(value));
    }

    [JsonPropertyName("format_uuid")]
    public required string FormatUuid
    {
        get => DefaultFormatUuid;
        init => _ = value == DefaultFormatUuid ? 0
            : throw new ArgumentException($"Format UUID is not equal to {DefaultFormatUuid}.", nameof(value));
    }

    [JsonPropertyName("tooth")]
    public required string Tooth { get; init; }

    [JsonPropertyName("version")]
    public required string Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (!StringValidator.IsVersionValid(value))
            {
                throw new ArgumentException($"Version {value} is invalid.", nameof(value));
            }

            _version = value;
        }
    }

    [JsonPropertyName("info")]
    public InfoType? Info { get; init; }

    [JsonPropertyName("variants")]
    public VariantType[]? Variants { get; init; }

    private string _version = "0.0.0"; // The default value does never get used.

    public static PackageManifest FromBytes(byte[] bytes)
    {
        PackageManifest? manifest = JsonSerializer.Deserialize<PackageManifest>(
            bytes,
            s_jsonSerializerOptions
        ) ?? throw new ArgumentException("Failed to deserialize package manifest.", nameof(bytes));

        return manifest;
    }

    public byte[] ToBytes()
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(this, s_jsonSerializerOptions);
        return bytes;
    }
}
