using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Sage.Platform.Upgrade.Test
{
    [TestFixture]
    class OrderedCollectionDiffMergeTest
    {
        [Test]
        public void GetItemIndex_WithMatchingId_ReturnsItemIndex()
        {
            string id = "B";
            var items = new List<KeyValuePair<string, string>>
                            {
                                new KeyValuePair<string, string>("A", ""),
                                new KeyValuePair<string, string>("B", "")
                            };

            int index = OrderedCollectionDiffMerge.GetItemIndex(id, items);
            Assert.AreEqual(1, index);
        }

        [Test]
        public void GetItemIndex_WithNoMatchingId_ReturnsNegativeOne()
        {
            string id = "NoMatch";
            var items = new List<KeyValuePair<string, string>>
                            {
                                new KeyValuePair<string, string>("A", ""),
                                new KeyValuePair<string, string>("B", "")
                            };

            int index = OrderedCollectionDiffMerge.GetItemIndex(id, items);
            Assert.AreEqual(-1, index);
        }

        [Test]
        public void GetIdOfItemBefore_WithNoMatchingItem_ReturnsNull()
        {
            var item = new KeyValuePair<string, string>("NoMatch", "");
            var items = new List<KeyValuePair<string, string>>
                            {
                                new KeyValuePair<string, string>("A", ""),
                                new KeyValuePair<string, string>("B", "")
                            };

            string idItemIsBefore = OrderedCollectionDiffMerge.GetIdOfItemBefore(item, items);
            Assert.AreEqual(null, idItemIsBefore);
        }

        [Test]
        public void GetIdOfItemBefore_WithMatchOnFirstItem_ReturnsNull()
        {
            var item = new KeyValuePair<string, string>("A", "");
            var items = new List<KeyValuePair<string, string>>
                            {
                                new KeyValuePair<string, string>("A", ""),
                                new KeyValuePair<string, string>("B", "")
                            };

            string idItemIsBefore = OrderedCollectionDiffMerge.GetIdOfItemBefore(item, items);
            Assert.AreEqual(null, idItemIsBefore);
        }

        [Test]
        public void GetIdOfItemBefore_WithMatchAfterFirstItem_ReturnsIdBeforeMatch()
        {
            var item = new KeyValuePair<string, string>("B", "");
            var items = new List<KeyValuePair<string, string>>
                            {
                                new KeyValuePair<string, string>("A", ""),
                                new KeyValuePair<string, string>("B", "")
                            };

            string idItemIsBefore = OrderedCollectionDiffMerge.GetIdOfItemBefore(item, items);
            Assert.AreEqual("A", idItemIsBefore);
        }

        [Test]
        public void GetIdOfItemAfter_WithNoMatchingItem_ReturnsNull()
        {
            var item = new KeyValuePair<string, string>("NoMatch", "");
            var items = new List<KeyValuePair<string, string>>
                            {
                                new KeyValuePair<string, string>("A", ""),
                                new KeyValuePair<string, string>("B", "")
                            };

            string idItemIsAfter = OrderedCollectionDiffMerge.GetIdOfItemAfter(item, items);
            Assert.AreEqual(null, idItemIsAfter);
        }

        [Test]
        public void GetIdOfItemAfter_WithMatchOnLastItem_ReturnsNull()
        {
            var item = new KeyValuePair<string, string>("B", "");
            var items = new List<KeyValuePair<string, string>>
                            {
                                new KeyValuePair<string, string>("A", ""),
                                new KeyValuePair<string, string>("B", "")
                            };

            string idItemIsAfter = OrderedCollectionDiffMerge.GetIdOfItemAfter(item, items);
            Assert.AreEqual(null, idItemIsAfter);
        }

        [Test]
        public void GetIdOfItemAfter_WithMatchBeforeLastItem_ReturnsIdBeforeMatch()
        {
            var item = new KeyValuePair<string, string>("A", "");
            var items = new List<KeyValuePair<string, string>>
                            {
                                new KeyValuePair<string, string>("A", ""),
                                new KeyValuePair<string, string>("B", "")
                            };

            string idItemIsAfter = OrderedCollectionDiffMerge.GetIdOfItemAfter(item, items);
            Assert.AreEqual("B", idItemIsAfter);
        }

        [Test]
        public void CalculateNewItemIndex_WithAfterAndBeforeIdSet_ReturnsIndexOfAfterIdPlusOne()
        {
            var change = new CollectionDiff()
                           {
                               Id = "A",
                               NowAfterId = "B",
                               NowBeforeId = "C"
                           };

            var items = new List<KeyValuePair<string, string>>
                            {
                                new KeyValuePair<string, string>("A", ""),
                                new KeyValuePair<string, string>("B", ""),
                                new KeyValuePair<string, string>("C", "")
                            };
            int newIndex = OrderedCollectionDiffMerge.CalculateNewItemIndex(change, items);
            Assert.AreEqual(2, newIndex);
        }

        [Test]
        public void CalculateNewItemIndex_WithBeforeIdSet_ReturnsIndexOfBeforeId()
        {
            var change = new CollectionDiff()
            {
                Id = "B",
                NowBeforeId = "A"
            };

            var items = new List<KeyValuePair<string, string>>
                            {
                                new KeyValuePair<string, string>("A", ""),
                                new KeyValuePair<string, string>("B", "")
                            };
            int newIndex = OrderedCollectionDiffMerge.CalculateNewItemIndex(change, items);
            Assert.AreEqual(0, newIndex);
        }

        [Test]
        public void CalculateNewItemIndex_WithNoBeforeOrAfterIdFound_ReturnsIndexAtEndOfList()
        {
            var change = new CollectionDiff()
            {
                Id = "C",
                NowBeforeId = "D"
            };

            var items = new List<KeyValuePair<string, string>>
                            {
                                new KeyValuePair<string, string>("A", ""),
                                new KeyValuePair<string, string>("B", "")
                            };
            int newIndex = OrderedCollectionDiffMerge.CalculateNewItemIndex(change, items);
            Assert.AreEqual(2, newIndex);
        }

        [Test]
        public void CompareOrderedItems_WithAddedItem_ReturnsAnAddDiff()
        {
            var previousOrderedItems = new List<KeyValuePair<string, string>>
                                          {
                                              new KeyValuePair<string, string>("A", "3")
                                          };
            var currentOrderedItems = new List<KeyValuePair<string, string>>
                                          {
                                              new KeyValuePair<string, string>("A", "1"),
                                              new KeyValuePair<string, string>("B", "2")
                                          };
            var diffs = OrderedCollectionDiffMerge.CompareOrderedItems(currentOrderedItems, previousOrderedItems);
            Assert.AreEqual(1, diffs.Count());
            Assert.AreEqual(CollectionDiffKind.Add, diffs.First().DiffKind);
            Assert.AreEqual("B", diffs.First().Id);
        }

        [Test]
        public void CompareOrderedItems_WithRemovedItem_ReturnsARemoveDiff()
        {
            var previousOrderedItems = new List<KeyValuePair<string, string>>
                                          {
                                              new KeyValuePair<string, string>("A", "3"),
                                              new KeyValuePair<string, string>("B", "2")
                                          };
            var currentOrderedItems = new List<KeyValuePair<string, string>>
                                          {
                                              new KeyValuePair<string, string>("A", "1")
                                          };
            var diffs = OrderedCollectionDiffMerge.CompareOrderedItems(currentOrderedItems, previousOrderedItems);
            Assert.AreEqual(1, diffs.Count());
            Assert.AreEqual(CollectionDiffKind.Remove, diffs.First().DiffKind);
            Assert.AreEqual("B", diffs.First().Id);
        }

        [Test]
        public void MergeChangeIntoOrderedItems_WithRemoveChange_RemovesTheItem()
        {
            var orderedItems = new List<KeyValuePair<string, string>>
                                    {
                                        new KeyValuePair<string, string>("A", "3"),
                                        new KeyValuePair<string, string>("B", "2")
                                    };
            var change = new CollectionDiff() {DiffKind = CollectionDiffKind.Remove, Id = "A"};
            OrderedCollectionDiffMerge.MergeChangeIntoOrderedItems(change, orderedItems);

            Assert.AreEqual(-1, OrderedCollectionDiffMerge.GetItemIndex("A", orderedItems));
        }

        [Test]
        public void MergeChangeIntoOrderedItems_WithAddChange_AddsTheItem()
        {
            var orderedItems = new List<KeyValuePair<string, string>>
                                    {
                                        new KeyValuePair<string, string>("A", "3"),
                                        new KeyValuePair<string, string>("B", "2")
                                    };
            var change = new CollectionDiff
            {
                DiffKind = CollectionDiffKind.Add,
                Id = "C",
                NowBeforeId = "A"
            };
            OrderedCollectionDiffMerge.MergeChangeIntoOrderedItems(change, orderedItems);

            Assert.AreEqual(0, OrderedCollectionDiffMerge.GetItemIndex("C", orderedItems));
        }

        [Test]
        public void MergeChangeIntoOrderedItems_WithRenameChange_RenamesTheItem()
        {
            var orderedItems = new List<KeyValuePair<string, string>>
                                    {
                                        new KeyValuePair<string, string>("A", "1"),
                                        new KeyValuePair<string, string>("B", "2")
                                    };
            var change = new CollectionDiff
            {
                DiffKind = CollectionDiffKind.FileNameChange,
                Id = "B",
                FileName = "newName"
            };
            OrderedCollectionDiffMerge.MergeChangeIntoOrderedItems(change, orderedItems);

            Assert.AreEqual("newName", orderedItems[1].Value);
        }
            
        [Test]
        public void FindRenamedFiles_WithRenamedFile_ReturnsRenameDiff()
        {
            var previousItems = new List<KeyValuePair<string, string>>
                                    {
                                        new KeyValuePair<string, string>("A", "1"),
                                        new KeyValuePair<string, string>("B", "2")
                                    };
            var currentItems = new List<KeyValuePair<string, string>>
                                    {
                                        new KeyValuePair<string, string>("A", "1"),
                                        new KeyValuePair<string, string>("B", "3")
                                    };

            var changes = OrderedCollectionDiffMerge.FindRenamedFiles(currentItems, previousItems);
            
            Assert.AreEqual(1, changes.Count());
            var change = changes.First();
            Assert.AreEqual(CollectionDiffKind.FileNameChange, change.DiffKind);
            Assert.AreEqual("B", change.Id);
            Assert.AreEqual("3", change.FileName);
        }

        [Test]
        public void Test()
        {
            var previousOrderedItems = new List<KeyValuePair<string, string>>
                {
                new KeyValuePair<string, string>("b160748c-699a-4073-89a9-623df46123e0", ""),
                new KeyValuePair<string, string>("5e54df83-7f33-4633-9984-7c1c1d464d00", ""),
                new KeyValuePair<string, string>("4b5d1b3b-8309-4389-b7ab-07ea47297bc2", ""),
                new KeyValuePair<string, string>("6555ff2c-a630-4a1c-82d3-31171c579bc3", ""),
                new KeyValuePair<string, string>("9aa788f2-65cc-4da3-9f9a-518e60fc46fd", ""),
                new KeyValuePair<string, string>("764cf670-66a7-4554-8c61-9a0254c7fd47", ""),
                new KeyValuePair<string, string>("97b08696-bcb0-4281-a58a-6a580e0009e7", ""),
                new KeyValuePair<string, string>("2a50a0f8-54b0-45ab-8ccd-6636fff5c2a7", ""),
                new KeyValuePair<string, string>("622aa4ce-edfa-48af-be43-e375c5e495b3", ""),
                new KeyValuePair<string, string>("2c851136-59b3-4a90-9de3-2e1122a8eaf6", ""),
                new KeyValuePair<string, string>("baf43b49-9778-4a9f-a554-e3b67b4cd8a9", ""),
                new KeyValuePair<string, string>("5f465038-3ae1-40ba-b44f-63cdfccf8ad1", ""),
                new KeyValuePair<string, string>("d0becec6-e61a-4f23-8857-46feedfc281a", ""),
                new KeyValuePair<string, string>("f14e4d15-b651-498e-8bc1-703ef7318147", ""),
                new KeyValuePair<string, string>("0cdb1372-f01c-4b1e-9bcf-893b9045601b", ""),
                new KeyValuePair<string, string>("8cc8c4fc-7742-4226-aa29-f2c3512dd843", ""),
                new KeyValuePair<string, string>("3b42df00-68f5-473e-bb16-422df7bd0a47", ""),
                new KeyValuePair<string, string>("26827e58-4396-41a0-8faf-fdbec11fdf92", ""),
                new KeyValuePair<string, string>("785cae3c-3c8c-4eaf-9e51-954329731379", ""),
                new KeyValuePair<string, string>("8e95c0ed-30ac-4d41-b2e1-dfeee4129d97", ""),
                new KeyValuePair<string, string>("c2c50ce3-74a4-4278-b769-8df9427730b1", ""),
                new KeyValuePair<string, string>("fcf9a45f-b655-4971-8c27-653a5e031b43", ""),
                new KeyValuePair<string, string>("1d8330ec-4d32-4822-8a84-bdf0418c4338", ""),
                new KeyValuePair<string, string>("1cd644d9-f207-4ef2-8483-79538ef62daf", ""),
                new KeyValuePair<string, string>("5230cabf-4ac8-4ab7-a856-95971d098bad", ""),
                new KeyValuePair<string, string>("d788f158-3597-4a79-9bcf-f3bf4a3a5f61", ""),
                new KeyValuePair<string, string>("3ce9eba4-5864-4f03-ae0f-4c3a4ee608f1", ""),
                new KeyValuePair<string, string>("263a2c6e-c1b7-47b9-889b-aab761f88a13", ""),
                new KeyValuePair<string, string>("d54cd193-a5f2-467c-a3b1-37ce889d82fd", "")
                };

            var currentOrderedItems = new List<KeyValuePair<string, string>>
                {
                new KeyValuePair<string, string>("b160748c-699a-4073-89a9-623df46123e0", ""),
                new KeyValuePair<string, string>("93c3fff0-543e-4574-8645-4c27d2f1188d", ""),
                new KeyValuePair<string, string>("d63e7e39-4dfc-4e1d-bea5-9dffc0c74543", ""),
                new KeyValuePair<string, string>("1cd644d9-f207-4ef2-8483-79538ef62daf", ""),
                new KeyValuePair<string, string>("5230cabf-4ac8-4ab7-a856-95971d098bad", ""),
                new KeyValuePair<string, string>("d788f158-3597-4a79-9bcf-f3bf4a3a5f61", ""),
                new KeyValuePair<string, string>("3ce9eba4-5864-4f03-ae0f-4c3a4ee608f1", ""),
                new KeyValuePair<string, string>("263a2c6e-c1b7-47b9-889b-aab761f88a13", ""),
                new KeyValuePair<string, string>("d54cd193-a5f2-467c-a3b1-37ce889d82fd", ""),
                new KeyValuePair<string, string>("5e54df83-7f33-4633-9984-7c1c1d464d00", ""),
                new KeyValuePair<string, string>("4b5d1b3b-8309-4389-b7ab-07ea47297bc2", ""),
                new KeyValuePair<string, string>("6555ff2c-a630-4a1c-82d3-31171c579bc3", ""),
                new KeyValuePair<string, string>("9aa788f2-65cc-4da3-9f9a-518e60fc46fd", ""),
                new KeyValuePair<string, string>("764cf670-66a7-4554-8c61-9a0254c7fd47", ""),
                new KeyValuePair<string, string>("97b08696-bcb0-4281-a58a-6a580e0009e7", ""),
                new KeyValuePair<string, string>("2a50a0f8-54b0-45ab-8ccd-6636fff5c2a7", ""),
                new KeyValuePair<string, string>("622aa4ce-edfa-48af-be43-e375c5e495b3", ""),
                new KeyValuePair<string, string>("2c851136-59b3-4a90-9de3-2e1122a8eaf6", ""),
                new KeyValuePair<string, string>("baf43b49-9778-4a9f-a554-e3b67b4cd8a9", ""),
                new KeyValuePair<string, string>("5f465038-3ae1-40ba-b44f-63cdfccf8ad1", ""),
                new KeyValuePair<string, string>("d0becec6-e61a-4f23-8857-46feedfc281a", ""),
                new KeyValuePair<string, string>("f14e4d15-b651-498e-8bc1-703ef7318147", ""),
                new KeyValuePair<string, string>("0cdb1372-f01c-4b1e-9bcf-893b9045601b", ""),
                new KeyValuePair<string, string>("8cc8c4fc-7742-4226-aa29-f2c3512dd843", ""),
                new KeyValuePair<string, string>("3b42df00-68f5-473e-bb16-422df7bd0a47", ""),
                new KeyValuePair<string, string>("26827e58-4396-41a0-8faf-fdbec11fdf92", ""),
                new KeyValuePair<string, string>("785cae3c-3c8c-4eaf-9e51-954329731379", ""),
                new KeyValuePair<string, string>("8e95c0ed-30ac-4d41-b2e1-dfeee4129d97", ""),
                new KeyValuePair<string, string>("c2c50ce3-74a4-4278-b769-8df9427730b1", ""),
                new KeyValuePair<string, string>("fcf9a45f-b655-4971-8c27-653a5e031b43", ""),
                new KeyValuePair<string, string>("1d8330ec-4d32-4822-8a84-bdf0418c4338", ""),
                new KeyValuePair<string, string>("462cb597-5378-47c0-ab61-e2e792a8249a", "")
                };

            IEnumerable<CollectionDiff> changes = OrderedCollectionDiffMerge.CompareOrderedItems(currentOrderedItems, previousOrderedItems);
        }
    }
}
