using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace App.IntegrationTests.Utils
{
    public class TestDataUtil
    {
        public static void PrepareInstance(int instanceOwnerId, string instanceGuid)
        {
            PrepareInstance(instanceOwnerId, new Guid(instanceGuid));
        }

        public static void PrepareInstance(int instanceOwnerId, Guid instanceGuid)
        {
            string instancePath = GetInstancePath(instanceOwnerId, instanceGuid);

            string preInstancePath = instancePath.Replace(".json", ".pretest.json");

            File.Copy(preInstancePath, instancePath);
        }

        public static void DeleteInstance(int instanceOwnerId, Guid instanceGuid)
        {
            string instancePath = GetInstancePath(instanceOwnerId, instanceGuid);
            if (File.Exists(instancePath))
            {
                File.Delete(instancePath);
            }
        }

        public static void DeleteInstanceAndDataAndBlobs(int instanceOwnerId, string instanceguid, string org, string app)
        {
            DeleteInstanceAndData(instanceOwnerId, new Guid(instanceguid));
            string path = GetBlobPathForApp(org, app, instanceguid);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        public static void DeleteInstanceAndData(int instanceOwnerId, string instanceguid)
        {
            DeleteInstanceAndData(instanceOwnerId, new Guid(instanceguid));
        }

        public static void DeleteInstanceAndData(int instanceOwnerId, Guid instanceGuid)
        {
           DeleteDataForInstance(instanceOwnerId, instanceGuid);

            string instancePath = GetInstancePath(instanceOwnerId, instanceGuid);
            if (File.Exists(instancePath))
            {
                File.Delete(instancePath);
            }
        }

        

        public static void DeleteDataForInstance(int instanceOwnerId, Guid instanceGuid)
        {
            string path = GetDataPath(instanceOwnerId, instanceGuid);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private static string GetInstancePath(int instanceOwnerId, Guid instanceGuid)
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(TestDataUtil).Assembly.CodeBase).LocalPath);
            return Path.Combine(unitTestFolder, @"..\..\..\data\instances\", instanceOwnerId + @"\", instanceGuid.ToString() + @".json");
        }

        private static string GetDataPath(int instanceOwnerId, Guid instanceGuid)
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(TestDataUtil).Assembly.CodeBase).LocalPath);
            return Path.Combine(unitTestFolder, @"..\..\..\data\instances\", instanceOwnerId + @"\", instanceGuid.ToString());
        }

        private static string GetDataBlobPath(int instanceOwnerId, Guid instanceGuid, Guid dataId)
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(TestDataUtil).Assembly.CodeBase).LocalPath);
            return Path.Combine(unitTestFolder, @"..\..\..\data\instances\", instanceOwnerId + @"\", instanceGuid.ToString() + @"\blob\" + dataId.ToString());
        }

        private static string GetBlobPathForApp(string org, string app, string instanceId)
        {
            string unitTestFolder = Path.GetDirectoryName(new Uri(typeof(TestDataUtil).Assembly.CodeBase).LocalPath);
            return Path.Combine(unitTestFolder, @"..\..\..\data\blob\", org + @"\", app + @"\", instanceId);
        }
    }
}