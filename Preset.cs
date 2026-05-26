using System.Xml.Serialization;

namespace CustomRPC.CLI
{
    /// <summary>
    /// Preset file structure — XML-compatible with the original Windows CustomRP app (.crp files).
    /// </summary>
    [Serializable]
    public struct Preset
    {
        public string ID;
        public int Type;
        public int Display;
        public string Name;
        public string Details;
        public string DetailsURL;
        public string State;
        public string StateURL;
        public int PartySize;
        public int PartyMax;
        public int Timestamps;
        public DateTime CustomTimestamp;
        public bool CustomTimestampEndEnabled;
        public DateTime CustomTimestampEnd;
        public string LargeKey;
        public string LargeText;
        public string LargeURL;
        public string SmallKey;
        public string SmallText;
        public string SmallURL;
        public string Button1Text;
        public string Button1URL;
        public string Button2Text;
        public string Button2URL;

        /// <summary>Load a .crp preset file.</summary>
        public static Preset? Load(string filePath)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(Preset));
                using var reader = new StreamReader(filePath);
                return (Preset?)serializer.Deserialize(reader);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error loading preset: {ex.Message}");
                return null;
            }
        }

        /// <summary>Save as a .crp preset file (readable by the Windows app too).</summary>
        public bool Save(string filePath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");
                var serializer = new XmlSerializer(typeof(Preset));
                using var writer = new StreamWriter(filePath);
                serializer.Serialize(writer, this);
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error saving preset: {ex.Message}");
                return false;
            }
        }

        /// <summary>Build a Preset from the current Config.</summary>
        public static Preset FromConfig(Config c) => new()
        {
            ID = c.Id,
            Type = c.Type,
            Display = c.Display,
            Name = c.Name,
            Details = c.Details,
            DetailsURL = c.DetailsUrl,
            State = c.State,
            StateURL = c.StateUrl,
            PartySize = c.PartySize,
            PartyMax = c.PartyMax,
            Timestamps = c.Timestamps,
            CustomTimestamp = c.CustomTimestamp,
            CustomTimestampEndEnabled = c.CustomTimestampEndEnabled,
            CustomTimestampEnd = c.CustomTimestampEnd,
            LargeKey = c.LargeKey,
            LargeText = c.LargeText,
            LargeURL = c.LargeUrl,
            SmallKey = c.SmallKey,
            SmallText = c.SmallText,
            SmallURL = c.SmallUrl,
            Button1Text = c.Button1Text,
            Button1URL = c.Button1Url,
            Button2Text = c.Button2Text,
            Button2URL = c.Button2Url,
        };

        /// <summary>Apply this preset's values onto a Config (keeps Id, Pipe, AutoConnect).</summary>
        public void ApplyTo(Config c)
        {
            c.Type = Type;
            c.Display = Display;
            c.Name = Name ?? "";
            c.Details = Details ?? "";
            c.DetailsUrl = DetailsURL ?? "";
            c.State = State ?? "";
            c.StateUrl = StateURL ?? "";
            c.PartySize = PartySize;
            c.PartyMax = PartyMax;
            c.Timestamps = Timestamps;
            c.CustomTimestamp = CustomTimestamp;
            c.CustomTimestampEndEnabled = CustomTimestampEndEnabled;
            c.CustomTimestampEnd = CustomTimestampEnd;
            c.LargeKey = LargeKey ?? "";
            c.LargeText = LargeText ?? "";
            c.LargeUrl = LargeURL ?? "";
            c.SmallKey = SmallKey ?? "";
            c.SmallText = SmallText ?? "";
            c.SmallUrl = SmallURL ?? "";
            c.Button1Text = Button1Text ?? "";
            c.Button1Url = Button1URL ?? "";
            c.Button2Text = Button2Text ?? "";
            c.Button2Url = Button2URL ?? "";
        }
    }
}
