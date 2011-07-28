using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Xml.Linq;
using Sage.Platform.FileSystem.Interfaces;
using System.Xml;
using System.Collections;
using System.Drawing;

namespace Sage.Platform.Upgrade
{
    public static class ResxDiffMerge
    {
        private static Dictionary<string, object> GetAllResources(IFileInfo resxFile)
        {
            using (var stream = resxFile.Open(FileMode.Open, FileAccess.Read))
            {
                return ReadAllResxEntries(stream);
            }
        }

        private static Dictionary<string, object> ReadAllResxEntries(Stream resxStream)
        {
            using (var reader = new ResXResourceReader(resxStream))
            {
                return reader
                    .Cast<DictionaryEntry>()
                    .ToDictionary(entry => (string)entry.Key, entry => entry.Value);
            }
        }

        public static ResxDifferences CompareResxFiles(IFileInfo currentResxFile, IFileInfo previousResxFile)
        {
            Dictionary<string, object> currentResources = GetAllResources(currentResxFile);
            Dictionary<string, object> previousResources = GetAllResources(previousResxFile);

            var resourcesAdded = currentResources
                .Where(pair => !previousResources.Keys.Contains(pair.Key))
                .OrderBy(pair => pair.Key);
            var resourcesRemoved = previousResources
                .Where(pair => !currentResources.Keys.Contains(pair.Key))
                .OrderBy(pair => pair.Key);
            var resourcesModified = from prevResource in previousResources
                                join curResource in currentResources
                                on prevResource.Key equals curResource.Key
                                where !ResourceEntriesAreEqual(prevResource.Value, curResource.Value)
                                orderby prevResource.Key
                                select new ResourceDifference(prevResource.Key, prevResource.Value, curResource.Value);

            return new ResxDifferences(resourcesAdded, resourcesRemoved, resourcesModified);
        }

        private static bool ResourceEntriesAreEqual(object resource1, object resource2)
        {
            if (resource1 is Image && resource2 is Image)
                return ImagesAreEqual((Image) resource1, (Image) resource2);

            return resource1.Equals(resource2);
        }

        private static bool ImagesAreEqual(Image image1, Image image2)
        {
            using (var stream1 = new MemoryStream())
            using (var stream2 = new MemoryStream())
            {
                image1.Save(stream1, image1.RawFormat);
                image2.Save(stream2, image1.RawFormat);
                return stream1.GetBuffer().SequenceEqual(stream2.GetBuffer());
            }
        }

        public static void MergeChangesIntoResx(ResxDifferences changes, IFileInfo targetResxFile)
        {
            Dictionary<string, object> currentEntries;
            using (var stream = targetResxFile.Open(FileMode.Open, FileAccess.Read))
            {
                currentEntries = ReadAllResxEntries(stream);
            }

            using (var newStream = targetResxFile.Open(FileMode.Truncate, FileAccess.Write))
            {
                MergeChangesIntoResx(changes, currentEntries, newStream);
            }
        }

        private static void MergeChangesIntoResx(ResxDifferences changes, Dictionary<string, object> currentEntries,
            Stream newResxStream)
        {
            var addedKeys = changes.ResourcesAdded.Select(pair => pair.Key);
            var removedKeys = changes.ResourcesRemoved.Select(pair => pair.Key);

            using (var writer = new ResXResourceWriter(newResxStream))
            {
                foreach (var resourceEntry in currentEntries)
                {
                    string currentKey = resourceEntry.Key;
                    if (addedKeys.Contains(currentKey))
                        continue;
                    if (removedKeys.Contains(currentKey))
                        continue;

                    var modifiedResource = changes.ResourcesModified.FirstOrDefault(mod => mod.ResourceName == currentKey);
                    if (modifiedResource != null)
                        writer.AddResource(currentKey, modifiedResource.NewValue);
                    else
                        writer.AddResource(currentKey, resourceEntry.Value);
                }

                changes.ResourcesAdded.ForEach(res => writer.AddResource(res.Key, res.Value));
            }    
        }
    }

    public class ResourceDifference
    {
        public string ResourceName { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }

        public ResourceDifference(string resourceName, object oldValue, object newValue)
        {
            ResourceName = resourceName;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    public class ResxDifferences
    {
        public IEnumerable<KeyValuePair<string, object>> ResourcesAdded { get; private set; }
        public IEnumerable<KeyValuePair<string, object>> ResourcesRemoved { get; private set; }
        public IEnumerable<ResourceDifference> ResourcesModified { get; private set; }

        public ResxDifferences(IEnumerable<KeyValuePair<string, object>> resourcesAdded, 
            IEnumerable<KeyValuePair<string, object>> resourcesRemoved, 
            IEnumerable<ResourceDifference> resourcesModified)
        {
            ResourcesAdded = resourcesAdded;
            ResourcesRemoved = resourcesRemoved;
            ResourcesModified = resourcesModified;
        }

        public bool None
        {
            get { return !ResourcesAdded.Any() && !ResourcesModified.Any() && !ResourcesRemoved.Any(); }
        }
    }
}