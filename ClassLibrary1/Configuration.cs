using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using PeterHan.PLib.Options;
using UnityEngine;

namespace ONI_MP
{
    [Serializable]
    [ConfigFile(SharedConfigLocation: true)]
    public class Configuration : SingletonOptions<Configuration>, IOptions
    {
 
        [JsonProperty]
        public HostSettings Host { get; set; } = new HostSettings();

        [JsonProperty]
        public ClientSettings Client { get; set; } = new ClientSettings();

        [Option("STRINGS.UI.CONFIGURATION.TITLES.HOST_SETTINGS.MAX_MESSAGES_PER_POLL", "STRINGS.UI.CONFIGURATION.TOOLTIPS.HOST_SETTINGS.MAX_MESSAGES_PER_POLL", "STRINGS.UI.CONFIGURATION.HEADERS.HOST_SETTINGS")]
        public int HostMaxMessagesPerPoll
        {
            get => Host.MaxMessagesPerPoll;
            set => Host.MaxMessagesPerPoll = Mathf.Clamp(value, 1, 1024);
        }

        [Option("STRINGS.UI.CONFIGURATION.TITLES.HOST_SETTINGS.SAVE_FILE_TRANSFER_CHUNK", "STRINGS.UI.CONFIGURATION.TOOLTIPS.HOST_SETTINGS.SAVE_FILE_TRANSFER_CHUNK", "STRINGS.UI.CONFIGURATION.HEADERS.HOST_SETTINGS")]
        public int SaveFileTransferChunkKB
        {
            get => Host.SaveFileTransferChunkKB;
            set => Host.SaveFileTransferChunkKB = Mathf.Clamp(value, 1, 1024);
        }

        [Option("STRINGS.UI.CONFIGURATION.TITLES.CLIENT_SETTINGS.MAX_MESSAGES_PER_POLL", "STRINGS.UI.CONFIGURATION.TOOLTIPS.CLIENT_SETTINGS.MAX_MESSAGES_PER_POLL", "STRINGS.UI.CONFIGURATION.HEADERS.CLIENT_SETTINGS")]
        public int ClientMaxMessagesPerPoll
        {
            get => Client.MaxMessagesPerPoll;
            set => Client.MaxMessagesPerPoll = Mathf.Clamp(value, 1, 1024);
        }

        [Option("STRINGS.UI.CONFIGURATION.TITLES.CURSOR_SETTINGS.RANDOM_COLOR", "STRINGS.UI.CONFIGURATION.TOOLTIPS.CURSOR_SETTINGS.RANDOM_COLOR", "STRINGS.UI.CONFIGURATION.HEADERS.CURSOR_SETTINGS")]
        public bool UseRandomPlayerColor
        {
            get => Client.UseRandomPlayerColor;
            set => Client.UseRandomPlayerColor = value;
        }

        [Option("STRINGS.UI.CONFIGURATION.TITLES.CURSOR_SETTINGS.RED", "STRINGS.UI.CONFIGURATION.TOOLTIPS.CURSOR_SETTINGS.RED", "STRINGS.UI.CONFIGURATION.HEADERS.CURSOR_SETTINGS")]
        public int CursorRed
        {
            get => (int) Client.PlayerColor.R;
            set => Client.PlayerColor.R = (byte)Mathf.Clamp(value, 0, 255);
        }

        [Option("STRINGS.UI.CONFIGURATION.TITLES.CURSOR_SETTINGS.GREEN", "STRINGS.UI.CONFIGURATION.TOOLTIPS.CURSOR_SETTINGS.GREEN", "STRINGS.UI.CONFIGURATION.HEADERS.CURSOR_SETTINGS")]
        public int CursorGreen
        {
            get => (int) Client.PlayerColor.G;
            set => Client.PlayerColor.G = (byte)Mathf.Clamp(value, 0, 255);
        }

        [Option("STRINGS.UI.CONFIGURATION.TITLES.CURSOR_SETTINGS.BLUE", "STRINGS.UI.CONFIGURATION.TOOLTIPS.CURSOR_SETTINGS.BLUE", "STRINGS.UI.CONFIGURATION.HEADERS.CURSOR_SETTINGS")]
        public int CursorBlue
        {
            get => (int) Client.PlayerColor.B;
            set => Client.PlayerColor.B = (byte)Mathf.Clamp(value, 0, 255);
        }

        [Option("STRINGS.UI.CONFIGURATION.TITLES.MISC_SETTINGS.PUFT_LOADINGSCREEN", "STRINGS.UI.CONFIGURATION.TOOLTIPS.MISC_SETTINGS.PUFT_LOADINGSCREEN", "STRINGS.UI.CONFIGURATION.HEADERS.MISC_SETTINGS")]
        public bool PuftAsLoadingIcon
        {
            get => Client.PuftAsLoadingIcon;
            set => Client.PuftAsLoadingIcon = value;
        }

        [Option("STRINGS.UI.CONFIGURATION.TITLES.MISC_SETTINGS.LOADINGSCREEN_COLOR", "STRINGS.UI.CONFIGURATION.TOOLTIPS.MISC_SETTINGS.LOADINGSCREEN_COLOR", "STRINGS.UI.CONFIGURATION.HEADERS.MISC_SETTINGS")]
        public bool UseCustomLoadingScreenColor
        {
            get => Client.UseCustomLoadingScreenColor;
            set => Client.UseCustomLoadingScreenColor = value;
        }

        public static T GetHostProperty<T>(string propertyName)
        {
            return Instance.GetProperty<T>(Instance.Host, propertyName);
        }

        public static T GetClientProperty<T>(string propertyName)
        {
            return Instance.GetProperty<T>(Instance.Client, propertyName);
        }

        public static void SetHostProperty<T>(string propertyName, T value)
        {
            Instance.SetProperty(Instance.Host, propertyName, value);
            Instance.Save();
        }

        public static void SetClientProperty<T>(string propertyName, T value)
        {
            Instance.SetProperty(Instance.Client, propertyName, value);
            Instance.Save();
        }

        private T GetProperty<T>(object obj, string propertyName)
        {
            var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

            if (prop == null)
                throw new ArgumentException($"Property '{propertyName}' not found on {obj.GetType().Name}");

            if (!typeof(T).IsAssignableFrom(prop.PropertyType))
                throw new InvalidCastException($"Property '{propertyName}' is of type {prop.PropertyType}, not {typeof(T)}");

            return (T)prop.GetValue(obj);
        }

        private void SetProperty<T>(object obj, string propertyName, T value)
        {
            var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

            if (prop == null)
                throw new ArgumentException($"Property '{propertyName}' not found on {obj.GetType().Name}");

            if (!typeof(T).IsAssignableFrom(prop.PropertyType))
                throw new InvalidCastException($"Value of type {typeof(T)} cannot be assigned to property '{propertyName}'");

            prop.SetValue(obj, value);
        }

        public void Save()
        {
            POptions.WriteSettings(this);
        }

        public IEnumerable<IOptionsEntry> CreateOptions()
        {
            return new List<IOptionsEntry>();
        }

        public void OnOptionsChanged()
        {
            Instance = this;
        }
    }

    [Serializable]
    public class HostSettings
    {
        [JsonProperty] public int NetworkTransport { get; set; } = 0;
        [JsonProperty] public int MaxLobbySize { get; set; } = 4;
        [JsonProperty] public int MaxMessagesPerPoll { get; set; } = 128;
        [JsonProperty] public int SaveFileTransferChunkKB { get; set; } = 256;

        [JsonProperty] public LanSettings LanSettings { get; set; } = new LanSettings();
        [JsonProperty] public LobbySettings Lobby { get; set; } = new LobbySettings();
    }

    [Serializable]
    public class ClientSettings
    {
        [JsonProperty] public int MaxMessagesPerPoll { get; set; } = 16;
        [JsonProperty] public bool UseRandomPlayerColor { get; set; } = true;
        [JsonProperty] public ColorRGB PlayerColor { get; set; } = new ColorRGB(255, 255, 255);
        [JsonProperty] public LanSettings LanSettings { get; set; } = new LanSettings();
        [JsonProperty] public bool PuftAsLoadingIcon { get; set; } = true;
        [JsonProperty] public bool UseCustomLoadingScreenColor { get; set; } = true;

    }

    [Serializable]
    public class LobbySettings
    {
        [JsonProperty] public bool IsPrivate { get; set; } = false;
        [JsonProperty] public bool RequirePassword { get; set; } = false;
        [JsonProperty] public string PasswordHash { get; set; } = "";
        [JsonProperty] public string LobbyName { get; set; } = "";
        [JsonProperty] public string Region { get; set; } = "";
    }

    [Serializable]
    public class LanSettings
    {
        [JsonProperty] public string Ip { get; set; } = "127.0.0.1";
        [JsonProperty] public int Port { get; set; } = 8080;

        public string GetHashedAddress()
        {
            string value = $"{Ip}:{Port}";
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            return Convert.ToBase64String(bytes);
        }
    }

    [Serializable]
    public class ColorRGB
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public ColorRGB() { }

        public ColorRGB(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public Color ToColor() => new Color(R / 255f, G / 255f, B / 255f);

        public static ColorRGB FromColor(Color color) =>
            new ColorRGB(
                (byte)(color.r * 255),
                (byte)(color.g * 255),
                (byte)(color.b * 255)
            );
    }
}
