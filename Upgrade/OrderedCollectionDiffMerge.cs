using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;
using Sage.Platform.FileSystem.Interfaces;
using Sage.Platform.Projects;

namespace Sage.Platform.Upgrade
{
    public static class OrderedCollectionDiffMerge
    {
        public static void MergeDifferencesIntoTargetOrderFile(IFileInfo baseOrderFile, IFileInfo sourceOrderFile, IFileInfo targetOrderFile)
        {
            List<KeyValuePair<string, string>> baseOrderedItems = GetOrderedItems(baseOrderFile);
            List<KeyValuePair<string, string>> sourceOrderedItems = GetOrderedItems(sourceOrderFile);
            List<KeyValuePair<string, string>> targetOrderedItems = GetOrderedItems(targetOrderFile);

            var mergedItems = MergeDifferencesIntoTargetOrderItems(baseOrderedItems, sourceOrderedItems, targetOrderedItems);
            SaveOrderedCollection(mergedItems, targetOrderFile);
        }

        public static List<KeyValuePair<string, string>> MergeDifferencesIntoTargetOrderItems(
            List<KeyValuePair<string, string>> baseOrderedItems, List<KeyValuePair<string, string>> sourceOrderedItems, 
            List<KeyValuePair<string, string>> targetOrderedItems)
        {
            var matchesOrderedByCurrent = GetOrderedMatches(sourceOrderedItems, baseOrderedItems);
            var matchesOrderedByBase = GetOrderedMatches(baseOrderedItems, sourceOrderedItems);
            bool itemsWereReordered = !matchesOrderedByCurrent.SequenceEqual(matchesOrderedByBase);

            if (itemsWereReordered)
            {
                //merge target to base diffs into source items
                var targetToBaseDiffs = CompareOrderedItems(targetOrderedItems, baseOrderedItems);
                targetToBaseDiffs.ForEach(change => MergeChangeIntoOrderedItems(change, sourceOrderedItems));
                return sourceOrderedItems;
            }
            else
            {
                //merge source to base diffs into target items
                var sourceToBaseDiffs = CompareOrderedItems(sourceOrderedItems, baseOrderedItems);
                sourceToBaseDiffs.ForEach(change => MergeChangeIntoOrderedItems(change, targetOrderedItems));
                return targetOrderedItems;
            }
        }

        private static List<KeyValuePair<string, string>> GetOrderedMatches(List<KeyValuePair<string, string>> orderedItems,
            List<KeyValuePair<string, string>> itemsToMatchOn)
        {
            return (from item in orderedItems
                    join potentionalMatch in itemsToMatchOn on item.Key equals potentionalMatch.Key
                    select item).ToList();
        }

        private static List<KeyValuePair<string, string>> GetOrderedItems(IFileInfo orderFile)
        {
            using (var stream = orderFile.Open(FileMode.Open, FileAccess.Read))
            using (var reader = new StreamReader(stream))
            {
                var xdoc = XDocument.Load(reader);
                return xdoc.Root.Elements("Item")
                    .Select(element => new KeyValuePair<string, string>(element.Attribute("Id").Value, element.Value))
                    .ToList();
            }
        }

        internal static IEnumerable<CollectionDiff> FindRenamedFiles(List<KeyValuePair<string, string>> currentOrderedItems,
            List<KeyValuePair<string, string>> previousOrderedItems)
        {
            var changedItems = from prevItem in previousOrderedItems
                               join curItem in currentOrderedItems on prevItem.Key equals curItem.Key
                               where prevItem.Value != curItem.Value
                               select new { PreviousItem = prevItem, CurrentItem = curItem };
            return changedItems.Select(change => new CollectionDiff
            {
                DiffKind = CollectionDiffKind.FileNameChange,
                Id = change.CurrentItem.Key,
                FileName = change.CurrentItem.Value
            });
        }

        internal static IEnumerable<CollectionDiff> CompareOrderedItems(List<KeyValuePair<string, string>> currentOrderedItems,
            List<KeyValuePair<string, string>> previousOrderedItems)
        {
            var diffs = new List<CollectionDiff>();

            var addedItems = currentOrderedItems.Except(previousOrderedItems, new KeyValuePairComparer());
            diffs.AddRange(CreateDiffsFromAddedItems(addedItems, currentOrderedItems));

            var removedItems = previousOrderedItems.Except(currentOrderedItems, new KeyValuePairComparer());
            diffs.AddRange(removedItems.Select(item => new CollectionDiff
            {
                DiffKind = CollectionDiffKind.Remove,
                Id = item.Key,
                FileName = item.Value
            }));

            diffs.AddRange(FindRenamedFiles(currentOrderedItems, previousOrderedItems));

            return diffs;
        }

        internal static void MergeChangeIntoOrderedItems(CollectionDiff change, List<KeyValuePair<string, string>> orderedItems)
        {
            if (change.DiffKind == CollectionDiffKind.Remove)
            {
                int index = GetItemIndex(change.Id, orderedItems);
                if (index > -1)
                    orderedItems.RemoveAt(index);
            }
            else if (change.DiffKind == CollectionDiffKind.Add)
            {
                int index = CalculateNewItemIndex(change, orderedItems);
                orderedItems.Insert(index, new KeyValuePair<string, string>(change.Id, change.FileName));
            }
            else if (change.DiffKind == CollectionDiffKind.FileNameChange)
            {
                int index = GetItemIndex(change.Id, orderedItems);
                if (index > -1)
                    orderedItems[index] = new KeyValuePair<string, string>(change.Id, change.FileName);
            }
        }

        internal static void SaveOrderedCollection(List<KeyValuePair<string, string>> orderedItems, IFileInfo orderFile)
        {
            var modelItemOrder = new ModelItemOrder();
            modelItemOrder.AddRange(orderedItems.Select(item => new ModelItemOrderInfo
            {
                FileName = item.Value,
                Id = new Guid(item.Key)
            }));

            XmlSerializer ser = new XmlSerializer(typeof(ModelItemOrder));
            using (Stream targetStream = orderFile.Open(FileMode.Create))
            {
                ser.Serialize(targetStream, modelItemOrder);
            }
        }

        internal static int CalculateNewItemIndex(CollectionDiff change, List<KeyValuePair<string, string>> orderedItems)
        {
            int nowAfterIndex = GetItemIndex(change.NowAfterId, orderedItems);
            int nowBeforeIndex = GetItemIndex(change.NowBeforeId, orderedItems);

            if (nowAfterIndex > -1)
                return nowAfterIndex + 1;
            if (nowBeforeIndex > -1)
                return nowBeforeIndex;

            return orderedItems.Count;
        }

        internal static int GetItemIndex(string id, List<KeyValuePair<string, string>> orderedItems)
        {
            if (string.IsNullOrEmpty(id))
                return -1;

            var matches = orderedItems
                .Select((item, index) => new { Id = item.Key, Position = index })
                .Where(item => item.Id == id);

            return matches.Any() ? matches.First().Position : -1;
        }

        private static IEnumerable<CollectionDiff> CreateDiffsFromAddedItems(IEnumerable<KeyValuePair<string, string>> addedItems,
            List<KeyValuePair<string, string>> currentOrderedItems)
        {
            return addedItems.Select(item => new CollectionDiff
            {
                DiffKind = CollectionDiffKind.Add,
                Id = item.Key,
                FileName = item.Value,
                NowBeforeId = GetIdOfItemAfter(item, currentOrderedItems),
                NowAfterId = GetIdOfItemBefore(item, currentOrderedItems)
            });

        }

        internal static string GetIdOfItemBefore(KeyValuePair<string, string> item, List<KeyValuePair<string, string>> items)
        {
            int addedIndex = GetItemIndex(item.Key, items);
            return (addedIndex > 0) ? items[addedIndex - 1].Key : null;
        }

        internal static string GetIdOfItemAfter(KeyValuePair<string, string> item, List<KeyValuePair<string, string>> items)
        {
            int addedIndex = GetItemIndex(item.Key, items);
            if (addedIndex == -1)
                return null;

            return (addedIndex < items.Count - 1) ? items[addedIndex + 1].Key : null;
        }

        private class KeyValuePairComparer : IEqualityComparer<KeyValuePair<string, string>>
        {
            public bool Equals(KeyValuePair<string, string> x, KeyValuePair<string, string> y)
            {
                return x.Key.Equals(y.Key);
            }

            public int GetHashCode(KeyValuePair<string, string> item)
            {
                return item.Key.GetHashCode();
            }
        }        
    }

    public enum CollectionDiffKind
    {
        Add,
        Remove,
        FileNameChange
    }

    public class CollectionDiff
    {
        public CollectionDiffKind DiffKind { get; set; }
        public string Id { get; set; }
        public string FileName { get; set; }
        public string NowBeforeId { get; set; }
        public string NowAfterId { get; set; }
    }
}
