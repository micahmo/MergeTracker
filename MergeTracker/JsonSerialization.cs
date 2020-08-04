using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using SpecialFolder = System.Environment.SpecialFolder;

namespace MergeTracker
{
    public class JsonSerialization
    {
        public static void SerializeObjectToCustomConfigFile<T>(string configFileName, T objectToSerialize, SpecialFolder specialFolder = SpecialFolder.CommonDocuments)
        {
            string customConfigFilePath = GetCustomConfigFilePath(specialFolder, configFileName);
            JsonSerializer serializer = new JsonSerializer();

            using var mutex = new Mutex(false, customConfigFilePath.GetHashCode().ToString());

            try
            {
                if (mutex.WaitOne())
                {
                    using Stream fileStream = new FileStream(customConfigFilePath, FileMode.Create);
                    using StreamWriter streamWriter = new StreamWriter(fileStream);
                    using JsonTextWriter jsonWriter = new JsonTextWriter(streamWriter);
                        
                    serializer.Serialize(jsonWriter, objectToSerialize);
                    jsonWriter.Flush();
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        public static T DeserializeObjectFromCustomConfigFile<T>(string configFileName, Environment.SpecialFolder specialFolder = Environment.SpecialFolder.CommonDocuments)
        {
            string customConfigFilePath = GetCustomConfigFilePath(specialFolder, configFileName);
            JsonSerializer serializer = new JsonSerializer();

            using FileStream configFile = File.Open(customConfigFilePath, FileMode.Open, FileAccess.Read);
            using StreamReader streamReader = new StreamReader(configFile);
            using JsonTextReader jsonTextReader = new JsonTextReader(streamReader);

            return serializer.Deserialize<T>(jsonTextReader);
        }

        public static string GetCustomConfigFilePath(SpecialFolder specialFolder, string configFileName, bool createIfNotExists = true)
        {
            string customConfigFilePath = Environment.GetFolderPath(specialFolder);
            customConfigFilePath = Path.Combine(customConfigFilePath, "MergeTracker");

            if (Directory.Exists(customConfigFilePath) == false)
            {
                // If the directory doesn't exist, create it.
                Directory.CreateDirectory(customConfigFilePath);
            }

            customConfigFilePath = Path.Combine(customConfigFilePath, configFileName);

            if (File.Exists(customConfigFilePath) == false && createIfNotExists)
            {
                // If the file doesn't exist, create it
                using (File.Create(customConfigFilePath)) { }
            }

            return customConfigFilePath;
        }
    }
}
