using System.Text.Json;
using RemoteMe.Core.Model;

namespace RemoteMe.Tests.Model;

public sealed class SettingsJsonTests {
    [Fact]
    public void Serialize_KeepsTheStoredFormat() {
        var settings = SampleData.Settings(password: "secret");

        var json = JsonSerializer.Serialize(settings, ModelJsonContext.Default.Settings);

        Assert.Equal(
            "{\"schemaVersion\":1,\"groups\":[\"Lab\"],\"collapsedGroups\":[],\"connections\":["
            + "{\"id\":\"6f1c2b8e-4a1d-4c3e-9b7a-2d5e8f0a1b3c\",\"name\":\"Build server\",\"group\":\"Lab\","
            + "\"kind\":\"Network\",\"address\":\"build.example.test\",\"userName\":\"test-user\",\"password\":\"secret\","
            + "\"redirection\":{\"clipboard\":true,\"audio\":true,\"microphone\":false,\"drives\":true,\"printers\":false}},"
            + "{\"id\":\"0b9e7d6c-5a4f-4e3d-8c2b-1a0f9e8d7c6b\",\"name\":\"Test VM\",\"kind\":\"HyperV\",\"address\":\"localhost\","
            + "\"vmId\":\"a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d\",\"userName\":\"\","
            + "\"redirection\":{\"clipboard\":true,\"audio\":true,\"microphone\":true,\"drives\":false,\"printers\":false}}]}",
            json);
    }

    [Fact]
    public void BasicSession_IsWrittenOnlyWhenOn() {
        var vm = SampleData.Settings().Connections[1] with { BasicSession = true };
        var settings = new Settings { Connections = [vm] };

        var json = JsonSerializer.Serialize(settings, ModelJsonContext.Default.Settings);
        var loaded = JsonSerializer.Deserialize(json, ModelJsonContext.Default.Settings);

        Assert.Contains("\"basicSession\":true", json, StringComparison.Ordinal);
        Assert.True(Assert.Single(loaded!.Connections).BasicSession);
    }

    [Fact]
    public void ClockTimeZone_IsTheLocalTimeByDefaultAndLeftOutOfTheFile() {
        var settings = new Settings();

        var json = JsonSerializer.Serialize(settings, ModelJsonContext.Default.Settings);

        Assert.Null(settings.ClockTimeZone);
        Assert.DoesNotContain("clockTimeZone", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ClockTimeZone_IsWrittenAndReadBack() {
        var settings = SampleData.Settings() with { ClockTimeZone = "N. Central Asia Standard Time" };

        var json = JsonSerializer.Serialize(settings, ModelJsonContext.Default.Settings);
        var loaded = JsonSerializer.Deserialize(json, ModelJsonContext.Default.Settings);

        Assert.Contains("\"clockTimeZone\":\"N. Central Asia Standard Time\"", json, StringComparison.Ordinal);
        Assert.Equal("N. Central Asia Standard Time", loaded!.ClockTimeZone);
    }

    [Fact]
    public void Deserialize_AFileWithoutClockTimeZone_ShowsTheLocalTime() {
        const string json = "{\"schemaVersion\":1,\"groups\":[],\"connections\":[]}";

        var loaded = JsonSerializer.Deserialize(json, ModelJsonContext.Default.Settings);

        Assert.Null(loaded!.ClockTimeZone);
    }

    [Fact]
    public void CollapsedGroups_AreWrittenAndReadBack() {
        var settings = SampleData.Settings() with { CollapsedGroups = ["Lab"] };

        var json = JsonSerializer.Serialize(settings, ModelJsonContext.Default.Settings);
        var loaded = JsonSerializer.Deserialize(json, ModelJsonContext.Default.Settings);

        Assert.Contains("\"collapsedGroups\":[\"Lab\"]", json, StringComparison.Ordinal);
        Assert.Equal(["Lab"], loaded!.CollapsedGroups);
    }

    [Fact]
    public void Deserialize_AFileWithoutCollapsedGroups_ShowsEveryGroupOpen() {
        const string json = "{\"schemaVersion\":1,\"groups\":[],\"connections\":[]}";

        var loaded = JsonSerializer.Deserialize(json, ModelJsonContext.Default.Settings);

        Assert.Empty(loaded!.CollapsedGroups);
    }

    [Fact]
    public void Deserialize_RestoresPasswordsThatNeedEscaping() {
        var settings = SampleData.Settings();

        var json = JsonSerializer.SerializeToUtf8Bytes(settings, ModelJsonContext.Default.Settings);
        var loaded = JsonSerializer.Deserialize(json, ModelJsonContext.Default.Settings);

        Assert.NotNull(loaded);
        SampleData.AssertSame(settings, loaded);
    }

    [Fact]
    public void Deserialize_WithoutARequiredProperty_Throws() {
        const string json = "{\"connections\":[{\"id\":\"6f1c2b8e-4a1d-4c3e-9b7a-2d5e8f0a1b3c\",\"address\":\"host\"}]}";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, ModelJsonContext.Default.Settings));
    }

    [Fact]
    public void NewRedirectionSettings_TurnOnClipboardAudioAndMicrophoneOnly() {
        var redirection = new RedirectionSettings();

        Assert.True(redirection.Clipboard);
        Assert.True(redirection.Audio);
        Assert.True(redirection.Microphone);
        Assert.False(redirection.Drives);
        Assert.False(redirection.Printers);
    }
}
